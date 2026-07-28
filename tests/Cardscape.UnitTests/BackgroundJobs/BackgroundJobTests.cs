using Cardscape.Domain.BackgroundJobs;

namespace Cardscape.UnitTests.BackgroundJobs;

public class BackgroundJobTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 1, 0, 0, 0, TimeSpan.Zero);

    private static BackgroundJob Enqueue(
        string type = "test:hello",
        string payload = "{}",
        DateTimeOffset? scheduledFor = null,
        int maxAttempts = 5) =>
        BackgroundJob.Enqueue(
            type,
            payload,
            scheduledFor ?? Now,
            maxAttempts,
            Now).Value;

    [Fact]
    public void Enqueue_Defaults_To_Pending_With_Zero_Attempts()
    {
        var job = Enqueue();

        job.Status.Should().Be(BackgroundJobStatus.Pending);
        job.Attempts.Should().Be(0);
        job.MaxAttempts.Should().Be(5);
        job.StartedAt.Should().BeNull();
        job.CompletedAt.Should().BeNull();
        job.LastError.Should().BeNull();
    }

    [Fact]
    public void Enqueue_Rejects_Blank_Type()
    {
        var result = BackgroundJob.Enqueue("   ", "{}", Now, 5, Now);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("background_jobs.type_required");
    }

    [Fact]
    public void Enqueue_Rejects_Oversized_Type()
    {
        var result = BackgroundJob.Enqueue(new string('x', 201), "{}", Now, 5, Now);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("background_jobs.type_too_long");
    }

    [Fact]
    public void Enqueue_Rejects_Oversized_Payload()
    {
        string huge = "{\"x\":\"" + new string('a', 16_001) + "\"}";

        var result = BackgroundJob.Enqueue("t", huge, Now, 5, Now);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("background_jobs.payload_too_large");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(101)]
    public void Enqueue_Rejects_MaxAttempts_Out_Of_Range(int max)
    {
        var result = BackgroundJob.Enqueue("t", "{}", Now, max, Now);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("background_jobs.max_attempts_out_of_range");
    }

    [Fact]
    public void Enqueue_Null_PayloadJson_Defaults_To_Empty_Object()
    {
        var job = BackgroundJob.Enqueue("t", null!, Now, 5, Now).Value;

        job.PayloadJson.Should().Be("{}");
    }

    [Fact]
    public void TryClaim_Transitions_To_Running_When_Pending_And_Due()
    {
        var job = Enqueue(scheduledFor: Now.AddMinutes(-1));

        var claimed = job.TryClaim(Now);

        claimed.Should().BeTrue();
        job.Status.Should().Be(BackgroundJobStatus.Running);
        job.Attempts.Should().Be(1);
        job.StartedAt.Should().Be(Now);
    }

    [Fact]
    public void TryClaim_Rejects_Before_ScheduledFor()
    {
        var job = Enqueue(scheduledFor: Now.AddMinutes(5));

        var claimed = job.TryClaim(Now);

        claimed.Should().BeFalse();
        job.Status.Should().Be(BackgroundJobStatus.Pending);
        job.Attempts.Should().Be(0);
    }

    [Fact]
    public void TryClaim_Rejects_When_Not_Pending()
    {
        var job = Enqueue();
        job.TryClaim(Now).Should().BeTrue();
        job.MarkCompleted(Now);

        var claimed = job.TryClaim(Now);

        claimed.Should().BeFalse();
    }

    [Fact]
    public void MarkCompleted_Transitions_To_Completed_And_Clears_LastError()
    {
        BackgroundJob job = Enqueue();
        job.TryClaim(Now);
        // Force a known prior error so we can verify MarkCompleted clears it.
        // We bump MaxAttempts to keep the job Pending on MarkFailed.
        job.MarkFailed("transient", Now);
        job.LastError.Should().Be("transient");

        job.TryClaim(Now);
        job.MarkCompleted(Now);

        job.Status.Should().Be(BackgroundJobStatus.Completed);
        job.CompletedAt.Should().Be(Now);
        job.LastError.Should().BeNull();
    }

    [Fact]
    public void MarkFailed_Returns_To_Pending_With_Backoff_When_Retries_Left()
    {
        var job = Enqueue(maxAttempts: 3);
        job.TryClaim(Now);

        job.MarkFailed("boom", Now);

        job.Status.Should().Be(BackgroundJobStatus.Pending);
        job.Attempts.Should().Be(1);
        job.LastError.Should().Be("boom");
        job.ScheduledFor.Should().BeAfter(Now);
        job.CompletedAt.Should().BeNull();
    }

    [Fact]
    public void MarkFailed_Dead_Letters_When_Retries_Exhausted()
    {
        // maxAttempts: 1 means the very first failure exhausts retries
        // and moves the job straight to DeadLetter. Cleaner than trying
        // to advance the fake clock through the backoff window.
        BackgroundJob job = Enqueue(maxAttempts: 1);
        job.TryClaim(Now);
        job.MarkFailed("boom", Now);

        job.Status.Should().Be(BackgroundJobStatus.DeadLetter);
        job.CompletedAt.Should().Be(Now);
        job.LastError.Should().Be("boom");
    }

    [Fact]
    public void MarkFailed_Retries_Within_MaxAttempts_Stay_Pending_Until_Backoff()
    {
        // Two attempts, both fail. After the first failure the job is
        // back to Pending with a future ScheduledFor; the second
        // MarkFailed can only run once the backoff window passes, so
        // we verify the intermediate state here without simulating
        // the clock.
        BackgroundJob job = Enqueue(maxAttempts: 3);
        job.TryClaim(Now);
        job.MarkFailed("first", Now);

        job.Status.Should().Be(BackgroundJobStatus.Pending);
        job.Attempts.Should().Be(1);
        job.ScheduledFor.Should().BeAfter(Now);
        job.CompletedAt.Should().BeNull();
    }

    [Fact]
    public void MarkFailed_Truncates_Long_Error_Messages()
    {
        BackgroundJob job = Enqueue();
        job.TryClaim(Now);

        string huge = new('x', 5_000);
        job.MarkFailed(huge, Now);

        job.LastError!.Length.Should().Be(4000);
    }

    [Theory]
    [InlineData(1, 5)]
    [InlineData(2, 10)]
    [InlineData(3, 20)]
    [InlineData(4, 40)]
    [InlineData(5, 80)]
    [InlineData(6, 160)]
    [InlineData(7, 300)]
    [InlineData(20, 300)]
    public void ComputeBackoff_Grows_Then_Caps(int attempt, int expectedSeconds)
    {
        BackgroundJob.ComputeBackoff(attempt).TotalSeconds.Should().Be(expectedSeconds);
    }
}
