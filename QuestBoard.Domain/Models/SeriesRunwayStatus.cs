namespace QuestBoard.Domain.Models;

// The projection the calendar horizon banner renders; it carries the title so the banner
// copy can name the series it is warning about.
public class SeriesRunwayStatus
{
    public int SeriesId { get; set; }
    public string Title { get; set; } = string.Empty;
    public int UpcomingCount { get; set; }
}
