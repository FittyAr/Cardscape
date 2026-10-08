using System.Collections.Concurrent;
using Cardscape.Application.Abstractions.Email;
using Cardscape.Application.Abstractions.Security;
using Cardscape.Contracts.Settings;
using Cardscape.Domain.Boards;
using Cardscape.Domain.Common;
using Cardscape.Domain.Workspaces;

namespace Cardscape.Tests.Common.Fakes;

/// <summary>
/// <see cref="IEmailSender"/> that records instead of talking SMTP. Set
/// <see cref="Fail"/> to simulate a server that rejects the message.
/// Thread-safe, because password-reset emails are sent off the request.
/// </summary>
public sealed class RecordingEmailSender : IEmailSender
{
    private readonly ConcurrentQueue<OutboundEmail> _sent = new();

    public bool Fail { get; set; }

    public IReadOnlyList<OutboundEmail> Sent => [.. _sent];

    /// <summary>Forgets what was sent so far (e.g. the verification emails of a test's own sign-ups).</summary>
    public void Clear() => _sent.Clear();

    public Task<Result> SendAsync(OutboundEmail email, CancellationToken ct)
    {
        if (Fail)
        {
            return Task.FromResult(Result.Failure(DomainError.External("email.send_failed", "550 mailbox unavailable")));
        }

        _sent.Enqueue(email);
        return Task.FromResult(Result.Success());
    }

    /// <summary>Waits for a message to <paramref name="to"/> (sends may happen in the background).</summary>
    public async Task<OutboundEmail?> WaitForAsync(string to, TimeSpan? timeout = null)
    {
        DateTime deadline = DateTime.UtcNow + (timeout ?? TimeSpan.FromSeconds(5));
        while (DateTime.UtcNow < deadline)
        {
            if (_sent.FirstOrDefault(email => string.Equals(email.To, to, StringComparison.OrdinalIgnoreCase)) is { } found)
            {
                return found;
            }

            await Task.Delay(25);
        }

        return null;
    }
}

/// <summary><see cref="IPublicLinkBuilder"/> with a fixed base address (or none).</summary>
public sealed class FakePublicLinkBuilder(string? baseUrl = "https://boards.example.test") : IPublicLinkBuilder
{
    public Task<string?> BuildAsync(string relativePath, CancellationToken ct) =>
        Task.FromResult(baseUrl is null ? null : $"{baseUrl.TrimEnd('/')}/{relativePath}");
}

/// <summary><see cref="IInvitationService"/> that mints predictable tokens and remembers what it issued.</summary>
public sealed class FakeInvitationService : IInvitationService
{
    public List<(WorkspaceId WorkspaceId, string Email, WorkspaceRole Role, TimeSpan? Lifetime)> Issued { get; } = [];

    public Task<WorkspaceInvitationIssuance> IssueAsync(
        WorkspaceId workspaceId, string email, WorkspaceRole role, Guid invitedBy, TimeSpan? lifetime,
        Guid? boardId, BoardMemberRole? boardRole, CancellationToken ct)
    {
        Issued.Add((workspaceId, email, role, lifetime));
        return Task.FromResult(new WorkspaceInvitationIssuance(
            new WorkspaceInvitationId(Guid.NewGuid()), $"inv_token+{Issued.Count}/x"));
    }

    public Task<Result<WorkspaceInvitationValidation>> ValidateAsync(string cleartextToken, DateTimeOffset now, CancellationToken ct) =>
        throw new NotSupportedException("Only issuance is faked.");
}

/// <summary>Email settings that pass <see cref="EmailSettings.CanSend"/>.</summary>
public static class TestEmailSettings
{
    public static EmailSettings Configured() => new()
    {
        Enabled = true,
        Host = "smtp.example.test",
        Port = 587,
        FromAddress = "boards@example.test",
    };
}
