using System.Security.Claims;
using System.Text;
using Cardscape.Api.Middleware;
using Cardscape.Application.Abstractions;
using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Tests.Common.Fakes;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Cardscape.UnitTests.Api;

public sealed class IdempotencyMiddlewareConcurrencyTests
{
    [Fact]
    public async Task InvokeAsync_ConcurrentSameRequest_ExecutesPipelineOnceAndReplaysResponse()
    {
        var store = new InMemoryIdempotencyKeyStore();
        var clock = new FakeClock();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var effects = 0;
        var middleware = new IdempotencyMiddleware(
            async context =>
            {
                Interlocked.Increment(ref effects);
                entered.SetResult();
                await release.Task;
                context.Response.StatusCode = StatusCodes.Status201Created;
                await context.Response.WriteAsync(
                    "{\"id\":7}", TestContext.Current.CancellationToken);
            },
            NullLoggerFactory.Instance);
        Guid userId = Guid.NewGuid();
        DefaultHttpContext firstContext = Context(store, clock, userId);
        DefaultHttpContext secondContext = Context(store, clock, userId);

        Task first = middleware.InvokeAsync(firstContext);
        await entered.Task;
        Task second = middleware.InvokeAsync(secondContext);
        release.SetResult();
        await Task.WhenAll(first, second);

        effects.Should().Be(1);
        firstContext.Response.StatusCode.Should().Be(StatusCodes.Status201Created);
        secondContext.Response.StatusCode.Should().Be(StatusCodes.Status201Created);
        secondContext.Response.Headers["Idempotent-Replayed"].ToString().Should().Be("true");
        string firstBody = await BodyAsync(firstContext);
        string secondBody = await BodyAsync(secondContext);
        secondBody.Should().Be(firstBody);
        store.All.Should().ContainSingle().Which.IsPending.Should().BeFalse();
    }

    private static DefaultHttpContext Context(
        IIdempotencyKeyStore store,
        IClock clock,
        Guid userId)
    {
        ServiceProvider services = new ServiceCollection()
            .AddSingleton(store)
            .AddSingleton(clock)
            .BuildServiceProvider();
        var context = new DefaultHttpContext
        {
            RequestServices = services,
            User = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(ClaimTypes.NameIdentifier, userId.ToString())],
                "test"))
        };
        context.Request.Method = HttpMethods.Post;
        context.Request.Path = "/api/cards";
        context.Request.Headers[IdempotencyMiddleware.HeaderName] = "http-concurrent-key";
        context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes("{\"title\":\"A\"}"));
        context.Response.Body = new MemoryStream();
        return context;
    }

    private static async Task<string> BodyAsync(HttpContext context)
    {
        context.Response.Body.Position = 0;
        using var reader = new StreamReader(context.Response.Body, leaveOpen: true);
        return await reader.ReadToEndAsync(TestContext.Current.CancellationToken);
    }
}
