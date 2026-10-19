---
phase: 92-move-a-quest-or-event-to-a-linked-board
plan: 06
subsystem: board-move
tags: [event, move, availability, campaign, super-admin, ef-core, feed-revision]
requires:
  - phase: 92-01
    provides: MoveStatus, MovePreview, BoardMoveSeed, MoveController
  - phase: 92-03
    provides: MoveEligibility.ForEvent, MovePreviewStamp, MoveCopy
  - phase: 92-04
    provides: shared Move page and MovePageViewModel
provides:
  - EventMovePlan, EventMovePreview
  - IEventMoveRepository / EventMoveRepository (single tracked save)
  - IEventMoveService / EventMoveService (preview, stamp-checked move, no mail dependency)
  - GET /Move/Event/{id} and POST ConfirmEvent (action name Event)
  - Shared private Moved-toast helper on MoveController
  - BoardMoveSeed event helpers (SeedEventAsync, SeedSeriesAsync, SeedEventSignupAsync, ReadEventAsync)
affects: [92-07, 92-08, 92-09]
tech-stack:
  added: []
  patterns:
    - "Event move mirrors the quest move line for line: tracked load, validate before mutate, GroupId assigned last, one SaveChangesAsync, ChangeTracker.Clear on failure"
key-files:
  created:
    - QuestBoard.Domain/Models/BoardMove/EventMovePlan.cs
    - QuestBoard.Domain/Models/BoardMove/EventMovePreview.cs
    - QuestBoard.Domain/Interfaces/IEventMoveRepository.cs
    - QuestBoard.Domain/Interfaces/IEventMoveService.cs
    - QuestBoard.Domain/Services/EventMoveService.cs
    - QuestBoard.Repository/EventMoveRepository.cs
    - QuestBoard.UnitTests/Repository/BoardMoveEventRepositoryTests.cs
    - QuestBoard.UnitTests/Services/BoardMoveEventServiceTests.cs
    - QuestBoard.IntegrationTests/Tests/BoardMoveEventTests.cs
  modified:
    - QuestBoard.Domain/Extensions/ServiceExtensions.cs
    - QuestBoard.Repository/Extensions/ServiceExtensions.cs
    - QuestBoard.Service/Controllers/MoveController.cs
    - QuestBoard.IntegrationTests/Helpers/BoardMoveSeed.cs
key-decisions:
  - "Kept availability rows are never rewritten, so their values and timestamps are byte-identical after the move"
  - "Automatic Yes rows are added only for a Campaign target, with the answered marker left unset"
  - "A successful event move returns to the Events overview"
patterns-established:
  - "One private controller helper builds the Moved toast for both kinds, taking the removed-noun singular and plural"
status: complete
actuals:
  tokens: 45000
  tasks: 3
  commits: 3
---

# Phase 92 Plan 06: Event move Summary

A super admin can now move a one-off event to a linked board from the same Move page: target members' availability answers stay exactly as given, strangers' answers are removed in the same save, a Campaign target gets the automatic Yes rows a new Campaign event would have, series occurrences and past events are refused, and nobody is emailed.

## What was built

- `EventMoveRepository.ApplyAsync` loads the tracked event with its signups under the source filter, returns `NotFound` off the source board, and returns `Changed` with nothing touched when the loaded signup ids differ from kept plus dropped or when an add would duplicate an existing user's row. Otherwise it removes dropped rows, adds the Campaign rows (`Availability = Yes`, answered marker unset), assigns `GroupId` last and saves once. A concurrency conflict returns `Changed`; any other failure clears the tracker and rethrows. `CancelledAt`, series fields and feed fields are never touched.
- `EventMoveService.BuildPreviewAsync` follows the quest order: no board, no event, eligibility (series occurrence first, then past), fresh linked set, target resolution, roster, target membership, kept and dropped by membership (dropped ordered by name), Campaign adds (distinct members with no row, Campaign set only), plan and stamp. `MoveAsync` mirrors the stamp check, `Changed` recompute, `Failed` catch and writes one log line with ids and counts only. The constructor takes no mail dispatcher, and a unit fact pins that.
- `MoveController` gains `Event` (GET) and `ConfirmEvent` (POST, action name `Event`). Refusals redirect to `Events/Details`; `Changed` re-renders the shared page with HTTP 200; `Moved` redirects to the Events overview. The Moved toast is one private helper used by both kinds, with "answer(s)" for events and "signup(s)" for quests.
- DI registered in both `ServiceExtensions` files. The shared Move views needed no change: they already switch copy on the preview kind.

## Verification

- Unit: `BoardMoveEventRepositoryTests` (12) and `BoardMoveEventServiceTests` (19) pass, along with the `BoardMove`, `Architecture`, `AmbientClock` and `FeedRevisionStamper` filters. Full unit suite: 1054 passed.
- Integration: `BoardMoveEventTests` 18 passed. Combined filter (`BoardMove`, `AntiForgeryTokenCoverage`, `EventsControllerIntegration`, `EventSeriesTenantIsolation`): 103 passed, including every quest-side `BoardMove*` fact after the toast helper extraction.
- `dotnet build`: succeeded. No planning references in added source lines (count 0).
- `CalendarFeedWriter.cs`, `CalendarSubscriptionService.cs` and `FeedRevisionStamper.cs` untouched. The write uses only property assignment, `Remove` and adding to the tracked collection, so the feed revision seam tests still pass.

## Deviations from Plan

None - plan executed as written. Process note: tests and implementation were committed together per task rather than as separate RED and GREEN commits; tests were written before the checks were run.

## Not established

- The Moved toast branch for a super admin who is also a member of the target board sets the TempData keys but is not asserted over HTTP, because the markup that renders those keys lands in a later plan. The shared helper is the same code the quest move already uses.
- InMemory does not enforce the unique (EventId, UserId) index, so duplicate-row protection is proven by the repository returning `Changed`, not by a database constraint.
- How the writes translate to SQL is not exercised; the harness uses the EF Core InMemory provider.

## Known Stubs

None.

## Threat Flags

None. The new endpoints sit under the existing class-level `SuperAdminOnly` policy and are the surface the plan's threat model covers (T-92-02, T-92-25 to T-92-28); each has a test above.

## Commits

- 13bd6d03: event move save reconciles answers and adds Campaign rows
- 0b3cdd83: event move preview and service with membership and Campaign fill-in
- cb7a1619: event Move actions on the shared page with HTTP proofs

## Self-Check: PASSED

All files listed above exist and the three commits are in `git log`.
