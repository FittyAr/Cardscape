namespace Cardscape.Web.Shared;

// ── Voting (v0.7.x) ─────────────────────────────────────────
public sealed record CardVoteStateDto(
    Guid CardId,
    int VoteCount,
    bool CurrentUserHasVoted);
