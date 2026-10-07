namespace Cardscape.Application.Voting;

/// <summary>Current voting state for one card.</summary>
public sealed record CardVoteStateDto(
    Guid CardId,
    int VoteCount,
    bool CurrentUserHasVoted);
