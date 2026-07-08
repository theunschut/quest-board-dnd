---
gsd_state_version: 1.0
milestone: v2.0
milestone_name: Omphalos Integration (redo)
current_phase: 65
current_phase_name: Platform Settings + Token Contract
status: planning
stopped_at: Phase 65 context gathered
last_updated: "2026-07-08T00:30:00.000Z"
last_activity: 2026-07-08 — merged main (v7.0 shipped through Phase 64); resumed as current milestone with Phases 35–37 renumbered to 65–67
progress:
  total_phases: 3
  completed_phases: 0
  total_plans: 0
  completed_plans: 0
  percent: 0
---

# Project State

## Project Reference

See: .planning/PROJECT.md (updated 2026-07-08 — v7.0 milestone close; v2.0 Omphalos Integration resumed in the same merge)

**Core value:** The quest board must reliably let DMs post quests and players sign up — everything else enhances that loop.
**Current focus:** Phase 65 — Platform Settings + Token Contract

## Current Position

Phase: 65 of 67 (Platform Settings + Token Contract)
Plan: — (not yet planned)
Status: Roadmap approved — ready to plan Phase 65
Last activity: 2026-07-08 — merged main (v7.0 shipped), Phases 35–37 renumbered to 65–67, resumed as current milestone

Progress: [░░░░░░░░░░] 0%

## Performance Metrics

**Velocity:**

- Total plans completed: 0
- Average duration: — min
- Total execution time: — hours

**By Phase:**

| Phase | Plans | Total | Avg/Plan |
|-------|-------|-------|----------|
| - | - | - | - |

**Recent Trend:**

- Last 5 plans: —
- Trend: —

*Updated after each plan completion*

- v7.0 shipped in ~3.1 days across 22 phases, 59 plans — largest milestone by phase count yet, driven by a long tail of ad-hoc backlog phases (47–64) folded in during execution. See `.planning/RETROSPECTIVE.md` for details.

## Accumulated Context

### Decisions

Decisions are logged in PROJECT.md Key Decisions table.
Recent decisions affecting current work:

- v2.0 scoping: HMAC-signed redirect token (not OAuth2/OIDC) behind a swappable `IIntegrationTokenService`/`SsoService` seam — both apps are first-party, under common operational control
- v2.0 scoping: identity match key is Quest Board's `UserEntity.Id` (int), never `Name`/`UserName`/email — precedented by the Phase 34.3 display-name authorization bug
- v2.0 scoping: replay protection is in scope this milestone (short-lived used-token tracking on top of the 5-minute TTL) — user pushed back on an initial "small trusted group" scope-cut
- v2.0 (redo) phases renumbered to 65–67, continuing from v7.0's Phase 64 — originally slotted as 35–37 continuing from v5.0's Phase 34.3, but v6.0/v6.1/v7.0 claimed those numbers on `main` while this milestone was deprioritized; old Phase 10/11 slots stay marked superseded, not reused

### Pending Todos

None yet.

### Blockers/Concerns

- Phase 67 touches `C:\Repos\omphalos`, a repo owned by another maintainer — goes through that repo's own PR review, not this project's branch protection. This is the milestone's actual critical path (external review latency, not implementation effort) and is the same failure mode that killed the original `milestone/3-omphalos-integration` attempt. Open that PR early.
- Phase 67 has two unverified LOW-confidence items flagged by research: Omphalos's live Postgres collation / whether `Users.Username` already has a unique index, and the exact SSO route path (`/api/sso/login` is a working assumption, not confirmed with the maintainer) — resolve both at the start of Phase 67, before writing the migration.
- `GroupSessionMiddleware` POST-body data-loss risk on session expiry mid-submission — still deferred from v5.0, not yet fixed.
- `Areas/Platform/Views/Shared/_Layout.Platform.Mobile.cshtml` appears to be dead code (Platform area's `_ViewStart.cshtml` never selects it) — discovered during Phase 42 research, deliberately left unfixed as out-of-scope for that phase. See PROJECT.md Known Issues.
- `GuildMembersController.Edit` POST's `SetAsMainCharacterAsync` demotion guard can never be true (dead code, predates Phase 56) — found during Phase 56 verification, flagged as a separate follow-up task, not yet actioned. See PROJECT.md Known Issues.

## Deferred Items

Items acknowledged and carried forward across milestone closes. Issue #78 (profile picture crop) was delivered by v7.0 and is no longer deferred.

| Category | Item | Status | Deferred At |
|----------|------|--------|-------------|
| requirement | EMAIL-04 — digest session reminder (multiple same-day quests → one email) | Still deferred — same-day quests have never occurred in over a year of operation | v4.0 close |
| requirement | REMIND-02 — combined reminder for multi-quest days | Still deferred — same as EMAIL-04 | v4.0 close |
| tech debt | `GroupSessionMiddleware` redirects on POST — data-loss risk if session expires mid-submission | Still deferred — flagged by code review in Phase 31, not yet fixed | v5.0 close |

## Session Continuity

Last session: 2026-07-08T00:30:00.000Z
Stopped at: Phase 65 context gathered (renumbered from Phase 35 during the v7.0 merge)
Resume file: .planning/phases/65-platform-settings-token-contract/65-CONTEXT.md
Next step: `/gsd:plan-phase 65`
