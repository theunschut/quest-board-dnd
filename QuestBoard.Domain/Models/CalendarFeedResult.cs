using QuestBoard.Domain.Enums;

namespace QuestBoard.Domain.Models;

public class CalendarFeedResult
{
    public CalendarFeedStatus Status { get; set; }

    public string? Body { get; set; }

    // Exists so the controller can log which subscription was served without ever holding
    // the address itself.
    public int? SubscriptionId { get; set; }

    // A strong entity tag derived from Body's UTF-8 bytes, quoted as HTTP requires. Present
    // only for a live (Ok) result. It changes when and only when the emitted document changes,
    // so an event edit produces a new tag automatically with no modified-timestamp column.
    public string? ETag { get; set; }
}
