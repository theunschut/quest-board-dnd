namespace QuestBoard.Domain.Models;

// What the series removal confirm reports. The past/future split is the fact that
// distinguishes a cleanup from a loss of history, and AnsweredCount counts only answers
// people actually gave.
public class SeriesRemovalImpact
{
    public int PastCount { get; set; }
    public int FutureCount { get; set; }
    public int AnsweredCount { get; set; }
    public int TotalCount => PastCount + FutureCount;
}
