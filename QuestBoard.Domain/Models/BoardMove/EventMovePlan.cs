namespace QuestBoard.Domain.Models.BoardMove;

/// <summary>
/// Everything the store needs to move one event in a single save: which availability answers
/// stay, which are removed, and which target-board members get an automatic row.
/// </summary>
/// <remarks>
/// <paramref name="SignupBasis"/> records whose answer each row was when the plan was drawn. The
/// store compares it, and the target board's membership, with what it loads at the moment of the
/// write, so a plan is never applied to rows that no longer match it.
/// </remarks>
public record EventMovePlan(
    int EventId,
    int SourceGroupId,
    int TargetGroupId,
    IReadOnlyList<int> KeptSignupIds,
    IReadOnlyList<int> DroppedSignupIds,
    IReadOnlyList<int> CampaignAddUserIds,
    IReadOnlyList<EventSignupBasis> SignupBasis);

/// <summary>
/// Whose answer one event signup row was when a move plan was drawn.
/// </summary>
public record EventSignupBasis(int SignupId, int UserId);
