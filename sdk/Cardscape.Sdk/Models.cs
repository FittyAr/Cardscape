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

/// <summary>Categories emitted by the activity timeline.</summary>
public enum ActivityKind
{
    /// <summary>A board was created.</summary>
    BoardCreated = 0,
    /// <summary>A board was renamed.</summary>
    BoardRenamed = 1,
    /// <summary>A board was archived.</summary>
    BoardArchived = 2,
    /// <summary>A board was restored.</summary>
    BoardUnarchived = 3,
    /// <summary>A list was created.</summary>
    ListCreated = 4,
    /// <summary>A list was renamed.</summary>
    ListRenamed = 5,
    /// <summary>A list was moved.</summary>
    ListMoved = 6,
    /// <summary>A list was archived.</summary>
    ListArchived = 7,
    /// <summary>A card was created.</summary>
    CardCreated = 8,
    /// <summary>A card was renamed.</summary>
    CardRenamed = 9,
    /// <summary>A card was moved.</summary>
    CardMoved = 10,
    /// <summary>A card was archived.</summary>
    CardArchived = 11,
    /// <summary>A card was restored.</summary>
    CardRestored = 12,
    /// <summary>A user was assigned to a card.</summary>
    CardAssigned = 13,
    /// <summary>A user was unassigned from a card.</summary>
    CardUnassigned = 14,
    /// <summary>A card due date was set.</summary>
    CardDueDateSet = 15,
    /// <summary>A card due date was cleared.</summary>
    CardDueDateCleared = 16,
    /// <summary>A label was attached.</summary>
    LabelAdded = 17,
    /// <summary>A label was detached.</summary>
    LabelRemoved = 18,
    /// <summary>A comment was added.</summary>
    CommentAdded = 19,
    /// <summary>A checklist was created.</summary>
    ChecklistCreated = 20,
    /// <summary>A checklist item was completed.</summary>
    ChecklistItemCompleted = 21,
    /// <summary>A checklist item was reopened.</summary>
    ChecklistItemUncompleted = 22,
    /// <summary>An attachment was added.</summary>
    AttachmentAdded = 23,
    /// <summary>An attachment was removed.</summary>
    AttachmentRemoved = 24
}

// ── Workspaces ─────────────────────────────────────────
/// <summary>Represents a workspace returned by the Cardscape API.</summary>
/// <param name="Id">The workspace identifier.</param>
/// <param name="Name">The display name.</param>
/// <param name="OwnerId">The identifier of the owning user.</param>
/// <param name="Region">The data-hosting region.</param>
/// <param name="IsArchived"><see langword="true"/> when the workspace is archived; otherwise, <see langword="false"/>.</param>
/// <param name="RequireTwoFactor"><see langword="true"/> when workspace members must use two-factor authentication.</param>
/// <param name="CreatedAt">The creation timestamp in UTC.</param>
/// <param name="MemberCount">The number of workspace members.</param>
public sealed record WorkspaceDto(
    [property: JsonPropertyName("id")] Guid Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("ownerId")] Guid OwnerId,
    [property: JsonPropertyName("region")] Region Region,
    [property: JsonPropertyName("isArchived")] bool IsArchived,
    [property: JsonPropertyName("requireTwoFactor")] bool RequireTwoFactor,
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
/// <param name="IsArchived"><see langword="true"/> when the list is archived; otherwise, <see langword="false"/>.</param>
/// <param name="CreatedAt">The creation timestamp in UTC.</param>
/// <param name="CardCount">The number of cards in the list.</param>
public sealed record BoardListDto(
    [property: JsonPropertyName("id")] Guid Id,
    [property: JsonPropertyName("boardId")] Guid BoardId,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("position")] double Position,
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
/// <param name="CoverColor">The optional card cover color.</param>
/// <param name="MemberCount">The assigned member count.</param>
/// <param name="LabelCount">The attached label count.</param>
/// <param name="CommentCount">The comment count.</param>
/// <param name="AttachmentCount">The attachment count.</param>
/// <param name="ChecklistCount">The checklist count.</param>
/// <param name="IsSnoozed"><see langword="true"/> when the card is currently snoozed.</param>
/// <param name="SnoozeUntil">The optional snooze expiration.</param>
/// <param name="MirrorOfCardId">The source card identifier when this card is a mirror.</param>
public sealed record CardDto(
    [property: JsonPropertyName("id")] Guid Id,
    [property: JsonPropertyName("listId")] Guid ListId,
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("description")] string Description,
    [property: JsonPropertyName("position")] double Position,
    [property: JsonPropertyName("dueDate")] DateTimeOffset? DueDate,
    [property: JsonPropertyName("isCompleted")] bool IsCompleted,
    [property: JsonPropertyName("isArchived")] bool IsArchived,
    [property: JsonPropertyName("createdAt")] DateTimeOffset CreatedAt,
    [property: JsonPropertyName("coverColor")] string? CoverColor,
    [property: JsonPropertyName("memberCount")] int MemberCount,
    [property: JsonPropertyName("labelCount")] int LabelCount,
    [property: JsonPropertyName("commentCount")] int CommentCount,
    [property: JsonPropertyName("attachmentCount")] int AttachmentCount,
    [property: JsonPropertyName("checklistCount")] int ChecklistCount,
    [property: JsonPropertyName("isSnoozed")] bool IsSnoozed,
    [property: JsonPropertyName("snoozeUntil")] DateTimeOffset? SnoozeUntil,
    [property: JsonPropertyName("mirrorOfCardId")] Guid? MirrorOfCardId);

/// <summary>Represents the compact card projection returned by board lists.</summary>
public sealed record CardSummaryDto(
    [property: JsonPropertyName("id")] Guid Id,
    [property: JsonPropertyName("listId")] Guid ListId,
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("position")] double Position,
    [property: JsonPropertyName("dueDate")] DateTimeOffset? DueDate,
    [property: JsonPropertyName("isCompleted")] bool IsCompleted,
    [property: JsonPropertyName("updatedAt")] DateTimeOffset UpdatedAt,
    [property: JsonPropertyName("isSnoozed")] bool IsSnoozed,
    [property: JsonPropertyName("snoozeUntil")] DateTimeOffset? SnoozeUntil,
    [property: JsonPropertyName("mirrorOfCardId")] Guid? MirrorOfCardId);

/// <summary>Provides the values required to create a card.</summary>
/// <param name="ListId">The list that will contain the card.</param>
/// <param name="Title">The card title.</param>
/// <param name="Description">The optional card description.</param>
public sealed record CreateCardRequest(
    [property: JsonPropertyName("listId")] Guid ListId,
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("description")] string? Description = null);

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
/// <param name="AuthorDisplayName">The current display name of the author, when available.</param>
/// <param name="Body">The comment content.</param>
/// <param name="CreatedAt">The creation timestamp in UTC.</param>
/// <param name="UpdatedAt">The last edit timestamp, when edited.</param>
public sealed record CommentDto(
    [property: JsonPropertyName("id")] Guid Id,
    [property: JsonPropertyName("cardId")] Guid CardId,
    [property: JsonPropertyName("authorId")] Guid AuthorId,
    [property: JsonPropertyName("authorDisplayName")] string? AuthorDisplayName,
    [property: JsonPropertyName("body")] string Body,
    [property: JsonPropertyName("createdAt")] DateTimeOffset CreatedAt,
    [property: JsonPropertyName("updatedAt")] DateTimeOffset? UpdatedAt);

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
/// <param name="ActorDisplayName">The current display name of the actor, when available.</param>
/// <param name="Kind">The machine-readable activity category.</param>
/// <param name="PayloadJson">The serialized action metadata.</param>
/// <param name="OccurredAt">The action timestamp in UTC.</param>
public sealed record ActivityDto(
    [property: JsonPropertyName("id")] Guid Id,
    [property: JsonPropertyName("boardId")] Guid BoardId,
    [property: JsonPropertyName("cardId")] Guid? CardId,
    [property: JsonPropertyName("actorId")] Guid ActorId,
    [property: JsonPropertyName("actorDisplayName")] string? ActorDisplayName,
    [property: JsonPropertyName("kind")] ActivityKind Kind,
    [property: JsonPropertyName("payloadJson")] string PayloadJson,
    [property: JsonPropertyName("occurredAt")] DateTimeOffset OccurredAt);

/// <summary>Represents one cursor-based page of activity events.</summary>
public sealed record ActivityPageDto(
    [property: JsonPropertyName("items")] IReadOnlyList<ActivityDto> Items,
    [property: JsonPropertyName("nextCursor")] string? NextCursor);
