---
phase: 37-navigation-access-control
reviewed: 2026-07-03T00:00:00Z
depth: standard
files_reviewed: 16
files_reviewed_list:
  - QuestBoard.Domain/Interfaces/IActiveGroupContext.cs
  - QuestBoard.Domain/Interfaces/IBoardTypeResolver.cs
  - QuestBoard.IntegrationTests/Controllers/AccountControllerIntegrationTests.cs
  - QuestBoard.IntegrationTests/Controllers/AdminControllerIntegrationTests.cs
  - QuestBoard.IntegrationTests/Controllers/LayoutNavigationTests.cs
  - QuestBoard.IntegrationTests/Helpers/MutableGroupContext.cs
  - QuestBoard.IntegrationTests/WebApplicationFactoryBase.cs
  - QuestBoard.Service/Controllers/Admin/AccountController.cs
  - QuestBoard.Service/Controllers/Admin/AdminController.cs
  - QuestBoard.Service/Program.cs
  - QuestBoard.Service/Services/ActiveGroupContextService.cs
  - QuestBoard.Service/Services/BoardTypeResolver.cs
  - QuestBoard.Service/Views/Shared/AccessDenied.cshtml
  - QuestBoard.Service/Views/Shared/_Layout.Mobile.cshtml
  - QuestBoard.Service/Views/Shared/_Layout.cshtml
  - QuestBoard.UnitTests/Services/SessionReminderJobTests.cs
findings:
  critical: 0
  warning: 2
  info: 2
  total: 4
status: issues_found
---

# Phase 37: Code Review Report

**Reviewed:** 2026-07-03T00:00:00Z
**Depth:** standard
**Files Reviewed:** 16
**Status:** issues_found

## Summary

Reviewed the nav-visibility gating by `BoardType`, the SuperAdmin-only Email Stats authorization gate, the `AccessDenied` page wiring, and the mid-phase fix that split `IBoardTypeResolver` out of `IActiveGroupContext` to break a circular DI dependency.

**DI-cycle fix: verified correct and complete.** Traced the full dependency graph by hand:
- `QuestBoardContext(IActiveGroupContext)` → `IActiveGroupContext` resolves via the `Program.cs` factory to the same scoped `ActiveGroupContextService` instance, which now depends **only** on `IHttpContextAccessor` (confirmed in `ActiveGroupContextService.cs`) — no path back to `QuestBoardContext`.
- `IBoardTypeResolver` → `BoardTypeResolver(IActiveGroupContext, IGroupService)` → `IGroupService` → `GroupService(IGroupRepository)` → `GroupRepository(QuestBoardContext, IMapper)` → `QuestBoardContext(IActiveGroupContext)` — this leg terminates cleanly because `IActiveGroupContext` no longer depends on `IGroupService`. The cycle described in the commit message (`QuestBoardContext -> IActiveGroupContext -> IGroupService -> QuestBoardContext`) is genuinely broken, not just hidden.
- Lifetimes are consistent (`IGroupService`/`IGroupRepository`/`IBoardTypeResolver` all Scoped; `QuestBoardContext` Scoped via `AddDbContext`) — no captive-dependency risk.
- Confirmed via `git show 9f83d28 --stat` that the fix commit touches exactly the 10 files expected, and via `dotnet build QuestBoard.slnx` that the solution compiles cleanly (0 errors). Ran the three phase-relevant integration test classes (56 tests) and `SessionReminderJobTests` (8 tests) — all pass.
- No leftover call sites still expect `GetBoardTypeAsync` on `IActiveGroupContext` (grep confirms only `.planning/` docs reference the old shape; all `.cs`/`.cshtml` call sites correctly use `IBoardTypeResolver`).

**Authorization-policy semantics: verified correct.** `AdminController` carries a class-level `[Authorize(Policy = "AdminOnly")]`; `EmailStats` adds a method-level `[Authorize(Policy = "SuperAdminOnly")]`. ASP.NET Core's default behavior ANDs multiple `[Authorize]` attributes at different scopes (class + method) rather than letting the more specific one override — this is exercised end-to-end by `AdminControllerIntegrationTests.EmailStats_WhenAdminNotSuperAdmin_ShouldBeRejected`, which passes, confirming an Admin who is not also a SuperAdmin is correctly rejected.

**Nav-gating logic: mostly correct, but incomplete in one layout.** Desktop (`_Layout.cshtml`) gates the Email Stats and Background Jobs links behind `User.IsInRole("SuperAdmin")` as intended. The mobile layout, however, never received the corresponding nav links at all — see WR-01 below. This directly contradicts the phase's own plan (`37-03-PLAN.md`), which explicitly calls for a mobile Email Stats link gated the same way, and its recorded human-verify checkpoint claims mobile was checked and approved.

## Warnings

### WR-01: Email Stats (and Background Jobs) nav link missing entirely from mobile layout

**File:** `QuestBoard.Service/Views/Shared/_Layout.Mobile.cshtml:58-70`
**Issue:** The mobile Admin-only nav block only renders "User Management" and "Quest Management":
```cshtml
@if ((await AuthorizationService.AuthorizeAsync(User, "AdminOnly")).Succeeded)
{
    <li class="nav-item">
        <a class="nav-link" asp-controller="Admin" asp-action="Users">...</a>
    </li>
    <li class="nav-item">
        <a class="nav-link" asp-controller="Admin" asp-action="Quests">...</a>
    </li>
}
```
There is no "Email Stats" link and no "Background Jobs" link anywhere in this file (confirmed by grep — zero matches for `SuperAdmin|EmailStats|hangfire` in `_Layout.Mobile.cshtml`). The desktop layout (`_Layout.cshtml:62-77`) has both, correctly wrapped in `User.IsInRole("SuperAdmin")`.

This is a functional regression against the phase's own plan (`37-03-PLAN.md` line 157: "Email Stats mobile link (in the AdminOnly region): wrap in `@if (User.IsInRole("SuperAdmin"))`, mirroring the desktop guard and the mobile Background Jobs guard if one exists") and its acceptance checklist (line 189: "Confirm the 'Email Stats' link is absent from the Admin dropdown (desktop) and **mobile nav**... Log in as a SuperAdmin and confirm 'Email Stats' IS present [in both]"). The recorded human-verify checkpoint claims this was checked for mobile and approved, but the shipped file has no such link to check. `LayoutNavigationTests.cs` also has no test asserting Email Stats visibility for mobile (only desktop-adjacent nav items like Calendar/Shop/Players are covered by `[Theory]` cases with both user agents), so this gap has no automated regression guard either.

Server-side authorization on `AdminController.EmailStats` is intact (`[Authorize(Policy = "SuperAdminOnly")]` still enforced regardless of how the user navigates there), so this is not an authorization bypass — a SuperAdmin on mobile simply has no discoverable nav path to Email Stats or the Hangfire dashboard; they'd have to know/type the URL directly.

**Fix:** Add the missing SuperAdmin-gated links to the mobile Admin-only block, mirroring desktop:
```cshtml
@if ((await AuthorizationService.AuthorizeAsync(User, "AdminOnly")).Succeeded)
{
    <li class="nav-item">
        <a class="nav-link" asp-controller="Admin" asp-action="Users">
            <i class="fas fa-users-cog me-2"></i>User Management
        </a>
    </li>
    <li class="nav-item">
        <a class="nav-link" asp-controller="Admin" asp-action="Quests">
            <i class="fas fa-scroll me-2"></i>Quest Management
        </a>
    </li>
    @if (User.IsInRole("SuperAdmin"))
    {
        <li class="nav-item">
            <a class="nav-link" href="/hangfire">
                <i class="fas fa-tasks me-2"></i>Background Jobs
            </a>
        </li>
        <li class="nav-item">
            <a class="nav-link" asp-controller="Admin" asp-action="EmailStats">
                <i class="fas fa-envelope-open-text me-2"></i>Email Stats
            </a>
        </li>
    }
}
```
Also add a `LayoutNavigationTests` case asserting Email Stats visibility (present for SuperAdmin, absent for plain Admin) on both user agents, since no test currently guards this.

### WR-02: Integration test suite never exercises the real production DI wiring for `IActiveGroupContext`/`IBoardTypeResolver`

**File:** `QuestBoard.IntegrationTests/WebApplicationFactoryBase.cs:69-70`
**Issue:** `ConfigureTestServices` overrides both interfaces with the same `MutableGroupContext` singleton:
```csharp
services.AddSingleton<IActiveGroupContext>(TestGroupContext);
services.AddSingleton<IBoardTypeResolver>(TestGroupContext);
```
This is necessary and reasonable for test isolation, but it means the entire integration test suite — including the tests specifically added to prove the nav-gating and DI-cycle fix work (`LayoutNavigationTests`, etc.) — never actually resolves `Program.cs`'s real `AddScoped<ActiveGroupContextService>()` / `AddScoped<IBoardTypeResolver, BoardTypeResolver>()` registrations end-to-end. The only proof the production DI graph builds without the cycle is (a) `dotnet build` succeeding (which only checks compile-time, not the runtime container graph — the incident this phase fixed was explicitly a *runtime* cycle that the compiler/analyzer did not catch) and (b) manual/human verification. There is no automated test (e.g. a minimal `WebApplicationFactory<Program>` variant that does *not* override the DI registrations, just enough to call `app.Services.CreateScope()` and resolve `QuestBoardContext`) that would fail fast if this cycle regresses in the future.

**Fix:** Consider adding a lightweight smoke test (outside `WebApplicationFactoryBase`'s test-double overrides) that boots the real `Program` DI container in the Testing environment and resolves `QuestBoardContext` and `IBoardTypeResolver` directly from a scope, asserting no `InvalidOperationException`/stack overflow. This would have caught the original regression automatically rather than requiring it to surface at app startup.

## Info

### IN-01: Unused `@using QuestBoard.Domain.Interfaces` in AccessDenied.cshtml

**File:** `QuestBoard.Service/Views/Shared/AccessDenied.cshtml:1`
**Issue:** The view imports `QuestBoard.Domain.Interfaces` but references no type from that namespace (no `IActiveGroupContext`, `IBoardTypeResolver`, `IUserService`, etc. are used in the file — those are already available globally via `_ViewImports.cshtml`).
**Fix:** Remove the unused `@using QuestBoard.Domain.Interfaces` line.

### IN-02: Redundant `[AllowAnonymous]` on `AccountController.AccessDenied`

**File:** `QuestBoard.Service/Controllers/Admin/AccountController.cs:32`
**Issue:** `AccountController` has no class-level `[Authorize]` attribute, so every action (including `AccessDenied`) is anonymous-accessible by default. The `[AllowAnonymous]` attribute on `AccessDenied` is dead weight — it documents intent but changes no behavior, since there's nothing to override. Harmless, but worth a comment instead if the intent is to guard against a future class-level `[Authorize]` being added without updating this action.
**Fix:** Either remove the attribute, or replace it with a code comment: `// AllowAnonymous kept as a guard in case a class-level [Authorize] is ever added to this controller`.

---

_Reviewed: 2026-07-03T00:00:00Z_
_Reviewer: Claude (gsd-code-reviewer)_
_Depth: standard_
