using QuestBoard.Domain.Enums;
using QuestBoard.Domain.Models.QuestBoard;

namespace QuestBoard.Domain.Extensions;

public static class WaitlistOrdering
{
    /// <summary>
    /// Orders a waitlist by each signup's vote for the finalized proposed date — Yes above
    /// Maybe above No/none — then by the earliest LastVoteChangeTime, falling back to
    /// SignupTime for a signup that has never changed its vote since original signup.
    /// This is a pure sort: the caller is responsible for passing an already-filtered
    /// (non-selected, player-role) collection.
    /// </summary>
    public static IEnumerable<PlayerSignup> OrderWaitlist(this IEnumerable<PlayerSignup> waitlist, int finalizedProposedDateId)
    {
        return waitlist
            .OrderByDescending(ps => VotePriority(ps, finalizedProposedDateId))
            .ThenBy(ps => ps.LastVoteChangeTime ?? ps.SignupTime);
    }

    private static int VotePriority(PlayerSignup ps, int proposedDateId)
    {
        var vote = ps.DateVotes.FirstOrDefault(dv => dv.ProposedDateId == proposedDateId)?.Vote ?? VoteType.No;
        return (int)vote;
    }
}
