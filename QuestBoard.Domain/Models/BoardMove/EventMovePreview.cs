namespace QuestBoard.Domain.Models.BoardMove;

/// <summary>
/// An event move preview or outcome, with the event snapshot and the plan it would apply.
/// </summary>
public class EventMovePreview : MovePreview
{
    public Event? Event { get; set; }

    /// <summary>
    /// How many target-board members get an automatic Yes row. Always zero for a One-Shot target.
    /// </summary>
    public int CampaignAddCount { get; set; }

    public EventMovePlan? Plan { get; set; }
}
