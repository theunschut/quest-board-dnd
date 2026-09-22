---
phase: 86-viewer-local-times-and-correct-job-scheduling
plan: 07
subsystem: ui
tags: [timezone, razor, wall-clock, quest-log, vanilla-js, intl-datetimeformat]

requires:
  - phase: 86-01
    provides: "Html.LocalTime/BuildLocalTime style table and TagBuilder markup pattern, and site.js's single DOMContentLoaded listener that hydrateWallClockTimes() now shares"
  - phase: 86-04
    provides: "The four QuestLog FinalizedDate/ClosedDate coalesce sites, already split into if/else-if branches so the finalized branch could be retargeted without touching the closed branch"
  - phase: 86-06
    provides: "The human-verification checkpoint where the operator flagged the cosmetic format mismatch between a finalized quest's static text and a closed quest's hydrated text, and chose locale-aware wall-clock rendering as the fix"
provides:
  - "Html.WallClock/BuildWallClock: a zone-less sibling to Html.LocalTime for floating local-time values, reusing the same style table"
  - "hydrateWallClockTimes(): a site.js pass that re-anchors parsed datetime components through Date.UTC(...) and formats with timeZone: 'UTC', making the viewer's own zone structurally unable to shift the displayed digits"
  - "All four QuestLog FinalizedDate branches (Details/Index, desktop + mobile) converted to Html.WallClock, matching their ClosedDate sibling's style"
affects: []

actuals:
  tokens: 5650
  tasks: 3
  commits: 3

tech-stack:
  added: []
  patterns:
    - "Html.WallClock/BuildWallClock: same TagBuilder shape and LocalTimeFormats style table as Html.LocalTime, but with no TimeZoneInfo/IBoardClock parameter, class=\"wall-clock\" instead of \"local-time\", a Z/offset-free datetime attribute, and no title attribute -- the type signature itself makes a timezone conversion impossible to apply."
    - "Conversion-proof client-side formatting: parse datetime components with a regex, re-anchor through Date.UTC(...), format with { timeZone: 'UTC' } -- never new Date(y, m, d, h, min), which builds in the viewer's local zone and can shift across a DST gap."

key-files:
  created:
    - QuestBoard.UnitTests/Extensions/WallClockMarkupTests.cs
  modified:
    - QuestBoard.Service/Extensions/HtmlHelperExtensions.cs
    - QuestBoard.Service/wwwroot/js/site.js
    - QuestBoard.Service/Views/QuestLog/Details.cshtml
    - QuestBoard.Service/Views/QuestLog/Details.Mobile.cshtml
    - QuestBoard.Service/Views/QuestLog/Index.cshtml
    - QuestBoard.Service/Views/QuestLog/Index.Mobile.cshtml
    - QuestBoard.IntegrationTests/Mobile/SiteJsHydrationTests.cs
    - QuestBoard.IntegrationTests/Controllers/WallClockUnmovedTests.cs

key-decisions:
  - "BuildWallClock takes no TimeZoneInfo/IBoardClock parameter at all, rather than accepting one and simply not calling TimeZoneInfo.ConvertTimeFromUtc. A wall-clock value has no zone to convert from, so the absence of the parameter is the safety argument, not a discipline the method has to maintain internally."
  - "The client-side anchoring comment describing the forbidden pattern was reworded to avoid the literal substring \"new Date(\" followed by non-Date.UTC text, because the SiteJsHydrationTests regex assertion (which pins that every \"new Date(\" call in the function is immediately followed by \"Date.UTC(\") matched the cautionary comment itself on first attempt. Reworded to describe the forbidden shape in prose (\"separate local year/month/day/hour/minute arguments\") instead of writing the literal anti-pattern call."

patterns-established:
  - "A zero-timezone-dependency rendering helper: when a value must never be converted, remove the conversion input from the method signature entirely rather than adding a runtime guard."

requirements-completed: [D-01, D-06]

coverage:
  - id: D1
    description: "Html.WallClock/BuildWallClock renders a floating wall-clock value as <time class=\"wall-clock\" data-style=... datetime=(no Z/offset)> with no title attribute, for all four canonical styles, in invariant culture"
    requirement: "D-06"
    verification:
      - kind: unit
        ref: "QuestBoard.UnitTests/Extensions/WallClockMarkupTests.cs (12 facts: class name, datetime shape, no title, all 4 styles' text, unrecognised-style throw, well-formedness)"
        status: pass
    human_judgment: false
  - id: D2
    description: "hydrateWallClockTimes() runs inside site.js's existing single DOMContentLoaded listener, scoped to time.wall-clock[datetime], anchors parsed components through Date.UTC(...) and formats with timeZone: 'UTC', never a bare local-component Date constructor, with per-element try/catch isolation"
    requirement: "D-06"
    verification:
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Mobile/SiteJsHydrationTests.cs (5 new facts: declaration+invocation, scoped selector, UTC anchor+format, no bare local-component constructor, try/catch)"
        status: pass
    human_judgment: false
  - id: D3
    description: "All four QuestLog FinalizedDate branches (Details/Index, desktop + mobile) render through Html.WallClock with the same style name as their ClosedDate sibling, and no other view's FinalizedDate render site changed"
    requirement: "D-01"
    verification:
      - kind: manual-grep
        ref: "grep -rn FinalizedDate.Value.ToString QuestBoard.Service/Views/QuestLog/ (empty) and the same grep outside QuestLog (unchanged: Quest/Index.cshtml, Quest/Index.Mobile.cshtml)"
        status: pass
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Controllers/WallClockUnmovedTests.cs#FinalizedQuestGameNight_OnQuestLogDetails_RendersTheStoredHourWithNoZAndNoTitle, #FinalizedQuestGameNight_NeverShowsTheUtcPlusTwoShiftedHour"
        status: pass
    human_judgment: false

duration: 12min
completed: 2026-09-20
status: complete
---

# Phase 86 Plan 07: QuestLog Wall-Clock Locale Rendering Gap Closure Summary

**A zone-less Html.WallClock helper plus a Date.UTC-anchored site.js hydration pass, converting all four QuestLog FinalizedDate branches to render in the viewer's own locale wording without ever touching a timezone.**

## Performance

- **Duration:** 12 min (approx., based on commit timestamps)
- **Started:** 2026-09-20T17:15:00Z (approx.)
- **Completed:** 2026-09-20T17:24:01Z
- **Tasks:** 3
- **Files modified:** 9

## Accomplishments
- `Html.WallClock`/`BuildWallClock` added to `HtmlHelperExtensions.cs` as a sibling to `Html.LocalTime`, reusing the exact same `LocalTimeFormats` style table so the two rendering paths cannot drift apart on what a style name means. The method takes no `TimeZoneInfo`/`IBoardClock` parameter at all -- the type signature itself makes a timezone conversion structurally impossible, not just avoided by convention.
- The emitted markup differs from `Html.LocalTime`'s in the three ways the plan required: `class="wall-clock"` (so the existing `local-time` hydration pass never touches it), a `datetime` attribute with no `Z`/offset (honest encoding for a value with no UTC instant), and no `title` attribute (a UTC tooltip would be a lie).
- `hydrateWallClockTimes()` added to `site.js`'s existing single `DOMContentLoaded` listener alongside `hydrateLocalTimes()`. It parses the `datetime` attribute's components with a regex, re-anchors them through `Date.UTC(...)`, and formats with `Intl.DateTimeFormat(undefined, { ...options, timeZone: 'UTC' })` -- anchoring and formatting both in UTC makes the displayed digits arithmetically identical to the parsed ones, so the viewer's own zone cannot enter the calculation at all.
- All four QuestLog `FinalizedDate` branches (`Details.cshtml`, `Details.Mobile.cshtml`, `Index.cshtml`, `Index.Mobile.cshtml`) converted to `Html.WallClock`, each using the same style name (`"date-time"` or `"date"`) its `ClosedDate` sibling branch already uses -- closing the cosmetic gap the 86-06 operator checkpoint flagged.
- Full test suite green after all three tasks: 588 unit + 860 integration tests, 0 failures. The pre-existing `WallClockUnmovedTests`, `CalendarFeedFloatingTimeGuardTests`, and `AmbientClockSeamTests` all pass untouched.

## Task Commits

Each task was committed atomically:

1. **Task 1: Add the wall-clock markup helper and its hydration pass** - `c20c089b` (feat)
2. **Task 2: Move the four QuestLog finalized branches onto the helper** - `3abf5f70` (feat)
3. **Task 3: Prove the digits did not move** - `48b4cf35` (test)

## Files Created/Modified
- `QuestBoard.Service/Extensions/HtmlHelperExtensions.cs` - Adds `BuildWallClock`/`WallClock`, `Markdown`/`LocalTime` unchanged
- `QuestBoard.Service/wwwroot/js/site.js` - Adds `hydrateWallClockTimes()`, invoked from the existing single listener alongside `hydrateLocalTimes()`
- `QuestBoard.Service/Views/QuestLog/Details.cshtml` / `Details.Mobile.cshtml` - `FinalizedDate` branch converted to `Html.WallClock(..., "date-time")`, matching the `ClosedDate` branch's style
- `QuestBoard.Service/Views/QuestLog/Index.cshtml` / `Index.Mobile.cshtml` - `FinalizedDate` branch converted to `Html.WallClock(..., "date")`, matching the `ClosedDate` branch's style; `Unknown Date` fallback untouched
- `QuestBoard.UnitTests/Extensions/WallClockMarkupTests.cs` - 12 facts pinning `BuildWallClock`'s markup contract (class, datetime shape, absent title, all 4 styles' invariant-culture text, unrecognised-style throw, well-formedness)
- `QuestBoard.IntegrationTests/Mobile/SiteJsHydrationTests.cs` - 5 new facts pinning `hydrateWallClockTimes()`'s declaration/invocation, scoped selector, UTC anchor+format, absence of a bare local-component `Date` constructor, and try/catch isolation
- `QuestBoard.IntegrationTests/Controllers/WallClockUnmovedTests.cs` - 2 new facts proving the QuestLog Details finalized branch renders the exact stored hour with no `Z`/title, and never shows the UTC+2-shifted hour a board-zone conversion would have produced

## Decisions Made
- `BuildWallClock` takes no `TimeZoneInfo`/`IBoardClock` parameter at all, rather than accepting one and simply never calling it. A wall-clock value has no zone to convert from, so the absence of the parameter from the method signature is the safety argument itself -- there is no runtime path that could apply a conversion, because there is nothing to convert with.
- The client-side comment warning against the local-component `Date` constructor was reworded away from the literal `new Date(y, m, d, h, min)` text, because `SiteJsHydrationTests`' regex assertion (every `new Date(` in the function body must be immediately followed by `Date.UTC(`) matched the cautionary comment itself on first attempt -- the comment was describing the forbidden call, not making it, but the regex could not tell the difference. Reworded to describe the forbidden shape in prose instead.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Cautionary code comment tripped its own anti-pattern regex test**
- **Found during:** Task 1 (`SiteJs_HydrateWallClockTimesBody_NeverBuildsADateFromBareLocalComponents`)
- **Issue:** The comment `// Do NOT use new Date(y, m, d, h, min) here` inside `hydrateWallClockTimes()` contains the literal substring `new Date(` not immediately followed by `Date.UTC(`, which is exactly the anti-pattern the new regex-based test was written to detect. The test failed against the comment text, not against any real code.
- **Fix:** Reworded the comment to `// Do NOT construct the Date from separate local year/month/day/hour/minute arguments` -- same warning, no literal `new Date(` substring.
- **Files modified:** `QuestBoard.Service/wwwroot/js/site.js`
- **Verification:** `SiteJs_HydrateWallClockTimesBody_NeverBuildsADateFromBareLocalComponents` passes; full `dotnet test` still green (588 unit + 860 integration).
- **Committed in:** `c20c089b` (Task 1 commit)

---

**Total deviations:** 1 auto-fixed (bug in test-pinnability, no production behavior affected)
**Impact on plan:** Comment-only wording change; the underlying conversion-proof rule (anchor and format through UTC, never a bare local-component constructor) is unchanged and now correctly pinned by the test.

## Issues Encountered

None beyond the deviation documented above.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness
- `Html.WallClock`/`BuildWallClock` is available for any future floating-local-time render site outside this plan's four-file scope, should one be discovered.
- The QuestLog "Completed On:"/"Completed:" column now renders both branches (finalized and closed) through the same locale-aware mechanism, closing the cosmetic asymmetry the 86-06 operator checkpoint flagged.
- `git diff --name-only 7cb3cc37f921ab91d7d15391c8e00ca6d216f869..HEAD | grep -E "CalendarFeedWriter|CalendarSubscriptionService"` returns nothing -- both files remain untouched, per the plan's non-regression requirement.
- No blockers. This closes the gap opened at the 86-06 checkpoint; no further plans are queued in this phase as of this SUMMARY.

## Self-Check: PASSED

All 8 files listed in Files Created/Modified plus this SUMMARY.md verified present on disk. All 3 commit hashes (`c20c089b`, `3abf5f70`, `48b4cf35`) verified present in `git log --oneline`.

---
*Phase: 86-viewer-local-times-and-correct-job-scheduling*
*Completed: 2026-09-20*
