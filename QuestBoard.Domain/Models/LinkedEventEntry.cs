namespace QuestBoard.Domain.Models;

/// <summary>
/// One event on a linked board, reduced to what a shared calendar shows. Cancelled events are
/// kept and flagged so the calendar can strike them through.
/// </summary>
public class LinkedEventEntry
{
    public int EventId { get; set; }

    public int GroupId { get; set; }

    public string Title { get; set; } = string.Empty;

    public DateOnly Date { get; set; }

    public TimeOnly? StartTime { get; set; }

    public bool IsCancelled { get; set; }

    public string BoardName { get; set; } = string.Empty;

    public bool ViewerIsMember { get; set; }
}
