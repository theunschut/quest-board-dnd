namespace QuestBoard.Domain.Models;

// Code defaults, overridable through configuration, so no deployment environment file has
// to change for the feature to work.
public class EventsOverviewOptions
{
    public const string SectionName = "EventsOverview";

    // A fixed count of events rather than a date window -- a board running several
    // recurring series holds many future occurrences per series, so a window would let
    // page width be set by data the page does not control.
    public int DefaultTake { get; set; } = 10;

    // The server-side ceiling that stops a client-supplied page size from turning into an
    // unbounded query.
    public int MaxTake { get; set; } = 100;

    public int PageIncrement { get; set; } = 10;
}
