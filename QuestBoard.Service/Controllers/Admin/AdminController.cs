using QuestBoard.Domain.Enums;
using QuestBoard.Domain.Interfaces;
using QuestBoard.Domain.Models;
using QuestBoard.Service.Extensions;
using QuestBoard.Service.Jobs;
using QuestBoard.Service.Services;
using QuestBoard.Service.ViewModels.AdminViewModels;
using Hangfire;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text;
using System.Threading.RateLimiting;

namespace QuestBoard.Service.Controllers.Admin;

[Authorize(Policy = "AdminOnly")]
public class AdminController(IUserService userService, IQuestService questService, IIdentityService identityService, IBackgroundJobClient jobClient, IOptions<EmailSettings> emailOptions, IMemoryCache cache, IActiveGroupContext activeGroupContext, ILogger<AdminController> logger, PartitionedRateLimiter<int> emailResendLimiter, ResendStatsClient resendStatsClient) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Users()
    {
        var groupId = activeGroupContext.ActiveGroupId;
        if (groupId == null) return RedirectToAction("Index", "GroupPicker");

        var allUsers = await userService.GetAllAsync();
        var userViewModels = new List<UserManagementViewModel>();

        foreach (var user in allUsers)
        {
            GroupRole? groupRole = await userService.GetGroupRoleByIdAsync(user.Id, groupId.Value);

            userViewModels.Add(new UserManagementViewModel
            {
                User = user,
                IsAdmin = groupRole == GroupRole.Admin,
                IsDungeonMaster = groupRole == GroupRole.DungeonMaster,
                IsPlayer = groupRole == GroupRole.Player,
                EmailConfirmed = user.EmailConfirmed
            });
        }

        // Sort by account type first (Admin, DM, Player), then alphabetically by name
        var sortedUsers = userViewModels
            .OrderBy(u => u.IsAdmin ? 0 : u.IsDungeonMaster ? 1 : 2)  // Admin=0, DM=1, Player=2
            .ThenBy(u => u.User.Name)
            .ToList();

        return View(sortedUsers);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> PromoteToAdmin(int userId)
    {
        var groupId = activeGroupContext.ActiveGroupId;
        if (groupId == null) return RedirectToAction(nameof(Users));
        await userService.SetGroupRoleAsync(userId, groupId.Value, GroupRole.Admin);
        return RedirectToAction(nameof(Users));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DemoteFromAdmin(int userId)
    {
        var groupId = activeGroupContext.ActiveGroupId;
        if (groupId == null) return RedirectToAction(nameof(Users));
        await userService.SetGroupRoleAsync(userId, groupId.Value, GroupRole.Player);
        return RedirectToAction(nameof(Users));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> PromoteToDM(int userId)
    {
        var groupId = activeGroupContext.ActiveGroupId;
        if (groupId == null) return RedirectToAction(nameof(Users));
        await userService.SetGroupRoleAsync(userId, groupId.Value, GroupRole.DungeonMaster);
        return RedirectToAction(nameof(Users));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DemoteToPlayer(int userId)
    {
        var groupId = activeGroupContext.ActiveGroupId;
        if (groupId == null) return RedirectToAction(nameof(Users));
        await userService.SetGroupRoleAsync(userId, groupId.Value, GroupRole.Player);
        return RedirectToAction(nameof(Users));
    }

    [HttpGet]
    public IActionResult CreateUser()
    {
        return View(new CreateUserViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateUser(CreateUserViewModel model)
    {
        if (this.TryReturnInvalidModel(model, out var invalidModelResult))
        {
            return invalidModelResult!;
        }

        var groupId = activeGroupContext.ActiveGroupId;
        if (groupId == null) return RedirectToAction("Index", "GroupPicker");

        var result = await userService.CreateAsync(model.Email, model.Name);

        if (result.Succeeded)
        {
            var userId = await identityService.GetIdByEmailAsync(model.Email);
            if (userId.HasValue)
            {
                await userService.SetGroupRoleAsync(userId.Value, groupId.Value, model.GroupRole);

                var rawToken = await identityService.GeneratePasswordResetTokenForUserAsync(userId.Value);
                if (rawToken != null)
                {
                    var encodedToken = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(rawToken));
                    var callbackUrl = Url.Action("SetPassword", "Account", new { userId = userId.Value, token = encodedToken }, Request.Scheme);
                    if (callbackUrl == null)
                        logger.LogError("Failed to generate SetPassword callback URL for userId {UserId}", userId.Value);
                    else
                        jobClient.Enqueue<WelcomeEmailJob>(j => j.ExecuteAsync(model.Email, model.Name, callbackUrl, true, CancellationToken.None));
                }
            }

            return this.RedirectWithSuccess(nameof(Users), $"Account created for {model.Name}. A welcome email with a set-password link has been sent.");
        }

        foreach (var error in result.Errors)
        {
            ModelState.AddModelError(string.Empty, error.Description);
        }

        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> EditUser(int userId)
    {
        var user = await userService.GetByIdAsync(userId);
        if (user == null)
        {
            return RedirectToAction(nameof(Users));
        }

        var model = new EditUserViewModel
        {
            Id = user.Id,
            Name = user.Name,
            Email = user.Email,
            HasKey = user.HasKey
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditUser(EditUserViewModel model)
    {
        if (ModelState.IsValid)
        {
            var user = await userService.GetByIdAsync(model.Id);
            if (user == null)
            {
                return RedirectToAction(nameof(Users));
            }

            var emailChanged = !string.Equals(user.Email, model.Email, StringComparison.OrdinalIgnoreCase);

            user.Name = model.Name;
            user.HasKey = model.HasKey;
            if (!emailChanged)
                user.Email = model.Email;

            // Role changes are handled through dedicated promotion/demotion buttons

            await userService.UpdateAsync(user);

            if (emailChanged && !string.IsNullOrEmpty(model.Email))
            {
                // Rate-limited on the same per-target-user budget as
                // SendConfirmationEmail — only email-changing saves are counted.
                using var lease = emailResendLimiter.AttemptAcquire(model.Id);
                if (!lease.IsAcquired)
                {
                    Response.StatusCode = StatusCodes.Status429TooManyRequests;
                    return Content("Too many requests. Please try again later.");
                }

                var rawToken = await identityService.GenerateChangeEmailTokenAsync(user.Id, model.Email);
                if (rawToken != null)
                {
                    var encodedToken = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(rawToken));
                    var callbackUrl = Url.Action("ConfirmEmailChange", "Account",
                        new { userId = user.Id, newEmail = model.Email, token = encodedToken }, Request.Scheme);
                    jobClient.Enqueue<ChangeEmailConfirmationJob>(j => j.ExecuteAsync(model.Email, user.Name, callbackUrl!, CancellationToken.None));
                    return this.RedirectWithSuccess(nameof(Users), $"A confirmation email has been sent to {model.Email} for {user.Name}. The address will update once confirmed.");
                }
            }

            return RedirectToAction(nameof(Users));
        }

        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> ResetPassword(int userId)
    {
        var user = await userService.GetByIdAsync(userId);
        if (user == null)
        {
            return RedirectToAction(nameof(Users));
        }

        var model = new ResetPasswordViewModel
        {
            UserId = user.Id,
            UserName = user.Name
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model)
    {
        if (ModelState.IsValid)
        {
            var user = await userService.GetByIdAsync(model.UserId);
            if (user == null)
            {
                return RedirectToAction(nameof(Users));
            }

            var result = await userService.ResetPasswordAsync(User, user, model.NewPassword);

            if (result.Succeeded)
            {
                return this.RedirectWithMessage(nameof(Users), "SuccessMessage", $"Password reset successfully for {user.Name}!");
            }

            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }
        }

        return View(model);
    }

    [HttpDelete]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteUser(int id)
    {
        var user = await userService.GetByIdAsync(id);
        if (user == null)
        {
            return NotFound();
        }

        await userService.RemoveAsync(user);
        return Ok();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SendConfirmationEmail(int userId)
    {
        // Repeatable manual resend button, rate-limited per target user (3/hour)
        // to protect the Resend relay's 100/day quota from accidental button-mashing.
        using var lease = emailResendLimiter.AttemptAcquire(userId);
        if (!lease.IsAcquired)
        {
            Response.StatusCode = StatusCodes.Status429TooManyRequests;
            return Content("Too many requests. Please try again later.");
        }

        var user = await userService.GetByIdAsync(userId);
        if (user == null)
        {
            return RedirectToAction(nameof(Users));
        }

        if (user.EmailConfirmed)
        {
            return this.RedirectWithError(nameof(Users), $"{user.Name} has already confirmed their account.");
        }

        var rawToken = await identityService.GeneratePasswordResetTokenForUserAsync(userId);
        if (rawToken == null || string.IsNullOrEmpty(user.Email))
        {
            return this.RedirectWithError(nameof(Users), $"Failed to send confirmation email to {user.Name}. Please try again.");
        }

        var encodedToken = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(rawToken));
        var callbackUrl = Url.Action("SetPassword", "Account", new { userId, token = encodedToken }, Request.Scheme);
        if (callbackUrl == null)
        {
            logger.LogError("Failed to generate SetPassword callback URL for userId {UserId}", userId);
            return this.RedirectWithError(nameof(Users), $"Failed to send confirmation email to {user.Name}. Please try again.");
        }

        // Legacy accounts created before the admin-set-password flow was retired may already have a
        // password set but never confirmed their email — the "opened an account in your name" copy
        // would be inaccurate for them, so Welcome.razor picks a different variant via IsNewAccount.
        var hasExistingPassword = await userService.HasPasswordAsync(userId);
        jobClient.Enqueue<WelcomeEmailJob>(j => j.ExecuteAsync(user.Email!, user.Name, callbackUrl, !hasExistingPassword, CancellationToken.None));
        return this.RedirectWithSuccess(nameof(Users), $"Welcome email queued for {user.Name}.");
    }

    [HttpGet]
    public async Task<IActionResult> Quests()
    {
        var allQuests = await questService.GetAllAsync();

        // Sort by creation date (newest first)
        var sortedQuests = allQuests
            .OrderByDescending(q => q.CreatedAt)
            .ToList();

        return View(sortedQuests);
    }

    [HttpDelete]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteQuest(int id)
    {
        var quest = await questService.GetByIdAsync(id);
        if (quest == null)
        {
            return NotFound();
        }

        await questService.RemoveAsync(quest);
        return Ok();
    }

    [HttpGet]
    public async Task<IActionResult> EmailStats(bool force = false, CancellationToken token = default)
    {
        var apiKey = emailOptions.Value.ResendApiKey;

        if (string.IsNullOrWhiteSpace(apiKey))
            return View(EmailStatsViewModel.MissingKey());

        const string cacheKey = "resend-email-stats";

        if (!force && cache.TryGetValue(cacheKey, out EmailStatsViewModel? cached) && cached is not null)
            return View(cached);

        cache.Remove(cacheKey);

        var (viewModel, error) = await GetResendStatsAsync(apiKey, token);

        if (error)
            return View(EmailStatsViewModel.ApiError());

        cache.Set(cacheKey, viewModel,
            new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5) });

        return View(viewModel);
    }

    private async Task<(EmailStatsViewModel stats, bool error)> GetResendStatsAsync(
        string apiKey, CancellationToken token)
    {
        var cutoff = DateTime.UtcNow.AddDays(-30);
        var (records, error) = await resendStatsClient.FetchAllRecordsAsync(apiKey, cutoff, token);

        if (error)
            return (new EmailStatsViewModel(), true);

        var counts = ResendStatsAggregator.Aggregate(records, cutoff);

        return (new EmailStatsViewModel
        {
            Sent = counts.Sent,
            Delivered = counts.Delivered,
            Bounced = counts.Bounced,
            Failed = counts.Failed,
            AsOf = DateTime.UtcNow
        }, false);
    }
}
