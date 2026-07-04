# Phase 47: Group Membership Email Notification Fix - Pattern Map

**Mapped:** 2026-07-05
**Files analyzed:** 1 (single file modified — no new files)
**Analogs found:** 1 / 1 (in-file analog, same class)

This is a targeted bug fix, not a new-file phase. There is exactly one file to modify (`GroupController.AddMember`), and its analog (`CreateMember`) lives in the same file, a few lines below it. No other files change — `UserService.CreateOrAddToGroupAsync`, `GroupMembershipAddedEmailJob`, `AddMemberViewModel`, and `UserRepository.GetAvailableUsers` are all consumed as-is (read-only reference), not modified.

## File Classification

| New/Modified File | Role | Data Flow | Closest Analog | Match Quality |
|--------------------|------|-----------|-----------------|----------------|
| `QuestBoard.Service/Areas/Platform/Controllers/GroupController.cs` (`AddMember` action, lines 143-163) | controller (MVC action) | request-response + event-driven (enqueues background email job) | `GroupController.CreateMember` (same file, lines 165-265) | exact — same controller, same shared service call, same outcome enum, only difference is input source (`UserId` vs `Email`+`Name`) |

**Consumed unchanged (no modification, reference only):**

| File | Role | Why it matters |
|------|------|-----------------|
| `QuestBoard.Domain/Services/UserService.cs` — `CreateOrAddToGroupAsync` (lines 161-232) | service | The shared method `AddMember` must call; already handles existing-user add + confirmed/stranded/already-member branching |
| `QuestBoard.Service/Jobs/GroupMembershipAddedEmailJob.cs` | job (event-driven, Hangfire) | Enqueued for the confirmed-account (`AddedToGroup`) outcome |
| `QuestBoard.Service/ViewModels/PlatformViewModels/AddMemberViewModel.cs` | view model | Unchanged; only carries `UserId` + `Role` — email/name must be resolved via `userService.GetByIdAsync` before calling the shared method |
| `QuestBoard.Repository/UserRepository.cs` — `GetAvailableUsers` (lines 62-75) | repository (CRUD/query) | Confirms available-users list is not filtered by `EmailConfirmed`, so stranded-account path is reachable |

## Pattern Assignments

### `GroupController.AddMember` (controller action, request-response + event-driven)

**Analog:** `GroupController.CreateMember` (same file, lines 165-265) — the direct, in-file pattern to replicate. This is the strongest possible analog since it is the same controller, same constructor dependencies (`userService`, `identityService`, `jobClient`, `logger`), and already implements the exact outcome-switch this fix needs to add to `AddMember`.

**Current (buggy) implementation to replace** (lines 143-163):
```csharp
[HttpPost]
[ValidateAntiForgeryToken]
public async Task<IActionResult> AddMember(int id, AddMemberViewModel model, string? search, string? memberSearch)
{
    if (!ModelState.IsValid)
    {
        TempData["Error"] = "Invalid form submission.";
        return RedirectToAction(nameof(Members), new { id, search, memberSearch });
    }
    try
    {
        await groupService.AddMemberAsync(id, model.UserId, model.Role);
        var user = await userService.GetByIdAsync(model.UserId);
        TempData["Success"] = $"{user?.Name ?? "User"} added to the group as {model.Role}.";
    }
    catch (InvalidOperationException)
    {
        TempData["Error"] = "This user is already a member of the group.";
    }
    return RedirectToAction(nameof(Members), new { id, search, memberSearch });
}
```

Note: `AddMember` doesn't currently load `group` at all (no `groupService.GetByIdAsync(id)` call) — but `group.Name` is required for `GroupMembershipAddedEmailJob`'s `groupName` parameter, so this must be added, matching `CreateMember`'s first line (line 169-170).

**Resolving email/name before the shared call** — `AddMember` only has `UserId`, so it must resolve the user first (this replaces the old post-hoc `userService.GetByIdAsync(model.UserId)` used only for the toast):
```csharp
var user = await userService.GetByIdAsync(model.UserId);
if (user == null)
{
    TempData["Error"] = "User not found.";
    return RedirectToAction(nameof(Members), new { id, search, memberSearch });
}
var result = await userService.CreateOrAddToGroupAsync(user.Email!, user.Name, id, model.Role);
```
(`user.Email`/`user.Name` feed `CreateOrAddToGroupAsync` exactly the way `model.Email`/`model.Name` do in `CreateMember` — `CreateOrAddToGroupAsync` re-resolves the existing user's real name/email internally anyway per `UserService.cs` lines 202-204, so the values passed in for an existing user are effectively just lookup keys.)

**Outcome-switch pattern to copy** (`CreateMember`, lines 189-264) — for `AddMember`, only three arms are reachable (`NewAccountCreated` is dead code since `AddMember` always operates on a pre-existing `UserId`; still handle it defensively via `default`/`Failed` per D-02):

Confirmed-account arm — copy verbatim, this is the fix's core deliverable (lines 208-218):
```csharp
case CreateOrAddToGroupOutcome.AddedToGroup:
    {
        var loginUrl = Url.Action("Login", "Account", null, Request.Scheme);
        if (loginUrl == null)
            logger.LogError("Failed to generate Login callback URL for userId {UserId}", result.UserId);
        else
            jobClient.Enqueue<GroupMembershipAddedEmailJob>(j => j.ExecuteAsync(result.Email, result.Name, group.Name, model.Role.ToString(), loginUrl, CancellationToken.None));

        TempData["Success"] = $"{result.Name} has been added to the group as {model.Role}. A notification email has been sent.";
        return RedirectToAction(nameof(Members), new { id, search, memberSearch });
    }
```
(Note: substitute `model.GroupRole` → `model.Role` since `AddMemberViewModel.Role` is the field name, not `GroupRole` as in `CreateMemberViewModel`.)

Stranded-account arm — copy verbatim including the `SetPassword` callback URL construction (lines 220-238):
```csharp
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

        TempData["Success"] = $"{result.Name} has been added to the group as {model.Role}. A notification email has been sent.";
        return RedirectToAction(nameof(Members), new { id, search, memberSearch });
    }
```

Already-member arm — replaces the old `catch (InvalidOperationException)` block (line 240-242 in `CreateMember`):
```csharp
case CreateOrAddToGroupOutcome.AlreadyMember:
    TempData["Warning"] = $"{result.Name} is already a member of this group.";
    return RedirectToAction(nameof(Members), new { id, search, memberSearch });
```

Failed arm (defensive, unreachable `NewAccountCreated` folds into `default` per D-02) — `CreateMember`'s failed arm (lines 244-263) re-renders the `Members` view with `ModelState` errors; `AddMember` posts from the same `Members` page but has no dedicated form view to redisplay with field-level errors (`AddMemberViewModel` has no partial view), so use `TempData["Error"]` + redirect instead of a `View()` return, consistent with `AddMember`'s existing error-handling convention (line 149-150, 158-160) rather than `CreateMember`'s convention. This is a deliberate deviation from the analog, driven by `AddMember`'s existing UX pattern:
```csharp
case CreateOrAddToGroupOutcome.Failed:
default:
    TempData["Error"] = result.Errors.Count > 0 ? string.Join(" ", result.Errors) : "Failed to add user to group.";
    return RedirectToAction(nameof(Members), new { id, search, memberSearch });
```

**Imports needed** — `AddMember`'s current imports already cover everything (`Hangfire`, `Microsoft.AspNetCore.WebUtilities`, `System.Text`, `QuestBoard.Service.Jobs`, `QuestBoard.Domain.Enums`) since they're file-level `using`s shared with `CreateMember` (lines 1-13). No new imports required — this is a single-file change.

---

## Shared Patterns

### Outcome-switch / shared-service-call pattern
**Source:** `GroupController.CreateMember`, lines 187-264
**Apply to:** `AddMember` (the only file being changed)
This is the canonical "third caller of `CreateOrAddToGroupAsync`" pattern per D-01/D-03 — same switch over `CreateOrAddToGroupOutcome`, same job-enqueue calls, same callback-URL construction via `Url.Action` + `WebEncoders.Base64UrlEncode`.

### Toast/TempData copy pattern
**Source:** `GroupController.CreateMember`, lines 204, 216, 236, 241
**Apply to:** `AddMember`'s success/warning messages (D-06) — reuse the "{name} has been added to the group as {role}. A notification email has been sent." phrasing verbatim for the confirmed and stranded cases, and "{name} is already a member of this group." for `AlreadyMember`.

## No Analog Found

None — the single file being modified has a strong in-file analog (`CreateMember`) covering every code path needed.

## Metadata

**Analog search scope:** `QuestBoard.Service/Areas/Platform/Controllers/GroupController.cs` (single file — both target and analog); `QuestBoard.Domain/Services/UserService.cs`; `QuestBoard.Service/Jobs/GroupMembershipAddedEmailJob.cs`; `QuestBoard.Service/ViewModels/PlatformViewModels/AddMemberViewModel.cs`; `QuestBoard.Repository/UserRepository.cs`
**Files scanned:** 5 (all provided by CONTEXT.md canonical refs; no additional Glob/Grep search needed given exact file/line references already supplied)
**Pattern extraction date:** 2026-07-05
