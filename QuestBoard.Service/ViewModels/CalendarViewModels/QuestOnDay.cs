using System.Globalization;
using QuestBoard.Domain.Models.QuestBoard;

namespace QuestBoard.Service.ViewModels.CalendarViewModels;

public class QuestOnDay
{
    public Quest Quest { get; set; } = null!;
    public ProposedDate ProposedDate { get; set; } = null!;
    public bool IsFinalized { get; set; }

    // The proposed time as plain text for attributes and accessible names, which cannot hold the
    // time element the visible time renders as. It is a wall-clock value, shown as stored, and it
    // formats with the current culture exactly as the expression it replaces did.
    public string TimeLabel => ProposedDate.Date.ToString("HH:mm", CultureInfo.CurrentCulture);
}