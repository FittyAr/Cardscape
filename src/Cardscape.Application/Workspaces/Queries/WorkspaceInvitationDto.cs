using Cardscape.Domain.Workspaces;

namespace Cardscape.Application.Workspaces.Queries;

public sealed record WorkspaceInvitationDto(
    Guid Id,
    Guid WorkspaceId,
    string WorkspaceName,
    string Email,
    WorkspaceRole Role,
    Guid InvitedBy,
    DateTimeOffset InvitedAt,
    DateTimeOffset ExpiresAt,
    string TokenPrefix);
