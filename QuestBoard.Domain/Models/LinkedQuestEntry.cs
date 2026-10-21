namespace QuestBoard.Domain.Models;

/// <summary>
/// One quest on a linked board, reduced to what a shared calendar shows: its title, who runs it,
/// whether it is a DM-only session, and the dates it is proposed for or fixed on. It carries
/// nothing about who signed up or how anyone voted.
/// </summary>
public class LinkedQuestEntry
{
    public int QuestId { get; set; }

    public int GroupId { get; set; }

    public string Title { get; set; } = string.Empty;

    public string DungeonMasterName { get; set; } = string.Empty;

    public bool DungeonMasterSession { get; set; }

    public bool IsFinalized { get; set; }

    public DateTime? FinalizedDate { get; set; }

    public List<DateTime> ProposedDates { get; set; } = [];

    public string BoardName { get; set; } = string.Empty;

    public bool ViewerIsMember { get; set; }

    /// <summary>
    /// True when the viewer may follow the entry's link: they are a member of the owning board,
    /// and that board still offers the page the link opens.
    /// </summary>
    public bool CanOpen { get; set; }
}
