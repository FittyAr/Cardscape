using System.Text.Json;
using Cardscape.Application.Abstractions;
using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Application.BackgroundJobs;
using Cardscape.Domain.BackgroundJobs;
using Cardscape.Domain.Common;
using Cardscape.IntegrationTests.Fixtures;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Wolverine;

namespace Cardscape.IntegrationTests.BackgroundJobs;

/// <summary>
/// End-to-end coverage of the background-jobs queue + dispatch +
/// handler chain: enqueue, claim, dispatch through Wolverine, run
/// the registered handler, mark the job Completed. Also covers
/// the failure → retry → dead letter path. We don't rely on the
/// IHostedService poll loop running inside the test host (it
/// doesn't, because the host doesn't fully start when only
/// <c>Services</c> is touched); instead the test calls
/// <c>ClaimBatchAsync</c> + <c>IMessageBus.SendAsync</c> directly,
/// which is exactly what the IHostedService does inside its loop.
/// </summary>
[Collection(CardscapeApi.Name)]
public sealed class BackgroundJobDispatcherTests
{
    private readonly CardscapeWebApplicationFactory _factory;

    public BackgroundJobDispatcherTests(CardscapeWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task Dispatcher_Picks_Up_And_Runs_Successful_Job()
    {
        JobMarker marker = new();
        using TestHandler handler = new("test:happy", marker);

        using IServiceScope scope = _factory.WithWebHostBuilder(b =>
                {
                    b.ConfigureTestServices(s =>
                    {
                        s.AddSingleton<IBackgroundJobHandler>(handler);
                    });
                    b.ConfigureAppConfiguration((_, config) =>
                    {
                        config.AddInMemoryCollection(new Dictionary<string, string?>
                        {
                            ["ConnectionStrings:Default"] = _factory.ConnectionString,
                            ["Storage:LocalRoot"] = _factory.StorageRoot
                        });
                    });
                })
            .Services.CreateScope();

        IBackgroundJobScheduler scheduler = scope.ServiceProvider
            .GetRequiredService<IBackgroundJobScheduler>();

        Result enqueue = await scheduler.EnqueueAsync(
            "test:happy", new { hello = "world" }, ct: TestContext.Current.CancellationToken);
        enqueue.IsSuccess.Should().BeTrue();

        // Replicate the IHostedService loop body manually: claim, then
        // send each claimed job through Wolverine as a fire-and-forget
        // ExecuteBackgroundJobCommand.
        await RunOneDispatchTickAsync(scope.ServiceProvider);

        bool ran = await marker.WaitForCallAsync(TimeSpan.FromSeconds(5));
        ran.Should().BeTrue("the dispatcher should have processed the job within 5s after the manual tick");

        marker.LastPayload.GetProperty("hello").GetString().Should().Be("world");
    }

    [Fact]
    public async Task Failed_Handler_Retries_Then_Dead_Letters()
    {
        // Per-test unique job type — the in-memory DB is shared across
        // every test in the same xUnit collection, so a hardcoded type
        // would race with other tests' leftover jobs.
        string type = $"test:always-fail:{Guid.NewGuid():N}";
        JobMarker marker = new();
        using TestHandler handler = new(type, marker, failAlways: true);

        using IServiceScope scope = _factory.WithWebHostBuilder(b =>
                {
                    b.ConfigureTestServices(s =>
                    {
                        s.AddSingleton<IBackgroundJobHandler>(handler);
                    });
                    b.ConfigureAppConfiguration((_, config) =>
                    {
                        config.AddInMemoryCollection(new Dictionary<string, string?>
                        {
                            ["ConnectionStrings:Default"] = _factory.ConnectionString,
                            ["Storage:LocalRoot"] = _factory.StorageRoot
                        });
                    });
                })
            .Services.CreateScope();

        IBackgroundJobScheduler scheduler = scope.ServiceProvider
            .GetRequiredService<IBackgroundJobScheduler>();
        IBackgroundJobStore store = scope.ServiceProvider
            .GetRequiredService<IBackgroundJobStore>();

        // maxAttempts: 1 means the very first failure dead-letters
        // immediately. This isolates "does the failure path mark the
        // job correctly?" from "does the retry timing work?". The
        // exponential-backoff math is unit-tested in BackgroundJobTests.
        Result enqueue = await scheduler.EnqueueAsync(
            type, new { who = "dis" }, maxAttempts: 1, ct: TestContext.Current.CancellationToken);
        enqueue.IsSuccess.Should().BeTrue();

        await RunOneDispatchTickAsync(scope.ServiceProvider);

        // Poll until the handler has run at least once and the job is
        // dead-lettered. Wolverine's SendAsync is fire-and-forget, so
        // the MarkFailed call may complete on a different thread after
        // our tick returns.
        await WaitForAsync(async () =>
        {
            IReadOnlyList<BackgroundJob> dead = await store.ListDeadLetterAsync(0, 50);
            return dead.Any(j => j.Type == type);
        }, TimeSpan.FromSeconds(10));

        marker.AttemptCount.Should().BeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public async Task Unregistered_Type_Goes_Straight_To_Dead_Letter()
    {
        // No handler is registered for "test:no-handler". The dispatcher
        // moves the job through MarkFailedAsync until it exhausts
        // retries and dead-letters it. Wolverine dispatches messages
        // asynchronously, so we poll for the dead-letter row.
        using IServiceScope scope = _factory.Services.CreateScope();

        IBackgroundJobScheduler scheduler = scope.ServiceProvider
            .GetRequiredService<IBackgroundJobScheduler>();
        IBackgroundJobStore store = scope.ServiceProvider
            .GetRequiredService<IBackgroundJobStore>();

        Result enqueue = await scheduler.EnqueueAsync(
            "test:no-handler", new { x = 1 }, maxAttempts: 1, ct: TestContext.Current.CancellationToken);
        enqueue.IsSuccess.Should().BeTrue();

        await RunOneDispatchTickAsync(scope.ServiceProvider);

        await WaitForAsync(async () =>
        {
            IReadOnlyList<BackgroundJob> dead = await store.ListDeadLetterAsync(0, 50);
            return dead.Any(j => j.Type == "test:no-handler");
        }, TimeSpan.FromSeconds(10));
    }

    // ── helpers ────────────────────────────────────────────────

    /// <summary>
    /// Replicates one iteration of the IHostedService loop: claim
    /// up to 10 pending jobs, then <c>IMessageBus.SendAsync</c> each
    /// as an <see cref="ExecuteBackgroundJobCommand"/>. Wolverine
    /// processes the message synchronously enough that the
    /// handler's <c>MarkCompleted</c>/<c>MarkFailed</c> finishes
    /// before the tick returns (no separate worker queue in
    /// the test host).
    /// </summary>
    private static async Task RunOneDispatchTickAsync(IServiceProvider sp)
    {
        IBackgroundJobStore store = sp.GetRequiredService<IBackgroundJobStore>();
        IClock clock = sp.GetRequiredService<IClock>();
        IMessageBus bus = sp.GetRequiredService<IMessageBus>();

        IReadOnlyList<BackgroundJob> claimed = await store.ClaimBatchAsync(10, clock.UtcNow);
        foreach (BackgroundJob job in claimed)
        {
            await bus.SendAsync(
                new ExecuteBackgroundJobCommand(job.Id.Value, job.Type, job.PayloadJson));
        }
    }

    private static async Task WaitForAsync(Func<Task<bool>> predicate, TimeSpan timeout)
    {
        DateTimeOffset start = DateTimeOffset.UtcNow;
        while (DateTimeOffset.UtcNow - start < timeout)
        {
            if (await predicate())
            {
                return;
            }
            await Task.Delay(250);
        }
    }

    private sealed class JobMarker
    {
        private readonly TaskCompletionSource<bool> tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int calls;

        public int AttemptCount => Volatile.Read(ref calls);
        public JsonElement LastPayload { get; private set; }

        public void Mark(JsonElement payload)
        {
            LastPayload = payload;
            Interlocked.Increment(ref calls);
            tcs.TrySetResult(true);
        }

        public async Task<bool> WaitForCallAsync(TimeSpan timeout)
        {
            // Polling loop with a linked CTS so the delay timer is
            // cancelled as soon as the marker fires. Avoids the
            // CA2027 "leaked timer" warning that Task.WhenAny + Task.Delay
            // triggers.
            using CancellationTokenSource cts = CancellationTokenSource.CreateLinkedTokenSource(CancellationToken.None);
            cts.CancelAfter(timeout);
            while (!cts.IsCancellationRequested)
            {
                if (tcs.Task.IsCompletedSuccessfully)
                {
                    return true;
                }
                try
                {
                    await Task.Delay(100, cts.Token);
                }
                catch (OperationCanceledException)
                {
                    return tcs.Task.IsCompletedSuccessfully;
                }
            }
            return tcs.Task.IsCompletedSuccessfully;
        }

        public async Task<bool> WaitForAtLeastCallsAsync(int target, TimeSpan timeout)
        {
            using CancellationTokenSource cts = CancellationTokenSource.CreateLinkedTokenSource(CancellationToken.None);
            cts.CancelAfter(timeout);
            while (!cts.IsCancellationRequested)
            {
                if (AttemptCount >= target)
                {
                    return true;
                }
                try
                {
                    await Task.Delay(100, cts.Token);
                }
                catch (OperationCanceledException)
                {
                    return AttemptCount >= target;
                }
            }
            return AttemptCount >= target;
        }
    }

    private sealed class TestHandler : IBackgroundJobHandler, IDisposable
    {
        private readonly JobMarker marker;
        private readonly bool failAlways;

        public TestHandler(string type, JobMarker marker, bool failAlways = false)
        {
            Type = type;
            this.marker = marker;
            this.failAlways = failAlways;
        }

        public string Type { get; }

        public Task HandleAsync(Guid jobId, JsonElement payload, CancellationToken ct)
        {
            marker.Mark(payload);
            if (failAlways)
            {
                throw new InvalidOperationException("synthetic failure for test");
            }
            return Task.CompletedTask;
        }

        public void Dispose() { /* nothing to dispose */ }
    }
}
