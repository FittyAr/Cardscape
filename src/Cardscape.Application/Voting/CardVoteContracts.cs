namespace Cardscape.Application.Voting;

/// <summary>DTO for a single vote row.</summary>
public sealed record CardVoteDto(Guid UserId, DateTimeOffset VotedAt);

/// <summary>Current voting state for one card.</summary>
public sealed record CardVoteStateDto(
    Guid CardId,
    int VoteCount,
    bool CurrentUserHasVoted);
