using QuestBoard.Domain.Enums;
using QuestBoard.Domain.Models;
using QuestBoard.Domain.Models.QuestBoard;

namespace QuestBoard.Domain.Extensions;

/// <summary>
/// Decides whether a quest or an event may be moved to another board.
/// </summary>
/// <remarks>
/// Pure on purpose: it never reads a clock. The caller hands in the board's own today, as
/// <see cref="QuestExtensions.HasBeenPlayed"/> does, so "has this happened yet" is judged
/// against the board's local date rather than the container's UTC date.
/// </remarks>
public static class MoveEligibility
{
    /// <summary>
    /// A closed quest is refused first, then a quest whose game night is already played.
    /// An open quest, or one finalized for today or later, can move.
    /// </summary>
    public static MoveBlockReason ForQuest(Quest quest, DateOnly boardToday)
    {
        if (quest.IsClosed)
        {
            return MoveBlockReason.Closed;
        }

        return quest.HasBeenPlayed(boardToday) ? MoveBlockReason.Played : MoveBlockReason.None;
    }

    /// <summary>
    /// A series occurrence is refused first, then an event dated before the board's today.
    /// Cancellation never blocks a move, and a detached occurrence carries no series id so it
    /// passes.
    /// </summary>
    public static MoveBlockReason ForEvent(Event evt, DateOnly boardToday)
    {
        if (evt.SeriesId != null)
        {
            return MoveBlockReason.SeriesOccurrence;
        }

        return evt.Date < boardToday ? MoveBlockReason.Past : MoveBlockReason.None;
    }
}
