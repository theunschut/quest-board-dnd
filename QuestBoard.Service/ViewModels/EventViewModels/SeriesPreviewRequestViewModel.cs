using System.ComponentModel.DataAnnotations;

namespace QuestBoard.Service.ViewModels.EventViewModels;

// The bound model for the debounced preview POST. It deliberately carries no event id and no
// group id, because the preview computes dates from a rule and touches no stored data at all.
public class SeriesPreviewRequestViewModel
{
    [DataType(DataType.Date)]
    public DateOnly AnchorDate { get; set; }

    [Range(1, 52)]
    public int IntervalWeeks { get; set; } = 1;

    public string CycleMask { get; set; } = string.Empty;

    [DataType(DataType.Date)]
    public DateOnly? EndDate { get; set; }
}
