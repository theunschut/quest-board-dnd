---
phase: 91-linked-boards-with-a-shared-calendar
plan: 10
subsystem: testing
tags: [integration-tests, linked-boards, calendar, platform, sql-server, human-verification]

requires:
  - phase: 91-linked-boards-with-a-shared-calendar
    provides: Platform Linked Boards page, linked calendar reads, Quest Details linked entries (plans 01-09)
provides:
  - End-to-end proof that link, see on four calendar surfaces, unlink and gone works through the real Platform POSTs
  - D-13 evidence that the feed, agenda, events overview and series files are untouched
  - Operator verification on SQL Server and in a browser (migration, windowed linked reads, visuals, unlink)
  - Mobile Linked Boards Remove Board button no longer wraps its label
affects: [phase 91 verify-work, ship]

actuals:
  tokens: 2600
  tasks: 2
  commits: 3

tech-stack:
  added: []
  patterns:
    - "Drive a set through the Platform form posts rather than seeding it, so the whole path is exercised"

key-files:
  created:
    - QuestBoard.IntegrationTests/Tests/LinkedBoardEndToEndTests.cs
  modified:
    - QuestBoard.Service/wwwroot/css/platform-linked-boards.mobile.css

key-decisions:
  - "The translation fallback (dropping the date predicates from the windowed linked reads) was not needed: SQL Server ran every linked read without a translation error"

patterns-established:
  - "In a flex row whose child is a form, the form is the element that needs flex-shrink: 0, not the button inside it"

requirements-completed: []

coverage:
  - id: D1
    description: "A set created and filled through the Platform POSTs shows board B's session on desktop calendar, phone calendar, desktop Quest Details and phone Quest Details, never board C's, and RemoveBoard makes B vanish from all four on the next request"
    verification:
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Tests/LinkedBoardEndToEndTests.cs#LinkingThroughThePlatformShowsTheOtherBoardOnAllFourSurfacesAndUnlinkingRemovesItAtOnce"
        status: pass
    human_judgment: false
  - id: D2
    description: "The feed, agenda, events overview and series-runway files show no diff against the pre-phase commit"
    verification:
      - kind: other
        ref: "git diff --stat c4abdf84 -- <the eight D-13 paths> printed nothing"
        status: pass
    human_judgment: false
  - id: D3
    description: "The AddLinkedBoardSets migration applies on SQL Server and the windowed linked reads translate and run"
    verification: []
    human_judgment: true
    rationale: "InMemory evaluates the linked queries as plain LINQ, so only a real SQL Server run can prove translation; the operator ran it and reported no errors"
  - id: D4
    description: "Desktop and phone presentation of linked entries, the Platform Linked Boards page at 390px, and unlink in a browser"
    verification: []
    human_judgment: true
    rationale: "Visual and layout checks the UI-SPEC holds back as human backstops; verified in the in-app browser, not on a physical phone"

duration: 60min
completed: 2026-10-16
status: complete
---

# Phase 91 Plan 10: End-to-end proof and operator verification Summary

**One Platform-driven end-to-end fact proving link, see on four calendar surfaces, unlink and gone, plus operator verification on SQL Server and a browser, and a one-rule CSS fix that keeps Remove Board on one line at 390px.**

## Performance

- **Duration:** about 60 min
- **Completed:** 2026-10-16
- **Tasks:** 2 (1 automated tracer, 1 blocking human-verify checkpoint)
- **Files modified:** 2 (one created, one fixed)

## Accomplishments

- `LinkedBoardEndToEndTests` has one fact that tells the whole story. A super admin creates a set and adds two boards with `POST /platform/LinkedBoards/Create` and `AddBoard`. The viewer then sees "BravoSession" on the desktop `/Calendar` (`linked-day-entries`), the phone `/Calendar` (`agenda-linked-entry`), desktop Quest Details (`linked-day-entries` plus `Also showing: Linked Board Bravo (read-only)`) and phone Quest Details (`also-that-day`). "CharlieSession" and the outside board's name never appear. After `POST RemoveBoard`, the viewer's next four GETs show no BravoSession and none of `also-that-day`, `linked-day-entries`, `agenda-linked-entry` or `calendar-linked-note`.
- The D-13 diff over the feed, agenda, events overview and series files printed nothing.
- The full suite is green and the operator approved the SQL Server and browser verification (below).

## Task Commits

1. **Task 1: End-to-end link, see, unlink, gone; D-13 untouched; full suite** - `a90c35ac` (test)
2. **Task 2: Human verification** - checkpoint, approved by the operator subject to one fix
3. **Fix found during Task 2 (deviation below)** - `2eeb578e` (fix)

## D-13 check

Command:

```
git diff --stat c4abdf84 -- QuestBoard.Domain/Services/CalendarFeedWriter.cs QuestBoard.Domain/Services/CalendarSubscriptionService.cs QuestBoard.Repository/FeedRevisionStamper.cs QuestBoard.Service/Controllers/AgendaController.cs QuestBoard.Service/Views/Agenda QuestBoard.Service/Controllers/Events/EventsController.cs QuestBoard.Service/Views/Events QuestBoard.Domain/Services/EventSeriesService.cs
```

Output: empty (exit 0). Every listed path exists, so the empty output is not a path typo.

## Full test run (Task 1)

- Unit tests: 916 passed, 0 failed.
- Integration tests: 1088 passed, 0 failed, 11 skipped. The 11 skips are the pre-existing `MigrationRunnerSqlServerTests`, which need a real SQL Server.
- No CLR flake occurred and no rerun was needed.
- After the CSS fix, the focused run of `MobileCssTests`, `LinkedBoardsPlatformIntegrationTests`, `LinkedBoardEndToEndTests` and `LinkedBoardStaticGuardTests` passed 65 of 65.

## Operator verification (Task 2): approved, subject to one fix

- **Date:** 2026-10-16.
- **Environment:** Linux, Docker container `mssql-dev` (`mcr.microsoft.com/mssql/server:2022-latest`) on localhost:1433, with a SQL-auth connection string from dotnet user-secrets. The orchestrator started the existing container at the operator's explicit request. Nothing was installed or provisioned, and this executor did not start, stop or provision any database or container.
- **Browser:** the in-app browser, at 1024-1366px desktop and at a 390x844 emulated Android mobile user agent. It was not run on a physical phone.

**Database checks**
- The app ran from the main checkout at 73bc7738.
- `__EFMigrationsHistory` latest is `20261016061911_AddLinkedBoardSets`, so the migration applied on startup.
- `FK_Groups_LinkedBoardSets_LinkedBoardSetId` has delete action `SET_NULL`.
- No "could not be translated" or other server errors at any point. The translation fallback was NOT needed.

**Test data**
- The set "Verification set O'Brien \"Q\"" (One-Shot) was created via the Platform POSTs, linking Euphoria Inn (id 1, viewer is Admin) and D&D Nerdjes/Vriendjes (id 6, viewer not a member).
- It was later renamed to a long name and finally deleted. The database was left with 0 sets and 0 linked groups.

**Desktop /calendar on board 6** (viewer is a member of linked board 1)
- 6 linked chips: No Money Mo Beans x4, Daggerheart (finalized), The Mark of the Beast.
- All are `a.linked-entry--open` to `/Quest/Details/{id}`. Proposed chips have a dashed 4px yellow left edge and the finalized chip has green.
- Hover reveals "Euphoria Inn · DM · 18:00". `linked-day-count` badges are present and the Legend lists Euphoria Inn.
- Clicking the Daggerheart chip landed on `/Quest/Details/12039` with the board switched to Euphoria Inn and a `board-switch-toast` reading "Switched to Euphoria Inn to open this. Switch back to D&D Nerdjes/Vriendjes".

**Desktop /calendar on board 1** (viewer is not a member of board 6)
- Linked chips are `div.linked-entry--static` with no href, no onclick and no tabindex.
- The Legend lists D&D Nerdjes/Vriendjes.
- The month window works: September shows 13 static chips.

**Desktop Quest Details 12040**
- "Also showing: D&D Nerdjes/Vriendjes (read-only)" appears on both month calendars, with static chips only.

**Mobile /calendar**
- "Also showing: D&D Nerdjes/Vriendjes" sits under the month bar.
- Linked rows come after own rows, are static (no onclick, no link), and show the board name and DM.
- No horizontal scroll.

**Mobile Quest Details 12040**
- 8 lines read "Also that day: D&D Nerdjes/Vriendjes — Baby in the Sky with Goblins, 18:00", placed directly above the unchanged `calendar-vote-row`.
- No horizontal scroll.

**Platform Linked Boards**
- The picker lists only One-Shot boards and excludes Campaign boards. Active and Inactive badges are correct.
- The confirm handler on a name containing `'` and `"` is a single intact `confirm("...")` call with `"` and `'` escapes.
- At 390px with a long set name, the name wraps and there is no horizontal scroll. Create, Rename, Add and Delete buttons are 44px.
- **Defect found:** "Remove Board" was 62px tall (2-line label) beside "D&D Nerdjes/Vriendjes". Fixed by the Rule 1 change below. The orchestrator re-checks it in the browser after merge.

**Unlink**
- RemoveBoard on board 6 showed the correct confirm text and the success toast, and the set went back to Inactive.
- The next loads of mobile /calendar, mobile Quest Details and desktop /calendar (September) on board 1 showed no linked rows, chips, count badges, legend entries or notes.

**Not verifiable with this data**
- Event chips and the cancelled-event styling (the database has no events).
- The Legend and "Also showing" lines with four or more linked boards (only 3 One-Shot boards exist). Both are covered by the automated suites only.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Remove Board label wrapped to two lines on the mobile Linked Boards page**
- **Found during:** Task 2 (operator verification at 390px)
- **Issue:** In `.linked-set-board-row` the flex child is the `<form>`, not the `.btn`. The existing `.linked-set-board-row .btn { flex-shrink: 0 }` therefore targeted the wrong element, the form shrank, and the label wrapped (button 62px tall, row 78px).
- **Fix:** Added `.linked-set-board-row > form { flex-shrink: 0; }` and `white-space: nowrap;` on `.linked-set-board-row .btn`. The existing name rule (`min-width: 0; overflow-wrap: anywhere`) is kept, so long board names wrap instead of the button. No `@media`.
- **Files modified:** `QuestBoard.Service/wwwroot/css/platform-linked-boards.mobile.css` (outside this plan's `files_modified`)
- **Test:** none added. `MobileCssTests` only asserts on `mobile.css` content, so there was no suitable place for a rule assertion.
- **Commit:** `2eeb578e`

---

**Total deviations:** 1 auto-fixed (1 bug). **Impact:** a CSS-only fix to a file from an earlier plan; no behaviour change elsewhere.

## Auth Gates

None. The SQL Server precondition was the operator's own environment and was satisfied by the operator.

## Known Stubs

None. The new test file and the CSS change contain no stubs, TODOs or placeholder data.

## Threat Flags

None. No new network endpoint, auth path or schema change was introduced by this plan.

## Self-Check: PASSED

- FOUND: `QuestBoard.IntegrationTests/Tests/LinkedBoardEndToEndTests.cs`
- FOUND: `QuestBoard.Service/wwwroot/css/platform-linked-boards.mobile.css` with the `> form` rule
- FOUND commit `a90c35ac` (Task 1 test)
- FOUND commit `2eeb578e` (CSS fix)
