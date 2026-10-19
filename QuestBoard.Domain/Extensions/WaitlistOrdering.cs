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

    /// <summary>
    /// Picks the signups that fill freed seats: unselected players whose vote for the finalized
    /// proposed date is Yes or Maybe, best first, up to <paramref name="seats"/>. It mirrors the
    /// repository's top-candidate rule applied once per freed seat, and a parity test keeps the
    /// two in step. It works on signups already in memory, for callers that can no longer query
    /// the quest's own board.
    /// </summary>
    public static IEnumerable<PlayerSignup> SelectPromotions(this IEnumerable<PlayerSignup> keptSignups, int finalizedProposedDateId, int seats)
    {
        if (seats <= 0)
        {
            return [];
        }

        return keptSignups
            .Where(ps => !ps.IsSelected && ps.Role == SignupRole.Player)
            .Where(ps =>
            {
                var vote = ps.DateVotes.FirstOrDefault(dv => dv.ProposedDateId == finalizedProposedDateId)?.Vote;
                return vote == VoteType.Yes || vote == VoteType.Maybe;
            })
            .OrderWaitlist(finalizedProposedDateId)
            .Take(seats)
            .ToList();
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
