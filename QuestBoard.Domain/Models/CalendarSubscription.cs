namespace QuestBoard.Domain.Models;

public class CalendarSubscription : IModel
{
    public int Id { get; set; }

    public int UserId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Token { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? LastFetchedAt { get; set; }

    public DateTime? RevokedAt { get; set; }

    // Get-only so it can never be mapped or bound from a form, following Event.IsCancelled's
    // shape -- the only way to change revocation state is by writing RevokedAt itself.
    public bool IsRevoked => RevokedAt != null;
}
