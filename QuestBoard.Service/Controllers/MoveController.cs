using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QuestBoard.Domain.Enums;
using QuestBoard.Domain.Interfaces;

namespace QuestBoard.Service.Controllers;

[Authorize(Policy = "SuperAdminOnly")]
public class MoveController(IQuestMoveService questMoveService, IUserService userService) : Controller
{
    [HttpPost, ActionName("Quest"), ValidateAntiForgeryToken]
    public async Task<IActionResult> ConfirmQuest(int id, int targetBoardId, CancellationToken token = default)
    {
        var actor = await userService.GetUserAsync(User);
        var outcome = await questMoveService.MoveAsync(id, targetBoardId, actor.Id, token);

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
                TempData["Success"] = message;
                return RedirectToAction("Index", "Quest");

            case MoveStatus.NotFound:
                return NotFound();

            default:
                TempData["Error"] = outcome.Status switch
                {
                    MoveStatus.NoLinkedBoards => "This board is not linked to any other board, so there is nowhere to move this.",
                    MoveStatus.TargetNotLinked => "That board is not linked to this one, so nothing was moved.",
                    MoveStatus.DungeonMasterNotEligible => $"{outcome.DmName} is not a Dungeon Master or Admin on {outcome.Target?.Name}, so the quest was not moved.",
                    _ => "The move did not complete and nothing was changed. Try again."
                };
                return RedirectToAction("Details", "Quest", new { id });
        }
    }
}
