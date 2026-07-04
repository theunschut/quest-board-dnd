using Hangfire;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using QuestBoard.Domain.Enums;
using QuestBoard.Domain.Interfaces;
using QuestBoard.Domain.Models;
using QuestBoard.Service.Extensions;
using QuestBoard.Service.Jobs;
using QuestBoard.Service.ViewModels.PlatformViewModels;
using System.Text;

namespace QuestBoard.Service.Areas.Platform.Controllers;

[Area("Platform")]
[Authorize(Policy = "SuperAdminOnly")]
public class GroupController(
    IGroupService groupService,
    IUserService userService,
    IIdentityService identityService,
    IBackgroundJobClient jobClient,
    ILogger<GroupController> logger) : Controller
{
    // Groups index
    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var groups = await groupService.GetAllWithMemberCountAsync();
        return View(new GroupListViewModel { Groups = groups });
    }

    // Create group
    [HttpGet]
    public IActionResult Create() => View(new GroupCreateViewModel());

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(GroupCreateViewModel model)
    {
        if (!ModelState.IsValid) return View(model);
        try
        {
            await groupService.AddAsync(new Group { Name = model.Name, BoardType = model.BoardType!.Value });
            TempData["Success"] = "Group created successfully.";
            return RedirectToAction(nameof(Index));
        }
        catch (DbUpdateException ex) when (
            ex.InnerException?.Message.Contains("unique", StringComparison.OrdinalIgnoreCase) == true ||
            ex.InnerException?.Message.Contains("duplicate", StringComparison.OrdinalIgnoreCase) == true)
        {
            ModelState.AddModelError(nameof(model.Name), "A group with that name already exists. Please choose a different name.");
            return View(model);
        }
    }

    // Edit group
    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var group = await groupService.GetByIdAsync(id);
        if (group == null) return RedirectToAction(nameof(Index));
        return View(new GroupEditViewModel { Id = group.Id, Name = group.Name, BoardType = group.BoardType });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(GroupEditViewModel model)
    {
        if (!ModelState.IsValid) return View(model);
        var group = await groupService.GetByIdAsync(model.Id);
        if (group == null) return RedirectToAction(nameof(Index));
        try
        {
            group.Name = model.Name;
            await groupService.UpdateAsync(group);
            TempData["Success"] = "Group name updated.";
            return RedirectToAction(nameof(Index));
        }
        catch (DbUpdateException ex) when (
            ex.InnerException?.Message.Contains("unique", StringComparison.OrdinalIgnoreCase) == true ||
            ex.InnerException?.Message.Contains("duplicate", StringComparison.OrdinalIgnoreCase) == true)
        {
            ModelState.AddModelError(nameof(model.Name), "A group with that name already exists. Please choose a different name.");
            return View(model);
        }
    }

    // Delete group (only empty groups)
    [HttpGet]
    public async Task<IActionResult> Delete(int id)
    {
        var group = await groupService.GetByIdAsync(id);
        if (group == null) return RedirectToAction(nameof(Index));
        var hasMembers = await groupService.HasMembersAsync(id);
        if (hasMembers)
        {
            TempData["Error"] = "Cannot delete a group that has members.";
            return RedirectToAction(nameof(Index));
        }
        return View(group);
    }

    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var group = await groupService.GetByIdAsync(id);
        var hasMembers = await groupService.HasMembersAsync(id);
        if (hasMembers)
        {
            TempData["Error"] = "Cannot delete a group that has members.";
            return RedirectToAction(nameof(Index));
        }
        if (group != null)
        {
            await groupService.RemoveAsync(group);
            TempData["Success"] = "Group deleted.";
        }
        return RedirectToAction(nameof(Index));
    }

    // Members page
    [HttpGet]
    public async Task<IActionResult> Members(int id, string? search, string? memberSearch)
    {
        var group = await groupService.GetByIdAsync(id);
        if (group == null) return RedirectToAction(nameof(Index));
        var members = await groupService.GetMembersAsync(id, memberSearch);
        var availableUsers = await userService.GetAvailableUsersAsync(id, search);
        return View(new GroupMembersViewModel
        {
            Group = group,
            Members = members,
            AvailableUsers = availableUsers,
            SearchQuery = search,
            MemberSearchQuery = memberSearch,
            AddMember = new AddMemberViewModel()
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddMember(int id, AddMemberViewModel model, string? search, string? memberSearch)
    {
        if (!ModelState.IsValid)
        {
            TempData["Error"] = "Invalid form submission.";
            return RedirectToAction(nameof(Members), new { id, search, memberSearch });
        }

        var group = await groupService.GetByIdAsync(id);
        if (group == null) return RedirectToAction(nameof(Index));

        var user = await userService.GetByIdAsync(model.UserId);
        if (user == null)
        {
            TempData["Error"] = "User not found.";
            return RedirectToAction(nameof(Members), new { id, search, memberSearch });
        }

        if (string.IsNullOrWhiteSpace(user.Email))
        {
            TempData["Error"] = "Selected user has no email address on file and cannot be added.";
            return RedirectToAction(nameof(Members), new { id, search, memberSearch });
        }

        var result = await userService.CreateOrAddToGroupAsync(user.Email, user.Name, id, model.Role);

        switch (result.Outcome)
        {
            case CreateOrAddToGroupOutcome.AddedToGroup:
                {
                    // Existing user added to a group they weren't in — notify them by email.
                    var loginUrl = Url.Action("Login", "Account", null, Request.Scheme);
                    if (loginUrl == null)
                        logger.LogError("Failed to generate Login callback URL for userId {UserId}", result.UserId);
                    else
                        jobClient.Enqueue<GroupMembershipAddedEmailJob>(j => j.ExecuteAsync(result.Email, result.Name, group.Name, model.Role.ToString(), loginUrl, CancellationToken.None));

                    TempData["Success"] = $"{result.Name} has been added to the group as {model.Role}. A notification email has been sent.";
                    return RedirectToAction(nameof(Members), new { id, search, memberSearch });
                }

            case CreateOrAddToGroupOutcome.AddedToGroupStrandedAccount:
                {
                    // Account was never confirmed — send a welcome email with a set-password link instead.
                    var rawToken = await identityService.GeneratePasswordResetTokenForUserAsync(result.UserId!.Value);
                    if (rawToken != null)
                    {
                        var encodedToken = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(rawToken));
                        var callbackUrl = Url.Action("SetPassword", "Account", new { userId = result.UserId.Value, token = encodedToken }, Request.Scheme);
                        if (callbackUrl == null)
                            logger.LogError("Failed to generate SetPassword callback URL for userId {UserId}", result.UserId.Value);
                        else
                        {
                            var hasExistingPassword = await userService.HasPasswordAsync(result.UserId.Value);
                            jobClient.Enqueue<WelcomeEmailJob>(j => j.ExecuteAsync(result.Email, result.Name, callbackUrl, !hasExistingPassword, CancellationToken.None));
                        }
                    }

                    TempData["Success"] = $"{result.Name} has been added to the group as {model.Role}. A notification email has been sent.";
                    return RedirectToAction(nameof(Members), new { id, search, memberSearch });
                }

            case CreateOrAddToGroupOutcome.AlreadyMember:
                TempData["Warning"] = $"{result.Name} is already a member of this group.";
                return RedirectToAction(nameof(Members), new { id, search, memberSearch });

            case CreateOrAddToGroupOutcome.Failed:
            default:
                // No dedicated form partial to redisplay with field-level errors here, so fall
                // back to a toast + redirect (matching this action's existing error convention).
                TempData["Error"] = result.Errors.Count > 0 ? string.Join(" ", result.Errors) : "Failed to add user to group.";
                return RedirectToAction(nameof(Members), new { id, search, memberSearch });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateMember(int id, CreateMemberViewModel model, string? search, string? memberSearch)
    {
        var group = await groupService.GetByIdAsync(id);
        if (group == null) return RedirectToAction(nameof(Index));

        if (!ModelState.IsValid)
        {
            var invalidMembers = await groupService.GetMembersAsync(id, memberSearch);
            var invalidAvailableUsers = await userService.GetAvailableUsersAsync(id, search);
            return View(nameof(Members), new GroupMembersViewModel
            {
                Group = group,
                Members = invalidMembers,
                AvailableUsers = invalidAvailableUsers,
                SearchQuery = search,
                MemberSearchQuery = memberSearch,
                CreateMember = model
            });
        }

        var result = await userService.CreateOrAddToGroupAsync(model.Email, model.Name, id, model.GroupRole);

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
                            logger.LogError("Failed to generate SetPassword callback URL for userId {UserId}", result.UserId.Value);
                        else
                            jobClient.Enqueue<WelcomeEmailJob>(j => j.ExecuteAsync(model.Email, model.Name, callbackUrl, true, CancellationToken.None));
                    }

                    TempData["Success"] = $"Account created for {model.Name}. A welcome email with a set-password link has been sent.";
                    return RedirectToAction(nameof(Members), new { id, search, memberSearch });
                }

            case CreateOrAddToGroupOutcome.AddedToGroup:
                {
                    var loginUrl = Url.Action("Login", "Account", null, Request.Scheme);
                    if (loginUrl == null)
                        logger.LogError("Failed to generate Login callback URL for userId {UserId}", result.UserId);
                    else
                        jobClient.Enqueue<GroupMembershipAddedEmailJob>(j => j.ExecuteAsync(result.Email, result.Name, group.Name, model.GroupRole.ToString(), loginUrl, CancellationToken.None));

                    TempData["Success"] = $"{result.Name} has been added to the group as {model.GroupRole}. A notification email has been sent.";
                    return RedirectToAction(nameof(Members), new { id, search, memberSearch });
                }

            case CreateOrAddToGroupOutcome.AddedToGroupStrandedAccount:
                {
                    var rawToken = await identityService.GeneratePasswordResetTokenForUserAsync(result.UserId!.Value);
                    if (rawToken != null)
                    {
                        var encodedToken = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(rawToken));
                        var callbackUrl = Url.Action("SetPassword", "Account", new { userId = result.UserId.Value, token = encodedToken }, Request.Scheme);
                        if (callbackUrl == null)
                            logger.LogError("Failed to generate SetPassword callback URL for userId {UserId}", result.UserId.Value);
                        else
                        {
                            var hasExistingPassword = await userService.HasPasswordAsync(result.UserId.Value);
                            jobClient.Enqueue<WelcomeEmailJob>(j => j.ExecuteAsync(result.Email, result.Name, callbackUrl, !hasExistingPassword, CancellationToken.None));
                        }
                    }

                    TempData["Success"] = $"{result.Name} has been added to the group as {model.GroupRole}. A notification email has been sent.";
                    return RedirectToAction(nameof(Members), new { id, search, memberSearch });
                }

            case CreateOrAddToGroupOutcome.AlreadyMember:
                TempData["Warning"] = $"{result.Name} is already a member of this group.";
                return RedirectToAction(nameof(Members), new { id, search, memberSearch });

            case CreateOrAddToGroupOutcome.Failed:
            default:
                {
                    foreach (var error in result.Errors)
                    {
                        ModelState.AddModelError(string.Empty, error);
                    }

                    var failedMembers = await groupService.GetMembersAsync(id, memberSearch);
                    var failedAvailableUsers = await userService.GetAvailableUsersAsync(id, search);
                    return View(nameof(Members), new GroupMembersViewModel
                    {
                        Group = group,
                        Members = failedMembers,
                        AvailableUsers = failedAvailableUsers,
                        SearchQuery = search,
                        MemberSearchQuery = memberSearch,
                        CreateMember = model
                    });
                }
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveMember(int id, int userId, string? search, string? memberSearch)
    {
        await groupService.RemoveMemberAsync(id, userId);
        TempData["Success"] = "Member removed from the group.";
        return RedirectToAction(nameof(Members), new { id, search, memberSearch });
    }
}
