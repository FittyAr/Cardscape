using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Domain.Members;
using Wolverine;

namespace Cardscape.Application.Users.Queries;

/// <summary>
/// Result of a GDPR Art. 15 right-of-access export. The
/// shape is a flat bundle: account data, workspaces,
/// boards, cards, comments, activity, audit log entries
/// (limited to entries where the user is the subject),
/// API tokens, OAuth apps, integration connections.
/// The bundle is the same shape the privacy notice
/// template asks for (the data subject sees what the
/// controller holds).
/// </summary>
public sealed record UserDataExportDto(
    UserExportAccountDto Account,
    IReadOnlyList<UserExportWorkspaceDto> Workspaces,
    IReadOnlyList<UserExportBoardDto> Boards,
    IReadOnlyList<UserExportCardDto> AuthoredCards,
    IReadOnlyList<UserExportCommentDto> AuthoredComments,
    IReadOnlyList<UserExportActivityDto> ActivityFeedEntries,
    IReadOnlyList<UserExportAuditDto> AuditLogEntries,
    IReadOnlyList<UserExportApiTokenDto> ApiTokens,
    IReadOnlyList<UserExportOAuthAppDto> OAuthApps,
    IReadOnlyList<UserExportIntegrationDto> Integrations,
    DateTimeOffset ExportedAt);

public sealed record UserExportAccountDto(
    Guid Id,
    string Email,
    string DisplayName,
    DateTimeOffset CreatedAt,
    DateTimeOffset? LastLoginAt,
    bool IsActive,
    bool IsDeleted,
    bool IsAnonymised,
    bool IsRestricted);

public sealed record UserExportWorkspaceDto(
    Guid Id,
    string Name,
    string Role,
    DateTimeOffset JoinedAt);

public sealed record UserExportBoardDto(
    Guid Id,
    string Name,
    string Role,
    Guid WorkspaceId,
    DateTimeOffset CreatedAt);

public sealed record UserExportCardDto(
    Guid Id,
    string Title,
    string? Description,
    Guid ListId,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);

public sealed record UserExportCommentDto(
    Guid Id,
    string Body,
    Guid CardId,
    DateTimeOffset CreatedAt);

public sealed record UserExportActivityDto(
    Guid Id,
    string ActionType,
    Guid TargetEntityId,
    DateTimeOffset OccurredAt);

public sealed record UserExportAuditDto(
    Guid Id,
    string EventType,
    string? IpAddress,
    string? UserAgent,
    DateTimeOffset OccurredAt);

public sealed record UserExportApiTokenDto(
    Guid Id,
    string Name,
    string Prefix,
    DateTimeOffset CreatedAt,
    DateTimeOffset? LastUsedAt,
    DateTimeOffset? ExpiresAt);

public sealed record UserExportOAuthAppDto(
    Guid Id,
    string Name,
    string ClientId,
    string SecretPrefix,
    IReadOnlyList<string> AllowedScopes,
    DateTimeOffset CreatedAt,
    bool IsRevoked);

public sealed record UserExportIntegrationDto(
    string Provider,
    string? ExternalId,
    bool Active,
    DateTimeOffset? ConnectedAt);

/// <summary>Returns the right-of-access export bundle for a user.</summary>
public sealed record GetUserDataExportQuery(Guid UserId);

public static class GetUserDataExportQueryHandler
{
    public static async Task<UserDataExportDto?> Handle(
        GetUserDataExportQuery query,
        IUserDataExportService export,
        CancellationToken cancellation)
    {
        return await export.BuildExportAsync(new UserId(query.UserId), cancellation);
    }
}
