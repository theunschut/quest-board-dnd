# Pitfalls Research

**Domain:** Adding admin/user-management gap fixes (group-scoped user list, searchable/filterable user table + inline create-user, existing-email collision auto-add-to-group) to an existing multi-tenant ASP.NET Core 10 MVC + Identity + EF Core app
**Researched:** 2026-07-03
**Confidence:** HIGH (grounded directly in this codebase's actual source — `AdminController.cs`, `GroupController.cs`, `IdentityService.cs`, `UserRepository.cs`, `QuestBoardContext.cs`, `WelcomeEmailJob.cs` — cross-checked against `.planning/PROJECT.md` decision history)

---

## Critical Pitfalls

### Pitfall 1: `UserEntity` Has No Global Query Filter — Group-Scoping Must Be Manual, and It's Trivially Easy to Forget

**What goes wrong:**
Unlike `QuestEntity` and `ShopItemEntity`, `UserEntity` is **deliberately excluded** from the EF Core Global Query Filter in `QuestBoardContext.OnModelCreating` (see the comment: "UserEntity intentionally excluded — HasQueryFilter on UserEntity breaks ASP.NET Core Identity"). This means `AdminController.Users()`'s current bug — calling `userService.GetAllAsync()` with zero group filtering — is not a filter mis-wiring, it's the *expected* behavior of an unfiltered base repository call. The fix must add an explicit join against `UserGroups` (exactly like `UserRepository.GetAllDungeonMasters`/`GetAllPlayers` already do), and there is no automatic safety net if that join is subtly wrong (e.g. left join instead of inner join, `Any()` on the wrong FK, or forgetting `token` propagation). A subtly wrong join can leak every platform user's email/name into a group-scoped page, or — the opposite failure — silently hide legitimate members if the join incorrectly excludes users who belong to the group via a role value that isn't checked.

**Why it happens:**
Every other tenant-scoped list in this codebase (`Quests`, `ShopItems`) gets automatic isolation "for free" from the Global Query Filter, so the muscle memory of "just call the repository, it's already scoped" is correct almost everywhere *except* User. This is the exact inversion of the pattern developers have been trained on by the rest of the codebase, making it the single most likely place for a new contributor (or a fast AI-driven edit) to assume safety that doesn't exist.

**How to avoid:**
- Add a dedicated repository method (e.g. `GetGroupMembersAsync(int groupId)`) that does an explicit `Where(u => DbContext.UserGroups.Any(ug => ug.UserId == u.Id && ug.GroupId == groupId))` — mirror the existing `GetAllDungeonMasters`/`GetAllPlayers` pattern exactly rather than inventing a new join style.
- Never call the bare `userService.GetAllAsync()` from `AdminController.Users()` again — that is the literal bug being fixed; a follow-up PR that reintroduces it (e.g. during a refactor) must be caught in review.
- Add an integration test that seeds two groups with disjoint user sets and asserts `AdminController.Users()` for Group A returns zero members of Group B (this directly encodes "no PII leak" as a regression guard, not just a manual check).

**Warning signs:**
- Any new code path that touches `IUserService.GetAllAsync()` or `IUserRepository.GetAllAsync()` (the unfiltered base method) inside a group-admin-facing controller.
- Code review sees `userService.GetAllAsync()` anywhere in `AdminController` or a new group-scoped view.

**Phase to address:** The group-scoped user list phase — this is the primary deliverable, not a side effect. Do not defer the join-correctness verification to "later testing."

---

### Pitfall 2: Platform "Create User" Entry Point Uses Session `ActiveGroupId` Instead of the Route `groupId`

**What goes wrong:**
`GroupController` (Platform area) is architecturally different from `AdminController` (group-admin area): every action takes an explicit route `id` (the group being managed) and never reads `IActiveGroupContext.ActiveGroupId`. `AdminController`, by contrast, reads `activeGroupContext.ActiveGroupId` from session in nearly every action (`Users`, `PromoteToAdmin`, `CreateUser`, etc.). If the new Platform "create new user" entry point is built by copy-pasting `AdminController.CreateUser`'s body (the natural implementation shortcut, since the milestone context explicitly says to "mirror the existing group-admin Create User form"), the copy is highly likely to carry over `var groupId = activeGroupContext.ActiveGroupId;` instead of using the route-provided group `id`. A SuperAdmin managing Group B's members page could end up creating the user scoped to whatever group happens to be in *their own* session (which may be a different group entirely, or `null`, causing a silent no-op or redirect to `GroupPicker`).

**Why it happens:**
The PROJECT.md decision log already documents this exact class of bug happening twice before: the `?? 1` fallback on `ActiveGroupId` that could let writes target the wrong group (Phase 30, only fixed by not using `?? 1` for writes), and the SuperAdmin/circular-DI crash at Phase 37's human-verify checkpoint. `IActiveGroupContext` is injected app-wide and reads naturally as "the current group," so any code written by pattern-matching against `AdminController` — including AI-assisted edits — will reach for it reflexively even in a controller that structurally never uses it.

**How to avoid:**
- The new Platform create-user action must take the group `id` as an explicit method parameter (matching every other `GroupController` action signature) and thread it through to `SetGroupRoleAsync(userId, id, role)` — never touch `IActiveGroupContext` inside `GroupController` at all.
- Do not inject `IActiveGroupContext` into `GroupController`'s constructor. Its absence from the constructor is itself a structural guardrail — if a future edit needs to add it, that should be a visible, reviewable diff line, not a silent reach for an already-injected dependency.
- Code review checklist item: any new `GroupController` action must be verified to use `id`/route data for the target group, never session state.
- Add an integration test where the SuperAdmin's session `ActiveGroupId` is set to Group A (or null) while creating a user via `/Platform/Group/Members/{groupId=B}` — assert the new membership lands in Group B, not Group A.

**Warning signs:**
- `IActiveGroupContext` appears in `GroupController`'s constructor parameters or a new `using QuestBoard.Domain.Interfaces` reference pulls it in incidentally.
- The new action signature doesn't take a `groupId`/`id` parameter, or takes it but doesn't pass it into `SetGroupRoleAsync`/`AddMemberAsync`.

**Phase to address:** The Platform create-user-entry-point phase. This is a review-gate item, not a "nice to catch" — flag it explicitly in the plan's verification checklist since it's a silent, hard-to-detect failure (the create succeeds, it just targets the wrong group).

---

### Pitfall 3: Existing-Email Collision Silently Grants Group Access/Role Without the User's Consent (Silent Privilege Escalation)

**What goes wrong:**
The requested behavior — when `CreateUserAsync` would fail with a duplicate-email error, auto-add the existing user to the group with the selected role instead — means an Admin (or SuperAdmin on the Platform page) can grant an existing, unrelated user Admin or DungeonMaster access to a group that user never asked to join, purely by typing their email into a "create user" form. Unlike the group-admin-created *brand-new* account flow (where the new user necessarily has no pre-existing access to protect), this path operates on an account that already has its own password, its own group memberships, and its own expectations about which groups it belongs to. If the role dropdown defaults to something other than the lowest-privilege role (`Player`), or if the admin doesn't realize the email they typed matched an existing account until after submission, a user can be silently escalated to Admin/DM in a group they have no relationship with — with no explicit accept/consent step, unlike typical "invite" flows (which usually require the invitee to accept before a role takes effect, or at minimum notify a human owner other than the actor who just clicked something).

**Why it happens:**
The milestone frames this as "friendlier than a hard failure" (fixing the "duplicate-username error" pain point), which is correct for the *no-such-membership-in-any-group* case, but the fix as specified conflates two very different situations: (a) email belongs to an existing user who is NOT yet in this group — auto-add is the requested fix, and (b) email belongs to a user who IS already in this group — must be a no-op/friendly message, not a duplicate-membership DB error (this is explicitly called out in the milestone context, and `GroupRepository.AddMemberAsync` already throws `InvalidOperationException` on the unique-composite-index collision, which `GroupController.AddMember` already catches — the new Create-User collision path must reuse this exact detection, not reinvent it). Case (a) still allows a privilege bump: if the existing user is a Player somewhere else, and the admin picks "Admin" in the role dropdown for this new group, that grants Admin access to a group they weren't a member of, with only the *submitting admin's* say-so.

**How to avoid:**
- Distinguish and test all three cases explicitly: (1) new email → create + welcome email (existing behavior, unchanged); (2) existing email, not yet a member of this group → auto-add with selected role + "added to group" notification email; (3) existing email, already a member of this group → treat as a no-op, surface a friendly message ("X is already a member of this group as <current role>"), and do NOT silently change their existing role to whatever was selected in the form (a second admin re-submitting the form with a different role should not be a stealth privilege-escalation vector either — if role changes are desired, that must go through the existing explicit Promote/Demote actions, not through the collision path).
- Reuse the existing `InvalidOperationException`-on-duplicate-membership signal from `GroupRepository.AddMemberAsync` to detect case (3) rather than pre-checking membership with a separate query that could race it (TOCTOU) — catch-and-branch, matching the pattern `GroupController.AddMember` already uses.
- Since the actor triggering this is always an already-authenticated Admin/SuperAdmin (not a public-facing form), the risk is "trusted-but-overreaching insider" rather than external enumeration — but the UI must make the collision outcome *visible* before/at submission, e.g. the success message should explicitly say "existing account added to group" (as opposed to "new account created") so the admin cannot mistake one for the other, matching the pattern the `WelcomeEmailJob`'s `isNewAccount` flag already establishes for email copy.
- Consider whether the role dropdown should be disabled/defaulted to a safe minimum (`Player`) when the email is later discovered to be an existing account rather than trusting whatever the admin pre-selected before the collision was known — at minimum, this needs an explicit product decision documented in Key Decisions, not an implicit default.

**Warning signs:**
- No distinct UI copy/message for "created" vs "added existing user" outcomes.
- The role passed to `SetGroupRoleAsync`/`AddMemberAsync` on collision is taken uncritically from the same form field used for brand-new accounts, with no separate confirmation step.
- Any code path that calls `SetGroupRoleAsync` (which upserts/overwrites role) instead of `AddMemberAsync` (which throws on existing membership) for the collision-handling branch — using the wrong one would silently change an existing member's role instead of treating same-group re-submission as a no-op.

**Phase to address:** The existing-email-collision phase — this is the highest-risk item in the milestone per the milestone context itself, and should get its own explicit UAT/human-verify checkpoint distinct from the other two features, given this codebase's documented pattern of authorization-adjacent bugs surviving to code review rather than being caught by planning.

---

### Pitfall 4: Email Case-Sensitivity Mismatch Between the Collision Check and ASP.NET Identity's Internal Normalization

**What goes wrong:**
ASP.NET Core Identity's `UserManager` normalizes email/username to uppercase internally (`NormalizedEmail`/`NormalizedUserName`) for lookups and uniqueness enforcement, and `UserManager.FindByEmailAsync` already does this correctly. `IIdentityService.GetIdByEmailAsync(string email)` delegates straight to `userManager.FindByEmailAsync(email)`, so as long as the new collision-detection path reuses `GetIdByEmailAsync` (or a new method that also delegates to `FindByEmailAsync`), case-sensitivity is handled correctly by construction. The pitfall is if a developer instead writes a **new, separate existence check** — e.g. a raw EF query like `DbSet.AnyAsync(u => u.Email == email)` or a repository-level `ExistsAsync` variant — bypassing `UserManager` entirely. A raw `==` comparison against the raw `Email` column (not `NormalizedEmail`) is case-sensitive at the SQL Server collation level in the default case-insensitive collation (usually fine) but becomes a live bug the moment collation assumptions change, and more importantly bypasses Identity's actual source of truth, potentially creating two different definitions of "does this email exist" that disagree (e.g. one path finds a match, `CreateAsync` still succeeds by luck of collation, but a subsequent read via the other definition doesn't).

**Why it happens:**
`CreateUserAsync` today calls `userManager.CreateAsync(entity)` and only learns about the duplicate via the returned `IdentityResult.Errors` — there's no pre-check today. Implementing the "detect collision, then look up the existing user, then add them to the group" flow naturally invites writing a new lookup, and the path of least resistance for someone unfamiliar with Identity internals is a direct DB query rather than routing through `UserManager`/`GetIdByEmailAsync`.

**How to avoid:**
- Always resolve the colliding user via the existing `identityService.GetIdByEmailAsync(model.Email)` (or add a symmetric `IIdentityService` method if a richer result is needed, e.g. returning the existing user's Name for the "already a member" message) — never add a second, ad hoc existence/lookup query against `UserEntity.Email` directly.
- Detect the collision either by (a) pre-checking `GetIdByEmailAsync` before calling `CreateAsync` (cleaner, avoids relying on `IdentityResult.Errors.Code` string matching) or (b) inspecting `result.Errors` for the `DuplicateUserName`/`DuplicateEmail` error codes if post-checking — but either way, resolve the *actual matched user* through `UserManager`, not a separately-written query.
- If a pre-check-then-create approach is used, be aware of the TOCTOU window between the check and `CreateAsync` — acceptable here given the low-concurrency, trusted-admin-actor context (17 members, no public registration), but worth a one-line comment noting it's accepted, not overlooked.

**Warning signs:**
- A new raw `Where(u => u.Email == email)` or `Email.ToLower() == ...` appears anywhere in `UserRepository` or `AdminController`/`GroupController`.
- The "existing user" branch resolves a *different* user than `CreateAsync`'s own duplicate-detection would have flagged (would surface as a confusing "added the wrong person" bug report).

**Phase to address:** Existing-email-collision phase, same as Pitfall 3 — this is a detail within that feature's implementation, not a separate phase, but must be explicitly checked in that phase's code review.

---

### Pitfall 5: Wrong Email Variant Sent — Extending `WelcomeEmailJob`'s `isNewAccount` Boolean Instead of Branching Cleanly

**What goes wrong:**
`WelcomeEmailJob.ExecuteAsync` currently takes an `isNewAccount: bool` that the `Welcome.razor` template uses to pick between "an account was opened in your name" copy and (per `SendConfirmationEmail`'s comment) a variant for legacy accounts with an existing password. The new "added to an existing group" notification is a **third, semantically distinct email** — the recipient already has full platform access and does not need a set-password link at all (unlike both existing `WelcomeEmailJob` variants, which always include a `CallbackUrl` for `SetPassword`). If this is implemented by adding a third bool/enum value onto the existing `isNewAccount` parameter and threading it through `Welcome.razor`'s conditional copy, it's easy to accidentally still send a `SetPassword` callback link to a user who already has a working password (confusing at best, and if `SetPassword` doesn't correctly no-op or re-confirm identity for an already-active account, potentially a way to force-reset an unrelated account's password via a link the user didn't request).

**Why it happens:**
Reusing `WelcomeEmailJob` is the path of least resistance (it already has the rendering/dispatch plumbing wired), and the milestone context explicitly warns about this: "risk of sending the WRONG email variant... if the existing `WelcomeEmailJob`'s `isNewAccount`-style branching isn't extended carefully." The natural first instinct is to add a parameter rather than a new job/template.

**How to avoid:**
- Prefer a **new, dedicated job/template** (e.g. `AddedToGroupEmailJob` + `AddedToGroup.razor`) rather than overloading `WelcomeEmailJob`/`Welcome.razor` with a third mode — this keeps the "does this email include a password-setting callback link" invariant simple and auditable per-template rather than conditional.
- If reuse is preferred for consistency (shared layout/branding), pass an explicit tri-state (e.g. `EmailVariant` enum: `NewAccount`, `ExistingAccountReconfirm`, `AddedToGroup`) rather than a second bool, and make the `CallbackUrl` parameter nullable/omitted entirely for the `AddedToGroup` case so it's structurally impossible to include a password-reset link by accident.
- The new email must not include any password-reset/set-password token — it's purely informational ("you've been added to Group X as Role Y"). Verify the job signature for this new path has no `callbackUrl`/token generation call at all, removing the class of bug entirely rather than relying on the template to ignore it.

**Warning signs:**
- The new notification email's job/template shares a `CallbackUrl` parameter with `WelcomeEmailJob`.
- `GeneratePasswordResetTokenForUserAsync` is called anywhere on the auto-add-to-existing-group path.

**Phase to address:** Existing-email-collision phase — the email-dispatch piece should be planned and reviewed alongside the group-membership logic, not bolted on afterward.

---

## Technical Debt Patterns

Shortcuts that seem reasonable but create long-term problems.

| Shortcut | Immediate Benefit | Long-term Cost | When Acceptable |
|----------|-------------------|-----------------|------------------|
| Client-side-only filtering/search on the new user table (no server-side fallback) | Fast to build, no new endpoint | Breaks pagination/search if the platform user list grows past what's reasonable to render in one page; degrades ungracefully with JS disabled | Acceptable now at 17 members / small platform scale — but the table markup should still degrade to a plain readable list (not blank) with JS off, since this is an admin tool where accessibility of the fallback matters more than polish |
| Reusing `WelcomeEmailJob`/`Welcome.razor` with a third branching bool instead of a dedicated job (see Pitfall 5) | Less new code | Conditional email templates accumulate untested branch combinations; a future 4th variant compounds the risk | Never — extract a new job now while there are only 2 existing variants, not after a 3rd is bolted on |
| Pre-check-then-create for email collision (TOCTOU window) instead of a DB-level unique constraint check | Simpler code path, avoids parsing `IdentityResult.Errors` | Theoretical double-submit race (two admins add the same email simultaneously) | Acceptable given single-digit concurrent admins and this being an internal trusted-actor flow; would not be acceptable for a public-facing signup form |
| Skipping a dedicated `GetGroupMembersAsync`-style repository method and inlining the join in the controller/service | Faster to ship | Duplicates the join logic that already exists 2x in `UserRepository` (`GetAllDungeonMasters`, `GetAllPlayers`); a 3rd near-identical inline join is exactly the kind of duplication this codebase's `Known issues` section already flags for `BoardType` lookup (3 near-identical implementations) | Never — this codebase has already paid down one instance of this exact debt category; don't reintroduce it in the same milestone |

## Integration Gotchas

Common mistakes when connecting to existing internal seams.

| Integration | Common Mistake | Correct Approach |
|-------------|----------------|-------------------|
| `IActiveGroupContext` (session-backed) | Reading `ActiveGroupId` inside `GroupController` (Platform area), which should only ever use the route `id` | Never inject/use `IActiveGroupContext` in `GroupController`; always thread the route `id` explicitly (Pitfall 2) |
| `UserManager<UserEntity>` email lookup | Writing a new raw EF query against `UserEntity.Email` instead of `FindByEmailAsync`/`GetIdByEmailAsync` | Always resolve "does this email already exist" through `IIdentityService.GetIdByEmailAsync`, which already delegates to Identity's normalized lookup (Pitfall 4) |
| `GroupRepository.AddMemberAsync` duplicate-membership detection | Re-implementing an existence pre-check (`UserGroups.AnyAsync(...)`) in the new collision-handling code instead of catching the existing `InvalidOperationException` | Reuse the existing catch-`InvalidOperationException` pattern already used by `GroupController.AddMember` for the "already a member of this group" no-op case |
| `Hangfire` job dispatch for the new notification email | Forgetting `IServiceScopeFactory`-based scope creation (the established pattern in every existing job) when adding the new email job | Copy the `WelcomeEmailJob` constructor/scope pattern exactly — scoped services cannot be constructor-injected into Hangfire jobs (already an established Key Decision in this codebase) |
| EF Core Global Query Filter mental model | Assuming the new group-scoped user query benefits from `HasQueryFilter` the way `Quests`/`ShopItems` do | `UserEntity` has no query filter by design (breaks Identity) — every group-scoping join for users must be explicit, every time (Pitfall 1) |

## Performance Traps

Patterns that work at small scale but fail as usage grows.

| Trap | Symptoms | Prevention | When It Breaks |
|------|----------|------------|-----------------|
| `AdminController.Users()`'s per-user `GetGroupRoleByIdAsync` loop (N+1 query — one query per user to resolve role) already exists in current code and will persist unless the group-scoping fix also fixes it | Slow page load as platform user count grows | Replace the N+1 loop with a single query joining `Users` to `UserGroups` for the target group and projecting role inline (same query that fixes Pitfall 1 can eliminate this for free) | Currently invisible at 17 members; worth fixing now since the group-scoping phase already has to touch this exact loop |
| Client-side table filter loading the *entire* group's/platform's user set into the DOM unpaginated | Fine at current scale (17 members), silently degrades if the platform grows to many groups × many users | Keep server-side result set scoped to the group (Pitfall 1 already forces this) so the client-side table only ever filters a small, bounded set — do not let the Platform "all available users" dropdown-replacement fetch every platform user unfiltered for client-side search | Breaks once platform user count reaches the point where a full unpaginated table becomes unwieldy — not a concern at current scale, but don't build in an assumption that Platform-area search must see literally all users if group-scoping the search would suffice |

## Security Mistakes

Domain-specific security issues beyond general web security, specific to this app's history.

| Mistake | Risk | Prevention |
|---------|------|------------|
| Silent role/access grant on email collision without a distinct consent or notice step (Pitfall 3) | An Admin can add an uninvolved existing user to their group with elevated role, and that user has no way to know until the notification email arrives (if it arrives — Resend relay has a 100/day cap) | Distinct "added existing user" UI messaging and email copy; treat this as an audit-log-worthy action even though this app doesn't currently have a formal audit log — at minimum the notification email itself functions as the user's only visibility into the change, so it must never be skipped/rate-limited into silence for this specific flow (unlike the existing `emailResendLimiter`, which caps admin-triggered resends) |
| Reusing `ActiveGroupId` session state where a route parameter should be authoritative (Pitfall 2) | Repeats the exact bug class already fixed once via the `?? 1` fallback removal (Phase 30) and the display-name ownership bypass (Phase 34.3) — this codebase has a demonstrated pattern of session/route confusion surviving to code review, not being caught earlier | Structural guardrail: don't inject `IActiveGroupContext` into `GroupController` at all; make the route `id` the only way to reach a group in that controller |
| Leaking non-member users' email addresses into a group-scoped list (Pitfall 1) | PII leak — group admins (who are not SuperAdmin/platform-level) would see emails of users outside their group, useful for phishing/social engineering against people the admin has no legitimate relationship with | Explicit join-based scoping with a regression test asserting cross-group isolation, not just a manual smoke test |
| Treating the Create-User collision path as equivalent-trust to the Platform SuperAdmin path | The group-admin `AdminController.CreateUser` collision handling and the Platform `GroupController` collision handling must behave identically (the milestone explicitly requires "applied consistently in both"), but `AdminController` runs under `AdminOnly` policy (Admin or Admin-of-a-group) while `GroupController` runs under `SuperAdminOnly` — behavior should be consistent but the authorization gate is intentionally different; do not accidentally weaken `GroupController`'s `SuperAdminOnly` gate to match `AdminController`'s broader policy while unifying the shared collision logic | Share the underlying service-layer logic (e.g. a single `IUserService`/`IGroupService` method both controllers call), but keep each controller's own `[Authorize]` policy untouched |

## UX Pitfalls

| Pitfall | User Impact | Better Approach |
|---------|-------------|-------------------|
| Ambiguous success message after Create User that doesn't distinguish "brand-new account created" from "existing account added to group" | Admin can't tell at a glance whether they just onboarded a new person or granted access to someone who already had an account elsewhere — increases the odds of an unnoticed unintended access grant (compounds Pitfall 3) | Two visibly different success messages/toast copy, mirroring the existing `RedirectWithSuccess` pattern already used elsewhere in `AdminController` |
| Client-side filter/search table with no empty-state or "no results" messaging | Looks broken when a search yields zero matches, especially once "create new user" is also on the same page (user might not realize they need to switch from "search" to "create") | Explicit no-results state that surfaces a "create new user for '{query}'" call-to-action, directly linking the searchable-table feature to the create-user feature as the milestone intends |
| Role dropdown pre-selected/defaulted the same way for both "definitely new user" and "possibly existing user" cases | Encourages the admin to not think about the role being granted when the email turns out to already exist (Pitfall 3) | Consider a lightweight confirm step or at least a highlighted callout when the system detects (post-submit) that the email matched an existing account, showing what role is about to be granted before committing |

## "Looks Done But Isn't" Checklist

- [ ] **Group-scoped user list:** Often missing a regression test proving cross-group isolation — verify with an integration test seeding 2 groups with disjoint users and asserting Group A's `Users()` view never contains Group B's emails/names.
- [ ] **Group-scoped user list:** Often still carries the N+1 `GetGroupRoleByIdAsync` loop even after adding the join — verify the fix collapses to a single query, not just an added `.Where()` on top of the existing loop.
- [ ] **Platform create-user entry point:** Often "works" in manual testing because the tester's own session `ActiveGroupId` happens to match the group they're editing on the Platform page — verify by testing with the SuperAdmin's session pointed at a *different* group than the one being managed via the Platform Members page URL.
- [ ] **Searchable/filterable table:** Often only tested with JS enabled and a small dataset — verify the "no results" and "JS disabled" states, and that the Platform "available users" data source stays group-relevant, not a full platform-wide unfiltered set (unless intentionally global, which should be an explicit decision, not a default).
- [ ] **Existing-email collision:** Often only tested for the "brand new email" and "existing email, new group" happy paths — verify the third case (existing email, ALREADY a member of THIS group) resolves as a friendly no-op message, not a raised/uncaught `InvalidOperationException` or a silent role overwrite.
- [ ] **Existing-email collision:** Often missing a check that no `SetPassword`/`CallbackUrl` token is generated or emailed for the "added to existing group" notification — verify by inspecting the actual `jobClient.Enqueue<...>` call site for the new branch, not just the email template's rendered copy.
- [ ] **Existing-email collision:** Often the new logic is added twice (once in `AdminController.CreateUser`, once in the new Platform entry point) with subtly diverging behavior — verify both call a single shared service method rather than duplicating the branch logic, given the milestone explicitly requires consistency between the two.

## Recovery Strategies

| Pitfall | Recovery Cost | Recovery Steps |
|---------|----------------|------------------|
| Group-scoped user list leaks cross-group PII (Pitfall 1) | LOW | Single-query fix (explicit join replacing `GetAllAsync()`); no data corruption occurred, only an over-broad read — no rollback/migration needed, just ship the corrected query and add the regression test |
| Platform create-user targets the wrong group via session `ActiveGroupId` (Pitfall 2) | MEDIUM | The bad membership row(s) need manual identification (`UserGroups` rows created around the incident window with the wrong `GroupId`) and removal via `RemoveMemberAsync`/direct SQL; any welcome email already sent cannot be recalled — requires a follow-up communication to the affected user |
| Existing user silently added to a group with unwanted role (Pitfall 3) | MEDIUM | `RemoveMemberAsync`/`DemoteFromAdmin` to revert; the notification email already sent is the only trace the affected user had — a human follow-up (in-app or direct message) is warranted since email content can't be recalled |
| Wrong email variant sent with an unwanted password-reset link (Pitfall 5) | HIGH | If a set-password token was actually emailed to an existing-password user's inbox, that token is live until used/expired — must invalidate via `identityService`'s token mechanism (Identity's `ResetPasswordTokenProvider` tokens are time-boxed but not individually revocable without a security-stamp bump); safest recovery is forcing a `UpdateSecurityStampAsync`-equivalent (verify current `UserManager` config) to invalidate all outstanding tokens for that user, then a manual explanation to the user |

## Pitfall-to-Phase Mapping

| Pitfall | Prevention Phase | Verification |
|---------|--------------------|----------------|
| Pitfall 1 — Unfiltered `GetAllAsync()` leaks PII across groups | Group-scoped user list phase | Integration test: 2 groups, disjoint users, assert no cross-group leakage in `Users()` view model |
| Pitfall 2 — Platform create-user uses session instead of route groupId | Platform table/create-user phase | Integration test: SuperAdmin session `ActiveGroupId` set to Group A, create user via `/Platform/Group/Members/{id=B}`, assert membership lands in B |
| Pitfall 3 — Silent privilege grant on email collision | Existing-email-collision phase | UAT/human-verify checkpoint (not just automated test) — this is a product/consent question as much as a code-correctness one; explicit test for the "already a member of this exact group" no-op case |
| Pitfall 4 — Email case-sensitivity / lookup bypassing `UserManager` | Existing-email-collision phase | Code review: grep for any new raw `Email ==` query; confirm collision detection routes through `IIdentityService` |
| Pitfall 5 — Wrong email variant / stray password-reset token on notification | Existing-email-collision phase | Code review: confirm the new notification job/branch never calls `GeneratePasswordResetTokenForUserAsync`; template/job diff review before merge |
| N+1 query in `Users()` role-resolution loop | Group-scoped user list phase (opportunistic — same query touch point as Pitfall 1) | Query count assertion or manual `dotnet-trace`/SQL profiler check during human-verify |

## Sources

- `QuestBoard.Service/Controllers/Admin/AdminController.cs` — current unfiltered `Users()` implementation, `CreateUser` flow, N+1 role-resolution loop
- `QuestBoard.Service/Areas/Platform/Controllers/GroupController.cs` — route-`id`-based Members/AddMember pattern, existing `InvalidOperationException` catch for duplicate membership
- `QuestBoard.Repository/UserRepository.cs` — established explicit-join pattern (`GetAllDungeonMasters`, `GetAllPlayers`) that the group-scoped fix should mirror
- `QuestBoard.Repository/GroupRepository.cs` — `AddMemberAsync`'s existence-check-then-throw pattern for duplicate membership
- `QuestBoard.Repository/Entities/QuestBoardContext.cs` — Global Query Filter setup and the explicit "UserEntity intentionally excluded" comment/rationale
- `QuestBoard.Repository/IdentityService.cs` / `IIdentityService.cs` — `GetIdByEmailAsync` delegating to `UserManager.FindByEmailAsync` (Identity-normalized lookup)
- `QuestBoard.Service/Jobs/WelcomeEmailJob.cs` — existing `isNewAccount` branching pattern and its `CallbackUrl`/token coupling
- `QuestBoard.Service/Services/ActiveGroupContextService.cs` — session-backed `IActiveGroupContext` semantics (null = "see all", override for Hangfire)
- `.planning/PROJECT.md` — Key Decisions log: `?? 1` fallback removal (Phase 30), display-name ownership bypass fix (Phase 34.3), SuperAdmin circular-DI crash (Phase 37) — establishes this codebase's documented recurring bug class of session/route/context confusion caught late (code review or human-verify, not planning)

---
*Pitfalls research for: admin user-management gap fixes (D&D Quest Board v6.1 Bugfixes milestone)*
*Researched: 2026-07-03*
