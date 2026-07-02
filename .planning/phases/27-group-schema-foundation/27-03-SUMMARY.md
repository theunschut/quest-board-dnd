---
phase: 27-group-schema-foundation
plan: 03
subsystem: database
tags: [ef-core, migrations, sql-server, identity, group-schema]

# Dependency graph
requires:
  - phase: 27-group-schema-foundation/27-02
    provides: AddGroupSchema migration with Groups/UserGroups tables, GroupId FKs, and data-seeding raw SQL
provides:
  - AddGroupSchema migration verified against live SQL Server (GROUP-04/05/06 confirmed)
  - Deployment-constraint comment in AddGroupSchema.cs warning operators of Phase 27-29 co-deploy requirement
affects: [28-group-domain-layer, 29-group-auth-handlers, deployment-runbook]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Deployment-constraint comment above migration class declaration documents operational risk visible at code review"

key-files:
  created: []
  modified:
    - QuestBoard.Repository/Migrations/20260630055221_AddGroupSchema.cs

key-decisions:
  - "Deployment constraint comment placed above the class declaration (not inside the class body) so it is immediately visible when opening the file"
  - "Phase 27 must be deployed together with Phases 28-29, or during a maintenance window — deploying Phase 27 alone leaves DungeonMasterHandler/AdminHandler broken on next user login"

patterns-established:
  - "Deployment-constraint comment pattern: place above the class declaration in the migration file, state the broken component, the cause, and the required co-deployment rule"

requirements-completed: [GROUP-04, GROUP-05, GROUP-06]

# Metrics
duration: 15min
completed: 2026-06-30
---

# Phase 27 Plan 03: Migration Verification and Deployment-Constraint Comment Summary

**AddGroupSchema migration verified against live SQL Server (EuphoriaInn seeded, all users in UserGroups with correct GroupRole, AspNetUserRoles cleared of Player/DM/Admin) and annotated with a Phase 27-29 co-deployment constraint comment**

## Performance

- **Duration:** ~15 min
- **Started:** 2026-06-30T07:00:00Z
- **Completed:** 2026-06-30T07:15:00Z
- **Tasks:** 2 (1 automated + 1 human-verify checkpoint)
- **Files modified:** 1

## Accomplishments

- Moved and expanded the deployment-constraint comment to sit above the `AddGroupSchema` class declaration, making it immediately visible to any operator reviewing the migration
- Comment documents exactly which handlers break (DungeonMasterHandler, AdminHandler), why (AspNetUserRoles Player/DM/Admin rows deleted), and the required co-deployment rule (Phases 27-29 together or maintenance window)
- Migration applied cleanly on local dev SQL Server: no FK-violation, no IDENTITY_INSERT error
- All 6 SQL spot-checks confirmed GROUP-04/05/06 against live SQL Server: one EuphoriaInn group (Id=1), all Quests and ShopItems with GroupId=1, every user has a UserGroups row with correct GroupRole (Admin=2, DungeonMaster=1, Player/none=0), AspNetUserRoles free of Player/DM/Admin rows

## Task Commits

Each task was committed atomically:

1. **Task 1: Document the Phase 27-29 co-deployment constraint in the migration** - `f45f8f5` (docs)
2. **Task 2: Apply migration on local dev SQL Server and verify seeding** - Human-verify checkpoint (no commit — verification only)

**Plan metadata:** (docs commit below)

## Files Created/Modified

- `QuestBoard.Repository/Migrations/20260630055221_AddGroupSchema.cs` — Deployment-constraint comment moved above class declaration; Up()/Down() logic unchanged

## Decisions Made

- Placed the comment above the class declaration rather than inside the class body: more visible at a glance when opening the file and matches the "operator warning" intent
- Wrote the constraint as plain `//` comments (not XML doc `///`) since the class already has `/// <inheritdoc />` and adding a `///` summary block would duplicate or conflict with the inherited doc

## Deviations from Plan

None - plan executed exactly as written. The comment already partially existed inside the class body from plan 02; this plan moved it above the class declaration and expanded it with the specific rule statement, which is exactly what the task specified.

## Issues Encountered

None. Build was green before and after (0 CS errors). Migration applied on local dev SQL Server with no exceptions. All 6 spot-checks returned expected values on the first apply.

## Threat Coverage

| Threat ID | Status |
|-----------|--------|
| T-27-06 (DoS — auth break on deploy) | Mitigated — deployment-constraint comment in migration file documents the risk and required co-deployment rule |
| T-27-07 (Integrity — seeding on real SQL Server) | Mitigated — human-run spot-checks confirmed all 6 expected outcomes against live SQL Server |

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

Phase 27 schema foundation is fully complete:
- All 3 plans executed (27-01 entities/domain, 27-02 migration, 27-03 verification)
- All GROUP-01 through GROUP-06 requirements met
- Migration verified on live SQL Server
- Deployment constraint documented

Ready for Phase 28 (group domain layer — repositories, services, AutoMapper profiles) and Phase 29 (auth handler replacement reading UserGroups.GroupRole). Phases 28 and 29 must ship together with Phase 27 in any production deployment.

---
*Phase: 27-group-schema-foundation*
*Completed: 2026-06-30*
