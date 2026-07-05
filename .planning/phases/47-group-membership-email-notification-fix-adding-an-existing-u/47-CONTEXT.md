# Phase 47: Group Membership Email Notification Fix - Context

**Gathered:** 2026-07-04
**Status:** Ready for planning

<domain>
## Phase Boundary

`GroupController.AddMember` (Platform area, SuperAdmin-only) — the action that adds an *existing* user, picked from the Members page's "available users" panel, directly to a group by `UserId` — sends no email notification on success. This is inconsistent with the other two "add a user to a group" entry points in the same codebase, `GroupController.CreateMember` and `AdminController.CreateUser`, which both already route through `UserService.CreateOrAddToGroupAsync` and enqueue either `GroupMembershipAddedEmailJob` (confirmed account) or `WelcomeEmailJob` (stranded/unconfirmed account) depending on outcome.

Triggered by a real incident: the user, acting as SuperAdmin, added a confirmed, already-has-a-password user to a different group via Platform → Group → Members → the available-users panel, and no email arrived.

**Not in scope:** `CreateMember`, `AdminController.CreateUser`, and their existing email-dispatch behavior — these are the reference pattern to match, not something to change. No new email templates or jobs — this phase reuses `GroupMembershipAddedEmailJob` and `WelcomeEmailJob` exactly as they exist today.

</domain>

<decisions>
## Implementation Decisions

### Fix approach — reuse the existing shared method
- **D-01:** `AddMember` reroutes through `userService.CreateOrAddToGroupAsync(email, name, groupId, role)` instead of calling `groupService.AddMemberAsync` directly. Since `AddMember` only has the picked user's `UserId` (not email/name), resolve those first via the same `userService.GetByIdAsync(model.UserId)` lookup the action already performs for its toast message.
- **D-02:** Switch on the same `CreateOrAddToGroupOutcome` enum `CreateMember`/`CreateUser` already use: `AddedToGroup`, `AddedToGroupStrandedAccount`, `AlreadyMember`, `Failed` all apply (`NewAccountCreated` is unreachable here since `AddMember` only ever operates on a pre-existing user).
- **D-03:** This makes `AddMember` the third caller of the one existing shared service method (`UserService.CreateOrAddToGroupAsync`), rather than inventing a second copy of the email-dispatch branching logic inline. Guarantees all three "add user to group" entry points (`AddMember`, `CreateMember`, `AdminController.CreateUser`) behave identically going forward — no possibility of a fourth silent gap.
- **D-04:** The existing `try/catch (InvalidOperationException)` around `groupService.AddMemberAsync` (used today for the "already a member" case) is replaced by the `AlreadyMember` outcome arm — `CreateOrAddToGroupAsync` returns this outcome rather than throwing, so the try/catch becomes dead code once rerouted.

### Stranded-account email choice
- **D-05:** Resolved by D-01 — no new branching logic needed. `CreateOrAddToGroupAsync` already distinguishes a confirmed account (`AddedToGroup` → `GroupMembershipAddedEmailJob`, the "you've been added to {group}" email) from a stranded/never-confirmed account (`AddedToGroupStrandedAccount` → `WelcomeEmailJob` with a password-reset/SetPassword callback URL, generated the same way `CreateMember` already does it). `AddMember`'s available-users list (`UserRepository.GetAvailableUsers`) is not filtered by `EmailConfirmed`, so this case is reachable in practice, not theoretical.

### Success toast copy
- **D-06:** `AddMember`'s success toast is updated to match `CreateMember`'s copy pattern per outcome — e.g. the confirmed-account case becomes `"{name} has been added to the group as {role}. A notification email has been sent."` (previously `"{name} added to the group as {role}."` with no mention of email). Apply the equivalent per-outcome message pattern `CreateMember` already uses for the stranded and already-member cases too, for consistency.

### Claude's Discretion
- Exact `TempData` message wording for each outcome arm beyond the confirmed-account case (D-06's principle — match `CreateMember`'s existing per-outcome copy) — follow `CreateMember`'s established messages verbatim where the scenario is identical (stranded account, already member); no new wording to invent.
- Whether/how to add a regression test asserting `GroupMembershipAddedEmailJob`/`WelcomeEmailJob` is enqueued from `AddMember` for each outcome — left to planning, following this codebase's existing test patterns for `CreateMember`/`CreateUser` (see `39-*` phase artifacts for precedent).

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### The pattern being matched (existing, working code)
- `QuestBoard.Service/Areas/Platform/Controllers/GroupController.cs` — `CreateMember` action (lines 167–265): the full outcome-switch pattern (`NewAccountCreated` / `AddedToGroup` / `AddedToGroupStrandedAccount` / `AlreadyMember` / `Failed`), including the `WelcomeEmailJob` set-password-link construction (lines 191–206, 220–238) and `GroupMembershipAddedEmailJob` enqueue (line 214) to replicate for `AddMember`.
- `QuestBoard.Service/Controllers/Admin/AdminController.cs` — `CreateUser` action, the third existing user of the same shared method; confirms the pattern is already used identically in two places.
- `QuestBoard.Domain/Services/UserService.cs` — `CreateOrAddToGroupAsync` (lines 161–~230): the shared method `AddMember` must call. Note the existing-account branch (line ~204 onward) — this is the exact path `AddMember` will always land on, since it only ever operates on a pre-existing `UserId`.
- `QuestBoard.Service/Jobs/GroupMembershipAddedEmailJob.cs` — the email job to enqueue for the confirmed-account case; signature `ExecuteAsync(string toEmail, string userName, string groupName, string role, string loginUrl, CancellationToken)`.

### The bug being fixed
- `QuestBoard.Service/Areas/Platform/Controllers/GroupController.cs` — `AddMember` action (lines 145–163): current no-email implementation to replace.
- `QuestBoard.Service/ViewModels/PlatformViewModels/AddMemberViewModel.cs` — only carries `UserId` + `Role`; email/name must be resolved via `userService.GetByIdAsync(model.UserId)` before calling `CreateOrAddToGroupAsync`.
- `QuestBoard.Repository/UserRepository.cs` — `GetAvailableUsers` (lines 62–75): confirms the "available users" list is not filtered by `EmailConfirmed`, so the stranded-account path is a real, reachable case for `AddMember`, not just a theoretical one.

No external ADRs/specs govern this — it's a targeted bug fix; requirements are fully captured in the decisions above. No REQ-IDs (ad-hoc phase, same as Phase 48).

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `UserService.CreateOrAddToGroupAsync` — already does everything `AddMember` needs (add-to-group + confirmed/stranded/already-member branching); zero new domain/repository code required.
- `Url.Action("SetPassword", "Account", ...)` / `Url.Action("Login", "Account", ...)` callback-URL construction — copy verbatim from `CreateMember`'s existing per-outcome blocks.

### Established Patterns
- All three "add user to group" entry points (`AddMember`, `CreateMember`, `AdminController.CreateUser`) live in different controllers but should now converge on one shared service call — matching this codebase's existing preference for shared service methods over per-controller duplication (see Phase 39's `CreateOrAddToGroupAsync` extraction itself, done for exactly this reason).
- `IBackgroundJobClient jobClient` is already a constructor dependency of `GroupController` (used nowhere yet in `AddMember`, but already imported/injected — `CreateMember` uses it at lines 201/214/232). No new DI wiring needed.

### Integration Points
- `GroupController.AddMember` (lines 145–163) is the only file that needs to change for the core fix. `AddMemberViewModel` is unchanged (still just `UserId` + `Role`).
- No view changes — the Members page's "Add" form already posts to this action as-is; only the controller's internal logic and the resulting toast text change.

</code_context>

<specifics>
## Specific Ideas

- The user's own framing of the fix: "should there be a service with the logic that both controllers call, so it will always work the same?" — confirmed this already exists (`CreateOrAddToGroupAsync`), and the fix is to make `AddMember` its third caller rather than to build something new.

</specifics>

<deferred>
## Deferred Ideas

None — discussion stayed within phase scope.

</deferred>

---

*Phase: 47-Group Membership Email Notification Fix*
*Context gathered: 2026-07-04*
