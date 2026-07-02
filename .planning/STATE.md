---
gsd_state_version: 1.0
milestone: v2.0
milestone_name: Omphalos Integration
status: planning
stopped_at: Phase 35 context gathered
last_updated: "2026-07-02T18:59:25.814Z"
last_activity: 2026-07-02 — ROADMAP.md created (Phases 35–37), 34/34 v1 requirements mapped
progress:
  total_phases: 3
  completed_phases: 0
  total_plans: 0
  completed_plans: 0
  percent: 0
---

# Project State

## Project Reference

See: .planning/PROJECT.md (updated 2026-07-02 — v2.0 Omphalos Integration milestone started)

**Core value:** The quest board must reliably let DMs post quests and players sign up — everything else enhances that loop.
**Current focus:** Phase 35 — Platform Settings + Token Contract

## Current Position

Phase: 35 of 37 (Platform Settings + Token Contract)
Plan: — (not yet planned)
Status: Roadmap approved — ready to plan Phase 35
Last activity: 2026-07-02 — ROADMAP.md created (Phases 35–37), 34/34 v1 requirements mapped

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

## Accumulated Context

### Decisions

Decisions are logged in PROJECT.md Key Decisions table.
Recent decisions affecting current work:

- v2.0 scoping: HMAC-signed redirect token (not OAuth2/OIDC) behind a swappable `IIntegrationTokenService`/`SsoService` seam — both apps are first-party, under common operational control
- v2.0 scoping: identity match key is Quest Board's `UserEntity.Id` (int), never `Name`/`UserName`/email — precedented by the Phase 34.3 display-name authorization bug
- v2.0 scoping: replay protection is in scope this milestone (short-lived used-token tracking on top of the 5-minute TTL) — user pushed back on an initial "small trusted group" scope-cut
- v2.0 phases start at Phase 35, continuing from v5.0's Phase 34.3 — old Phase 10/11 slots stay marked superseded, not reused

### Pending Todos

None yet.

### Blockers/Concerns

- Phase 37 touches `C:\Repos\omphalos`, a repo owned by another maintainer — goes through that repo's own PR review, not this project's branch protection. This is the milestone's actual critical path (external review latency, not implementation effort) and is the same failure mode that killed the original `milestone/3-omphalos-integration` attempt. Open that PR early.
- Phase 37 has two unverified LOW-confidence items flagged by research: Omphalos's live Postgres collation / whether `Users.Username` already has a unique index, and the exact SSO route path (`/api/sso/login` is a working assumption, not confirmed with the maintainer) — resolve both at the start of Phase 37, before writing the migration.
- `GroupSessionMiddleware` POST-body data-loss risk on session expiry mid-submission — still deferred from v5.0, not yet fixed.

## Deferred Items

Items acknowledged and carried forward from previous milestone close:

| Category | Item | Status | Deferred At |
|----------|------|--------|-------------|
| requirement | EMAIL-04 / REMIND-02 — digest session reminder (multiple same-day quests → one email) | Deferred — same-day quests have never occurred in one year of operation | v4.0 close |
| tech debt | `GroupSessionMiddleware` redirects on POST — data-loss risk if session expires mid-submission | Deferred — flagged by code review in Phase 31, not yet fixed | v5.0 close |
| requirement | Profile picture crop/avatar selection (issue #78) | Deferred — SkiaSharp native lib availability unverified on deployment host | v5.0 close (originally v2.x) |

## Session Continuity

Last session: 2026-07-02T18:59:25.800Z
Stopped at: Phase 35 context gathered
Resume file: .planning/phases/35-platform-settings-token-contract/35-CONTEXT.md
Next step: `/gsd:plan-phase 35`
