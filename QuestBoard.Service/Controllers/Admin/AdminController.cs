using QuestBoard.Domain.Enums;
using QuestBoard.Domain.Interfaces;
using QuestBoard.Domain.Models;
using QuestBoard.Service.Extensions;
using QuestBoard.Service.Filters;
using QuestBoard.Service.Jobs;
using QuestBoard.Service.Services;
using QuestBoard.Service.ViewModels.AdminViewModels;
using Hangfire;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Security.Claims;
using System.Text;
using System.Threading.RateLimiting;

namespace QuestBoard.Service.Controllers.Admin;

[Authorize(Policy = "AdminOnly")]
public class AdminController(IUserService userService, IQuestService questService, IGroupService groupService, IIdentityService identityService, IBackgroundJobClient jobClient, IOptions<EmailSettings> emailOptions, IMemoryCache cache, IActiveGroupContext activeGroupContext, ILogger<AdminController> logger, PartitionedRateLimiter<int> emailResendLimiter, [FromKeyedServices(AdminRateLimitKeys.CreateUser)] PartitionedRateLimiter<int> createUserLimiter, ResendStatsClient resendStatsClient, IBoardTypeResolver boardTypeResolver, IBoardClock boardClock) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Users()
    {
        var groupId = activeGroupContext.ActiveGroupId;
        if (groupId == null) return RedirectToAction("Index", "GroupPicker");

        var allUsers = await userService.GetAllGroupMembersAsync(groupId.Value);
        var userViewModels = new List<UserManagementViewModel>();

        // The page shows what the server will accept, so each row's refusals come from the same rules that refuse a
        // crafted request: one reach question for every row and one count of the board's Admins.
        var actorId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var reach = await userService.GetAccountReachAsync(User, groupId.Value, allUsers.Select(u => u.Id).ToList());
        var adminCount = await groupService.CountMembersWithRoleAsync(groupId.Value, GroupRole.Admin);

        foreach (var user in allUsers)
        {
            GroupRole? groupRole = await userService.GetGroupRoleByIdAsync(user.Id, groupId.Value);

            userViewModels.Add(new UserManagementViewModel
            {
                User = user,
                Role = groupRole,
                IsAdmin = groupRole == GroupRole.Admin,
                IsDungeonMaster = groupRole == GroupRole.DungeonMaster,
                IsPlayer = groupRole == GroupRole.Player,
                EmailConfirmed = user.EmailConfirmed,
                IsSelf = user.Id == actorId,
                IsLastAdmin = groupRole == GroupRole.Admin && adminCount <= 1,
                Reach = reach.GetValueOrDefault(user.Id, AccountReach.Reachable)
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
    public Task<IActionResult> PromoteToAdmin(int userId) => ChangeRoleAsync(userId, GroupRole.Admin);

    [HttpPost]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> DemoteFromAdmin(int userId) => ChangeRoleAsync(userId, GroupRole.DungeonMaster);

    [HttpPost]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> PromoteToDM(int userId) => ChangeRoleAsync(userId, GroupRole.DungeonMaster);

    [HttpPost]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> DemoteToPlayer(int userId) => ChangeRoleAsync(userId, GroupRole.Player);

    // All four role buttons end here. The Domain judges the change on the role the person would end up with, so a
    // request that names the wrong button cannot get past the self and last-Admin rules.
    private async Task<IActionResult> ChangeRoleAsync(int userId, GroupRole newRole)
    {
        var groupId = activeGroupContext.ActiveGroupId;
        if (groupId == null) return RedirectToAction(nameof(Users));

        var actorId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var outcome = await userService.ChangeMemberRoleAsync(groupId.Value, actorId, userId, newRole);
        return outcome switch
        {
            MemberChangeOutcome.NotAMember => this.RedirectWithError(nameof(Users), AdminActionMessages.MemberStale),
            MemberChangeOutcome.Self => this.RedirectWithError(nameof(Users), AdminActionMessages.SelfRefused),
            MemberChangeOutcome.LastAdmin => this.RedirectWithError(nameof(Users), AdminActionMessages.LastAdminRefused),
            _ => RedirectToAction(nameof(Users))
        };
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

        // Each administrator has their own hourly budget for creating accounts. It is spent only by a request that
        // passed validation, and it is asked before any account work so a refused request creates nothing.
        var actorId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        using var createLease = createUserLimiter.AttemptAcquire(actorId);
        if (!createLease.IsAcquired)
            return this.RedirectWithError(nameof(Users), AdminActionMessages.CreateLimited);

        var result = await userService.CreateOrAddToGroupAsync(model.Email, model.Name, groupId.Value, model.GroupRole);

        switch (result.Outcome)
        {
            case CreateOrAddToGroupOutcome.NewAccountCreated:
                {
                    var rawToken = await identityService.GeneratePasswordResetTokenForUserAsync(result.UserId!.Value);
                    if (rawToken != null)
                    {
                        var encodedToken = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(rawToken));
                        var callbackUrl = Url.Action("SetPassword", "Account", new { userId = result.UserId.Value, token = encodedToken }, Request.Scheme);
                        if (callbackUrl == null)
                            // userId is a database-internal integer identifier, not personal data — despite
                            // flowing from the newly created account, it carries no PII of its own.
                            logger.LogError("Failed to generate SetPassword callback URL for userId {UserId}", result.UserId.Value);
                        else
                            jobClient.Enqueue<WelcomeEmailJob>(j => j.ExecuteAsync(model.Email, model.Name, callbackUrl, true, CancellationToken.None));
                    }

                    return this.RedirectWithSuccess(nameof(Users), $"Account created for {model.Name}. A welcome email with a set-password link has been sent.");
                }

            case CreateOrAddToGroupOutcome.AddedToGroup:
                {
                    var group = await groupService.GetByIdAsync(groupId.Value);
                    var loginUrl = Url.Action("Login", "Account", null, Request.Scheme);
                    if (loginUrl == null || group == null)
                        // userId is a database-internal integer identifier, not personal data — despite
                        // flowing from the newly created account, it carries no PII of its own.
                        logger.LogError("Failed to generate Login callback URL or resolve group for userId {UserId}", result.UserId);
                    else
                        jobClient.Enqueue<GroupMembershipAddedEmailJob>(j => j.ExecuteAsync(result.Email, result.Name, group.Name, model.GroupRole.ToString(), loginUrl, CancellationToken.None));

                    return this.RedirectWithSuccess(nameof(Users), $"{result.Name} has been added to the group as {model.GroupRole}. A notification email has been sent.");
                }

            case CreateOrAddToGroupOutcome.AddedToGroupStrandedAccount:
                {
                    var rawToken = await identityService.GeneratePasswordResetTokenForUserAsync(result.UserId!.Value);
                    if (rawToken != null)
                    {
                        var encodedToken = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(rawToken));
                        var callbackUrl = Url.Action("SetPassword", "Account", new { userId = result.UserId.Value, token = encodedToken }, Request.Scheme);
                        if (callbackUrl == null)
                            // userId is a database-internal integer identifier, not personal data — despite
                            // flowing from the newly created account, it carries no PII of its own.
                            logger.LogError("Failed to generate SetPassword callback URL for userId {UserId}", result.UserId.Value);
                        else
                        {
                            var hasExistingPassword = await userService.HasPasswordAsync(result.UserId.Value);
                            jobClient.Enqueue<WelcomeEmailJob>(j => j.ExecuteAsync(result.Email, result.Name, callbackUrl, !hasExistingPassword, CancellationToken.None));
                        }
                    }

                    return this.RedirectWithSuccess(nameof(Users), $"{result.Name} has been added to the group as {model.GroupRole}. A notification email has been sent.");
                }

            case CreateOrAddToGroupOutcome.AlreadyMember:
                return this.RedirectWithWarning(nameof(Users), $"{result.Name} is already a member of this group.");

            case CreateOrAddToGroupOutcome.Failed:
            default:
                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError(string.Empty, error);
                }

                return View(model);
        }
    }

    [HttpGet]
    public async Task<IActionResult> EditUser(int userId)
    {
        var groupId = activeGroupContext.ActiveGroupId;
        if (groupId == null) return RedirectToAction(nameof(Users));
        // Guard against a crafted request for a user outside the active group
        var targetRole = await userService.GetGroupRoleByIdAsync(userId, groupId.Value);
        if (targetRole == null) return this.RedirectWithError(nameof(Users), AdminActionMessages.MemberStale);

        if (!await IsWithinReachAsync(groupId.Value, userId))
            return this.RedirectWithError(nameof(Users), AdminActionMessages.ReachRefused);

        var user = await userService.GetByIdAsync(userId);
        if (user == null)
        {
            return this.RedirectWithError(nameof(Users), AdminActionMessages.MemberStale);
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
            var groupId = activeGroupContext.ActiveGroupId;
            if (groupId == null) return RedirectToAction(nameof(Users));
            // Guard against a crafted POST for a user outside the active group
            var targetRole = await userService.GetGroupRoleByIdAsync(model.Id, groupId.Value);
            if (targetRole == null) return this.RedirectWithError(nameof(Users), AdminActionMessages.MemberStale);

            if (!await IsWithinReachAsync(groupId.Value, model.Id))
                return this.RedirectWithError(nameof(Users), AdminActionMessages.ReachRefused);

            var user = await userService.GetByIdAsync(model.Id);
            if (user == null)
            {
                return this.RedirectWithError(nameof(Users), AdminActionMessages.MemberStale);
            }

            var emailChanged = !string.Equals(user.Email, model.Email, StringComparison.OrdinalIgnoreCase);
            var newEmail = emailChanged && !string.IsNullOrEmpty(model.Email) ? model.Email : null;

            // Rate-limited on the same per-target-user budget as SendConfirmationEmail — only email-changing saves
            // are counted. Asked before anything is saved, so a refused change leaves the account exactly as it was.
            using var lease = newEmail != null ? emailResendLimiter.AttemptAcquire(model.Id) : null;
            if (lease is { IsAcquired: false })
                return this.RedirectWithError(nameof(Users), AdminActionMessages.EmailRateLimited);

            user.Name = model.Name;
            user.HasKey = model.HasKey;
            if (!emailChanged)
                user.Email = model.Email;

            // Role changes are handled through dedicated promotion/demotion buttons

            await userService.UpdateAsync(user);

            if (newEmail != null)
            {
                var rawToken = await identityService.GenerateChangeEmailTokenAsync(user.Id, newEmail);
                if (rawToken != null)
                {
                    var encodedToken = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(rawToken));
                    var callbackUrl = Url.Action("ConfirmEmailChange", "Account",
                        new { userId = user.Id, newEmail, token = encodedToken }, Request.Scheme);
                    if (callbackUrl == null)
                    {
                        logger.LogError("Failed to generate ConfirmEmailChange callback URL for userId {UserId}", user.Id);
                        return this.RedirectWithError(nameof(Users), $"Failed to send confirmation email to {newEmail} for {user.Name}. Please try again.");
                    }

                    jobClient.Enqueue<ChangeEmailConfirmationJob>(j => j.ExecuteAsync(newEmail, user.Name, callbackUrl, CancellationToken.None));
                    return this.RedirectWithSuccess(nameof(Users), $"A confirmation email has been sent to {newEmail} for {user.Name}. The address will update once confirmed.");
                }
            }

            return RedirectToAction(nameof(Users));
        }

        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> ResetPassword(int userId)
    {
        var groupId = activeGroupContext.ActiveGroupId;
        if (groupId == null) return RedirectToAction(nameof(Users));
        // Guard against a crafted request for a user outside the active group
        var targetRole = await userService.GetGroupRoleByIdAsync(userId, groupId.Value);
        if (targetRole == null) return this.RedirectWithError(nameof(Users), AdminActionMessages.MemberStale);

        if (!await IsWithinReachAsync(groupId.Value, userId))
            return this.RedirectWithError(nameof(Users), AdminActionMessages.ReachRefused);

        var user = await userService.GetByIdAsync(userId);
        if (user == null)
        {
            return this.RedirectWithError(nameof(Users), AdminActionMessages.MemberStale);
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
            var groupId = activeGroupContext.ActiveGroupId;
            if (groupId == null) return RedirectToAction(nameof(Users));
            // Guard against a crafted POST for a user outside the active group
            var targetRole = await userService.GetGroupRoleByIdAsync(model.UserId, groupId.Value);
            if (targetRole == null) return this.RedirectWithError(nameof(Users), AdminActionMessages.MemberStale);

            if (!await IsWithinReachAsync(groupId.Value, model.UserId))
                return this.RedirectWithError(nameof(Users), AdminActionMessages.ReachRefused);

            var user = await userService.GetByIdAsync(model.UserId);
            if (user == null)
            {
                return this.RedirectWithError(nameof(Users), AdminActionMessages.MemberStale);
            }

            var result = await userService.ResetPasswordAsync(User, groupId.Value, user, model.NewPassword);

            if (result.Succeeded)
            {
                return this.RedirectWithSuccess(nameof(Users), AdminActionMessages.PasswordReset(user.Name));
            }

            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }
        }

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteUser(int id)
    {
        var groupId = activeGroupContext.ActiveGroupId;
        if (groupId == null) return RedirectToAction(nameof(Users));

        var user = await userService.GetByIdAsync(id);
        if (user == null)
        {
            return this.RedirectWithError(nameof(Users), AdminActionMessages.MemberStale);
        }

        // The Domain refuses the removal of the acting user and of the board's only Admin, and answers NotAMember for
        // a crafted request that names someone outside the active group.
        var actorId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var outcome = await userService.TryRemoveMemberAsync(groupId.Value, actorId, id);
        return outcome switch
        {
            MemberChangeOutcome.NotAMember => this.RedirectWithError(nameof(Users), AdminActionMessages.MemberStale),
            MemberChangeOutcome.Self => this.RedirectWithError(nameof(Users), AdminActionMessages.SelfRefused),
            MemberChangeOutcome.LastAdmin => this.RedirectWithError(nameof(Users), AdminActionMessages.LastAdminRefused),
            _ => this.RedirectWithSuccess(nameof(Users), AdminActionMessages.MemberRemoved(user.Name))
        };
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SendConfirmationEmail(int userId)
    {
        var groupId = activeGroupContext.ActiveGroupId;
        if (groupId == null) return RedirectToAction(nameof(Users));
        // Guard against a crafted POST for a user outside the active group
        var targetRole = await userService.GetGroupRoleByIdAsync(userId, groupId.Value);
        if (targetRole == null) return this.RedirectWithError(nameof(Users), AdminActionMessages.MemberStale);

        if (!await IsWithinReachAsync(groupId.Value, userId))
            return this.RedirectWithError(nameof(Users), AdminActionMessages.ReachRefused);

        var user = await userService.GetByIdAsync(userId);
        if (user == null)
        {
            return this.RedirectWithError(nameof(Users), AdminActionMessages.MemberStale);
        }

        if (user.EmailConfirmed)
        {
            return this.RedirectWithError(nameof(Users), $"{user.Name} has already confirmed their account.");
        }

        // Repeatable manual resend button, rate-limited per target user (3/hour) to protect the Resend relay's
        // 100/day quota from accidental button-mashing. The permit is taken only once every check above has passed,
        // so a request that is refused, out of board or pointless never spends the account's own budget.
        using var lease = emailResendLimiter.AttemptAcquire(userId);
        if (!lease.IsAcquired)
            return this.RedirectWithError(nameof(Users), AdminActionMessages.EmailRateLimited);

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
    [BoardPage(BoardPage.Quests)]
    public async Task<IActionResult> Quests()
    {
        var allQuests = await questService.GetAllAsync();

        // Sort by creation date (newest first)
        var sortedQuests = allQuests
            .OrderByDescending(q => q.CreatedAt)
            .ToList();

        ViewBag.BoardType = await boardTypeResolver.GetBoardTypeAsync() ?? BoardType.OneShot;
        // The status badge decides "Done" against a floating wall-clock game night, so the view
        // is handed the board's own today rather than reading a clock itself.
        ViewBag.BoardToday = boardClock.Today;

        return View(sortedQuests);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [BoardPage(BoardPage.Quests)]
    public async Task<IActionResult> DeleteQuest(int id)
    {
        // The board scope is applied by the quest query itself, so an id of another board reads as missing.
        var quest = await questService.GetByIdAsync(id);
        if (quest == null)
        {
            return this.RedirectWithError(nameof(Quests), AdminActionMessages.QuestMissing);
        }

        try
        {
            await questService.RemoveAsync(quest);
        }
        catch (DbUpdateException)
        {
            return this.RedirectWithError(nameof(Quests), AdminActionMessages.QuestRefused);
        }

        return this.RedirectWithSuccess(nameof(Quests), AdminActionMessages.QuestDeleted);
    }

    // Requires SuperAdmin in addition to the class-level AdminOnly policy — an Admin who is
    // not also a SuperAdmin must not see platform-wide email delivery stats.
    [Authorize(Policy = "SuperAdminOnly")]
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

    // Whether the signed-in account may change the given account from the active board. Asked before any read of the
    // account's form and before any write, so a refused account is never touched.
    private async Task<bool> IsWithinReachAsync(int groupId, int targetUserId)
    {
        var reach = await userService.GetAccountReachAsync(User, groupId, [targetUserId]);
        return reach[targetUserId] == AccountReach.Reachable;
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
