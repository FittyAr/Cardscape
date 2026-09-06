using System.Text.Json.Serialization;

namespace Cardscape.Sdk;

// ── Enums ──────────────────────────────────────────────
/// <summary>Specifies who can discover and access a board.</summary>
public enum BoardVisibility
{
    /// <summary>Restricts access to explicitly assigned board members.</summary>
    Private = 0,

    /// <summary>Allows members of the owning workspace to discover the board.</summary>
    Workspace = 1,

    /// <summary>Allows the board to be publicly discoverable.</summary>
    Public = 2
}

/// <summary>Specifies the geographic region in which workspace data is hosted.</summary>
public enum Region
{
    /// <summary>Uses the server-defined default region.</summary>
    Unspecified = 0,

    /// <summary>Hosts workspace data in Europe.</summary>
    Europe = 1,

    /// <summary>Hosts workspace data in North America.</summary>
    NorthAmerica = 2,

    /// <summary>Hosts workspace data in the Asia-Pacific region.</summary>
    AsiaPacific = 3,

    /// <summary>Hosts workspace data in South America.</summary>
    SouthAmerica = 4
}

/// <summary>Specifies a member's permissions within a workspace.</summary>
public enum WorkspaceRole
{
    /// <summary>Grants permission to administer the workspace and its membership.</summary>
    Admin = 0,

    /// <summary>Grants permission to create and modify workspace content.</summary>
    Member = 1,

    /// <summary>Grants read-only access to workspace content.</summary>
    Observer = 2
}

// ── Workspaces ─────────────────────────────────────────
/// <summary>Represents a workspace returned by the Cardscape API.</summary>
/// <param name="Id">The workspace identifier.</param>
/// <param name="Name">The display name.</param>
/// <param name="OwnerId">The identifier of the owning user.</param>
/// <param name="Region">The data-hosting region.</param>
/// <param name="IsArchived"><see langword="true"/> when the workspace is archived; otherwise, <see langword="false"/>.</param>
/// <param name="CreatedAt">The creation timestamp in UTC.</param>
/// <param name="MemberCount">The number of workspace members.</param>
public sealed record WorkspaceDto(
    [property: JsonPropertyName("id")] Guid Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("ownerId")] Guid OwnerId,
    [property: JsonPropertyName("region")] Region Region,
    [property: JsonPropertyName("isArchived")] bool IsArchived,
    [property: JsonPropertyName("createdAt")] DateTimeOffset CreatedAt,
    [property: JsonPropertyName("memberCount")] int MemberCount);

/// <summary>Represents a user's membership in a workspace.</summary>
/// <param name="UserId">The user identifier.</param>
/// <param name="Email">The member's email address.</param>
/// <param name="DisplayName">The member's display name.</param>
/// <param name="Role">The permissions assigned within the workspace.</param>
/// <param name="JoinedAt">The membership creation timestamp in UTC.</param>
public sealed record WorkspaceMemberDto(
    [property: JsonPropertyName("userId")] Guid UserId,
    [property: JsonPropertyName("email")] string Email,
    [property: JsonPropertyName("displayName")] string DisplayName,
    [property: JsonPropertyName("role")] WorkspaceRole Role,
    [property: JsonPropertyName("joinedAt")] DateTimeOffset JoinedAt);

/// <summary>Provides the values required to create a workspace.</summary>
/// <param name="Name">The display name.</param>
/// <param name="Region">The data-hosting region, or <see langword="null"/> to use the server-defined default.</param>
public sealed record CreateWorkspaceRequest(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("region")] Region? Region = null);

/// <summary>Provides the region selected for an existing workspace.</summary>
/// <param name="Region">The new data-hosting region.</param>
public sealed record SetWorkspaceRegionRequest(
    [property: JsonPropertyName("region")] Region Region);

// ── Boards ─────────────────────────────────────────────
/// <summary>Represents a detailed board returned by the Cardscape API.</summary>
/// <param name="Id">The board identifier.</param>
/// <param name="WorkspaceId">The owning workspace identifier.</param>
/// <param name="Name">The display name.</param>
/// <param name="Description">The optional board description.</param>
/// <param name="Visibility">The board's access scope.</param>
/// <param name="IsArchived"><see langword="true"/> when the board is archived; otherwise, <see langword="false"/>.</param>
/// <param name="IsStarred"><see langword="true"/> when the current user has starred the board; otherwise, <see langword="false"/>.</param>
/// <param name="CreatedAt">The creation timestamp in UTC.</param>
/// <param name="MemberCount">The number of board members.</param>
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

/// <summary>Represents the compact board projection used in board collections.</summary>
/// <param name="Id">The board identifier.</param>
/// <param name="Name">The display name.</param>
/// <param name="Visibility">The board's access scope.</param>
/// <param name="IsArchived"><see langword="true"/> when the board is archived; otherwise, <see langword="false"/>.</param>
/// <param name="IsStarred"><see langword="true"/> when the current user has starred the board; otherwise, <see langword="false"/>.</param>
/// <param name="CreatedAt">The creation timestamp in UTC.</param>
public sealed record BoardSummaryDto(
    [property: JsonPropertyName("id")] Guid Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("visibility")] BoardVisibility Visibility,
    [property: JsonPropertyName("isArchived")] bool IsArchived,
    [property: JsonPropertyName("isStarred")] bool IsStarred,
    [property: JsonPropertyName("createdAt")] DateTimeOffset CreatedAt);

/// <summary>Provides the values required to create a board.</summary>
/// <param name="WorkspaceId">The workspace that will own the board.</param>
/// <param name="Name">The display name.</param>
/// <param name="Description">The optional board description.</param>
/// <param name="Visibility">The initial access scope. The default is <see cref="BoardVisibility.Private"/>.</param>
public sealed record CreateBoardRequest(
    [property: JsonPropertyName("workspaceId")] Guid WorkspaceId,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("description")] string? Description = null,
    [property: JsonPropertyName("visibility")] BoardVisibility Visibility = BoardVisibility.Private);

/// <summary>Provides a replacement display name for a board.</summary>
/// <param name="Name">The new display name.</param>
public sealed record RenameBoardRequest(
    [property: JsonPropertyName("name")] string Name);

// ── Lists ──────────────────────────────────────────────
/// <summary>Represents a list and its ordering metadata within a board.</summary>
/// <param name="Id">The list identifier.</param>
/// <param name="BoardId">The owning board identifier.</param>
/// <param name="Name">The display name.</param>
/// <param name="Position">The relative ordering value within the board.</param>
/// <param name="WipLimit">The optional maximum number of active cards.</param>
/// <param name="IsArchived"><see langword="true"/> when the list is archived; otherwise, <see langword="false"/>.</param>
/// <param name="CreatedAt">The creation timestamp in UTC.</param>
/// <param name="CardCount">The number of cards in the list.</param>
public sealed record BoardListDto(
    [property: JsonPropertyName("id")] Guid Id,
    [property: JsonPropertyName("boardId")] Guid BoardId,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("position")] double Position,
    [property: JsonPropertyName("wipLimit")] int? WipLimit,
    [property: JsonPropertyName("isArchived")] bool IsArchived,
    [property: JsonPropertyName("createdAt")] DateTimeOffset CreatedAt,
    [property: JsonPropertyName("cardCount")] int CardCount);

/// <summary>Provides the values required to create a list.</summary>
/// <param name="BoardId">The board that will own the list.</param>
/// <param name="Name">The display name.</param>
public sealed record CreateListRequest(
    [property: JsonPropertyName("boardId")] Guid BoardId,
    [property: JsonPropertyName("name")] string Name);

// ── Cards ──────────────────────────────────────────────
/// <summary>Represents a card and its workflow state.</summary>
/// <param name="Id">The card identifier.</param>
/// <param name="ListId">The containing list identifier.</param>
/// <param name="Title">The card title.</param>
/// <param name="Description">The optional card description.</param>
/// <param name="Position">The relative ordering value within the list.</param>
/// <param name="DueDate">The optional due timestamp.</param>
/// <param name="IsCompleted"><see langword="true"/> when the card is completed; otherwise, <see langword="false"/>.</param>
/// <param name="IsArchived"><see langword="true"/> when the card is archived; otherwise, <see langword="false"/>.</param>
/// <param name="CreatedAt">The creation timestamp in UTC.</param>
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

/// <summary>Provides the values required to create a card.</summary>
/// <param name="ListId">The list that will contain the card.</param>
/// <param name="Title">The card title.</param>
/// <param name="Description">The optional card description.</param>
public sealed record CreateCardRequest(
    [property: JsonPropertyName("listId")] Guid ListId,
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("description")] string? Description = null);

/// <summary>Provides optional replacement values for mutable card fields.</summary>
/// <param name="Title">The replacement title, or <see langword="null"/> to leave the current title unchanged.</param>
/// <param name="Description">The replacement description, or <see langword="null"/> to leave the current description unchanged.</param>
/// <param name="DueDate">The replacement due timestamp, or <see langword="null"/> to leave the current due date unchanged.</param>
/// <remarks>This contract cannot distinguish an omitted nullable field from an explicit request to clear that field.</remarks>
public sealed record UpdateCardRequest(
    [property: JsonPropertyName("title")] string? Title = null,
    [property: JsonPropertyName("description")] string? Description = null,
    [property: JsonPropertyName("dueDate")] DateTimeOffset? DueDate = null);

/// <summary>Provides the destination and ordering value used to move a card.</summary>
/// <param name="ListId">The destination list identifier.</param>
/// <param name="Position">The relative ordering value within the destination list.</param>
public sealed record MoveCardRequest(
    [property: JsonPropertyName("listId")] Guid ListId,
    [property: JsonPropertyName("position")] double Position);

// ── Labels ─────────────────────────────────────────────
/// <summary>Represents a board-scoped label returned by the Cardscape API.</summary>
/// <param name="Id">The label identifier.</param>
/// <param name="BoardId">The owning board identifier.</param>
/// <param name="Name">The display name.</param>
/// <param name="Color">The color value accepted by the Cardscape API.</param>
public sealed record LabelDto(
    [property: JsonPropertyName("id")] Guid Id,
    [property: JsonPropertyName("boardId")] Guid BoardId,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("color")] string Color);

/// <summary>Provides the values required to create a board label.</summary>
/// <param name="BoardId">The board that will own the label.</param>
/// <param name="Name">The display name.</param>
/// <param name="Color">The color value accepted by the Cardscape API.</param>
public sealed record CreateLabelRequest(
    [property: JsonPropertyName("boardId")] Guid BoardId,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("color")] string Color);

// ── Comments ───────────────────────────────────────────
/// <summary>Represents a comment attached to a card.</summary>
/// <param name="Id">The comment identifier.</param>
/// <param name="CardId">The commented card identifier.</param>
/// <param name="AuthorId">The authoring user identifier.</param>
/// <param name="Body">The comment content.</param>
/// <param name="CreatedAt">The creation timestamp in UTC.</param>
public sealed record CommentDto(
    [property: JsonPropertyName("id")] Guid Id,
    [property: JsonPropertyName("cardId")] Guid CardId,
    [property: JsonPropertyName("authorId")] Guid AuthorId,
    [property: JsonPropertyName("body")] string Body,
    [property: JsonPropertyName("createdAt")] DateTimeOffset CreatedAt);

/// <summary>Provides the content required to add a comment.</summary>
/// <param name="Body">The comment content.</param>
public sealed record AddCommentRequest(
    [property: JsonPropertyName("body")] string Body);

// ── Activities ─────────────────────────────────────────
/// <summary>Represents an auditable action recorded for a board or card.</summary>
/// <param name="Id">The activity identifier.</param>
/// <param name="BoardId">The associated board identifier.</param>
/// <param name="CardId">The associated card identifier, when the action targets a card.</param>
/// <param name="ActorId">The identifier of the user who performed the action.</param>
/// <param name="Verb">The machine-readable action name.</param>
/// <param name="Payload">The optional serialized action metadata.</param>
/// <param name="OccurredAt">The action timestamp in UTC.</param>
public sealed record ActivityDto(
    [property: JsonPropertyName("id")] Guid Id,
    [property: JsonPropertyName("boardId")] Guid BoardId,
    [property: JsonPropertyName("cardId")] Guid? CardId,
    [property: JsonPropertyName("actorId")] Guid ActorId,
    [property: JsonPropertyName("verb")] string Verb,
    [property: JsonPropertyName("payload")] string? Payload,
    [property: JsonPropertyName("occurredAt")] DateTimeOffset OccurredAt);
