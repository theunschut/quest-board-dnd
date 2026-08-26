---
phase: 74-event-schema-crud-and-calendar-display
plan: 04
subsystem: api
tags: [automapper, aspnet-mvc, authorization, tenant-isolation]

# Dependency graph
requires:
  - phase: 74-event-schema-crud-and-calendar-display (plan 03)
    provides: Event domain model, IEventService (GetEventsForCalendarAsync, GetEventWithDetailsAsync, GetSeriesGroupIdAsync)
  - phase: 74-event-schema-crud-and-calendar-display (plan 01)
    provides: EventsControllerIntegrationTests RED scaffold (10 facts) defining the exact routes, status codes and redirect targets this controller must satisfy
provides:
  - EventViewModel (title/description/date/optional start time, display-only CanManage, computed TimeLabel)
  - Two AutoMapper entries (Event <-> EventViewModel) with GroupId/SeriesId/SeriesSlotIndex/CreatedAt ignored on the write direction
  - EventsController with six actions (Details, Create GET/POST, Edit GET/POST, Delete), five DungeonMasterOnly-gated write actions, three antiforgery-protected POSTs
  - Board-scoped write stamping (GroupId from IActiveGroupContext, never from the request) on both Create and Edit
  - Cross-board repeating-schedule rejection (SeriesIsOnActiveBoardAsync) as an explicit second layer independent of the read-side query filter
  - Month-accurate redirect (RedirectToCalendarMonth) used by all three write actions
affects: [74-05, 75-event-availability, 76-event-recurrence, 77-availability-overview]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Board-scoped write stamping: GroupId is always assigned from activeGroupContext.RequireActiveGroupId() on every write, never mapped from the submitted view model — the AutoMapper entry ignores it explicitly as a second layer beyond the ForMember(...Ignore()) declaration"
    - "Fail-closed second layer for schedule ownership: a nullable owner lookup (GetSeriesGroupIdAsync) is compared against the active board and treated as a rejection on null, independent of the entity's query filter"

key-files:
  created:
    - QuestBoard.Service/ViewModels/EventViewModels/EventViewModel.cs
    - QuestBoard.Service/Controllers/Events/EventsController.cs
  modified:
    - QuestBoard.Service/Automapper/ViewModelProfile.cs

key-decisions: []

patterns-established:
  - "EventsController mirrors ContactsController's constructor DI shape (IEventService, IUserService, IActiveGroupContext, IMapper) minus image validation, and reuses its IsDmTierAsync display-flag pattern verbatim"

requirements-completed: [EVENT-01, EVENT-02, EVENT-05]

coverage:
  - id: D1
    description: "A DM can POST title, optional Markdown description, date, and optional start time to /Events/Create and get a persisted event, redirecting to the calendar at the event's own month"
    requirement: "EVENT-01"
    verification:
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Controllers/EventsControllerIntegrationTests.cs#Create_Post_ValidEvent_PersistsAndRedirectsToEventMonth"
        status: pass
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Controllers/EventsControllerIntegrationTests.cs#Create_Post_PastDate_IsAccepted"
        status: pass
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Controllers/EventsControllerIntegrationTests.cs#Create_Post_WithoutStartTime_IsAccepted"
        status: pass
    human_judgment: false
  - id: D2
    description: "A DM can edit (by any DM on the board) and delete any event on their own board with a month-accurate redirect; a Player is rejected by the DungeonMasterOnly policy on Create GET and Delete POST"
    requirement: "EVENT-02"
    verification:
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Controllers/EventsControllerIntegrationTests.cs#Edit_Post_ByAnyDmOnBoard_Succeeds"
        status: pass
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Controllers/EventsControllerIntegrationTests.cs#Delete_Post_ByDm_RedirectsToEventMonth"
        status: pass
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Controllers/EventsControllerIntegrationTests.cs#Create_Get_PlayerAccess_ShouldBeBlocked"
        status: pass
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Controllers/EventsControllerIntegrationTests.cs#Delete_Post_PlayerAccess_ShouldBeBlocked"
        status: pass
    human_judgment: false
  - id: D3
    description: "Quest creation remains provably unaffected by events — an existing same-day event does not block or alter quest creation"
    requirement: "EVENT-05"
    verification:
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Controllers/EventCalendarPartialTests.cs#QuestCreate_WithExistingEventOnChosenDate_SucceedsUnchanged"
        status: pass
    human_judgment: false
  - id: D4
    description: "Create GET, the no-title validation error path, and board-member Details read render a Razor view; these three facts stay RED until plan 74-05 adds Views/Events/ — expected, not a defect in this plan"
    human_judgment: true
    rationale: "These three integration facts (Create_Get_DungeonMasterAccess_ShouldSucceed, Create_Post_WithoutTitle_ReturnsFormWithValidationError, Details_Get_BoardMember_CanRead) require a Razor view to render a 200 response; the controller action code is correct and complete, but no human/automated signal exists yet to confirm the view itself until plan 74-05 lands Views/Events/. Verified here only that they fail for the expected reason (missing view, not missing route/logic)."

# Metrics
duration: ~12min
completed: 2026-08-26
status: complete
---

# Phase 74 Plan 04: Event View Model and Controller Summary

**EventViewModel plus a six-action EventsController that stamps the active board server-side on every write and rejects a cross-board repeating-schedule reference with BadRequest before save.**

## Performance

- **Duration:** ~12 min
- **Started:** 2026-08-26T14:10Z (approx)
- **Completed:** 2026-08-26T14:22Z
- **Tasks:** 2/2
- **Files modified:** 3 (2 created, 1 edited)

## Accomplishments
- `EventViewModel` with exactly seven public members: `Id`, `Title` (required, 200 chars), `Description` (unbounded Markdown), `Date` (required `DateOnly`), `StartTime` (optional `TimeOnly?`), `CanManage` (display-only), and `TimeLabel` (computed — `"HH:mm"` when a start time is present, `"All day"` otherwise, the single place that decides how an event's time is worded)
- `ViewModelProfile` gained the `Event <-> EventViewModel` mapping block; the write direction (`EventViewModel -> Event`) ignores `GroupId`, `SeriesId`, `SeriesSlotIndex` and `CreatedAt` so none of the four can be set from a submitted form
- `EventsController` — six actions matching the plan's exact spec: `Details` (any authenticated board member, `CanManage` computed for display only), `Create` GET/POST (`DungeonMasterOnly`), `Edit` GET/POST (`DungeonMasterOnly`), `Delete` (`DungeonMasterOnly`)
- Board stamped server-side on both Create (`newEvent.GroupId = activeGroupContext.RequireActiveGroupId()`) and Edit (re-assigned on every write, never round-tripped through the form)
- `SeriesIsOnActiveBoardAsync` rejects an Edit whose existing event belongs to a repeating schedule owned by another board, returning `BadRequest()` — an explicit second layer independent of the entity query filter
- All three write actions (Create, Edit, Delete) redirect to `/Calendar` at the event's own month (`Year`/`Month` read directly off the `DateOnly`, no conversion) with a `TempData["Success"]` toast
- Past dates and a missing start time are both accepted without validation error, matching D-19's "event is a record of something that happened" intent
- Zero references to `IQuestService`, `IQuestRepository`, `QuestViewModel`, or the literal `"Quest"` anywhere in the new controller or view model; `QuestController.cs` shows no modification

## Task Commits

Each task was committed atomically:

1. **Task 1: Add EventViewModel and its AutoMapper entries** - `110d6ea` (feat)
2. **Task 2: Implement EventsController with DM-gated writes and board-scoped stamping** - `6c1cef2` (feat)

_This is a worktree-mode execution; the plan-metadata commit (SUMMARY.md) is committed separately per the worktree protocol — no STATE.md/ROADMAP.md changes are made here._

## Files Created/Modified
- `QuestBoard.Service/ViewModels/EventViewModels/EventViewModel.cs` - The form-bindable surface of an event: title, description, date, optional start time, plus `CanManage` and `TimeLabel`
- `QuestBoard.Service/Automapper/ViewModelProfile.cs` - Added `Event <-> EventViewModel` mapping block; write direction ignores the four server-derived fields
- `QuestBoard.Service/Controllers/Events/EventsController.cs` - Six-action DM-gated CRUD controller with board-scoped write stamping and cross-board schedule rejection

## Decisions Made
None - plan executed exactly as written; no ambiguity encountered during implementation.

## Deviations from Plan

None - plan executed exactly as written. Both tasks matched their `read_first` references and `action` specs precisely; every grep-based acceptance criterion passed on the first attempt.

## Issues Encountered

None. `dotnet build` succeeded on the first attempt for both tasks (only pre-existing, unrelated NU1608 NuGet warnings about `AngleSharp` version constraints). The seven redirect-or-reject facts specified by the plan's Task 2 acceptance criteria all turned green on the first `dotnet test` run; the three facts that require a Razor view (`Create_Get_DungeonMasterAccess_ShouldSucceed`, `Create_Post_WithoutTitle_ReturnsFormWithValidationError`, `Details_Get_BoardMember_CanRead`) failed as expected — this is documented in the plan itself as deferred to 74-05, not a defect. `dotnet test --filter "FullyQualifiedName~EventCalendarPartialTests.QuestCreate"` confirmed quest creation remains unaffected by an existing same-day event.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness
- `EventsController` and `EventViewModel` are complete and build/test-verified; plan 74-05 can add `Views/Events/Create.cshtml`, `Edit.cshtml`, and `Details.cshtml` directly against this view model with no further controller changes expected.
- The three view-dependent integration facts (RED today) are the exact acceptance signal for 74-05's Razor views — once those views exist and render `EventViewModel` correctly, all ten `EventsControllerIntegrationTests` facts and the calendar-partial facts should turn green.
- No blockers or concerns. Quest creation and `QuestController.cs` remain provably untouched by this plan.

---
*Phase: 74-event-schema-crud-and-calendar-display*
*Completed: 2026-08-26*
