using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuestBoard.Domain.Enums;
using QuestBoard.Domain.Interfaces;
using QuestBoard.Domain.Models;
using QuestBoard.Service.ViewModels.PlatformViewModels;

namespace QuestBoard.Service.Areas.Platform.Controllers;

[Area("Platform")]
[Authorize(Policy = "SuperAdminOnly")]
public class GroupController(IGroupService groupService, IUserService userService) : Controller
{
    // Groups index
    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var groups = await groupService.GetAllWithMemberCountAsync();
        return View(new GroupListViewModel { Groups = groups });
    }

    // Create group
    [HttpGet]
    public IActionResult Create() => View(new GroupCreateViewModel());

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(GroupCreateViewModel model)
    {
        if (!ModelState.IsValid) return View(model);
        try
        {
            await groupService.AddAsync(new Group { Name = model.Name, BoardType = model.BoardType!.Value });
            TempData["Success"] = "Group created successfully.";
            return RedirectToAction(nameof(Index));
        }
        catch (DbUpdateException ex) when (
            ex.InnerException?.Message.Contains("unique", StringComparison.OrdinalIgnoreCase) == true ||
            ex.InnerException?.Message.Contains("duplicate", StringComparison.OrdinalIgnoreCase) == true)
        {
            ModelState.AddModelError(nameof(model.Name), "A group with that name already exists. Please choose a different name.");
            return View(model);
        }
    }

    // Edit group
    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var group = await groupService.GetByIdAsync(id);
        if (group == null) return RedirectToAction(nameof(Index));
        return View(new GroupEditViewModel { Id = group.Id, Name = group.Name, BoardType = group.BoardType });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(GroupEditViewModel model)
    {
        if (!ModelState.IsValid) return View(model);
        var group = await groupService.GetByIdAsync(model.Id);
        if (group == null) return RedirectToAction(nameof(Index));
        try
        {
            group.Name = model.Name;
            await groupService.UpdateAsync(group);
            TempData["Success"] = "Group name updated.";
            return RedirectToAction(nameof(Index));
        }
        catch (DbUpdateException ex) when (
            ex.InnerException?.Message.Contains("unique", StringComparison.OrdinalIgnoreCase) == true ||
            ex.InnerException?.Message.Contains("duplicate", StringComparison.OrdinalIgnoreCase) == true)
        {
            ModelState.AddModelError(nameof(model.Name), "A group with that name already exists. Please choose a different name.");
            return View(model);
        }
    }

    // Delete group (only empty groups)
    [HttpGet]
    public async Task<IActionResult> Delete(int id)
    {
        var group = await groupService.GetByIdAsync(id);
        if (group == null) return RedirectToAction(nameof(Index));
        var hasMembers = await groupService.HasMembersAsync(id);
        if (hasMembers)
        {
            TempData["Error"] = "Cannot delete a group that has members.";
            return RedirectToAction(nameof(Index));
        }
        return View(group);
    }

    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var group = await groupService.GetByIdAsync(id);
        var hasMembers = await groupService.HasMembersAsync(id);
        if (hasMembers)
        {
            TempData["Error"] = "Cannot delete a group that has members.";
            return RedirectToAction(nameof(Index));
        }
        if (group != null)
        {
            await groupService.RemoveAsync(group);
            TempData["Success"] = "Group deleted.";
        }
        return RedirectToAction(nameof(Index));
    }

    // Members page
    [HttpGet]
    public async Task<IActionResult> Members(int id)
    {
        var group = await groupService.GetByIdAsync(id);
        if (group == null) return RedirectToAction(nameof(Index));
        var members = await groupService.GetMembersAsync(id);
        var allUsers = await userService.GetAllAsync();
        var memberUserIds = members.Select(m => m.UserId).ToHashSet();
        var availableUsers = allUsers.Where(u => !memberUserIds.Contains(u.Id)).ToList();
        return View(new GroupMembersViewModel
        {
            Group = group,
            Members = members,
            AvailableUsers = availableUsers,
            AddMember = new AddMemberViewModel()
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddMember(int id, [Bind(Prefix = "AddMember")] AddMemberViewModel model)
    {
        if (!ModelState.IsValid)
        {
            TempData["Error"] = "Invalid form submission.";
            return RedirectToAction(nameof(Members), new { id });
        }
        try
        {
            await groupService.AddMemberAsync(id, model.UserId, model.Role);
            var user = await userService.GetByIdAsync(model.UserId);
            TempData["Success"] = $"{user?.Name ?? "User"} added to the group as {model.Role}.";
        }
        catch (InvalidOperationException)
        {
            TempData["Error"] = "This user is already a member of the group.";
        }
        return RedirectToAction(nameof(Members), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveMember(int id, int userId)
    {
        await groupService.RemoveMemberAsync(id, userId);
        TempData["Success"] = "Member removed from the group.";
        return RedirectToAction(nameof(Members), new { id });
    }
}
