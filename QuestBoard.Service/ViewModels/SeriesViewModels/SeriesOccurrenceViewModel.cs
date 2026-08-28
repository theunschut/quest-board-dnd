namespace QuestBoard.Service.ViewModels.SeriesViewModels;

public class SeriesOccurrenceViewModel
{
    public int Id { get; set; }

    public DateOnly Date { get; set; }

    // A null start time means the occurrence runs all day.
    public TimeOnly? StartTime { get; set; }

    public bool IsCancelled { get; set; }

    // Worded rather than left blank, because an empty time slot is indistinguishable from a
    // rendering failure.
    public string TimeLabel => StartTime.HasValue ? StartTime.Value.ToString("HH:mm") : "All day";
}
