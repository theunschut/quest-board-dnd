using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QuestBoard.Domain.Interfaces;
using QuestBoard.Service.ViewModels.PlatformViewModels;

namespace QuestBoard.Service.Areas.Platform.Controllers;

[Area("Platform")]
[Authorize(Policy = "SuperAdminOnly")]
public class UsersController(IUserService userService, IIdentityService identityService) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var users = await userService.GetAllAsync();
        var viewModels = new List<PlatformUserViewModel>();
        foreach (var user in users)
        {
            var lockoutEnd = await identityService.GetLockoutEndAsync(user.Id);
            viewModels.Add(new PlatformUserViewModel
            {
                User = user,
                IsDisabled = lockoutEnd == DateTimeOffset.MaxValue
            });
        }
        return View(viewModels);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Disable(int userId)
    {
        var currentUserId = await identityService.GetUserIdAsync(User);
        if (currentUserId == userId)
        {
            TempData["Error"] = "You cannot disable your own account.";
            return RedirectToAction(nameof(Index));
        }

        await identityService.DisableUserAsync(userId);
        TempData["Success"] = "Account disabled.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Enable(int userId)
    {
        await identityService.EnableUserAsync(userId);
        TempData["Success"] = "Account re-enabled.";
        return RedirectToAction(nameof(Index));
    }
}
