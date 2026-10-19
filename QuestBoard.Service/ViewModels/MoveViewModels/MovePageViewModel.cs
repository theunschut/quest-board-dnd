using QuestBoard.Domain.Enums;
using QuestBoard.Domain.Models.BoardMove;

namespace QuestBoard.Service.ViewModels.MoveViewModels;

/// <summary>
/// What the Move page shows: the shared preview, plus whether the page is being shown again
/// because the consequences changed after the viewer last looked.
/// </summary>
public class MovePageViewModel
{
    public required MovePreview Preview { get; set; }

    public bool ChangedSincePreview { get; set; }

    public bool IsQuest => Preview is QuestMovePreview;

    public string KindWord => IsQuest ? "quest" : "event";

    public string KindTitle => IsQuest ? "Quest" : "Event";

    /// <summary>The controller that owns the entry's Details page.</summary>
    public string EntryController => IsQuest ? "Quest" : "Events";

    /// <summary>The Move action name, for both the picker and the confirm form.</summary>
    public string MoveAction => IsQuest ? "Quest" : "Event";

    // A tampered target still gets the picker, so the viewer can choose a board from the list.
    public bool ShowPicker => Preview.LinkedBoards.Count > 1
        || (Preview.LinkedBoards.Count == 1 && Preview.Status == MoveStatus.TargetNotLinked);

    public bool IsReadyLike => Preview.Status is MoveStatus.Ready or MoveStatus.Changed;

    public bool ShowPreview => Preview.Target != null
        && (IsReadyLike || Preview.Status == MoveStatus.DungeonMasterNotEligible);

    public bool ShowConfirm => Preview.Target != null
        && IsReadyLike
        && !string.IsNullOrEmpty(Preview.Stamp);

    public string KeptSentence
    {
        get
        {
            var count = Preview.KeptCount;
            if (IsQuest)
            {
                return count switch
                {
                    0 => "No signups to keep.",
                    1 => "1 signup stays on the quest, with its role, selection and date votes.",
                    _ => $"{count} signups stay on the quest, with their role, selection and date votes."
                };
            }

            return count switch
            {
                0 => "No availability answers to keep.",
                1 => "1 availability answer stays exactly as given.",
                _ => $"{count} availability answers stay exactly as given."
            };
        }
    }

    public string EmailSentence => IsQuest
        ? "The Dungeon Master and every player who stays are emailed that the quest has moved. Every removed player is emailed why. Only confirmed email addresses are mailed."
        : "No emails are sent for an event move.";
}
