using QuestBoard.Domain.Interfaces;
using QuestBoard.Domain.Models;
using QuestBoard.Service.Components.Emails;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace QuestBoard.Service.Controllers.Admin;

[Authorize(Policy = "AdminOnly")]
public class EmailPreviewController(IEmailRenderService emailRenderService, IOptions<EmailSettings> emailOptions) : Controller
{
    private static readonly IList<string> SamplePlayers = ["Arannis", "Tordek", "Mialee"];

    // Structured-Markdown sample exercising headings/lists/blockquote styling and exceeding the
    // default truncation budget (3 top-level blocks / 400 plain-text characters, 2/350 for
    // SessionReminder's own override) so the read-more link is visible in the browser preview.
    private const string SampleMarkdownDescription = """
        ## The Sunken Archive

        Deep beneath the tidal caves of Blackwater Bay lies a flooded library, said to hold the last surviving records of the Sunken Kings. Recovering even a single intact scroll would make any adventurer's name legend.

        Before you descend, know this:

        - The tide floods the lower archive twice a day — time your descent carefully
        - Bioluminescent eels guard the deepest shelves and are drawn to torchlight
        - The scrolls dissolve in fresh air within minutes unless sealed in wax

        > "Three parties have gone in. None have come back with more than water-logged parchment and a grim story." — Harbor-master Wren

        1. Scout the tidal entrance at low tide
        2. Secure a sealed retrieval case before opening any shelf
        3. Retreat the moment the bells of the tide-clock begin to toll
        """;

    [HttpGet]
    public IActionResult Index()
    {
        var appUrl = emailOptions.Value.AppUrl;
        var html = $$"""
            <!doctype html><html><head><meta charset="utf-8">
            <title>Email Preview — Admin</title>
            <style>body{font-family:sans-serif;padding:2rem;max-width:600px;margin:auto}
            h1{margin-bottom:1rem}ul{list-style:none;padding:0}
            li{margin:.5rem 0}a{color:#4a6cf7;text-decoration:none;font-size:1.1rem}
            a:hover{text-decoration:underline}</style></head>
            <body><h1>Email Template Previews</h1>
            <ul>
              <li><a href="{{appUrl}}/EmailPreview/QuestFinalized">Quest Finalized</a></li>
              <li><a href="{{appUrl}}/EmailPreview/QuestDateChanged">Quest Date Changed</a></li>
              <li><a href="{{appUrl}}/EmailPreview/SessionReminder">Session Reminder</a></li>
              <li><a href="{{appUrl}}/EmailPreview/WaitlistPromoted">Waitlist Promoted</a></li>
              <li><a href="{{appUrl}}/EmailPreview/Welcome">Welcome</a></li>
              <li><a href="{{appUrl}}/EmailPreview/AddedToGroup">Added To Group</a></li>
              <li><a href="{{appUrl}}/EmailPreview/ForgotPassword">Forgot Password</a></li>
              <li><a href="{{appUrl}}/EmailPreview/ChangeEmailConfirm">Change Email Confirm</a></li>
            </ul></body></html>
            """;
        return Content(html, "text/html");
    }

    [HttpGet]
    public async Task<IActionResult> QuestFinalized()
    {
        var appUrl = emailOptions.Value.AppUrl;
        var html = await emailRenderService.RenderAsync<Components.Emails.QuestFinalized>(new()
        {
            [nameof(Components.Emails.QuestFinalized.QuestTitle)] = "The Tomb of Annihilation",
            [nameof(Components.Emails.QuestFinalized.DmName)] = "Dungeon Master Theomund",
            [nameof(Components.Emails.QuestFinalized.QuestDate)] = DateTime.Today.AddDays(7),
            [nameof(Components.Emails.QuestFinalized.QuestDescription)] = SampleMarkdownDescription,
            [nameof(Components.Emails.QuestFinalized.ConfirmedPlayerNames)] = SamplePlayers,
            [nameof(Components.Emails.QuestFinalized.QuestUrl)] = $"{appUrl}/Quest",
            [nameof(Components.Emails.QuestFinalized.ChallengeRating)] = 9,
            [nameof(Components.Emails.QuestFinalized.AppUrl)] = appUrl,
        });
        return Content(html, "text/html");
    }

    [HttpGet]
    public async Task<IActionResult> QuestDateChanged()
    {
        var appUrl = emailOptions.Value.AppUrl;
        var html = await emailRenderService.RenderAsync<Components.Emails.QuestDateChanged>(new()
        {
            [nameof(Components.Emails.QuestDateChanged.QuestTitle)] = "The Tomb of Annihilation",
            [nameof(Components.Emails.QuestDateChanged.DmName)] = "Dungeon Master Theomund",
            [nameof(Components.Emails.QuestDateChanged.OldDate)] = DateTime.Today.AddDays(7),
            [nameof(Components.Emails.QuestDateChanged.NewDate)] = DateTime.Today.AddDays(14),
            [nameof(Components.Emails.QuestDateChanged.QuestUrl)] = $"{appUrl}/Quest",
            [nameof(Components.Emails.QuestDateChanged.AppUrl)] = appUrl,
        });
        return Content(html, "text/html");
    }

    [HttpGet]
    public async Task<IActionResult> Welcome()
    {
        var appUrl = emailOptions.Value.AppUrl;
        var html = await emailRenderService.RenderAsync<Components.Emails.Welcome>(new()
        {
            [nameof(Components.Emails.Welcome.UserName)] = "Arannis",
            [nameof(Components.Emails.Welcome.CallbackUrl)] = $"{appUrl}/Account/SetPassword?userId=preview&token=preview-token",
            [nameof(Components.Emails.Welcome.AppUrl)] = appUrl,
        });
        return Content(html, "text/html");
    }

    [HttpGet]
    public async Task<IActionResult> AddedToGroup()
    {
        var appUrl = emailOptions.Value.AppUrl;
        var html = await emailRenderService.RenderAsync<Components.Emails.AddedToGroup>(new()
        {
            [nameof(Components.Emails.AddedToGroup.UserName)] = "Arannis",
            [nameof(Components.Emails.AddedToGroup.GroupName)] = "The Iron Vanguard",
            [nameof(Components.Emails.AddedToGroup.Role)] = "Player",
            [nameof(Components.Emails.AddedToGroup.LoginUrl)] = $"{appUrl}/Account/Login",
            [nameof(Components.Emails.AddedToGroup.AppUrl)] = appUrl,
        });
        return Content(html, "text/html");
    }

    [HttpGet]
    public async Task<IActionResult> ForgotPassword()
    {
        var appUrl = emailOptions.Value.AppUrl;
        var html = await emailRenderService.RenderAsync<Components.Emails.ForgotPassword>(new()
        {
            [nameof(Components.Emails.ForgotPassword.CallbackUrl)] = $"{appUrl}/Account/SetPassword?userId=preview&token=preview-token",
            [nameof(Components.Emails.ForgotPassword.AppUrl)] = appUrl,
        });
        return Content(html, "text/html");
    }

    [HttpGet]
    public async Task<IActionResult> ChangeEmailConfirm()
    {
        var appUrl = emailOptions.Value.AppUrl;
        var html = await emailRenderService.RenderAsync<Components.Emails.ChangeEmailConfirm>(new()
        {
            [nameof(Components.Emails.ChangeEmailConfirm.UserName)] = "Arannis",
            [nameof(Components.Emails.ChangeEmailConfirm.CallbackUrl)] = $"{appUrl}/Account/ConfirmEmailChange?userId=preview&newEmail=new%40example.com&token=preview-token",
            [nameof(Components.Emails.ChangeEmailConfirm.AppUrl)] = appUrl,
        });
        return Content(html, "text/html");
    }

    [HttpGet]
    public async Task<IActionResult> SessionReminder()
    {
        var appUrl = emailOptions.Value.AppUrl;
        var html = await emailRenderService.RenderAsync<Components.Emails.SessionReminder>(new()
        {
            [nameof(Components.Emails.SessionReminder.QuestTitle)] = "The Tomb of Annihilation",
            [nameof(Components.Emails.SessionReminder.DmName)] = "Dungeon Master Theomund",
            [nameof(Components.Emails.SessionReminder.QuestDate)] = DateTime.Today.AddDays(1),
            [nameof(Components.Emails.SessionReminder.QuestDescription)] = SampleMarkdownDescription,
            [nameof(Components.Emails.SessionReminder.ConfirmedPlayerNames)] = SamplePlayers,
            [nameof(Components.Emails.SessionReminder.QuestUrl)] = $"{appUrl}/Quest",
            [nameof(Components.Emails.SessionReminder.ChallengeRating)] = 9,
            [nameof(Components.Emails.SessionReminder.AppUrl)] = appUrl,
        });
        return Content(html, "text/html");
    }

    [HttpGet]
    public async Task<IActionResult> WaitlistPromoted()
    {
        var appUrl = emailOptions.Value.AppUrl;
        var html = await emailRenderService.RenderAsync<Components.Emails.WaitlistPromoted>(new()
        {
            [nameof(Components.Emails.WaitlistPromoted.QuestTitle)] = "The Tomb of Annihilation",
            [nameof(Components.Emails.WaitlistPromoted.DmName)] = "Dungeon Master Theomund",
            [nameof(Components.Emails.WaitlistPromoted.QuestDate)] = DateTime.Today.AddDays(7),
            [nameof(Components.Emails.WaitlistPromoted.QuestDescription)] = SampleMarkdownDescription,
            [nameof(Components.Emails.WaitlistPromoted.PlayerName)] = "Arannis",
            [nameof(Components.Emails.WaitlistPromoted.QuestUrl)] = $"{appUrl}/Quest",
            [nameof(Components.Emails.WaitlistPromoted.ChallengeRating)] = 9,
            [nameof(Components.Emails.WaitlistPromoted.AppUrl)] = appUrl,
        });
        return Content(html, "text/html");
    }
}
