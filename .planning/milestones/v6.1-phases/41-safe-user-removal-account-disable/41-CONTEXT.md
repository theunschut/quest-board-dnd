# Phase 41: Safe User Removal & Account Disable - Context

**Gathered:** 2026-07-04
**Status:** Ready for planning

<domain>
## Phase Boundary

Two related but separable safety fixes to user-management flows:

1. **Group removal (SAFE-01):** `AdminController.DeleteUser` currently hard-deletes the `UserEntity` row via `userService.RemoveAsync(user)` — cascading their membership out of *every* group (not just the active one) and cascading their `Character` rows, while throwing an unhandled `DbUpdateException` for any user with quest/shop/transaction/trade/reminder history (those FKs are `DeleteBehavior.NoAction`). This phase repurposes it to remove the user from the active group only, reusing the exact `IGroupService.RemoveMemberAsync(groupId, userId)` primitive already built for Phase 40's Platform Members page.
2. **Account disable/enable (SAFE-02, SAFE-03, SAFE-04):** SuperAdmin gets a real way to deactivate a problem account without deleting any data, reusing ASP.NET Core Identity's existing `LockoutEnd` mechanism (not a new "disabled" column). This requires a new SuperAdmin-only surface to trigger it, a way to distinguish a disabled account from a real 15-minute failed-attempt lockout in the login error message, and a decision on whether disabling kicks out an already-active session.

This phase does not touch `AdminController.Users()`'s read-path scoping (Phase 38) or the Platform Members page's add/search flows (Phase 40) beyond reusing their established patterns.

</domain>

<decisions>
## Implementation Decisions

### Group removal only, not account deletion (SAFE-01)
- **D-01:** `AdminController.DeleteUser` calls `IGroupService.RemoveMemberAsync(groupId, user.Id)` instead of `userService.RemoveAsync(user)` — the identical primitive `Areas/Platform/Controllers/GroupController.RemoveMember` already uses. No new repository/service method needed. This removes only the `UserGroupEntity` row for the active group; the account, its other group memberships, characters, quest history, shop items, transactions, and reminder log rows are all untouched (avoiding every `NoAction`-FK `DbUpdateException` the old hard-delete path could throw).
- **D-02:** The "Delete" button on `Views/Admin/Users.cshtml` + `Users.Mobile.cshtml` is renamed to **"Remove from Group"** — button label and the `deleteUser()` confirm-dialog copy both updated to accurately describe what happens now (e.g., "Are you sure you want to remove this user from the group? They will keep their account and any other group memberships."). Not just a copy fix under the old "Delete" label — user explicitly chose the rename.
- **D-03:** No extra warning or special-case handling when a removal leaves a user with zero group memberships — `GroupSessionMiddleware` (Phase 31) already redirects a groupless authenticated user to `/groups/pick` gracefully. Confirmed as a non-issue, not a gap to close.

### New Platform Users page for disable/enable (SAFE-02, SAFE-03)
- **D-04:** Disable/enable does **not** live on the existing (group-scoped) `AdminController.Users()` page or on the Platform Members page — both of those are about group *membership*, and disable is an account-wide concept, not a group concept. Instead: a new `Areas/Platform/Controllers/UsersController.cs` (`[Area("Platform")]`, `[Authorize(Policy = "SuperAdminOnly")]`), with an `Index` action listing **every** platform user cross-group (Name, Email, confirmed status, Active/Disabled badge) and a Disable/Enable button per row. Reuses the existing `IUserService.GetAllAsync()` (the same method Phase 38 removed from the group-scoped `AdminController.Users()` for leaking cross-tenant data — here, in a genuinely cross-group SuperAdmin-only view, showing every user is correct, not a leak).
- **D-05:** A matching `Index.Mobile.cshtml` companion view ships in the same phase — consistent with every other Platform-area view having mobile support (`Areas/Platform/Views/Shared/_Layout.Platform.Mobile.cshtml` already exists).
- **D-06:** Entry point: a link/button added to `Areas/Platform/Views/Group/Index.cshtml`'s header bar, next to the existing "Create Group" button — mirrors Phase 40's header-bar-buttons-in-title-row pattern rather than adding a new global nav dropdown item.
- **D-07:** A SuperAdmin **cannot disable their own account** — the Disable control is hidden/disabled when the target `userId` matches the current user's id. Prevents accidentally locking yourself out with no one else able to re-enable you.
- **D-08:** A SuperAdmin **can** disable another SuperAdmin's account — no special-casing. SuperAdmin is treated as a trusted, system-wide role; the tool shouldn't block one SuperAdmin acting on another (e.g. a compromised or departing admin).
- **D-09 (important — deliberate, not a gap):** The Disable action sets **only** `LockoutEnd` (via `UserManager.SetLockoutEndDateAsync(user, DateTimeOffset.MaxValue)`) — it must **not** also force `LockoutEnabled = true`. All users currently have `LockoutEnabled = true` (backfilled by the `EnableLockoutForExistingUsers` migration; auto-set on every new account via `options.Lockout.AllowedForNewUsers = true`), and nothing in the app exposes a way to change it. The user will deliberately flip `LockoutEnabled = false` directly in the database for specific trusted accounts (including possibly their own) as an out-of-band, DB-only escape hatch — a row with `LockoutEnabled = false` makes `UserManager.IsLockedOutAsync` always return `false` regardless of `LockoutEnd`, so that account can never be disabled by this in-app feature, even by accident. **Do not add any code that sets `LockoutEnabled`.**
- **D-10:** Disabling also calls `UserManager.UpdateSecurityStampAsync(user)` alongside setting `LockoutEnd` — this invalidates any already-issued auth cookie for that user (not just future login attempts), because ASP.NET Core Identity's `SecurityStampValidator` only checks for a stamp mismatch, never lockout status directly.
- **D-11:** The app-wide `SecurityStampValidatorOptions.ValidationInterval` (Identity default: 30 minutes) is shortened to **~5 minutes**, configured in `Program.cs`. Negligible DB load at this app's scale (17 members); makes a disabled "problem account" get kicked out of an active session much sooner than the 30-minute default would. This is an app-wide config change (affects all security-stamp re-validation timing, not just disable).
- **D-12 (Claude's discretion, low-risk default):** Re-enable (SAFE-03) clears `LockoutEnd` back to `null` via `SetLockoutEndDateAsync(user, null)` — no `SecurityStamp` bump needed on re-enable since a disabled/locked-out user has no active session to invalidate (they were already blocked from signing in).

### Login messaging — disabled vs. temporary lockout (SAFE-04)
- **D-13:** `AccountController.Login`'s `result.IsLockedOut` branch is updated to look up the target user's actual `LockoutEnd` value and compare it **exactly** against `DateTimeOffset.MaxValue`. If it matches → show `"This account has been disabled. Contact an administrator."`. Otherwise → keep the existing `"Account locked due to too many failed attempts. Try again in 15 minutes."` copy unchanged. User explicitly chose an exact `MaxValue` match over a fuzzy "more than N days in the future" threshold check, since `MaxValue` is the literal sentinel this feature sets and a real failed-attempt lockout is always exactly 15 minutes (`options.Lockout.DefaultLockoutTimeSpan`, `Program.cs`).

### Claude's Discretion
- Exact new `IIdentityService`/`IdentityService` method name(s) and signatures for disable, enable, and looking up a user's current `LockoutEnd` (e.g. `DisableUserAsync(int userId)`, `EnableUserAsync(int userId)`, `GetLockoutEndAsync(int userId)`) — not discussed, follow the existing `UserManager`-wrapping style already used throughout `IdentityService.cs`.
- Exact button styling/icons for Disable/Enable and the renamed "Remove from Group" button — follow `CLAUDE.md` UI/UX Design Guidelines (modern-card pattern, filled colored buttons, FontAwesome icons with `me-2`).
- Whether the new Platform Users page needs search/filter — Phase 40's Members page has one, but this is a fresh cross-group list at a small scale (17 users); add one only if it falls out naturally, not required by any locked decision here.
- Exact route/action names for the new `UsersController` (e.g. `Disable`/`Enable` vs. a single `ToggleLockout`).

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Requirements & roadmap
- `.planning/REQUIREMENTS.md` — SAFE-01, SAFE-02, SAFE-03, SAFE-04 (the four requirements this phase satisfies)
- `.planning/ROADMAP.md` §"Phase 41: Safe User Removal & Account Disable" — goal and the five locked success criteria
- `.planning/PROJECT.md` §"Active" — milestone-level framing of this requirement; §"Known issues / tech debt" — Phase 37's mobile-nav-gap precedent, relevant context for D-05's mobile-parity decision

### Prior-phase conventions this phase must match
- `.planning/phases/40-platform-members-page-redesign/40-CONTEXT.md` — established Platform-area conventions (`SuperAdminOnly` policy, header-bar-buttons-in-title-row pattern used for D-06, modal/redirect conventions) the new `UsersController` should follow
- `.planning/phases/38-group-scoped-user-list/38-CONTEXT.md` — established manual-join/group-scoping conventions and the "silently redirect, no error" guard pattern for crafted requests

### UI/UX conventions
- `CLAUDE.md` §"UI/UX Design Guidelines" — modern-card pattern (`modern-card`, `modern-card-header`, `modern-card-body`), filled colored buttons, FontAwesome icons with `me-2` spacing, `d-flex justify-content-between` button layout
- `.planning/codebase/CONVENTIONS.md` §"UI/UX Design" — same conventions, restated with codebase-verified examples

No other external specs/ADRs apply — requirements fully captured in Decisions above.

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `QuestBoard.Domain/Interfaces/IGroupService.cs` / `IGroupRepository.cs` — `RemoveMemberAsync(groupId, userId)` already implemented and already used by `Areas/Platform/Controllers/GroupController.cs:269` (`RemoveMember` action) — directly reusable for D-01, no new repository code needed for SAFE-01.
- `QuestBoard.Service/Controllers/Admin/AdminController.cs:339-358` (`DeleteUser`) — the action to fix; currently calls `userService.RemoveAsync(user)`, which hard-deletes via `BaseRepository.RemoveAsync` (`DbSet.Remove` + `SaveChangesAsync`).
- `QuestBoard.Service/Views/Admin/Users.cshtml:169-172, 190-206` — the Delete button + `deleteUser()` JS confirm dialog to update for D-02.
- `Areas/Platform/Controllers/GroupController.cs` (`Index`, header-bar layout in `Views/Group/Index.cshtml`) — existing `SuperAdminOnly` Platform controller/view pattern to mirror for the new `UsersController` and its entry point (D-06).
- `QuestBoard.Service/Areas/Platform/Views/Shared/_Layout.Platform.cshtml` + `_Layout.Platform.Mobile.cshtml` — existing Platform-area layout/mobile split the new views render under.
- `QuestBoard.Repository/IdentityService.cs` — existing `IIdentityService` implementation style (thin `UserManager`-wrapping methods) — new Disable/Enable/lockout-lookup methods should follow the same pattern.
- `QuestBoard.Service/Controllers/Admin/AccountController.cs:130-140` (`Login` POST, `result.IsLockedOut` branch) — to update for D-13.
- `QuestBoard.Service/Program.cs:57-61` — existing Identity lockout options (`MaxFailedAccessAttempts = 5`, `DefaultLockoutTimeSpan = 15 min`, `AllowedForNewUsers = true`) — confirms every account already has `LockoutEnabled = true` by default.
- `QuestBoard.Repository/Migrations/20260420142117_EnableLockoutForExistingUsers.cs` — precedent migration confirming `LockoutEnabled = true` was already backfilled for all pre-existing accounts; no new migration needed for D-09.

### Established Patterns
- `QuestBoard.Repository/Entities/QuestBoardContext.cs` `OnModelCreating` — `UserGroupEntity → UserEntity` is `Cascade` (removing a `UserEntity` deletes ALL its group memberships); `UserEntity` is referenced with `DeleteBehavior.NoAction` from `QuestEntity.DungeonMasterId`, `ShopItemEntity.CreatedByDmId`, `UserTransactionEntity.UserId`, `TradeItemEntity.OfferedByPlayerId`, and `ReminderLogEntity.PlayerId` (each throws `DbUpdateException` on a hard `UserEntity` delete) — this is exactly why D-01 switches to group-scoped removal instead of `userService.RemoveAsync`.
- ASP.NET Core Identity internals: `UserManager.IsLockedOutAsync` short-circuits to `false` whenever `LockoutEnabled` is `false`, regardless of `LockoutEnd` — the mechanism behind the deliberate DB-only immunity escape hatch (D-09). Nothing in this codebase currently sets `LockoutEnabled` except the one-time migration and `AllowedForNewUsers` at account-creation time.
- `SecurityStampValidator` re-validates the auth cookie against the stored `SecurityStamp` on an interval (Identity default: 30 minutes) — it does not check lockout status itself; only a stamp mismatch forces re-authentication. D-10 and D-11 work together to make disable take effect against an already-active session within a bounded time.
- `User.IsInRole("SuperAdmin")` conditional pattern already used in `Views/Shared/_Layout.cshtml` for SuperAdmin-only nav items — the same style of check applies anywhere a self-disable guard needs client-visible enforcement, though the new `UsersController` is already gated server-side by the `SuperAdminOnly` policy at the controller level.

### Integration Points
- New: `Areas/Platform/Controllers/UsersController.cs`, `Areas/Platform/Views/Users/Index.cshtml` + `Index.Mobile.cshtml`
- `Areas/Platform/Views/Group/Index.cshtml` — header bar gains a new link/button to the Users page (D-06)
- `QuestBoard.Service/Controllers/Admin/AdminController.cs` — `DeleteUser` (D-01); `Views/Admin/Users.cshtml` + `Users.Mobile.cshtml` (D-02)
- `QuestBoard.Service/Controllers/Admin/AccountController.cs` — `Login` POST (D-13)
- `QuestBoard.Domain/Interfaces/IIdentityService.cs` + `QuestBoard.Repository/IdentityService.cs` — new methods for disable/enable/lockout-end lookup
- `QuestBoard.Service/Program.cs` — `SecurityStampValidatorOptions.ValidationInterval` config (D-11)

</code_context>

<specifics>
## Specific Ideas

- Disabled-account login message, exact copy: **"This account has been disabled. Contact an administrator."**
- `LockoutEnabled = false` is a deliberate, DB-only manual escape hatch the user will apply directly in production to make specific trusted accounts (including possibly their own) permanently immune to the in-app disable feature. The app must never set, clear, or expose this flag — treat it as pre-existing, out-of-band state.
- "Remove from Group" button copy should make clear the account and other group memberships stay intact (e.g. in the confirm dialog).

</specifics>

<deferred>
## Deferred Ideas

None — discussion stayed within phase scope; no scope creep raised.

### Reviewed Todos (not folded)
None — `todo.match-phase` returned zero matches for Phase 41.

</deferred>

---

*Phase: 41-safe-user-removal-account-disable*
*Context gathered: 2026-07-04*
