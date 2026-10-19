namespace QuestBoard.Domain.Models.BoardMove;

/// <summary>
/// Everything the store needs to move one event in a single save: which availability answers
/// stay, which are removed, and which target-board members get an automatic row.
/// </summary>
public record EventMovePlan(
    int EventId,
    int SourceGroupId,
    int TargetGroupId,
    IReadOnlyList<int> KeptSignupIds,
    IReadOnlyList<int> DroppedSignupIds,
    IReadOnlyList<int> CampaignAddUserIds);
