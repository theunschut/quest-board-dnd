namespace QuestBoard.Domain.Enums;

// The entry identifier is built from this member's name, so two sources sharing a numeric id
// can never collide.
public enum CalendarFeedSource
{
    Event,
    Quest
}
