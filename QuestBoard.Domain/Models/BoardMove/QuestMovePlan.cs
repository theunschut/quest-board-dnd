namespace QuestBoard.Domain.Models.BoardMove;

/// <summary>
/// Everything the store needs to move one quest in a single save: which signups stay, which
/// are removed, which waitlisted players are promoted, and which chain links are cut.
/// </summary>
public record QuestMovePlan(
    int QuestId,
    int SourceGroupId,
    int TargetGroupId,
    IReadOnlyList<int> KeptSignupIds,
    IReadOnlyList<int> DroppedSignupIds,
    IReadOnlyList<int> PromotedSignupIds,
    bool ClearOriginalLink,
    IReadOnlyList<int> FollowUpQuestIdsToUnlink);
