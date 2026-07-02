# Phase 30: Group UX & Admin User Creation - Context

**Gathered:** 2026-06-30
**Status:** Ready for planning

<domain>
## Phase Boundary

Wire the group-context lifecycle from login through to navigation: a dedicated `GroupPickerController` intercepts users after login, auto-selects the group for single-group users, shows a card-grid picker for multi-group users, and always shows the picker for SuperAdmin (who also gets a "Go to Platform →" button). The active group is stored in `ISession` and drives all subsequent requests. The quest board nav dropdown gains a group switch item. Public self-registration is removed; group admins create new user accounts via a `CreateUser` action in `AdminController`. All existing mobile view patterns are extended to cover new views.

This phase does NOT include: group invitation flows, per-group email configuration, cross-group quest browsing, or any changes to the Platform area.

</domain>

<decisions>
## Implementation Decisions

### Post-Login Group Routing

- **D-01:** `AccountController.Login` POST redirects to `/groups/pick?returnUrl=<encoded>` immediately after `SignInAsync` succeeds — always, unconditionally. Login no longer calls `RedirectToLocal`. The `returnUrl` currently handled by `RedirectToLocal` is preserved by threading it as a query parameter to the picker.

- **D-02:** A new `GroupPickerController` (non-area, main controllers directory) handles the group-context lifecycle at route `/groups/pick`. It has two actions:
  - `Index` GET: checks group count for the logged-in user; if exactly one group → sets session + redirects to `returnUrl` or `Home/Index` (no picker shown). If multiple groups OR SuperAdmin → renders the picker page.
  - `SelectGroup` POST: receives `groupId` + optional `returnUrl`; writes `SessionKeys.ActiveGroupId` to `ISession`; redirects to `returnUrl` or `Home/Index`.

- **D-03:** Session writing in `GroupPickerController` writes directly to `HttpContext.Session` using `SessionKeys.ActiveGroupId` (the constant already defined at `QuestBoard.Service/Constants/SessionKeys.cs`). No changes to `IActiveGroupContext` interface.

- **D-04 [informational]:** When a user navigates to a protected page with an expired session (no `ActiveGroupId`), the existing Phase 29 D-03 logic applies — `AdminHandler` / `DungeonMasterHandler` call `context.Fail()` → 403. No new middleware or redirect interception needed for this phase. Verified at plan-checking time: `AdminHandler.cs` and `DungeonMasterHandler.cs` both call `context.Fail()` on null group context (confirmed by grep against the live codebase). No plan task required.

### Group-Picker Page

- **D-05:** Group-picker route is `/groups/pick` (new `GroupPickerController`). Two actions: `Index` GET + `SelectGroup` POST (see D-02).

- **D-06:** Visual design — cards grid using the existing `modern-card` pattern. One card per group showing group name + member count. Clicking a card submits `SelectGroup` POST for that group.

- **D-07:** SuperAdmin sees all group cards + a distinct "Go to Platform →" button above or below the cards that links to `/platform`. Non-SuperAdmin users see only their own groups.

- **D-08:** The group-picker page uses a stripped-down layout (like the login page) — NOT `_Layout.cshtml`. The user has no active group context at this point; showing the full quest board nav would be misleading. Create a `_Layout.GroupPicker.cshtml` (or reuse `_Layout.Login.cshtml` if it exists, else create a minimal new one).

### Self-Registration Removal + Admin User Creation

- **D-09:** Remove `AccountController.Register` GET, POST, and `Register.cshtml` (and `Register.Mobile.cshtml`) entirely. The `/account/register` route will return 404. Any nav links to Register in views should also be removed.

- **D-10:** New `CreateUser` GET + POST actions in `AdminController` at route `/admin/create-user`. Already protected by `[Authorize(Policy = "AdminOnly")]` (inherits from the controller attribute). The admin is already on the Users management page when they create a user.

- **D-11:** The `CreateUser` form fields: email, display name, password, GroupRole picker (Player / DungeonMaster / Admin — all three options available). After `userService.CreateAsync` succeeds, call `userService.SetGroupRoleAsync(userId, activeGroupContext.ActiveGroupId.Value, selectedGroupRole)` to assign the new user to the admin's active group with the chosen role.

- **D-12:** Email confirmation flow for admin-created accounts reuses the existing `ConfirmationEmailJob` — same as self-registration used. Enqueue the job immediately after account creation (REG-03).

- **D-13:** After successful `CreateUser` POST, redirect to `nameof(Users)` (the Users list). TempData success message: "Account created for {name}. A confirmation email has been sent."

### Navigation Group Display

- **D-14:** Use `@inject IActiveGroupContext activeGroupContext` in `_Layout.cshtml` (and `_Layout.Mobile.cshtml`). The group name requires an additional `IGroupService` inject to look up the name by `ActiveGroupId`. Alternatively, the `GroupPickerController.SelectGroup` POST can store the group name in session alongside the ID (as `SessionKeys.ActiveGroupName`) to avoid the extra DB lookup per request.

- **D-15:** The existing user dropdown in `_Layout.cshtml` (lines ~127–143) gets a new menu item added after Profile, before Logout:
  ```html
  <li><hr class="dropdown-divider"></li>
  <li>
      <a class="dropdown-item" asp-controller="GroupPicker" asp-action="Index">
          <i class="fas fa-arrows-rotate me-2"></i>@activeGroupContext.ActiveGroupName
      </a>
  </li>
  ```
  Clicking this link navigates to `/groups/pick` (the GroupPickerController Index), which re-runs the group selection flow. The same item is added to `_Layout.Mobile.cshtml`.

- **D-16 (Claude's discretion):** Whether to store the group name in session (as `SessionKeys.ActiveGroupName`) or look it up via `IGroupService` on each request is left to the planner. Storing in session avoids a DB call per page load; looking it up always stays in sync if the group is renamed.

### ?? 1 Fallback Removal

- **D-17:** All `?? 1` fallbacks introduced in Phase 28/29 are REMOVED in Phase 30 once the group-picker sets `SessionKeys.ActiveGroupId` at login. Specifically:
  - `AdminController.Users()` — remove `groupId ?? 1`
  - `IUserService.GetAllPlayersAsync` — remove `?? 1` fallback
  - `IUserService.GetAllDungeonMastersAsync` — remove `?? 1` fallback

  This is a **locked requirement** from STATE.md. The planner must include these removals in plan scope.

### Mobile View Parity

- **D-18:** All new views created in Phase 30 require a `.Mobile.cshtml` variant:
  - `GroupPicker/Index.Mobile.cshtml` — same cards, stack naturally on mobile
  - `Admin/CreateUser.Mobile.cshtml` — the create user form
  - `_Layout.Mobile.cshtml` — add D-15 group switch item to the mobile layout dropdown (if mobile layout has an equivalent dropdown)

- **D-19:** Delete `Views/Account/Register.Mobile.cshtml` alongside `Views/Account/Register.cshtml`.

### Claude's Discretion

- Whether `GroupPickerController` lives under `Controllers/` (no area) or a new `Controllers/GroupPicker/` subdirectory
- Group name storage strategy: session vs. per-request DB lookup (see D-16)
- Exact layout file name for the picker page (`_Layout.GroupPicker.cshtml` vs. reusing the login layout)
- Whether `GroupPickerController.Index` is protected with `[Authorize]` only (no policy) — the group picker is accessible to any authenticated user, not role-restricted
- CSS class for the "Go to Platform →" button on the picker page (functional, not decorative)
- Exact column count for the group cards grid (likely auto-fit or 2-3 columns)

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Requirements & Scope
- `.planning/REQUIREMENTS.md` §Group UX (UX-01–UX-05) — login routing, group picker, session persistence, nav display requirements
- `.planning/REQUIREMENTS.md` §Management Area (MGMT-07, MGMT-08) — group admin user creation and promote/demote
- `.planning/REQUIREMENTS.md` §User Creation (REG-01, REG-02, REG-03) — remove self-registration, auto-assign to group, trigger email confirmation
- `.planning/ROADMAP.md` §Phase 30 — phase goal, success criteria, dependency on Phase 29

### Prior Phase Decisions (locked, do not re-litigate)
- `.planning/phases/29-superadmin-role-and-management-area/29-CONTEXT.md` — D-03: null ActiveGroupId → context.Fail() (403); D-09: promote/demote write to UserGroups; ?? 1 fallback warning and Phase 30 mandate to remove them
- `.planning/phases/28-tenant-isolation/28-CONTEXT.md` — D-02: ActiveGroupContextService reads SessionKeys.ActiveGroupId from ISession; D-09: SetGroupId on concrete class; D-10/D-11: test stub pattern
- `.planning/STATE.md` §Key Architectural Decisions (v5.0) — locked: per-group roles in UserGroups.GroupRole; SessionKeys.ActiveGroupId session key; ?? 1 fallback removal mandate for Phase 30

### Key Files to Modify
- `QuestBoard.Service/Controllers/Admin/AccountController.cs` — modify Login POST to redirect to /groups/pick; remove Register GET/POST
- `QuestBoard.Service/Controllers/Admin/AdminController.cs` — add CreateUser GET/POST; remove ?? 1 fallbacks from Users()
- `QuestBoard.Service/Views/Shared/_Layout.cshtml` — add @inject + group switch dropdown item (D-14, D-15)
- `QuestBoard.Service/Views/Shared/_Layout.Mobile.cshtml` — same changes as _Layout.cshtml (D-15, D-18)
- `QuestBoard.Service/Domain/Interfaces/IUserService.cs` — verify GetAllPlayersAsync / GetAllDungeonMastersAsync signatures after ?? 1 removal
- `QuestBoard.Service/Constants/SessionKeys.cs` — may need ActiveGroupName constant (D-16)

### Files to Delete
- `QuestBoard.Service/Views/Account/Register.cshtml`
- `QuestBoard.Service/Views/Account/Register.Mobile.cshtml`
- `QuestBoard.Service/Controllers/Admin/AccountController.cs` Register GET/POST actions (not the whole file)

### New Files to Create
- `QuestBoard.Service/Controllers/GroupPickerController.cs`
- `QuestBoard.Service/Views/GroupPicker/Index.cshtml`
- `QuestBoard.Service/Views/GroupPicker/Index.Mobile.cshtml`
- `QuestBoard.Service/Views/Shared/_Layout.GroupPicker.cshtml` (or reuse login layout)
- `QuestBoard.Service/Views/Admin/CreateUser.cshtml`
- `QuestBoard.Service/Views/Admin/CreateUser.Mobile.cshtml`
- `QuestBoard.Service/ViewModels/AdminViewModels/CreateUserViewModel.cs`
- `QuestBoard.Service/ViewModels/GroupPickerViewModels/GroupPickerViewModel.cs`

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `SessionKeys.ActiveGroupId` — already defined at `QuestBoard.Service/Constants/SessionKeys.cs`; `GroupPickerController` writes to `HttpContext.Session.SetInt32(SessionKeys.ActiveGroupId, groupId)` using the same key `ActiveGroupContextService` reads
- `IGroupService` / `GroupService` — already exists from Phase 29; `GetAllAsync()` returns all groups with member counts (via `GroupWithMemberCount` DTO). Use to populate the picker and look up group name.
- `ConfirmationEmailJob` — already exists and handles email confirmation. `AdminController.CreateUser` enqueues it exactly as the old `AccountController.Register` did.
- `userService.CreateAsync(email, name, password)` — existing method; no changes needed
- `userService.SetGroupRoleAsync(userId, groupId, role)` — exists from Phase 29; used to assign new user to admin's group
- `modern-card` CSS classes — already available for the group picker cards grid

### Established Patterns
- **Login layout:** `Views/Account/Login.cshtml` uses a stripped-down layout — check which layout it specifies in `@{ Layout = "..."; }` to reuse for the picker
- **Mobile views:** Existing `.Mobile.cshtml` files in `Views/Account/` and `Views/Admin/` as templates for new mobile views; view-location expander already wired in Phase 12
- **AdminController admin actions:** `EditUser`, `ResetPassword` — same `[ValidateAntiForgeryToken]` + `RedirectToAction(nameof(Users))` on success pattern for `CreateUser`
- **GroupRole enum:** `QuestBoard.Domain.Enums.GroupRole` — Player / DungeonMaster / Admin; use in `CreateUserViewModel.GroupRole` property and picker dropdown

### Integration Points
- `AccountController.Login POST` — change the success redirect from `RedirectToLocal(returnUrl)` to `Redirect("/groups/pick?returnUrl=<encoded>")` (or `RedirectToAction("Index", "GroupPicker", new { returnUrl })`)
- `Program.cs` — register `GroupPickerController` route (should work with default MVC route `{controller}/{action}/{id?}`); no area route needed
- `_Layout.cshtml` lines 127–143 — user dropdown; add `@inject` directives at top of file; add group switch item between Profile and Logout
- `AdminController.Users()` — remove `groupId ?? 1` (D-17); query now correctly uses `activeGroupContext.ActiveGroupId` which Phase 30 guarantees is always set

### Known Landmines
- `GroupPickerController.Index` must handle the case where a logged-in user somehow has zero group memberships (e.g. SuperAdmin before Phase 27 data migration ran). Guard: if no groups found and not SuperAdmin, show an error message rather than crashing.
- The `returnUrl` passed from Login to picker to final redirect must be URL-encoded to survive as a query parameter. Use `Url.Action` with `returnUrl` as a route value, or encode it manually.
- `ISession.SetInt32` / `GetInt32` are extension methods from `Microsoft.AspNetCore.Http`; no additional packages needed.
- `??  1` fallback removal (D-17) will cause null reference or 403 if a user somehow reaches those actions before group context is set. Phase 30's login redirect ensures this can't happen in normal flow, but integration tests must set `factory.GroupContext.ActiveGroupId = 1` (already the default from Phase 28 D-10).
- The `MGMT-08` requirement (promote/demote within group) is largely already satisfied by the existing `AdminController` promote/demote actions — Phase 29 D-09 updated them to write to `UserGroups.GroupRole` and Phase 30's ?? 1 removal makes them work correctly. The planner should verify this works end-to-end rather than building new code.

</code_context>

<specifics>
## Specific Ideas

- **Group switch dropdown item** (D-15): appears after Profile, before Logout. Icon: `fas fa-arrows-rotate`. Link text: the current group name. Clicking navigates to `/groups/pick` (GroupPickerController.Index). The divider before this item goes between Profile and the new item.
- **SuperAdmin picker extras** (D-07): "Go to Platform →" button is a distinct button (not a card), visually separated from the group cards. Could use a secondary "outline" button style or a card with a different color. Route: `/platform`.
- **Cards click behavior** (D-06): clicking a group card should submit a POST (with anti-forgery token) to `SelectGroup`. Use a form around each card or a hidden form + JavaScript `submit()` on card click, consistent with how the existing site handles button-triggered POSTs.

</specifics>

<deferred>
## Deferred Ideas

- **Redirect to group picker on expired session mid-request** — instead of 403, redirect authenticated users with no active group to `/groups/pick`. Deferred: adds middleware complexity; Phase 29's existing 403 behavior is acceptable for v5.0.
- **Group name in nav bar permanently visible** (badge next to user name outside dropdown) — user decided dropdown-only is sufficient.
- **Per-group email configuration** — future milestone per REQUIREMENTS.md §Future Requirements.
- **Group invitation flow** (invite by email link) — future milestone.

None — discussion stayed within phase scope.

</deferred>

---

*Phase: 30-group-ux-admin-user-creation*
*Context gathered: 2026-06-30*
