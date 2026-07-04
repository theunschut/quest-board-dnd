# Stack Research

**Domain:** ASP.NET Core 10 MVC admin/user-management bugfixes (server-rendered Razor, EF Core 10, ASP.NET Core Identity)
**Researched:** 2026-07-03
**Confidence:** HIGH

## Summary: No New NuGet Packages Required

All three v6.1 features — group-scoped user list, searchable/filterable Members table, existing-email-collision auto-add-to-group — are achievable entirely with what's already installed and, in most cases, with patterns the codebase already implements elsewhere. This is a "zero new packages, zero new JS dependencies" milestone. No `<PackageReference>` changes to any `.csproj`, no new npm/CDN scripts, no schema changes (`UserGroups` junction and `UserEntity.Email`/`UserName` already exist), no new migrations.

## Recommended Stack

### Core Technologies

| Technology | Version | Purpose | Why Recommended |
|------------|---------|---------|------------------|
| Microsoft.AspNetCore.Identity (`UserManager<UserEntity>`) | 10.0.9 (already installed) | Email-collision check via `FindByEmailAsync` before `CreateAsync` | Already wrapped by `IdentityService.GetIdByEmailAsync`; reordering existing calls avoids relying on `IdentityResult.Failed` string/code-matching |
| Microsoft.EntityFrameworkCore(.SqlServer) | 10.0.9 (already installed) | Group-scoped user queries via `UserGroups` junction | `UserRepository.GetAllDungeonMasters`/`GetAllPlayers` already implement the exact `DbSet.Where(u => DbContext.UserGroups.Any(ug => ...))` pattern needed |
| Bootstrap 5 (`modern-card`, `table`, `table-striped`) | already installed | Table + form styling for the redesigned Members page | `ShopController`/Shop views already use plain Bootstrap tables + `<select>`/query-string filters at comparable scale; no upgrade needed |

### Supporting Libraries

None required.

| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| — | — | — | No supporting libraries needed for any of the three features |

### Development Tools

| Tool | Purpose | Notes |
|------|---------|-------|
| EF Core migrations (`dotnet ef migrations add`) | Not needed this milestone | All three features are query/controller/view changes only |

## Installation

```bash
# No installation needed — zero new packages for this milestone.
```

## Question-by-Question Findings

### (a) Does UserManager have a built-in collision-check pattern before CreateAsync?

**Yes — `FindByEmailAsync` is the idiomatic pre-check, and the codebase already has the wrapper method needed.**

`UserManager<TUser>.FindByEmailAsync(string email)` returns `null` if no user has that email, or the `TUser` entity if one exists. This is the standard Identity pattern confirmed against Microsoft's official Identity docs and the `dotnet/aspnetcore` source: call `FindByEmailAsync` first, branch on `null` vs. non-null, only call `CreateAsync` in the `null` branch. This avoids parsing `IdentityResult.Errors` for a `"DuplicateUserName"` code/description, which is brittle.

**Integration point — this codebase already has the lookup wired up. Reuse it, don't add a new one:**

```csharp
// IdentityService.cs:152 — already exists, already exposed through IIdentityService
public async Task<int?> GetIdByEmailAsync(string email)
{
    var entity = await userManager.FindByEmailAsync(email);
    return entity?.Id;
}
```

**Recommended flow for `AdminController.CreateUser` and the new Platform create-user entry point:**

1. Controller calls `identityService.GetIdByEmailAsync(model.Email)` *before* calling `userService.CreateAsync`.
2. If non-null (collision):
   - Skip `CreateAsync` entirely.
   - Call `groupService.AddMemberAsync(groupId, userId.Value, model.GroupRole)` — the same call `GroupController.AddMember` already uses. Catch `InvalidOperationException` the same way, for the edge case where the found user is already a member of *this* group.
   - Enqueue the new "added to group" email variant (see Stack Patterns by Variant) instead of `WelcomeEmailJob` with `isNewAccount: true`.
3. If `GetIdByEmailAsync` returns `null`, fall through to the existing `userService.CreateAsync` → `SetGroupRoleAsync` → `WelcomeEmailJob(isNewAccount: true)` path, unchanged.

This is a pure reordering of existing calls, not new capability. `AdminController` already injects `IIdentityService identityService`, `IUserService userService`, `IBackgroundJobClient jobClient` — no new constructor dependencies needed there. The new Platform create-user controller currently only injects `IGroupService groupService, IUserService userService` (see `GroupController.cs:13`) — it will need `IIdentityService` and `IBackgroundJobClient` added, following `AdminController`'s exact existing constructor shape.

**Do not** catch `DbUpdateException` for this. `GroupController.Create`/`Edit` already use `DbUpdateException` pattern-matching for *group name* uniqueness because that's DB-enforced with no cheap service-level pre-check. Email/username uniqueness has a pre-check available (`FindByEmailAsync`), so copying the `DbUpdateException` pattern here would be reaching for the wrong precedent.

**Race-condition note (informational, not a blocker at this scale):** `FindByEmailAsync` then `CreateAsync` is a check-then-act with a small TOCTOU window. At 17 users and admin-only usage (not self-service registration), this is not worth mitigating further — a genuine race would surface as an unhandled `IdentityResult.Failed`/exception, an acceptable edge case at this scale. Do not add extra defensive machinery for it.

### (b) Idiomatic lightweight searchable/filterable table for this app's scale

**Server-side query-string filtering — the codebase already has this exact pattern for the Shop item list, at comparable or larger scale. Copy it; do not introduce a client-side JS library.**

Evidence from the codebase (`ShopController`/`ShopRepository`):
- `ShopController.Index` already accepts `type`, `rarity`, `sort`, `search`, `page`, `pageSize` as query-string parameters.
- `ShopRepository` composes `IQueryable<ShopItemEntity>` with conditional `.Where(...)` clauses and a search clause: `query.Where(si => si.Name.ToLower().Contains(searchLower) || si.Description.ToLower().Contains(searchLower))`, then a `sort` switch expression.
- No JS filter/grid library exists anywhere in `wwwroot/js` (only `site.js`) or `wwwroot/lib` (no `lib` folder at all).

**Apply the identical shape to the Platform group Members "available users" list:**

- Controller: `GroupController.Members(int id, string? search = null)` — GET action takes an optional `search` query param; a plain `<form method="get">` search box submits back to the same action (full-page GET like Shop already does — no AJAX/fetch needed).
- Repository/Service: add a method (e.g. `IUserService.GetAvailableForGroupAsync(int groupId, string? search, CancellationToken)`) that composes:
  ```csharp
  var query = DbSet.Where(u => !DbContext.UserGroups.Any(ug => ug.UserId == u.Id && ug.GroupId == groupId));
  if (!string.IsNullOrWhiteSpace(search))
  {
      var s = search.ToLower();
      query = query.Where(u => u.Name.ToLower().Contains(s) || u.Email.ToLower().Contains(s));
  }
  return await query.OrderBy(u => u.Name).ToListAsync(token);
  ```
  This replaces the current `GroupController.Members` in-memory `allUsers.Where(u => !memberUserIds.Contains(u.Id))` (line 120-122, after loading `GetAllAsync()`) with a single DB-side query, and adds search in the same pass.
- View: replace the `<select asp-for="AddMember.UserId">` in `Members.cshtml` (lines 100-126) with a search `<input>` (GET form, `id` route value preserved) above a `<table>` of available users (Name, Email, per-row Add button/form posting `UserId` + a Role selector to the existing `AddMember` action). Reuse the `table table-striped table-hover` classes already used for the Members table directly above it in the same view.

**Why not a client-side JS filter (vanilla `input` + `.filter()`/`display:none` toggling)?** At 17 rows today it would work either way, but:
1. The milestone explicitly says "should scale reasonably" — server-side `Where`/`Contains` scales to thousands of rows with zero code change (add pagination later if needed), whereas a client-side filter ships the entire user list to the browser regardless of platform size, which gets worse exactly as the feature is meant to scale.
2. It would introduce a second filtering *paradigm* alongside Shop's server-query approach for the same kind of problem, adding cognitive overhead for zero benefit at this scale.
3. Zero JS to write/test/maintain; Shop already proves `[HttpGet]` re-render-on-filter works fine UX-wise for this app's admin-facing pages.

**Why not a datatable/grid library (DataTables.net, Tom Select, Choices.js, Select2)?** None are installed (verified — no `wwwroot/lib`, no CDN `<script>` tags for any grid/select-enhancement library anywhere in the codebase). Introducing one for a sub-100-row admin table is exactly what the milestone's own framing warns against — it would add a new client dependency, a new install/CDN-pinning decision, and a UI paradigm inconsistent with every other admin table in the app, to solve a problem the existing `Where`/`Contains` server pattern already solves at zero marginal cost.

### (c) EF Core patterns for scoping a query by GroupId through the UserGroups junction

**No new pattern needed — copy `UserRepository.GetAllDungeonMasters`/`GetAllPlayers` verbatim.**

The codebase already has the canonical shape for "all `UserEntity` rows that have a `UserGroups` row matching this `GroupId` (and optionally a role)":

```csharp
// UserRepository.cs:20-33 — existing, proven pattern
var entities = await DbSet
    .Where(u => DbContext.UserGroups
        .Any(ug => ug.UserId == u.Id
                && ug.GroupId == groupId.Value
                && (ug.GroupRole == (int)GroupRole.DungeonMaster
                    || ug.GroupRole == (int)GroupRole.Admin)))
    .ToListAsync(cancellationToken: token);
```

This is a correlated `EXISTS`-subquery pattern (`.Any()` inside `.Where()`) against the explicit `UserGroupEntity` junction table (not a shadow/skip-navigation many-to-many). EF Core translates this to `WHERE EXISTS (...)` SQL, which is efficient and avoids join-explosion risk. This matters more than usual in this codebase because `QuestBoardContext` already applies EF Core Global Query Filters for multi-tenancy — sticking to the same explicit-junction-`Any()` shape used elsewhere avoids interacting with those filters via a new join path.

**For `AdminController.Users()` group-scoping specifically:**

Add a new method — e.g. `IUserService.GetAllForGroupAsync(int groupId, CancellationToken)` — mirroring `GetAllDungeonMasters`/`GetAllPlayers` exactly, minus the role filter:

```csharp
public async Task<IList<User>> GetAllForGroupAsync(int groupId, CancellationToken token = default)
{
    var entities = await DbSet
        .Where(u => DbContext.UserGroups.Any(ug => ug.UserId == u.Id && ug.GroupId == groupId))
        .ToListAsync(cancellationToken: token);
    return Mapper.Map<IList<User>>(entities);
}
```

Then in `AdminController.Users()` (`AdminController.cs:24-53`), replace `await userService.GetAllAsync()` (line 29 — the unscoped bug) with `await userService.GetAllForGroupAsync(groupId.Value)`. This is strictly fewer DB round-trips than today: `Users()` currently loops **every platform user** and calls `GetGroupRoleByIdAsync` per user to classify/filter client-side; scoping the initial query first means the per-user role lookup loop only runs over group members, not the whole platform.

**One thing to actively avoid:** do not add `.Include(u => u.UserGroups)` navigation-based eager loading as an alternative. `UserEntity`/`GroupEntity` have no configured navigation properties to `UserGroupEntity` in this codebase — existing code always goes through `DbContext.UserGroups` directly. Introducing an `Include`-based join would require wiring up new navigation properties and mapping profiles, a much larger change than the existing three-line `Any()` pattern.

## Alternatives Considered

| Recommended | Alternative | When to Use Alternative |
|-------------|-------------|--------------------------|
| `FindByEmailAsync`/`GetIdByEmailAsync` pre-check before `CreateAsync` | Catch `IdentityResult.Errors` and match `Code == "DuplicateUserName"` | Only if you need to distinguish *which* uniqueness rule failed after the fact — not applicable here since `UserName == Email` in this app, so there's only one collision to detect and the pre-check is strictly cleaner |
| Server-side query-string search (`?search=`) mirroring Shop | Client-side JS array filter (`input` + `.filter()`) | Only if the list needed to filter/re-render without a page reload — not a stated requirement, and would be the first client-filter pattern in the app |
| Server-side query-string search | AJAX partial-view search (fetch + replace `<tbody>`) | Worth it later if the Members page becomes a high-frequency, no-reload workflow — premature for this milestone; adds a fetch/partial-view/JSON-fragment decision with no stated UX requirement |
| Explicit-junction `.Any()` subquery (existing pattern) | EF Core 5+ implicit skip-navigation many-to-many (`GroupEntity.Users`, `UserEntity.Groups`) | Only if the junction table had no extra columns — it does (`GroupRole`), so the "join entity with payload" shape already in place is required |

## What NOT to Use

| Avoid | Why | Use Instead |
|-------|-----|--------------|
| DataTables.net / jQuery DataTables | Pulls in jQuery + a plugin + CSS theme for a sub-100-row admin table; would be the first jQuery dependency in an app that has none today | Server-side `?search=` query param, same as Shop |
| Select2 / Choices.js / Tom Select (searchable `<select>` enhancement) | New CDN/npm dependency, new JS init pattern, solves a problem a plain filtered `<table>` + GET form already solves per the milestone's own explicit ask ("replace the dropdown with a table") | Plain HTML `<table>` with per-row Add button/form, filtered server-side |
| Catching `DbUpdateException` for the email-collision case | `IdentityService.CreateUserAsync` doesn't throw on duplicate username — `UserManager.CreateAsync` returns a *failed* `IdentityResult`, it doesn't throw. Parsing `IdentityResult.Errors[].Code` is stringly-typed and only detectable *after* attempting the write | `FindByEmailAsync`/`GetIdByEmailAsync` pre-check, detects it *before* attempting the write |
| `.Include()` navigation-based join for `UserGroups` | No navigation property currently configured between `UserEntity`/`GroupEntity` and `UserGroupEntity`; introducing one is a larger, unrelated modeling change | `DbContext.UserGroups.Any(ug => ...)` correlated subquery, consistent with `GetAllDungeonMasters`/`GetAllPlayers` |

## Stack Patterns by Variant

**Platform "create new user, scoped to group" entry point (per PROJECT.md: "mirroring the existing group-admin Create User form"):**
- Add `Create`/`CreateUser`-style GET+POST actions to `Areas/Platform/Controllers/GroupController.cs` (163 lines currently, room to extend) or a new small `Areas/Platform/Controllers/UserController.cs` if preferred for separation.
- Inject `IIdentityService identityService` and `IBackgroundJobClient jobClient` into the constructor alongside the existing `IGroupService groupService, IUserService userService` — the same four dependencies `AdminController` already has.
- Reuse `CreateUserViewModel` (`Email`, `Name`, `GroupRole`) as-is; add a `GroupId` route/hidden field since the Platform variant targets an arbitrary group (vs. `AdminController`'s implicit `activeGroupContext.ActiveGroupId`).

**New "added to group" notification email (per PROJECT.md: "distinct from the new-account welcome email"):**
- Do **not** reuse `Welcome.razor`'s `IsNewAccount` bool for this — that toggle already distinguishes "brand-new account" vs. "existing account still needs a password set," both of which still route through the SetPassword flow. This third scenario is "existing account, already has a password, just gained access to a new group" — semantically different, no `CallbackUrl`/password-set link needed.
- Add a new `AddedToGroupEmailJob.cs` mirroring `WelcomeEmailJob.cs` exactly (`IServiceScopeFactory`, `ILogger<T>`, `ExecuteAsync(toEmail, userName, groupName, ..., CancellationToken)`) and a new `AddedToGroup.razor` component mirroring `Welcome.razor`'s `_EmailLayout` wrapper and visual pattern, but without the password-set CTA — just a "you've been added to `{groupName}`" notice.
- No new rendering infrastructure needed — `IEmailRenderService.RenderAsync<T>` (used identically by `WelcomeEmailJob`) already generalizes to any `[Parameter]`-decorated Razor component.

## Version Compatibility

| Package A | Compatible With | Notes |
|-----------|-------------------|-------|
| Microsoft.AspNetCore.Identity.EntityFrameworkCore 10.0.9 | Microsoft.EntityFrameworkCore(.SqlServer) 10.0.9 | Already matched in `QuestBoard.Repository.csproj`; no version changes needed for any of the three features |
| .NET 10 / ASP.NET Core 10 MVC | Razor Components (`.razor`) for email rendering | Already proven via `HtmlRenderer` for `Welcome.razor`; the new `AddedToGroup.razor` component follows the identical rendering path |

## Sources

- [UserManager<TUser>.FindByEmailAsync(String) Method — Microsoft Learn](https://learn.microsoft.com/en-us/dotnet/api/microsoft.aspnetcore.identity.usermanager-1.findbyemailasync?view=aspnetcore-7.0) — HIGH confidence, official API docs, confirms `null`-return-on-miss pre-check pattern
- [Introduction to Identity on ASP.NET Core — Microsoft Learn](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/identity?view=aspnetcore-10.0) — HIGH confidence, official docs (ASP.NET Core 10), general Identity usage patterns
- [dotnet/aspnetcore UserManager.cs source — GitHub](https://github.com/dotnet/aspnetcore/blob/main/src/Identity/Extensions.Core/src/UserManager.cs) — HIGH confidence, primary source, confirms `FindByEmailAsync`/`CreateAsync` implementation
- [Many-to-many relationships — EF Core, Microsoft Learn](https://learn.microsoft.com/en-us/ef/core/modeling/relationships/many-to-many) — HIGH confidence, official docs, confirms explicit join-entity-with-payload pattern is correct when the junction table has extra columns (`GroupRole`)
- [Join with query filter causes nested subquery and poor query performance — dotnet/efcore#21082](https://github.com/dotnet/efcore/issues/21082) — MEDIUM confidence, community/issue-tracker discussion; informs the "avoid `.Include()` across Global Query Filters" caution, not a blocking concern at this app's scale
- Direct codebase inspection (`UserRepository.cs`, `ShopRepository.cs`, `ShopController.cs`, `IdentityService.cs`, `UserService.cs`, `GroupService.cs`, `AdminController.cs`, `GroupController.cs`, `Members.cshtml`, `WelcomeEmailJob.cs`, `Welcome.razor`, `CreateUserViewModel.cs`, `AddMemberViewModel.cs`) — HIGH confidence, primary source; all recommendations above are extensions of patterns already proven and shipped in this exact codebase

---
*Stack research for: D&D Quest Board v6.1 Bugfixes milestone (group-scoped user list, searchable Members table, email-collision auto-add-to-group)*
*Researched: 2026-07-03*
