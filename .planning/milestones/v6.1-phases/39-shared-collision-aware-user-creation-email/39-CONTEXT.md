# Phase 39: Shared Collision-Aware User Creation & Email - Context

**Gathered:** 2026-07-04
**Status:** Ready for planning

<domain>
## Phase Boundary

Creating a user whose email already exists on the platform adds that person to the target group instead of hard-failing with a duplicate-account error — with three concrete outcomes depending on state, applied identically regardless of which screen triggers creation (group-admin Create User form today; the not-yet-built platform-level create-user entry point in Phase 40):

1. **Brand-new email** → unchanged: create account, send existing Welcome email with set-password link.
2. **Existing email, not yet in this group** → add to group with the selected role, send a new distinct "AddedToGroup" notification email (no set-password link) — unless the existing account never finished its own onboarding (see Decisions), in which case a different email fires instead.
3. **Existing email, already a member of this group** → friendly "already a member" message, no error, no duplicate membership row, no email sent.

This phase builds the shared method itself (living where both current and future callers can reach it — Domain layer, per the Service → Domain → Repository architecture) even though Phase 40's platform-level caller doesn't exist yet.

</domain>

<decisions>
## Implementation Decisions

### Stranded-account edge case (existing user, never finished onboarding)
- **D-01:** If the colliding email belongs to a user whose `EmailConfirmed` is `false` (never completed the original Welcome/SetPassword flow — no password set), the collision-add path must **not** send the plain AddedToGroup notification. Instead, resend the Welcome/SetPassword flow: generate a fresh `SetPassword` token/callback URL and enqueue the existing `WelcomeEmailJob`, mirroring the resend pattern already implemented in `AdminController.SendConfirmationEmail` (`QuestBoard.Service/Controllers/Admin/AdminController.cs:340-377`).
- **D-02:** The resent Welcome email stays generic — reuse `Welcome.razor` exactly as-is, with **no** group-specific copy or template changes. The user sees their new group membership once they log in; the email itself doesn't need to name it.
- **D-03:** The group-admin's post-submit flash message is the **same wording** whether this stranded-account resend path fires or the normal AddedToGroup path fires — the admin doesn't need to know which email variant was sent under the hood.

### AddedToGroup email content & design
- **D-04:** New email template matches `Welcome.razor`'s established visual design exactly — same `_EmailLayout.razor` shell, wax-seal image, Cinzel serif titles, parchment background. Do not introduce a lighter/simpler style; every email in the app currently shares this look.
- **D-05:** Includes a CTA button labeled "Log In" linking to `/Account/Login` — plain link, **no token** (the user already has a password from their original account).
- **D-06:** Email copy names both the group and the role granted, e.g. "You've been added to {GroupName} as a {Role}."

### Name field on collision
- **D-07:** When the submitted email collides with an existing user (either "not yet in this group" or "already a member" outcome), the Name value typed into the Create User form is **ignored** — the existing user's account Name is left untouched. Only applies to the brand-new-account path, where Name is used to create the account as it does today.

### Flash message copy & styling
- **D-08:** Add a new `RedirectWithWarning()` helper to `QuestBoard.Service/Extensions/ControllerExtensions.cs`, alongside the existing `RedirectWithSuccess`/`RedirectWithError` (which map to Bootstrap `alert-success`/`alert-danger`). The new helper maps to `alert-warning` (yellow) and is TempData-keyed like its siblings. Used specifically for the "already a member" case (CREATE-03) — it isn't a real error, but doesn't fit the existing two styles either.
- **D-09:** Three distinct flash messages after Create User submits, each naming what actually happened:
  - New account (unchanged): `"Account created for {Name}. A welcome email with a set-password link has been sent."` — `RedirectWithSuccess`
  - Collision-add (new): `"{Name} has been added to the group as {Role}. A notification email has been sent."` — `RedirectWithSuccess`
  - Already a member (new, CREATE-03): `"{Name} is already a member of this group."` — `RedirectWithWarning`
- **D-10:** Flash messages for this phase stay as the existing static alert-banner pattern (`_Layout.cshtml`'s TempData rendering) — **not** converted to toast notifications, even though the Shop view (`Views/Shop/Index.cshtml:450-489`) already has a working per-view toast pattern (Bootstrap `.toast`, `data-bs-autohide`, 5000ms delay) that could have been reused. See Deferred Ideas — user wants a site-wide toast conversion done as its own phase later, not a partial toast/banner mix introduced now.

### Claude's Discretion
- Exact name/signature of the shared creation method (e.g., `IUserService.CreateOrAddToGroupAsync(...)`) and where the "not yet a member" vs. "already a member" check lives — existing precedent: `GroupRepository.AddMemberAsync` (`QuestBoard.Repository/GroupRepository.cs:49-64`) already does an explicit existence-check-then-throw (`InvalidOperationException`) that the Platform `GroupController.AddMember` action already catches for its own "already a member" message (`QuestBoard.Service/Areas/Platform/Controllers/GroupController.cs:134-152`) — reuse or mirror this rather than the upsert-style `SetGroupRoleAsync` currently used by `AdminController.CreateUser`.
- Naming for the new email job/component (`GroupMembershipAddedEmailJob` / `AddedToGroup.razor` were suggested in prior research notes captured in STATE.md's Risk Flags — treat as a strong naming hint, not a hard requirement).
- Email subject line for the AddedToGroup email — follow the established per-email subject style (e.g. `WelcomeEmailJob` uses `"Welcome to the D&D Quest Board — set your password"`).
- Whether the shared method takes a `GroupRole` parameter or the caller sets role separately — implementation detail.

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Requirements & scope
- `.planning/REQUIREMENTS.md` §"Shared User Creation & Email-Collision Handling" — CREATE-01 through CREATE-04, the locked requirement text for this phase
- `.planning/ROADMAP.md` §"Phase 39: Shared Collision-Aware User Creation & Email" — goal, success criteria, and the explicit dependency note that Phase 40 reuses this phase's shared method
- `.planning/PROJECT.md` §"Active" — mirrors the milestone-level framing of this requirement

### Project state / risk flags
- `.planning/STATE.md` §"Risk Flags for Planning (from research)" — Phase 39 entry: "no consent step before auto-adding an existing user to a group on email collision (by milestone spec) — needs explicit UAT/human-verify since it's a silent privilege-grant path", plus the suggested `GroupMembershipAddedEmailJob`/`AddedToGroup.razor` naming

### UI/UX conventions
- `CLAUDE.md` §"UI/UX Design Guidelines" — modern-card pattern, button layout conventions that apply to any view changes in this phase (unlikely to be needed — this phase is primarily controller/service/email logic, not new views, per current scope)

No other external specs/ADRs apply — requirements fully captured in Decisions above.

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `AdminController.SendConfirmationEmail` (`QuestBoard.Service/Controllers/Admin/AdminController.cs:340-377`) — existing resend-Welcome-email pattern (fresh token, `WelcomeEmailJob` re-enqueue) to mirror for the stranded-account edge case (D-01)
- `GroupRepository.AddMemberAsync` (`QuestBoard.Repository/GroupRepository.cs:49-64`) — existence-check-then-throw `InvalidOperationException` pattern for "already a member" detection
- Platform `GroupController.AddMember` (`QuestBoard.Service/Areas/Platform/Controllers/GroupController.cs:134-152`) — already catches that exception and shows a friendly "already a member" TempData message; same shape needed here
- `IIdentityService.GetIdByEmailAsync` / `HasPasswordAsync` (`QuestBoard.Domain/Interfaces/IIdentityService.cs`) — already exist, both needed for collision detection and the stranded-account check (`EmailConfirmed` is on the `User` domain model, `QuestBoard.Domain/Models/User.cs:20`)
- `Welcome.razor`, `ChangeEmailConfirm.razor`, `_EmailLayout.razor` (`QuestBoard.Service/Components/Emails/`) — visual/structural template to copy for the new AddedToGroup email component
- `WelcomeEmailJob` (`QuestBoard.Service/Jobs/WelcomeEmailJob.cs`) — structural template (IServiceScopeFactory scope, IEmailRenderService + IEmailService) for the new email job
- `ControllerExtensions.cs` (`QuestBoard.Service/Extensions/ControllerExtensions.cs:15-31`) — `RedirectWithMessage`/`RedirectWithSuccess`/`RedirectWithError`; needs a new `RedirectWithWarning` sibling (D-08)

### Established Patterns
- Domain → Repository dependency direction means the shared collision-aware method belongs on `IUserService`/`UserService` (Domain), calling into `IIdentityService` (already does, for `CreateUserAsync`/`GetIdByEmailAsync`) and `IGroupRepository`/`IUserRepository` as needed — not on the controller.
- Hangfire jobs always take `IServiceScopeFactory`, never constructor-inject scoped services directly (established since Phase 20).
- Existing `AdminController.CreateUser` currently calls `userService.SetGroupRoleAsync` (upsert, no existence signal) rather than `GroupService.AddMemberAsync` (throws on collision) — the shared method needs the throw-on-collision variant to detect CREATE-03, diverging from `AdminController.CreateUser`'s current approach.

### Integration Points
- `AdminController.CreateUser` (`QuestBoard.Service/Controllers/Admin/AdminController.cs:113-157`) — today's only caller; must be refactored onto the new shared method
- Platform `GroupController` (`QuestBoard.Service/Areas/Platform/Controllers/GroupController.cs`) — Phase 40's future create-user entry point will call the same shared method; not built yet, no changes needed here beyond ensuring the shared method's signature works for a caller with no session-level active group (Phase 40 sources `groupId` from the route, not `IActiveGroupContext` — see STATE.md Risk Flags for Phase 40)

</code_context>

<specifics>
## Specific Ideas

- Exact stranded-account walkthrough confirmed with the user: DM in Group A creates a user (Welcome email sent, never completed) → DM in Group B creates a user with the same email → collision detected, `EmailConfirmed == false` → resend Welcome/SetPassword email (fresh token) instead of AddedToGroup, generic copy, same flash message as the normal collision-add case.
- Flash message copy locked verbatim — see D-09.
- AddedToGroup email: wax-seal/Cinzel/parchment styling, "Log In" CTA to `/Account/Login` (no token), names both group and role.

</specifics>

<deferred>
## Deferred Ideas

- **Site-wide toast notification redesign** — user wants all flash messages across the app (not just this phase's) converted from static alert banners to Bootstrap toast notifications (auto-hide, top-right), matching the pattern the Shop view already has locally (`Views/Shop/Index.cshtml:450-489`). Explicitly deferred to its own future roadmap phase rather than bundled into Phase 39 or partially applied — user confirmed this after discussing the scope tradeoff (a full conversion touches every controller's flash messages, well beyond CREATE-01..04).

### Reviewed Todos (not folded)
None — `todo.match-phase` returned zero matches for Phase 39.

</deferred>

---

*Phase: 39-shared-collision-aware-user-creation-email*
*Context gathered: 2026-07-04*
