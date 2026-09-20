---
phase: 86
slug: viewer-local-times-and-correct-job-scheduling
# status lifecycle: draft (seeded by plan-phase) → validated (set by validate-phase §6)
status: draft
nyquist_compliant: true
wave_0_complete: true
created: 2026-09-20
---

# Phase 86 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.
> Seeded by `/gsd-plan-phase` from `86-RESEARCH.md` § Validation Architecture.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit v3 (`xunit.v3` 3.2.2) with `FluentAssertions` 8.10.0 and `NSubstitute` 5.3.0 |
| **Config file** | none — settings live in `QuestBoard.UnitTests.csproj` / `QuestBoard.IntegrationTests.csproj` |
| **Quick run command** | `dotnet test QuestBoard.UnitTests` |
| **Full suite command** | `dotnet test` |
| **Estimated runtime** | quick ~15s (no DB) · full suite dominated by integration tests |

---

## Sampling Rate

- **After every task commit:** Run `dotnet test QuestBoard.UnitTests`
- **After every plan wave:** Run `dotnet test`
- **Before `/gsd-verify-work`:** Full suite must be green
- **Max feedback latency:** ~15 seconds for the per-task signal

---

## Per-Task Verification Map

> Task IDs are assigned by the planner. This table is seeded with the behaviors
> RESEARCH.md identified; `/gsd-validate-phase` fills in the Task ID / Plan / Wave
> columns once PLAN.md files exist.

| Task ID | Plan | Wave | Requirement | Threat Ref | Secure Behavior | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|------------|-----------------|-----------|-------------------|-------------|--------|
| Task 1 | 86-06 | 3 | wall-clock unmoved | T-86-10 | N/A | integration | `dotnet test QuestBoard.IntegrationTests --filter FullyQualifiedName~WallClockUnmoved` | ✅ | ✅ green |
| Task 1 | 86-01 | 1 | real-instant render | — | N/A | integration | `dotnet test QuestBoard.IntegrationTests --filter FullyQualifiedName~LocalTimeRender` | ✅ | ✅ green |
| Task 2 | 86-01 | 1 | unresolvable zone → UTC fallback + Degraded | T-86-16 | fails safe to UTC, app still starts | unit | `dotnet test QuestBoard.UnitTests --filter FullyQualifiedName~BoardClockFallback` | ✅ | ✅ green |
| Task 2 | 86-02 | 2 | `/health` 200 + Degraded body when fallback active | T-86-16 | N/A | integration | `dotnet test QuestBoard.IntegrationTests --filter FullyQualifiedName~HealthCheck` | ✅ | ✅ green |
| Task 1 | 86-03 | 2 | `EventSeriesService` results unchanged after `DateTime.Today` → board clock | — | N/A | unit | `dotnet test QuestBoard.UnitTests --filter FullyQualifiedName~EventSeriesService` | ✅ | ✅ green |
| Task 1 | 86-02 | 2 | Hangfire `RecurringJobOptions.TimeZone` resolves to the configured board zone | — | N/A | unit | `dotnet test QuestBoard.UnitTests --filter FullyQualifiedName~RecurringJobOptions` | ✅ | ✅ green |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

- [x] A hand-rolled fake board clock / `FixedTimeProvider`-equivalent test double, matching the existing hand-rolled (not package-based) pattern in `QuestBoard.UnitTests/.../EventsOverviewAggregationTests.cs:20-25` — built as `QuestBoard.UnitTests/Helpers/FakeBoardClock.cs` in 86-01, shared by 86-02/86-03/86-06
- [x] `EventSeriesServiceTests` — RESEARCH.md found no existing test class; the 7-site `DateTime.Today` migration currently has no regression net — built in 86-03 (six facts against a `FakeBoardClock` pinned to a date the host's own clock does not share)
- [x] A wrapped/injectable timezone resolver so a test can force `TimeZoneInfo.FindSystemTimeZoneById` to throw, exercising the UTC-fallback + Degraded path that a healthy container would never reach — not built as a separate interface: an unresolvable id (e.g. `"Definitely/NotAZone"`) passed straight to the real `TimeZoneInfo.FindSystemTimeZoneById` inside `BoardClock`'s own construction exercises the identical fallback path with no extra seam, since the real call already throws `TimeZoneNotFoundException` for a bad id on any host. `BoardClockTests.cs` (86-01) and `BoardTimeZoneHealthCheckTests.cs` (86-02) both use this directly.
- [x] A test seam for the Hangfire recurring-job options (extract options construction into a testable method — Hangfire is not registered in the `Testing` environment per `WebApplicationFactoryBase.cs`, so the live registration cannot be integration-tested) — built as `QuestBoard.Service/Extensions/RecurringJobOptionsFactory.cs` in 86-02

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| `tzdata` is present in the running container and `Europe/Amsterdam` resolves | RESEARCH.md Assumption A1 | Requires the real built image; CI's test host resolves zones regardless | Build the image and run `docker run --rm <image> /bin/sh -c 'ls /usr/share/zoneinfo/Europe/Amsterdam'`, or hit `/health` on a running container and confirm the board-clock check is Healthy (not Degraded) |
| No flash of UTC on first paint for a viewer outside the board zone | CONTEXT.md client-side rendering decision | Visual/timing behavior in a real browser | Load a page with rendered instants with the browser zone set to e.g. `America/New_York`; confirm the pre-hydration value is the board-zone formatted string with no zone label at all, and that after hydration it becomes the viewer's own zone |
| The calendar feed is byte-identical before and after the phase | ROADMAP risk "Shifting the calendar feed" | End-to-end artifact comparison against a subscribed client | Capture the `.ics` output on `main` and on the phase branch for the same seeded data; diff must be empty |

---

## Validation Sign-Off

- [x] All tasks have `<automated>` verify or Wave 0 dependencies
- [x] Sampling continuity: no 3 consecutive tasks without automated verify
- [x] Wave 0 covers all MISSING references
- [x] No watch-mode flags
- [x] Feedback latency < 30s
- [x] `nyquist_compliant: true` set in frontmatter

**Approval:** All six Per-Task Verification Map rows are green across the five completed plans (86-01 through 86-06 Tasks 1-3); the phase's own blocking human-verification checkpoint (86-06 Task 4) is the one item this document cannot close on its own.
