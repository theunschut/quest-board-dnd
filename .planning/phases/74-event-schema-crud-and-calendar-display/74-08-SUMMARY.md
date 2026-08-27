---
phase: 74-event-schema-crud-and-calendar-display
plan: 08
subsystem: testing
tags: [xunit, integration-tests, tenant-isolation, calendar, mobile]

# Dependency graph
requires:
  - phase: 74-event-schema-crud-and-calendar-display (plan 02)
    provides: EventEntity/EventSeriesEntity fail-closed HasQueryFilter tenant scoping
  - phase: 74-event-schema-crud-and-calendar-display (plan 04)
    provides: EventsController CRUD actions and their status-code contract
  - phase: 74-event-schema-crud-and-calendar-display (plan 06)
    provides: Desktop calendar event rendering (calendar-events/quest-events containers, legend row)
  - phase: 74-event-schema-crud-and-calendar-display (plan 07)
    provides: Mobile agenda event rendering (agenda-event-entry, month-neutral empty state)
provides:
  - EventTenantIsolationTests — a genuine two-group isolation and write-scoping suite for events
  - Desktop calendar assertions for event/quest ordering, chip link, legend, and all-day wording
  - Navbar assertions proving Create Event is present for a DM on both layouts and board types, absent for a Player
  - Mobile agenda assertions for events-only days, all-day wording, event-before-quest ordering, and the neutral empty state
affects: [75-event-availability, 76-event-recurrence, 77-availability-overview]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Two-group isolation fact structure: seed via factory.Database.CreateContext() (ActiveGroupId=null, writes are unfiltered), assert via an authenticated client scoped to the active group, pair every NotContain with a Contain so a broken page cannot pass an absence assertion for free"
    - "Fail-closed read verification: a null ActiveGroupId context sees zero rows on read, so proving a row 'still exists' on another board requires temporarily setting the singleton TestGroupContext.ActiveGroupId to that board rather than reusing the null-context seeding helper"

key-files:
  created:
    - QuestBoard.IntegrationTests/Tests/EventTenantIsolationTests.cs
  modified:
    - QuestBoard.IntegrationTests/Controllers/CalendarControllerIntegrationTests.cs
    - QuestBoard.IntegrationTests/Controllers/LayoutNavigationTests.cs
    - QuestBoard.IntegrationTests/Mobile/MobileViewsTests.cs

key-decisions:
  - "Delete_Post_EventFromOtherGroup_ReturnsNotFound originally verified the row 'still exists' by reading through a fresh factory.Database.CreateContext() (ActiveGroupId=null). That context is fail-closed on reads (null shows nothing, not everything — proven by the pre-existing GroupFilter_NullGroupIdShowsNoGroups fact), so the assertion always failed regardless of whether the delete actually happened. Fixed by temporarily setting factory.TestGroupContext.ActiveGroupId = 2 (the seeded board) and reading through a scoped QuestBoardContext from factory.Services, then restoring ActiveGroupId = 1 immediately after. This is a same-task [Rule 1] correction to the test's own arrange/assert logic, not a defect in the application code."

patterns-established:
  - "Reuse of the existing finalized-quest arrange block (dm + proposed date + FinalizedDate update) as the shared-day fixture for both the desktop ordering fact and the mobile ordering fact, so all three (application code, desktop test, mobile test) exercise the identical seeding shape"

requirements-completed: [EVENT-02, EVENT-03, EVENT-04, EVENT-05, EVENT-06]

coverage:
  - id: D1
    description: "A two-group integration suite proves one board's events are invisible to the other on the desktop calendar, the mobile agenda, and a direct event identifier, each paired with a positive same-board assertion so the absence facts cannot pass vacuously"
    requirement: "EVENT-02"
    verification:
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Tests/EventTenantIsolationTests.cs#GroupFilter_HidesEventFromOtherGroupOnDesktopCalendar"
        status: pass
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Tests/EventTenantIsolationTests.cs#GroupFilter_ShowsEventFromSameGroup"
        status: pass
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Tests/EventTenantIsolationTests.cs#GroupFilter_HidesEventFromOtherGroupOnMobileAgenda"
        status: pass
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Tests/EventTenantIsolationTests.cs#Details_EventFromOtherGroup_ReturnsNotFound"
        status: pass
    human_judgment: false
  - id: D2
    description: "A posted board identifier cannot override the server-side stamp on create, and edit/delete against another board's event are rejected before any state change (with the delete fact re-checking the row still exists)"
    requirement: "EVENT-02"
    verification:
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Tests/EventTenantIsolationTests.cs#Create_Post_PostedGroupIdIsIgnored_ServerStampsActiveBoard"
        status: pass
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Tests/EventTenantIsolationTests.cs#Edit_Post_EventFromOtherGroup_ReturnsNotFound"
        status: pass
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Tests/EventTenantIsolationTests.cs#Delete_Post_EventFromOtherGroup_ReturnsNotFound"
        status: pass
    human_judgment: false
  - id: D3
    description: "An event whose repeating-schedule reference belongs to another board is rejected on edit with a bad request"
    requirement: "EVENT-02"
    verification:
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Tests/EventTenantIsolationTests.cs#Edit_Post_EventPointingAtAnotherBoardSchedule_ReturnsBadRequest"
        status: pass
    human_judgment: false
  - id: D4
    description: "The desktop calendar renders the event block above the quest block on a shared day, the event chip links to event details, the legend explains the event swatch, and an event with no start time renders the all-day wording"
    requirement: "EVENT-03"
    verification:
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Controllers/CalendarControllerIntegrationTests.cs#Index_WithEventAndQuestOnSameDay_RendersEventBlockAboveQuestBlock"
        status: pass
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Controllers/CalendarControllerIntegrationTests.cs#Index_WithEvent_RendersClickableChipLinkingToEventDetails"
        status: pass
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Controllers/CalendarControllerIntegrationTests.cs#Index_LegendExplainsEventChip"
        status: pass
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Controllers/CalendarControllerIntegrationTests.cs#Index_EventWithoutStartTime_RendersAllDayWording"
        status: pass
    human_judgment: false
  - id: D5
    description: "Under a real mobile User-Agent, an events-only day appears in the agenda, events render before quests on a shared day, an event with no start time renders the all-day wording, and a month with neither renders the neutral empty state"
    requirement: "EVENT-04"
    verification:
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Mobile/MobileViewsTests.cs#MobileCalendar_DayWithEventButNoQuest_AppearsInAgenda"
        status: pass
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Mobile/MobileViewsTests.cs#MobileCalendar_EventWithoutStartTime_RendersAllDayWording"
        status: pass
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Mobile/MobileViewsTests.cs#MobileCalendar_EventAndQuestOnSameDay_EventEntryRendersFirst"
        status: pass
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Mobile/MobileViewsTests.cs#MobileCalendar_MonthWithNeitherQuestNorEvent_RendersNeutralEmptyState"
        status: pass
    human_judgment: false
  - id: D6
    description: "The Create Event navbar entry is present for a Dungeon Master on both layouts and on the campaign board type, and absent for a Player"
    requirement: "EVENT-06"
    verification:
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Controllers/LayoutNavigationTests.cs#Nav_DungeonMaster_CreateEventEntryPresent"
        status: pass
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Controllers/LayoutNavigationTests.cs#Nav_Player_CreateEventEntryAbsent"
        status: pass
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Controllers/LayoutNavigationTests.cs#Nav_CampaignBoard_DungeonMaster_CreateEventEntryStillPresent"
        status: pass
    human_judgment: false

# Metrics
duration: 45min
completed: 2026-08-27
status: complete
---

# Phase 74 Plan 08: Validation Suite — Tenant Isolation, Write Scoping, and Render Assertions Summary

**One new 8-fact two-group tenant isolation and write-scoping suite for events, plus 11 new render assertions extending the desktop calendar, navbar, and mobile agenda suites — closing every remaining validation gap the roadmap makes a hard success criterion for Phase 74.**

## Performance

- **Duration:** ~45 min
- **Completed:** 2026-08-27T06:20Z
- **Tasks:** 3/3
- **Files modified:** 4 (1 created, 3 extended)

## Accomplishments
- `EventTenantIsolationTests` seeds a genuine second board and proves one board's events are invisible to the other on the desktop calendar, the mobile agenda, and a direct event identifier — each absence fact paired with a positive same-board assertion so it cannot pass on a blank or errored page
- Proves a posted `GroupId` form field cannot override the server-side stamp on create, that edit/delete against another board's event both return not-found before any state change, and that delete's rejection is verified by re-reading the row (not just trusting the status code)
- Proves an event whose `SeriesId` points at another board's `EventSeries` row is rejected on edit with a bad request — the read filter and the controller's explicit comparison are both exercised
- Desktop calendar: event block renders above the quest block (position asserted via string-index comparison, not just presence), the chip links to `/Events/Details/{id}`, the legend explains the event swatch, and a start-time-less event renders "All day"
- Mobile agenda: an events-only day now appears (previously invisible), events render before quests on a shared day, the all-day wording renders, and the neutral "Nothing This Month" empty state is proven present while the old quest-only heading is proven absent — all four facts issue a real iPhone User-Agent, never viewport emulation
- Navbar: `Create Event` proven present for a Dungeon Master on both the desktop and mobile layouts and on the campaign board type (restored via `finally`), and absent for a Player while proving the navbar itself rendered

## Task Commits

Each task was committed atomically:

1. **Task 1: Write the two-group event isolation and write-side scoping suite** - `ef2859f` (test)
2. **Task 2: Extend the desktop calendar and navbar suites** - `206aff0` (test)
3. **Task 3: Extend the mobile suite with real-User-Agent agenda assertions** - `4aba2c5` (test)

**Plan metadata:** committed alongside this SUMMARY (see final commit below)

_This is a worktree-mode execution; per the worktree protocol, STATE.md/ROADMAP.md are not modified here — the orchestrator owns those writes after the wave completes._

## Files Created/Modified
- `QuestBoard.IntegrationTests/Tests/EventTenantIsolationTests.cs` - New: 8 facts proving cross-board read isolation (desktop, mobile, direct identifier), write-side board stamping on create, edit/delete rejection for another board's event, and cross-board schedule-reference rejection on edit
- `QuestBoard.IntegrationTests/Controllers/CalendarControllerIntegrationTests.cs` - 4 new facts: event-above-quest ordering, chip link, legend row, all-day wording
- `QuestBoard.IntegrationTests/Controllers/LayoutNavigationTests.cs` - 3 new test methods (1 `[Theory]` with 2 cases + 2 `[Fact]`s): Create Event present for DM on both layouts, absent for Player, still present on the campaign board type
- `QuestBoard.IntegrationTests/Mobile/MobileViewsTests.cs` - 4 new facts: events-only day, all-day wording, event-before-quest ordering, neutral empty state — all under a real mobile User-Agent

## Decisions Made
- Followed the plan's read/write seeding pattern exactly for 7 of 8 Task 1 facts: `factory.Database.CreateContext()` (unfiltered writes) to seed the other board, an authenticated client scoped to the active board to exercise reads and writes. See Deviations below for the one fact that needed a same-task fix to its own verification logic.
- Reused the existing `Index_WithFinalizedQuests_ShouldDisplayQuestsOnCalendar` arrange block verbatim for both the desktop and mobile ordering facts, so the seeding shape stays identical to the pattern the rest of the suite already trusts.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Fixed a fail-closed read in the Delete fact's own "row still exists" verification**
- **Found during:** Task 1 (`Delete_Post_EventFromOtherGroup_ReturnsNotFound`)
- **Issue:** The plan's action text says to verify the group-2 row still exists "through `factory.Database.CreateContext()`". That context runs with `ActiveGroupId = null`, and the tenant query filter is fail-closed — a null active group sees zero rows on a read, not every board's rows (this is exactly what the pre-existing `GroupFilter_NullGroupIdShowsNoGroups` fact proves). Reading through that context therefore always reports the row as absent, regardless of whether the delete actually happened, making the assertion meaningless.
- **Fix:** Temporarily set `factory.TestGroupContext.ActiveGroupId = 2` (the board the seeded row belongs to), read through a scoped `QuestBoardContext` obtained from `factory.Services`, assert the row exists, then immediately restore `ActiveGroupId = 1` before the fact returns.
- **Files modified:** `QuestBoard.IntegrationTests/Tests/EventTenantIsolationTests.cs`
- **Verification:** `dotnet test --filter "FullyQualifiedName~EventTenantIsolationTests"` — 8/8 passed after the fix (was 7/8 before, with only this fact failing).
- **Committed in:** `ef2859f` (part of Task 1's single commit — the fix was made before the task's first commit, so there is no separate correction commit)

---

**Total deviations:** 1 auto-fixed (test-logic bug, not an application defect)
**Impact on plan:** No change to scope or to any application code. The fix corrects the test's own verification method to respect the fail-closed filter semantics the rest of the suite (and this very fact) is asserting.

## Issues Encountered

**Acceptance-criteria scope note:** Task 1's prohibition lists `grep -rn "IgnoreQueryFilters" QuestBoard.IntegrationTests/` as returning "no matches" for the whole directory. Two pre-existing files outside this plan's scope (`CharactersControllerIntegrationTests.cs`, `ContactsControllerIntegrationTests.cs`) already call `IgnoreQueryFilters()` for unrelated features created in earlier phases. `EventTenantIsolationTests.cs` itself contains zero occurrences, satisfying the intent of the prohibition (event isolation reads only through the application's own filtered path). The directory-wide grep result is a pre-existing condition, out of this plan's scope per the SCOPE BOUNDARY rule, and is noted here rather than silently ignored.

No other issues. All three tasks' acceptance criteria (fact counts, grep-verifiable structural checks, insertions-only diffs) verified directly.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness
- Every roadmap-mandated validation gap for Phase 74 is closed: two-group tenant isolation with paired positive assertions, write-side board stamping, cross-board schedule rejection, desktop render assertions, mobile render assertions under a real User-Agent, and navbar visibility assertions.
- Full solution suite: 313 unit + 466 integration = 779/779 passing, 0 failures (baseline was 759; 20 new tests added across the three tasks).
- `dotnet test --filter "FullyQualifiedName~TenantIsolationTests"` (the pre-existing quest isolation suite) still passes on a full run after `EventTenantIsolationTests` runs, confirming the shared `MutableGroupContext` singleton was correctly reset and did not bleed state into a later test class.
- No blockers or concerns for Phase 75 (event availability), which can build directly on the now-fully-validated Phase 74 storage, CRUD, and render layers.

---
*Phase: 74-event-schema-crud-and-calendar-display*
*Completed: 2026-08-27*

## Self-Check: PASSED

- FOUND: QuestBoard.IntegrationTests/Tests/EventTenantIsolationTests.cs
- FOUND: .planning/phases/74-event-schema-crud-and-calendar-display/74-08-SUMMARY.md
- FOUND commit ef2859f (Task 1)
- FOUND commit 206aff0 (Task 2)
- FOUND commit 4aba2c5 (Task 3)
