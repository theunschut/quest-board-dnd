---
phase: 86-viewer-local-times-and-correct-job-scheduling
plan: 02
subsystem: infra
tags: [hangfire, timezone, health-check, cron, board-clock]

requires: [86-01]
provides:
  - RecurringJobOptionsFactory.ForBoardZone: the testable seam every Hangfire cron registration builds its RecurringJobOptions through
  - BoardTimeZoneHealthCheck: the "board-timezone" health check exposing the board clock's UTC fallback on /health
  - DailyReminderJob reading IBoardClock.Today: the cron and the job it wakes now resolve the same zone
affects: []

actuals:
  tokens: 3955
  tasks: 3
  commits: 3

tech-stack:
  added: []
  patterns:
    - "RecurringJobOptionsFactory: a static factory pulling RecurringJobOptions construction into its own directly unit-testable seam, since Hangfire is not registered in the Testing environment and the live cron registration cannot be observed by an integration test"
    - "BoardTimeZoneHealthCheck: IHealthCheck returning Degraded (never Unhealthy) so a config typo cannot restart-loop the docker-compose container, with fixed-copy description strings so the configured value is never disclosed on the unauthenticated /health endpoint"

key-files:
  created:
    - QuestBoard.Service/Extensions/RecurringJobOptionsFactory.cs
    - QuestBoard.Service/HealthChecks/BoardTimeZoneHealthCheck.cs
    - QuestBoard.UnitTests/Services/RecurringJobOptionsFactoryTests.cs
    - QuestBoard.IntegrationTests/Controllers/BoardTimeZoneHealthCheckTests.cs
  modified:
    - QuestBoard.Service/Program.cs
    - QuestBoard.Service/Jobs/DailyReminderJob.cs
    - QuestBoard.UnitTests/Services/DailyReminderJobTests.cs

key-decisions:
  - "Split the health-check plan's single degraded-state test into two facts (Health_WithUnresolvableZone_ReturnsOkAndDegraded, Health_WithUnresolvableZone_DoesNotDiscloseConfiguredZoneId) so the suite reports at least 3 tests as the plan's acceptance criteria required, rather than folding both assertions into one fact."
  - "Removed a pre-existing, unrelated 'RESEARCH.md Pitfall 1' comment reference in Program.cs (email-resend rate limiting) while editing the same file for Task 1, since CLAUDE.md forbids planning-document references in source comments and the task's own acceptance criteria required zero RESEARCH.md occurrences in Program.cs."

requirements-completed: [D-03, D-04, D-07]

coverage:
  - id: T1
    description: "RecurringJobOptionsFactory.ForBoardZone returns options carrying the board clock's TimeZone; all three RecurringJob.AddOrUpdate calls in Program.cs pass it, cron hours unchanged, no obsolete Hangfire overload used"
    requirement: "D-03"
    verification:
      - kind: unit
        ref: "QuestBoard.UnitTests/Services/RecurringJobOptionsFactoryTests.cs (2 facts: reference-equality to clock zone, non-UTC-default regression)"
        status: pass
      - kind: build
        ref: "dotnet build — 0 CS0618 warnings naming AddOrUpdate"
        status: pass
    human_judgment: false
  - id: T2
    description: "BoardTimeZoneHealthCheck reports Degraded (HTTP 200) when the board clock fell back to UTC, Healthy otherwise; never Unhealthy; response body never discloses the configured zone id"
    requirement: "D-04"
    verification:
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Controllers/BoardTimeZoneHealthCheckTests.cs (3 facts: healthy path, degraded path stays HTTP 200, configured id not disclosed)"
        status: pass
    human_judgment: false
  - id: T3
    description: "DailyReminderJob computes tomorrow from IBoardClock.Today instead of DateTime.Today, so the cron zone and the job's own clock cannot drift apart"
    requirement: "D-07"
    verification:
      - kind: unit
        ref: "QuestBoard.UnitTests/Services/DailyReminderJobTests.cs#ExecuteAsync_ComputesTomorrow_FromBoardClockNotHostClock"
        status: pass
    human_judgment: false

duration: 35min
completed: 2026-09-20
status: complete
---

# Phase 86 Plan 02: Board-Zone Cron Registration and Degraded-State Health Check Summary

**All three Hangfire sweeps now pin to the board's resolved time zone through one testable factory seam, `DailyReminderJob` computes "tomorrow" from the same board clock, and a new `board-timezone` health check surfaces a UTC fallback on `/health` without restart-looping the container or disclosing the configured zone id.**

## Performance

- **Duration:** ~35 min
- **Tasks:** 3
- **Files modified:** 7 (4 created, 3 modified)

## Accomplishments
- `RecurringJobOptionsFactory.ForBoardZone(IBoardClock)`: the one testable seam standing in for the live Hangfire registration (Hangfire is not registered in the Testing environment, so the registration itself is unobservable to an integration test).
- All three `RecurringJob.AddOrUpdate` calls in `Program.cs` (`daily-session-reminders`, `recurring-occurrence-top-up`, `calendar-subscription-retention`) now pass `RecurringJobOptions { TimeZone = boardClock.TimeZone }` as a fourth argument. Cron hours (`0 9 * * *`, `0 3 * * *`, `0 4 * * *`) are unchanged — the fix is the zone, not a compensating offset.
- The four false "server local time (CET/CEST)" comments (three in `Program.cs`, one in `DailyReminderJob.cs`) are replaced with plain-language descriptions of the real mechanism, with no phase/decision/requirement identifiers.
- `BoardTimeZoneHealthCheck` (new `QuestBoard.Service/HealthChecks/` folder): reports `Degraded` — never `Unhealthy` — when `IBoardClock.IsDegraded` is true, registered as `"board-timezone"`. `GET /health` stays HTTP 200 in both states, so `docker-compose.yml`'s `curl -f` healthcheck never restart-loops the container over a timezone-id typo. Fixed-copy description strings never interpolate the configured id, a host path, or an exception message.
- `DailyReminderJob` now takes `IBoardClock` as a constructor parameter and computes `tomorrow` from `boardClock.Today.AddDays(1).ToDateTime(TimeOnly.MinValue)` instead of `DateTime.Today` — the cron that wakes the sweep and the clock the sweep computes with can no longer resolve two different zones.
- Full test suite: 551 unit (+3) and 850 integration (+3) tests, all green.

## Task Commits

Each task was committed atomically:

1. **Task 1: Pin all three Hangfire sweeps to the board zone through one testable seam** — `e6927627` (feat)
2. **Task 2: Make the UTC fallback visible from outside the container** — `12e3224a` (feat)
3. **Task 3: Move DailyReminderJob's "tomorrow" onto the same board clock the cron uses** — `0c86b3df` (fix)

## Files Created/Modified
- `QuestBoard.Service/Extensions/RecurringJobOptionsFactory.cs` — the testable seam wrapping `RecurringJobOptions` construction
- `QuestBoard.Service/HealthChecks/BoardTimeZoneHealthCheck.cs` — the `board-timezone` health check (new folder)
- `QuestBoard.Service/Program.cs` — wires the factory into all three cron registrations, registers the health check, rewrites four stale comments plus one unrelated stray planning-doc reference
- `QuestBoard.Service/Jobs/DailyReminderJob.cs` — reads `IBoardClock.Today` instead of `DateTime.Today`
- `QuestBoard.UnitTests/Services/RecurringJobOptionsFactoryTests.cs` — reference-equality and non-UTC-default coverage
- `QuestBoard.UnitTests/Services/DailyReminderJobTests.cs` — `FakeBoardClock`-backed fixture, new board-clock-vs-host-clock regression test
- `QuestBoard.IntegrationTests/Controllers/BoardTimeZoneHealthCheckTests.cs` — healthy path, degraded-stays-200 path, non-disclosure path

## Decisions Made
- Split the plan's single degraded-state health-check fact into two test methods so the suite reports at least 3 tests, matching the plan's acceptance criteria literally rather than folding two assertions into one fact.
- Fixed a pre-existing, unrelated `RESEARCH.md` comment reference in `Program.cs` (an email-resend rate-limiting comment untouched by any other phase-86 plan) while already editing that file for Task 1, since CLAUDE.md forbids planning-document references in source comments and Task 1's own acceptance criteria required zero `RESEARCH.md` occurrences anywhere in `Program.cs`.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 2 / CLAUDE.md enforcement] Stray unrelated planning-doc reference in Program.cs**
- **Found during:** Task 1 acceptance-criteria verification (`grep -c 'RESEARCH.md' QuestBoard.Service/Program.cs` returned 1, not the required 0)
- **Issue:** An unrelated comment on the email-resend rate limiter (`// ... (RESEARCH.md Pitfall 1). 3 requests / 1 hour ...`) referenced a planning document by name, violating CLAUDE.md's "Code Comments" rule and this task's own acceptance criterion for the file being edited.
- **Fix:** Removed the `(RESEARCH.md Pitfall 1)` parenthetical, keeping the rest of the comment's plain-language explanation intact.
- **Files modified:** `QuestBoard.Service/Program.cs`
- **Verification:** `grep -c 'RESEARCH.md' QuestBoard.Service/Program.cs` returns 0; `dotnet build` still succeeds.
- **Committed in:** `e6927627` (Task 1 commit)

**2. [Rule 4-adjacent test-shape adjustment] Health-check test count**
- **Found during:** Task 2, writing `BoardTimeZoneHealthCheckTests.cs`
- **Issue:** The plan described "two cases" (default factory, degraded factory), but the task's own acceptance criteria required `dotnet test ... --filter FullyQualifiedName~HealthCheck` to report "at least 3 tests run."
- **Fix:** Split the degraded-factory case into two `[Fact]` methods — one asserting HTTP 200 + `Degraded` body, one asserting the configured zone id is not disclosed — rather than combining both assertions into a single fact. No behavior or coverage was lost; this is purely a test-organization choice to satisfy the literal acceptance count.
- **Files modified:** `QuestBoard.IntegrationTests/Controllers/BoardTimeZoneHealthCheckTests.cs`
- **Verification:** `dotnet test QuestBoard.IntegrationTests --filter FullyQualifiedName~HealthCheck` reports 3 passed.
- **Committed in:** `12e3224a` (Task 2 commit)

---

**Total deviations:** 2 auto-fixed (1 CLAUDE.md-driven comment cleanup, 1 test-shape adjustment to meet an acceptance count). No production behavior changed beyond what the plan specified.

## Issues Encountered

None. This plan ran non-interactively as a parallel worktree executor; the objective's automated `<verify>` commands for all three tasks passed without requiring a checkpoint.

## User Setup Required

None — no external service configuration required.

## Next Phase Readiness
- All three Hangfire recurring registrations, `DailyReminderJob`, and `/health` now resolve the board's own zone through the same `IBoardClock` seam established in 86-01.
- No blockers for later plans in this phase (86-03 through 86-06), which continue converting ambient `DateTime.Today`/`Now` reads and real-instant render sites onto the same seam.
- `git status --porcelain QuestBoard.Domain/Services/CalendarFeedWriter.cs QuestBoard.Domain/Services/CalendarSubscriptionService.cs` confirmed empty (untouched, per plan's non-regression check).

## Self-Check: PASSED

All 7 files listed in Files Created/Modified verified present on disk. All 3 commit hashes (`e6927627`, `12e3224a`, `0c86b3df`) verified present in `git log --oneline`.

---
*Phase: 86-viewer-local-times-and-correct-job-scheduling*
*Completed: 2026-09-20*
