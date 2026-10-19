using QuestBoard.Domain.Enums;

namespace QuestBoard.Service.Helpers;

/// <summary>
/// The sentences users read when an entry cannot be moved: a short one beside the disabled
/// control and a fuller one in the toast after a refused attempt.
/// </summary>
public static class MoveCopy
{
    public static string ControlReason(MoveBlockReason reason) => reason switch
    {
        MoveBlockReason.Closed => "Closed in the Quest Log.",
        MoveBlockReason.Played => "Already played.",
        MoveBlockReason.SeriesOccurrence => "Part of a recurring series. Detach it first.",
        MoveBlockReason.Past => "Already in the past.",
        _ => string.Empty
    };

    public static string ToastReason(MoveBlockReason reason) => reason switch
    {
        MoveBlockReason.Closed => "It is closed in the Quest Log.",
        MoveBlockReason.Played => "It has already been played.",
        MoveBlockReason.SeriesOccurrence => "It is part of a recurring series. Use \"Detach sessions\" on the series page first.",
        MoveBlockReason.Past => "It is in the past.",
        _ => string.Empty
    };
}
