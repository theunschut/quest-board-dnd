using AutoMapper;
using QuestBoard.Domain.Enums;
using QuestBoard.Domain.Extensions;
using QuestBoard.Domain.Interfaces;
using QuestBoard.Service.ViewModels.DungeonMasterViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace QuestBoard.Service.Controllers.DungeonMaster;

[Authorize]
public class DungeonMasterController(
    IDungeonMasterProfileService dmProfileService,
    IUserService userService,
    IQuestService questService,
    IMapper mapper,
    IActiveGroupContext activeGroupContext) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Profile(int id, CancellationToken token = default)
    {
        var user = await userService.GetByIdAsync(id, token);
        if (user == null) return NotFound();

        var profile = await dmProfileService.GetProfileByUserIdAsync(id, token);
        var quests = await questService.GetQuestsByDungeonMasterAsync(id, token);
        var currentUser = await userService.GetUserAsync(User);

        GroupRole? role = null;
        if (currentUser != null)
        {
            role = await GetEffectiveRoleAsync();
        }

        var viewModel = new DMProfileViewModel
        {
            UserId = id,
            Name = user.Name ?? string.Empty,
            Bio = profile?.Bio,
            HasProfilePicture = profile?.ProfilePicture?.Length > 0,
            CanEdit = currentUser != null && (currentUser.Equals(user) || role == GroupRole.Admin),
            Quests = mapper.Map<List<QuestSummaryViewModel>>(quests)
        };

        return View(viewModel);
    }

    [HttpGet]
    [Authorize(Policy = "DungeonMasterOnly")]
    public async Task<IActionResult> EditProfile(int? id, CancellationToken token = default)
    {
        var currentUser = await userService.GetUserAsync(User);
        if (currentUser == null)
        {
            return Challenge();
        }

        var targetUser = id.HasValue ? await userService.GetByIdAsync(id.Value, token) : currentUser;
        if (targetUser == null) return NotFound();

        var role = await GetEffectiveRoleAsync();
        if (!currentUser.Equals(targetUser) && role != GroupRole.Admin)
        {
            return Forbid();
        }

        var profile = await dmProfileService.GetProfileByUserIdAsync(targetUser.Id, token);
        var viewModel = new EditDMProfileViewModel
        {
            DungeonMasterId = targetUser.Id,
            Bio = profile?.Bio,
            ProfilePicture = profile?.ProfilePicture
        };

        return View(viewModel);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = "DungeonMasterOnly")]
    public async Task<IActionResult> EditProfile(int? id, EditDMProfileViewModel viewModel, CancellationToken token = default)
    {
        var currentUser = await userService.GetUserAsync(User);
        if (currentUser == null)
        {
            return Challenge();
        }

        var targetUser = id.HasValue ? await userService.GetByIdAsync(id.Value, token) : currentUser;
        if (targetUser == null) return NotFound();

        var role = await GetEffectiveRoleAsync();
        if (!currentUser.Equals(targetUser) && role != GroupRole.Admin)
        {
            return Forbid();
        }

        if (!ModelState.IsValid)
            return View(viewModel);

        byte[]? imageBytes = null;
        if (viewModel.ProfilePictureFile != null && viewModel.ProfilePictureFile.Length > 0)
        {
            const long maxFileSizeBytes = 5 * 1024 * 1024;
            if (viewModel.ProfilePictureFile.Length > maxFileSizeBytes)
            {
                ModelState.AddModelError(nameof(viewModel.ProfilePictureFile),
                    "Profile picture cannot exceed 5 MB.");
                return View(viewModel);
            }
            using var memoryStream = new MemoryStream();
            await viewModel.ProfilePictureFile.CopyToAsync(memoryStream, token);
            imageBytes = memoryStream.ToArray();
        }

        await dmProfileService.UpsertProfileAsync(targetUser.Id, viewModel.Bio, imageBytes, token: token);

        return RedirectToAction(nameof(Profile), new { id = targetUser.Id });
    }

    [HttpGet]
    public async Task<IActionResult> GetDMProfilePicture(int id, CancellationToken token = default)
    {
        var bytes = await dmProfileService.GetProfilePictureAsync(id, token);
        if (bytes == null || bytes.Length == 0) return NotFound();

        var contentType = bytes.Length >= 4 && bytes[0] == 0x89 && bytes[1] == 0x50
            ? "image/png"
            : bytes.Length >= 6 && bytes[0] == 0x47 && bytes[1] == 0x49 && bytes[2] == 0x46
            ? "image/gif"
            : "image/jpeg";

        return File(bytes, contentType);
    }

    // SuperAdmin legitimately has no active group selected (see ActiveGroupContextExtensions'
    // documented contract), so short-circuit to Admin here rather than calling
    // RequireActiveGroupId(), which would throw for a SuperAdmin with no active group.
    private async Task<GroupRole?> GetEffectiveRoleAsync() =>
        User.IsInRole("SuperAdmin")
            ? GroupRole.Admin
            : await userService.GetEffectiveGroupRoleAsync(User, activeGroupContext.RequireActiveGroupId());
}
