using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QuestBoard.Domain.Enums;
using QuestBoard.Domain.Interfaces;
using QuestBoard.Domain.Models.BoardMove;
using QuestBoard.Service.Constants;
using QuestBoard.Service.Filters;
using QuestBoard.Service.Helpers;
using QuestBoard.Service.ViewModels.MoveViewModels;

namespace QuestBoard.Service.Controllers;

[Authorize(Policy = "SuperAdminOnly")]
public class MoveController(
    IQuestMoveService questMoveService,
    IEventMoveService eventMoveService,
    IUserService userService) : Controller
{
    [HttpGet]
    [BoardPage(BoardPage.Quests)]
    public async Task<IActionResult> Quest(int id, int? targetBoardId, CancellationToken token = default)
    {
        var actor = await userService.GetUserAsync(User);
        var preview = await questMoveService.PreviewAsync(id, targetBoardId, actor.Id, token);

        switch (preview.Status)
        {
            case MoveStatus.NotFound:
                return NoLongerOnThisBoard("quest", "Quest");

            case MoveStatus.Blocked:
                TempData["Error"] = $"This quest cannot be moved. {MoveCopy.ToastReason(preview.BlockReason)}";
                return RedirectToAction("Details", "Quest", new { id });

            case MoveStatus.NoLinkedBoards:
                TempData["Error"] = "This board is not linked to any other board, so there is nowhere to move this.";
                return RedirectToAction("Details", "Quest", new { id });

            case MoveStatus.TargetPageOff:
                TempData["Error"] = $"Quests aren't used on {preview.Target?.Name}, so nothing can be moved there.";
                return RedirectToAction("Details", "Quest", new { id });

            default:
                return View("Index", new MovePageViewModel { Preview = preview });
        }
    }

    [HttpPost, ActionName("Quest"), ValidateAntiForgeryToken]
    [BoardPage(BoardPage.Quests)]
    public async Task<IActionResult> ConfirmQuest(int id, int targetBoardId, string? stamp, CancellationToken token = default)
    {
        var actor = await userService.GetUserAsync(User);
        var outcome = await questMoveService.MoveAsync(id, targetBoardId, stamp ?? string.Empty, actor.Id, token);

        switch (outcome.Status)
        {
            case MoveStatus.Moved:
                SetMovedToast(outcome, "Quest", id, "signup", "signups");
                return RedirectToAction("Index", "Quest");

            case MoveStatus.NotFound:
                return NoLongerOnThisBoard("quest", "Quest");

            case MoveStatus.Changed:
                return View("Index", new MovePageViewModel { Preview = outcome, ChangedSincePreview = true });

            default:
                TempData["Error"] = outcome.Status switch
                {
                    MoveStatus.Blocked => $"This quest cannot be moved. {MoveCopy.ToastReason(outcome.BlockReason)}",
                    MoveStatus.NoLinkedBoards => "This board is not linked to any other board, so there is nowhere to move this.",
                    MoveStatus.TargetNotLinked => "That board is not linked to this one, so nothing was moved.",
                    MoveStatus.DungeonMasterNotEligible => $"{outcome.DmName} is not a Dungeon Master or Admin on {outcome.Target?.Name}, so the quest was not moved.",
                    MoveStatus.TargetPageOff => $"Quests aren't used on {outcome.Target?.Name}, so nothing was moved.",
                    _ => "The move did not complete and nothing was changed. Try again."
                };
                return RedirectToAction("Details", "Quest", new { id });
        }
    }

    [HttpGet]
    [BoardPage(BoardPage.Events)]
    public async Task<IActionResult> Event(int id, int? targetBoardId, CancellationToken token = default)
    {
        var actor = await userService.GetUserAsync(User);
        var preview = await eventMoveService.PreviewAsync(id, targetBoardId, actor.Id, token);

        switch (preview.Status)
        {
            case MoveStatus.NotFound:
                return NoLongerOnThisBoard("event", "Events");

            case MoveStatus.Blocked:
                TempData["Error"] = $"This event cannot be moved. {MoveCopy.ToastReason(preview.BlockReason)}";
                return RedirectToAction("Details", "Events", new { id });

            case MoveStatus.NoLinkedBoards:
                TempData["Error"] = "This board is not linked to any other board, so there is nowhere to move this.";
                return RedirectToAction("Details", "Events", new { id });

            case MoveStatus.TargetPageOff:
                TempData["Error"] = $"Events aren't used on {preview.Target?.Name}, so nothing can be moved there.";
                return RedirectToAction("Details", "Events", new { id });

            default:
                return View("Index", new MovePageViewModel { Preview = preview });
        }
    }

    [HttpPost, ActionName("Event"), ValidateAntiForgeryToken]
    [BoardPage(BoardPage.Events)]
    public async Task<IActionResult> ConfirmEvent(int id, int targetBoardId, string? stamp, CancellationToken token = default)
    {
        var actor = await userService.GetUserAsync(User);
        var outcome = await eventMoveService.MoveAsync(id, targetBoardId, stamp ?? string.Empty, actor.Id, token);

        switch (outcome.Status)
        {
            case MoveStatus.Moved:
                SetMovedToast(outcome, "Events", id, "answer", "answers");
                return RedirectToAction("Index", "Events");

            case MoveStatus.NotFound:
                return NoLongerOnThisBoard("event", "Events");

            case MoveStatus.Changed:
                return View("Index", new MovePageViewModel { Preview = outcome, ChangedSincePreview = true });

            default:
                TempData["Error"] = outcome.Status switch
                {
                    MoveStatus.Blocked => $"This event cannot be moved. {MoveCopy.ToastReason(outcome.BlockReason)}",
                    MoveStatus.NoLinkedBoards => "This board is not linked to any other board, so there is nowhere to move this.",
                    MoveStatus.TargetNotLinked => "That board is not linked to this one, so nothing was moved.",
                    MoveStatus.TargetPageOff => $"Events aren't used on {outcome.Target?.Name}, so nothing was moved.",
                    _ => "The move did not complete and nothing was changed. Try again."
                };
                return RedirectToAction("Details", "Events", new { id });
        }
    }

    // An entry that is not on the active board reads the same whether it never existed, sits on a
    // board the viewer cannot see, or has just been moved away by this very person (a resubmitted
    // form or the browser's back button). One redirect with one message serves all three, so the
    // reply never says which of them it was.
    private RedirectToActionResult NoLongerOnThisBoard(string kind, string listController)
    {
        TempData["Info"] = $"That {kind} is no longer on this board.";
        return RedirectToAction("Index", listController);
    }

    // A viewer who also belongs to the target board gets a link to the moved entry there; anyone
    // else gets the plain success toast, because they cannot open it from here.
    private void SetMovedToast(MovePreview outcome, string detailsController, int id, string removedSingular, string removedPlural)
    {
        var message = $"Moved \"{outcome.EntryTitle}\" to \"{outcome.Target?.Name}\".";
        if (outcome.Dropped.Count > 0)
        {
            message += outcome.Dropped.Count == 1
                ? $" 1 {removedSingular} was removed."
                : $" {outcome.Dropped.Count} {removedPlural} were removed.";
        }

        if (outcome.Target?.ViewerIsMember == true)
        {
            TempData[TempDataKeys.MovedMessage] = message;
            TempData[TempDataKeys.MovedEntryUrl] = Url.Action("Details", detailsController, new { id });
            TempData[TempDataKeys.MovedTargetName] = outcome.Target.Name;
        }
        else
        {
            TempData["Success"] = message;
        }
    }
}
