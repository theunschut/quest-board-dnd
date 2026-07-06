using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QuestBoard.Domain.Interfaces;
using QuestBoard.Domain.Models;
using QuestBoard.Service.Constants;
using QuestBoard.Service.ViewModels.GroupPickerViewModels;
using System.Security.Claims;

namespace QuestBoard.Service.Controllers;

[Authorize]
public class GroupPickerController(IGroupService groupService, IUserService userService) : Controller
{
    [HttpGet]
    [Route("groups/pick")]
    [Route("[controller]/[action]")]
    public async Task<IActionResult> Index(string? returnUrl = null)
    {
        var isSuperAdmin = User.IsInRole("SuperAdmin");
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        IList<GroupWithMemberCount> groups = isSuperAdmin
            ? await groupService.GetAllWithMemberCountAsync()
            : await groupService.GetGroupsForUserAsync(userId);

        if (!isSuperAdmin && groups.Count == 0)
        {
            return View(new GroupPickerViewModel { Groups = [], IsSuperAdmin = false, HasNoGroups = true, ReturnUrl = returnUrl });
        }

        if (!isSuperAdmin && groups.Count == 1)
        {
            HttpContext.Session.SetInt32(SessionKeys.ActiveGroupId, groups[0].Id);
            HttpContext.Session.SetString(SessionKeys.ActiveGroupName, groups[0].Name);
            HttpContext.Session.SetString(SessionKeys.ActiveGroupValidatedAtUtc, DateTime.UtcNow.ToString("O"));
            return RedirectToLocal(returnUrl);
        }

        return View(new GroupPickerViewModel { Groups = groups, IsSuperAdmin = isSuperAdmin, ReturnUrl = returnUrl });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SelectGroup(int groupId, string? returnUrl = null)
    {
        var group = await groupService.GetByIdAsync(groupId);
        if (group == null) return NotFound();

        var isSuperAdmin = User.IsInRole("SuperAdmin");
        if (!isSuperAdmin)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var role = await userService.GetGroupRoleByIdAsync(userId, groupId);
            if (role == null) return NotFound();
        }

        HttpContext.Session.SetInt32(SessionKeys.ActiveGroupId, group.Id);
        HttpContext.Session.SetString(SessionKeys.ActiveGroupName, group.Name);
        HttpContext.Session.SetString(SessionKeys.ActiveGroupValidatedAtUtc, DateTime.UtcNow.ToString("O"));
        return RedirectToLocal(returnUrl);
    }

    private IActionResult RedirectToLocal(string? returnUrl)
    {
        if (Url.IsLocalUrl(returnUrl))
        {
            return Redirect(returnUrl);
        }
        else
        {
            return RedirectToAction("Index", "Quest");
        }
    }
}
