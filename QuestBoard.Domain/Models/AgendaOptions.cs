namespace QuestBoard.Domain.Models;

// Code defaults, overridable through configuration, so no deployment environment file has
// to change for the feature to work.
public class AgendaOptions
{
    public const string SectionName = "Agenda";

    // Lower than the availability overview's page size on purpose: every row here carries a
    // whole roster, so five rows is already a heavier page than ten grid rows.
    public int DefaultTake { get; set; } = 5;

    // The server-side ceiling that stops a client-supplied page size from turning into an
    // unbounded query.
    public int MaxTake { get; set; } = 50;

    public int PageIncrement { get; set; } = 5;

    // All three counts are page sizes; a value below one makes the page unservable, so the
    // application refuses to start rather than failing per request.
    public bool IsValid() => DefaultTake >= 1 && MaxTake >= 1 && PageIncrement >= 1 && DefaultTake <= MaxTake;
}
