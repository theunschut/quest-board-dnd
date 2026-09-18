using QuestBoard.Domain.Enums;

namespace QuestBoard.Domain.Models;

public class CalendarFeedResult
{
    public CalendarFeedStatus Status { get; set; }

    public string? Body { get; set; }

    // Exists so the controller can log which subscription was served without ever holding
    // the address itself.
    public int? SubscriptionId { get; set; }
}
