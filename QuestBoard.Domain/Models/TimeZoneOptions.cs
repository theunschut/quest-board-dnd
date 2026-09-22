namespace QuestBoard.Domain.Models;

// Code default, overridable through configuration, so no deployment environment file has to
// change for the board's own wall clock to resolve correctly.
public class TimeZoneOptions
{
    public const string SectionName = "TimeZone";

    public string BoardTimeZoneId { get; set; } = "Europe/Amsterdam";

    // Only checks that a value is present -- it deliberately does not call
    // TimeZoneInfo.FindSystemTimeZoneById, because that call throws on a bad id and this
    // predicate runs during ValidateOnStart(). A typo here should degrade the board clock at
    // runtime (see BoardClock), not fail the whole application's boot.
    public bool IsValid() => !string.IsNullOrWhiteSpace(BoardTimeZoneId);
}
