---
phase: 86-viewer-local-times-and-correct-job-scheduling
plan: 04
subsystem: views
tags: [timezone, razor, html-localtime, quest-log, contacts, platform-group]

requires: [86-01]
provides:
  - QuestLog Details/Index (desktop + mobile) converted to Html.LocalTime, with the FinalizedDate/ClosedDate coalesce split into two branches
  - Contacts Details note byline converted to Html.LocalTime, ContactNote.UpdatedAt untouched
  - Platform Group Index (desktop + mobile) converted to Html.LocalTime, retiring the one raw-ISO "yyyy-MM-dd" render
affects: [86-06]

actuals:
  tokens: 2377
  tasks: 3
  commits: 3

tech-stack:
  added: []
  patterns:
    - "Coalesce split: a nullable string local or single .ToString() call over (WallClockDate ?? RealInstantDate) cannot survive conversion to Html.LocalTime (IHtmlContent, not string) -- split into an if/else-if/else branch at the render position, one branch per meaning, no shared local and no shared format string."

key-files:
  created: []
  modified:
    - QuestBoard.Service/Views/QuestLog/Details.cshtml
    - QuestBoard.Service/Views/QuestLog/Details.Mobile.cshtml
    - QuestBoard.Service/Views/QuestLog/Index.cshtml
    - QuestBoard.Service/Views/QuestLog/Index.Mobile.cshtml
    - QuestBoard.Service/Views/Contacts/Details.cshtml
    - QuestBoard.Service/Views/Contacts/Details.Mobile.cshtml
    - QuestBoard.Service/Areas/Platform/Views/Group/Index.cshtml
    - QuestBoard.Service/Areas/Platform/Views/Group/Index.Mobile.cshtml

key-decisions:
  - "QuestLog Index.cshtml's questDate string local was deleted rather than adapted, because Html.LocalTime returns IHtmlContent and a string local cannot hold either branch's output -- the decision moved to the render position inside the <span>, per the plan's explicit instruction."
  - "The finalized-quest branch in all four coalesce sites keeps its pre-existing format string unchanged and never passes through Html.LocalTime -- FinalizedDate is the same floating-local-time value the calendar feed emits, and converting it would move every subscriber's game night by the board's UTC offset."

requirements-completed: [D-01, D-02, D-05, D-06]

coverage:
  - id: D1
    description: "QuestLog Details CreatedAt and split Completed On coalesce, desktop + mobile"
    requirement: "D-06"
    verification:
      - kind: integration
        ref: "dotnet test QuestBoard.IntegrationTests --filter FullyQualifiedName~QuestLog (19 passed)"
        status: pass
      - kind: manual-grep
        ref: "acceptance_criteria greps for Html.LocalTime call counts, preserved format strings, absent zone labels"
        status: pass
    human_judgment: false
  - id: D2
    description: "QuestLog Index split Completed coalesce, desktop + mobile, Unknown Date fallback preserved"
    requirement: "D-06"
    verification:
      - kind: integration
        ref: "dotnet test QuestBoard.IntegrationTests --filter FullyQualifiedName~QuestLog (19 passed)"
        status: pass
      - kind: manual-grep
        ref: "acceptance_criteria greps confirming questDate removed, Unknown Date preserved once per file"
        status: pass
    human_judgment: false
  - id: D3
    description: "Contacts note byline and Platform Group list converted, ContactNote.UpdatedAt untouched, raw-ISO outlier retired"
    requirement: "D-01, D-02, D-05"
    verification:
      - kind: integration
        ref: "dotnet test QuestBoard.IntegrationTests --filter \"FullyQualifiedName~Contacts|FullyQualifiedName~PlatformArea\" (102 passed)"
        status: pass
      - kind: manual-grep
        ref: "acceptance_criteria greps for Html.LocalTime call counts, (edited) marker preserved, yyyy-MM-dd retired"
        status: pass
    human_judgment: false

duration: 25min
completed: 2026-09-20
status: complete
---

# Phase 86 Plan 04: QuestLog, Contacts, and Platform Group Local Times Summary

**Converted eight QuestLog/Contacts/Platform-Group view files (four desktop, four mobile twins) to Html.LocalTime, splitting the four FinalizedDate/ClosedDate coalesce sites so the wall-clock game night byte never touches the conversion path.**

## Performance

- **Duration:** 25 min
- **Started:** 2026-09-20T12:41:00Z (approx.)
- **Completed:** 2026-09-20T13:06:06Z
- **Tasks:** 3
- **Files modified:** 8

## Accomplishments
- `QuestLog/Details.cshtml` and its mobile twin: `CreatedAt` converted to `Html.LocalTime(..., "date")`; the `Completed On:` coalesce split into a three-way branch -- finalized keeps `"MMMM dd, yyyy 'at' h:mm tt"` verbatim with no helper call, closed-only renders through `Html.LocalTime(..., "date-time")`, neither present renders nothing.
- `QuestLog/Index.cshtml` and its mobile twin: the `questDate` string local (which could not survive the split, since `Html.LocalTime` returns `IHtmlContent`) was removed and the same three-way branch moved to the render position, preserving each layout's own pre-existing finalized-branch format string (`"MMMM dd, yyyy"` desktop, `"MMM dd, yyyy"` mobile) and the literal `"Unknown Date"` fallback.
- `Contacts/Details.cshtml` and its mobile twin: the note byline's `CreatedAt` converted to `Html.LocalTime(..., "date-time")`; `ContactNote.UpdatedAt`'s `!= null` check and its `" (edited)"` literal left untouched, per the design contract.
- `Areas/Platform/Views/Group/Index.cshtml` and its mobile twin: `item.CreatedAt` converted to `Html.LocalTime(..., "date")`, retiring the one raw-ISO `"yyyy-MM-dd"` outlier in the entire view tree in favour of the canonical short-date style.
- Full test suite verified green after all three tasks: 548 unit + 847 integration tests, 0 failures.

## Task Commits

Each task was committed atomically:

1. **Task 1: QuestLog Details -- convert CreatedAt and split the Completed-On coalesce (desktop + mobile twin)** - `996d1581` (feat)
2. **Task 2: QuestLog Index -- split the Completed coalesce on both layouts** - `a8d2f88c` (feat)
3. **Task 3: Contact notes and the Platform group list (desktop + mobile twins)** - `efba9577` (feat)

## Files Created/Modified
- `QuestBoard.Service/Views/QuestLog/Details.cshtml` - `CreatedAt` -> `Html.LocalTime(..., "date")`; `Completed On:` split into finalized/closed/none branches
- `QuestBoard.Service/Views/QuestLog/Details.Mobile.cshtml` - Identical change on the mobile twin
- `QuestBoard.Service/Views/QuestLog/Index.cshtml` - Removed the `questDate` string local; moved the finalized/closed/"Unknown Date" branch to the render position
- `QuestBoard.Service/Views/QuestLog/Index.Mobile.cshtml` - Identical branch inline in the mobile twin's `<small>`, each layout's own pre-existing finalized format string preserved
- `QuestBoard.Service/Views/Contacts/Details.cshtml` - Note byline `CreatedAt` -> `Html.LocalTime(..., "date-time")`; `(edited)` marker untouched
- `QuestBoard.Service/Views/Contacts/Details.Mobile.cshtml` - Identical change on the mobile twin
- `QuestBoard.Service/Areas/Platform/Views/Group/Index.cshtml` - `item.CreatedAt` -> `Html.LocalTime(..., "date")`, retiring the raw-ISO render
- `QuestBoard.Service/Areas/Platform/Views/Group/Index.Mobile.cshtml` - Identical change on the mobile twin

## Decisions Made
- The `questDate` string local in `QuestLog/Index.cshtml` was deleted rather than kept as a helper-of-some-kind, because `Html.LocalTime` returns `IHtmlContent`, which cannot share a code path with a plain `string` -- the plan's own instruction was followed exactly: move the decision to the render position.
- Each of the four coalesce sites' finalized branch keeps its pre-existing format string completely unchanged (no shared local, no shared format string with the closed branch), because `FinalizedDate` is the same value the calendar feed emits as floating local time; passing it through any conversion would silently shift every subscriber's game night by the board's UTC offset.

## Deviations from Plan

None - plan executed exactly as written. All acceptance criteria greps (call counts, preserved format strings, absent zone labels, `questDate`/raw-ISO removal) passed on first implementation; no auto-fixes were required.

## Issues Encountered

None.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness
- All five real-instant render sites plus their mobile twins named in this plan's `must_haves.truths` are now wrapped in `Html.LocalTime`.
- The four coalesce sites are split; each finalized branch keeps its format string verbatim and never reaches the helper, verified by grep on all four file pairs.
- `ContactNote.UpdatedAt` is confirmed still a bare null check appending `" (edited)"`.
- No visible text on any of the eight touched files carries a timezone label (verified by grep across all eight files together).
- Ready for 86-06's human-verification checkpoint, which is expected to flag the accepted cosmetic asymmetry between a finalized quest's and a closed-only quest's "Completed" date format as a copy difference, not a time bug (per this plan's `<recorded_correction>` §B).

## Self-Check: PASSED

All 8 files listed in Files Created/Modified verified present on disk. All 3 commit hashes (`996d1581`, `a8d2f88c`, `efba9577`) verified present in `git log --oneline`.

---
*Phase: 86-viewer-local-times-and-correct-job-scheduling*
*Completed: 2026-09-20*
