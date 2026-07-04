---
phase: 41-safe-user-removal-account-disable
reviewed: 2026-07-04T13:11:37Z
depth: standard
files_reviewed: 16
files_reviewed_list:
  - QuestBoard.Domain/Interfaces/IIdentityService.cs
  - QuestBoard.IntegrationTests/Controllers/AccountControllerIntegrationTests.cs
  - QuestBoard.IntegrationTests/Controllers/AdminControllerIntegrationTests.cs
  - QuestBoard.IntegrationTests/Controllers/UsersControllerIntegrationTests.cs
  - QuestBoard.Repository/IdentityService.cs
  - QuestBoard.Service/Areas/Platform/Controllers/UsersController.cs
  - QuestBoard.Service/Areas/Platform/Views/Group/Index.Mobile.cshtml
  - QuestBoard.Service/Areas/Platform/Views/Group/Index.cshtml
  - QuestBoard.Service/Areas/Platform/Views/Users/Index.Mobile.cshtml
  - QuestBoard.Service/Areas/Platform/Views/Users/Index.cshtml
  - QuestBoard.Service/Controllers/Admin/AccountController.cs
  - QuestBoard.Service/Controllers/Admin/AdminController.cs
  - QuestBoard.Service/Program.cs
  - QuestBoard.Service/ViewModels/PlatformViewModels/PlatformUserViewModel.cs
  - QuestBoard.Service/Views/Admin/Users.Mobile.cshtml
  - QuestBoard.Service/Views/Admin/Users.cshtml
findings:
  critical: 0
  warning: 2
  info: 2
  total: 4
status: issues_found
---

# Phase 41: Code Review Report

**Reviewed:** 2026-07-04T13:11:37Z
**Depth:** standard
**Files Reviewed:** 16
**Status:** issues_found

## Summary

This phase replaces the group-admin "Delete User" hard-delete (which cascaded a user out of every
group and threw `DbUpdateException` for users with quest/shop/transaction/reminder history) with a
reversible, group-scoped `RemoveMemberAsync` call, and adds a new SuperAdmin-only Platform-area
"Disable/Enable Account" feature built on ASP.NET Core Identity's native `LockoutEnd`/`SecurityStamp`
primitives.

I traced the new `IIdentityService.DisableUserAsync` / `EnableUserAsync` / `GetLockoutEndAsync`
methods against their `UserManager<UserEntity>` calls, the `AdminController.DeleteUser` behavior
change against `GroupService.RemoveMemberAsync`, the `AccountController.Login` lockout-message
branching, and the new `UsersController` self-disable guard. The core security-relevant logic
(exact `DateTimeOffset.MaxValue` sentinel comparison, never touching `LockoutEnabled`, security-stamp
invalidation on disable but not enable, self-disable guard using `int?`/`int` comparison, class-level
`SuperAdminOnly` policy) is implemented correctly and matches the phase's own design/pitfalls
research. Test coverage (`UsersControllerIntegrationTests`, the `AdminControllerIntegrationTests`
`DeleteUser_*` tests, and the `AccountControllerIntegrationTests` lockout-message tests) is thorough
and exercises the actual DB-level side effects, not just HTTP status codes.

Two real (non-security) defects were found: a missing mobile stylesheet for the new Platform Users
page (the page will render with none of its custom card/badge styling on mobile), and both new
`Disable`/`Enable` controller actions discard the `IdentityResult` returned by the identity service
and unconditionally show a success message, so a failure (e.g. target user no longer exists) is
silently reported as success to the SuperAdmin.

## Warnings

### WR-01: Disable/Enable controller actions silently report success even when the operation fails

**File:** `QuestBoard.Service/Areas/Platform/Controllers/UsersController.cs:40-42` and `:47-51`
**Issue:** `Disable` and `Enable` both call `identityService.DisableUserAsync(userId)` /
`EnableUserAsync(userId)`, which return `IdentityResult.Failed(...)` when `FindByIdAsync` can't
resolve the target `userId` (see `QuestBoard.Repository/IdentityService.cs:184-207`). The controller
discards the returned `IdentityResult` entirely and always sets `TempData["Success"] = "Account
disabled."` / `"Account re-enabled."`, regardless of whether the target user actually existed. A
stale page (target deleted/renamed between page load and submit), a tampered `userId` form field, or
any other resolution failure will show a false "success" to the SuperAdmin with no actual state
change and no error surfaced.
**Fix:**
```csharp
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

    var result = await identityService.DisableUserAsync(userId);
    TempData[result.Succeeded ? "Success" : "Error"] =
        result.Succeeded ? "Account disabled." : "Failed to disable account. The user may no longer exist.";
    return RedirectToAction(nameof(Index));
}
```
Apply the equivalent change to `Enable`.

### WR-02: New Platform Users mobile view has no stylesheet — renders unstyled

**File:** `QuestBoard.Service/Areas/Platform/Views/Users/Index.Mobile.cshtml:7,36`
**Issue:** The view uses custom classes `platform-users-card-mobile` (line 7) and `user-card-mobile`
(line 36), but never includes an `@section Styles { ... }` block to load a matching stylesheet — every
other Platform-area mobile view in this same diff follows the pattern of pairing a `*-card-mobile`
wrapper class with a dedicated `@section Styles` stylesheet (see
`Areas/Platform/Views/Group/Index.Mobile.cshtml:6-8`, which loads `platform-group.mobile.css` and
defines `.platform-group-card-mobile`/`.group-card-mobile`). No CSS file anywhere in
`wwwroot/css` defines `.platform-users-card-mobile`, and `.user-card-mobile` is only defined in
`wwwroot/css/admin-users.mobile.css` (loaded by the unrelated `Views/Admin/Users.Mobile.cshtml`, not
this file). The result: on mobile, the new Users management page (badges, card spacing,
`parchment-text` styling, borders) will render as unstyled default HTML instead of matching the rest
of the Platform area's mobile UI.
**Fix:** Add a dedicated stylesheet (e.g. `wwwroot/css/platform-users.mobile.css`) defining
`.platform-users-card-mobile` / `.user-card-mobile`, and load it the same way the Group view does:
```html
@section Styles {
    <link href="~/css/platform-users.mobile.css" asp-append-version="true" rel="stylesheet" />
}
```

## Info

### IN-01: Login lockout-message branch adds a minor account-existence oracle for authenticated-lockout state

**File:** `QuestBoard.Service/Controllers/Admin/AccountController.cs:137-153`
**Issue:** The new code distinguishes "This account has been disabled. Contact an administrator." from
"Account locked due to too many failed attempts. Try again in 15 minutes." This branch only executes
after `result.IsLockedOut`, which itself already only fires for an account that exists (an unknown
email always falls through to the generic "Invalid login attempt." message), so this is not a new
account-enumeration vector — existence was already disclosed by the pre-existing lockout path. It
does, however, give a would-be attacker who has triggered lockout (e.g. via repeated guesses) an
extra bit of information about *why* the account is unavailable. The phase's own research doc
explicitly accepts this as intentional and low-risk (post-authentication-attempt state, not a
pre-auth oracle). No fix required; noting for the record since it is new behavior introduced by this
diff.
**Fix:** None required — documented, accepted tradeoff. If the exposure is ever revisited, both
branches could be flattened to a single generic "Account unavailable, contact an administrator."
message.

### IN-02: Repeated identical role-check/redirect boilerplate not part of this diff, worth folding in later

**File:** `QuestBoard.Service/Controllers/Admin/AdminController.cs:343-354` (and similarly
`EditUser`, `ResetPassword`, `SendConfirmationEmail`, role-change actions elsewhere in the same file)
**Issue:** `DeleteUser`'s group-scoping guard (`groupId == null` → `NotFound()`, then
`GetGroupRoleByIdAsync` → `NotFound()`, then `GetByIdAsync` → `NotFound()`) duplicates the identical
three-step pattern already present in five other actions in this controller. This diff only changed
the line after the guard (`RemoveAsync` → `RemoveMemberAsync`), so the duplication itself pre-dates
this phase, but since `DeleteUser` was touched here it's a reasonable opportunity to note for a future
cleanup pass (e.g. a private `ResolveGroupScopedTargetAsync(int id)` helper returning the guard result
or a `NotFound()`).
**Fix:** Non-blocking; consider extracting a shared private helper in a future refactor, not this
phase.

---

_Reviewed: 2026-07-04T13:11:37Z_
_Reviewer: Claude (gsd-code-reviewer)_
_Depth: standard_
