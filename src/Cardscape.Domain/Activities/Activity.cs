using System.Text.Json;
using Cardscape.Domain.Boards;
using Cardscape.Domain.Common;

namespace Cardscape.Domain.Activities;

/// <summary>
/// Append-only activity entry for a board. The activity log is
/// populated by handlers of other contexts' domain events.
/// </summary>
public sealed class Activity : Entity<ActivityId>
{
    public const int AgentNameMaxLength = 80;

    private static readonly JsonSerializerOptions PayloadJsonOptions = new(JsonSerializerDefaults.Web);

    public BoardId BoardId { get; private set; } = null!;
    public Guid? CardId { get; private set; }
    public Guid ActorId { get; private set; }

    /// <summary>
    /// The AI agent key the actor used, when the change was made by the
    /// actor's agent rather than by the person (ADR 0015).
    /// </summary>
    public Guid? AgentId { get; private set; }

    /// <summary>Snapshot of the agent's name (its key's label) when <see cref="AgentId"/> is set.</summary>
    public string? AgentName { get; private set; }
    public ActivityKind Kind { get; private set; }
    public string PayloadJson { get; private set; } = "{}";
    public DateTimeOffset OccurredAt { get; private set; }

    private Activity() { }

    private Activity(
        ActivityId id,
        BoardId boardId,
        Guid? cardId,
        Guid actorId,
        ActivityKind kind,
        string payloadJson,
        DateTimeOffset occurredAt)
    {
        Id = id;
        BoardId = boardId;
        CardId = cardId;
        ActorId = actorId;
        Kind = kind;
        PayloadJson = payloadJson;
        OccurredAt = occurredAt;
        CreatedAt = occurredAt;
    }

    public static Activity Create(
        BoardId boardId,
        Guid? cardId,
        Guid actorId,
        ActivityKind kind,
        string payloadJson,
        DateTimeOffset occurredAt) =>
        new(ActivityId.New(), boardId, cardId, actorId, kind, payloadJson ?? "{}", occurredAt);

    /// <summary>Marks this entry as made by the actor's AI agent.</summary>
    public void AttributeToAgent(Guid agentId, string agentName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(agentName);
        AgentId = agentId;
        AgentName = agentName.Length <= AgentNameMaxLength ? agentName : agentName[..AgentNameMaxLength];
    }

    /// <summary>
    /// Records an activity whose payload is serialised from
    /// <paramref name="payload"/> (camelCase JSON; an anonymous object is
    /// fine). Prefer this over <see cref="Create"/>: it keeps the JSON
    /// well-formed regardless of quotes or newlines in user content.
    /// </summary>
    public static Activity Record(
        BoardId boardId,
        Guid? cardId,
        Guid actorId,
        ActivityKind kind,
        DateTimeOffset occurredAt,
        object? payload = null) =>
        Create(
            boardId,
            cardId,
            actorId,
            kind,
            payload is null ? "{}" : JsonSerializer.Serialize(payload, payload.GetType(), PayloadJsonOptions),
            occurredAt);
}
