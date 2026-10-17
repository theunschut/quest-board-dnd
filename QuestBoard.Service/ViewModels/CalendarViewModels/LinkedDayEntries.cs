namespace QuestBoard.Service.ViewModels.CalendarViewModels;

/// <summary>
/// The read-only entries other boards in the linked set place on one calendar day. Kept apart
/// from the board's own quests and events so a linked entry can never reach the markup that
/// belongs to the board's own entries.
/// </summary>
public class LinkedDayEntries
{
    public List<LinkedEventOnDay> Events { get; set; } = [];

    public List<LinkedQuestOnDay> Quests { get; set; } = [];

    public int Count => Events.Count + Quests.Count;

    public bool HasEntries => Count > 0;
}
