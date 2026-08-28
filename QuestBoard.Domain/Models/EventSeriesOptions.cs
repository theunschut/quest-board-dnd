namespace QuestBoard.Domain.Models;

// Code defaults, overridable through configuration, so no deployment environment file has
// to change for the feature to work.
public class EventSeriesOptions
{
    public const string SectionName = "EventSeries";

    public int RunwaySize { get; set; } = 20;

    public int PreviewCount { get; set; } = 10;
}
