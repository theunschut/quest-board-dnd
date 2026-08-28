namespace QuestBoard.Domain.Enums;

// Two scopes only; there is deliberately no "all events" scope, because past occurrences
// are the record of sessions that happened.
public enum EventEditScope
{
    OnlyThisEvent = 0,
    ThisAndFutureEvents = 1
}
