<!-- refreshed: 2026-07-03 -->
# Architecture Research — v6.1 Bugfixes (Admin User-Management Gaps)

**Domain:** Integration architecture for 3 admin/user-management fixes into an existing ASP.NET Core 10 MVC + EF Core clean-architecture app
**Researched:** 2026-07-03
**Confidence:** HIGH (all findings grounded in direct reads of the actual interfaces/controllers in this repo, not external research)

## Summary

All three features are additive extensions of existing seams — no new architectural layer, dispatcher pattern, or cross-cutting abstraction is required. The codebase already contains most of the primitives needed:

- `IGroupService.GetMembersAsync(groupId)` already returns group-scoped `UserGroup` rows with `User` loaded — this is the join Feature 1 needs, just not yet used by `AdminController.Users()`.
- `IIdentityService.GetIdByEmailAsync(email)` already exists (used by `AdminController.CreateUser` today, right after creation, not before). The "does this email already exist" check needed for Feature 3 is a **pre-check reuse of an existing method**, not a new one.
- `Platform/GroupController` already has a full "existing user → add to group" flow (`Members` GET + `AddMember` POST, using `AddMemberViewModel`) that Feature 2's "create new user in this group" entry point should sit beside, reusing `groupService.AddMemberAsync` for the collision branch.
- The email-job pattern has **no dispatcher interface for user-management emails** (unlike quest emails, which go through `IQuestEmailDispatcher`) — `AdminController` enqueues `WelcomeEmailJob`/`ChangeEmailConfirmationJob` directly via constructor-injected `IBackgroundJobClient`. A new `GroupMembershipAddedEmailJob` (or similarly named) should follow this exact same direct-enqueue pattern, not introduce a dispatcher abstraction.

The one real architectural decision is **where the shared "existing-email-collision → add to group + notify" logic lives**. It must go in `UserService` (Domain layer), not in either controller, because both `AdminController` (group-scoped, Service layer, group-admin) and `Platform/GroupController` (SuperAdmin, Service layer, route-scoped) need to call it identically, and Domain is the only layer both controllers already depend on without depending on each other.

## Feature 1 — Scope `AdminController.Users()` to the Active Group

### Current state (the bug)

`AdminController.Users()` (`QuestBoard.Service/Controllers/Admin/AdminController.cs:24-53`) calls `userService.GetAllAsync()` — inherited from `IBaseService<User>` — which returns **every user on the platform**, then does a per-user `GetGroupRoleByIdAsync` N+1 lookup to compute each row's role in the active group. Users who are not members of the active group still appear in the list (with all three role flags `false`), leaking cross-tenant user data into a group admin's view.

### Integration point

`IGroupService.GetMembersAsync(int groupId)` (`QuestBoard.Domain/Interfaces/IGroupService.cs:37`) already does the correct join: `UserGroups.Include(ug => ug.User).Where(ug => ug.GroupId == groupId)`, returning `IList<UserGroup>` with `GroupRole` and `User` populated per row. This is **already group-scoped and already has the role on it** — no per-user N+1 lookup needed at all.

### New vs Modified

| Component | New or Modified | What |
|---|---|---|
| `AdminController.Users()` | **Modified** | Replace `userService.GetAllAsync()` + `GetGroupRoleByIdAsync` loop with `groupService.GetMembersAsync(groupId.Value)`, project `UserManagementViewModel` from each `UserGroup.User` + `UserGroup.GroupRole` directly |
| `AdminController` constructor | **Modified** | Add `IGroupService groupService` to the constructor parameter list (not currently injected there) |
| `IUserService` / `UserService` | **No change needed** | `GetGroupRoleByIdAsync` becomes dead code for this call site but should stay — still used by other flows (verify via `FindReferences`/`FindCallers` before removing anything) |
| `UserManagementViewModel` | **No change needed** | Already holds `User` + 3 bool role flags; `UserGroup.User` supplies the same shape `userService.GetAllAsync()` did |

### Why not add `IUserService.GetByGroupIdAsync`

The milestone context suggested "a new `UserService` method (e.g. `GetByGroupIdAsync`) that joins through the UserGroups junction." That method would duplicate `IGroupService.GetMembersAsync` — same join, same table, same shape (`UserGroup` already carries `User`). Adding a second method that returns the same data via a different path is exactly the kind of quiet duplication this codebase's Key Decisions log flags as a recurring source of drift (see the 3x-duplicated `GetActiveBoardTypeAsync` noted in `PROJECT.md`'s Known Issues). Reuse `IGroupService.GetMembersAsync` instead — it is already `GroupService`-side group-scoped, already returns `GroupRole`, and is already exercised by `Platform/GroupController.Members`, so both admin surfaces read group membership through the same seam.

**Risk:** `GetMembersAsync` is currently only called from `Platform/GroupController` (SuperAdmin, no `[Authorize(Policy = "AdminOnly")]` scoping concerns there). Calling it from `AdminController` (group-admin surface) is safe because the `groupId` passed in is always `activeGroupContext.ActiveGroupId.Value` — same trust boundary as the code it replaces.

## Feature 2 — Platform `GroupController.Members`: Searchable Table + Group-Scoped "Create New User"

### Current state

`Members(int id)` (`Areas/Platform/Controllers/GroupController.cs:114-130`) already computes `availableUsers` (platform users minus current members) and renders them in `GroupMembersViewModel.AvailableUsers` for a `<select>` dropdown, bound via `AddMemberViewModel` (`[Bind(Prefix = "AddMember")]`) to the `AddMember` POST action. This is the existing-user path and does not need re-architecting — only the view template's dropdown-to-searchable-table change, which is presentation-layer only (no controller/service change required for that half).

The new requirement is a **second entry point**: "create new user in this group," mirroring `AdminController.CreateUser` but reachable when there is no `IActiveGroupContext.ActiveGroupId` in session (SuperAdmin is deliberately unscoped — see `ARCHITECTURE.md`'s "SuperAdmin group scoping" architectural constraint). The target `groupId` must come from the route/model (`id`), never from session.

### Integration point

`Platform/GroupController` already takes `IUserService userService` in its constructor (used today for `GetAllAsync` in `Members` and `GetByIdAsync` in `AddMember`). It does **not** currently take `IIdentityService` or `IBackgroundJobClient`/Hangfire — both need to be added.

### New vs Modified

| Component | New or Modified | What |
|---|---|---|
| `Platform/GroupController` constructor | **Modified** | Add `IIdentityService identityService`, `IBackgroundJobClient jobClient` (mirrors `AdminController`'s constructor shape) |
| `Platform/GroupController.CreateMember(int id)` GET | **New action** | Renders a create-user form scoped to route `id`, analogous to `AdminController.CreateUser()` GET |
| `Platform/GroupController.CreateMember(int id, CreateGroupMemberViewModel model)` POST | **New action** | Calls the shared collision-aware creation method (see Feature 3) with `groupId = id` from the route — never `activeGroupContext.ActiveGroupId`, which is null for SuperAdmin |
| `CreateGroupMemberViewModel` (Platform ViewModels namespace) | **New** | Same shape as `CreateUserViewModel` (`Email`, `Name`, `GroupRole`) — reuse the type directly instead of duplicating it if the two views can share a partial/model (see Build Order note below) |
| `GroupMembersViewModel` | **Modified (optional)** | Add a `CreateMember` sub-viewmodel property if the "create new user" form renders inline on the same Members page rather than a separate view, mirroring how `AddMember` is already nested |
| Members.cshtml view | **Modified** | Replace `<select>` dropdown with searchable/filterable table (presentation only); add "create new user" entry point button/link to the new `CreateMember` action |

### Why route `id`, not `IActiveGroupContext`

`IActiveGroupContext.ActiveGroupId` (`QuestBoard.Domain/Interfaces/IActiveGroupContext.cs`) is session-backed via `ActiveGroupContextService`, and is `null` by design for SuperAdmin (documented anti-pattern: "SuperAdmin group scoping... intentionally has `ActiveGroupId = null`"). The new Platform create-user action must take `groupId` as an explicit method parameter sourced from the route, exactly as `Members`/`AddMember`/`RemoveMember` already do (`int id` route parameter throughout `Platform/GroupController`). This also means the shared collision-handling service method (Feature 3) must accept `groupId` as an explicit parameter — it cannot read `IActiveGroupContext` internally, or it would silently no-op for SuperAdmin callers.

## Feature 3 — Existing-Email Collision Handling in Create User Flows

### Current state (the bug)

`IdentityService.CreateUserAsync` (`QuestBoard.Repository/IdentityService.cs:37-55`) calls `userManager.CreateAsync(entity)` with no pre-check. ASP.NET Core Identity's `UserManager` enforces unique `UserName` (`= email` in this app's `CreateUserAsync`), so a duplicate email produces an `IdentityResult` failure (`DuplicateUserName` error) that `AdminController.CreateUser` currently surfaces as raw `ModelState` errors — a confusing/hard-fail UX instead of "add this existing user to your group."

### Integration point — the collision check already exists, half-built

`IIdentityService.GetIdByEmailAsync(string email)` (`QuestBoard.Domain/Interfaces/IIdentityService.cs:101`, implemented via `userManager.FindByEmailAsync` in `IdentityService.cs:152-156`) is **already in the codebase** and already called by `AdminController.CreateUser` — but only *after* a successful create, to fetch the just-created user's ID (`AdminController.cs:117`). The fix reorders this: call `GetIdByEmailAsync` (or a new `IUserService`-level wrapper) **before** attempting creation, and branch.

### Where the shared logic must live: `UserService` (Domain layer), not either controller

Both `AdminController.CreateUser` (group-scoped, reads `groupId` from `IActiveGroupContext`) and the new `Platform/GroupController.CreateMember` (reads `groupId` from the route) need identical branching logic:

1. Look up `identityService.GetIdByEmailAsync(model.Email)`.
2. If **no existing user** → current path: `userService.CreateAsync(...)` → `SetGroupRoleAsync` → generate reset token → enqueue `WelcomeEmailJob` (new-account variant).
3. If **existing user found** → new path: `userService.SetGroupRoleAsync(existingUserId, groupId, model.GroupRole)` (reuses the exact same method `PromoteToAdmin`/`AddMember` already use to upsert a `UserGroupEntity` row — it is idempotent-safe, unlike `GroupService.AddMemberAsync` which throws `InvalidOperationException` on an existing row) → enqueue a new "added to group" notification email (distinct from `WelcomeEmailJob`).

This branching logic is business logic with no HTTP/view concerns — it belongs in `UserService` per this codebase's established pattern ("Business logic lives in services, not controllers" — validated requirement from v1.x, `PROJECT.md` line 45). Concretely:

```csharp
// QuestBoard.Domain/Interfaces/IUserService.cs — new method
/// <summary>
/// Creates a new user and adds them to the group, or — if a user with this email
/// already exists — adds the existing user to the group instead. Returns which
/// branch was taken so the caller can pick the correct notification email and
/// success message.
/// </summary>
Task<CreateOrAddToGroupResult> CreateOrAddToGroupAsync(string email, string name, int groupId, GroupRole role);
```

Both controllers then reduce to: call `userService.CreateOrAddToGroupAsync(...)`, branch only on the **result** to pick which Hangfire job to enqueue (email rendering/URL-building stays in the Service layer, since `Url.Action(...)` needs `IUrlHelper`, which is a controller/MVC concern `UserService` in Domain cannot reach — this is why the email dispatch itself stays in the controllers, only the create-or-add decision moves to Domain).

### New vs Modified — Feature 3

| Component | New or Modified | What |
|---|---|---|
| `IUserService.CreateOrAddToGroupAsync(...)` | **New method** on existing interface | Returns an enum/result distinguishing "created new" vs "added existing", plus the resolved `userId` |
| `UserService.CreateOrAddToGroupAsync` | **New implementation** | Calls `identityService.GetIdByEmailAsync` first; branches to `CreateAsync`+`SetGroupRoleAsync` or straight to `SetGroupRoleAsync` |
| `CreateOrAddToGroupResult` (or a simple enum `UserCreationOutcome { Created, AddedExisting }` + userId out param) | **New small model** | Lives in `QuestBoard.Domain/Models` or `Enums` |
| `AdminController.CreateUser` POST | **Modified** | Replace direct `userService.CreateAsync` + `SetGroupRoleAsync` + welcome-email block with a call to `CreateOrAddToGroupAsync`, then branch only on which email job to enqueue |
| `Platform/GroupController.CreateMember` POST (new, Feature 2) | **New, but calls the same method** | Identical branch, `groupId` from route instead of `activeGroupContext` |
| New email Razor component, e.g. `QuestBoard.Service/Components/Emails/AddedToGroup.razor` | **New** | Mirrors `Welcome.razor`/`ChangeEmailConfirm.razor` structure (`_EmailLayout` wrapper, `[Parameter, EditorRequired]` fields: `UserName`, `GroupName`, `AppUrl`, and probably a link straight into the app rather than a callback token, since the user already has a password) |
| New Hangfire job, e.g. `QuestBoard.Service/Jobs/GroupMembershipAddedEmailJob.cs` | **New** | Copy the exact shape of `ChangeEmailConfirmationJob.cs` (`IServiceScopeFactory` + `ILogger<T>` constructor, `ExecuteAsync` resolves `IEmailRenderService`/`IEmailService`/`IOptions<EmailSettings>` from a fresh scope) — **do not** introduce an `IGroupMembershipEmailDispatcher` interface; no other user-management email in this codebase uses a dispatcher abstraction, only quest-related emails do (`IQuestEmailDispatcher`) because those need the swap-for-`NullQuestEmailDispatcher`-in-tests treatment that `AdminController`'s jobs never needed (`AdminController` already takes `IBackgroundJobClient` directly per the Known Issues note in `PROJECT.md`: "AdminController takes IBackgroundJobClient directly as constructor arg (fixed 2026-06-28)") |
| `GroupService.AddMemberAsync` | **Not touched** | Still used for the *existing-user manual add* path in `Platform/GroupController.AddMember` (throws on duplicate — correct there, since the whole point of that screen is picking a user who is definitely not yet a member). `UserService.CreateOrAddToGroupAsync`'s existing-user branch should call `userService.SetGroupRoleAsync` instead (upsert semantics), because in the collision case the target user may already belong to *other* groups but this is their first time being added to *this* group — using upsert avoids a spurious exception path for what is actually the common/expected case here |

### Do not modify `IIdentityService.CreateUserAsync` or `GetIdByEmailAsync` signatures

Both already have the right shape. No Repository-layer change is needed for this feature — everything new is additive at the Domain (`IUserService`) and Service (controllers, new job, new email component, new ViewModel fields) layers, consistent with the "EF packages belong only in Repository" / "Domain must not depend on Repository entities" constraints already validated in this codebase.

## Build Order

Recommended sequence, minimizing risk and avoiding rework:

1. **`UserService.CreateOrAddToGroupAsync` (Feature 3's core, built once).** This is the shared seam both controllers will call. Build and unit-test it in isolation first — it has no HTTP/view dependencies, so it's the cheapest place to get the branching logic (existing vs new, `SetGroupRoleAsync` upsert semantics) correct before any controller touches it. Building this first also means Feature 2's new "create user" action is written against a finished interface, not a moving target.

2. **New email component + Hangfire job (`AddedToGroup.razor` + `GroupMembershipAddedEmailJob`).** Independent of both controllers; can be built and manually verified (render + send) in parallel with step 1, or immediately after. Model directly off `ChangeEmailConfirmationJob`/`ChangeEmailConfirm.razor` since that's the simplest existing job (no token/callback-URL complexity like `WelcomeEmailJob`'s SetPassword link — the "added to group" notification likely just needs a plain "go to the app" link, not a token flow, since the recipient already has a password by definition of being an *existing* user).

3. **Feature 1 (`AdminController.Users()` rescoping).** Fully independent of Features 2/3 — do this any time, ideally first or in parallel with step 1, since it's a pure read-path swap (`GetAllAsync` + N+1 → `groupService.GetMembersAsync`) with no email/creation logic involved and the smallest blast radius. Requires adding `IGroupService` to `AdminController`'s constructor.

4. **`AdminController.CreateUser` POST — rewire onto `CreateOrAddToGroupAsync`.** Now that step 1 exists, update the existing group-admin create-user flow to call the shared method and branch on the result to pick `WelcomeEmailJob` vs the new `GroupMembershipAddedEmailJob`. This is a **modification of a working flow**, so it should ship and be manually verified before Feature 2 reuses the same seam — regressions here would otherwise surface twice (once per controller).

5. **Feature 2 — `Platform/GroupController.CreateMember` (new action) + Members view searchable-table change.** Built last because it depends on: the shared `CreateOrAddToGroupAsync` method (step 1) being proven correct via step 4's real-world usage, and the new email job (step 2) already existing. The searchable/filterable table replacement for the existing-user dropdown is presentation-only and can be built independently/in parallel at any point, since it doesn't touch the create-new-user logic at all — but sequencing it last alongside the new "create user" entry point avoids two separate view-file churns on `Members.cshtml`.

**Why this order avoids duplicate logic:** Steps 1–2 build the shared pieces once, with no controller depending on them yet. Step 3 is a decoy-free warm-up (touches neither shared piece). Step 4 proves the shared seam against the *existing*, already-tested group-admin flow — the lowest-risk place to catch a bug in the branching logic, since `AdminController.CreateUser` already has coverage and a known-good baseline behavior to diff against. Only after that proof does step 5 add the *second* caller (Platform), which by construction cannot duplicate the collision logic because it's calling the same `IUserService` method, not reimplementing it.

## Anti-Patterns to Avoid

### Anti-Pattern: Duplicating the collision check per controller

**What people might do:** Write `if (existingUserId.HasValue) { ... } else { ... }` inline in both `AdminController.CreateUser` and `Platform/GroupController.CreateMember`.

**Why it's wrong:** Two independent implementations of "is this an upsert or a create" will drift the moment one gets a bugfix the other doesn't — the same failure mode this codebase's own history warns about (`PROJECT.md`'s Known Issues explicitly calls out `GetActiveBoardTypeAsync` being implemented 3x as tech debt requiring a dedicated audit to confirm it was still safe).

**Do this instead:** One `IUserService.CreateOrAddToGroupAsync` method; both controllers call it and only branch on the *result* to choose which email job to enqueue (since email dispatch requires `Url.Action`/`IUrlHelper`, a Service-layer/controller concern that cannot move into Domain).

### Anti-Pattern: Introducing an `IUserEmailDispatcher` abstraction for the new email

**What people might do:** Copy the `IQuestEmailDispatcher`/`HangfireQuestEmailDispatcher` pattern "for consistency."

**Why it's wrong:** Every existing user-management email (`WelcomeEmailJob`, `ChangeEmailConfirmationJob`, `ForgotPasswordEmailJob`) is enqueued directly from controllers via constructor-injected `IBackgroundJobClient.Enqueue<T>(...)` — no dispatcher interface exists for this category, and `AdminController`'s Known Issues note explicitly documents that it deliberately takes `IBackgroundJobClient` directly (fixed away from a dispatcher-style abstraction on 2026-06-28). Introducing a dispatcher only for the new email breaks consistency with its siblings for no benefit — nothing here needs a `NullObject` test double swap the way quest-finalization emails do.

**Do this instead:** `jobClient.Enqueue<GroupMembershipAddedEmailJob>(j => j.ExecuteAsync(...))`, identical to how `ChangeEmailConfirmationJob` is enqueued today.

### Anti-Pattern: Reading `IActiveGroupContext.ActiveGroupId` inside the new Platform create-user action or inside `CreateOrAddToGroupAsync`

**What people might do:** Have `UserService.CreateOrAddToGroupAsync` internally resolve `activeGroupContext.ActiveGroupId` instead of taking `groupId` as a parameter, since that's the pattern `AdminController`'s existing actions use.

**Why it's wrong:** `ActiveGroupId` is `null` for SuperAdmin by design (documented architectural constraint). If the shared method reads it internally, the Platform caller — which has a concrete target group from the route — would silently fail or throw for every SuperAdmin invocation, since SuperAdmin's session never has an active group.

**Do this instead:** `groupId` is always an explicit method parameter on the shared service method. `AdminController` passes `activeGroupContext.ActiveGroupId.Value` (after its existing null-guard); `Platform/GroupController.CreateMember` passes the route's `id` directly.

## Integration Points Summary Table

| Existing Seam | Used By (today) | New Caller | Change Needed |
|---|---|---|---|
| `IGroupService.GetMembersAsync(groupId)` | `Platform/GroupController.Members` | `AdminController.Users()` | None — reuse as-is |
| `IIdentityService.GetIdByEmailAsync(email)` | `AdminController.CreateUser` (post-create, to fetch new ID) | `UserService.CreateOrAddToGroupAsync` (pre-create, as the collision check) | None — reuse as-is, just called earlier/from a new call site |
| `IUserService.SetGroupRoleAsync(userId, groupId, role)` | `AdminController.PromoteToAdmin/DemoteFromAdmin/PromoteToDM/DemoteToPlayer/CreateUser` | `UserService.CreateOrAddToGroupAsync`'s existing-user branch | None — reuse as-is (upsert semantics already correct for this case) |
| `IBackgroundJobClient.Enqueue<T>` | `AdminController` (constructor-injected) | `Platform/GroupController` (needs adding to constructor) | Add constructor param to `Platform/GroupController` |
| `IIdentityService` | `AdminController` (constructor-injected) | `Platform/GroupController` (needs adding to constructor) | Add constructor param to `Platform/GroupController` |
| N/A | N/A | `IUserService.CreateOrAddToGroupAsync` | **New method** — the one genuinely new piece of business logic |
| N/A | N/A | `GroupMembershipAddedEmailJob` + `AddedToGroup.razor` | **New job + new email template**, modeled directly on `ChangeEmailConfirmationJob`/`ChangeEmailConfirm.razor` |

## Sources

- Direct codebase reads (this repository, 2026-07-03): `QuestBoard.Domain/Interfaces/IUserService.cs`, `IIdentityService.cs`, `IGroupService.cs`, `IActiveGroupContext.cs`, `IBaseService.cs`, `IUserRepository.cs`; `QuestBoard.Domain/Services/UserService.cs`, `GroupService.cs`; `QuestBoard.Repository/IdentityService.cs`, `UserRepository.cs`, `GroupRepository.cs`; `QuestBoard.Service/Controllers/Admin/AdminController.cs`; `QuestBoard.Service/Areas/Platform/Controllers/GroupController.cs`; `QuestBoard.Service/Jobs/WelcomeEmailJob.cs`, `ChangeEmailConfirmationJob.cs`; `QuestBoard.Service/Components/Emails/Welcome.razor`; `QuestBoard.Service/ViewModels/AdminViewModels/CreateUserViewModel.cs`, `QuestBoard.Service/ViewModels/PlatformViewModels/GroupMembersViewModel.cs`, `AddMemberViewModel.cs`; `QuestBoard.Domain/Models/UserGroup.cs`.
- `.planning/PROJECT.md` (v6.1 milestone scope, Key Decisions log, Known Issues)
- `.planning/codebase/ARCHITECTURE.md` (layer structure, dependency direction, anti-patterns, SuperAdmin scoping constraint)

---
*Architecture research for: D&D Quest Board v6.1 Bugfixes milestone*
*Researched: 2026-07-03*
