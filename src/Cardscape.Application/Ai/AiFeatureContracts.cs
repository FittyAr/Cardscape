using Cardscape.Application.Abstractions;
using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Application.Abstractions.Security;
using Cardscape.Application.Comments;
using Cardscape.Domain.Boards;
using Cardscape.Domain.Cards;
using Cardscape.Domain.Common;
using Cardscape.Domain.Lists;
using Cardscape.Domain.Members;
using Wolverine;

namespace Cardscape.Application.Ai;

/// <summary>
/// AI-powered features exposed to the Web UI and the MCP
/// server. Every command is a thin wrapper over
/// <see cref="IAiService"/>; the heavy lifting (prompt
/// composition, persistence) lives in the handlers.
/// </summary>
public static class AiFeatures
{
    // ── Command / response records ────────────────────────────

    public sealed record GenerateCardDescriptionCommand(
        Guid CardId,
        string? ExtraContext = null) : IMessage;

    public sealed record SummarizeCommentThreadCommand(
        IReadOnlyList<Guid> CommentIds) : IMessage;

    public sealed record GenerateChecklistFromDescriptionCommand(
        Guid CardId) : IMessage;

    public sealed record SuggestCardOwnersCommand(
        Guid CardId,
        int MaxSuggestions = 3) : IMessage;

    public sealed record AiGeneratedText(string Text, string Model);
    public sealed record AiGeneratedChecklist(IReadOnlyList<string> Items, string Model);
    public sealed record AiOwnerSuggestion(Guid UserId, string DisplayName, string Reason);
    public sealed record AiOwnerSuggestions(IReadOnlyList<AiOwnerSuggestion> Suggestions, string Model);
}

// ── Handlers ───────────────────────────────────────────────────
