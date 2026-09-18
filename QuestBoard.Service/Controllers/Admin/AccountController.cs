using QuestBoard.Domain.Enums;
using QuestBoard.Domain.Interfaces;
using QuestBoard.Domain.Models;
using QuestBoard.Service.Controllers.QuestBoard;
using QuestBoard.Service.Helpers;
using QuestBoard.Service.Jobs;
using QuestBoard.Service.ViewModels.AccountViewModels;
using AutoMapper;
using Hangfire;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text;

namespace QuestBoard.Service.Controllers.Admin;

public class AccountController(
    IUserService userService,
    IIdentityService identityService,
    IBackgroundJobClient jobClient,
    ILogger<AccountController> logger,
    IActiveGroupContext activeGroupContext,
    ICalendarSubscriptionService calendarSubscriptionService,
    IOptions<EmailSettings> emailSettings,
    IMapper mapper) : Controller
{
    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        ViewData["ReturnUrl"] = returnUrl;
        return View();
    }

    [HttpGet]
    public IActionResult ForgotPassword()
    {
        return View();
    }

    [HttpGet]
    [AllowAnonymous]
    public IActionResult AccessDenied()
    {
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("forgot-password")]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordViewModel model)
    {
        if (ModelState.IsValid)
        {
            var userId = await identityService.GetIdByEmailAsync(model.Email);
            if (userId.HasValue)
            {
                var rawToken = await identityService.GeneratePasswordResetTokenForUserAsync(userId.Value);
                if (rawToken != null)
                {
                    var encodedToken = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(rawToken));
                    var callbackUrl = Url.Action(nameof(SetPassword), "Account",
                        new { userId = userId.Value, token = encodedToken }, Request.Scheme);
                    if (callbackUrl == null)
                    {
                        // userId is a database-internal integer identifier, not personal data — despite
                        // flowing from GetIdByEmailAsync(model.Email), it carries no PII of its own.
                        logger.LogError("Failed to generate SetPassword callback URL for userId {UserId}", userId.Value);
                    }
                    else
                    {
                        // Send to the canonically stored email, not the requester's typed casing.
                        var user = await userService.GetByIdAsync(userId.Value);
                        var recipientEmail = string.IsNullOrEmpty(user?.Email) ? model.Email : user.Email;
                        jobClient.Enqueue<ForgotPasswordEmailJob>(j => j.ExecuteAsync(recipientEmail, callbackUrl, CancellationToken.None));
                    }
                }
            }

            // Enumeration-safe: identical message/redirect whether or not the email matched an account.
            TempData["Success"] = "If that email is registered, a reset link has been sent.";
            return RedirectToAction(nameof(ForgotPassword));
        }

        return View(model);
    }

    [HttpGet]
    public IActionResult SetPassword(int userId, string token)
    {
        return View(new SetPasswordViewModel { UserId = userId, Token = token });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("set-password")]
    public async Task<IActionResult> SetPassword(SetPasswordViewModel model)
    {
        if (ModelState.IsValid)
        {
            try
            {
                var decodedToken = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(model.Token));
                var result = await identityService.ResetPasswordAsync(model.UserId, decodedToken, model.NewPassword);

                if (result.Succeeded)
                {
                    var confirmResult = await identityService.ConfirmEmailDirectlyAsync(model.UserId);
                    if (!confirmResult.Succeeded)
                        logger.LogWarning("ConfirmEmailDirectlyAsync failed for userId {UserId} after password reset", model.UserId);

                    TempData["Success"] = "Your password has been set. Please log in.";
                    return RedirectToAction(nameof(Login));
                }

                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "SetPassword failed for userId {UserId}", model.UserId);
                TempData["Error"] = "Password reset failed. The link may be expired or invalid. Please request a new one.";
                return RedirectToAction(nameof(Login));
            }
        }

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
    {
        ViewData["ReturnUrl"] = returnUrl;

        if (ModelState.IsValid)
        {
            var result = await userService.PasswordSignInAsync(model.Email, model.Password, model.RememberMe, lockoutOnFailure: true);

            if (result.Succeeded)
            {
                return RedirectToAction("Index", "GroupPicker", new { returnUrl });
            }

            if (result.IsLockedOut)
            {
                // SignInResult.LockedOut carries no LockoutEnd payload, so re-resolve the user
                // to tell a deliberately disabled account (LockoutEnd == MaxValue) apart from an
                // ordinary temporary lockout from too many failed attempts.
                var lockedOutUserId = await identityService.GetIdByEmailAsync(model.Email);
                DateTimeOffset? lockoutEnd = lockedOutUserId.HasValue
                    ? await identityService.GetLockoutEndAsync(lockedOutUserId.Value)
                    : null;

                var lockoutMessage = lockoutEnd == DateTimeOffset.MaxValue
                    ? "This account has been disabled. Contact an administrator."
                    : "Account locked due to too many failed attempts. Try again in 15 minutes.";

                ModelState.AddModelError(string.Empty, lockoutMessage);
                return View(model);
            }

            ModelState.AddModelError(string.Empty, "Invalid login attempt.");
        }

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await userService.SignOutAsync();
        return RedirectToAction(nameof(HomeController.Index), "Home");
    }

    [HttpGet]
    [Authorize]
    public async Task<IActionResult> Profile(CancellationToken token = default)
    {
        var user = await userService.GetUserAsync(User);

        GroupRole? role = null;
        if (activeGroupContext.ActiveGroupId is { } groupId)
        {
            role = await userService.GetEffectiveGroupRoleAsync(User, groupId);
        }

        var subscriptions = await calendarSubscriptionService.GetForUserAsync(user.Id, token);
        var subscriptionRows = subscriptions.Select(subscription =>
        {
            var row = mapper.Map<CalendarSubscriptionViewModel>(subscription);
            row.HttpsAddress = CalendarSubscriptionAddress.BuildHttps(emailSettings.Value.AppUrl, subscription.Token);
            row.WebcalAddress = CalendarSubscriptionAddress.BuildWebcal(emailSettings.Value.AppUrl, subscription.Token);

            // The code encodes the calendar-handoff form rather than the plain web form: scanning
            // it hands off to the phone's calendar application and subscribes, whereas the plain
            // web form opens a browser that downloads the document as a one-time import rather
            // than a subscription. The handoff scheme is not honoured everywhere, which is exactly
            // why the row also offers the plain address and a manual-entry fallback next to it.
            row.QrCodeSvg = CalendarSubscriptionQrCode.ToSvg(row.WebcalAddress);
            if (row.QrCodeSvg == null)
            {
                logger.LogWarning("Calendar subscription QR code generation failed for subscription {SubscriptionId}.", subscription.Id);
            }

            return row;
        }).ToList();

        var model = new ProfileViewModel
        {
            User = user,
            CalendarSubscriptions = subscriptionRows
        };

        ViewData["IsDungeonMaster"] = role == GroupRole.DungeonMaster || role == GroupRole.Admin;
        ViewData["IsAdmin"] = role == GroupRole.Admin;

        return View(model);
    }

    [HttpGet]
    [Authorize]
    public async Task<IActionResult> Edit()
    {
        var user = await userService.GetUserAsync(User);

        GroupRole? role = null;
        if (activeGroupContext.ActiveGroupId is { } groupId)
        {
            role = await userService.GetEffectiveGroupRoleAsync(User, groupId);
        }

        var model = new EditProfileViewModel
        {
            Id = user.Id,
            Name = user.Name,
            Email = user.Email,
            IsDungeonMaster = role == GroupRole.DungeonMaster || role == GroupRole.Admin,
            HasKey = user.HasKey
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize]
    public async Task<IActionResult> Edit(EditProfileViewModel model)
    {
        if (ModelState.IsValid)
        {
            var user = await userService.GetUserAsync(User);

            var emailChanged = !string.Equals(user.Email, model.Email, StringComparison.OrdinalIgnoreCase);

            user.Name = model.Name;
            user.HasKey = model.HasKey;
            if (!emailChanged)
                user.Email = model.Email;

            // Role changes are now handled only through Admin User Management

            await userService.UpdateAsync(user);

            if (emailChanged && !string.IsNullOrEmpty(model.Email))
            {
                var rawToken = await identityService.GenerateChangeEmailTokenAsync(user.Id, model.Email);
                if (rawToken != null)
                {
                    var encodedToken = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(rawToken));
                    var callbackUrl = Url.Action(nameof(ConfirmEmailChange), "Account",
                        new { userId = user.Id, newEmail = model.Email, token = encodedToken }, Request.Scheme);
                    if (callbackUrl == null)
                    {
                        logger.LogError("Failed to generate ConfirmEmailChange callback URL for userId {UserId}", user.Id);
                    }
                    else
                    {
                        jobClient.Enqueue<ChangeEmailConfirmationJob>(j => j.ExecuteAsync(model.Email, user.Name, callbackUrl, CancellationToken.None));
                        TempData["Info"] = $"A confirmation email has been sent to {model.Email}. Click the link to complete the change.";
                        return RedirectToAction(nameof(Profile));
                    }
                }
            }

            TempData["Success"] = "Profile updated successfully!";
            return RedirectToAction(nameof(Profile));
        }

        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> ConfirmEmailChange(int userId, string newEmail, string token)
    {
        try
        {
            var decodedToken = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(token));
            var result = await identityService.ChangeEmailAsync(userId, newEmail, decodedToken);

            if (result.Succeeded)
                TempData["Success"] = "Email address updated. Please sign in with your new address.";
            else
                TempData["Error"] = "Email confirmation failed. The link may have expired.";
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "ConfirmEmailChange failed for userId {UserId}", userId);
            TempData["Error"] = "Email confirmation failed. The link may have expired.";
        }

        return RedirectToAction(nameof(Login));
    }

    [HttpGet]
    [Authorize]
    public IActionResult ChangePassword()
    {
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize]
    public async Task<IActionResult> ChangePassword(ChangePasswordViewModel model)
    {
        if (ModelState.IsValid)
        {
            var result = await userService.ChangePasswordAsync(User, model.CurrentPassword, model.NewPassword);

            if (result.Succeeded)
            {
                TempData["Success"] = "Password changed successfully!";
                return RedirectToAction(nameof(Profile));
            }

            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }
        }

        return View(model);
    }

    [HttpPost]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddCalendarSubscription(CancellationToken token = default)
    {
        var user = await userService.GetUserAsync(User);

        // The control that triggers this is a form submit, so a double click, a browser retry
        // or a replayed request would otherwise mint a second credential that never expires from
        // a single intent, leaving the member with an extra live address they never asked for.
        // Ten seconds is long enough to absorb a double submit and short enough not to obstruct
        // someone deliberately adding a second device.
        var existingSubscriptions = await calendarSubscriptionService.GetForUserAsync(user.Id, token);
        var mostRecent = existingSubscriptions.OrderByDescending(s => s.CreatedAt).FirstOrDefault();
        if (mostRecent != null && DateTime.UtcNow - mostRecent.CreatedAt < TimeSpan.FromSeconds(10))
        {
            TempData["Error"] = "Couldn't add this subscription. Try again, and if it keeps happening, let a Dungeon Master know.";
            return RedirectToAction(nameof(Profile));
        }

        await calendarSubscriptionService.MintForUserAsync(user.Id, token);
        TempData["Success"] = "Subscription added.";
        return RedirectToAction(nameof(Profile));
    }

    [HttpPost]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RenameCalendarSubscription(int id, string name, CancellationToken token = default)
    {
        var user = await userService.GetUserAsync(User);

        // Model binding's ConvertEmptyStringToNull treats a whitespace-only submission the same
        // as an absent field, handing this action a null name rather than a string of spaces --
        // the null-conditional keeps that case on the same "refused as empty" path below instead
        // of throwing before the check ever runs.
        var trimmedName = name?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(trimmedName) || trimmedName.Length > 60)
        {
            TempData["Error"] = "Couldn't rename this subscription. Try again, and if it keeps happening, let a Dungeon Master know.";
            return RedirectToAction(nameof(Profile));
        }

        // The service and repository match on the subscription id and the member id together,
        // so an id belonging to someone else simply matches nothing. A false result below is
        // reported as one neutral failure, never distinguishing "not yours" from "not found" --
        // a message that did would leak the existence of another member's subscription.
        var renamed = await calendarSubscriptionService.RenameAsync(id, user.Id, trimmedName, token);
        if (renamed)
        {
            TempData["Success"] = "Subscription renamed.";
        }
        else
        {
            TempData["Error"] = "Couldn't rename this subscription. Try again, and if it keeps happening, let a Dungeon Master know.";
        }

        return RedirectToAction(nameof(Profile));
    }

    [HttpPost]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RevokeCalendarSubscription(int id, CancellationToken token = default)
    {
        var user = await userService.GetUserAsync(User);

        // The visible control for this action is labelled Delete even though what it performs is
        // a retirement -- the action name says revoke because that is what the code does. The
        // label is fixed by the copy contract and the behaviour is fixed by the tombstone
        // decision; do not rename one to match the other. As with rename above, a false result
        // is reported as one neutral failure that does not confirm whether the id exists at all.
        var revoked = await calendarSubscriptionService.RevokeAsync(id, user.Id, token);
        if (revoked)
        {
            TempData["Success"] = "Subscription deleted.";
        }
        else
        {
            TempData["Error"] = "Couldn't delete this subscription. Try again, and if it keeps happening, let a Dungeon Master know.";
        }

        return RedirectToAction(nameof(Profile));
    }
}