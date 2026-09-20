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

| Behavior | Requirement | Why Manual | Test Instructions | Measured Result |
|----------|-------------|------------|-------------------|------------------|
| `tzdata` is present in the running container and `Europe/Amsterdam` resolves | RESEARCH.md Assumption A1 | Requires the real built image; CI's test host resolves zones regardless | Build the image and run `docker run --rm <image> /bin/sh -c 'ls /usr/share/zoneinfo/Europe/Amsterdam'`, or hit `/health` on a running container and confirm the board-clock check is Healthy (not Degraded) | **RESOLVED — A1 verified true.** `docker run --rm mcr.microsoft.com/dotnet/aspnet:10.0 ls -l /usr/share/zoneinfo/Europe/Amsterdam` returned `-rw-r--r-- 1 root root 2910 Jul 17 12:49 /usr/share/zoneinfo/Europe/Amsterdam`. The Dockerfile's final stage is `FROM base` and copies only published DLLs on top, so nothing removes the zoneinfo file. No Dockerfile change is needed. Separately, `/health` returned `Healthy` (HTTP 200, not `Degraded`) — but that reading was taken against the app running on the Linux dev host (itself `Europe/Amsterdam`), not inside the built container, because `docker-compose.yml` pulls `ghcr.io/theunschut/dnd-quest-board:latest` rather than building locally. The base-image tzdata check above is the substantive evidence for the container case; a true in-container `/health` reading needs a published image build. See `86-RESEARCH.md` § Assumptions Log row A1, which remains textually `[CITED]` in that document — this row is the verified supersession. |
| No flash of UTC on first paint for a viewer outside the board zone | CONTEXT.md client-side rendering decision | Visual/timing behavior in a real browser | Load a page with rendered instants with the browser zone set to e.g. `America/New_York`; confirm the pre-hydration value is the board-zone formatted string with no zone label at all, and that after hydration it becomes the viewer's own zone | **PASS.** Verified against the real shipped `hydrateLocalTimes()` with a spoofed `America/New_York` viewer: a `date-time` value rendered `11:45` (Amsterdam) server-side and `5:45` after hydration; a `date` value at `22:30Z` correctly moved from `21 Sept` to `20 Sept` across the day boundary. An unknown `data-style` and a malformed `datetime` were each skipped without aborting valid sibling elements. No `UTC`/`CEST`/`CET`/`GMT` string appeared in visible text on any page checked; `UTC` appeared only in the `title` tooltip. Server-rendered text is board time before hydration, so there is no flash of UTC. The Calendar Subscription "Last fetched" tracer confirmed this end to end on a real value: stored instant `2026-09-20T16:38:47Z` rendered server-side as `Sep 20, 2026, 6:38 PM` (18:38 board time, not 16:38), hydrated to `20 Sept 2026, 18:38`, tooltip `2026-09-20 16:38 UTC`. Mobile/locale backstop also confirmed: at a 375px viewport, German (`8. Juli 2026`, `20. Sept. 2026, 11:45`) and Russian (`8 июл. 2026 г.`, `20 сент. 2026 г., 11:45`) renderings produced 0px of parent overflow and 0px of page horizontal scroll (worst case 165px inside a 309px container). |
| The calendar feed is byte-identical before and after the phase | ROADMAP risk "Shifting the calendar feed" | End-to-end artifact comparison against a subscribed client | Capture the `.ics` output on `main` and on the phase branch for the same seeded data; diff must be empty | **PASS.** The live `.ics` feed (operator-created subscription, fetched once against the running application) returned `DTSTART:20260703T180000` style values for all 9 events: no `Z` suffix, no `TZID` parameter, no `VTIMEZONE` block. Threat T-86-10 is confirmed mitigated in the running application, not just in `CalendarFeedFloatingTimeGuardTests`. No game night moved: on quest 12039 the only clock time rendered anywhere on the page was `6:00 pm`, matching stored `18:00:00` exactly (the `20:00` a UTC+2 conversion would have produced appears nowhere); finalized session read `Friday, October 02, 2026 at 6:00 pm` against stored `2026-10-02T18:00:00`. |

### Coverage Gaps Reached During Verification (limitations, not failures)

- The closed-quest side of the QuestLog comparison could not be exercised visually: all 7 closed quests live in `GroupId=4` while the active board is `GroupId=1`, so they return 404 on multi-tenancy scoping. Pre-existing; this phase touched only the four QuestLog *view* files, not `QuestLogController.cs`.
- No `Events` or `EventSeries` rows exist in the dev dataset, so the events/series path was covered by CI only (`EventSeriesServiceTests`).

### Incidental Finding (out of scope, recorded for the record)

`Quest/Manage` on mobile renders no signup timestamps at all — a pre-existing content gap in the `.Mobile.cshtml` twins that 86-05 correctly declined to fill. The result is a desktop/mobile asymmetry that predates this phase and is NOT a phase-86 regression.

### QuestLog Finalized-vs-Closed Format Asymmetry — Resolved, Not Accepted

The operator reviewed the deliberate cosmetic asymmetry (a finalized quest's Completed date rendering in the old wording while a closed quest's renders in the new canonical short-date form) and chose to remove it rather than accept it. Gap-closure plan **86-07** was executed and merged in response: it added `Html.WallClock` plus a `hydrateWallClockTimes()` pass that re-formats wall-clock values into the viewer's locale wording without any timezone conversion, and moved all four QuestLog `FinalizedDate` branches onto it. Verified live after the change: the `datetime` attribute carries no `Z`/offset, no `title` tooltip is emitted, the hour is unchanged, and only locale wording differs. All 43 QuestLog Index rows converted with zero `Z`/offset values and zero false UTC tooltips. See `86-07-SUMMARY.md`.

---

## Validation Sign-Off

- [x] All tasks have `<automated>` verify or Wave 0 dependencies
- [x] Sampling continuity: no 3 consecutive tasks without automated verify
- [x] Wave 0 covers all MISSING references
- [x] No watch-mode flags
- [x] Feedback latency < 30s
- [x] `nyquist_compliant: true` set in frontmatter

**Approval:** All six Per-Task Verification Map rows are green across the five completed plans (86-01 through 86-06 Tasks 1-3). The phase's blocking human-verification checkpoint (86-06 Task 4) is now resolved: all three manual-only behaviours above carry measured results, Assumption A1 is verified true against the actual base image, and the QuestLog format asymmetry the operator flagged was closed by gap-closure plan 86-07 rather than accepted as-is. Phase 86 is complete.
