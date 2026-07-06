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
    IActiveGroupContext activeGroupContext,
    IGroupService groupService
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

        ViewBag.BoardType = await GetActiveBoardTypeAsync(token);
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

        // Verify this is a completed quest (DM-only sessions are not shown in the quest log),
        // admitting closed campaign quests even though they never set FinalizedDate.
        var isCompletedOneShot = quest.IsFinalized && quest.FinalizedDate.HasValue
            && quest.FinalizedDate.Value.Date <= DateTime.UtcNow.AddDays(-1).Date
            && !quest.DungeonMasterSession;
        if (!isCompletedOneShot && !quest.IsClosed)
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
        var isQuestDm = currentUser != null && currentUser.Id == quest.DungeonMaster?.Id;
        var isAdmin = currentUser != null && await GetEffectiveRoleAsync() == GroupRole.Admin;
        ViewBag.CanEditRecap = isQuestDm || isAdmin;

        var viewModel = new QuestLogDetailsViewModel
        {
            Quest = quest
        };

        ViewBag.BoardType = await GetActiveBoardTypeAsync(token);
        return View(viewModel);
    }

    [HttpGet]
    [Authorize(Policy = "DungeonMasterOnly")]
    public async Task<IActionResult> EditRecap(int id, CancellationToken token = default)
    {
        var quest = await questService.GetQuestWithDetailsAsync(id, token);

        if (quest == null)
        {
            return NotFound();
        }

        // Verify this is a completed quest (DM-only sessions are not shown in the quest log),
        // admitting closed campaign quests even though they never set FinalizedDate.
        var isCompletedOneShot = quest.IsFinalized && quest.FinalizedDate.HasValue
            && quest.FinalizedDate.Value.Date <= DateTime.UtcNow.AddDays(-1).Date
            && !quest.DungeonMasterSession;
        if (!isCompletedOneShot && !quest.IsClosed)
        {
            return NotFound();
        }

        var currentUser = await userService.GetUserAsync(User);
        if (currentUser == null)
        {
            return Challenge();
        }

        // Check if current user is the quest's DM or an admin
        var isQuestDm = currentUser.Id == quest.DungeonMaster?.Id;
        var isAdmin = await GetEffectiveRoleAsync() == GroupRole.Admin;

        if (!isQuestDm && !isAdmin)
        {
            return Forbid();
        }

        return View(new EditRecapViewModel { Id = quest.Id, Recap = quest.Recap, Quest = quest });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = "DungeonMasterOnly")]
    public async Task<IActionResult> EditRecap(int id, string recap, CancellationToken token = default)
    {
        var quest = await questService.GetQuestWithDetailsAsync(id, token);

        if (quest == null)
        {
            return NotFound();
        }

        // Verify this is a completed quest (DM-only sessions are not shown in the quest log),
        // admitting closed campaign quests even though they never set FinalizedDate.
        var isCompletedOneShot = quest.IsFinalized && quest.FinalizedDate.HasValue
            && quest.FinalizedDate.Value.Date <= DateTime.UtcNow.AddDays(-1).Date
            && !quest.DungeonMasterSession;
        if (!isCompletedOneShot && !quest.IsClosed)
        {
            return BadRequest("Cannot update recap for a quest that is not completed.");
        }

        var currentUser = await userService.GetUserAsync(User);
        if (currentUser == null)
        {
            return Challenge();
        }

        // Check if current user is the quest's DM or an admin
        var isQuestDm = currentUser.Id == quest.DungeonMaster?.Id;
        var isAdmin = await GetEffectiveRoleAsync() == GroupRole.Admin;

        if (!isQuestDm && !isAdmin)
        {
            return Forbid();
        }

        await questService.UpdateQuestRecapAsync(id, recap, token);

        return RedirectToAction("Details", new { id });
    }

    // SuperAdmin legitimately has no active group selected (see ActiveGroupContextExtensions'
    // documented contract), so short-circuit to Admin here rather than calling
    // RequireActiveGroupId(), which would throw for a SuperAdmin with no active group.
    private async Task<GroupRole?> GetEffectiveRoleAsync() =>
        User.IsInRole("SuperAdmin")
            ? GroupRole.Admin
            : await userService.GetEffectiveGroupRoleAsync(User, activeGroupContext.RequireActiveGroupId());

    // Resolves the active group's board type server-side, mirroring QuestController's helper.
    // SuperAdmin legitimately has no active group selected, so default to OneShot rather than
    // calling RequireActiveGroupId(), which would throw.
    private async Task<BoardType> GetActiveBoardTypeAsync(CancellationToken token = default)
    {
        if (activeGroupContext.ActiveGroupId is not { } groupId)
        {
            return BoardType.OneShot;
        }

        var group = await groupService.GetByIdAsync(groupId, token);
        return group?.BoardType ?? BoardType.OneShot;
    }
}
