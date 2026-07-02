# Phase 30: Group UX & Admin User Creation — Research

**Researched:** 2026-06-30
**Domain:** ASP.NET Core 10 MVC — session-based group context, post-login routing, admin user creation
**Confidence:** HIGH

---

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions

**Post-Login Group Routing**
- D-01: `AccountController.Login` POST redirects to `/groups/pick?returnUrl=<encoded>` immediately after `SignInAsync` succeeds — always, unconditionally. Login no longer calls `RedirectToLocal`.
- D-02: New `GroupPickerController` (non-area, main controllers directory) at route `/groups/pick`. Two actions: `Index` GET (auto-redirect for single-group; show picker for multi-group/SuperAdmin) and `SelectGroup` POST (writes session, redirects to returnUrl or Home/Index).
- D-03: Session writing uses `HttpContext.Session.SetInt32(SessionKeys.ActiveGroupId, groupId)`. No changes to `IActiveGroupContext` interface.
- D-04 (Claude's discretion): Expired session (no `ActiveGroupId`) → existing 403 behavior from Phase 29 handlers applies. No new middleware needed.

**Group-Picker Page**
- D-05: Route is `/groups/pick` (new `GroupPickerController`).
- D-06: Card grid using `modern-card` pattern. Clicking a card submits a `SelectGroup` POST for that group.
- D-07: SuperAdmin sees all group cards + "Go to Platform →" button linking to `/platform`. Non-SuperAdmin see only their own groups.
- D-08: Picker uses a stripped-down layout (`_Layout.GroupPicker.cshtml`), NOT `_Layout.cshtml`.

**Self-Registration Removal + Admin User Creation**
- D-09: Remove `AccountController.Register` GET, POST, and `Register.cshtml` / `Register.Mobile.cshtml` entirely. `/account/register` returns 404. Remove nav links to Register.
- D-10: New `CreateUser` GET + POST in `AdminController` at `/admin/create-user`, protected by `[Authorize(Policy = "AdminOnly")]`.
- D-11: `CreateUser` form fields: email, display name, password, GroupRole picker (Player / DungeonMaster / Admin). After `userService.CreateAsync` succeeds, call `userService.SetGroupRoleAsync(userId, activeGroupContext.ActiveGroupId.Value, selectedGroupRole)`.
- D-12: Email confirmation reuses existing `ConfirmationEmailJob` — enqueue immediately after account creation (REG-03).
- D-13: On success, redirect to `nameof(Users)`. TempData["Success"] = "Account created for {name}. A confirmation email has been sent."

**Navigation Group Display**
- D-14: Use `@inject IActiveGroupContext activeGroupContext` in `_Layout.cshtml` and `_Layout.Mobile.cshtml`. Group name displayed via session (`SessionKeys.ActiveGroupName`) or DB lookup — see D-16.
- D-15: User dropdown gains a group switch item after Profile, before Logout. Icon: `fas fa-arrows-rotate`. Clicking navigates to `GroupPicker/Index`.
- D-16 (Claude's discretion): Whether to store group name in session as `SessionKeys.ActiveGroupName` or do a per-request DB lookup. Session storage preferred for performance.

**?? 1 Fallback Removal**
- D-17 (LOCKED REQUIREMENT): Remove all three `?? 1` fallbacks introduced in Phase 28/29:
  - `AdminController.Users()` — remove `groupId ?? 1`
  - `IUserService.GetAllPlayersAsync` call sites — remove `?? 1` fallback (if present)
  - `IUserService.GetAllDungeonMastersAsync` call sites — remove `?? 1` fallback (if present)

**Mobile View Parity**
- D-18: All new views require `.Mobile.cshtml` variants:
  - `GroupPicker/Index.Mobile.cshtml`
  - `Admin/CreateUser.Mobile.cshtml`
  - `_Layout.Mobile.cshtml` — add D-15 group switch item
- D-19: Delete `Views/Account/Register.Mobile.cshtml` alongside `Views/Account/Register.cshtml`.

### Claude's Discretion

- Whether `GroupPickerController` lives under `Controllers/` or `Controllers/GroupPicker/`
- Group name storage: session (`SessionKeys.ActiveGroupName`) vs. per-request DB lookup via `IGroupService`
- Exact layout file name for picker page (`_Layout.GroupPicker.cshtml` vs. reusing login layout)
- Whether `GroupPickerController.Index` is protected with `[Authorize]` only (no policy)
- CSS class for the "Go to Platform →" button
- Exact column count for the group cards grid

### Deferred Ideas (OUT OF SCOPE)

- Redirect to group picker on expired session mid-request (instead of 403)
- Group name in nav bar permanently visible (badge next to user name outside dropdown)
- Per-group email configuration
- Group invitation flow
</user_constraints>

---

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|------------------|
| UX-01 | User belonging to exactly one group is automatically redirected to that group's content after login (no picker shown) | `GroupPickerController.Index` GET: count user's groups via `IGroupService.GetMembersAsync` or a new user-groups query; if count == 1, write session and redirect |
| UX-02 | User belonging to multiple groups sees a group-picker page after login | `GroupPickerController.Index` GET renders picker when user has multiple groups |
| UX-03 | SuperAdmin always lands on the group-picker page after login and can enter any group or go to the management area | `User.IsInRole("SuperAdmin")` check in `GroupPickerController.Index`; show all groups + "Go to Platform" button |
| UX-04 | Active group stored in ASP.NET Core Session per request; persists until session expires or user switches | `HttpContext.Session.SetInt32(SessionKeys.ActiveGroupId, groupId)` — existing mechanism, no changes to `IActiveGroupContext` |
| UX-05 | Navigation displays current group name and "Switch group" link | `_Layout.cshtml` and `_Layout.Mobile.cshtml` modifications; `SessionKeys.ActiveGroupName` added to session by `SelectGroup` POST |
| MGMT-07 | Group admin can create new user accounts within their group | `AdminController.CreateUser` GET/POST + `CreateUserViewModel` + `IUserService.CreateAsync` + `SetGroupRoleAsync` |
| MGMT-08 | Group admin can promote/demote users within their group | Already satisfied by Phase 29 promote/demote actions; only `?? 1` fallback removal (D-17) needed to make them correct for multi-group |
| REG-01 | Public self-registration removed or restricted | Remove `AccountController.Register` GET/POST + both `Register.cshtml` views |
| REG-02 | Newly created user accounts automatically assigned to creating admin's active group with specified `GroupRole` | `userService.SetGroupRoleAsync(newUserId, activeGroupContext.ActiveGroupId.Value, selectedGroupRole)` in `CreateUser` POST |
| REG-03 | Existing email confirmation flow triggered when admin creates account | `jobClient.Enqueue<ConfirmationEmailJob>(...)` — identical to old Register action, already exists |
</phase_requirements>

---

## Summary

Phase 30 completes the multi-tenancy UX loop started in Phases 27–29. The codebase already has all the service and repository infrastructure needed — `IGroupService.GetAllWithMemberCountAsync()`, `IUserService.SetGroupRoleAsync()`, `IActiveGroupContext` reading `SessionKeys.ActiveGroupId` from `HttpContext.Session`, and `ConfirmationEmailJob`. This phase wires those pieces together at the HTTP level: post-login routing to `/groups/pick`, a new `GroupPickerController`, session-stored group context, navigation display, and admin-only user creation.

The most significant work is: (1) a new `GroupPickerController` with `Index` GET (auto-redirect or show picker) and `SelectGroup` POST (write session, redirect); (2) modifications to `AccountController.Login` POST to redirect to the picker; (3) removal of `Register` GET/POST and both Register views; (4) a new `CreateUser` GET/POST in `AdminController` modeled on the existing `EditUser` pattern; (5) layout injection of group name + switch link; and (6) removal of all `?? 1` fallbacks. No new packages, no new EF migrations, and no new domain models beyond two ViewModels.

**Primary recommendation:** Implement in four sequenced plans — (1) `GroupPickerController` + login redirect, (2) nav group display + session name storage, (3) `AdminController.CreateUser` + Register removal, (4) `?? 1` fallback removal + test updates. Plans 1 and 3 can potentially be done in the same wave; plan 2 depends on plan 1 (session name written by `SelectGroup`); plan 4 depends on plans 1–3.

---

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|-------------|----------------|-----------|
| Post-login group routing | Frontend Server (MVC controller) | — | `AccountController.Login` POST issues HTTP 302; `GroupPickerController` handles the pick lifecycle |
| Group context persistence | Frontend Server (Session) | — | `HttpContext.Session.SetInt32(SessionKeys.ActiveGroupId)` — already the established pattern from Phase 28 |
| Group picker display | Frontend Server (Razor view) | — | `GroupPickerController.Index` GET renders groups from `IGroupService.GetAllWithMemberCountAsync()` |
| Navigation group display | Frontend Server (Razor layout) | — | `_Layout.cshtml` / `_Layout.Mobile.cshtml` inject `IActiveGroupContext` and read `SessionKeys.ActiveGroupName` from session |
| Admin user creation | Frontend Server (MVC controller) | Domain Service | `AdminController.CreateUser` POST orchestrates `IUserService.CreateAsync` + `SetGroupRoleAsync` + `ConfirmationEmailJob` |
| Self-registration removal | Frontend Server | — | Delete controller actions and views; no domain or repository changes |
| Email confirmation on admin-create | Background Job (Hangfire) | — | `ConfirmationEmailJob` already exists; just needs to be enqueued from `CreateUser` POST |

---

## Standard Stack

No new packages are required. All infrastructure is already in place.

### Core (existing, no changes)
| Library | Version | Purpose | Notes |
|---------|---------|---------|-------|
| ASP.NET Core 10 MVC | 10.0 | Controllers, Razor views, session | No changes to pipeline |
| ASP.NET Core Session | (built-in) | `HttpContext.Session.SetInt32/GetInt32` | `SessionKeys.ActiveGroupId` already defined |
| ASP.NET Core Identity | (built-in) | User creation, role checks | `UserManager<UserEntity>` via `IUserService` |
| Hangfire | existing | `ConfirmationEmailJob` enqueueing | Already registered; Testing env uses `NoOpBackgroundJobClient` |
| Bootstrap 5.3.0 | CDN | Card grid, form styling | `modern-card`, `btn-warning`, `btn-secondary`, grid |
| Font Awesome 6.4.0 | CDN | `fa-arrows-rotate`, `fa-user-plus`, `fa-layer-group`, `fa-cog` | All icons available |

### New ViewModels (in-project)
| File | Purpose |
|------|---------|
| `ViewModels/AdminViewModels/CreateUserViewModel.cs` | Binds CreateUser form (Email, Name, Password, GroupRole) |
| `ViewModels/GroupPickerViewModels/GroupPickerViewModel.cs` | Carries `IList<GroupWithMemberCount>` + `IsSuperAdmin` flag to picker view |

**Version verification:** No new packages — all dependencies are established project dependencies. [ASSUMED] is not applicable here; these are confirmed by reading the codebase directly.

---

## Package Legitimacy Audit

No new external packages are installed in this phase. All dependencies are built-in ASP.NET Core components or existing project packages.

| Package | Registry | Age | Verdict | Disposition |
|---------|----------|-----|---------|-------------|
| (none) | — | — | — | No new packages |

---

## Architecture Patterns

### System Architecture Diagram

```
[Browser POST /Account/Login]
        |
        v
[AccountController.Login POST]
   - SignInAsync succeeds
   - Redirect to /groups/pick?returnUrl={encoded}
        |
        v
[GroupPickerController.Index GET]  <-- [Authorize] only, no policy
   - Load user's groups via IGroupService
   - if count == 1 and not SuperAdmin:
       → SetInt32(SessionKeys.ActiveGroupId, groupId)
       → SetString(SessionKeys.ActiveGroupName, name)  [D-16]
       → Redirect(returnUrl or "/")
   - else (multiple groups OR SuperAdmin):
       → Render GroupPicker/Index.cshtml with GroupPickerViewModel
        |
        v (card click)
[GroupPickerController.SelectGroup POST]
   - Receive groupId + returnUrl
   - SetInt32(SessionKeys.ActiveGroupId, groupId)
   - SetString(SessionKeys.ActiveGroupName, name)  [D-16]
   - Redirect(returnUrl or "/")
        |
        v
[_Layout.cshtml / _Layout.Mobile.cshtml]
   - @inject IActiveGroupContext activeGroupContext
   - Reads ViewBag.ActiveGroupName (set by SelectGroup or base controller) OR Session directly
   - User dropdown: "Switch Group" item → /groups/pick

[AdminController.CreateUser GET/POST]  <-- [AdminOnly] policy (inherited from controller)
   - GET: Render CreateUser form
   - POST: CreateAsync + SetGroupRoleAsync + Enqueue<ConfirmationEmailJob> → Redirect(Users)

[AccountController.Register] → REMOVED (404)
[Views/Account/Register.cshtml] → DELETED
[Views/Account/Register.Mobile.cshtml] → DELETED
```

### Recommended Project Structure (new files only)
```
QuestBoard.Service/
├── Controllers/
│   └── GroupPickerController.cs           (new — D-02)
├── Views/
│   ├── GroupPicker/
│   │   ├── Index.cshtml                   (new — D-06)
│   │   └── Index.Mobile.cshtml            (new — D-18)
│   ├── Admin/
│   │   ├── CreateUser.cshtml              (new — D-10)
│   │   └── CreateUser.Mobile.cshtml       (new — D-18)
│   └── Shared/
│       └── _Layout.GroupPicker.cshtml     (new — D-08)
└── ViewModels/
    ├── AdminViewModels/
    │   └── CreateUserViewModel.cs         (new)
    └── GroupPickerViewModels/
        └── GroupPickerViewModel.cs        (new)
```

### Pattern 1: GroupPickerController — Auto-redirect vs. Show Picker

**What:** `Index` GET reads the user's group memberships. Single-group users are silently set and redirected; multi-group and SuperAdmin users see the picker.

**Source:** CONTEXT.md D-01, D-02, D-07; `IGroupService.GetAllWithMemberCountAsync` already filters for SuperAdmin (returns all); for non-SuperAdmin users, we need their specific groups.

**Key detail:** `IGroupService.GetAllWithMemberCountAsync()` returns ALL groups (for SuperAdmin context). To get only the groups a specific user belongs to, use `IGroupService.GetMembersAsync()` — but that takes a `groupId`, not a `userId`. The better approach is to query user's groups via `IUserService` or directly via the user's group memberships. [ASSUMED] Check whether a `GetUserGroupsAsync(userId)` method exists; if not, the controller can call `IGroupService.GetAllWithMemberCountAsync()` and filter by membership — but this is inefficient. See Open Questions below.

**Likely implementation:**

```csharp
// Source: CONTEXT.md D-02; IGroupService interface (confirmed by codebase read)
[Authorize]
public class GroupPickerController(IGroupService groupService, IUserService userService) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(string? returnUrl = null)
    {
        var isSuperAdmin = User.IsInRole("SuperAdmin");

        IList<GroupWithMemberCount> groups;
        if (isSuperAdmin)
        {
            groups = await groupService.GetAllWithMemberCountAsync();
        }
        else
        {
            // Get groups where this user is a member
            // Requires a user-groups query — see Open Questions
            var userId = /* parse from User claims */;
            groups = await groupService.GetUserGroupsWithMemberCountAsync(userId); // [ASSUMED: method may not exist]
        }

        if (!isSuperAdmin && groups.Count == 1)
        {
            HttpContext.Session.SetInt32(SessionKeys.ActiveGroupId, groups[0].Id);
            HttpContext.Session.SetString(SessionKeys.ActiveGroupName, groups[0].Name);
            return Redirect(Url.IsLocalUrl(returnUrl) ? returnUrl! : Url.Action("Index", "Home")!);
        }

        var vm = new GroupPickerViewModel { Groups = groups, IsSuperAdmin = isSuperAdmin, ReturnUrl = returnUrl };
        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SelectGroup(int groupId, string? returnUrl = null)
    {
        var group = await groupService.GetByIdAsync(groupId);
        if (group == null) return NotFound();

        HttpContext.Session.SetInt32(SessionKeys.ActiveGroupId, group.Id);
        HttpContext.Session.SetString(SessionKeys.ActiveGroupName, group.Name);
        return Redirect(Url.IsLocalUrl(returnUrl) ? returnUrl! : Url.Action("Index", "Home")!);
    }
}
```

[ASSUMED: `HttpContext.Session.SetString` is a built-in extension method from `Microsoft.AspNetCore.Http`]

### Pattern 2: Login POST Redirect to Picker

```csharp
// Source: CONTEXT.md D-01; existing AccountController.Login POST (confirmed by codebase read)
if (result.Succeeded)
{
    // D-01: always redirect to group picker; picker handles returnUrl threading
    return RedirectToAction("Index", "GroupPicker", new { returnUrl });
}
```

The existing `RedirectToLocal(returnUrl)` call is replaced entirely.

### Pattern 3: CreateUser — Mirrors Register + SetGroupRole

```csharp
// Source: CONTEXT.md D-11, D-12, D-13; existing AccountController.Register POST (confirmed by codebase read)
[HttpPost]
[ValidateAntiForgeryToken]
public async Task<IActionResult> CreateUser(CreateUserViewModel model)
{
    if (ModelState.IsValid)
    {
        var groupId = activeGroupContext.ActiveGroupId;
        if (groupId == null) return RedirectToAction(nameof(Users));

        var result = await userService.CreateAsync(model.Email, model.Name, model.Password);
        if (result.Succeeded)
        {
            var userId = await identityService.GetIdByEmailAsync(model.Email);
            if (userId.HasValue)
            {
                await userService.SetGroupRoleAsync(userId.Value, groupId.Value, model.GroupRole);

                var rawToken = await identityService.GenerateEmailConfirmationAsync(userId.Value);
                if (rawToken != null)
                {
                    var encodedToken = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(rawToken));
                    var callbackUrl = Url.Action("ConfirmEmail", "Account",
                        new { userId = userId.Value, token = encodedToken }, Request.Scheme);
                    jobClient.Enqueue<ConfirmationEmailJob>(
                        j => j.ExecuteAsync(model.Email, model.Name, callbackUrl!, CancellationToken.None));
                }
            }

            TempData["Success"] = $"Account created for {model.Name}. A confirmation email has been sent.";
            return RedirectToAction(nameof(Users));
        }

        foreach (var error in result.Errors)
            ModelState.AddModelError(string.Empty, error.Description);
    }
    return View(model);
}
```

`AdminController` already has `IIdentityService identityService` and `IBackgroundJobClient jobClient` in its constructor (confirmed by codebase read).

### Pattern 4: Session Name Storage (D-16 recommendation)

Store the group name in session at `SelectGroup` POST time (and in the auto-redirect in `Index`). Add `SessionKeys.ActiveGroupName` constant. Display in layout via `HttpContext.Session.GetString(SessionKeys.ActiveGroupName)` or via `ViewBag.ActiveGroupName` set in a base controller or via `@inject IHttpContextAccessor` in the layout.

**Pragmatic approach:** The layout (`_Layout.cshtml`) already uses `@inject IUserService UserService` (from `_ViewImports.cshtml`). Add `@inject Microsoft.AspNetCore.Http.IHttpContextAccessor HttpContextAccessor` or read directly from the session via an injected `IHttpContextAccessor`. [ASSUMED: layouts can inject `IHttpContextAccessor` directly via `@inject`]

**Alternative (simpler):** Use a base controller that sets `ViewBag.ActiveGroupName` from `HttpContext.Session.GetString(SessionKeys.ActiveGroupName)` in `OnActionExecuting`. However, this requires all controllers to inherit from a common base. Given the existing codebase has no base controller, the simplest approach is to inject `IHttpContextAccessor` into the layout directly.

### Pattern 5: ??1 Fallback Removal (D-17)

```csharp
// BEFORE (AdminController.Users)
GroupRole? groupRole = await userService.GetGroupRoleByIdAsync(user.Id, groupId ?? 1);

// AFTER (Phase 30 guarantees groupId is always set at login)
GroupRole? groupRole = await userService.GetGroupRoleByIdAsync(user.Id, groupId!.Value);
// Guard: if groupId is null, return early (safety net — should never happen after Phase 30)
```

Check `IUserService.GetAllPlayersAsync` and `IUserService.GetAllDungeonMastersAsync` implementations for any `?? 1` fallback at the call site. [ASSUMED: these may be in `UserService` implementation or called from controllers; need to verify at planning time]

### Anti-Patterns to Avoid

- **Using `RedirectToLocal` in the Login POST after Phase 30:** The login action must always redirect to `/groups/pick`, never directly to the returnUrl. Using `RedirectToLocal` skips the group selection step.
- **Reading group name from DB in the layout on every request:** Inject `IGroupService` into `_Layout.cshtml` and calling `GetByIdAsync(activeGroupId)` every page load is wasteful. Use session-stored name instead.
- **Putting the group picker in an MVC Area:** The picker is reachable by any authenticated user and should be in the main controller directory, not in the Platform area which is SuperAdmin-only.
- **Forgetting to URL-encode the returnUrl when threading through the picker:** If returnUrl contains `?` or `&` characters (e.g. `/Quest/Details?id=5`), it must be encoded before appending as a query parameter. `Url.Action("Index", "GroupPicker", new { returnUrl })` handles this automatically via MVC tag helper.
- **Leaving Register link in Login.cshtml:** The `Login.cshtml` currently has a "Don't have an account? Create Account" section with `asp-action="Register"`. This must be removed (D-09).
- **Using `[Authorize(Policy = "AdminOnly")]` on GroupPickerController:** The picker is accessible to any authenticated user — it must use `[Authorize]` only, without a policy.

---

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| URL encoding of returnUrl | Manual string concatenation | `RedirectToAction("Index", "GroupPicker", new { returnUrl })` or `asp-route-returnUrl` tag helper | MVC handles encoding automatically |
| Email confirmation token generation | Custom token service | `identityService.GenerateEmailConfirmationAsync(userId)` + `WebEncoders.Base64UrlEncode` | Already implemented in `IdentityService`; same pattern as existing Register action |
| Anti-forgery token on group card forms | Custom CSRF mechanism | `@Html.AntiForgeryToken()` + `[ValidateAntiForgeryToken]` | Built-in; already used by every POST in the project |
| Session SetString | Custom session serializer | `HttpContext.Session.SetString(SessionKeys.ActiveGroupName, name)` | Built-in extension method from `Microsoft.AspNetCore.Http` |
| Group role assignment | Direct EF writes in controller | `userService.SetGroupRoleAsync(userId, groupId, role)` | Already implemented in `UserService`; Phase 29 D-09 |

---

## Common Pitfalls

### Pitfall 1: Zero-group user crashes GroupPickerController

**What goes wrong:** An authenticated user with no UserGroups membership (possible for SuperAdmin assigned before Phase 27 data migration, or for a user whose last group was deleted) navigates to `/groups/pick` — `GroupPickerController.Index` returns an empty list and the view renders with no cards or crashes.

**Why it happens:** The group picker assumes every authenticated user has at least one group, but there is no DB constraint enforcing this.

**How to avoid:** Guard in `GroupPickerController.Index`:
```csharp
if (!isSuperAdmin && groups.Count == 0)
{
    // Show error — no groups assigned
    return View(new GroupPickerViewModel { Groups = [], IsSuperAdmin = false, HasNoGroups = true });
}
```
Show `alert-warning` in the view: "Your account is not assigned to any group. Please contact your administrator."

**Warning signs:** NullReferenceException or an empty page on the group picker for non-SuperAdmin users.

---

### Pitfall 2: returnUrl threading is broken when Login redirects to picker

**What goes wrong:** User tries to navigate to `/Quest/Details/5` while unauthenticated. ASP.NET Core Identity redirects to `/Account/Login?ReturnUrl=%2FQuest%2FDetails%2F5`. Login POST currently captures `returnUrl` parameter. After Phase 30, Login POST redirects to `/groups/pick?returnUrl=%2FQuest%2FDetails%2F5`. If the picker's `SelectGroup` POST doesn't thread `returnUrl` correctly, the user ends up at Home instead of their intended destination.

**Why it happens:** `returnUrl` must survive three hops: Login → Picker (GET) → SelectGroup (POST) → final destination.

**How to avoid:**
1. Login POST: `RedirectToAction("Index", "GroupPicker", new { returnUrl })` — MVC encodes returnUrl as query param.
2. Picker GET view: pass `returnUrl` as a hidden field in each group card's POST form.
3. `SelectGroup` POST: receive `returnUrl`, validate with `Url.IsLocalUrl(returnUrl)`, then redirect.

**Warning signs:** After login, user lands on Home regardless of what page they were trying to access.

---

### Pitfall 3: ?? 1 fallback removal causes 403 in existing tests

**What goes wrong:** After removing `groupId ?? 1` from `AdminController.Users()`, existing integration tests that call `/Admin/Users` with an authenticated admin user may get 403 because `activeGroupContext.ActiveGroupId` returns `null` (no session set in the test).

**Why it happens:** The test factory registers `MutableGroupContext` with `ActiveGroupId = 1` as the default (confirmed in `WebApplicationFactoryBase.cs` and `MutableGroupContext.cs`). This is already correct — the `MutableGroupContext` defaults to `GroupId = 1`. So existing tests should continue to work after `?? 1` removal because `MutableGroupContext.ActiveGroupId` is already `1`.

**Verification:** Run `dotnet test` after removing the `?? 1` fallbacks. If any test fails with 403 or NullReferenceException, it means the test created a client with `factory.TestGroupContext.ActiveGroupId = null` — set it back to `1` for that test.

**Warning signs:** `AdminControllerIntegrationTests` failures after D-17 removal.

---

### Pitfall 4: AccountControllerIntegrationTests has Register tests that will fail after D-09

**What goes wrong:** `AccountControllerIntegrationTests` has `Register_Get_ShouldReturnSuccessStatusCode` and `Register_Post_WithValidData_ShouldCreateUser` tests (confirmed by reading the test file). After removing `Register`, these return 404.

**How to avoid:** Update existing Register tests: `Register_Get` should now assert `HttpStatusCode.NotFound`; `Register_Post` tests should be removed or converted to test `AdminController.CreateUser`.

**Warning signs:** `AccountControllerIntegrationTests` failing with 404 on Register endpoints.

---

### Pitfall 5: `IGroupService` does not have a "get groups for user" method

**What goes wrong:** `GroupPickerController.Index` needs to get only the groups where the current user is a member. `IGroupService.GetAllWithMemberCountAsync()` returns ALL groups (used by SuperAdmin). `IGroupService.GetMembersAsync(groupId)` takes a groupId (not userId). There is no `GetGroupsForUserAsync(userId)` method.

**Why it happens:** Phase 29 was SuperAdmin-focused; it built group management tools, not user-perspective group queries.

**How to avoid:** Two options:
1. Add `GetGroupsForUserAsync(int userId)` to `IGroupService` and implement it in `GroupService` (new method returning `IList<GroupWithMemberCount>` for groups where `userId` is a member).
2. In `GroupPickerController`, fetch all groups with `GetAllWithMemberCountAsync()` and filter in-memory by checking `IGroupService.GetMembersAsync(groupId)` for each — but this is O(N) DB calls.

**Recommendation:** Add `GetGroupsForUserAsync(int userId)` to `IGroupService` and `IGroupRepository`. This is a single JOIN query: `UserGroups` WHERE `UserId = userId` JOIN `Groups`. The planner must include this as a task.

**Warning signs:** GroupPickerController cannot populate picker with only the current user's groups without N+1 queries or a new method.

---

### Pitfall 6: `_Layout.cshtml` UserService injection conflict

**What goes wrong:** `_Layout.cshtml` already uses `var currentUser = await UserService.GetUserAsync(User)` inside the authenticated block (line 125). Adding `@inject IActiveGroupContext activeGroupContext` and reading session works fine. However, trying to look up group name via `IGroupService` in the layout requires another inject, and `IGroupService` is not currently in `_ViewImports.cshtml`.

**How to avoid:** Store group name in session (`SessionKeys.ActiveGroupName`) at `SelectGroup` POST time. Then the layout reads: `HttpContext.Session.GetString(SessionKeys.ActiveGroupName)`. No `IGroupService` inject needed in the layout. Add `SessionKeys.ActiveGroupName` constant to `SessionKeys.cs`.

**Warning signs:** Compiler error in `_Layout.cshtml` if `IGroupService` is injected but not in `_ViewImports.cshtml`.

---

### Pitfall 7: SuperAdmin has no group context — null `ActiveGroupId` throughout

**What goes wrong:** A SuperAdmin who selects a group via the picker has `ActiveGroupId` set normally. But a SuperAdmin who clicks "Go to Platform →" never sets a group context. If that SuperAdmin also has an Admin GroupRole in some group and navigates to `/Admin/Users`, `activeGroupContext.ActiveGroupId` will be null — the `?? 1` fallback removal (D-17) means the null guard `if (groupId == null) return RedirectToAction(nameof(Users))` would loop.

**Why it happens:** D-17 removal only removes fallbacks; it doesn't add middleware. The behavior when `ActiveGroupId` is null depends on D-04: handlers call `context.Fail()` → 403. For `AdminController.Users()`, the null guard should redirect cleanly rather than loop.

**How to avoid:** After removing `?? 1`, the null guard in `AdminController.Users()` should redirect to the group picker, not back to Users:
```csharp
var groupId = activeGroupContext.ActiveGroupId;
if (groupId == null) return RedirectToAction("Index", "GroupPicker");
```
Or keep a 403 guard consistent with D-04. The planner should decide the exact behavior.

---

## Code Examples

### GroupPickerViewModel (new)
```csharp
// Source: CONTEXT.md D-02, D-07; GroupWithMemberCount in codebase (confirmed by codebase read)
namespace QuestBoard.Service.ViewModels.GroupPickerViewModels;

public class GroupPickerViewModel
{
    public IList<GroupWithMemberCount> Groups { get; set; } = [];
    public bool IsSuperAdmin { get; set; }
    public bool HasNoGroups { get; set; }
    public string? ReturnUrl { get; set; }
}
```

### CreateUserViewModel (new)
```csharp
// Source: CONTEXT.md D-11; existing RegisterViewModel pattern (confirmed by codebase read)
using QuestBoard.Domain.Enums;
using System.ComponentModel.DataAnnotations;

namespace QuestBoard.Service.ViewModels.AdminViewModels;

public class CreateUserViewModel
{
    [Required]
    [EmailAddress]
    [Display(Name = "Email")]
    public string Email { get; set; } = string.Empty;

    [Required]
    [StringLength(100)]
    [Display(Name = "Display Name")]
    public string Name { get; set; } = string.Empty;

    [Required]
    [StringLength(100, MinimumLength = 8)]
    [DataType(DataType.Password)]
    [Display(Name = "Password")]
    public string Password { get; set; } = string.Empty;

    [Display(Name = "Group Role")]
    public GroupRole GroupRole { get; set; } = GroupRole.Player;
}
```

### SessionKeys (updated)
```csharp
// Source: Existing SessionKeys.cs (confirmed by codebase read); CONTEXT.md D-16
public static class SessionKeys
{
    public const string ActiveGroupId = "ActiveGroupId";
    public const string ActiveGroupName = "ActiveGroupName";  // NEW — Phase 30
}
```

### Login view Register link removal
```cshtml
{{-- Remove this block from Views/Account/Login.cshtml: --}}
<hr class="my-4">
<div class="text-center">
    <p class="mb-0">Don't have an account?</p>
    <a asp-action="Register" asp-route-returnurl="@ViewData["ReturnUrl"]" class="btn btn-warning">
        <i class="fas fa-user-plus me-2"></i>
        Create Account
    </a>
</div>
{{-- Source: CONTEXT.md D-09; existing Login.cshtml lines 64–73 (confirmed by codebase read) --}}
```

---

## Runtime State Inventory

Not applicable — this phase is not a rename/refactor/migration phase. No stored data, live service config, OS-registered state, secrets/env vars, or build artifacts are affected by the changes in this phase.

---

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| Public self-registration | Admin-only user creation | Phase 30 | Users can no longer self-register; must be created by a group admin |
| Login redirects to Home or returnUrl | Login always redirects to group picker | Phase 30 | Every login flows through group selection |
| `?? 1` fallback for group ID | No fallback — group ID always set by picker | Phase 30 | Actual multi-group isolation works; if session is missing, existing 403 behavior applies |
| No group name in nav | Group name + switch link in user dropdown | Phase 30 | Users can see and change their active group context |

---

## Project Constraints (from CLAUDE.md)

- **Platform:** Windows; CRLF line endings in new files
- **Tech stack:** Stay on ASP.NET Core 10 MVC + SQL Server + EF Core — no framework changes
- **Architecture:** Three-layer clean architecture — Service → Domain → Repository. EF packages belong only in `QuestBoard.Repository`
- **UI pattern:** All new views must use `modern-card`, `modern-card-header`, `modern-card-body` CSS classes. `<hr>` before button section. Filled colored buttons (not outline), FontAwesome icons with `me-2` spacing. Button layout: `d-flex justify-content-between`.
- **No EF migrations needed:** This phase has no schema changes.
- **No commits directly to main:** All work on `milestone/v5-multi-tenancy` branch.
- **RIP MCP:** If the `rip` MCP server is available, use it for symbol navigation before reading files.

---

## Open Questions

1. **Does `IGroupService` have a method to get groups for a specific user?**
   - What we know: `IGroupService` has `GetAllWithMemberCountAsync()` (all groups) and `GetMembersAsync(groupId)` (members of a group). There is no `GetGroupsForUserAsync(userId)`.
   - What's unclear: Whether there's a way to get a user's groups without adding a new service method.
   - Recommendation: Add `Task<IList<GroupWithMemberCount>> GetGroupsForUserAsync(int userId)` to `IGroupService` + `IGroupRepository`. Single SQL JOIN. The planner should include this as a task in Plan 1.

2. **How to inject `IHttpContextAccessor` or group name into `_Layout.cshtml`?**
   - What we know: `_Layout.cshtml` uses `@inject` directives. `_ViewImports.cshtml` already injects `IUserService UserService` and `IAuthorizationService AuthorizationService`. `IHttpContextAccessor` is registered in DI (confirmed in `Program.cs`).
   - What's unclear: Whether to read session in the layout via `@inject Microsoft.AspNetCore.Http.IHttpContextAccessor HttpContextAccessor` and then `HttpContextAccessor.HttpContext?.Session?.GetString(SessionKeys.ActiveGroupName)`, or to use a base controller + `ViewBag`.
   - Recommendation: Use `@inject Microsoft.AspNetCore.Http.IHttpContextAccessor HttpContextAccessor` in `_Layout.cshtml` and `_Layout.Mobile.cshtml` directly. This avoids creating a base controller and aligns with the existing `@inject` pattern in the layout.

3. **What happens to `GetAllPlayersAsync` and `GetAllDungeonMastersAsync` `?? 1` fallbacks?**
   - What we know: D-17 says to remove them. The call sites are in service methods or controllers — need to grep to find exact locations.
   - What's unclear: Where exactly these `?? 1` fallbacks are (they may be in `UserService` implementation, not just in controller call sites).
   - Recommendation: The planner should grep for `?? 1` in the codebase before removing. The `MutableGroupContext` defaults to `GroupId = 1`, so integration tests won't break; the `?? 1` removal is safe once the login redirect guarantees session is set.

---

## Environment Availability

Step 2.6: SKIPPED — this phase is purely code and view changes. No external CLI tools, databases, or services beyond the existing project infrastructure are needed.

---

## Validation Architecture

### Test Framework

| Property | Value |
|----------|-------|
| Framework | xUnit v3 + FluentAssertions |
| Config file | none (discovery by convention) |
| Quick run command | `dotnet test QuestBoard.IntegrationTests --filter "FullyQualifiedName~GroupPicker\|FullyQualifiedName~AccountController\|FullyQualifiedName~AdminController"` |
| Full suite command | `dotnet test` |

### Phase Requirements → Test Map

| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| UX-01 | Single-group user auto-redirected after login | Integration | `dotnet test --filter "FullyQualifiedName~GroupPickerController"` | No — Wave 0 |
| UX-02 | Multi-group user sees picker page | Integration | `dotnet test --filter "FullyQualifiedName~GroupPickerController"` | No — Wave 0 |
| UX-03 | SuperAdmin sees picker + "Go to Platform" | Integration | `dotnet test --filter "FullyQualifiedName~GroupPickerController"` | No — Wave 0 |
| UX-04 | Group selection persists in session | Integration | `dotnet test --filter "FullyQualifiedName~GroupPickerController"` | No — Wave 0 |
| UX-05 | Nav shows group name + switch link | Integration | `dotnet test --filter "FullyQualifiedName~Layout"` | No — Wave 0 |
| MGMT-07 | Admin creates user → assigned to group | Integration | `dotnet test --filter "FullyQualifiedName~AdminController"` | Partial — update existing |
| MGMT-08 | Promote/demote works without ?? 1 | Integration | `dotnet test --filter "FullyQualifiedName~AdminController"` | Yes — update existing |
| REG-01 | /Account/Register returns 404 | Integration | `dotnet test --filter "FullyQualifiedName~AccountController"` | Yes — update existing (currently asserts 200) |
| REG-02 | Created user assigned to admin's group | Integration | `dotnet test --filter "FullyQualifiedName~AdminController"` | No — Wave 0 |
| REG-03 | Email confirmation job enqueued | Unit | `dotnet test QuestBoard.UnitTests --filter "FullyQualifiedName~CreateUser"` | No — Wave 0 |

### Sampling Rate

- **Per task commit:** `dotnet test --filter "FullyQualifiedName~GroupPickerController\|FullyQualifiedName~AdminController\|FullyQualifiedName~AccountController"`
- **Per wave merge:** `dotnet test`
- **Phase gate:** Full suite green (currently 219 tests) before `/gsd-verify-work`

### Wave 0 Gaps

- [ ] `QuestBoard.IntegrationTests/Controllers/GroupPickerControllerIntegrationTests.cs` — covers UX-01, UX-02, UX-03, UX-04
- [ ] Update `AccountControllerIntegrationTests.cs` — `Register_Get` and `Register_Post` tests must assert 404 instead of 200/Redirect
- [ ] Update `AdminControllerIntegrationTests.cs` — add `CreateUser_WhenAdmin_CreatesUserInGroup` test (REG-02, MGMT-07)

---

## Security Domain

### Applicable ASVS Categories

| ASVS Category | Applies | Standard Control |
|---------------|---------|-----------------|
| V2 Authentication | yes | ASP.NET Core Identity + session; Login POST always routes through picker |
| V3 Session Management | yes | `ISession.SetInt32/SetString` — built-in; `IdleTimeout = 24h` in `Program.cs` |
| V4 Access Control | yes | `[Authorize(Policy = "AdminOnly")]` on `CreateUser`; `[Authorize]` on `GroupPickerController` |
| V5 Input Validation | yes | `CreateUserViewModel` with Data Annotations; `ModelState.IsValid` check in POST |
| V6 Cryptography | no | No new crypto — email token generation reuses `identityService.GenerateEmailConfirmationAsync` |

### Known Threat Patterns for This Phase

| Pattern | STRIDE | Standard Mitigation |
|---------|--------|---------------------|
| Open redirect via returnUrl | Spoofing | `Url.IsLocalUrl(returnUrl)` validation before redirect (existing `RedirectToLocal` pattern; must be replicated in `SelectGroup`) |
| CSRF on group card form POST | Tampering | `@Html.AntiForgeryToken()` in each card form + `[ValidateAntiForgeryToken]` on `SelectGroup` POST |
| Admin creates user in wrong group (IDOR) | Tampering | `activeGroupContext.ActiveGroupId` is read from server-side session, not from the form — cannot be tampered by user |
| Unauthenticated access to picker | Elevation of Privilege | `[Authorize]` on `GroupPickerController` ensures authentication before group selection |

---

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|---------------|
| A1 | `HttpContext.Session.SetString` is available as an extension method from `Microsoft.AspNetCore.Http` | Architecture Patterns, Pattern 1 | Minimal — this is a well-known built-in; if missing, use `SetInt32` pattern already confirmed in codebase |
| A2 | `IGroupService` does NOT currently have a `GetGroupsForUserAsync(userId)` method | Open Questions, Pitfall 5 | If it already exists, one planned task (add method) can be dropped |
| A3 | The `?? 1` fallbacks in `GetAllPlayersAsync` / `GetAllDungeonMastersAsync` are in the service layer (not just AdminController) | Architecture Patterns, Pattern 5 | If fallbacks are only in controllers, no domain/repository changes needed |
| A4 | `@inject Microsoft.AspNetCore.Http.IHttpContextAccessor` works directly in Razor layout views | Architecture Patterns, Pattern 4 | If not supported, fallback is ViewBag set in a base controller |
| A5 | `Login.Mobile.cshtml` also contains a Register link that must be removed (D-09) | Common Pitfalls, Anti-Patterns | If no Register link in mobile login view, no change needed there |

---

## Sources

### Primary (HIGH confidence)

- Codebase: `QuestBoard.Service/Controllers/Admin/AccountController.cs` — Login POST flow, Register actions, ConfirmationEmailJob enqueueing pattern
- Codebase: `QuestBoard.Service/Controllers/Admin/AdminController.cs` — existing ?? 1 fallback, EditUser/ResetPassword pattern for CreateUser
- Codebase: `QuestBoard.Service/Constants/SessionKeys.cs` — existing `ActiveGroupId` constant
- Codebase: `QuestBoard.Service/Services/ActiveGroupContextService.cs` — how session is read
- Codebase: `QuestBoard.Domain/Interfaces/IGroupService.cs` — available group service methods
- Codebase: `QuestBoard.Domain/Interfaces/IUserService.cs` — `CreateAsync`, `SetGroupRoleAsync`
- Codebase: `QuestBoard.IntegrationTests/WebApplicationFactoryBase.cs` — `MutableGroupContext` default, test infrastructure
- Codebase: `QuestBoard.IntegrationTests/Helpers/AuthenticationHelper.cs` — UserGroups seeding in test helper
- Codebase: `QuestBoard.Service/Views/Shared/_Layout.cshtml` — existing user dropdown structure (lines 127–146)
- Codebase: `QuestBoard.Service/Views/_ViewImports.cshtml` — existing injections in views
- CONTEXT.md D-01 through D-19 — all locked decisions confirmed against codebase

### Secondary (MEDIUM confidence)

- REQUIREMENTS.md UX-01 through UX-05, MGMT-07, MGMT-08, REG-01 through REG-03
- STATE.md Phase 30 mandate to remove ?? 1 fallbacks
- UI-SPEC.md — visual and interaction contract already approved

---

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH — no new packages; all confirmed in codebase
- Architecture: HIGH — all service interfaces and patterns confirmed by reading existing files
- Pitfalls: HIGH for pitfalls 1–4, 6–7 (confirmed against code); MEDIUM for pitfall 5 (assumed absence of `GetGroupsForUserAsync`)
- Open questions: 3 items; none are blockers for planning

**Research date:** 2026-06-30
**Valid until:** 2026-07-30 (stable tech; dependent only on codebase state which is locked at Phase 29 completion)
