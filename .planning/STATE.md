---
gsd_state_version: 1.0
milestone: v6.1
milestone_name: Bugfixes
current_phase: 42
status: milestone_complete_pending_archive
stopped_at: Phase 42 complete — v6.1 milestone (5 phases) fully done, pending /gsd-complete-milestone
last_updated: "2026-07-04T15:31:23.756Z"
last_activity: 2026-07-04
last_activity_desc: Phase 42 complete
progress:
  total_phases: 5
  completed_phases: 5
  total_plans: 16
  completed_plans: 16
  percent: 100
current_phase_name: Site-Wide Toast Notification Redesign
---

# Project State

## Project Reference

See: .planning/PROJECT.md (updated 2026-07-04 — Phase 42 complete, v6.1 milestone's 5 phases all done)

**Core value:** The quest board must reliably let DMs post quests and players sign up — everything else enhances that loop.
**Current focus:** v6.1 Bugfixes milestone complete (5/5 phases) — awaiting `/gsd-complete-milestone v6.1`

## Current Position

Phase: 42 (Site-Wide Toast Notification Redesign) — COMPLETE
Plan: 5/5 complete
Status: v6.1 Bugfixes milestone complete — pending archive
Last activity: 2026-07-04 — Phase 42 complete, UAT approved, all 5 v6.1 phases done

## Performance Metrics

**Velocity:**

- Total plans completed (v6.0): 11/11
- Timeline: ~1.4 days (2026-07-02 → 2026-07-03)

**By Phase:**

| Phase | Plans | Status |
|-------|-------|--------|
| 38. Group-Scoped User List | 1/1 | Complete — ready for verification |
| 39. Shared Collision-Aware User Creation & Email | 3/3 | Complete — ready for verification |
| 40. Platform Members Page Redesign | 0/TBD | Not started |

**Recent Trend:**

- v6.0 shipped in ~1.4 days across 3 phases — fastest pace yet. See .planning/RETROSPECTIVE.md for details.
- v6.1 is a 3-phase bugfix milestone (8 requirements), continuing directly from Phase 37.

| Phase 38 P01 | 25min | 3 tasks | 6 files |
| Phase 39 P01 | 12min | 3 tasks | 4 files |
| Phase 39 P02 | 3min | 3 tasks | 4 files |
| Phase 39 P03 | 8min | 3 tasks | 1 files |

## Accumulated Context

### Decisions

- Phase ordering: Phase 39 (shared collision-aware creation logic) must ship before Phase 40 (Platform Members page) because Phase 40's new create-user entry point reuses Phase 39's shared method — avoids this codebase's recurring duplicated-logic-diverges bug class (see `GetActiveBoardTypeAsync` 3x precedent in PROJECT.md Known Issues).
- Phase 38 is fully independent and safest to ship first — pure read-path fix, smallest blast radius, closes a live PII leak.
- MEMBERS-01 was refined after research: the Platform Members page uses a two-column layout (current members left, searchable available-users + Add User + Create New User right), not a single-column redesign. Phase 40's scope reflects this.

Full prior-milestone decision log: PROJECT.md Key Decisions table; v6.0 detail in `.planning/milestones/v6.0-ROADMAP.md` and `.planning/RETROSPECTIVE.md`.

- [Phase 38]: Added a dedicated GetAllGroupMembers/GetAllGroupMembersAsync method rather than unioning GetAllDungeonMasters+GetAllPlayers, per plan's explicit non-default choice
- [Phase 38]: Membership guard on the four role-change POST actions reuses existing GetGroupRoleByIdAsync(userId, groupId) rather than adding a new IsMemberOfGroupAsync method
- [Phase 39]: UserService now composes IGroupService (constructor dependency) to use throw-on-collision AddMemberAsync for already-member detection, diverging from AdminController.CreateUser's current upsert-based SetGroupRoleAsync
- [Phase 39]: AddedToGroup.razor CTA links to plain /Account/Login with no token — user already has a password from their original account
- [Phase 39]: Warning banner reuses existing dismissible alert markup (icon + message + btn-close) rather than a toast notification, per the deferred site-wide toast conversion decision
- [Phase 39]: IGroupService injected directly into AdminController's primary constructor to resolve the group Name for the AddedToGroup email body
- [Phase 39]: AddedToGroupStrandedAccount branch returns the identical success flash string as AddedToGroup so the admin sees no distinction between which email template actually fired
- [Phase 42]: RESEARCH found CONTEXT.md's stated "3 layouts" was incomplete — the Platform Area (`Areas/Platform/Views/Shared/_Layout.Platform.cshtml`/`.Platform.Mobile.cshtml`) resolves via its own separate `_ViewStart.cshtml`, independent of the root layouts; all 5 layouts were wired with the shared `_Toasts.cshtml` partial
- [Phase 42]: TempData key naming standardized app-wide to Success/Error/Warning/Info, retiring AccountController's outlier SuccessMessage/ErrorMessage/InfoMessage keys — incidentally fixed two previously-silent broken message paths as an intentional side effect, not scope creep
- [Phase 42]: GoldReceived "+X gp" toast kept as a bespoke Shop-specific block layered alongside the shared partial's generic Success toast, not folded into the generic system

### Roadmap Evolution

- Phase 42 added: Site-Wide Toast Notification Redesign — promotes the "Deferred Ideas" item from `39-DISCUSSION-LOG.md` (convert all flash messages app-wide from static alert banners to Bootstrap toast notifications, matching the Shop view's existing local toast pattern) into its own roadmap phase for this milestone.
- Phase 42 complete (2026-07-04) — v6.1 Bugfixes milestone's 5 phases (38, 39, 40, 41, 42) all done. Milestone awaiting `/gsd-complete-milestone v6.1`.

### Pending Todos

None — all 5 v6.1 phases complete. Next: `/gsd-complete-milestone v6.1` to archive the milestone.

### Blockers/Concerns

None open for v6.1 — all 5 phases shipped clean (no unresolved code review findings, no open UAT gaps).

Carried forward, still unresolved, not addressed by any milestone yet:

- `GroupSessionMiddleware` redirects on all HTTP verbs including POST — a POST-body data-loss risk if the session expires mid-submission; flagged by code review during Phase 31, not yet fixed.

### Risk Flags for Planning (from research)

- Phase 38: group-scoping join must be an explicit manual join (`UserEntity` has no Global Query Filter) — mirror `UserRepository.GetAllDungeonMasters`/`GetAllPlayers` exactly; add a cross-group-isolation regression test.
- Phase 39: no consent step before auto-adding an existing user to a group on email collision (by milestone spec) — needs explicit UAT/human-verify since it's a silent privilege-grant path. Build a dedicated `GroupMembershipAddedEmailJob`/`AddedToGroup.razor` with no callback-URL/password-reset token.
- Phase 40: new Platform `CreateMember`/create-user action must source `groupId` strictly from the route, never from session `IActiveGroupContext` — this exact session/route confusion bug class has already caused two prior incidents (Phase 30, Phase 34.3). Never inject `IActiveGroupContext` into `GroupController`.

## Deferred Items

Items acknowledged and carried forward from previous milestone close (2026-07-02):

| Category | Item | Status | Deferred At |
|----------|------|--------|-------------|
| requirement | EMAIL-04 — digest session reminder (multiple same-day quests → one email) | Deferred since v4.0 — same-day quests have never occurred in one year of operation | v4.0 close |
| requirement | REMIND-02 — combined reminder for multi-quest days | Deferred — same as EMAIL-04 | v4.0 close |
| tech debt | `GroupSessionMiddleware` redirects on POST — data-loss risk if session expires mid-submission | Deferred — flagged by code review in Phase 31, not yet fixed | v5.0 close |
| requirement | Profile picture crop/avatar selection (issue #78) | Deferred since v2.x — SkiaSharp native lib availability unverified on deployment host | v5.0 close |

## Session Continuity

Last session: 2026-07-04T00:00:00Z
Stopped at: Phase 42 complete — v6.1 milestone (5 phases) fully done, pending `/gsd-complete-milestone`
Resume file: None
Next step: Run `/gsd-complete-milestone v6.1` to archive the milestone and prepare for the next one

## Operator Next Steps

- v6.1 Bugfixes milestone is fully complete — all 5 phases (38-42) executed, verified, and UAT-approved. Run `/gsd-complete-milestone v6.1` to archive the milestone and start the next one.
