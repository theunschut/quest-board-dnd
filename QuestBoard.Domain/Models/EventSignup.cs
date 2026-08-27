using QuestBoard.Domain.Enums;

namespace QuestBoard.Domain.Models;

public class EventSignup : IModel
{
    public int Id { get; set; }

    public int EventId { get; set; }

    public int UserId { get; set; }

    // Display-only; populated from the signup's User navigation via mapping.
    public string UserName { get; set; } = string.Empty;

    public VoteType Availability { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    // Distinguishes an answer a person actually gave from a row an automatic pass created.
    // The timestamp is the storage mechanism for that fact rather than a change log: it is
    // stamped on the click that creates the row just as much as on any later change.
    public bool HasAnswered => UpdatedAt != null;
}
