# Phase 40: Platform Members Page Redesign - Research

**Researched:** 2026-07-04
**Domain:** ASP.NET Core MVC — controller/query/view redesign (no new external dependencies)
**Confidence:** HIGH

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions

- **D-01:** Search is a server round-trip — a GET request with a `search` query parameter that reloads the Members page with the non-member table filtered, mirroring `Views/Shop/Index.cshtml`'s existing filter-row pattern (`shop-filter-form`, `BuildTabUrl`-style query-string construction). Not a client-side JS instant filter.
- **D-02:** Each non-member row shows Name + Email only — matches today's dropdown option text (`@u.Name (@u.Email)`). No new "other group memberships" column; no new repository query needed for this.
- **D-03:** Each non-member row gets an inline role dropdown (defaulting to `GroupRole.Player`) and a small "Add" button — one action per row, replacing today's separate "select user, pick role, submit" form below the table entirely. The standalone `AddMember` form section is removed from the view.
- **D-04:** Submitting "Add" for a row redirects back to `Members` preserving the current `search` query string (not resetting to the unfiltered list).
- **D-05:** Create New User is a Bootstrap modal triggered by a button in the right column — mirrors `Views/ShopManagement/Index.cshtml`'s modal-with-form-post pattern (`modal fade` → `modal-dialog` → `modal-content` containing `<form method="post">` with `@Html.AntiForgeryToken()`). Not an inline expandable panel.
- **D-06:** The modal's form fields mirror `Views/Admin/CreateUser.cshtml` exactly (Email, Name, GroupRole dropdown) but posts to a new Platform-area action that takes `groupId` from the route (the `Members` page's `id`), never from `IActiveGroupContext`. **Never inject `IActiveGroupContext` into `GroupController`.**
- **D-07:** The new Platform create-user action reuses `IUserService.CreateOrAddToGroupAsync` (Phase 39) and reuses its exact flash-message wording verbatim for all four outcomes, applying the same `RedirectWithSuccess`/`RedirectWithWarning` helper pattern `AdminController.CreateUser` already uses:
  - New account: `"Account created for {Name}. A welcome email with a set-password link has been sent."`
  - Added to group (both `AddedToGroup` and `AddedToGroupStrandedAccount` outcomes): `"{Name} has been added to the group as {Role}. A notification email has been sent."`
  - Already a member: `"{Name} is already a member of this group."` via `RedirectWithWarning`
  - No SuperAdmin-specific copy variant.
- **D-08:** `Members.Mobile.cshtml` stacks the two columns vertically — current members list first (unchanged), then the search box + non-member cards, then the Create New User trigger below — no tab/toggle switch.
- **D-09 (Claude's discretion, resolved by this research — see Pattern 4):** Whether Create New User stays a modal on mobile or becomes a full-page form.

### Claude's Discretion (resolved below in this document)

- Exact non-member repository/service query method name for "users not in this group, matching search" — resolved in **Architecture Patterns → Pattern 1**.
- Exact new Platform action name/route for the create-user entry point (must accept `groupId` from the route per D-06) — resolved in **Architecture Patterns → Pattern 3**.
- Whether the per-row inline "Add" control needs its own antiforgery-protected mini-form per row, or a single delegated form/JS submit — resolved in **Architecture Patterns → Pattern 2**.
- D-09 (mobile Create New User presentation) — resolved in **Architecture Patterns → Pattern 4**.

### Deferred Ideas (OUT OF SCOPE)

None raised during discussion — all four selected gray areas stayed within this phase's scope.

</user_constraints>

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|------------------|
| MEMBERS-01 | Two-column layout — members left, non-members + Add User right | Verified current `Members.cshtml`/`Members.Mobile.cshtml` single-column-plus-dropdown structure (see Code Examples); confirmed modern-card wrapper convention applies to both columns |
| MEMBERS-02 | Right-hand list searchable/filterable by name or email (not a plain dropdown) | Verified `Views/Shop/Index.cshtml` GET+query-string filter-row pattern to mirror (D-01); confirmed `UserEntity` has no EF Core global query filter, so the "not-in-group + search" query must be an explicit manual join (Pattern 1) |
| MEMBERS-03 | "Create New User" entry point creates/adds a user scoped to the group, without a session-level active group | Verified exact `AdminController.CreateUser` call shape (`CreateOrAddToGroupAsync(email, name, groupId, role)`) that takes `groupId` as a plain parameter — directly reusable from `GroupController` using the route's `id`, confirmed no `IActiveGroupContext` dependency required |

</phase_requirements>

## Summary

This phase is a pure in-codebase redesign: no new NuGet packages, no new external services, no new infrastructure. Every pattern this phase needs already exists somewhere in the codebase and just needs to be mirrored into `GroupController`/`Members.cshtml`/`Members.Mobile.cshtml`. The three open discretion questions from CONTEXT.md (query method shape, per-row Add form mechanism, mobile modal presentation) all have clear, low-risk answers once the actual code is inspected — this research resolves all three so the planner can write concrete task diffs rather than exploring alternatives.

The riskiest part of this phase is not technical difficulty but precision: `GroupController.Members` today does an in-memory `allUsers.Where(u => !memberUserIds.Contains(u.Id))` over `userService.GetAllAsync()` (all platform users, no group scoping needed since it's the inverse of the group's own members) — this must become a search-filtered, DB-side query. `UserEntity` has no EF Core global query filter (confirmed: no `HasQueryFilter` call for `UserEntity` exists anywhere in `QuestBoardContext.cs`), so the query needs the same explicit manual-join style already used by `UserRepository.GetAllGroupMembers` (Phase 38) — just negated (`!DbContext.UserGroups.Any(...)`) and with a `Contains` filter added for the search term.

**Primary recommendation:** Add `IUserRepository.GetAvailableUsersAsync(int groupId, string? search, CancellationToken)` / `IUserService.GetAvailableUsersAsync(...)` following the exact `GetAllGroupMembers` manual-join shape (negated), reshape `GroupMembersViewModel` to carry `SearchQuery` + per-row `AddMemberRowViewModel` entries, keep the existing per-row `<form>` pattern (mirroring today's `RemoveMember` form, NOT a JS-delegated single form) for the inline Add action, add a new `CreateMember` POST action on `GroupController` that calls `CreateOrAddToGroupAsync` with `id` from the route, and reuse the Bootstrap modal pattern from `ShopManagement/Index.cshtml` verbatim (including reusing it unchanged on mobile, per that view's own Mobile variant).

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|-------------|----------------|-----------|
| Two-column Members page layout | Frontend Server (Razor View) | — | Pure view-layer change to `Members.cshtml`/`.Mobile.cshtml`; no new client-side JS framework needed |
| Non-member search/filter | API/Backend (Controller + Domain query) | Frontend Server (GET form) | Server round-trip (D-01) — filtering logic lives in a repository query, not JS; the view only renders a `<form method="get">` |
| Add-to-group action | API/Backend (Controller → `IGroupService.AddMemberAsync`) | Frontend Server (per-row form) | Existing `AddMemberAsync` throw-on-collision method already does this; only the trigering form's shape changes (per-row instead of single form) |
| Create New User entry point | API/Backend (Controller → `IUserService.CreateOrAddToGroupAsync`) | Frontend Server (Bootstrap modal + form POST) | Reuses Phase 39's Domain-layer method unchanged; modal is pure presentation, posts a normal form |
| Authorization | API/Backend (`[Authorize(Policy = "SuperAdminOnly")]`, already class-level) | — | No new authorization work — already enforced on `GroupController` |
| Database query for "available users" | Database/Storage (manual join, no query filter) | — | `UserEntity` has no EF Core Global Query Filter; must be an explicit `Where` + `DbContext.UserGroups.Any(...)` negation, same shape as Phase 38's `GetAllGroupMembers` |

## Standard Stack

No new libraries. This phase is 100% additive/modificative work within the existing ASP.NET Core 10 MVC + EF Core 10 + Bootstrap stack already in the codebase.

### Core (existing, reused — no version changes)
| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| Microsoft.EntityFrameworkCore (via EFCore.Tools) | 10.0.9 [VERIFIED: `QuestBoard.Service.csproj` and `QuestBoard.Repository.csproj`] | Manual-join LINQ query for available-users search | Already used identically in `UserRepository.GetAllGroupMembers`/`GetAllDungeonMasters` |
| Bootstrap (bundled, via `_Layout.cshtml`) | Already in use — modal/alert/badge classes throughout | Modal, alert, table, badge markup | `ShopManagement/Index.cshtml`, `Views/Admin/CreateUser.cshtml` already establish every visual pattern this phase needs |

### Supporting

None new. `AutoMapper` (already registered) continues to map `User`/`UserGroup` domain models to entities as it does today — no new mapping profiles needed since no new domain models are introduced (only a new ViewModel shape and one new repository/service method signature).

### Alternatives Considered

| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| Server round-trip GET search (D-01, locked) | Client-side JS instant filter (e.g. simple `Array.filter` over rendered rows) | Rejected by user explicitly — app has zero client-side JS filtering dependencies today (per REQUIREMENTS.md "Out of Scope": "JS grid/DataTables library or AJAX search... overkill at ~17 users") |
| Manual EF join for "not in group" query | A `NOT IN` subquery via raw SQL, or client-side set-difference in C# (today's approach) | Manual join (LINQ `!DbSet.Any(...)`) keeps filtering DB-side so `search` and "not-in-group" combine into one SQL query instead of loading all users into memory first — matches existing Phase 38 precedent exactly |

**Installation:** None required — no new packages.

**Version verification:** Verified via `grep` against `QuestBoard.Service.csproj`/`QuestBoard.Repository.csproj`: target framework `net10.0`, EF Core packages at `10.0.9`. No package changes needed for this phase — confirmed no new `PackageReference` entries are required.

## Package Legitimacy Audit

Not applicable — this phase installs zero external packages. No `npm install`/`dotnet add package` commands are needed; every dependency this phase touches (`Microsoft.EntityFrameworkCore`, Bootstrap via existing `_Layout.cshtml`) is already present and unchanged in version.

**Packages removed due to [SLOP] verdict:** none (N/A — no packages evaluated)
**Packages flagged as suspicious [SUS]:** none (N/A — no packages evaluated)

## Architecture Patterns

### System Architecture Diagram

```
GET /platform/Group/Members/{id}?search=foo
        │
        ▼
GroupController.Members(id, search)
        │
        ├──▶ groupService.GetByIdAsync(id)                     ─┐
        ├──▶ groupService.GetMembersAsync(id)                    │  left column data
        │                                                        ─┘
        └──▶ userService.GetAvailableUsersAsync(id, search)     ── right column data
                    │
                    ▼
             UserRepository.GetAvailableUsers(groupId, search)
                    │  DbSet<UserEntity>
                    │    .Where(u => !DbContext.UserGroups.Any(ug => ug.UserId==u.Id && ug.GroupId==groupId))
                    │    .Where(u => search==null || u.Name.Contains(search) || u.Email.Contains(search))
                    ▼
             returns IList<User> (mapped via AutoMapper)
        │
        ▼
View(GroupMembersViewModel { Group, Members, AvailableUsers, SearchQuery })
        │
        ▼
Members.cshtml (two-column) / Members.Mobile.cshtml (stacked)
   ├── Left: existing Members table (unchanged) + RemoveMember per-row form
   ├── Right: GET filter form (search box, D-01) + per-row Add form (D-03/D-04)
   └── Right: "Create New User" button → Bootstrap modal (D-05)


POST /platform/Group/AddMember/{id}   (per-row Add, preserves ?search=)
        │
        ▼
GroupController.AddMember(id, userId, role, search)
        │
        ▼
groupService.AddMemberAsync(id, userId, role)   ── unchanged (Phase 38/existing, throw-on-collision)
        │
        ▼
RedirectToAction(Members, { id, search })         ── D-04: preserve filter


POST /platform/Group/CreateMember/{id}   (modal form, D-05/D-06)
        │
        ▼
GroupController.CreateMember(id, CreateMemberViewModel)
        │
        ▼
userService.CreateOrAddToGroupAsync(email, name, id, role)   ── Phase 39 shared method, groupId from route
        │
        ├─ NewAccountCreated           → enqueue WelcomeEmailJob            → RedirectWithSuccess
        ├─ AddedToGroup                → enqueue GroupMembershipAddedEmailJob → RedirectWithSuccess
        ├─ AddedToGroupStrandedAccount → enqueue WelcomeEmailJob (resend)   → RedirectWithSuccess (same text)
        ├─ AlreadyMember               → (no email)                        → RedirectWithWarning
        └─ Failed                      → ModelState errors                 → re-render Members with modal open
```

### Recommended Project Structure

No new folders. Files touched:
```
QuestBoard.Domain/
├── Interfaces/IUserRepository.cs      # + GetAvailableUsers(groupId, search, token)
├── Interfaces/IUserService.cs         # + GetAvailableUsersAsync(groupId, search, token)
└── Services/UserService.cs            # delegates to repository

QuestBoard.Repository/
└── UserRepository.cs                  # + GetAvailableUsers manual-join query (Pattern 1)

QuestBoard.Service/
├── Areas/Platform/Controllers/GroupController.cs   # Members(id, search) + AddMember adapted + new CreateMember action
├── Areas/Platform/Views/Group/Members.cshtml        # two-column redesign
├── Areas/Platform/Views/Group/Members.Mobile.cshtml # stacked redesign
├── ViewModels/PlatformViewModels/GroupMembersViewModel.cs   # + SearchQuery, reshaped AvailableUsers entries
├── ViewModels/PlatformViewModels/AddMemberViewModel.cs      # kept (per-row Role binding) or superseded — see Pattern 2
└── ViewModels/PlatformViewModels/CreateMemberViewModel.cs   # new — mirrors CreateUserViewModel (Email, Name, GroupRole)
```

### Pattern 1: Group-scoped "available users" query (negated manual join)

**What:** A repository method returning platform users who are NOT members of the given group, optionally filtered by a search term matching Name or Email.
**When to use:** `GroupController.Members` GET action, to populate the right-hand column.
**Why this shape:** `UserEntity` (`QuestBoard.Repository/Entities/UserEntity.cs`) has no `HasQueryFilter` registration anywhere in `QuestBoardContext.cs` (confirmed via direct grep — zero matches). Every other group-scoped user query in this codebase (`GetAllDungeonMasters`, `GetAllPlayers`, `GetAllGroupMembers`) uses the identical `DbSet.Where(u => DbContext.UserGroups.Any(ug => ...))` manual-join shape. This new method is simply the **negation** of `GetAllGroupMembers`, with an additional search predicate.

**Example (mirrors verified `UserRepository.GetAllGroupMembers`, `QuestBoard.Repository/UserRepository.cs:50-59`):**
```csharp
// Source: existing UserRepository.GetAllGroupMembers, negated + search predicate added
/// <inheritdoc/>
public async Task<IList<User>> GetAvailableUsers(int groupId, string? search, CancellationToken token = default)
{
    var query = DbSet
        .Where(u => !DbContext.UserGroups
            .Any(ug => ug.UserId == u.Id && ug.GroupId == groupId));

    if (!string.IsNullOrWhiteSpace(search))
    {
        query = query.Where(u =>
            u.Name.Contains(search) ||
            (u.Email != null && u.Email.Contains(search)));
    }

    var entities = await query.ToListAsync(cancellationToken: token);
    return Mapper.Map<IList<User>>(entities);
}
```

Naming: `GetAvailableUsers` (repository, matches existing non-`Async`-suffixed repository interface naming — e.g. `GetAllGroupMembers`, `GetAllDungeonMasters` in `IUserRepository`) / `GetAvailableUsersAsync` (service layer, matches `IUserService`'s `Async`-suffixed convention — e.g. `GetAllGroupMembersAsync`). This exact repository-vs-service naming asymmetry already exists for every other paired method in these two interfaces — not a new inconsistency.

**Case sensitivity note:** `u.Name.Contains(search)` on SQL Server with the database's default collation is case-insensitive already (same behavior relied on implicitly by `ExistsAsync`'s explicit `StringComparison.CurrentCultureIgnoreCase`, which is only needed there because it's an equality check, not a `Contains`). No special handling needed for the search predicate beyond what's shown above — confirmed no other repository method in this codebase does anything more elaborate for a `Contains`-based text filter (verified via `Views/Shop`'s equivalent search, which also does a plain EF `Contains`).

### Pattern 2: Per-row inline Add — plain form-per-row (NOT a delegated single form)

**What:** Each non-member row gets its own `<form method="post">` containing a hidden `UserId` input, a `Role` select, an antiforgery token, and a submit button.
**When to use:** The right-hand column's per-row Add action (D-03).
**Why this shape, not a JS-delegated single form:** The codebase's only existing "trigger an action from a table row" precedent that reuses a *single* shared element is the **modal** pattern (`ShopManagement/Index.cshtml`'s `denyModal`, populated via `data-item-id`/`data-item-name` attributes and a `show.bs.modal` JS listener that rewrites the form's `action` attribute) — that pattern exists specifically because a modal needs a confirmation/reason-entry step. There is no precedent anywhere in this codebase for a plain (non-modal) multi-row action using a single delegated form or JS submit — the "Bulk Actions" button in `ShopManagement/Index.cshtml` is an unimplemented placeholder (`alert(...)`, not a real submit) and does not establish a working precedent. The one working precedent for "trigger a POST from a specific table row, no confirmation needed" is today's own `Members.cshtml` `RemoveMember` form (`<form asp-action="RemoveMember" ... class="d-inline">` with a hidden `userId` input) — a plain per-row form. The Add action should mirror this exactly, just with an added `Role` select input in the same per-row form.

**Example (mirrors verified `RemoveMember` form, `Areas/Platform/Views/Group/Members.cshtml:75-80`):**
```html
<!-- Source: existing RemoveMember form pattern, extended with a Role select for Add -->
<form asp-action="AddMember" asp-route-id="@Model.Group.Id" asp-route-search="@Model.SearchQuery"
      method="post" asp-antiforgery="true" class="d-inline-flex align-items-center gap-2">
    <input type="hidden" name="UserId" value="@u.Id" />
    <select name="Role" class="form-select form-select-sm" style="width:auto;">
        @foreach (var role in Enum.GetValues<GroupRole>())
        {
            <option value="@role" selected="@(role == GroupRole.Player)">@role</option>
        }
    </select>
    <button type="submit" class="btn btn-sm btn-success">
        <i class="fas fa-user-plus me-1"></i>Add
    </button>
</form>
```

Each row's form independently carries `@Html.AntiForgeryToken()` via `asp-antiforgery="true"` (Tag Helper equivalent, already used on every existing form in this view) — one antiforgery token per row is the same cost the codebase already pays for `RemoveMember`'s per-row forms today, so this is not new overhead, just one more input per row.

**D-04 (preserve search across Add submit):** Pass `search` through as an `asp-route-search` value on the form (shown above) so the controller action's redirect can echo it back: `RedirectToAction(nameof(Members), new { id, search })`.

### Pattern 3: New Platform create-user action — route-scoped groupId

**What:** A new `[HttpGet]`/`[HttpPost]` action pair on `GroupController` (or POST-only, since the modal already lives on the `Members` GET view) that calls `CreateOrAddToGroupAsync` with `groupId` sourced strictly from the route.
**When to use:** The Create New User modal's form target (D-05/D-06).
**Naming:** `CreateMember` — mirrors the existing `AddMember`/`RemoveMember` naming convention on this same controller (verb + noun, no "User" suffix needed since the controller is already scoped to `Group`). A GET action is unnecessary since the form lives inline in the modal on the `Members` view — only a `[HttpPost]` is needed, matching `AddMember`/`RemoveMember`'s shape (no separate GET view).

**Example (mirrors verified `AdminController.CreateUser` POST call shape, `Controllers/Admin/AdminController.cs:113-190`, adapted for route-sourced groupId):**
```csharp
// Source: exact call shape verified in AdminController.CreateUser (Phase 39),
// groupId sourced from the route parameter instead of IActiveGroupContext.
[HttpPost]
[ValidateAntiForgeryToken]
public async Task<IActionResult> CreateMember(int id, CreateMemberViewModel model)
{
    if (!ModelState.IsValid)
    {
        // Re-render Members with the modal's validation errors and current available-users list
        var group = await groupService.GetByIdAsync(id);
        if (group == null) return RedirectToAction(nameof(Index));
        var members = await groupService.GetMembersAsync(id);
        var availableUsers = await userService.GetAvailableUsersAsync(id, null);
        return View(nameof(Members), new GroupMembersViewModel
        {
            Group = group, Members = members, AvailableUsers = availableUsers, CreateMember = model
        });
    }

    var result = await userService.CreateOrAddToGroupAsync(model.Email, model.Name, id, model.GroupRole);

    switch (result.Outcome)
    {
        case CreateOrAddToGroupOutcome.NewAccountCreated:
            // ... identical WelcomeEmailJob enqueue + RedirectWithSuccess as AdminController.CreateUser
        case CreateOrAddToGroupOutcome.AddedToGroup:
        case CreateOrAddToGroupOutcome.AddedToGroupStrandedAccount:
            // ... identical GroupMembershipAddedEmailJob / resend-WelcomeEmailJob + RedirectWithSuccess, verbatim D-07 text
        case CreateOrAddToGroupOutcome.AlreadyMember:
            return this.RedirectWithWarning(nameof(Members), $"{result.Name} is already a member of this group.");
    }
    // ...
    return RedirectToAction(nameof(Members), new { id });
}
```

**Constructor note:** `GroupController` currently takes `(IGroupService groupService, IUserService userService)`. The new action needs `IIdentityService` (for `GeneratePasswordResetTokenForUserAsync`), `IBackgroundJobClient` (Hangfire), and `ILogger<GroupController>` — the exact same additional dependencies `AdminController` already carries for its equivalent `CreateUser` action. **Do not inject `IActiveGroupContext`** — this is the explicit hard constraint from STATE.md's risk flag (Phase 30 and Phase 34.3 both had bugs from exactly this session/route confusion).

### Pattern 4: Mobile Create-New-User presentation — modal (not full page)

**What:** Resolves D-09. The Create New User entry point stays a Bootstrap modal on mobile, identical to desktop.
**Why:** Verified via direct file inspection — `ShopManagement/Index.Mobile.cshtml` already reuses the exact same `#denyModal` markup and JS as `ShopManagement/Index.cshtml` (confirmed via grep: `denyModal`, `denyForm`, `denyItemName` all appear in both files with the same IDs/behavior). This is the codebase's only existing precedent for "a Bootstrap modal on a page that also has a `.Mobile.cshtml` variant," and it shows modals are reused unchanged, not replaced with a full-page form. The `Manage.cshtml`/`Manage.Mobile.cshtml` split (referenced in CONTEXT.md as the alternative pattern to consider) is a full **page-level** split for two entirely different layouts, not a precedent for swapping a modal-triggered form specifically for a full-page form on mobile — no such swap exists anywhere in this codebase.

**Recommendation:** `Members.Mobile.cshtml` includes the identical Create New User modal markup/JS as `Members.cshtml` (same modal `id`, same form fields) — no new mobile-specific presentation logic needed. Bootstrap modals are inherently responsive (confirmed rendering behavior already relied upon by `ShopManagement.Mobile.cshtml`).

### Anti-Patterns to Avoid

- **Injecting `IActiveGroupContext` into `GroupController`:** Hard-blocked by STATE.md risk flag — this exact confusion already caused two prior incidents (Phase 30, Phase 34.3). The new `CreateMember` action must take `groupId` only from the route parameter `id`.
- **Loading all platform users into memory then filtering in C#:** Today's `Members` GET does exactly this (`userService.GetAllAsync()` then `.Where(...)` in-memory) — acceptable at ~17 users but should not be extended to also do search-string matching in-memory. Push both the not-in-group filter and the search filter into the same EF query (Pattern 1) since we're touching this code anyway.
- **Building a JS-delegated single form for the per-row Add action:** No precedent exists for this outside the modal confirmation-step use case (Pattern 2) — don't introduce one for a plain single-click action when a per-row `<form>` (already used for `RemoveMember`) works identically with less code.
- **Adding a client-side instant-filter JS library:** Explicitly out of scope per REQUIREMENTS.md and D-01 — the round-trip GET pattern is a locked decision, not a discretion item.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Collision-aware user creation (email already exists) | A second inline `CreateAsync`/`SetGroupRoleAsync` branch duplicated from `AdminController` | `IUserService.CreateOrAddToGroupAsync` (Phase 39, already ships all 4 outcomes + tests) | This codebase has a documented recurring bug class of duplicated logic diverging across call sites (`GetActiveBoardTypeAsync` 3x precedent, cited in PROJECT.md) — Phase 39 was built specifically so Phase 40 doesn't repeat this |
| Antiforgery protection per form | Custom token validation | `asp-antiforgery="true"` Tag Helper / `[ValidateAntiForgeryToken]` (already used on every POST action in `GroupController`) | Framework-provided, already the established pattern; per-row forms each get their own token automatically via the Tag Helper |
| Flash messaging for the new create-user action | A new TempData key/banner style | Existing `RedirectWithSuccess`/`RedirectWithWarning` (`ControllerExtensions.cs`, added in Phase 39) | Already exists, already styled (`alert-success`/`alert-warning`), reuse verbatim per D-07 |

**Key insight:** Every "hard part" of this phase (collision handling, email dispatch, flash styling) was already built in Phase 39. This phase's actual net-new code is small: one repository query method, one reshaped ViewModel, one new controller action that's ~90% a copy of `AdminController.CreateUser`'s existing switch statement, and view markup changes.

## Common Pitfalls

### Pitfall 1: Forgetting to preserve `search` across the Add-to-group redirect
**What goes wrong:** SuperAdmin filters the non-member list, clicks Add on a row, and lands back on the unfiltered full list — losing their place (explicitly called out as unwanted in D-04).
**Why it happens:** `RedirectToAction(nameof(Members), new { id })` is the natural/default thing to write; it's easy to forget the extra route value.
**How to avoid:** Route value must include `search` — `RedirectToAction(nameof(Members), new { id, search })`. The per-row form must carry the current search term as a hidden/route value so the controller has it to redirect with (see Pattern 2's `asp-route-search`).
**Warning signs:** Manual QA — filter for a name, click Add, check the resulting page still shows the filter term in the search box and only matching rows.

### Pitfall 2: Sourcing `groupId` from `IActiveGroupContext` instead of the route
**What goes wrong:** The new create-user action silently creates/adds a user to whatever group happens to be the SuperAdmin's *session-level active group* instead of the group whose Members page they're viewing — a data-integrity bug where the wrong group gets the new member.
**Why it happens:** `AdminController.CreateUser` (the pattern being mirrored) does use `activeGroupContext.ActiveGroupId` — copying that pattern verbatim without noticing `GroupController` is a different controller with no session-group concept is an easy mistake, especially since it's the exact bug class that already happened twice (Phase 30, Phase 34.3, per STATE.md).
**How to avoid:** `groupId` must come only from the `id` route parameter (matches `Members`/`AddMember`/`RemoveMember`'s existing convention on this same controller). Never add `IActiveGroupContext` to `GroupController`'s constructor.
**Warning signs:** Code review — any new reference to `IActiveGroupContext` or `activeGroupContext` anywhere in `GroupController.cs` is a red flag and should block the PR.

### Pitfall 3: In-memory filtering defeating the purpose of the new query method
**What goes wrong:** `GetAvailableUsersAsync` is added but the controller still calls `userService.GetAllAsync()` and filters in C#, making the new repository method dead code or only partially used.
**Why it happens:** The existing `Members` action already does in-memory filtering; it's tempting to just add a `.Where(u => u.Name.Contains(search) || ...)` on top of the existing in-memory `availableUsers` list rather than pushing both predicates into the DB query.
**How to avoid:** Replace the entire `allUsers = await userService.GetAllAsync(); ... .Where(...)` block with a single call to the new `GetAvailableUsersAsync(id, search)` method (Pattern 1).
**Warning signs:** Code review — if `userService.GetAllAsync()` still appears in `GroupController.Members` after this phase, the refactor is incomplete.

### Pitfall 4: Reintroducing the removed `AddMemberViewModel` binding accidentally
**What goes wrong:** D-03 removes the standalone "select user, pick role, submit" form entirely. If `GroupMembersViewModel.AddMember` (the old single-form ViewModel) is left in place alongside the new per-row forms, stale/dead code and confusing binding prefixes (`[Bind(Prefix = "AddMember")]`) can linger.
**How to avoid:** Decide explicitly whether `AddMemberViewModel` is repurposed as the per-row Add binding target (recommended — `UserId` + `Role` fields already match what each row's form needs) or replaced by a new, differently-named ViewModel. Either way, remove the `[Bind(Prefix = "AddMember")]` attribute from the `AddMember` action signature since the per-row form (Pattern 2) posts `UserId`/`Role` as top-level fields, not nested under an `AddMember.` prefix.
**Warning signs:** `dotnet build` will not catch this (model binding failures are runtime-only) — must be caught by manual QA of the Add button, or ideally by an integration test posting to `/platform/Group/AddMember/{id}`.

## Code Examples

Verified patterns from the codebase (all file:line references confirmed via direct read in this research session):

### Existing collision-aware controller wiring to mirror exactly (minus IActiveGroupContext)
```csharp
// Source: QuestBoard.Service/Controllers/Admin/AdminController.cs:125-189 (verified)
var result = await userService.CreateOrAddToGroupAsync(model.Email, model.Name, groupId.Value, model.GroupRole);

switch (result.Outcome)
{
    case CreateOrAddToGroupOutcome.NewAccountCreated:
        // GeneratePasswordResetTokenForUserAsync + WelcomeEmailJob enqueue + RedirectWithSuccess
    case CreateOrAddToGroupOutcome.AddedToGroup:
        // GroupMembershipAddedEmailJob enqueue + RedirectWithSuccess
    case CreateOrAddToGroupOutcome.AddedToGroupStrandedAccount:
        // WelcomeEmailJob resend + RedirectWithSuccess (identical text to AddedToGroup)
    case CreateOrAddToGroupOutcome.AlreadyMember:
        return this.RedirectWithWarning(nameof(Users), $"{result.Name} is already a member of this group.");
}
```
For Phase 40, replace `groupId.Value` (from `activeGroupContext.ActiveGroupId`) with `id` (the route parameter), and replace `nameof(Users)` with `nameof(Members)` (plus the `id` route value in every redirect).

### Existing manual-join query to negate
```csharp
// Source: QuestBoard.Repository/UserRepository.cs:50-59 (verified, Phase 38 precedent)
public async Task<IList<User>> GetAllGroupMembers(int groupId, CancellationToken token = default)
{
    var entities = await DbSet
        .Where(u => DbContext.UserGroups
            .Any(ug => ug.UserId == u.Id && ug.GroupId == groupId))
        .ToListAsync(cancellationToken: token);
    return Mapper.Map<IList<User>>(entities);
}
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|---------------|--------|
| `AdminController.CreateUser` used inline `CreateAsync` + `SetGroupRoleAsync` (upsert, no collision signal) | `CreateOrAddToGroupAsync` (throw-on-collision via `IGroupService.AddMemberAsync`) | Phase 39 (2026-07-04) | Phase 40's new action is the second consumer of this method — confirms the abstraction generalizes correctly across two different controllers/routes |
| `GroupMembersViewModel.AvailableUsers` populated via in-memory `.Where(!Contains)` over `GetAllAsync()` | Will become a DB-side `GetAvailableUsersAsync(groupId, search)` query | This phase | Matches the group-scoping precedent already established in Phase 38 for other user-list queries |

**Deprecated/outdated:** The standalone single "select user + pick role + submit" `AddMember` form section in `Members.cshtml`/`.Mobile.cshtml` is fully removed per D-03 — do not preserve it alongside the new per-row forms.

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|---------------|
| A1 | New repository method should be named `GetAvailableUsers`/`GetAvailableUsersAsync` (not, e.g., `GetNonMembersAsync` or `GetUsersNotInGroupAsync`) | Architecture Patterns → Pattern 1 | Low — CONTEXT.md explicitly left this to Claude's discretion and even suggested this exact name (`GetAvailableUsersAsync(groupId, search)`) as an example; any consistent, descriptive name is acceptable and easy to rename later |
| A2 | The new create-user action should be POST-only (no separate GET view) since it's modal-triggered | Architecture Patterns → Pattern 3 | Low — matches `AddMember`/`RemoveMember`'s existing POST-only shape on the same controller; if the planner prefers a GET+POST pair for testability that is a minor, low-risk deviation |
| A3 | `AddMemberViewModel` (`UserId` + `Role`) can be repurposed as the per-row Add binding target rather than introducing a new ViewModel | Common Pitfalls → Pitfall 4 | Low — the shape already matches exactly what each row's form needs; worst case is a redundant rename task |

**If this table is empty:** N/A — see entries above. All three assumptions are low-risk, discretion-level naming/structuring choices, not behavioral or compliance claims — every functional/behavioral claim in this document was verified directly against the codebase (file reads, grep) in this research session.

## Open Questions

None blocking. All four CONTEXT.md discretion items have concrete resolutions above (Patterns 1–4). One minor structural choice remains fully at the planner's discretion with no wrong answer:

1. **Should `CreateMemberViewModel` be a brand-new class, or should `GroupMembersViewModel` gain nested `Email`/`Name`/`GroupRole` properties directly?**
   - What we know: `Views/Admin/CreateUser.cshtml` uses a dedicated `CreateUserViewModel` (Email, Name, GroupRole) as its own model; the Members page's modal needs the same three fields but lives inside the larger `GroupMembersViewModel` for the `Members` view.
   - What's unclear: Whether to nest a `CreateMemberViewModel CreateMember { get; set; }` property on `GroupMembersViewModel` (mirroring today's `AddMemberViewModel AddMember` nesting) or keep the modal's fields as top-level properties.
   - Recommendation: Nest it — mirrors the exact existing `GroupMembersViewModel.AddMember` pattern being removed/replaced, keeps `[Bind(Prefix = "CreateMember")]` symmetry with how `AddMember` currently binds, and keeps `Members.cshtml`'s model surface single-purpose.

## Environment Availability

Skipped — this phase has no external dependencies (no new packages, no new services, no new CLI tools). All work happens within the existing ASP.NET Core / EF Core / SQL Server stack already running in dev and production.

## Validation Architecture

### Test Framework
| Property | Value |
|----------|-------|
| Framework | xUnit v3 (3.2.2) + NSubstitute 5.3.0 (unit) / Microsoft.AspNetCore.Mvc.Testing 10.0.9 + EFCore.InMemory 10.0.9 (integration) [VERIFIED: `QuestBoard.UnitTests.csproj`, `QuestBoard.IntegrationTests.csproj`] |
| Config file | `QuestBoard.UnitTests/QuestBoard.UnitTests.csproj`, `QuestBoard.IntegrationTests/QuestBoard.IntegrationTests.csproj` |
| Quick run command | `dotnet test QuestBoard.UnitTests --filter FullyQualifiedName~UserServiceTests` |
| Full suite command | `dotnet test` (from repo root — runs both `QuestBoard.UnitTests` and `QuestBoard.IntegrationTests`) |

### Phase Requirements → Test Map
| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| MEMBERS-01 | Members page renders two-column layout with members left, available users right | integration | `dotnet test --filter FullyQualifiedName~GroupManagementIntegrationTests` | ✅ file exists (`GroupManagementIntegrationTests.cs`), ❌ new test cases needed — Wave 0 |
| MEMBERS-02 | `GetAvailableUsers` returns only non-members, filtered correctly by search term (case-insensitive, matches Name or Email) | unit | `dotnet test QuestBoard.UnitTests --filter FullyQualifiedName~UserRepositoryTests` or `UserServiceTests` | ❌ no `UserRepositoryTests.cs` exists today — Wave 0 |
| MEMBERS-02 | Members GET with `?search=` query param renders filtered results and echoes the term back into the search box | integration | `dotnet test --filter FullyQualifiedName~GroupManagementIntegrationTests` | ✅ file exists, ❌ new test case needed — Wave 0 |
| MEMBERS-03 | `CreateMember` POST creates/adds a user scoped to the route's `groupId`, not any session-level active group | integration | `dotnet test --filter FullyQualifiedName~GroupManagementIntegrationTests` | ✅ file exists, ❌ new test case needed — Wave 0 |
| MEMBERS-03 | `CreateMember` POST fires the identical flash messages/outcomes as `AdminController.CreateUser` (D-07) | unit + integration | same as above | ❌ new test cases needed — Wave 0 |
| D-04 | Add-to-group redirect preserves the `search` query string | integration | `dotnet test --filter FullyQualifiedName~GroupManagementIntegrationTests` | ❌ new test case needed — Wave 0 |

### Sampling Rate
- **Per task commit:** `dotnet test QuestBoard.UnitTests --filter FullyQualifiedName~UserServiceTests|FullyQualifiedName~UserRepositoryTests`
- **Per wave merge:** `dotnet test` (full unit + integration suite)
- **Phase gate:** Full suite green before `/gsd-verify-work`

### Wave 0 Gaps
- [ ] `QuestBoard.UnitTests/Repository/UserRepositoryTests.cs` (or extend existing `UserServiceTests.cs`) — covers `GetAvailableUsers`/`GetAvailableUsersAsync` search + not-in-group filtering (MEMBERS-02)
- [ ] New test cases inside existing `QuestBoard.IntegrationTests/Controllers/GroupManagementIntegrationTests.cs` — covers two-column Members render, search round-trip, per-row Add with search preserved, and the new `CreateMember` action's four outcomes (MEMBERS-01, MEMBERS-02, MEMBERS-03, D-04)
- Framework install: none — both test projects and their frameworks already exist and are wired into CI

*(No new test framework or fixture setup needed — only new test files/cases within existing infrastructure.)*

## Security Domain

### Applicable ASVS Categories

| ASVS Category | Applies | Standard Control |
|---------------|---------|-------------------|
| V2 Authentication | no | No change — SuperAdmin auth flow untouched |
| V3 Session Management | yes | Must NOT introduce `IActiveGroupContext` (session-derived group) into `GroupController` — `groupId` must come only from the route `id`, per the hard D-06 constraint and STATE.md risk flag |
| V4 Access Control | yes | `[Authorize(Policy = "SuperAdminOnly")]` already enforced class-level on `GroupController` — every new action inherits it automatically; no new authorization logic needed, but code review should confirm no new action bypasses the class-level policy with a conflicting `[AllowAnonymous]` or narrower override |
| V5 Input Validation | yes | Data annotations on `CreateMemberViewModel` (mirror `CreateUserViewModel`: `[Required][EmailAddress]` on Email, `[Required][StringLength(100)]` on Name) — ASP.NET Core model binding validates before the action body runs, same as every other form in this codebase |
| V6 Cryptography | no | No new cryptographic operations — password/token generation is entirely inside the already-verified `CreateOrAddToGroupAsync`/`GeneratePasswordResetTokenForUserAsync` methods from Phase 39, untouched by this phase |

### Known Threat Patterns for this stack

| Pattern | STRIDE | Standard Mitigation |
|---------|--------|----------------------|
| CSRF on the new per-row Add forms and the Create New User modal form | Tampering/Spoofing | `[ValidateAntiForgeryToken]` on the controller action + `asp-antiforgery="true"` / `@Html.AntiForgeryToken()` in every form — already the established, enforced pattern on every existing POST action in `GroupController` |
| Session/route `groupId` confusion (privilege-scope confusion — silently acting on the wrong group) | Tampering / Elevation of Privilege | `groupId` sourced strictly from the route parameter `id`, never from `IActiveGroupContext` — this is the explicit, pre-flagged risk for this exact phase (Phase 30, Phase 34.3 precedent incidents) |
| Crafted POST to `AddMember`/`CreateMember` for a group the SuperAdmin shouldn't manage | Elevation of Privilege | Not applicable in the same way as `AdminController`'s Admin-scoped guards (`GetGroupRoleByIdAsync` membership checks) — `GroupController` is `SuperAdminOnly`, and SuperAdmins are trusted to manage *any* group by design (this is the Platform-level group-management surface, not a group-admin surface) — no additional per-group ownership check is needed or appropriate here |
| Search input reflected back into the `<input value="@Model.SearchQuery">` search box | Tampering (XSS) | Razor's default HTML-encoding on `@Model.SearchQuery` already prevents this — no raw HTML rendering of the search term anywhere in the plan; same protection already relied on by `Views/Shop/Index.cshtml`'s identical `value="@Model.SearchQuery"` usage |

## Sources

### Primary (HIGH confidence — verified via direct file read/grep in this session)
- `QuestBoard.Service/Areas/Platform/Controllers/GroupController.cs` — current `Members`/`AddMember`/`RemoveMember` implementation
- `QuestBoard.Repository/UserRepository.cs` — `GetAllGroupMembers` manual-join precedent (lines 50-59)
- `QuestBoard.Domain/Services/UserService.cs` — `CreateOrAddToGroupAsync` full implementation (lines 155-226)
- `QuestBoard.Service/Controllers/Admin/AdminController.cs` — `CreateUser` POST full outcome-switch wiring (lines 113-190)
- `QuestBoard.Service/Views/Shop/Index.cshtml` — GET+query-string filter-row pattern (lines 201-283)
- `QuestBoard.Service/Views/ShopManagement/Index.cshtml` + `.Mobile.cshtml` — Bootstrap modal pattern, deny-modal JS delegation, mobile modal reuse (lines 1-60, 400-510)
- `QuestBoard.Service/Views/Admin/CreateUser.cshtml` — form field shape to mirror in the new modal
- `QuestBoard.Service/Extensions/ControllerExtensions.cs` — `RedirectWithSuccess`/`RedirectWithWarning` helpers
- `QuestBoard.Repository/QuestBoardContext.cs` — confirmed zero `HasQueryFilter` registrations (grep, no matches)
- `QuestBoard.Repository/Entities/UserEntity.cs` — confirmed entity shape (Name, HasKey, no group-scoping properties)
- `QuestBoard.UnitTests/Services/UserServiceTests.cs`, `QuestBoard.IntegrationTests/Controllers/GroupManagementIntegrationTests.cs` — existing test infrastructure and patterns
- `.planning/phases/39-shared-collision-aware-user-creation-email/39-{01,02,03}-SUMMARY.md` — Phase 39's actual shipped implementation details

### Secondary (MEDIUM confidence)
- `.planning/STATE.md` §"Risk Flags for Planning" — Phase 40 risk flag on `IActiveGroupContext` (cited, cross-referenced against the actual Phase 30/34.3 pattern description, not independently re-verified against those phases' own artifacts in this session)

### Tertiary (LOW confidence)
None — no web search was needed or available for this phase (no external providers configured; not required since the phase is entirely internal-codebase pattern-matching).

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH — no new dependencies; all verified via `.csproj` inspection
- Architecture: HIGH — every pattern recommended here was read directly from the existing codebase in this session, not inferred from training data
- Pitfalls: HIGH — all four pitfalls are grounded in either a documented past incident (STATE.md risk flags) or a directly observed structural fact (e.g., in-memory filtering in the current `Members` action)
- Package legitimacy: N/A — no packages installed
- Validation architecture: HIGH — test frameworks and existing test files verified directly

**Research date:** 2026-07-04
**Valid until:** No expiry concern — this research is grounded entirely in the current state of this specific repository, not external/time-sensitive documentation. Re-verify only if `GroupController.cs`, `UserRepository.cs`, or `UserService.cs` change materially before planning executes.
