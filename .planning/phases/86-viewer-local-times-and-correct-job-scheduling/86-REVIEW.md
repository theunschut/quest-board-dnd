---
phase: 86-viewer-local-times-and-correct-job-scheduling
reviewed: 2026-09-20T17:38:40Z
depth: standard
files_reviewed: 46
files_reviewed_list:
  - QuestBoard.Domain/Extensions/ServiceExtensions.cs
  - QuestBoard.Domain/Interfaces/IBoardClock.cs
  - QuestBoard.Domain/Models/TimeZoneOptions.cs
  - QuestBoard.Domain/Services/BoardClock.cs
  - QuestBoard.Domain/Services/EventSeriesService.cs
  - QuestBoard.Repository/GroupRepository.cs
  - QuestBoard.Service/Controllers/Events/EventsController.cs
  - QuestBoard.Service/Controllers/Events/SeriesController.cs
  - QuestBoard.Service/Controllers/QuestBoard/CalendarController.cs
  - QuestBoard.Service/Extensions/HtmlHelperExtensions.cs
  - QuestBoard.Service/Extensions/RecurringJobOptionsFactory.cs
  - QuestBoard.Service/HealthChecks/BoardTimeZoneHealthCheck.cs
  - QuestBoard.Service/Jobs/DailyReminderJob.cs
  - QuestBoard.Service/Program.cs
  - QuestBoard.Service/Properties/AssemblyInfo.cs
  - QuestBoard.Service/ViewModels/SeriesViewModels/SeriesDetailsViewModel.cs
  - QuestBoard.Service/wwwroot/js/site.js
  - QuestBoard.Service/Views/_ViewImports.cshtml
  - QuestBoard.Service/Areas/Platform/Views/_ViewImports.cshtml
  - QuestBoard.Service/Areas/Platform/Views/Group/Index.cshtml
  - QuestBoard.Service/Areas/Platform/Views/Group/Index.Mobile.cshtml
  - QuestBoard.Service/Views/Account/Profile.cshtml
  - QuestBoard.Service/Views/Account/Profile.Mobile.cshtml
  - QuestBoard.Service/Views/Admin/EmailStats.cshtml
  - QuestBoard.Service/Views/Contacts/Details.cshtml
  - QuestBoard.Service/Views/Contacts/Details.Mobile.cshtml
  - QuestBoard.Service/Views/Quest/Details.cshtml
  - QuestBoard.Service/Views/Quest/Manage.cshtml
  - QuestBoard.Service/Views/Quest/_QuestCard.cshtml
  - QuestBoard.Service/Views/QuestLog/Details.cshtml
  - QuestBoard.Service/Views/QuestLog/Details.Mobile.cshtml
  - QuestBoard.Service/Views/QuestLog/Index.cshtml
  - QuestBoard.Service/Views/QuestLog/Index.Mobile.cshtml
  - QuestBoard.Service/Views/Series/Details.cshtml
  - QuestBoard.Service/Views/Shop/Index.cshtml
  - QuestBoard.Service/Views/ShopManagement/Index.cshtml
  - QuestBoard.IntegrationTests/Controllers/BoardTimeZoneHealthCheckTests.cs
  - QuestBoard.IntegrationTests/Controllers/LocalTimeRenderTests.cs
  - QuestBoard.IntegrationTests/Controllers/WallClockUnmovedTests.cs
  - QuestBoard.IntegrationTests/Mobile/SiteJsHydrationTests.cs
  - QuestBoard.UnitTests/Architecture/AmbientClockSeamTests.cs
  - QuestBoard.UnitTests/Extensions/LocalTimeMarkupTests.cs
  - QuestBoard.UnitTests/Extensions/TimeZoneOptionsValidationTests.cs
  - QuestBoard.UnitTests/Extensions/WallClockMarkupTests.cs
  - QuestBoard.UnitTests/Helpers/FakeBoardClock.cs
  - QuestBoard.UnitTests/Repository/EventSeriesMaterializationTests.cs
  - QuestBoard.UnitTests/Repository/GroupRepositoryTests.cs
  - QuestBoard.UnitTests/Services/BoardClockTests.cs
  - QuestBoard.UnitTests/Services/CalendarFeedFloatingTimeGuardTests.cs
  - QuestBoard.UnitTests/Services/DailyReminderJobTests.cs
  - QuestBoard.UnitTests/Services/EventSeriesServiceTests.cs
  - QuestBoard.UnitTests/Services/RecurringJobOptionsFactoryTests.cs
findings:
  critical: 0
  warning: 1
  info: 2
  total: 3
status: issues
---

# Phase 86: Code Review Report

**Reviewed:** 2026-09-20T17:38:40Z
**Depth:** standard
**Files Reviewed:** 52 (listed above; the 52nd, `CalendarFeedFloatingTimeGuardTests.cs`, is covered under Info-01 below along with the rest of the test list)
**Status:** issues_found

## Summary

This phase's central risk — confusing a real UTC instant with a floating wall-clock value —
is handled correctly everywhere it was checked. Traced every real-instant/wall-clock render
site the plan set out to convert (QuestLog's four `FinalizedDate`/`ClosedDate` coalesces,
`Quest/Manage`, `Quest/Details`, `Quest/_QuestCard`, `Shop/Index`, `ShopManagement/Index`,
`Admin/EmailStats`, `Contacts/Details`, `Platform/Group/Index`, `Account/Profile`, all with
their `.Mobile` twins) and confirmed each renders through the correct helper with the correct
classification, with no cross-contamination. `CalendarFeedWriter.cs` and
`CalendarSubscriptionService.cs` are confirmed byte-for-byte untouched (`git diff` empty), and
a dedicated `CalendarFeedFloatingTimeGuardTests.cs` pins the floating-`DTSTART` contract with a
real `TimeZoneInfo`/`IBoardClock` held in scope to prove the writer is structurally immune, not
merely untested.

`Html.WallClock`/`BuildWallClock` genuinely cannot apply a zone conversion — the method takes
no `TimeZoneInfo`/`IBoardClock` parameter at all, which is a stronger guarantee than a runtime
check. `site.js`'s `hydrateWallClockTimes()` anchors through `Date.UTC(...)` and formats with
`timeZone: 'UTC'`, which is airtight against any viewer offset (whole-hour, half-hour, or
45-minute) and any DST boundary, because the viewer's zone never enters either the anchoring or
the formatting step — this holds independent of what the viewer's local offset actually is.
Both hydration passes write via `textContent` (no `innerHTML`/injection risk) and wrap each
element in an independent `try`/`catch`, so one malformed `datetime` cannot abort the loop for
the rest of the page — confirmed against the actual `forEach`+`try`/`catch` shape in the code,
not just the accompanying comment.

`BoardTimeZoneHealthCheck` returns `Degraded` (never `Unhealthy`) on a UTC fallback, stays HTTP
200, and its description strings are fixed copy that never interpolates the configured zone id,
a host path, or an exception message — verified against both the implementation and its three
integration tests, one of which explicitly asserts the configured garbage zone id does not
appear in the response body. All three `RecurringJob.AddOrUpdate` calls in `Program.cs` use the
non-deprecated four-argument overload (`RecurringJobOptions`, not a bare `TimeZoneInfo`), and
`BoardClock` never throws on an unresolvable id (it degrades and logs), so nothing in the
startup path can crash the app over a timezone typo. `EventSeriesService.cs` and
`GroupRepository.cs` have zero remaining ambient `DateTime.Now`/`UtcNow`/`Today` reads compared
against a board-local date — confirmed by direct grep and cross-checked against the new
`AmbientClockSeamTests` architecture-level regression guard, which pins all seven D-07 sites by
source-scanning the actual files on disk.

One real maintainability gap was found (Warning, below) in the client-side format-table
duplication the phase's own design introduced. Two Info-level items round out the review: a
CLAUDE.md "no planning-ID in comments" violation in two test files, and a test-coverage
observation about the wall-clock hydration path having no executable (only static/file-content)
JS coverage. Given the strength of the existing regression net — the closed-list architecture
test, the two-zone "never moved" integration tests, and the explicit non-default-zone guard
tests — none of the specific concerns raised for this review turned up a functional defect.

## Warnings

### WR-01: Client-side style tables can drift into three independently-maintained copies

**File:** `QuestBoard.Service/wwwroot/js/site.js:237-242` and `:272-277`
**Issue:** The server side correctly shares one `LocalTimeFormats` dictionary between
`BuildLocalTime` and `BuildWallClock` in `HtmlHelperExtensions.cs`, so the two C# rendering
paths cannot diverge on what a style name means. But `site.js` declares two textually
independent, byte-identical copies of the same table — `localTimeFormats` inside
`hydrateLocalTimes()` and `wallClockFormats` inside `hydrateWallClockTimes()` — plus a comment
on each asking a future editor to keep it "in sync with the server-side format table." Nothing
enforces that. `SiteJsHydrationTests` checks that each function's body *contains* the four style
key substrings, but never diffs `localTimeFormats` against `wallClockFormats`, and no test
compares either JS table against the C# `LocalTimeFormats` dictionary. If a fifth style is added
to the C# table and to only one of the two JS tables (a very plausible mistake, since
`BuildLocalTime`/`BuildWallClock` share one table and an editor could reasonably assume the JS
side does too), the affected style would render correctly server-side on first paint and then
silently regress to a different granularity — or the "unknown style, leave server text alone"
branch — the instant client-side hydration runs. This does not cause a wrong *time* (the
correctness invariant this phase cares about most), but it is exactly the kind of two-sided
table this phase explicitly called out as a risk, and it currently only has one owner enforcing
consistency (code review) rather than a test.
**Fix:** Either (a) factor `localTimeFormats`/`wallClockFormats` in `site.js` into one shared
`const timeFormats = {...}` object referenced by both hydration functions (mirroring the C#
side's single-table sharing), or (b) add a `SiteJsHydrationTests` fact that extracts both JS
table literals and asserts they are textually identical, so a future one-sided edit fails a test
instead of shipping silently. Option (a) is the smaller, more direct fix and matches the
pattern already used server-side.

## Info

### IN-01: Planning-ID references in test-file comments violate CLAUDE.md's "Code Comments" rule

**File:** `QuestBoard.UnitTests/Services/CalendarFeedFloatingTimeGuardTests.cs:12` (references
`T-86-10`) and `QuestBoard.IntegrationTests/Controllers/WallClockUnmovedTests.cs:135`
(references `86-07's gap closure`)
**Issue:** CLAUDE.md's "Code Comments" section forbids phase/plan/requirement/review-finding IDs
in comments anywhere in source, specifically because they go stale once the phase closes. Both
occurrences are in test files (out of the review's primary blast radius, and they do not affect
test reliability — the tests themselves are well-constructed and correct), but the instruction
set for this review explicitly asked for a CLAUDE.md compliance check across the phase's diff,
and production code in this phase was clean of this pattern (verified by a targeted grep across
`QuestBoard.Domain`, `QuestBoard.Repository`, and `QuestBoard.Service`). These two are the only
occurrences anywhere in the phase's diff.
**Fix:** Reword to describe the invariant in plain language, e.g. replace "per this phase's own
T-86-10 mitigation" with "the invariant that CalendarFeedWriter/CalendarSubscriptionService must
never appear in a timezone-related change's diff," and replace "86-07's gap closure" with "the
QuestLog Details finalized branch."

### IN-02: Wall-clock hydration's DST/half-hour-offset behavior is only proven by static analysis, not execution

**File:** `QuestBoard.IntegrationTests/Mobile/SiteJsHydrationTests.cs` (whole file, wall-clock
facts at lines covering `hydrateWallClockTimes`)
**Issue:** All coverage of `hydrateWallClockTimes()` is file-content/regex assertion over the
JS source text (e.g., "the function body contains `Date.UTC(`", "no bare `new Date(y, m, d, ...)`
constructor appears") — there is no test that actually executes the JavaScript (via a headless
browser or a JS runtime harness) with a simulated viewer timezone such as `Asia/Kathmandu`
(+05:45) or across a real DST transition instant, to observe the rendered output end to end.
The underlying mechanism is sound by inspection — anchoring via `Date.UTC(...)` and formatting
with `timeZone: 'UTC'` makes the viewer's local offset structurally unable to enter either step
— so this is not a functional gap, only a coverage gap: nothing would catch a regression that
*looked* like it matched the static pattern (e.g., a copy-paste that anchors correctly but drops
`timeZone: 'UTC'` from only one of the two `Intl.DateTimeFormat` call sites) without a human or
an executable test actually running the function.
**Fix:** Optional given the codebase has no existing JS execution test harness (this would be a
new test-infrastructure investment, not a small addition) — if one is ever introduced for other
reasons, add an executed case pinning `hydrateWallClockTimes()`'s output for a fixed wall-clock
input under both a positive and a fractional-offset viewer timezone.

---

_Reviewed: 2026-09-20T17:38:40Z_
_Reviewer: Claude (gsd-code-reviewer)_
_Depth: standard_
