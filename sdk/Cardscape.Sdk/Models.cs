using System.Text.Json.Serialization;

namespace Cardscape.Sdk;

// ── Enums ──────────────────────────────────────────────
public enum BoardVisibility
{
    Private = 0,
    Workspace = 1,
    Public = 2
}

public enum Region
{
    Unspecified = 0,
    Europe = 1,
    NorthAmerica = 2,
    AsiaPacific = 3,
    SouthAmerica = 4
}

public enum WorkspaceRole
{
    Admin = 0,
    Member = 1,
    Observer = 2
}

// ── Workspaces ─────────────────────────────────────────
public sealed record WorkspaceDto(
    [property: JsonPropertyName("id")] Guid Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("ownerId")] Guid OwnerId,
    [property: JsonPropertyName("region")] Region Region,
    [property: JsonPropertyName("isArchived")] bool IsArchived,
    [property: JsonPropertyName("createdAt")] DateTimeOffset CreatedAt,
    [property: JsonPropertyName("memberCount")] int MemberCount);

public sealed record WorkspaceMemberDto(
    [property: JsonPropertyName("userId")] Guid UserId,
    [property: JsonPropertyName("email")] string Email,
    [property: JsonPropertyName("displayName")] string DisplayName,
    [property: JsonPropertyName("role")] WorkspaceRole Role,
    [property: JsonPropertyName("joinedAt")] DateTimeOffset JoinedAt);

public sealed record CreateWorkspaceRequest(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("region")] Region? Region = null);

public sealed record SetWorkspaceRegionRequest(
    [property: JsonPropertyName("region")] Region Region);

// ── Boards ─────────────────────────────────────────────
public sealed record BoardDto(
    [property: JsonPropertyName("id")] Guid Id,
    [property: JsonPropertyName("workspaceId")] Guid WorkspaceId,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("description")] string? Description,
    [property: JsonPropertyName("visibility")] BoardVisibility Visibility,
    [property: JsonPropertyName("isArchived")] bool IsArchived,
    [property: JsonPropertyName("isStarred")] bool IsStarred,
    [property: JsonPropertyName("createdAt")] DateTimeOffset CreatedAt,
    [property: JsonPropertyName("memberCount")] int MemberCount);

public sealed record BoardSummaryDto(
    [property: JsonPropertyName("id")] Guid Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("visibility")] BoardVisibility Visibility,
    [property: JsonPropertyName("isArchived")] bool IsArchived,
    [property: JsonPropertyName("isStarred")] bool IsStarred,
    [property: JsonPropertyName("createdAt")] DateTimeOffset CreatedAt);

public sealed record CreateBoardRequest(
    [property: JsonPropertyName("workspaceId")] Guid WorkspaceId,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("description")] string? Description = null,
    [property: JsonPropertyName("visibility")] BoardVisibility Visibility = BoardVisibility.Private);

public sealed record RenameBoardRequest(
    [property: JsonPropertyName("name")] string Name);

// ── Lists ──────────────────────────────────────────────
public sealed record BoardListDto(
    [property: JsonPropertyName("id")] Guid Id,
    [property: JsonPropertyName("boardId")] Guid BoardId,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("position")] double Position,
    [property: JsonPropertyName("wipLimit")] int? WipLimit,
    [property: JsonPropertyName("isArchived")] bool IsArchived,
    [property: JsonPropertyName("createdAt")] DateTimeOffset CreatedAt,
    [property: JsonPropertyName("cardCount")] int CardCount);

public sealed record CreateListRequest(
    [property: JsonPropertyName("boardId")] Guid BoardId,
    [property: JsonPropertyName("name")] string Name);

// ── Cards ──────────────────────────────────────────────
public sealed record CardDto(
    [property: JsonPropertyName("id")] Guid Id,
    [property: JsonPropertyName("listId")] Guid ListId,
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("description")] string? Description,
    [property: JsonPropertyName("position")] double Position,
    [property: JsonPropertyName("dueDate")] DateTimeOffset? DueDate,
    [property: JsonPropertyName("isCompleted")] bool IsCompleted,
    [property: JsonPropertyName("isArchived")] bool IsArchived,
    [property: JsonPropertyName("createdAt")] DateTimeOffset CreatedAt);

public sealed record CreateCardRequest(
    [property: JsonPropertyName("listId")] Guid ListId,
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("description")] string? Description = null);

public sealed record UpdateCardRequest(
    [property: JsonPropertyName("title")] string? Title = null,
    [property: JsonPropertyName("description")] string? Description = null,
    [property: JsonPropertyName("dueDate")] DateTimeOffset? DueDate = null);

public sealed record MoveCardRequest(
    [property: JsonPropertyName("listId")] Guid ListId,
    [property: JsonPropertyName("position")] double Position);

// ── Labels ─────────────────────────────────────────────
public sealed record LabelDto(
    [property: JsonPropertyName("id")] Guid Id,
    [property: JsonPropertyName("boardId")] Guid BoardId,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("color")] string Color);

public sealed record CreateLabelRequest(
    [property: JsonPropertyName("boardId")] Guid BoardId,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("color")] string Color);

// ── Comments ───────────────────────────────────────────
public sealed record CommentDto(
    [property: JsonPropertyName("id")] Guid Id,
    [property: JsonPropertyName("cardId")] Guid CardId,
    [property: JsonPropertyName("authorId")] Guid AuthorId,
    [property: JsonPropertyName("body")] string Body,
    [property: JsonPropertyName("createdAt")] DateTimeOffset CreatedAt);

public sealed record AddCommentRequest(
    [property: JsonPropertyName("body")] string Body);

// ── Activities ─────────────────────────────────────────
public sealed record ActivityDto(
    [property: JsonPropertyName("id")] Guid Id,
    [property: JsonPropertyName("boardId")] Guid BoardId,
    [property: JsonPropertyName("cardId")] Guid? CardId,
    [property: JsonPropertyName("actorId")] Guid ActorId,
    [property: JsonPropertyName("verb")] string Verb,
    [property: JsonPropertyName("payload")] string? Payload,
    [property: JsonPropertyName("occurredAt")] DateTimeOffset OccurredAt);
