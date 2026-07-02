using QuestBoard.Domain.Enums;
using QuestBoard.Domain.Extensions;
using QuestBoard.Domain.Interfaces;
using QuestBoard.Service.ViewModels.QuestLogViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace QuestBoard.Service.Controllers.QuestBoard;

[Authorize]
public class QuestLogController(
    IUserService userService,
    IQuestService questService,
    IActiveGroupContext activeGroupContext
    ) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken token = default)
    {
        var completedQuests = await questService.GetCompletedQuestsAsync(token);

        var viewModel = new QuestLogIndexViewModel
        {
            CompletedQuests = completedQuests
        };

        return View(viewModel);
    }

    [HttpGet]
    public async Task<IActionResult> Details(int id, CancellationToken token = default)
    {
        var quest = await questService.GetQuestWithDetailsAsync(id, token);

        if (quest == null)
        {
            return NotFound();
        }

        // Verify this is a completed quest (DM-only sessions are not shown in the quest log)
        if (!quest.IsFinalized || !quest.FinalizedDate.HasValue || quest.FinalizedDate.Value.Date > DateTime.UtcNow.AddDays(-1).Date || quest.DungeonMasterSession)
        {
            return NotFound();
        }

        // Get current user if authenticated
        Domain.Models.User? currentUser = null;
        if (User.Identity?.IsAuthenticated == true)
        {
            var userEntity = await userService.GetUserAsync(User);
            if (userEntity != null)
            {
                currentUser = await userService.GetByIdAsync(userEntity.Id, token);
            }
        }

        // Check if current user can edit recap (DM or admin)
        var isQuestDm = currentUser?.Name == quest.DungeonMaster?.Name;
        var isAdmin = currentUser != null && await userService.GetEffectiveGroupRoleAsync(User, activeGroupContext.RequireActiveGroupId()) == GroupRole.Admin;
        ViewBag.CanEditRecap = isQuestDm || isAdmin;

        var viewModel = new QuestLogDetailsViewModel
        {
            Quest = quest
        };

        return View(viewModel);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = "DungeonMasterOnly")]
    public async Task<IActionResult> UpdateRecap(int id, string recap, CancellationToken token = default)
    {
        var quest = await questService.GetQuestWithDetailsAsync(id, token);

        if (quest == null)
        {
            return NotFound();
        }

        // Verify this is a completed quest
        if (!quest.IsFinalized || !quest.FinalizedDate.HasValue || quest.FinalizedDate.Value.Date > DateTime.UtcNow.AddDays(-1).Date)
        {
            return BadRequest("Cannot update recap for a quest that is not completed.");
        }

        var currentUser = await userService.GetUserAsync(User);
        if (currentUser == null)
        {
            return Challenge();
        }

        // Check if current user is the quest's DM or an admin
        var isQuestDm = currentUser.Name.Equals(quest.DungeonMaster?.Name, StringComparison.OrdinalIgnoreCase);
        var isAdmin = await userService.GetEffectiveGroupRoleAsync(User, activeGroupContext.RequireActiveGroupId()) == GroupRole.Admin;

        if (!isQuestDm && !isAdmin)
        {
            return Forbid();
        }

        await questService.UpdateQuestRecapAsync(id, recap, token);

        return RedirectToAction("Details", new { id });
    }
}
