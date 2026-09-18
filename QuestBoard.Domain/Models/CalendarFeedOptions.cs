namespace QuestBoard.Domain.Models;

// Code defaults, overridable through configuration, so no deployment environment file has
// to change for the feature to work.
public class CalendarFeedOptions
{
    public const string SectionName = "CalendarFeed";

    // History exists so the calendar can answer "when did we last play?", and a few months is
    // enough for that without every device re-downloading years of it several times a day.
    public int MonthsBack { get; set; } = 3;

    // Roughly a year forward covers any schedule anyone plans, and the file stops growing
    // without bound.
    public int MonthsAhead { get; set; } = 12;

    // The fetch-time timestamp write is the only write this read path performs, so it is
    // rate-limited by time rather than performed on every poll.
    public int LastFetchedThrottleMinutes { get; set; } = 15;

    // A retired address keeps answering "gone" for long enough that any calendar client still
    // holding it has stopped asking, and after that the row is purged so the table does not
    // grow without bound. Once purged the address answers "not found" instead, which is the
    // deliberate end of the retirement guarantee.
    public int RetentionDays { get; set; } = 30;

    // A window with no forward reach or a zero throttle makes the feature unserviceable, so
    // the application refuses to start rather than failing per request.
    public bool IsValid() => MonthsBack >= 0 && MonthsAhead >= 1 && LastFetchedThrottleMinutes >= 1 && RetentionDays >= 1;
}
