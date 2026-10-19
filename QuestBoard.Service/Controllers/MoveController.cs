using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QuestBoard.Domain.Enums;
using QuestBoard.Domain.Interfaces;
using QuestBoard.Service.Constants;
using QuestBoard.Service.Helpers;
using QuestBoard.Service.ViewModels.MoveViewModels;

namespace QuestBoard.Service.Controllers;

[Authorize(Policy = "SuperAdminOnly")]
public class MoveController(IQuestMoveService questMoveService, IUserService userService) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Quest(int id, int? targetBoardId, CancellationToken token = default)
    {
        var actor = await userService.GetUserAsync(User);
        var preview = await questMoveService.PreviewAsync(id, targetBoardId, actor.Id, token);

        switch (preview.Status)
        {
            case MoveStatus.NotFound:
                return NotFound();

            case MoveStatus.Blocked:
                TempData["Error"] = $"This quest cannot be moved. {MoveCopy.ToastReason(preview.BlockReason)}";
                return RedirectToAction("Details", "Quest", new { id });

            case MoveStatus.NoLinkedBoards:
                TempData["Error"] = "This board is not linked to any other board, so there is nowhere to move this.";
                return RedirectToAction("Details", "Quest", new { id });

            default:
                return View("Index", new MovePageViewModel { Preview = preview });
        }
    }

    [HttpPost, ActionName("Quest"), ValidateAntiForgeryToken]
    public async Task<IActionResult> ConfirmQuest(int id, int targetBoardId, string? stamp, CancellationToken token = default)
    {
        var actor = await userService.GetUserAsync(User);
        var outcome = await questMoveService.MoveAsync(id, targetBoardId, stamp ?? string.Empty, actor.Id, token);

        switch (outcome.Status)
        {
            case MoveStatus.Moved:
                var message = $"Moved \"{outcome.EntryTitle}\" to \"{outcome.Target?.Name}\".";
                if (outcome.Dropped.Count > 0)
                {
                    message += outcome.Dropped.Count == 1
                        ? " 1 signup was removed."
                        : $" {outcome.Dropped.Count} signups were removed.";
                }
                if (outcome.Target?.ViewerIsMember == true)
                {
                    TempData[TempDataKeys.MovedMessage] = message;
                    TempData[TempDataKeys.MovedEntryUrl] = Url.Action("Details", "Quest", new { id });
                    TempData[TempDataKeys.MovedTargetName] = outcome.Target.Name;
                }
                else
                {
                    TempData["Success"] = message;
                }
                return RedirectToAction("Index", "Quest");

            case MoveStatus.NotFound:
                return NotFound();

            case MoveStatus.Changed:
                return View("Index", new MovePageViewModel { Preview = outcome, ChangedSincePreview = true });

            default:
                TempData["Error"] = outcome.Status switch
                {
                    MoveStatus.Blocked => $"This quest cannot be moved. {MoveCopy.ToastReason(outcome.BlockReason)}",
                    MoveStatus.NoLinkedBoards => "This board is not linked to any other board, so there is nowhere to move this.",
                    MoveStatus.TargetNotLinked => "That board is not linked to this one, so nothing was moved.",
                    MoveStatus.DungeonMasterNotEligible => $"{outcome.DmName} is not a Dungeon Master or Admin on {outcome.Target?.Name}, so the quest was not moved.",
                    _ => "The move did not complete and nothing was changed. Try again."
                };
                return RedirectToAction("Details", "Quest", new { id });
        }
    }
}
