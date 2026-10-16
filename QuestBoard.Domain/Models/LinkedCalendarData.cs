namespace QuestBoard.Domain.Models;

/// <summary>
/// What the linked boards add to a calendar: the boards themselves and their quests and events
/// for the window being shown. Carries board names only, never the name of the set.
/// </summary>
public class LinkedCalendarData
{
    public IReadOnlyList<LinkedBoard> Boards { get; set; } = [];

    public IReadOnlyList<LinkedQuestEntry> Quests { get; set; } = [];

    public IReadOnlyList<LinkedEventEntry> Events { get; set; } = [];

    public static LinkedCalendarData Empty => new();
}
