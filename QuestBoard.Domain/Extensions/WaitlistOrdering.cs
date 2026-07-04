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

    /// <summary>
    /// The shared vote-priority/tiebreak rule used to rank waitlist candidates, expressed on
    /// plain values so it can be reused by callers (e.g. Repository entities) that don't have a
    /// domain <see cref="PlayerSignup"/> to hand. Higher vote value sorts first (Yes above Maybe
    /// above No/none); ties break by the earliest ordering timestamp.
    /// </summary>
    public static int VotePriority(int? vote) => vote ?? (int)VoteType.No;
}
