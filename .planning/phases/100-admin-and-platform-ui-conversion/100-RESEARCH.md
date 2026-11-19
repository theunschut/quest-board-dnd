# Phase 100: Admin and Platform UI conversion - Research

**Researched:** 2026-11-18
**Domain:** ASP.NET Core 10 MVC authorization and account rules (Admin and Platform controllers, Domain services, EF repositories), Razor views and layered vanilla CSS on the Phase 94 foundation, Playwright review tooling
**Confidence:** HIGH for every finding read in source or probed this session; the few exceptions are tagged `[ASSUMED]`. No page was rendered and no live request was made: SQL Server at `localhost:1433` is down (see Environment Availability), so rendered facts (320px bar, contrast, targets) are listed as "verify", not asserted.

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions

**Server-rule audit (new for this phase)**
- **D-01: Audit early, fix in this phase.** Before any UI slice, research (or plan 1) audits every POST, DELETE and state-changing GET reachable from the 25 views, in `AdminController` and the Platform `GroupController`, `UsersController` and `LinkedBoardsController`:
  - who may act on whom;
  - cross-board effects (the action is board-scoped but changes a global account or another board);
  - self-actions and last-Admin cases;
  - board scoping of ids (for example `DeleteQuest` has no board check in the controller and relies on whatever the service and query filters do; the audit confirms or refutes it);
  - anti-forgery, rate limits and the response a crafted request gets.
  The report lists each gap with a proposed rule and lands in a `100-SECURITY.md`-style document.
- **D-02: Account edits stay within the acting Admin's reach.** A board Admin may reset the password of, or edit the name and email of, only an account whose highest role on any board is no higher than theirs. They are refused for an account that is a SuperAdmin or an Admin of another board. The refusal is a clear Error toast and the controls are disabled with a visible reason. A SuperAdmin may act on anyone. Today `EditUser` and `ResetPassword` check only that the target is a member of the active board, yet they change the global account. The same reach rule applies to `SendConfirmationEmail` if the audit shows the same shape.
- **D-03: Self and last-Admin guards on the role buttons and Remove from Group.** The server refuses an Admin who demotes or removes themselves. It also refuses any change that would leave the board with no Admin. Each refusal is an Error toast, and the matching buttons are hidden or disabled with a visible reason (not a `title`, as in A-admin-042).
- **D-04: What counts as in scope.** A gap is fixed here when an action on one of the 25 converted views reaches it, including in Domain or Repository code that action calls. A gap not reachable from these views goes to `deferred-items.md` with a severity. Layer rules hold: no EF Core in the Service project.
- **D-05: An operator checkpoint follows the audit, before any fix slice.** The operator approves, adjusts or accepts as a residual each gap and its proposed rule. That record is the security document's basis (the Phase 99 pattern). D-02 and D-03 are already decided and are not re-asked there.

**Fixtures, real data and live actions**
- **D-06: The existing Phase 94 SuperAdmin `qb-audit-platform@example.invalid` is reused** for the Platform pages. Its credentials stay where Phase 94 put them and are never printed. No new SuperAdmin is created; no app form can grant one.
- **D-07: New p100 fixtures are created through the app's own forms** with the reused admin and SuperAdmin logins (Phase 94 D-25):
  - accounts `qb-audit-p100admin`, `p100dm`, `p100player`, `p100unconfirmed` and others as the guard tests need (for example an account that is an Admin on a second p100 board, for D-02), all `@example.invalid`, with passwords only in `~/qb-ui-review/phase-100/credentials.json` (mode 600);
  - boards "P100 Audit ..." (One-Shot and Campaign, so Q-87 shows both status branches; an empty one; one with no members, so its Delete appears);
  - a p100 linked set with p100 boards;
  - every empty state listed in `94-AUDIT-admin.md` § "Empty-state copy".
  No existing account, board, membership or linked set is changed. Fixtures are kept afterwards.
- **D-08: All state-changing actions may run, on p100 fixtures only.** That covers role changes, reset password, welcome resend (to Mailpit), add, remove and create member, Disable and Enable, and linked set create, rename, add, remove and delete. They are used both to capture states and to prove the D-02 and D-03 guards live. They are never run on a real account, board or linked set.
- **D-09: Captures of pages listing real members and boards are taken as they are and stay local.** This covers Platform/Users, Group/Index, Members and Admin/Users. Screenshots stay under `~/qb-ui-review/phase-100/`. Reports describe them structurally and never quote a real name, email or board. These gate packages are not published to a review branch unless real rows are masked first.
- **D-10: The EmailStats statistics branch is exercised through a test-only stub of `ResendStatsClient`.** Canned figures feed the integration tests and a dev-only capture, so the stat tiles' contrast is measured (A-admin-036). No real key is used, and `appsettings.json` is not edited.
- **D-11: Any email the phase triggers goes to the Mailpit sink only** (`qb-mailpit`; starting it is pre-approved).

**Destructive actions**
- **D-12: Remove from Group (Admin/Users) and Delete (Admin/Quests) become ordinary POST forms.** They are the same as every other action in the area: anti-forgery, a PRG redirect, and Success or Error toasts. The `[HttpDelete]` endpoints `DeleteUser` and `DeleteQuest` become POST, and their tests are updated to the same intent. The four inline copies of the fetch script and their `alert()` go (A-admin-033).
- **D-13: Confirmations stay native `confirm()` with today's wording**, through `wwwroot/js/confirm-submit.js` `data-confirm-message` (the Phase 95, 98 and 99 precedent). This covers Remove Member, Remove Board, Delete Set, Disable, Remove from Group and Delete Quest. The LinkedBoards handler strings built in Razor become data attributes.

**Gate 1, look and copy**
- **D-14: The Gate 1 look check compares against the `quest-form` Direction A render and the live converted Phase 98 and 99 pages.** No Direction A admin render exists. There is one mockup per page type, each side by side with its reference:
  - a table list (Admin/Users or Group/Index);
  - a form (EditUser);
  - a confirmation page (Group/Delete);
  - the Members Create New User modal on the opaque modal surface (A-admin-006).
  The glass, scrim, badges, buttons and table cells must match what shipped in v6.3 and v6.4.
- **D-15: Gate 1 also carries:**
  - the AFTERs of Q-124, Q-125 and Q-126, desktop and phone;
  - the D-16 wording changes;
  - how the D-02 and D-03 refusals look (disabled buttons with their visible reason).
  Nothing in those is built before Gate 1 approves it.
- **D-16: The copy drift in A-admin-044 is fixed now** with one shared display mapping for board type ("One-Shot", "Campaign") and group role, used by both layouts. It is display text only; enums are untouched. Each wording change, for example "DungeonMaster" to "Dungeon Master" and "Admin" against "Administrator", is a Gate 1 item for the operator's yes.
- **D-17: The Platform layouts are verified on every page, and anything still failing is fixed here.** This covers A-admin-008 (the 320px bar), A-admin-014 (nav contrast), A-shell-037 and 038, the layout part of A-admin-029 (skip link, landmarks, toggler and offcanvas names) and A-admin-030 (toggler size). Any remaining failure is fixed as a slice in this phase, not sent back to the foundation.

**Gates, fixtures and delivery (carried over from Phases 98 and 99)**
- **D-18: Two operator gates, reviewed from a local package.**
  - Gate 1, early: `~/qb-ui-review/phase-100/gate1/`, with `index.html`, `README.md` and `img/`.
  - Gate 2, at the end: `~/qb-ui-review/phase-100/gate2/`, one before/after sheet, desktop and phone, with each contrast or target-size exception as an explicit accept or reject item.
  A rejected AFTER is redrawn until approved. The packages are published to a review branch only on request, and D-09 applies.
- **D-19: Run on the Linux machine that ran Phases 94 to 99**, reusing `~/qb-ui-review/phase-94` (Playwright, tools, renders, BEFORE paths). BEFORE is re-captured from the current branch into `~/qb-ui-review/phase-100/`. Q-126 has no populated BEFORE yet (G1 note), so this phase takes one. Front-end work invokes the `senior-frontend` skill.
- **D-20: The first plan opens with an operator checkpoint.** The operator confirms SQL Server at `localhost:1433`, which the agent never provisions. The local database holds real member data.
- **D-21: Work lands in slices on `milestone/v9-rolling-improvements` in this checkout**, with one release, v6.5.0, through a PR into `main` once both gates have passed. Each slice leaves every page usable and the suite green. Each plan's docs commit updates ROADMAP.md and STATE.md. Real-phone and screen-reader checks go in the phase UAT and may run after deploy.

### Claude's Discretion

- How the D-02 "highest role anywhere" check is expressed: a Domain service method or a query, kept in the right layer.
- Slice order and plan count. A sensible order:
  1. the audit, its checkpoint and the server fixes, with integration tests;
  2. the shared badge, button, table, link and muted-text fixes and the Platform layout verification (they clear A-admin-001 to 005 and 010 to 013 together);
  3. the forms (CreateUser, EditUser, ResetPassword, Group Create, Edit and Delete);
  4. the lists (Admin/Users, Admin/Quests, Group/Index, Members, Platform/Users, LinkedBoards), with the gated relayouts after Gate 1;
  5. EmailStats.
- How the six phone sheets and the admin ranges of `modern-card.css` and `site.css` consolidate into page sheets loaded through `@section Styles`. Dead rules go (A-admin-043, the nine duplicate glass cards of A-admin-038).
- The `autocomplete="new-password"` fix (A-admin-037), the Actions header alignment (A-admin-039) and the stable button positions (A-admin-040) follow the audit's proposed fixes.
- Ports: a free pair, checked before use.

### Deferred Ideas (OUT OF SCOPE)

- Server gaps found by the audit that no converted view reaches go to `deferred-items.md` (D-04).
- None raised as new capabilities during the discussion.
</user_constraints>

<phase_requirements>
## Phase Requirements

No requirement IDs are mapped (ROADMAP: "Requirements: TBD"). The planner tracks CONTEXT decisions D-01 to D-21 and the ten-point "Definition of converted" instead.

| ID | Description | Research Support |
|----|-------------|------------------|
| D-01, D-04, D-05 | Server-rules audit, scope rule, operator checkpoint | "Server-rules audit" gap table G-01 to G-23; the checkpoint question list O-1 to O-13 |
| D-02 | Account reach rule | "D-02 and D-03: how" (layering, SuperAdmin model, what `ResetPasswordAsync` enforces); G-01, G-02, G-07, G-08 |
| D-03 | Self and last-Admin guards | "D-02 and D-03: how"; G-03, G-04, G-14 |
| D-06, D-07, D-08, D-09, D-11, D-18, D-19, D-20 | Fixtures, live actions, gates, environment | "Operator and fixture environment", Environment Availability |
| D-10 | EmailStats stub | "D-10: the EmailStats seam" (handler stub proven this session) |
| D-12, D-13 | DELETE to POST, native confirms | "D-12 and D-13" (affected scripts, tests, wording quoted verbatim) |
| D-14, D-15, D-16 | Gate 1 look, relayouts, wording | "UI conversion facts", Gate 1 item list |
| D-17 | Platform layouts | "Platform layout items (D-17)": five of six are already in source and pinned; render verification only |
| D-21 | Slices, release v6.5.0, docs commits | "Tracking commands", "Slice proposal" |

</phase_requirements>

## Summary

The phase is two jobs in one, and the first changes the order of everything. **The server-rules audit found one high-severity chain and a handful of smaller gaps.** A board Admin can take over any account on the platform: `CreateUser` accepts the email of an existing account (including a SuperAdmin's) and silently adds it to the Admin's board (`UserService.cs:178-195`), after which `ResetPassword` and `EditUser` pass their only guard, "target is a member of the active board" (`AdminController.cs:318-320`, `231-233`), and change the *global* account. `IdentityService.AdminResetPasswordAsync` checks nothing about the target and, despite its XML doc, nothing about the acting role (`IdentityService.cs:67-79`). D-02's reach rule closes the SuperAdmin and foreign-Admin cases. It does not close the add-by-email step for ordinary accounts that sit on other boards; that residual is operator question O-1.

**`DeleteQuest` is limited to the active board; the question is settled with a probe.** The `QuestEntity` global filter is fail-closed (`QuestBoardContext.cs:480-483`), `DbSet.FindAsync` applies it when it reaches the database (run this session: a quest on another board returns `null`, and `null` when no board is active; only an already-tracked entity bypasses it), and `QuestService.RemoveAsync` re-reads the quest through a filtered query before it deletes anything. A crafted id from another board gets 404. The real defect on that path is different: `ReminderLogs.QuestId` is `NoAction` (`QuestBoardContext.cs:274-279`) and nothing deletes reminder logs, so on SQL Server a quest that had a session reminder sent cannot be deleted (unhandled `DbUpdateException`, today an `alert()` after a failed fetch, tomorrow a 500 after the PRG change). EF InMemory does not enforce foreign keys, so no test sees it.

**The UI half is smaller than the ROADMAP counts suggest, and has drifted since they were written.** The foundation already fixed the Platform layouts in source (skip link, landmarks, collapsing bar named "Toggle navigation", offcanvas named "Open navigation menu" and "Close navigation menu", 44px toggler, same foundation links on both layouts), pinned by `UiFoundationDesktopShellGuardTests` and `UiFoundationMobileShellGuardTests`. `modern-card.css` and `site.css` no longer exist; the admin ranges are gone and `.modern-card`, `.badge`, `.table` live in `components.css`. Phase 102 (board deletion, already merged) added a converted Group/Delete pair with a partial, a module and three clean layered sheets, so the Platform area is 26 view files and nine sheets, not 25 and six. What remains is markup re-pointing, the six unlayered phone sheets, twelve inline handlers, four inline scripts, one inline `style=`, the twins' content answers, three gated relayouts, and a rendered sweep.

**Primary recommendation:** Plan the server work first as its own slice set: re-verify the gap table with red tests, hold the D-05 checkpoint, then fix in this order (account reach D-02, role and removal guards D-03, DELETE to POST with its minimal view forms plus the reminder-log cleanup, EmailStats seam). Capture the BEFORE baseline *before* any code changes so it stays a pure baseline. Convert the UI after Gate 1 onto two page sheets per layout (`admin-pages`, `platform-pages`, each with a `.mobile` twin, `@layer pages`), and end every plan with the tracking block under "Tracking commands".

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|-------------|----------------|-----------|
| Who may act on whom (D-02 reach) | Domain service (`IUserService`) | Repository query, Identity role read | A rule about accounts, not about HTTP; the Service project may not hold EF Core. The controller only turns a refusal into a toast |
| Self and last-Admin guards (D-03) | Domain service (`IGroupService`) | Repository count query | Must hold for every caller; `RemoveMemberAsync` is documented as the one chokepoint for removals, so the guarded method wraps it rather than changing it (the Platform page must still empty a board) |
| Active-board scoping of quest ids | Repository (EF global query filter) | Domain `QuestService` re-read | Already fail-closed; nothing to add at the controller |
| Anti-forgery on every state change | API / Backend (`[ValidateAntiForgeryToken]`) | Razor form tag helper adds the token | Reflection sweep `AntiForgeryTokenCoverageTests` covers `[HttpPost]` only, so `[HttpDelete]` was uncovered until D-12 |
| Rate limit on manual email sends | API / Backend (singleton `PartitionedRateLimiter<int>`) | — | Keyed by target user id; form fields bind after policy middleware, hence the manual `AttemptAcquire` |
| Confirmations | Browser (`confirm-submit.js`) | Server renders the sentence into `data-confirm-message` | No markup built from strings; works for forms present at load |
| Status, role and board-type labels | Frontend Server (one static mapping, as `ShopItemDisplay`) | — | One mapping stops twin drift (D-16, A-admin-016, A-admin-044) |
| Disabled-with-reason controls (D-02, D-03) | Frontend Server (per-row view-model flags) | Domain service computes the flags | The server both decides and renders the reason; the same service answers a crafted POST |
| EmailStats data | API / Backend (`ResendStatsClient`, 5-minute memory cache) | External Resend API | Stub at the HTTP handler in tests; see D-10 section |
| Platform shell (bar, offcanvas, landmarks) | Frontend Server (the two Platform layouts) | CDN / Static (`shell.css`, `shell.mobile.css`) | Foundation owns it; this phase verifies |

## Standard Stack

No new packages. Everything is the project's existing stack.

### Core
| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| ASP.NET Core MVC + Razor, .NET SDK | 10.0.115 `[VERIFIED: dotnet --version]` | Controllers, views, `.Mobile` twin expander | Project stack (CLAUDE.md) |
| EF Core + Identity EF store | 10.0.9 `[VERIFIED: QuestBoard.Repository.csproj:10-16]` | Repositories, `UserManager`, SuperAdmin Identity role | Existing; EF stays in `QuestBoard.Repository` |
| Bootstrap | 5.3.8 vendored | Modal, offcanvas, collapse | Existing |
| xUnit v3 + FluentAssertions + `Microsoft.AspNetCore.Mvc.Testing` + EF InMemory | repo versions | Unit, integration and static guard tests | Phase 94 to 99 pattern |
| Playwright | 1.64.1 `[VERIFIED: ~/qb-ui-review/phase-94/tools/node_modules/playwright/package.json]`, Chromium headless shell present | Gate captures, probes, contrast | Reused through the symlinked `node_modules`; never `npm install` |

### Supporting
| Item | Purpose | When to Use |
|------|---------|-------------|
| `wwwroot/js/confirm-submit.js` | `form[data-confirm-message]` native confirm | All six D-13 confirmations (forms present at load) |
| `RedirectWithSuccess` / `RedirectWithError` / `RedirectWithWarning` (`Extensions/ControllerExtensions`) | PRG plus toast | Admin controller refusals and D-12 results; Platform controllers write `TempData` keys directly |
| `Html.LocalTime` / `Html.WallClock` | Dates | Group/Index "Created" already uses `LocalTime`; nothing else in the area renders a date |
| `IIdentityService.IsSuperAdminAsync(int)` | SuperAdmin test by id | Exists and is used by `QuestMoveService.cs:237` `[VERIFIED: IdentityService.cs:174-178]` |
| `.empty-state`, `.form-page`, `.form-actions`, `.refusal-line`, `.move-control-reason` | Foundation components | Empty states, forms, the visible reason under a disabled control (the last is move-specific by name; a neutral class is a components-layer addition) |
| `~/qb-ui-review/phase-99/tools/` (238 files) | Capture, fixtures, gates | Copy and re-point to `phase-100`; never edit in place |

### Alternatives Considered
| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| Reach check inside the controller only | Enforce inside `UserService.ResetPasswordAsync` as well | Recommended: both. The service overload already receives the acting principal and is the only way to reset another account's password |
| Last-Admin guard inside `RemoveMemberAsync` | A separate guarded method that calls it | Recommended: separate. The Platform Members page (SuperAdmin) must be able to empty a board, and board deletion requires zero members (`GroupController.cs:216-221`) |
| Interface extraction for `ResendStatsClient` | Replace the named `Resend` HTTP handler in tests | Recommended: handler (zero production change, exercises the real client and aggregator). See D-10 |

**Installation:** none. **Version verification:** no package is added, so there is nothing to look up on a registry.

## Package Legitimacy Audit

No external packages are installed or changed by this phase. Nothing to audit.

| Package | Registry | Age | Downloads | Source Repo | Verdict | Disposition |
|---------|----------|-----|-----------|-------------|---------|-------------|
| (none) | — | — | — | — | — | — |

**Packages removed due to [SLOP] verdict:** none
**Packages flagged as suspicious [SUS]:** none

## Server-rules audit (D-01)

**Method:** every `[HttpPost]`, `[HttpDelete]` and state-changing GET in `AdminController`, Platform `GroupController`, `UsersController` and `LinkedBoardsController` was read in full, then followed into `UserService`, `GroupService`, `LinkedBoardSetService`, `IdentityService`, `UserRepository`, `GroupRepository`, `BaseRepository`, `QuestService` and `QuestRepository`. Two behaviours were probed in scratch projects outside the repository (EF `Find` against a global filter; MVC binding of an undefined enum value). RIP MCP was not available in this session; grep and reads were used. Everything below is **source reading unless marked "probed"**; nothing was run against the application or SQL Server.

### Surface map (what exists, who may call it)

| Controller | Gate | State-changing actions (all `[ValidateAntiForgeryToken]` `[VERIFIED by reading each action]`) |
|-----------|------|----------------------------------------------------------------------------------------------|
| `AdminController` (`Controllers/Admin/AdminController.cs`) | class `AdminOnly` (`:21`): SuperAdmin, or `GroupRole.Admin` on the active board (`AdminHandler.cs:17-35`); membership of the active board is re-verified on every non-GET request (`GroupSessionMiddleware`) | `PromoteToAdmin :58`, `DemoteFromAdmin :71`, `PromoteToDM :84`, `DemoteToPlayer :97`, `CreateUser :116`, `EditUser :225`, `ResetPassword :312`, `DeleteUser :346` (`[HttpDelete]`), `SendConfirmationEmail :367`, `DeleteQuest :439` (`[HttpDelete]`, `[BoardPage(Quests)]`); `EmailStats :455` adds `SuperAdminOnly` |
| Platform `GroupController` | class `SuperAdminOnly` (`:22`) | `Create :56`, `Edit :90`, `DeleteConfirmed :228` (name-confirmed board delete, Phase 102), `AddMember :314`, `CreateMember :395`, `RemoveMember :503` |
| Platform `UsersController` | class `SuperAdminOnly` (`:9`) | `Disable :33`, `Enable :50` |
| Platform `LinkedBoardsController` | class `SuperAdminOnly` (`:68`) | `Create :86`, `AddBoard :103`, `Rename :112`, `RemoveBoard :121`, `Delete :130` |

State-changing GET: only `Admin/EmailStats?force=true` (clears the cache and calls the Resend API, `AdminController.cs:454-478`). `AccountController.ConfirmEmailChange` is an anonymous token GET that changes an account's email (`AccountController.cs:298-318`); it is reached from an email link, not from any of the 25 views.

**SuperAdmin is modelled as an Identity role, not a flag and not a group role.** `policy.RequireRole("SuperAdmin")` (`Program.cs:91-92`); `IsInRole("SuperAdmin")` short-circuits `AdminHandler` (`:17`) and `UserService.GetEffectiveGroupRoleAsync` returns `GroupRole.Admin` for it (`UserService.cs:67-73`). `GroupRole` is `Player = 0, DungeonMaster = 1, Admin = 2` (`Enums/GroupRole.cs`); a SuperAdmin may or may not also hold a `UserGroups` row (the test helper seeds a Player row, `AuthenticationHelper.cs:119-141`). So "is this target a SuperAdmin" must read the Identity role (`IsSuperAdminAsync`), never a membership role.

### Gap table

Severity scale: High (account takeover or data loss reachable by an authenticated Admin), Medium (wrong result or unhandled error on a normal path), Low (UX or hygiene), Info (recorded, no change proposed). "In scope" follows D-04: reachable from one of the 25 views.

| ID | Action | Current behaviour (evidence) | Proposed rule | Scope | Severity |
|----|--------|------------------------------|---------------|-------|----------|
| G-01 | `ResetPassword` POST, `EditUser` POST (name, email, key flag) | Both check only `GetGroupRoleByIdAsync(target, activeGroup) != null` (`AdminController.cs:318-320`, `:231-233`) and then change the global account: `ResetPasswordAsync(User, user, pw)` → `identityService.AdminResetPasswordAsync` (`UserService.cs:107-110`) which generates a token and resets with no target check and no role check, although its doc says it "Fails if the calling ClaimsPrincipal is not in the Admin role" (`IdentityService.cs:67-79`; `IIdentityService.cs:43-47`). An email change mails a change token to the NEW address (`AdminController.cs:263-277`) and `ConfirmEmailChange` is anonymous and also renames the username (`IdentityService.cs:124-135`), so a board Admin who controls the new address takes the account, then uses Forgot Password. Chain verified by reading, not executed | D-02 reach rule (decided): refuse when the actor is not a SuperAdmin and the target is a SuperAdmin or holds `Admin` on any board other than the active one. Enforce in the controller (GET and POST) and inside the service overload | In scope (D-02) | High |
| G-02 | `CreateUser` POST | An email that already belongs to an account is not refused: `CreateOrAddToGroupAsync` adds the existing account to the active board with the posted role, ignoring the posted name (`UserService.cs:178-195`, outcomes at `AdminController.cs:148-181`). It is the step that turns any account into a "member of the active board", so the only precondition on G-01 is not a boundary | D-02 closes it for SuperAdmin and foreign-Admin accounts. For an ordinary account that also sits on another board the Admin can still add then reset. Operator question O-1: accept as the decided rule (the add emails the owner: `GroupMembershipAddedEmailJob` / `WelcomeEmailJob`) or go stricter | In scope | High before D-02, Medium after |
| G-03 | `PromoteToAdmin`, `DemoteFromAdmin`, `PromoteToDM`, `DemoteToPlayer`, `DeleteUser` | No self or last-Admin check: an Admin can demote or remove themselves, and the last Admin of a board can be demoted or removed (`AdminController.cs:58-106`, `:344-363`; `UserRepository.SetGroupRoleAsync :105-120`; `GroupService.RemoveMemberAsync :40-41`). The action name is not validated against the current role: `PromoteToDM` on an Admin demotes, `PromoteToAdmin` on a Player skips a level, `DemoteToPlayer` on an Admin demotes two levels | D-03 (decided). The guard judges the **resulting role**, not the action name: refuse when target is the actor, or when the target is currently Admin, the new role is not Admin and the board has exactly one Admin. Same for removal. Guarded method on `IGroupService`, repository count query | In scope (D-03) | Medium |
| G-04 | Same five actions, board scope | Target outside the active board: silent redirect to Users with no toast (`:63-64`, `:76-77`, `:89-90`, `:102-103`; `DeleteUser` returns 404, `:353`). Board scope itself holds (covered by `RoleChangeActions_WhenTargetUserOutOfGroup_ShouldNotChangeMembership`, `AdminControllerIntegrationTests.cs:318-355`) | Keep the scope check; answer a stale page with an Error toast ("That person is not a member of this board any more.") instead of silence | In scope | Low |
| G-05 | `DeleteQuest` | **Board-scoped (see verdict below).** Defect on the path: `QuestService.RemoveAsync` deletes the quest's player signups and the quest (`QuestService.cs:110-125`) but not its `ReminderLogs`; `ReminderLogEntity → Quest` is `NoAction` (`QuestBoardContext.cs:274-279`, migration snapshot `QuestBoardContextModelSnapshot.cs:1524-1528`) and no code removes reminder logs outside board deletion (`GroupRepository.cs:173-174`). On SQL Server the delete throws `DbUpdateException`. Same for a follow-up quest on another board: `Include(FollowUpQuest)` is filtered by the board filter, so an other-board follow-up is not tracked and not nulled (`QuestRepository.cs:96-115`, relation `ClientSetNull` at `QuestBoardContext.cs:267-272`; such rows exist, `GroupRepository.cs:216-218`). Not reproduced: InMemory does not enforce FKs and the SQL suites are skipped | In `QuestService.RemoveAsync`, remove the quest's reminder logs through the change tracker in the same save and clear an other-board follow-up's `OriginalQuestId` (as board deletion does, `GroupRepository.cs:246-247`); controller turns any remaining `DbUpdateException` into an Error toast. The same fix serves `QuestController.Delete` (deferred-items note: not reachable from the 25 views) | In scope | Medium |
| G-06 | `SendConfirmationEmail` | Rate-limit lease is taken **before** the membership check (`:371-382`), so an Admin can burn the 3-per-hour budget (`Program.cs:178-187`) of any account id, including accounts on other boards; their own board Admin is then refused for an hour | Take the lease after the scope check, the reach check and the already-confirmed check | In scope | Low |
| G-07 | `SendConfirmationEmail` shape (D-02 asks) | The shape is **not** the takeover shape: the set-password link goes to the account's own stored address (`:395-413`). It is a mail-nuisance shape. A reach check is cheap and consistent (the row's button is disabled with the same reason) | Apply the D-02 reach rule for consistency; operator confirms at the checkpoint | In scope | Low |
| G-08 | `EditUser` POST with an email change | Name and key flag are saved (`:250`) before the limiter is consulted (`:256`); when refused the response is a bare-text 429 page (`:257-261`), after part of the edit has been written. `SendConfirmationEmail` returns the same bare 429 (`:372-376`) | Acquire before any write; answer with a PRG Error toast instead of a bare page (tests currently expect 429: `AdminControllerRateLimitIntegrationTests.cs:70-95`, `:135-185`). Operator question O-4 (status contract) | In scope | Low |
| G-09 | `CreateUser` POST | The one-shot welcome email is deliberately exempt from the limiter (`Program.cs:176-177`), so an Admin can create accounts for arbitrary external addresses without limit and spend the relay's 100-per-day quota that the resend limiter exists to protect (`AdminController.cs:369-370`) | Per-actor limiter (for example 20 per hour) or accept. Operator question O-5 | In scope | Low |
| G-10 | `CreateUser` / Platform `AddMember` toasts | The toast prints the real name of an existing account for any email (`AdminController.cs:159`, `:180`) | Accept (collision-aware by design, documented in `IUserService.cs:124-135`) | In scope | Info |
| G-11 | `EditUser` | Name, email and `HasKey` are global account fields edited from a board page; a board Admin changes how a plain member appears on every board they sit on. `HasKey` is read only on pages elsewhere. D-02 permits this for non-elevated accounts | Record as residual in the security document | In scope | Info |
| G-12 | Reset and email change | No notification reaches the account owner on an admin password reset, and the old address is not told of an admin-initiated email change (`AdminController.cs:263-277`, `:328-332`) | New email templates are a new capability and Phase 101 territory: defer with severity Low. Operator question O-8 | Deferred | Low |
| G-13 | Platform `RemoveMember` | No check that the group or the membership exists; the toast says "Member removed from the group." even when nothing was removed (`GroupController.cs:501-508`; `GroupRepository.cs:114-118` returns silently) | Success only if a row went; otherwise Warning "was not a member" | In scope | Low |
| G-14 | Platform `RemoveMember` and the last Admin | A SuperAdmin may remove every member including the last Admin. Board deletion requires zero members (`GroupController.cs:216-221`), so the Platform page must keep doing this | Exempt from D-03 (the guard lives on the Admin-page methods, not in `RemoveMemberAsync`). Operator question O-2 | In scope | Info |
| G-15 | Platform `CreateMember` (modal) | On an invalid model or a failed create the page re-renders with the validation summary **inside the closed modal** (`GroupController.cs:400-412`, `:478-497`; `Members.cshtml` has no script, census `<script`=0; nothing reopens `#createMemberModal`), so the refusal is invisible | Reopen the modal on load when the model has errors (module, data attribute), or redirect with an Error toast like `AddMember`. Operator question O-12 | In scope | Low |
| G-16 | Platform `Disable` / `Enable` | Self-disable is refused (`UsersController.cs:35-40`); a peer SuperAdmin may be disabled by design (`Disable_Post_PeerSuperAdmin_IsAllowed`, `UsersControllerIntegrationTests.cs:124`); a failed disable or enable toasts. Two SuperAdmins can lock each other out and only SQL recovers | Accept; recorded | In scope | Info |
| G-17 | `EmailStats?force=true` | A GET that clears the 5-minute cache and calls Resend (`:454-478`). SuperAdmin only, no data written | Accept (idempotent refresh, SuperAdmin only) or make Refresh a POST. Operator question O-6 | In scope | Info |
| G-18 | Remove from Group confirm wording | The Admin page's sentence says the person "will keep their account and any other group memberships" (`Users.cshtml:165`) but `RemoveMemberAsync` also deletes the person's event availability answers on this board (`GroupRepository.cs:121-127`); the Platform sentence says so (`Members.cshtml:77`) | Gate 1 wording item: bring the Admin sentence in line (D-13 keeps today's wording unless the operator approves a change) | In scope | Low |
| G-19 | ResetPassword form hint | The view model says at least 6 characters (`ResetPasswordViewModel.cs:12`), Identity requires 8 with upper, lower and digit (`Program.cs:50-55`), so 6 or 7 characters pass the form and fail on the server with Identity's message | Align the attribute to the policy; Gate 1 copy item | In scope | Low |
| G-20 | Undefined enum values (`GroupRole`, `BoardType`) | **Refuted by probe.** MVC rejects `GroupRole=7` and `-1` with "The value '7' is invalid." (ModelState invalid), so a crafted role never reaches the service. `CreateUser`, `AddMember`, `CreateMember`, group `Create` all handle invalid ModelState | None | — | Info (no gap) |
| G-21 | Linked boards (5 actions) | Name length and emptiness, defined board type, duplicate name, type mismatch, already linked, not in set and concurrent changes are all validated in the service with explicit outcomes and toasts; writes are tracked (`LinkedBoardSetService.cs`, `LinkedBoardSetRepository.cs`) | None | — | Info (no gap) |
| G-22 | Anti-forgery | Every POST and DELETE in the four controllers carries `[ValidateAntiForgeryToken]` `[VERIFIED: reading each action]`. `AntiForgeryTokenCoverageTests` reflects over `HttpPostAttribute` only (`AntiForgeryTokenCoverageTests.cs:29-33`) and the test factory validates nothing (`TestAntiforgeryDecorator`), so the two `[HttpDelete]` actions were guarded only by hand | D-12 makes both POST, which brings them under the sweep; keep the sweep green | In scope | Info |
| G-23 | Crafted-request responses | Out-of-board id: silent redirect (role actions, EditUser, ResetPassword, SendConfirmationEmail), 404 (`DeleteUser`, `DeleteQuest`); missing token: framework 400; invalid model: re-render | Normalise to PRG plus Error toast on the Admin controller (G-04) | In scope | Low |

### DeleteQuest verdict (CONTEXT asked for a definitive answer)

**`DeleteQuest` is limited to the active board.** Trace, each hop read this session:
1. `AdminController.DeleteQuest(int id)` calls `questService.GetByIdAsync(id)` (`AdminController.cs:441`) with no board check of its own.
2. `QuestService` does not override `GetByIdAsync`; `QuestRepository` does not either, so it is `BaseRepository.GetByIdAsync` → `DbSet.FindAsync([id])` (`BaseRepository.cs:44-48`).
3. `QuestEntity` has a global query filter: `activeGroupContext.ActiveGroupId != null && e.GroupId == activeGroupContext.ActiveGroupId` (`QuestBoardContext.cs:480-483`). It is fail-closed and has no SuperAdmin escape.
4. **Probed (EF Core InMemory 10.0.9, scratch project):** with the filter above and active board 1, `FindAsync` for a quest on board 2 printed `null`; `FirstOrDefault` printed `null`; with no active board `FindAsync` printed `null`; **only when the entity was already tracked** did `Find` return the other board's row. In `DeleteQuest` nothing has tracked a quest before the first call. SQL Server runs the same query path (the filter is part of the compiled query), so this holds there too. `[VERIFIED: probe run this session]`
5. Even if step 4 returned the row, `QuestService.RemoveAsync` re-reads through `QuestRepository.GetQuestWithManageDetailsAsync` (`DbContext.Quests`, filtered) and returns without deleting when it gets `null` (`QuestService.cs:110-125`, `QuestRepository.cs:96-115`). The delete cannot cross boards.
6. A crafted id from another board therefore answers **404** (`AdminController.cs:442-445`). The active board itself is membership-verified on every non-GET request (`GroupSessionMiddleware`) and the quest list gate `[BoardPage(Quests)]` refuses when the page is off (`BoardPageGateMatrixTests.cs:390-402`).

No board-scoping rule is needed. Needed instead: the reminder-log and cross-board follow-up cleanup (G-05), a regression fact that a cross-board id deletes nothing and answers a toast after the POST change (no test exists today: grep of `DeleteQuest` in the test projects finds only the gate-matrix refusal and the coverage map), and keeping the write on the change tracker (`DbSet.Remove` plus `SaveChanges`, `BaseRepository.cs:51-57`): the `FeedRevisionStamper` rule is not endangered by a delete, and the concurrency token on `FeedRevision` guards it (`QuestBoardContext.cs:126`).

### Writes: all through the change tracker

`grep` for `ExecuteUpdate`, `ExecuteDelete`, `FromSql`, `ExecuteSql`, `.Attach(` and hand-set `EntityState` across `QuestBoard.Repository`, `QuestBoard.Domain` and `QuestBoard.Service` finds only the stamper's own reads of `EntityState` (`FeedRevisionStamper.cs`) and one read in `ContactRepository.cs:103`. `[VERIFIED: grep this session]` Per action on these paths:

| Action | Write |
|--------|-------|
| Role changes | tracked `UserGroupEntity` edit or add, `SaveChangesAsync` (`UserRepository.cs:105-120`) |
| CreateUser / AddMember / CreateMember | `UserManager.CreateAsync`, tracked `UserGroups.Add` (+ campaign event signups added, `GroupRepository.cs:53-112`) |
| EditUser | `BaseRepository.UpdateAsync`: `FindAsync`, `Mapper.Map(model, entity)`, `SaveChanges` (`BaseRepository.cs:63-69`); the `User → UserEntity` map ignores `PasswordHash`, `SecurityStamp`, `ConcurrencyStamp` and sets `UserName` from `Email` (`EntityProfile.cs:37-41`) |
| ResetPassword, Disable, Enable, ConfirmEmail | `UserManager` calls |
| DeleteUser / RemoveMember | tracked `UserGroups.Remove` plus `EventSignups.RemoveRange` for that board only (`GroupRepository.cs:114-130`) |
| DeleteQuest | tracked `Remove` (signups, then quest) |
| Group create / edit / delete, linked sets | tracked entity edits and removes; board delete is Phase 102's tracked removal |

Nothing writes `Events` or `Quests` except `DeleteQuest`'s tracked `Remove`; Definition-of-converted point 9 is therefore a statement about not changing that write, which D-12 respects (only the verb and the redirect change).

### Existing coverage of these controllers (what a plan can extend)

| File | Covers |
|------|--------|
| `Controllers/AdminControllerIntegrationTests.cs` (490 lines) | auth gates, `ResetPassword_Post_WhenAdmin_SucceedsForTargetUser` (`:72`), EmailStats gating (`:108-158`), CreateUser, SendConfirmationEmail, `Users_WhenAdmin_DoesNotShowUsersFromOtherGroups`, out-of-group role changes (`:318-355`), `DeleteUser` two facts (`:392-490`, both use `DeleteAsync`) |
| `Controllers/AdminControllerRateLimitIntegrationTests.cs` (225 lines) | the three limiter behaviours, own fixture |
| `Controllers/AdminHandlerIntegrationTests.cs` | `AdminOnly` and `DungeonMasterOnly` by role and SuperAdmin |
| `Controllers/UsersControllerIntegrationTests.cs` | Platform Disable/Enable, self-disable refusal, peer SuperAdmin allowed, missing-token rejection, Boards column (Phase 102) |
| `Controllers/GroupManagementIntegrationTests.cs` (961 lines), `PlatformBoardDeleteTests.cs`, `BoardDeletePageGateRenderTests.cs` | Platform Group actions, member add/remove/create, board delete |
| `Controllers/LinkedBoardsPlatformIntegrationTests.cs` (755 lines) | linked-set actions and both layouts |
| `Security/AntiForgeryTokenCoverageTests.cs`, `BoardPageCoverageTests.cs` | reflection sweeps |
| `UnitTests/Services/UserServiceTests.cs` (13 facts, constructs `new UserService(identityService, repository, mapper, groupService)`), `GroupServiceTests.cs`, `LinkedBoardSetServiceTests.cs`, `ResendStatsClientTests.cs`, `ResendStatsAggregatorTests.cs` | Domain behaviour |

**Not covered anywhere (red tests for the audit plan):** any reach rule, self-demotion, last-Admin, `ResetPasswordAsync` target checks, `DeleteQuest` success, cross-board id and reminder-log case, the EmailStats statistics branch, Platform `RemoveMember` outcome.

## D-02 and D-03: how

### Where "highest role anywhere" lives (answer)

Domain service method backed by one repository query and one Identity read; the controller only renders the outcome. No EF Core in `QuestBoard.Service`.

| Piece | Layer | Shape (proposed names) |
|-------|-------|------------------------|
| SuperAdmin ids | `IIdentityService` (Domain interface, implemented in `QuestBoard.Repository/IdentityService.cs`) | reuse `IsSuperAdminAsync(int)` for single reads; add a batch read of SuperAdmin ids (for example `GetUsersInRoleAsync("SuperAdmin")` ids) for the Users list so a page does not do one role query per row (the list already does one `GetGroupRoleByIdAsync` per row, `AdminController.cs:35`, so either is consistent with today) |
| "Admin on a board other than this one" | `IUserRepository` | one query over `DbContext.UserGroups` (no global filter on that entity; `GetBoardNamesByUserAsync` already reads it unfiltered, `UserRepository.cs:80`): `ug.UserId in ids && ug.GroupId != groupId && ug.GroupRole == (int)GroupRole.Admin`, returning the distinct user ids |
| The rule | `IUserService` | `GetAccountReachAsync(ClaimsPrincipal actor, int groupId, IReadOnlyCollection<int> targetUserIds)` returning, per id, a small outcome (reachable, SuperAdmin account, administers another board). An actor in the SuperAdmin role reaches everyone and costs no query (`IsInRole`, as `GetEffectiveGroupRoleAsync` does) |
| Enforcement | `UserService.ResetPasswordAsync(ClaimsPrincipal adminUser, int groupId, User user, string newPassword)` | add the group id and refuse with `IdentityResult.Failed(...)` before calling `AdminResetPasswordAsync`; the old overload has exactly one caller (`AdminController.cs:328`). Correct the false XML doc on `IIdentityService.AdminResetPasswordAsync` (`:43-47`) |
| Refusal UI | controller + view model | GET and POST of `EditUser` and `ResetPassword` and POST `SendConfirmationEmail` answer an Error toast and a redirect to Users; `UserManagementViewModel` gains per-row `Reach` so the view disables Edit and Resend with a visible reason |

**What `ResetPasswordAsync(User, user, …)` already enforces: nothing about the target.** The principal is only used by `IdentityService` to confirm the actor exists (`IdentityService.cs:69-71`); the target is fetched by id and reset with a fresh token (`:73-78`). A previous regression test notes an inner role check was removed because it was always false (`AdminControllerIntegrationTests.cs:69-72`). The only authorization is the controller's `AdminOnly` policy plus the membership guard.

**Rule reading to confirm at the checkpoint (A1).** D-02's two sentences read together: refuse when the target is a SuperAdmin or an Admin of **another** board; an Admin of only the active board is "no higher" and is reachable. An Admin who administers two boards (including the actor acting on themselves) is "an Admin of another board" and would be refused, so decide whether self is exempt (recommended: self is reachable for name and email, and `ResetPassword` on self goes through the same rule because it skips the current-password check that `ChangePassword` requires; operator question O-3).

### Self and last-Admin guard (D-03)

| Piece | Layer | Shape |
|-------|-------|-------|
| Admin count | `IGroupRepository` (precedent: `HasMembersAsync`, `GroupRepository.cs:49-50`) | `CountMembersWithRoleAsync(groupId, role)` |
| Guarded role change | `IGroupService` (or `IUserService`) | `ChangeMemberRoleAsync(int groupId, int actingUserId, int targetUserId, GroupRole newRole)` returning an outcome (changed, self, last Admin, not a member). Judged on the resulting role (G-03) |
| Guarded removal | `IGroupService` | `RemoveMemberAsync` stays the unguarded chokepoint (its XML doc says Platform and Admin both call it, `GroupService.cs:34-41`); add `TryRemoveMemberAsync(groupId, actingUserId, targetUserId)` that applies the same checks and then calls it. `AdminController.DeleteUser` uses the guarded one; Platform `RemoveMember` keeps the plain one (G-14) |
| Acting id | controller | `userService.GetUserAsync(User)` or `identityService.GetUserIdAsync(User)` |
| Per-row flags | `UserManagementViewModel` | `IsSelf`, `IsLastAdmin` (computed once per page from the Admin count), `Reach`; the view hides or disables the buttons with a visible reason (not a `title`) |

A SuperAdmin acting on a board they do not belong to is never a "self" target; a SuperAdmin who is also a member is treated like anyone (literal D-03). Concurrency residual: two Admins demoting each other in the same instant both pass the count check. A serializable transaction is not an option under EF InMemory (the test provider), the self rule blocks the single-actor case, and a SuperAdmin can restore an Admin from the Platform page; record as accepted (O-7).

## D-10: the EmailStats seam

**Today:** `ResendStatsClient` is a concrete public class (`Services/ResendStatsClient.cs:7`), `FetchAllRecordsAsync` is non-virtual, the controller takes the concrete type (`AdminController.cs:22`), it is registered `AddScoped<ResendStatsClient>()` (`Program.cs:229`) and gets its HTTP client from the named `"Resend"` client whose base address is hard-coded (`Program.cs:223-228`). `appsettings.json` carries an empty `ResendApiKey` (checked: length 0), so the page shows the "not configured" state by default. There is no interface and no virtual member.

**Tests (no production change): replace the named client's primary handler.** Probed this session: with the app's `AddHttpClient("Resend", …)` registered first, a later `services.AddHttpClient("Resend").ConfigurePrimaryHttpMessageHandler(() => stub)` wins and the client answers the stub (`Microsoft.Extensions.Http`, scratch project). In the integration factory this goes in `ConfigureTestServices` (which runs after the app's registrations); set the key with `UseSetting("EmailSettings:ResendApiKey", "<stub>")` (options bind from the `EmailSettings` section, `ServiceExtensions.cs:18`). This runs the real client, the real aggregator and the real view with canned `emails` JSON, and `ResendStatsClientTests` already shows the JSON shape and a `StubHttpMessageHandler` (`ResendStatsClientTests.cs:13-40`). The five-minute `IMemoryCache` entry is per host, so one test per figure set or `?force=true`.

**Dev-only capture (live app, no key, no appsettings edit): needs one small production seam.** A running app calls `https://api.resend.com/` whatever key it is given, and nothing outside the process can intercept that TLS call. Recommended: an optional `EmailSettings:ResendBaseUrl` read where the named client is built (`Program.cs:223-228`), default unchanged; the capture sets `EmailSettings__ResendApiKey=<stub>` and `EmailSettings__ResendBaseUrl=http://127.0.0.1:<port>/` through `run-app.mjs --env` and a scratch Node server answers `GET /emails?limit=100` with canned records. A test pins the default. Alternative if the operator prefers no config knob: extract `IResendStatsClient` and register a Development-only canned implementation; that ships stub code in the production assembly, so it is the second choice. **Recommendation recorded as resolved (see Open Questions, R5); operator can overrule at the checkpoint (O-13).**

## D-12 and D-13: DELETE to POST and native confirms

| Item | Today | Change |
|------|-------|--------|
| `DeleteUser` | `[HttpDelete] [ValidateAntiForgeryToken]` (`AdminController.cs:344-346`), returns `Ok()`/`NotFound()`; fetch from `Users.cshtml:163-180` and `Users.Mobile.cshtml:117-136` with header `RequestVerificationToken`, `alert("Remove from group failed.")` | `[HttpPost]`, `RedirectWithSuccess`/`RedirectWithError` to `Users`; guarded removal (D-03); a `<form asp-action="DeleteUser" asp-route-id=… method="post" data-confirm-message=…>` per row, both layouts |
| `DeleteQuest` | `[HttpDelete]` (`:436-439`), fetch from `Quests.cshtml:142-159` and `Quests.Mobile.cshtml:81-100`, `alert("Delete failed.")` | `[HttpPost]` keeping `[BoardPage(Quests)]`; Success toast; a missing or other-board id gets an Error toast ("That quest no longer exists on this board."), not a 404 |
| Inline fetch scripts | four copies (2 per view pair), four `onclick` buttons | all gone; both views drop `@inject Antiforgery` and `GetAndStoreTokens` (`Users.cshtml:3-8`, `Quests.cshtml:5-9`, `Users.Mobile.cshtml:5-7`, `Quests.Mobile.cshtml:6-8`); the form tag helper adds the token; each layout loads `confirm-submit.js` as `type="module"` from `@section Scripts` (the Platform layouts and `_Layout` both render `Scripts`) |
| Toast wording | none exists | new sentences ("{name} was removed from the group.", "Quest deleted."): list as Gate 1 copy items (D-15) |

**Confirmation sentences to carry verbatim into `data-confirm-message` (read from source):**
- Remove from Group (Admin): `Are you sure you want to remove this user from the group? They will keep their account and any other group memberships.` (`Users.cshtml:165`; see G-18)
- Delete Quest: `Are you sure you want to delete this quest? This action cannot be undone.` (`Quests.cshtml:144`)
- Remove Member (Platform): `Remove this member from the group? Their availability answers for events on this board will be deleted and cannot be recovered.` (`Members.cshtml:77`)
- Disable: `Disable this account? They will be signed out and unable to log in until re-enabled. No data will be deleted.` (`Users/Index.cshtml:96`)
- Delete Set: `Delete the set "{set.Name}"? Every board in it goes back to showing only its own calendar. Boards, quests and events are not deleted.` (`LinkedBoards/Index.cshtml:64`); the Remove Board sentence is built the same way in `Index.Mobile.cshtml:106`. They are now JSON-serialised then HTML-encoded into `onsubmit` (`Index.cshtml:65`); as a `data-confirm-message` attribute Razor's encoding alone is correct and simpler.

**Tests touched by D-12:** `AdminControllerIntegrationTests.cs:412` and `:475` (`DeleteAsync` + `HttpStatusCode.OK` become a form `PostAsync` + redirect), `:55-69` (theory GETs `/Admin/DeleteUser/1`; still a non-success, comment says "requires DELETE"), `BoardPageGateMatrixTests.cs:398` (`HttpMethod.Delete` becomes `Post`; the gate filter is method-agnostic, `BoardPageGateFilter.cs`), `BoardPageCoverageTests.cs:56` (unchanged), `AntiForgeryTokenCoverageTests` (now covers both). `LinkedBoardsPlatformIntegrationTests.cs:540-547` and `:724-731` pin the `onsubmit="return confirm(\"…\");"` shape and must be rewritten to the same intent on the attribute (a name with quotes or script stays inert text; no `onsubmit`).

**Ordering warning for the planner:** changing the verbs breaks the existing fetch buttons at once, so the controller change and the form markup for Remove from Group and Delete Quest (both layouts) land in the same plan; the restyle of those two pages comes later.

## UI conversion facts

### Files, drift and the real counts (re-measured this session)

| Area | Count now | Note |
|------|-----------|------|
| Admin views | 11 (five twin pairs + `EmailStats`) | unchanged |
| Platform `Group` views | 10 + `_BoardDeleteBody.cshtml` partial | Delete pair, partial, `platform-board-delete.js` and three sheets were added by Phase 102 and are already layered and literal-free |
| Platform `Users/Index` | 2 | Phase 102 added the Boards column and `platform-users-boards.mobile.css` |
| Platform `LinkedBoards/Index` | 2 | unchanged |
| Inline census (views) | **1** `style=` (`Quests.cshtml:50`), **0** `<style>`, **4** inline `<script>` (Users and Quests, both layouts), **12** inline handlers (Users 2, Quests 2, Members 2, Platform Users 2, LinkedBoards 4) | matches ROADMAP; the Delete pair's `<script type="module" src=…>` is an external module |
| Sheets in the ROADMAP list | `admin-form.mobile.css` 32 lines, `admin-quests.mobile.css` 40, `admin-users.mobile.css` 40, `platform-group.mobile.css` 98, `platform-users.mobile.css` 46, `platform-linked-boards.mobile.css` 39 | hex 6 (all in `platform-group.mobile.css`), `rgb()` 57, `!important` 29 lines (ROADMAP: 28; counted by line); none is in a layer; `platform-linked-boards.mobile.css` is clean |
| Phase 102 sheets | `platform-group-delete.css`, `platform-group-delete.mobile.css`, `platform-users-boards.mobile.css` | `@layer`, no literals, no `!important`; **not** in `UiFoundationLayerGuardTests.LayeredFiles` (they have their own guard, `BoardDeletionStaticGuardTests.cs:50-58`) |
| `modern-card.css`, `site.css` | **do not exist** | cards, tables, badges, buttons live in `components.css` (917 lines); `legacy-pages.css` is a single rule `.container.mt-4 { max-width: none; }` and stays |

The desktop pages need no page sheet to read correctly today; the six sheets are phone-only. The `.mobile.css` header comments ("No @media queries — exclusively loaded by …") hold.

### `@section Styles` mechanics

Both Platform layouts render `@await RenderSectionAsync("Styles", required: false)` in `<head>` (`_Layout.Platform.cshtml:16`, `_Layout.Platform.Mobile.cshtml:17`) and `Scripts` before `</body>`; the main layouts do the same. Platform desktop links `app, tokens, base, components, shell, legacy-pages`; Platform phone links `app, tokens, base, components, shell, shell.mobile, mobile` (no `legacy-pages`). A page sheet is a `<link rel="stylesheet" href="~/css/….css" asp-append-version="true" />` inside the view's `@section Styles`, wrapped in `@layer pages { }`, scoped (phone sheets under `.mobile-layout .page-class`, as `players.mobile.css` does). Naming precedent: `shop-pages.css` and `shop-pages.mobile.css`. **Recommended consolidation (discretion):** `admin-pages.css` + `admin-pages.mobile.css` and `platform-pages.css` + `platform-pages.mobile.css` (`@layer pages`, tokens only), the six old sheets deleted as their last consumer converts, and each new sheet added to `LayeredFiles` and `NoLiteralFiles` in `UiFoundationLayerGuardTests.cs:51-104`. A partial cannot render `@section Styles`, so the Delete pair keeps linking its own sheets from the page views.

**Converted-page conventions to copy** (read from Phase 98 and 99 views): the page title is an `h1` inside `.modern-card-header` on both layouts, sections are `h2` (`Characters/Index.cshtml:16,27`, `ShopManagement/Index.cshtml:20,41`); phone views use `.modern-card` directly (no `*-card-mobile` wrapper; `Players/Index.Mobile.cshtml`); empty states use `.empty-state`; buttons are filled and by role (`ui-guidelines.md`); forms carry `.form-page` and `.form-actions`. The Admin and Platform desktop titles are `h2` today and the Delete pair's desktop title is `h2` (`Delete.cshtml:14`) against `h1` on its phone twin: align to `h1`.

### Shared fixes already supplied by the foundation (what views must stop fighting)

`components.css:62-65` retunes `.bg-info.text-dark` for the dark info fill; `:66-80` maps status colours to ink on paper table cells; `:516-531` sets one `.badge` (14px, 600, `--qb-on-fill` label, near-black on amber); `:647-649` bolds `.table .email-link`. So A-admin-001 to 004 on desktop are source-fixed; the phone sheets (unlayered, `!important`, literals) still win on their own selectors and are what the conversion removes. The `_BoardDeleteBody.cshtml` badge (`bg-info text-dark`, desktop and phone) is the same shape as A-admin-001 and needs a rendered check. Targets: `--qb-hit-min` is 44px (`tokens.css:170`), the phone shell rule covers controls and the 44px toggler (`shell.css:175-179`), but links in table cells and phone rows are page-owned (WINDOWS entry 24: 43 `a.email-link` at 22px on `/Platform/Users`, waived to this phase).

### Platform layout items (D-17): five of six already in source and pinned

| Item | State in source | Pinned by | Left to do |
|------|-----------------|-----------|------------|
| A-admin-008 / A-shell-037 (320px bar) | `_Layout.Platform.cshtml:21-27`: `navbar-expand-lg`, collapse `#platformNavbar`, toggler `aria-label="Toggle navigation"`; `Styles` section present | `UiFoundationDesktopShellGuardTests` (`Layouts` list with collapse id `platformNavbar`) | Render at 320 on all seven pages with `~/qb-ui-review/phase-94/audit/admin/probe320.mjs` |
| A-admin-014 (nav contrast) | foundation chrome tokens on `.chrome` | `UiFoundationWcagContrastTests` | Re-measure `a.nav-link` and the user name on both layouts at the lightest gradient stop |
| A-shell-038 (same foundation both layouts) | both layouts link `app, tokens, base, components, shell`; phone adds `shell.mobile`, `mobile` | `UiFoundationLayerGuardTests` (`Layouts` includes both Platform layouts) | none in source |
| A-admin-029 layout part | skip link first, `<header>`, `<nav aria-label="Platform">`, `<main id="main-content">`, toggler "Open navigation menu", close "Close navigation menu", footer GitHub link named (`_Footer.cshtml:5`, `_Footer.Mobile.cshtml:5`) | shell guard tests (landmarks, names, mobile footer link) | the Create New User modal's `.btn-close` has no name and the dialog no `aria-labelledby` (`Members.cshtml:185-196`); that is page work, not layout |
| A-admin-030 (toggler size) | `min-width/min-height: var(--qb-hit-min)` (`shell.css:175-179`) | `MobileShell_DeclaresTouchTargetRule` | Measure the rendered toggler (A-admin-030 recorded 56x40) |

So D-17 is a verification slice with a probe per page and layout, expected to find nothing, with a fix slice only if a probe fails. **Not rendered this session (database down): treat as "verify", not "passes".**

### Twin content answers to build (inherited, CONTEXT)

Q-85 Resend form on phone Admin/Users (unconfirmed users with an address; the desktop condition is `!EmailConfirmed && !string.IsNullOrEmpty(Email)`, `Users.cshtml:123-133`); Q-87 shared Campaign status mapping (desktop branches on `boardType` and `IsClosed`, `Quests.cshtml:73-107`; phone lacks it, `Quests.Mobile.cshtml:24-45`; the controller already supplies `ViewBag.BoardType` and `ViewBag.BoardToday`, `AdminController.cs:428-431`; precedent for a shared static mapping: `ShopItemDisplay`, `Service/ViewModels/ShopViewModels/ShopItemDisplay.cs`; existing pins `QuestStatusBoardClockTests.cs:120`); Q-88 "Current Members" on phone Members; Q-92 "Password Management" heading and sentence on phone EditUser; Q-93 user's name in phone ResetPassword heading. D-16 mapping needs: board type ("OneShot" appears in the Group Create and Edit selects, `BoardType` is `OneShot = 0, Campaign = 1`), group role ("DungeonMaster" in Platform Members, "Dungeon Master" in Admin/Users, "Admin" against "Administrator").

### Images

No admin or Platform view or sheet shows a picture: `grep` for `<img`, `background-image`, `url(`, `aspect-ratio` and `object-fit` across the 26 views and the nine sheets finds none. `[VERIFIED: grep this session]` The rule stands for any future addition: a picture meant to show whole never gets `aspect-ratio` together with `object-fit: cover` (Phase 98 lesson). A one-line static guard (no `aspect-ratio` or `object-fit` in the area's sheets, with a scanner self-test) is cheap and mirrors `ShopConversionGuardTests`.

### Guard tests to rewrite to the same intent (current locations; none deleted)

| Test | Where | What it pins now | Rewrite |
|------|-------|------------------|---------|
| `PlatformAreaLayoutTests` | `Controllers/PlatformAreaLayoutTests.cs:1-36` | the Platform layout links `components`, `tokens`, `base`, `app` | Still true; keep, add the new sheets' links per page |
| `LinkedBoardsPlatformIntegrationTests` | `:540-547`, `:724-731` (onsubmit shape), `:598-611` (`platform-group-card-mobile`, `platform-linked-boards.mobile.css`) | handler strings and old class and sheet names | attribute-based fact; new class and sheet names |
| `BoardPageStaticGuardTests` | `Tests/BoardPageStaticGuardTests.cs:133-136` (Platform Group Edit and Index views), `:145` (`platform-group.mobile.css`) | planning-reference scan over the feature files; `FeatureSourceFiles_ReachThePageSelectionFeature` needs at least 50 present | swap the retired sheet for its successor; keep the count and the two named files |
| `MobileViewsTests` Admin entries | `Mobile/MobileViewsTests.cs:1419-1437` (Users), `:1445-1472` (EditUser), `:1480-1499` (Quests), `:1507-1534` (ResetPassword): `admin-*-card-mobile` and the sheet links | new class names and sheet names. There are no Platform entries in this file; the Platform phone facts live in the next rows |
| `QuestStatusBoardClockTests` | `Controllers/QuestStatusBoardClockTests.cs:120` | phone Admin/Quests renders `admin-quests-card-mobile` | new class; keep the board-clock fact |
| `UsersControllerIntegrationTests` | `:306-345` | Platform Users phone links `platform-users-boards.mobile.css` and `platform-users.mobile.css` | new sheet names |
| `PlatformBoardDeleteTests` | `:390-391` | phone Delete must not carry `platform-group-card-mobile` or `platform-group.mobile.css` | keep; names may change |
| `BoardDeletionStaticGuardTests` | `:50-58`, `:220-226` | Phase 102's three sheets and the Delete pair's links | keep untouched unless the sheets are consolidated; if so rewrite, never drop |
| `UiFoundationLayerGuardTests` arrays | `:51-104` | layered files and no-literal files | add the new sheets; remove the six retired names only when deleted |
| `UiFoundationDesktopShellGuardTests`, `UiFoundationMobileShellGuardTests`, `AntiForgeryTokenCoverageTests`, `BoardPageCoverageTests`, `AccountPagesRenderTests`, `MobileCssTests` | reflection and shell pins | no edit expected; run in every plan's quick filter |

New classes (Wave 0 of the first plan that needs them): `AdminPlatformConversionGuardTests` (scope lists, no inline code, links, layer, retired files gone, planning-reference scan, raw-date scan, no `aspect-ratio`/`object-fit`, no markup-from-strings in new modules, every `method="post"` form carries a token, no `onsubmit`/`onclick`), `AdminPlatformRenderTests` (both User-Agents: badges, buttons, empty states, twin answers, status mapping), `AdminAccountReachTests`, `AdminRoleGuardTests`, `AdminQuestDeleteTests`, `EmailStatsRenderTests`, and unit facts in `UserServiceTests` and `GroupServiceTests`.

## Operator and fixture environment

**What phase 99 did, to copy:**
- **Scratch root** `~/qb-ui-review/phase-99/` with `ENVIRONMENT.md`, `tools/` (copy of the previous phase re-pointed through the `QBR` environment variable and the ports; `tools/node_modules` symlinked to `phase-94/tools/node_modules`), `credentials.json` and `migrator.env` mode 600, `fixtures.json`, `manifest.json`, `before/`, `after/`, `gate1/`, `gate2/`, `state/`. `[VERIFIED: ls this session; stat shows 600 on both credentials files]`
- **Email suppression is forced in the launcher, not configured:** `tools/run-app.mjs` starts QuestBoard.Service in Development and always sets `EmailSettings__SuppressSending=true`, `EmailSettings__SmtpServer=127.0.0.1`, `EmailSettings__SmtpPort=1025`; it refuses any `--env` pair that turns suppression off or names another host or port, verifies the listener's `/proc/<pid>/environ` after `/health` answers, never stops a listener it did not start, and writes pid and source tree to `logs/app-<port>.pid` (`run-app.mjs` header, lines 1-50 read). Phase 100's `--env EmailSettings__ResendApiKey=… --env EmailSettings__ResendBaseUrl=…` fits the same mechanism.
- **Ports:** 98 used 8310/8311, 99 used 8320/8321 (working tree and frozen baseline). Probed this session with `ss -ltn`: nothing listens on 1433, 1025, 8025 or any 8xxx port. **Recommend 8340 (working tree) and 8341 (frozen baseline)**, re-checked with `ss -ltnp` (never a zsh `/dev/tcp` probe) when plan 1 runs, so earlier phases' `state/` storage states cannot be picked up.
- **Accounts:** the Phase 94 credentials file holds four roles: `superadmin` (`qb-audit-platform@example.invalid`, D-06), `admin` (`qb-audit-admin@example.invalid`), `player`, `dm`, plus the board "Audit Board" (names only were read; no secret printed). `~/qb-ui-review/phase-94/audit/admin/` holds the probes to reuse (`probe320.mjs`, `probe-modal.mjs`, `probe-layout.mjs`, `probe-cost.mjs`, `probe-spacing.mjs`, `targets.py`, `fails.py`).
- **Fixtures through the app's forms:** `register99.mjs`, `fixlib.mjs` and `fixtures.mjs` are the models; Admin/CreateUser creates accounts (welcome emails are suppressed), Platform Members creates and adds, Platform Create makes boards and sets. A second-board Admin for D-02 needs a p100 account that is Admin of two p100 boards. SuperAdmin cannot be granted from any form (D-06), so a "SuperAdmin target" for the live reach proof is the reused audit SuperAdmin, added to a p100 board as a member by the SuperAdmin's own session.
- **Mailpit:** `qb-mailpit` does not exist now (`docker ps -a` lists `postgres-dev`, `mssql-dev` (Exited 137, 12 hours ago), `portainer`, `ddb-scraper`). Starting it is pre-approved (D-11) and only needed to read a message (the welcome resend).
- **Gate builders:** `build-gate1.py`, `build-gate2.py`, `gate-hygiene.py`, `lookcheck.mjs`, `look-check-98.mjs`, `build-look-pairs.py` exist; Gate 1 references `~/qb-ui-review/phase-94/mockups/direction-a/png/quest-form-{1440,820,390}.png` and `quest-form--edit-*` `[VERIFIED: ls]`. D-09: Admin/Users, Platform Users, Members and Group/Index list real members; captures stay local and reports never quote a row.
- **Never:** start, stop or provision SQL Server or any container other than `qb-mailpit`; edit `appsettings.json` or user-secrets; print a password or connection string; push, tag or open a PR (the orchestrator pushes the milestone branch after each green wave and the operator opens the PR, per project memory).

**Capture BEFORE first.** BEFORE must be the current unconverted look (Q-126 has no populated BEFORE). Capture it from a frozen copy (`git archive` at the start, as the phase-99 baseline did) before the first server fix lands, so later D-12 form buttons do not alter it.

## Tracking commands (D-21)

From phase 99 (`99-01-PLAN.md` action, `99-24-PLAN.md:598-601`, `99-RESEARCH.md` Priority 6) `[VERIFIED: files read; `node "$GSD" query roadmap.get-phase 100` ran and returned the section]`. Run from the repository root, in this checkout, never inside a worktree (`execute-plan.md` skips STATE and ROADMAP updates in a worktree; `workflow.use_worktrees` is `true`, which is why phases 97 and 98 plans 1 to 12 left both files stale):

```bash
GSD="$HOME/.claude/gsd-core/bin/gsd-tools.cjs"
# first plan only: STATE still says current_phase 99
if [ "$(grep -c '^current_phase: 100' .planning/STATE.md)" = "0" ]; then
  node "$GSD" query state.begin-phase --phase 100 --name "Admin and Platform UI conversion" --plans <N>
fi
# every plan, after its SUMMARY exists:
node "$GSD" query state.advance-plan
node "$GSD" query state.update-progress
node "$GSD" query state.record-session --stopped-at "Completed 100-NN-PLAN.md" --resume-file "None"
node "$GSD" query roadmap.update-plan-progress 100
node "$GSD" query commit "docs(100-NN): complete <plan name> plan" \
  --files .planning/phases/100-admin-and-platform-ui-conversion/100-NN-SUMMARY.md .planning/STATE.md .planning/ROADMAP.md
git show --name-only --format= HEAD | grep -c -E '(^|/)(STATE|ROADMAP)\.md$'   # expect 2
```

`roadmap.update-plan-progress` counts PLAN against SUMMARY files on disk, so the planner must first replace "**Plans:** 0 plans" and "- [ ] TBD" under Phase 100 in ROADMAP.md with the full plan list (checkbox lines `- [ ] 100-NN-PLAN.md — …`, grouped under `**Wave n**`, as Phase 99's section does). STATE's free-text "Current Position" and "Last activity" lines are not touched by these commands; the closing plan patches them by hand (phase 98 follow-up `be2e61d2`). `state.add-decision` per notable decision when a SUMMARY records one.

## Architecture Patterns

### System Architecture Diagram

```
 Admin page (board Admin, or SuperAdmin) -- browser form POST + antiforgery token
   /Admin/{PromoteToAdmin|DemoteFromAdmin|PromoteToDM|DemoteToPlayer|DeleteUser|SendConfirmationEmail|EditUser|ResetPassword|CreateUser|DeleteQuest}
        |
        v  [AdminOnly policy: SuperAdmin role OR UserGroups.Role == Admin on the active board]
   GroupSessionMiddleware (non-GET: membership of the active board re-verified)
        |
        v  AdminController action
   scope check ........ GetGroupRoleByIdAsync(target, activeGroup) != null   (kept)
   NEW reach check .... IUserService.GetAccountReachAsync(actor, group, ids)      -> refuse = Error toast + redirect
   NEW role guard ..... IGroupService.ChangeMemberRoleAsync / TryRemoveMemberAsync -> refuse = Error toast + redirect
        |
        v  Domain services -> repositories -> QuestBoardContext (change tracker, global filters)
        |      Identity UserManager (password reset, email change token, SuperAdmin role read)
        |      Hangfire jobs (welcome, change-email): SMTP 127.0.0.1:1025 in review runs
        v
   PRG redirect -> /Admin/Users or /Admin/Quests, toast via TempData (Success / Error / Warning)

 Platform page (SuperAdmin only, /platform/..., exempt from the active-board middleware)
   Group {Create|Edit|Delete|AddMember|CreateMember|RemoveMember}, Users {Disable|Enable},
   LinkedBoards {Create|AddBoard|Rename|RemoveBoard|Delete}
        -> services (validate, outcome enums) -> repositories -> tracked writes -> TempData toast -> redirect

 Phone UA -> _Layout.Mobile / _Layout.Platform.Mobile (+ view .Mobile twin + page .mobile.css via @section Styles)
 EmailStats (SuperAdminOnly) -> memory cache (5 min) -> ResendStatsClient -> named "Resend" HttpClient
        -> real API  |  test: stub handler  |  dev capture: ResendBaseUrl -> scratch stub server
```

### Recommended Project Structure

```
QuestBoard.Domain/
├── Interfaces/IUserService.cs        # + GetAccountReachAsync; ResetPasswordAsync gains the group id
├── Interfaces/IGroupService.cs       # + ChangeMemberRoleAsync, TryRemoveMemberAsync
├── Interfaces/IUserRepository.cs     # + admin-on-other-boards id query
├── Interfaces/IGroupRepository.cs    # + CountMembersWithRoleAsync
└── Services/{UserService,GroupService,QuestService}.cs
QuestBoard.Repository/                # EF only here
QuestBoard.Service/
├── Controllers/Admin/AdminController.cs      # POST verbs, toasts, refusals
├── ViewModels/AdminViewModels/               # per-row Reach / IsSelf / IsLastAdmin
├── ViewModels/…/AdminPlatformDisplay.cs      # one mapping: role, board type, quest status (as ShopItemDisplay)
├── Views/Admin/*, Areas/Platform/Views/*     # converted views, no inline code
└── wwwroot/css/{admin-pages,admin-pages.mobile,platform-pages,platform-pages.mobile}.css   # @layer pages
```

### Pattern 1: Refusal as a toast, control disabled with a visible reason
**What:** the server answers a refused action with `RedirectWithError(nameof(Users), reason)`; the row renders the control disabled and a one-line reason linked by `aria-describedby`, the same shape as the move control (`.move-control-reason`, `Quest/Manage.cshtml:709`). A disabled `<button>` is not focusable, so the reason must be text next to it, not a `title` (A-admin-042).
**When:** D-02 rows (Edit, Resend Welcome Email), D-03 rows (role buttons, Remove from Group), Platform Users own-account Disable.

### Pattern 2: Guard judged on the resulting state
**What:** compute the outcome of the whole change (new role, remaining Admin count), never trust the action name or the rendered button.
```csharp
// Domain: shape only; names are proposals, the rule is G-03
public async Task<MemberChangeOutcome> ChangeMemberRoleAsync(
    int groupId, int actingUserId, int targetUserId, GroupRole newRole, CancellationToken token = default)
{
    var current = await userRepository.GetGroupRoleAsync(targetUserId, groupId);
    if (current is null) return MemberChangeOutcome.NotAMember;
    if (targetUserId == actingUserId && newRole != current) return MemberChangeOutcome.Self;
    if (current == GroupRole.Admin && newRole != GroupRole.Admin
        && await repository.CountMembersWithRoleAsync(groupId, GroupRole.Admin, token) <= 1)
        return MemberChangeOutcome.LastAdmin;
    await userRepository.SetGroupRoleAsync(targetUserId, groupId, newRole);
    return MemberChangeOutcome.Changed;
}
```

### Pattern 3: Native confirm through the shared module (D-13)
```cshtml
@* Users.cshtml, one form per row; no onclick, no fetch, no token code *@
<form asp-action="DeleteUser" asp-route-id="@userModel.User.Id" method="post"
      data-confirm-message="Are you sure you want to remove this user from the group? They will keep their account and any other group memberships.">
    <button type="submit" class="btn btn-danger btn-sm"><i class="fas fa-user-minus me-2" aria-hidden="true"></i>Remove from Group</button>
</form>
@section Scripts { <script type="module" src="~/js/confirm-submit.js" asp-append-version="true"></script> }
```
Source: the pattern is `Views/Shop/Index.cshtml:379` and `Views/ContactCategoryManagement/Manage.cshtml:111`; `confirm-submit.js` binds forms present at load.

### Pattern 4: EmailStats test stub without production change
```csharp
// In the integration factory's ConfigureTestServices (probed pattern):
services.AddHttpClient("Resend").ConfigurePrimaryHttpMessageHandler(() => new CannedResendHandler(json));
// and builder.UseSetting("EmailSettings:ResendApiKey", "<stub>") so the key is non-blank
```

### Anti-Patterns to Avoid
- **Putting the last-Admin check inside `RemoveMemberAsync`:** it is the chokepoint the Platform page also uses, and board deletion needs it to empty a board.
- **Reading SuperAdmin from `UserGroups`:** it is an Identity role; a SuperAdmin may have any membership row or none.
- **Judging the guard by the button the page offered:** `PromoteToDM` can demote (G-03).
- **Taking the rate-limit lease before the checks:** burns another account's budget (G-06).
- **A new unlayered sheet or a layered rule on a selector an unlayered phone sheet still styles:** unlayered rules win on their own selectors (`ui-guidelines.md` "Legacy page sheets are unlayered"); delete the old phone rules in the same commit that converts the view.
- **Planning references in source:** no decision, finding or plan ids, no planning document names in code, CSS, comments or test names (CLAUDE.md; `BoardPageStaticGuardTests` scans).

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Confirmation dialogs | `onclick="return confirm(...)"` or a fetch | `confirm-submit.js` + `data-confirm-message` | Phase 95, 98, 99 pattern; no markup from strings |
| Toast after an action | toast markup or `alert()` | `RedirectWithSuccess/Error/Warning` or `TempData["Success"|"Error"|"Warning"|"Info"]` | Shared `_Toasts` partial renders every layout |
| Is this account a SuperAdmin | a flag, a claim copy or a membership role | `IIdentityService.IsSuperAdminAsync` / `IsInRole("SuperAdmin")` | Already the model; used by `QuestMoveService` |
| Reminder-log and signup cleanup on quest delete | raw SQL or `ExecuteDelete` | tracked `RemoveRange` through the repository, then `SaveChanges` | The calendar feed guard test forbids bulk and raw writes on Events and Quests |
| Rate limiting | a new limiter | the existing singleton `PartitionedRateLimiter<int>` | Already per target id |
| Role, board-type and status labels | per-view `if` chains | one static mapping (as `ShopItemDisplay`) | Twin drift is the finding (A-admin-016, 044) |
| Empty state, form layout, refusal line, visible reason | bespoke classes | `.empty-state`, `.form-page`, `.form-actions`, `.refusal-line`, a neutral reason class | Foundation components |
| Dates | `.ToString(...)` | `Html.LocalTime` (Group/Index Created) | `ui-guidelines.md` |
| Stub for the Resend API in tests | an interface or a virtual member | a replacement primary handler on the named client | Proven this session; zero production change |

**Key insight:** every rule this phase adds is a small function over data that already exists (UserGroups rows, the SuperAdmin role, the Admin count). The risk is placing it in the wrong layer or at the wrong moment (before the scope check, or inside a shared chokepoint), not computing it.

## Runtime State Inventory

Not a rename or migration phase. Omitted. The only stored-state change is behavioural: D-12 changes the verb of two endpoints and adds no stored data. Fixtures are real rows in the local database created through the app's forms and kept afterwards (D-07); no migration is expected (reminder-log cleanup and the guards use existing columns).

## Common Pitfalls

### Pitfall 1: The reach rule leaks through add-by-email
**What goes wrong:** the rule refuses a SuperAdmin or foreign Admin, but an ordinary account on another board can still be added by email and then reset (G-02).
**Why:** membership of the active board is the only precondition and `CreateUser` creates membership.
**How to avoid:** put O-1 to the operator; whatever is decided, test the chain end to end (add by email, then ResetPassword) for both an elevated and an ordinary target.
**Warning signs:** a reach test that seeds the target as a member directly and never goes through `CreateUser`.

### Pitfall 2: Fixing D-12 view-side before the server verb (or the reverse)
**What goes wrong:** buttons POST to a `[HttpDelete]` action (405) or fetch DELETE to a POST action.
**How to avoid:** one plan changes controller, both layouts' forms and the touched tests together (see D-12 ordering warning).

### Pitfall 3: Quest delete passes every test and fails on SQL Server
**What goes wrong:** EF InMemory does not enforce foreign keys, and the SQL-backed suites are skipped unless `QUESTBOARD_MIGRATOR_TEST_REQUIRED=1`, so the reminder-log defect (G-05) is invisible in the normal run.
**How to avoid:** a unit test on the repository level that asserts the reminder logs are marked for removal (tracked entries in `Deleted` state), plus one SQL-backed fact in the closing sweep if the operator says yes; `BoardDeletionSqlServerTests.cs` is the template.

### Pitfall 4: Disabled button is not focusable, so a `title` reason is invisible
**What goes wrong:** keyboard and touch users never get the reason (A-admin-042).
**How to avoid:** visible text beside the control, `aria-describedby` on the control, the reason also on the Error toast the server sends for a crafted request.

### Pitfall 5: Unlayered phone sheets beat the layered components
**What goes wrong:** converting a phone view but leaving its old sheet linked keeps `!important` and literals winning, and the contrast failures survive.
**How to avoid:** delete each old sheet's rules in the commit that converts its last consumer; note `platform-group.mobile.css` serves five views and `platform-linked-boards.mobile.css` one that also links `platform-group.mobile.css`.

### Pitfall 6: Modal errors hidden in a closed modal
**What goes wrong:** a failed Create New User re-renders the page with the modal closed (G-15).
**How to avoid:** decided with O-12; either reopen on load from a data attribute in a module or redirect with a toast; add a render fact that the refusal text is visible.

### Pitfall 7: Measuring before the stub exists
**What goes wrong:** the EmailStats tiles never render in a normal run, so contrast is "read from source" again.
**How to avoid:** the dev capture needs the `ResendBaseUrl` seam in place and a scratch stub server running before the sweep; the integration stub proves markup, not contrast.

### Pitfall 8: `.mobile-layout` follows the User-Agent
Phone checks run under `devices['Pixel 7']` with touch; a narrow desktop window is not a phone.

### Pitfall 9: Racing the shared build output
Stop any running app before `dotnet build` or `dotnet test` (shared `bin/obj`); run unit and integration projects separately (CLR flake `0x80131506`: re-run once; never set GC variables).

## Code Examples

### Reach outcome in the controller (shape)
```csharp
// AdminController.EditUser POST, after the scope check and before any write
var reach = (await userService.GetAccountReachAsync(User, groupId.Value, [model.Id]))[model.Id];
if (reach != AccountReach.Reachable)
    return this.RedirectWithError(nameof(Users), AccountReachCopy.Refusal(reach));
```
Source: shape proposed here; the existing guard pattern is `AdminController.cs:229-239`.

### The existing admin overload that has no target check
```csharp
// QuestBoard.Domain/Services/UserService.cs:107-110
public async Task<IdentityResult> ResetPasswordAsync(ClaimsPrincipal adminUser, User user, string newPassword)
{
    return await identityService.AdminResetPasswordAsync(adminUser, user.Id, newPassword);
}
```

### Guard-test shape for a retired sheet (from Phase 99 pattern)
```csharp
// every listed sheet must exist, open its layer and hold no literal or !important; a deleted sheet leaves the list on purpose
LayeredFiles: ("admin-pages.css", "pages"), ("admin-pages.mobile.css", "pages"), ("platform-pages.css", "pages"), ("platform-pages.mobile.css", "pages")
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| `modern-card.css` + `site.css` with `!important` and bare `span` colour rules | One layered `components.css` (`@layer components`), tokens, `[data-theme]` | Phase 94 foundation | Desktop admin pages already read; phone sheets are the remaining fight |
| Platform bar with no collapse, no landmarks | Collapse and toggler, skip link, landmarks, offcanvas names, 44px toggler | Phase 94 foundation | D-17 becomes verification |
| Fetch DELETE plus `alert()` | POST form, PRG, toast, `confirm-submit.js` | Phases 95 to 99 elsewhere; D-12 here | Works without script state, covered by the CSRF reflection sweep |
| Inline `onsubmit` with Razor-built JS strings | `data-confirm-message` | Phase 98 and 99 | Razor encoding is enough; no JSON-in-attribute |
| One-line group delete | Name-confirmed delete with preview, partial and module | Phase 102 | The Delete pair is largely converted; verify only |

**Deprecated or outdated (to delete in this phase):** the six phone sheets above, the nine duplicate glass cards (A-admin-038), `.table` double definition (already gone with `modern-card.css`), the four inline fetch scripts, the twelve inline handlers, `Quests.cshtml:50` inline `style=`.

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|---------------|
| A1 | D-02 means: refuse a SuperAdmin or an Admin of a *different* board; an Admin of only the active board is reachable; self is reachable (for name and email) | D-02 and D-03: how | A peer Admin is refused, or self-service reset is allowed that the operator wanted refused; confirm at O-3 |
| A2 | The takeover chain G-01 works end to end (EditUser email to an attacker address, anonymous `ConfirmEmailChange`, Forgot Password) | Gap table G-01 | Each hop was read; none was run. A live proof on p100 fixtures (D-08) is part of the audit plan |
| A3 | `QuestService.RemoveAsync` fails on SQL Server for a quest with reminder-log rows or an other-board follow-up | G-05 | If a cascade exists in the live schema the defect is smaller; the migration snapshot says `NoAction` (read). Confirm with the SQL-backed suite or on a p100 quest after a reminder is sent |
| A4 | The Platform layout items pass when rendered (320px bar, nav contrast, 44px toggler) | Platform layout items | Source and guard tests say yes; no render this session (DB down) |
| A5 | Two Admins demoting each other in the same instant is rare enough to accept (O-7) | D-02 and D-03: how | A board ends with no Admin until a SuperAdmin adds one |
| A6 | Ports 8340 and 8341 are free when plan 1 runs | Operator and fixture environment | Pick another pair; plan 1 re-checks |
| A7 | Screen-reader behaviour of the disabled-with-reason rows and the reopened modal | Pitfalls 4 and 6 | Caught by the phase UAT, not by automation |

## Open Questions (RESOLVED)

1. **Is `DeleteQuest` limited to the active board?** RESOLVED: yes. Controller → `BaseRepository.GetByIdAsync` (`FindAsync`) → fail-closed `QuestEntity` filter, plus a filtered re-read in `QuestService.RemoveAsync`; probed with EF InMemory 10.0.9. A cross-board id answers 404 today. See "DeleteQuest verdict".
2. **Where should "highest role anywhere" live?** RESOLVED: Domain `IUserService` method over one `IUserRepository` query and the Identity SuperAdmin read; enforcement inside `UserService.ResetPasswordAsync` as well as in the controller.
3. **What does `ResetPasswordAsync(User, user, …)` enforce?** RESOLVED: nothing about the target and, in the implementation, nothing about the acting role (only that the actor exists). `IdentityService.cs:67-79`.
4. **How is SuperAdmin modelled?** RESOLVED: Identity role "SuperAdmin"; `IsSuperAdminAsync(int)` exists; `GetEffectiveGroupRoleAsync` maps it to Admin.
5. **Which EmailStats seam?** RESOLVED (recommendation): tests replace the named `Resend` handler (probed); the live capture uses an optional `EmailSettings:ResendBaseUrl` (default unchanged) with a scratch stub server. Operator may overrule (O-13).
6. **Does a crafted undefined `GroupRole` reach the service?** RESOLVED: no; MVC rejects it (probed).
7. **Are the Platform layout items still open?** RESOLVED: five of six are already in source and pinned; render verification only (A4).
8. **Do any admin or Platform pages show a picture?** RESOLVED: no.
9. **Do all writes use the change tracker?** RESOLVED: yes (grep and per-action table).
10. **Is the ROADMAP scope current?** RESOLVED: no; Phase 102 added a partial, a module and three sheets, and the foundation removed `modern-card.css` and `site.css`. The plans use the counts in "Files, drift and the real counts".
11. **Does D-03 apply to the Platform Members page?** RESOLVED as a recommendation (exempt, G-14); operator confirms at O-2.

### Operator-owned questions for the D-05 checkpoint

D-02 and D-03 are decided and are not re-asked; these are the points around them and the other gaps.

| # | Question | Recommendation |
|---|----------|----------------|
| O-1 | G-02: an ordinary account (Player or Dungeon Master) on another board can still be added by email and then reset by this board's Admin. Accept as D-02 stands, or also refuse edit and reset for any account that holds any role on another board? | Accept as decided; the add emails the owner. Record the residual |
| O-2 | G-14: the last-Admin guard applies to the Admin page only, not to the SuperAdmin's Platform Members removal (needed to empty a board before deleting it). Confirm | Exempt |
| O-3 | A1: peer Admin of the same board reachable; self reachable for name and email; self `ResetPassword` allowed or sent to the account page (it skips the current-password check)? | Peer and self reachable; self reset allowed |
| O-4 | G-08: replace the bare 429 page with an Error toast and move the limiter check before any write (the 429 status contract in tests changes)? | Yes |
| O-5 | G-09: add a per-actor limit on `CreateUser`, or accept? | Add a generous limit (for example 20 per hour per actor) |
| O-6 | G-17: `EmailStats` Refresh stays a GET link, or becomes a POST? | Stay GET (SuperAdmin only, idempotent) |
| O-7 | A5: accept the simultaneous mutual-demotion race | Accept |
| O-8 | G-12: owner notification on admin reset and admin email change is a new email, deferred to the email phase | Defer |
| O-9 | Wording of the D-02 refusal reason: generic ("only a platform administrator can change this account") or naming why? Naming discloses another board's role | Generic |
| O-10 | G-18 and G-19 copy: add the availability-answers sentence to the Admin Remove from Group confirm; align the reset-password minimum to 8 | Yes to both, as Gate 1 items |
| O-11 | G-05: approve deleting reminder logs and unlinking an other-board follow-up as part of quest delete | Yes |
| O-12 | G-15: reopen the modal on errors, or redirect with a toast | Toast redirect (same as Add Member) |
| O-13 | D-10 seam: `ResendBaseUrl` setting (recommended) or an interface with a Development stub | Setting |

## Environment Availability

| Dependency | Required By | Available | Version | Fallback |
|------------|------------|-----------|---------|----------|
| SQL Server `localhost:1433` | Fixtures, BEFORE and AFTER captures, live guard proofs, SQL-backed tests | **No** (`ss -ltn` shows no listener; `mssql-dev` "Exited (137) 12 hours ago") | — | None for captures: the operator starts it at plan 1 (D-20). The agent must not start or provision it. Unit and integration tests use EF InMemory and need no database |
| .NET SDK | Build and tests | Yes | 10.0.115 | — |
| Node | Playwright tools | Yes | v24.19.0 | — |
| Python | Gate builders, scorers | Yes | 3.14.4 | — |
| Playwright + Chromium | Captures, probes | Yes | 1.64.1, chromium headless shell present | — |
| Ports 8340, 8341 | Working tree and baseline | Free at research time | — | any free pair |
| Mailpit `qb-mailpit` (127.0.0.1:1025, 8025) | Reading the welcome resend | No container exists | — | Not needed unless a message must be read; starting it is pre-approved |
| `senior-frontend` skill | D-19 front-end work | Yes (listed) | — | — |
| `gsd-tools` | Docs commits | Yes (`~/.claude/gsd-core/bin/gsd-tools.cjs`) | — | — |
| Phase 94 tools, Direction A renders, Phase 99 tool set | Capture, gates, look check | Yes (238 tool files; `quest-form-*.png`) | — | — |

**Missing dependencies with no fallback:** SQL Server at 1433 (operator).
**Missing dependencies with fallback:** Mailpit (not needed unless a message must be read).

## Validation Architecture

### Test Framework
| Property | Value |
|----------|-------|
| Framework | xUnit v3 + FluentAssertions; `Microsoft.AspNetCore.Mvc.Testing`; EF Core InMemory (no database) |
| Config file | `QuestBoard.IntegrationTests/xunit.runner.json` |
| Quick run (integration) | `dotnet test QuestBoard.IntegrationTests --filter "FullyQualifiedName~AdminPlatform\|FullyQualifiedName~AdminAccountReach\|FullyQualifiedName~AdminRoleGuard\|FullyQualifiedName~AdminQuestDelete\|FullyQualifiedName~EmailStats\|FullyQualifiedName~AdminControllerIntegrationTests\|FullyQualifiedName~AdminControllerRateLimitIntegrationTests\|FullyQualifiedName~AdminHandlerIntegrationTests\|FullyQualifiedName~UsersControllerIntegrationTests\|FullyQualifiedName~GroupManagementIntegrationTests\|FullyQualifiedName~LinkedBoardsPlatformIntegrationTests\|FullyQualifiedName~Platform\|FullyQualifiedName~BoardDeletion\|FullyQualifiedName~MobileViewsTests\|FullyQualifiedName~MobileCssTests\|FullyQualifiedName~QuestStatusBoardClockTests\|FullyQualifiedName~BoardPageStaticGuardTests\|FullyQualifiedName~BoardPageGateMatrixTests\|FullyQualifiedName~BoardPageCoverageTests\|FullyQualifiedName~AntiForgeryTokenCoverageTests\|FullyQualifiedName~UiFoundation\|FullyQualifiedName~KeyboardScriptGuardTests\|FullyQualifiedName~QuestsConversionGuardTests"` |
| Quick run (unit) | `dotnet test QuestBoard.UnitTests --filter "FullyQualifiedName~UserService\|FullyQualifiedName~GroupService\|FullyQualifiedName~QuestService\|FullyQualifiedName~ResendStats\|FullyQualifiedName~LinkedBoardSetService\|FullyQualifiedName~AmbientClockSeamTests"` |
| Full suite | `dotnet test QuestBoard.UnitTests` then `dotnet test QuestBoard.IntegrationTests` (separately, avoids the CLR flake). SQL-backed tests need `QUESTBOARD_MIGRATOR_TEST_REQUIRED=1` and SQL at 1433; once at the end, on the operator's yes |
| Browser checks | Scratch Playwright under `~/qb-ui-review/phase-100/` (desktop 1440x900; phone `devices['Pixel 7']` with touch); probes copied from `phase-94/audit/admin/` |
| Estimated runtime | about 20 s quick after build, 2 to 3 minutes full |

Notes: re-run once on `0x80131506`; never set GC environment variables; stop the running app before build or test; stage files explicitly; no watch mode.

### Phase Requirements to Test Map

| Req | Behavior | Test Type | Automated Command | File Exists? |
|-----|----------|-----------|-------------------|-------------|
| D-01 | Every gap has a reproducing fact (red before its fix, green after) and a row in the security document | integration + unit | `~AdminAccountReach`, `~AdminRoleGuard`, `~AdminQuestDelete` | ❌ Wave 0 |
| D-02 | Board Admin refused (Error toast, nothing changed) for EditUser GET/POST, ResetPassword GET/POST, SendConfirmationEmail on a SuperAdmin and on an Admin of another board; allowed for Player, DM, same-board Admin; SuperAdmin acts on anyone; chain through `CreateUser` by email; `UserService.ResetPasswordAsync` refuses at service level; rows render disabled with a visible reason | unit + integration + render | `~UserServiceTests`, `~AdminAccountReach`, `~AdminPlatformRenderTests` | ❌ Wave 0 |
| D-03 | Self demotion and self removal refused; last-Admin demotion (all four role actions, including a crafted `PromoteToDM` on an Admin) and removal refused; each outcome an Error toast; non-last-Admin and non-self paths still work; Platform `RemoveMember` unaffected; buttons hidden or disabled with a visible reason | unit + integration + render | `~GroupServiceTests`, `~AdminRoleGuard`, `~GroupManagementIntegrationTests` | ❌ Wave 0 |
| D-04 | Deferred rows exist in `deferred-items.md` with severity | process | review | ❌ |
| D-05 | Checkpoint answers recorded; security document registers each gap with evidence | process + doc | `100-SECURITY.md`, `audit-answers.json` | ❌ |
| D-06 to D-09, D-11, D-19, D-20 | Fixtures through forms, no existing row changed, email suppression verified in `/proc` environ, local-only captures, SQL confirmed by the operator | manual / scratch scripts | `fixtures.mjs --check`, `run-app.mjs` | manual-run |
| D-10 | EmailStats statistics branch renders canned figures (four tiles, "as of" line), the error and missing-key states, force refresh bypasses cache; `ResendBaseUrl` default unchanged | integration | `~EmailStatsRenderTests`, `~ResendStatsClientTests` | ❌ Wave 0 |
| D-12 | `DeleteUser` and `DeleteQuest` are `[HttpPost]` with the token attribute, both layouts render a form (no fetch, no `onclick`), PRG with Success and Error toasts, cross-board quest id deletes nothing and toasts, reminder-log and follow-up cleanup, antiforgery sweep covers them | integration + static | `~AdminControllerIntegrationTests`, `~AdminQuestDelete`, `~AntiForgeryTokenCoverageTests`, `~BoardPageGateMatrixTests` | ✅ rewrite + ❌ new |
| D-13 | Each of the six confirmations is a `data-confirm-message` attribute with today's sentence, HTML-encoded; no `onsubmit`/`onclick` in the area; `confirm-submit.js` module loaded | static + render | `~AdminPlatformConversionGuardTests`, `~LinkedBoardsPlatformIntegrationTests` | ✅ rewrite + ❌ new |
| D-14, D-15, D-18 | Gate 1 and Gate 2 packages approved by the operator | manual | `~/qb-ui-review/phase-100/gate1/`, `gate2/` | manual-only |
| D-16 | One mapping for role, board type and quest status used by both layouts; no raw enum text in the area's views | static + render | `~AdminPlatformConversionGuardTests`, `~AdminPlatformRenderTests` | ❌ Wave 0 |
| D-17 | Platform layouts: landmarks, names, toggler, no horizontal scroll at 320, nav contrast, 44px toggler, on all seven pages | static (exists) + Playwright | `~UiFoundationDesktopShellGuardTests`, `~UiFoundationMobileShellGuardTests`; scratch `probe320.mjs`, `contrast.mjs` | ✅ static / manual-run |
| D-21 | Plans' docs commits carry STATE and ROADMAP | process | `git show --name-only --format= HEAD \| grep -c -E '(^|/)(STATE\|ROADMAP)\.md$'` = 2 | process |
| Twin answers Q-85, 87, 88, 92, 93 | Phone Users has the Resend form for unconfirmed users with an address; phone Quests shows Open and Closed for Campaign boards through the shared mapping; phone Members has "Current Members"; phone EditUser has "Password Management" and its sentence; phone ResetPassword heading names the user | integration | `~AdminPlatformRenderTests`, `~MobileViewsTests` | ❌ Wave 0 |
| Relayouts Q-124, 125, 126 | After Gate 1: one page title in the card header (Create and Edit user), Members stacked full width, Users with Email under Name; every field, column, form and button still present | integration | `~AdminPlatformRenderTests` | ❌ (after Gate 1) |
| Definition of converted 1 to 3, 8 | No inline `style`, `<style>`, `<script>` or handler; no hex, `rgb()`, `!important`; sheets in `@layer pages` linked through `@section Styles`; guards rewritten, none deleted | static | `~AdminPlatformConversionGuardTests`, `~UiFoundationLayerGuardTests` | ❌ Wave 0 |
| Definition of converted 5, 7 | A-1 to A-11 bar, targets (24 desktop, 44 phone), contrast incl. EmailStats tiles measured with the stub | Playwright | scratch `sweep.mjs`, `contrast.mjs`, `a11y-*` | manual-run |
| Definition of converted 9 | Helpers untouched; calendar feed files untouched; `DeleteQuest` write stays a tracked remove | unit + diff | `~WallClockMarkupTests`, `~LocalTimeMarkupTests`, `~CalendarSubscriptionStaticGuardTests`; `git diff --stat` | ✅ |
| Images | No `aspect-ratio` or `object-fit` in the area's sheets, scanner self-test | static | `~AdminPlatformConversionGuardTests` | ❌ Wave 0 |
| Definition of converted 10 | Every page works | full suites + sweep | both full suites | ✅ |

### Sampling Rate
- **Per task commit:** the quick filters narrowed to the slice's files; for UI slices the scratch probe on the touched pages under both User-Agents.
- **Per wave merge:** both full suites, per project.
- **Phase gate:** full suites green, `dotnet build` clean, AFTER sweep shows no new contrast failure and no hidden control against BEFORE; `git diff --stat` shows no change to `HtmlHelperExtensions.cs`, `CalendarFeedWriter.cs`, `CalendarSubscriptionService.cs`, `FeedRevisionStamper.cs`; no bulk, raw or hand-set write.
- **Max feedback latency:** 120 seconds.

### Wave 0 Gaps
- [ ] `QuestBoard.IntegrationTests/Tests/AdminPlatformConversionGuardTests.cs` (clone of `ShopConversionGuardTests`)
- [ ] `QuestBoard.IntegrationTests/Tests/AdminPlatformRenderTests.cs` (both User-Agents, seeded boards and members)
- [ ] `QuestBoard.IntegrationTests/Controllers/AdminAccountReachTests.cs`, `AdminRoleGuardTests.cs`, `AdminQuestDeleteTests.cs`, `EmailStatsRenderTests.cs` (stub handler in the factory)
- [ ] Facts in `QuestBoard.UnitTests/Services/UserServiceTests.cs`, `GroupServiceTests.cs`, `QuestServiceTests.cs`
- [ ] `~/qb-ui-review/phase-100/tools` and `ENVIRONMENT.md` re-pointed from phase 99
- Framework install: none

### Manual-Only Verifications
| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| Gate 1 and Gate 2 approvals; each contrast or target exception accepted or rejected | D-14, D-15, D-18 | Operator taste gate | local packages under `~/qb-ui-review/phase-100/` |
| Live proof of the D-02 and D-03 guards on p100 fixtures | D-08 | Needs the running app and the database | `fixtures.mjs` click-throughs, recorded in the security document |
| Rendered contrast, targets, overflow at 320 desktop and 390 phone, EmailStats tiles | Definition of converted 5 | Rendered measurement | `~/qb-ui-review/phase-100/after/` |
| Real phone, screen reader on the disabled-with-reason rows | D-21 | Assistive technology and device | phase UAT after deploy |
| SQL-backed quest delete with a reminder log | G-05 | EF InMemory ignores FKs | `QUESTBOARD_MIGRATOR_TEST_REQUIRED=1`, on the operator's yes |

## Security Domain

`security_enforcement` is not set to false in `.planning/config.json`, so this section applies. ASVS level 1, block on high (the Phase 99 setting).

### Applicable ASVS Categories

| ASVS Category | Applies | Standard Control |
|---------------|---------|-----------------|
| V2 Authentication | yes | Identity password policy (length 8, upper, lower, digit); admin reset and email change flows (G-01, G-19); change-email tokens valid 7 days (`Program.cs:75-81`) |
| V3 Session Management | yes | Security stamp revalidation every 5 minutes (`Program.cs:70-73`); a reset or disable invalidates sessions at the next validation; active board re-verified on non-GET requests |
| V4 Access Control | yes (the centre) | Policies `AdminOnly` and `SuperAdminOnly`, board query filters, the new reach and role guards in the Domain layer (G-01 to G-04) |
| V5 Input Validation | yes | Data annotations and Razor encoding; undefined enum values rejected by MVC (probed); `data-confirm-message` carries encoded plain text |
| V6 Cryptography | no | none added; Identity tokens and hashing only |
| V8 / V13 CSRF and API | yes | `[ValidateAntiForgeryToken]` on every state change; both DELETE actions become POST so the reflection sweep covers them |

### Known Threat Patterns for this stack

| Pattern | STRIDE | Standard Mitigation |
|---------|--------|---------------------|
| Board Admin takes over a global account via reset or email change (G-01, G-02) | Elevation of privilege, Spoofing | Domain reach rule enforced at the controller and inside the service; SuperAdmin read from the Identity role |
| Self-lockout or Admin-less board (G-03) | Denial of service | Guard on the resulting role and the Admin count; SuperAdmin recovery path stays |
| Cross-board id (quest, user, group) | Tampering, Information disclosure | Fail-closed global filter, membership scope check, refusal as a toast |
| CSRF on a state change | Tampering | Token on every form, reflection sweep, no GET state change except the recorded EmailStats refresh |
| Mail abuse through admin buttons (G-06, G-09) | Denial of service | Per-target limiter after the checks; per-actor limiter on create |
| Stored text in an inline handler | Tampering | Remove the handlers; `data-confirm-message` with Razor encoding |
| Unhandled DB error as a 500 (G-05) | Denial of service | Cleanup in the service, controller catches and toasts |

## Slice proposal (research proposal; the binding list goes in ROADMAP)

Each plan ends with the tracking block. Plans 04 to 08 each leave every page usable and the suite green.

| Plan | Autonomous | Content | Depends on |
|------|-----------|---------|------------|
| 100-01 | no | Opening checkpoint: operator confirms SQL Server at 1433; ports 8340/8341; `qb-mailpit`; fixtures approved; migrator suite yes or no; scratch root and tools copied and re-pointed; `ENVIRONMENT.md`; `senior-frontend`; `begin-phase`; record the scope drift (Phase 102 additions, removed sheets) | — |
| 100-02 | yes | p100 fixtures through the forms, frozen baseline copy and BEFORE capture of every row, both User-Agents (before any code change) | 01 |
| 100-03 | yes | Server audit re-verification: red facts for G-01 to G-08, G-13, G-15; live proofs on p100 fixtures; `100-SECURITY.md` draft with the gap table, `deferred-items.md` rows | 02 |
| 100-04 | no | D-05 operator checkpoint: O-1 to O-13 answered; `audit-answers.json` | 03 |
| 100-05 | yes | D-02 reach rule: repository query, Identity batch read, `GetAccountReachAsync`, service-level refusal, controller refusals, limiter order, tests | 04 |
| 100-06 | yes | D-03 guarded role change and removal, per-row flags, non-member toast, tests | 05 |
| 100-07 | yes | D-12: `DeleteUser` and `DeleteQuest` to POST with minimal forms on both layouts, `confirm-submit.js`, reminder-log and follow-up cleanup, rewritten tests | 06 |
| 100-08 | yes | D-10 seam and EmailStats stub tests, `ResendBaseUrl` default pin | 07 |
| 100-09 to 100-11 | no | Gate 1 package A (list, form, confirmation, modal against `quest-form` and the live 98 and 99 pages), package B (Q-124, Q-125, Q-126 AFTERs, D-16 wording, D-02 and D-03 refusal looks, G-18 and G-19 copy), the Gate 1 sheet and decision (redraw loop) | 08 |
| 100-12 | yes | Shared slice: display mapping (role, board type, quest status), badge, button, table, link and muted fixes, Platform layout verification probes (D-17), new page sheets scaffolded | 11 |
| 100-13 | yes | Forms: CreateUser, EditUser, ResetPassword, Group Create, Edit, Delete pair verified, `autocomplete="new-password"`, Q-92, Q-93 | 12 |
| 100-14 | yes | Lists: Admin/Users (Q-85, Q-126), Admin/Quests (Q-87), Group/Index, Members (Q-88, Q-125, modal, G-15), Platform/Users, LinkedBoards, both layouts, disabled-with-reason rows | 13 |
| 100-15 | yes | EmailStats (stub capture, tiles as siblings) | 14 |
| 100-16 | yes | Cleanup and guards: six sheets deleted, zero counts, guard tests rewritten, planning-reference scan, no test deleted, untouched-paths diff | 15 |
| 100-17 | yes | Closing sweep: contrast (EmailStats with the stub), targets, overflow, focus, reduced motion, full AFTER, exceptions file; `100-SECURITY.md` finalised and verified | 16 |
| 100-18 | no | Gate 2 package and decision, gap-closure loop, v6.5.0 hand-off (no PR, no tag, no push; GHCR caveat), STATE and ROADMAP free-text patched, UAT list | 17 |

Plan count and split are the planner's call (CONTEXT, Claude's Discretion); the constraint worth keeping is that BEFORE is captured before the first server change, the D-05 checkpoint precedes every fix plan, and no UI relayout is built before Gate 1.

## Project Constraints (from CLAUDE.md)

- Never commit to `main`; all work on `milestone/v9-rolling-improvements`. Executors stage files explicitly and never push; no PR, tag or release inside plans.
- Local database at `localhost:1433`: never install, start or tear down; if unreachable, stop and tell the operator. Never edit `appsettings.json` (override with user-secrets only; do not "fix" `Cannot generate SSPI context` by deleting `Trusted_Connection=true`). Integration tests use InMemory.
- No GSD planning references in source: no requirement, decision, finding, phase or plan ids, no planning document names in code, XML docs, CSS comments, string literals or test names. Commit messages may carry them.
- Before any Razor view change read `.claude/ui-guidelines.md`: card pattern, filled buttons by role, no inline code, tokens only, no `!important` in layered sheets, `.mobile-layout` twins, `Html.LocalTime` and `Html.WallClock`, `h1` page title.
- Never use `DateTime.Now`, `UtcNow` or `Today` for board-day reads (`IBoardClock`); `AmbientClockSeamTests` holds a closed list. `AdminController.GetResendStatsAsync` reads `DateTime.UtcNow` for a real instant (cutoff and "as of"); it is not on the closed list's board-day concern.
- Writes to `Events` and `Quests` go through the change tracker (no bulk, raw SQL, attach or hand-set state); `CalendarFeedWriter.cs`, `CalendarSubscriptionService.cs` and `FeedRevisionStamper.cs` are high-risk and untouched here.
- EF packages only in `QuestBoard.Repository`; migrations auto-apply; this phase needs none.
- On Linux never set GC-constraining environment variables; `0x80131506` is a flake, re-run once.
- Do not cite a fixed member count for the quest board (project memory); the planning docs carry a stale figure.
- Do not auto-provision the environment (SQL, containers); report what is missing.
- Push the milestone branch after each green wave is the orchestrator's job; re-push after each PR because head branches are auto-deleted.

## Sources

### Primary (HIGH confidence)
- Repository files read this session, cited by path and line above: `AdminController.cs`, `GroupController.cs`, `UsersController.cs`, `LinkedBoardsController.cs`, `AdminHandler.cs`, `Program.cs`, `UserService.cs`, `GroupService.cs`, `LinkedBoardSetService.cs`, `QuestService.cs`, `IdentityService.cs`, `IIdentityService.cs`, `IUserService.cs`, `IGroupService.cs`, `UserRepository.cs`, `GroupRepository.cs`, `BaseRepository.cs`, `QuestRepository.cs`, `QuestBoardContext.cs`, `FeedRevisionStamper.cs`, `EntityProfile.cs`, `ResendStatsClient.cs`, `GroupSessionMiddleware.cs`, `BoardPageGateFilter.cs`, all 26 in-scope views (inventory by grep; the main ones read in full), both Platform layouts, `components.css`, `legacy-pages.css`, the six area sheets and the three Phase 102 sheets, `confirm-submit.js`, `.claude/ui-guidelines.md`, the test files named in the coverage and guard tables.
- Probes run this session in scratch projects outside the repository (`/tmp/claude-1000/-mnt-Data-repos-quest-board-dnd/b8fc13d6-8736-4a94-80ff-1d341885e2b9/scratchpad/{findprobe,httpprobe,enumprobe,mvcprobe}`): EF Core InMemory 10.0.9 `Find` against a global filter; `ConfigurePrimaryHttpMessageHandler` on a named client registered later; `TypeDescriptor` enum conversion; an ASP.NET Core 10 minimal MVC app posting `GroupRole=7` and `-1` (both rejected). No repository file was modified.
- Planning documents: 100-CONTEXT, 100-DISCUSSION-LOG, ROADMAP Phase 100 and Phase 102, STATE, `94-AUDIT-admin.md` (all 44 findings, census, empty-state copy), `99-RESEARCH.md`, `99-SECURITY.md`, `99-VALIDATION.md`, `99-24-PLAN.md` and `99-01-PLAN.md` (tracking commands), deferred-items of phase 99, `~/qb-ui-review/phase-99/ENVIRONMENT.md` and `run-app.mjs`.

### Secondary (MEDIUM confidence)
- `ls`, `stat` and `ss` observations of `~/qb-ui-review/`, the docker container list and open ports (state at research time).

### Tertiary (LOW confidence)
- None beyond the `[ASSUMED]` items in the Assumptions Log.

## Metadata

**Confidence breakdown:**
- Server audit and DeleteQuest verdict: HIGH (code read; EF behaviour probed); the chains in G-01 and G-05 are read, not executed (A2, A3)
- D-02 and D-03 layering and what existing code enforces: HIGH
- D-10 seam: HIGH for the handler stub (probed), MEDIUM for the `ResendBaseUrl` capture seam (design, not built)
- UI conversion facts, counts, guard test locations: HIGH (re-measured and read)
- Platform layout items passing when rendered: MEDIUM (source and guard tests only)
- Slice proposal: MEDIUM (proposal)

**Research date:** 2026-11-18
**Valid until:** 2026-12-18, or until another phase touches `components.css`, the Platform layouts, `UserService` or `GroupService`.
