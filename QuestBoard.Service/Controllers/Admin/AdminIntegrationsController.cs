using QuestBoard.Domain.Interfaces;
using QuestBoard.Service.Extensions;
using QuestBoard.Service.ViewModels.AdminViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace QuestBoard.Service.Controllers.Admin;

[Authorize(Policy = "AdminOnly")]
public class AdminIntegrationsController(IPlatformSettingService settingService, IActiveGroupContext activeGroupContext) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var groupId = activeGroupContext.ActiveGroupId;
        if (groupId == null) return RedirectToAction("Index", "GroupPicker");

        var model = await BuildViewModelAsync(groupId.Value);

        if (TempData["GeneratedSecret"] is string generatedSecret)
        {
            model.GeneratedSecret = generatedSecret;
        }

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(GroupIntegrationSettingsViewModel model)
    {
        // Guard against a crafted POST — always re-derive the active group, never trust a posted id
        var groupId = activeGroupContext.ActiveGroupId;
        if (groupId == null) return RedirectToAction(nameof(Index));

        if (this.TryReturnInvalidModel(model, out var invalidResult))
        {
            var refreshed = await BuildViewModelAsync(groupId.Value);
            model.HasOverride = refreshed.HasOverride;
            model.HasSecretConfigured = refreshed.HasSecretConfigured;
            model.InstanceDefaultConfigured = refreshed.InstanceDefaultConfigured;
            model.InstanceDefaultEnabled = refreshed.InstanceDefaultEnabled;
            return View("Index", model);
        }

        // Blank secret preserves the previously-stored value rather than overwriting it
        var newSecret = string.IsNullOrWhiteSpace(model.SharedSecret) ? null : model.SharedSecret;
        await settingService.SaveAsync(groupId.Value, model.OmphalosUrl, newSecret, model.IsEnabled);

        return this.RedirectWithSuccess(nameof(Index), "Group override saved. This group now uses its own Omphalos configuration.");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> GenerateSecret()
    {
        var groupId = activeGroupContext.ActiveGroupId;
        if (groupId == null) return RedirectToAction(nameof(Index));

        var secret = await settingService.GenerateAndSaveSecretAsync(groupId.Value);
        TempData["GeneratedSecret"] = secret;

        return this.RedirectWithSuccess(nameof(Index), "New shared secret generated for this group. Copy it now and update Omphalos's configuration — it won't be shown again.");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ClearOverride()
    {
        var groupId = activeGroupContext.ActiveGroupId;
        if (groupId == null) return RedirectToAction(nameof(Index));

        await settingService.ClearScopeAsync(groupId.Value);

        return this.RedirectWithSuccess(nameof(Index), "Group override cleared. This group now uses the instance-wide default.");
    }

    private async Task<GroupIntegrationSettingsViewModel> BuildViewModelAsync(int groupId)
    {
        var own = await settingService.GetForScopeAsync(groupId);
        var hasOverride = await settingService.HasOwnSettingsAsync(groupId);

        // Resolved only to derive cascade booleans — never copy the default's URL/secret onto the ViewModel
        var def = await settingService.GetResolvedAsync(null);

        var model = new GroupIntegrationSettingsViewModel
        {
            HasOverride = hasOverride,
            InstanceDefaultConfigured = def.HasSecret || !string.IsNullOrEmpty(def.Url),
            InstanceDefaultEnabled = def.IsEnabled,
            HasSecretConfigured = own.HasSecret,
            SharedSecret = null
        };

        if (hasOverride)
        {
            model.OmphalosUrl = own.Url;
            model.IsEnabled = own.IsEnabled;
        }

        return model;
    }
}
