---
phase: 86-viewer-local-times-and-correct-job-scheduling
verified: 2026-09-20T00:00:00Z
status: passed
score: 17/17 must-haves verified
behavior_unverified: 0
overrides_applied: 0
---

# Phase 86: Viewer-Local Times and Correct Job Scheduling Verification Report

**Phase Goal:** A reader sees every real timestamp in their own browser's timezone instead of UTC,
and the three nightly sweeps fire at the hour their registration claims — without moving a single
game night by so much as a minute.

**Verified:** 2026-09-20
**Status:** passed
**Re-verification:** No — initial verification

**Traceability note:** `.planning/REQUIREMENTS.md` carries no Phase 86 rows by design. Traceability
runs on `86-CONTEXT.md`'s locked decisions D-01…D-07, cross-referenced against each PLAN's
`requirements:` frontmatter. No REQ-ID absence is reported as a gap, per the task brief.

## Goal Achievement

### Observable Truths (goal-backward, all 7 plans' must_haves merged)

| # | Truth | Status | Evidence |
|---|-------|--------|----------|
| 1 | One seam (`IBoardClock`) supplies the board zone to every consumer (cron + ambient reads) (D-03/D-07) | ✓ VERIFIED | `QuestBoard.Domain/Services/BoardClock.cs` resolves zone once at construction; `RecurringJobOptionsFactory.ForBoardZone(boardClock)` and all D-07 consumers (`EventSeriesService`, `GroupRepository`, `CalendarController`, `EventsController`, `SeriesController`, `DailyReminderJob`) inject the same `IBoardClock`. |
| 2 | Unresolvable zone falls back to UTC, app still boots, `IsDegraded` true, warning logged (D-04) | ✓ VERIFIED | `BoardClock.ResolveZone` catches `TimeZoneNotFoundException`/`InvalidTimeZoneException`, logs a warning, returns `(TimeZoneInfo.Utc, true)`. `BoardClockTests` + `BoardTimeZoneHealthCheckTests.Health_WithUnresolvableZone_ReturnsOkAndDegraded` pass (verified by direct test run). |
| 3 | Named health check `board-timezone` reports Degraded on fallback, `/health` stays HTTP 200 (D-04) | ✓ VERIFIED | `Program.cs:42` registers `AddCheck<BoardTimeZoneHealthCheck>("board-timezone")`; `BoardTimeZoneHealthCheck` returns `HealthCheckResult.Degraded` (never `Unhealthy`); default `MapHealthChecks` status-code mapping keeps Degraded at 200. Confirmed by passing integration test `Health_WithUnresolvableZone_ReturnsOkAndDegraded`. |
| 4 | Degraded body discloses no configured zone id, host path, or stack trace | ✓ VERIFIED | Fixed copy strings in `BoardTimeZoneHealthCheck`; `Health_WithUnresolvableZone_DoesNotDiscloseConfiguredZoneId` passes. |
| 5 | All three `RecurringJob.AddOrUpdate` calls pass a `TimeZone` so sweeps fire at board-local hour (D-03) | ✓ VERIFIED | `Program.cs` builds one `recurringJobOptions = RecurringJobOptionsFactory.ForBoardZone(boardClock)` and passes it to all three registrations (`daily-session-reminders`, `recurring-occurrence-top-up`, `calendar-subscription-retention`). `RecurringJobOptionsFactoryTests` pass. |
| 6 | `DailyReminderJob` computes "tomorrow" from the same board clock the cron is pinned to | ✓ VERIFIED | `DailyReminderJob.cs:19`: `boardClock.Today.AddDays(1).ToDateTime(...)`. |
| 7 | Stale "server local time (CET/CEST)" comments corrected in `Program.cs`/`DailyReminderJob.cs` | ✓ VERIFIED | `grep` for "server local"/"CET"/"CEST"/"LXC" across both files returns nothing; replaced with plain-language board-zone wording. |
| 8 | Every D-07 ambient clock read compared to a board-local date routes through `IBoardClock` (`EventSeriesService` 7 sites, `GroupRepository`, `CalendarController`, `EventsController`, `SeriesController`, `Series/Details.cshtml` via view model) | ✓ VERIFIED | Direct grep confirms `boardClock.Today`/`boardClock.Now` at every site; `AmbientClockSeamTests` (closed 7-file list) passes, including the positive `MentionsIBoardClock`/`MentionsModelToday` assertions that make it non-vacuous. `EventSeriesServiceTests` (new, `FakeBoardClock`-driven) and `GroupRepositoryTests` pass. |
| 9 | `EmailPreviewController`'s five `DateTime.Today` uses are deliberately untouched (cosmetic) | ✓ VERIFIED | `AmbientClockSeamTests.EmailPreviewController_StillContainsFiveCosmeticDateTimeTodayUses` pins the count at 5; confirmed by direct read of the file. |
| 10 | A reader loading `/Account/Profile` sees `CreatedAt`/`LastFetchedAt` in `<time class="local-time">` with untouched UTC `datetime` | ✓ VERIFIED | `Profile.cshtml`/`Profile.Mobile.cshtml` both call `Html.LocalTime(subscription.CreatedAt/LastFetchedAt.Value, ...)`. |
| 11 | Server-rendered text is board-zone formatted (D-05, no UTC flash, no placeholder for no-JS readers) | ✓ VERIFIED | `BuildLocalTime` converts UTC→board zone via `TimeZoneInfo.ConvertTimeFromUtc` before writing `InnerHtml`; `86-VALIDATION.md` manual check confirms server text is board time pre-hydration with no flash. |
| 12 | Converted instant carries a `title` with raw UTC at `yyyy-MM-dd HH:mm UTC`, the only place a zone designator appears (D-02) | ✓ VERIFIED | `BuildLocalTime` sets `title` to that exact format; `LocalTimeMarkupTests` pins it; `BuildWallClock` explicitly omits `title`. |
| 13 | Every real-instant render site collapses to one of 4 canonical styles; no bespoke format string remains | ✓ VERIFIED | Single `LocalTimeFormats` dictionary (`date`, `date-time`, `date-compact`, `date-time-compact`) is the only style source for `BuildLocalTime`/`BuildWallClock`; grep across all touched views shows only these four style keys passed to `Html.LocalTime`/`Html.WallClock`. |
| 14 | The four QuestLog `FinalizedDate`/`ClosedDate` coalesce sites are split, `FinalizedDate` never timezone-converted | ✓ VERIFIED | `QuestLog/Details.cshtml`, `Details.Mobile.cshtml`, `Index.cshtml`, `Index.Mobile.cshtml` all branch `FinalizedDate` → `Html.WallClock` and `ClosedDate` → `Html.LocalTime`, matching styles across the if/else (`date-time`/`date-time`, `date`/`date`). |
| 15 | `CalendarFeedWriter.cs`/`CalendarSubscriptionService.cs` provably untouched by this phase | ✓ VERIFIED | `git diff --name-only 7cb3cc37f9..HEAD -- '*CalendarFeedWriter*' '*CalendarSubscriptionService*'` returns empty. `CalendarFeedFloatingTimeGuardTests` additionally pins the floating-`DTSTART` contract structurally. |
| 16 | `Html.WallClock`/`BuildWallClock` takes no `TimeZoneInfo`/`IBoardClock` and makes no conversion call (D-01/D-06, 86-07 gap closure) | ✓ VERIFIED | `BuildWallClock(DateTime wallClock, string style)` signature has no zone parameter; no `TimeZoneInfo`/`ConvertTime` call anywhere in the method body. `WallClockMarkupTests` pins the no-`Z`/no-offset `datetime` attribute and the four canonical renders. |
| 17 | `site.js` defines exactly one style table read by both hydration passes (WR-01 review fix) | ✓ VERIFIED | `site.js` now declares a single `const TIMESTAMP_FORMATS = {...}` (line 238), read by both `hydrateLocalTimes()` (line 250) and `hydrateWallClockTimes()` (line 276). Landed in commit `afa10392`, confirmed present at HEAD. |

**Score:** 17/17 truths verified (0 present-but-behavior-unverified)

### Required Artifacts

| Artifact | Expected | Status | Details |
|----------|----------|--------|---------|
| `QuestBoard.Domain/Models/TimeZoneOptions.cs` | Code-default + config override, `IsValid()` | ✓ VERIFIED | Matches `CalendarFeedOptions` idiom; default `Europe/Amsterdam`. |
| `QuestBoard.Domain/Interfaces/IBoardClock.cs` | `TimeZone`, `IsDegraded`, `Today`, `Now` | ✓ VERIFIED | All four members present, consumed correctly everywhere. |
| `QuestBoard.Domain/Services/BoardClock.cs` | Resolve-once, fallback-to-UTC | ✓ VERIFIED | Resolved once at construction via `readonly` field. |
| `QuestBoard.Service/Extensions/HtmlHelperExtensions.cs` | `LocalTime`/`WallClock` helpers + `BuildLocalTime`/`BuildWallClock` pure functions | ✓ VERIFIED | Both pairs present; shared `LocalTimeFormats` table; correct title/no-title split. |
| `QuestBoard.Service/Extensions/RecurringJobOptionsFactory.cs` | Testable seam for Hangfire options | ✓ VERIFIED | `ForBoardZone(IBoardClock)` returns `RecurringJobOptions { TimeZone = boardClock.TimeZone }`. |
| `QuestBoard.Service/HealthChecks/BoardTimeZoneHealthCheck.cs` | Degraded (not Unhealthy) on fallback | ✓ VERIFIED | Confirmed by code + passing tests. |
| `QuestBoard.UnitTests/Architecture/AmbientClockSeamTests.cs` | Closed-list regression guard, not a rubber stamp | ✓ VERIFIED | Strips comments before scanning (real code, not narrative), has positive assertions (`MentionsIBoardClock`) so it can't pass vacuously, and a documented single exemption (`EventsController`'s `SetCancelledAsync` write) checked line-by-line rather than whole-file-skipped. |
| `QuestBoard.IntegrationTests/Controllers/WallClockUnmovedTests.cs` | Wall-clock unmoved under non-default board zone | ✓ VERIFIED | Present; passes. |
| `QuestBoard.UnitTests/Services/CalendarFeedFloatingTimeGuardTests.cs` | Structural proof feed stays floating | ✓ VERIFIED | Present; passes; planning-ID reference removed per review fix. |
| `QuestBoard.UnitTests/Extensions/WallClockMarkupTests.cs` (86-07) | Pins `BuildWallClock`'s no-zone contract | ✓ VERIFIED | Present; passes. |

### Key Link Verification

| From | To | Via | Status | Details |
|------|----|----|--------|---------|
| `Program.cs` cron registrations | `IBoardClock` | `RecurringJobOptionsFactory.ForBoardZone` | ✓ WIRED | One shared `recurringJobOptions` instance passed to all three `RecurringJob.AddOrUpdate` calls. |
| `BoardTimeZoneHealthCheck` | `IBoardClock.IsDegraded` | constructor injection | ✓ WIRED | Same DI instance the cron registrations read `TimeZone` from. |
| `docker-compose.yml` healthcheck | `/health` | `curl -f` | ✓ WIRED | Default ASP.NET Core health-check status-code mapping returns 200 for both Healthy and Degraded — confirmed by passing `Health_WithUnresolvableZone_ReturnsOkAndDegraded`. |
| Views (`_ViewImports.cshtml`, both root and `Areas/Platform`) | `Html.LocalTime`/`Html.WallClock` | `@using QuestBoard.Service.Extensions` | ✓ WIRED | Confirmed present in both import files; helpers resolve in Platform-area views (`Group/Index.cshtml`) and root views alike. |
| `site.js` hydration passes | `TIMESTAMP_FORMATS` | single shared const | ✓ WIRED | Both `hydrateLocalTimes()` and `hydrateWallClockTimes()` read the same object (post-WR-01 fix). |
| `SeriesController.Details` | `Series/Details.cshtml` | `SeriesDetailsViewModel.Today`/`TodayLabel` | ✓ WIRED | View reads `Model.Today`/`Model.TodayLabel`, never `DateTime.Today` directly; `AmbientClockSeamTests.SeriesDetailsView_MentionsModelToday` pins it. |

### Behavioral Spot-Checks / Test Execution

| Behavior | Command | Result | Status |
|----------|---------|--------|--------|
| `dotnet build` (whole solution) | `dotnet build` | 0 errors, 20 pre-existing NuGet version warnings (unrelated to this phase) | ✓ PASS |
| Phase 86 unit-test cluster (8 test classes: `AmbientClockSeamTests`, `BoardClockTests`, `RecurringJobOptionsFactoryTests`, `TimeZoneOptionsValidationTests`, `LocalTimeMarkupTests`, `WallClockMarkupTests`, `CalendarFeedFloatingTimeGuardTests`, `EventSeriesServiceTests`) | `dotnet test QuestBoard.UnitTests --filter "FullyQualifiedName~<names>"` | 61 passed, 0 failed | ✓ PASS |
| Phase 86 integration-test cluster (`WallClockUnmovedTests`, `LocalTimeRenderTests`, `SiteJsHydrationTests`, `BoardTimeZoneHealthCheckTests`) | `dotnet test QuestBoard.IntegrationTests --filter "FullyQualifiedName~<names>"` | 22 passed, 0 failed | ✓ PASS |
| `CalendarFeedWriter.cs`/`CalendarSubscriptionService.cs` absent from phase diff | `git diff --name-only 7cb3cc37f9..HEAD -- '*CalendarFeedWriter*' '*CalendarSubscriptionService*'` | empty output | ✓ PASS |
| No planning-ID/phase references leaked into any file touched by this phase | `git diff --name-only 7cb3cc37f9..HEAD` piped through a `D-0[0-9]\|T-86-\|86-0[0-9]\|Phase 86\|REQ-` grep, excluding `.planning/` | 0 matches | ✓ PASS |
| No debt markers (`TBD`/`FIXME`/`XXX`/`TODO`/`HACK`/`PLACEHOLDER`) in phase-touched files | same file list, grepped | 0 matches | ✓ PASS |

### Code Review Findings — Fix Verification

| Finding | Fix Claimed | Verified Landed |
|---------|-------------|------------------|
| WR-01: two independent client-side style tables could drift | Hoisted to one `TIMESTAMP_FORMATS` const in `site.js`, plus a `SiteJsHydrationTests` fact pinning exactly one table literal | ✓ Confirmed at HEAD (`site.js:238`), commit `afa10392` |
| IN-01: planning-ID references in `CalendarFeedFloatingTimeGuardTests.cs`/`WallClockUnmovedTests.cs`/`LocalTimeMarkupTests.cs` | Reworded to plain language | ✓ Confirmed — grep for `T-86-10`/`86-07`/`D-0x` across all listed test files returns nothing, commits `afa10392` + `8c2d670d` |
| IN-02: wall-clock hydration DST/offset behavior only proven by static analysis | Accepted as optional / no existing JS execution harness | N/A — informational, no fix required or claimed |

### Anti-Patterns Found

None blocking. No `TBD`/`FIXME`/`XXX`/`TODO`/`HACK`/`PLACEHOLDER` markers in any file this phase touched; no stray planning-ID references in source.

### Notable Finding — Pre-Existing Ambient-Clock Pattern Outside This Phase's Declared Scope (non-blocking)

A broader grep across `QuestBoard.Domain`/`QuestBoard.Repository`/`QuestBoard.Service` (per the
task's specific check #1) surfaced a **different, pre-existing** instance of the same bug class
D-07 fixes — an ambient `DateTime.UtcNow` read compared against a board-local wall-clock date —
that was **not** included in `86-CONTEXT.md`'s locked D-07 site list and **not** touched by any of
the 7 plans in this phase:

- `QuestBoard.Domain/Services/QuestService.cs:183` (`GetCompletedQuestsAsync`)
- `QuestBoard.Repository/QuestRepository.cs:59,70` (`GetQuestsWithSignupsAsync`, `GetQuestsWithSignupsForRoleAsync`, via `oneDayAgo = DateTime.UtcNow.AddDays(-1)`)
- `QuestBoard.Service/Controllers/QuestBoard/QuestLogController.cs:45,93,122` (`Details`, `EditRecap` guards)
- `QuestBoard.Service/Views/Quest/_QuestCard.cshtml:9`, `Quest/Details.cshtml:781`, `Quest/Manage.cshtml:709` (the "Done" vs. "Finalized" status-badge cutoff)

All of these compare `FinalizedDate.Value.Date` (wall-clock, board-local by intent) against
`DateTime.UtcNow.AddDays(-1).Date` to decide whether a quest has moved from "Finalized" to "Done"
status, or from "active" to the quest log. Between midnight and roughly 02:00 board time this can
misclassify a quest's status for a few hours — the same species of bug `EventSeriesService`/
`GroupRepository` had, just in an unrelated feature area (quest status display, not event series
or job scheduling).

**Why this does not block Phase 86:**
- It is not part of any plan's `must_haves` (truths, artifacts, or key_links) — nothing in this
  phase's contract was broken.
- `86-CONTEXT.md`'s D-07 decision explicitly enumerates a closed list of files
  (`EventSeriesService`, `CalendarController`, `EventsController`, `SeriesController`,
  `GroupRepository`, `DailyReminderJob`); this pattern was apparently missed during
  `86-RESEARCH.md`'s discovery pass rather than deliberately deferred (no mention in
  `86-RESEARCH.md`, `86-CONTEXT.md`, or `86-DISCUSSION-LOG.md`).
- It does not affect either half of the phase goal: it doesn't move a rendered timestamp and it
  doesn't affect nightly sweep timing (it's an on-request UI classification, not a Hangfire job).
- `AmbientClockSeamTests`' closed 7-file list correctly does not claim to catch this — it
  self-documents "adding an eighth ambient-clock call site anywhere else in the codebase is not
  caught by this test."

**Recommendation:** file as a follow-up item (its own small phase or an addition to a future
quest-status phase) rather than reopening Phase 86 — it is pre-existing tech debt of the same
shape D-07 fixed elsewhere, not a regression this phase introduced.

### Requirements Coverage

No REQ-IDs exist for Phase 86 by design (confirmed: `grep "Phase 86" .planning/REQUIREMENTS.md`
returns nothing). Traceability instead runs on CONTEXT.md's D-01…D-07, all of which are addressed
above in the Observable Truths table (D-01 via wall-clock-untouched checks throughout, D-02 via
title-attribute truth #12, D-03/D-04 via truths #1–7, D-05 via truth #11, D-06 via truths #12–14
and #16, D-07 via truths #8–9).

### Human Verification Required

None. `86-06`'s blocking human-verification checkpoint (Task 4) is resolved per `86-VALIDATION.md`:
all three manual-only behaviors (container tzdata/`Europe/Amsterdam` resolution, no-flash-of-UTC
across a spoofed non-board viewer zone plus a mobile/locale backstop, and calendar-feed
byte-identity) carry measured PASS results with concrete evidence, not narrative claims. The
QuestLog format-asymmetry the operator flagged at that checkpoint was subsequently closed by
gap-closure plan 86-07, verified live per `86-VALIDATION.md`'s final section.

### Gaps Summary

No gaps. All 17 merged must-have truths across the 7 plans verify against the actual codebase, not
just against SUMMARY.md narrative. The phase's central invariant — a wall-clock value must never
reach a timezone-conversion path — holds structurally: `BuildWallClock` has no `TimeZoneInfo`/
`IBoardClock` parameter at all (a compile-time guarantee, not a runtime check), `CalendarFeedWriter`/
`CalendarSubscriptionService` are byte-for-byte absent from the phase's diff, and the four QuestLog
coalesce sites that mixed both categories are correctly split with matching styles. The code-review
Warning (client-side style-table duplication) and both Info findings were addressed in follow-up
commits `afa10392`/`8c2d670d`, confirmed landed at HEAD. One notable pre-existing bug of the same
shape was found outside this phase's declared scope (see above) — recommended as a follow-up, not
a Phase 86 gap.

---

_Verified: 2026-09-20_
_Verifier: Claude (gsd-verifier)_
