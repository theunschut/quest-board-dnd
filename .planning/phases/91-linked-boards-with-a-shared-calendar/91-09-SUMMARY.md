---
phase: 91-linked-boards-with-a-shared-calendar
plan: 09
subsystem: calendar
tags: [linked-boards, quest-details, razor, read-only, static-guard]
status: complete

requires:
  - phase: 91-linked-boards-with-a-shared-calendar
    provides: "91-02 Details DM-only rule and own-quest re-add; 91-05 ILinkedCalendarService, linked view-model types, _LinkedDayEntries chips and Details note slot"
provides:
  - "Quest Details month models filled with linked quests, events and boards from one windowed read"
  - "_LinkedAlsoThatDay partial and its slot in _Calendar.Mobile.cshtml"
  - "LinkedBoardQuestDetailsTests (desktop and mobile) and LinkedBoardStaticGuardTests"
affects: [91-10]

tech-stack:
  added: []
  patterns:
    - "Linked content reaches a vote form only through form-free partials with their own model"
    - "Static guard reads view source on disk and reflects over linked model types"

key-files:
  created:
    - QuestBoard.Service/Views/Shared/_LinkedAlsoThatDay.cshtml
    - QuestBoard.IntegrationTests/Tests/LinkedBoardQuestDetailsTests.cs
    - QuestBoard.IntegrationTests/Tests/LinkedBoardStaticGuardTests.cs
  modified:
    - QuestBoard.Service/Controllers/QuestBoard/QuestController.cs
    - QuestBoard.Service/Views/Shared/_Calendar.Mobile.cshtml
    - QuestBoard.Service/wwwroot/css/quests.mobile.css

key-decisions:
  - "One GetForViewerAsync call spans first day of the earliest proposed-date month to first day after the latest; skipped when the quest has no proposed dates"
  - "Month models never get Events, so the board's own events stay off Details while linked ones show"
  - "Added .also-that-day + .calendar-vote-row margin because last-of-type counts divs and the vote row is a div, so the planned last-of-type rule alone would not space the last line from the vote row"

requirements-completed: []

duration: 30min
completed: 2026-10-16
actuals:
  tokens: 20000
  tasks: 3
  commits: 4
---

# Phase 91 Plan 09: Linked entries on the Quest Details calendar Summary

**Quest Details now shows the other boards' sessions where date clashes are decided: linked chips plus a read-only "Also showing" note in the desktop grid, and "Also that day" lines under each clashing proposed date on mobile, with a source-level guard that none of it can reach the vote markup.**

## Accomplishments

- `QuestController` takes `ILinkedCalendarService`; `Details` makes one windowed read after the month models are built and assigns `LinkedQuests`, `LinkedEvents`, `LinkedBoards` on each. `Details.cshtml` and `Details.Mobile.cshtml` are untouched; the note comes from `_Calendar.cshtml` reading the month model.
- `_LinkedAlsoThatDay.cshtml` renders one line per linked event (then quest): link icon, "Also that day: ", board name, em dash, title (anchor for a member, text otherwise), time, and visually-hidden status. Cancelled events get `also-that-day--cancelled`. No form control, id, for attribute or raw output.
- Slot in `_Calendar.Mobile.cshtml` is five added lines directly after the date label, before the vote branch. Zero removed lines.
- `quests.mobile.css` gains the `.also-that-day*` rules.
- `LinkedBoardQuestDetailsTests` (15 facts): desktop chips, board C absent, own event absent, note text, no controls in linked blocks, `DateVotes[` input count identical with and without the link, unlinked board unchanged, DM-only visibility; mobile clash/no-clash, placement between label and vote row, zero/one/many ordering (event first), cancelled, member anchor vs plain text, DM-only, no `id`/`for`/controls and unchanged vote input count.
- `LinkedBoardStaticGuardTests` (22 cases): partial content (both partials), desktop slot after the own-quest vote branch, mobile slot between label and first vote row, reflection over eight linked model types (no vote/signup/player/roster/character/description member, no full Quest/Event/ProposedDate/User-typed member), `CrossBoardLink` prefix set pinned to the three deep-link types, planning-reference scan over the feature's source files.

## Task Commits

1. Task 1 (tracer): Details linked read + desktop facts - `3646e487`
2. Task 2 RED: failing mobile facts - `ec4e6466`
3. Task 2 GREEN: partial, slot, CSS - `36a0bcce`
4. Task 3: static and reflection guard - `a3f9e89c`

## Verification

- Tracer gate (autonomous form): after `3646e487`, `LinkedBoardQuestDetailsTests`, `EventCalendarPartialTests`, `CalendarDungeonMasterSessionTests` passed (23) and `AmbientClockSeamTests` passed (50), then execution continued.
- Task 2 RED: 7 of 8 new mobile facts failed before the implementation (the unlinked-board fact passes by construction); all pass after.
- Guard mutation check: appending `<input type="hidden" />` to `_LinkedDayEntries.cshtml` made the partial-content fact fail; reverted immediately (`git checkout`), file unchanged in the commit.
- Full `QuestBoard.IntegrationTests`: 1063 passed, 11 skipped (pre-existing), 0 failed. `Architecture` unit tests: 72 passed. `CalendarSubscriptionStaticGuardTests` passes unchanged.
- No diff under `QuestBoard.Service/Views/Quest/`; `grep -c linkedCalendarService.GetForViewerAsync` is 1; `_LinkedAlsoThatDay.cshtml` has no `<input|<label|<button|<form`.
- Not proven by InMemory: SQL translation of the linked reads (covered by the local SQL Server checkpoint in the final plan).

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] last-of-type spacing rule would not match the last line before the vote row**
- **Found during:** Task 2
- **Issue:** The planned `.calendar-date-entry-mobile .also-that-day:last-of-type` matches by element type, and the vote row is also a `div`, so the last line would get no gap before the vote row when a vote row follows.
- **Fix:** Kept the planned rule and added `.also-that-day + .calendar-vote-row { margin-top: 8px; }`.
- **Files modified:** QuestBoard.Service/wwwroot/css/quests.mobile.css
- **Commit:** 36a0bcce

Otherwise the plan was executed as written. The planning-reference scan skips files in its list that do not exist yet, so it can name files owned by sibling plans merged in the same wave; this is documented in the test.

## Known Stubs

None.

## Threat Flags

None beyond the plan's threat model (T-91-31 to T-91-35 mitigated as planned).

## Self-Check: PASSED

- All three created files exist; commits `3646e487`, `ec4e6466`, `36a0bcce`, `a3f9e89c` are in git log.
