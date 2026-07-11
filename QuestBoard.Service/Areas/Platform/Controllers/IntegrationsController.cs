using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QuestBoard.Domain.Interfaces;
using QuestBoard.Service.Extensions;
using QuestBoard.Service.ViewModels.PlatformViewModels;

namespace QuestBoard.Service.Areas.Platform.Controllers;

[Area("Platform")]
[Authorize(Policy = "SuperAdminOnly")]
public class IntegrationsController(IPlatformSettingService settingService) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var settings = await settingService.GetForScopeAsync(null);
        var model = new IntegrationSettingsViewModel
        {
            OmphalosUrl = settings.Url,
            IsEnabled = settings.IsEnabled,
            HasSecretConfigured = settings.HasSecret,
            // A freshly generated secret is carried through TempData for exactly one render
            // (see GenerateSecret below); the real stored secret is never assigned here.
            SharedSecret = TempData["GeneratedSecret"] as string
        };
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Index(IntegrationSettingsViewModel model)
    {
        if (!ModelState.IsValid)
        {
            var settings = await settingService.GetForScopeAsync(null);
            model.HasSecretConfigured = settings.HasSecret;
            return View(model);
        }

        // Blank means "keep the current secret" — the service preserves it unchanged.
        var newSecret = string.IsNullOrWhiteSpace(model.SharedSecret) ? null : model.SharedSecret;
        await settingService.SaveAsync(null, model.OmphalosUrl, newSecret, model.IsEnabled);
        return this.RedirectWithSuccess(nameof(Index), "Integration settings saved.");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> GenerateSecret()
    {
        var secret = await settingService.GenerateAndSaveSecretAsync(null);
        TempData["GeneratedSecret"] = secret;
        return this.RedirectWithSuccess(
            nameof(Index),
            "New shared secret generated. Copy it now and update Omphalos's configuration — it won't be shown again.");
    }
}
