using QuestBoard.Domain.Enums;

namespace QuestBoard.Service.ViewModels.AgendaViewModels;

// One roster member's answer on an agenda row.
public class AgendaRosterEntryViewModel
{
    public int UserId { get; set; }

    public string Name { get; set; } = string.Empty;

    public AvailabilityCellState Cell { get; set; }

    public bool IsViewer { get; set; }
}
