using Cardscape.Application.Abstractions;
using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Application.Abstractions.Security;
using Cardscape.Domain.Authentication.Scim;
using Cardscape.Domain.Common;
using Cardscape.Domain.Workspaces;
using Wolverine;

namespace Cardscape.Application.Scim;

public sealed record IssueScimTokenCommand(Guid WorkspaceId, string Name) : IMessage;

public sealed record IssueScimTokenResult(ScimTokenDto Token, string PlaintextToken);

public static class IssueScimTokenCommandHandler
{
    public static async Task<Result<IssueScimTokenResult>> Handle(
        IssueScimTokenCommand command,
        IRepository<Workspace, WorkspaceId> workspaces,
        IScimTokenRepository tokens,
        IUnitOfWork unitOfWork,
        ICurrentUser currentUser,
        IClock clock,
        CancellationToken ct)
    {
        if (currentUser.Id is null)
        {
            return Result.Failure<IssueScimTokenResult>(DomainError.Unauthenticated(
                "auth.required", "Authentication is required."));
        }

        var workspace = await workspaces.GetByIdAsync(new WorkspaceId(command.WorkspaceId), ct);
        if (workspace is null)
        {
            return Result.Failure<IssueScimTokenResult>(DomainError.NotFound(
                "scim.workspace_not_found", $"Workspace {command.WorkspaceId} was not found."));
        }

        if (!workspace.HasMember(currentUser.Id.Value))
        {
            return Result.Failure<IssueScimTokenResult>(DomainError.Forbidden(
                "scim.not_member", "You are not a member of this workspace."));
        }

        var (token, plaintext) = ScimToken.Issue(
            ScimTokenId.New(), new WorkspaceId(command.WorkspaceId), command.Name, clock.UtcNow);

        await tokens.AddAsync(token, ct);
        await unitOfWork.SaveChangesAsync(ct);

        return Result.Success(new IssueScimTokenResult(
            new ScimTokenDto(token.Id.Value, token.WorkspaceId.Value, token.Name,
                token.TokenPrefix, token.CreatedAt, token.LastUsedAt, token.IsRevoked),
            plaintext));
    }
}

public sealed record RevokeScimTokenCommand(Guid TokenId) : IMessage;

public static class RevokeScimTokenCommandHandler
{
    public static async Task<Result> Handle(
        RevokeScimTokenCommand command,
        IScimTokenRepository tokens,
        IUnitOfWork unitOfWork,
        IClock clock,
        CancellationToken ct)
    {
        var token = await tokens.FindByIdAsync(new ScimTokenId(command.TokenId), ct);
        if (token is null)
        {
            return Result.Failure(DomainError.NotFound(
                "scim.token_not_found", $"SCIM token {command.TokenId} was not found."));
        }

        token.Revoke(clock.UtcNow);
        await unitOfWork.SaveChangesAsync(ct);
        return Result.Success();
    }
}

public sealed record ListScimTokensQuery(Guid WorkspaceId) : IMessage;

public static class ListScimTokensQueryHandler
{
    public static async Task<Result<IReadOnlyList<ScimTokenDto>>> Handle(
        ListScimTokensQuery query,
        IScimTokenRepository tokens,
        CancellationToken ct)
    {
        IReadOnlyList<ScimToken> rows = await tokens.ListForWorkspaceAsync(query.WorkspaceId, ct);
        return Result.Success<IReadOnlyList<ScimTokenDto>>(
            rows.Select(x => new ScimTokenDto(
                x.Id.Value, x.WorkspaceId.Value, x.Name, x.TokenPrefix,
                x.CreatedAt, x.LastUsedAt, x.IsRevoked)).ToList());
    }
}

public sealed record ScimTokenDto(
    Guid Id,
    Guid WorkspaceId,
    string Name,
    string TokenPrefix,
    DateTimeOffset CreatedAt,
    DateTimeOffset? LastUsedAt,
    bool IsRevoked);
