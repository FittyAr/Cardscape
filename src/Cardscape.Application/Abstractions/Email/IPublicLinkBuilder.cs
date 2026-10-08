namespace Cardscape.Application.Abstractions.Email;

/// <summary>Builds absolute links to Web client pages for messages that leave the app (emails).</summary>
public interface IPublicLinkBuilder
{
    /// <summary>
    /// Absolute URL for <paramref name="relativePath"/> (e.g. <c>invitations/accept?token=…</c>),
    /// or null when the public address of the instance cannot be determined.
    /// </summary>
    Task<string?> BuildAsync(string relativePath, CancellationToken ct);
}
