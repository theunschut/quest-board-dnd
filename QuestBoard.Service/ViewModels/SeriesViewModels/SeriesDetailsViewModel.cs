namespace QuestBoard.Service.ViewModels.SeriesViewModels;

public class SeriesDetailsViewModel
{
    public int Id { get; set; }

    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }

    public DateOnly AnchorDate { get; set; }

    // A null start time means every generated occurrence runs all day.
    public TimeOnly? StartTime { get; set; }

    public int IntervalWeeks { get; set; }

    public int WeekDay { get; set; }

    public string CycleMask { get; set; } = string.Empty;

    public DateOnly? EndDate { get; set; }

    // The parsed mask, filled by the controller from the Domain parser so the read-only strip
    // renders from the same rule the generator uses rather than a second parse written here.
    public IList<bool> CyclePositions { get; set; } = [];

    public IList<SeriesOccurrenceViewModel> Occurrences { get; set; } = [];

    // Display flag for the Actions card only; the authorization policy on the write actions is
    // the actual boundary.
    public bool CanManage { get; set; }

    // The removal-impact counts. Filled only when the viewer is a manager -- there is no reason
    // to compute or send them to a player.
    public int PastCount { get; set; }

    public int FutureCount { get; set; }

    public int AnsweredCount { get; set; }

    // A single place that words the cadence so the header, the rule card and any future
    // surface cannot drift.
    public string CadenceLabel => $"Every {IntervalWeeks} week(s) on {AnchorDate.DayOfWeek}s";

    // Same rule as the occurrence row's own TimeLabel.
    public string TimeLabel => StartTime.HasValue ? StartTime.Value.ToString("HH:mm") : "All day";

    public int TotalCount => PastCount + FutureCount;
}
