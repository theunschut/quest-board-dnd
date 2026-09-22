using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QuestBoard.Domain.Interfaces;
using QuestBoard.Domain.Models;
using QuestBoard.Service.Constants;
using QuestBoard.Service.Helpers;
using QuestBoard.Service.Services;
using QuestBoard.Service.ViewModels.GroupPickerViewModels;
using System.Security.Claims;

namespace QuestBoard.Service.Controllers;

[Authorize]
public class GroupPickerController(
    IGroupService groupService,
    IUserService userService,
    IActiveBoardSwitcher activeBoardSwitcher,
    ICrossBoardLinkResolver crossBoardLinkResolver) : Controller
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

        // The link that sent the viewer here already names which board the page lives on, so
        // asking them to guess is asking for information the application already has. Never runs
        // for a SuperAdmin: their board list above is drawn from every group on the platform, not
        // from their own memberships, while the resolver below only ever answers from the
        // viewer's own memberships -- a SuperAdmin could not be skipped onto anything even if
        // this gate were removed, so today's picker is what they get. When the return URL does
        // not name a board the viewer belongs to -- an unmapped route, a nonexistent id, or
        // someone else's board -- nothing below says so: control falls straight through to the
        // single-board branch or the picker view exactly as it renders today.
        if (!isSuperAdmin && !string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl)
            && CrossBoardRouteTarget.TryFromLocalUrl(returnUrl, out var target) && target != null)
        {
            var resolved = await crossBoardLinkResolver.ResolveAsync(target.Kind, target.Id, userId);
            if (resolved != null)
            {
                await activeBoardSwitcher.SwitchAsync(HttpContext, resolved.GroupId, resolved.GroupName);
                // Only the target-name key is written -- the viewer had no active board before
                // this, so there is no previous board to offer a way back to, and the shared
                // banner partial already renders the plain sentence when that key is absent.
                TempData[TempDataKeys.BoardSwitchTargetName] = resolved.GroupName;
                return RedirectToLocal(returnUrl);
            }
        }

        if (!isSuperAdmin && groups.Count == 1)
        {
            await activeBoardSwitcher.SwitchAsync(HttpContext, groups[0].Id, groups[0].Name);
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

        await activeBoardSwitcher.SwitchAsync(HttpContext, group.Id, group.Name);
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
