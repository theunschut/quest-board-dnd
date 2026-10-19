using QuestBoard.Domain.Enums;

namespace QuestBoard.Domain.Models.BoardMove;

/// <summary>
/// Everything the store needs to move one quest in a single save: which signups stay, which
/// are removed, which waitlisted players are promoted, and which chain links are cut.
/// </summary>
/// <remarks>
/// <paramref name="SignupBasis"/> records what each signup looked like when the plan was drawn,
/// and <paramref name="FinalizedProposedDateId"/> names the date a promotion was ranked by (null
/// when no seat is refilled). The store compares both with what it loads at the moment of the
/// write, so a plan is never applied to signups that have since been selected, revoked, switched
/// role or re-voted.
/// </remarks>
public record QuestMovePlan(
    int QuestId,
    int SourceGroupId,
    int TargetGroupId,
    IReadOnlyList<int> KeptSignupIds,
    IReadOnlyList<int> DroppedSignupIds,
    IReadOnlyList<int> PromotedSignupIds,
    bool ClearOriginalLink,
    IReadOnlyList<int> FollowUpQuestIdsToUnlink,
    IReadOnlyList<QuestSignupBasis> SignupBasis,
    int? FinalizedProposedDateId);

/// <summary>
/// What one quest signup looked like when a move plan was drawn: who it belongs to, whether it
/// holds a seat, its role, and its vote for the finalized date (null when there is no vote or no
/// finalized date).
/// </summary>
public record QuestSignupBasis(
    int SignupId,
    int PlayerId,
    bool IsSelected,
    SignupRole Role,
    VoteType? FinalizedDateVote);
