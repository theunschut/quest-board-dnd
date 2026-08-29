namespace QuestBoard.Domain.Models;

// One column of the availability grid: a member who holds at least one signup row across
// the events being shown.
public class AvailabilityMember
{
    public int UserId { get; init; }

    public string Name { get; init; } = string.Empty;
}
