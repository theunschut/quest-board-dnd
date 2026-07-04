---
gsd_state_version: 1.0
milestone: v6.1
milestone_name: Bugfixes
current_phase: 41
current_phase_name: Safe User Removal & Account Disable
status: executing
stopped_at: Phase 41 UI-SPEC approved
last_updated: "2026-07-04T12:33:59.563Z"
last_activity: 2026-07-04
last_activity_desc: Phase 41 execution started
progress:
  total_phases: 4
  completed_phases: 3
  total_plans: 11
  completed_plans: 7
  percent: 64
---

# Project State

## Project Reference

See: .planning/PROJECT.md (updated 2026-07-03 — v6.1 Bugfixes milestone started)

**Core value:** The quest board must reliably let DMs post quests and players sign up — everything else enhances that loop.
**Current focus:** Phase 41 — Safe User Removal & Account Disable

## Current Position

Phase: 41 (Safe User Removal & Account Disable) — EXECUTING
Plan: 1 of 4
Status: Executing Phase 41
Last activity: 2026-07-04 — Phase 41 execution started

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

### Roadmap Evolution

- Phase 42 added: Site-Wide Toast Notification Redesign — promotes the "Deferred Ideas" item from `39-DISCUSSION-LOG.md` (convert all flash messages app-wide from static alert banners to Bootstrap toast notifications, matching the Shop view's existing local toast pattern) into its own roadmap phase for this milestone.

### Pending Todos

None yet — Phase 38 planned. Next: `/gsd-execute-phase 38`.

### Blockers/Concerns

None open for v6.1 yet.

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

Last session: 2026-07-04T11:45:04.358Z
Stopped at: Phase 41 UI-SPEC approved
Resume file: .planning/phases/41-safe-user-removal-account-disable/41-UI-SPEC.md
Next step: Phase 39 complete — proceed to Phase 40 (Platform Members Page Redesign)

## Operator Next Steps

- Phase 39 (Shared Collision-Aware User Creation & Email) is fully complete — all 3 plans executed, human-verify checkpoint approved. Run `/gsd-plan-phase 40` to plan the Platform Members Page Redesign phase, which reuses `CreateOrAddToGroupAsync` for its new create-user entry point.
