---
phase: 84-calendar-feed-foundation-and-event-subscription
plan: 02
subsystem: calendar-feed
tags: [ef-core, tenant-isolation, rfc5545, aspnetcore, integration-test]

requires:
  - phase: 82-personal-cross-board-event-agenda
    provides: the IgnoreQueryFilters() + pinned-membership + second-layer-re-check pattern this plan's feed query and service reuse
  - phase: 75-event-availability
    provides: EventSignupEntity/EventSignup (Availability, HasAnswered) that the feed query and writer read from
provides:
  - CalendarSubscriptions table (Token unique-indexed, RevokedAt tombstone, no GroupId, no query filter) and its migration
  - ICalendarSubscriptionRepository/Service, EventSignups-rooted GetFeedRowsForUserAsync, CalendarFeedWriter (timed VEVENT), CalendarFeedController
  - A proven, real-HTTP tracer fact that an anonymous fetch of a minted address returns the correct VCALENDAR body
affects: [84-03, 84-04, 84-05, 84-06, 84-07, 84-08, 85]

actuals:
  tokens: 27365
  tasks: 3
  commits: 2

tech-stack:
  added: []
  patterns:
    - "EventSignups-rooted cross-board read (third instance of the pinned-membership + IgnoreQueryFilters() + second-layer-recheck pattern, after EventSeriesGenerationJob and Phase 82's agenda)"
    - "Hand-rolled RFC 5545 VEVENT writer (no external calendar library) for a five-field, no-timezone, no-recurrence surface"
    - "Anonymous, token-bearer-authenticated controller deliberately excluded from GroupSessionMiddleware's exempt-path list"

key-files:
  created:
    - QuestBoard.Repository/Entities/CalendarSubscriptionEntity.cs
    - QuestBoard.Repository/CalendarSubscriptionRepository.cs
    - QuestBoard.Repository/Migrations/20260918080027_AddCalendarSubscriptions.cs
    - QuestBoard.Domain/Enums/CalendarFeedSource.cs
    - QuestBoard.Domain/Enums/CalendarFeedStatus.cs
    - QuestBoard.Domain/Models/CalendarSubscription.cs
    - QuestBoard.Domain/Models/CalendarFeedEntry.cs
    - QuestBoard.Domain/Models/CalendarFeedResult.cs
    - QuestBoard.Domain/Models/EventFeedRow.cs
    - QuestBoard.Domain/Interfaces/ICalendarSubscriptionRepository.cs
    - QuestBoard.Domain/Interfaces/ICalendarSubscriptionService.cs
    - QuestBoard.Domain/Interfaces/ICalendarFeedWriter.cs
    - QuestBoard.Domain/Services/CalendarSubscriptionService.cs
    - QuestBoard.Domain/Services/CalendarFeedWriter.cs
    - QuestBoard.Service/Controllers/CalendarFeedController.cs
    - QuestBoard.IntegrationTests/Tests/CalendarSubscriptionFeedTests.cs
  modified:
    - QuestBoard.Repository/Entities/QuestBoardContext.cs
    - QuestBoard.Repository/EventSignupRepository.cs
    - QuestBoard.Domain/Interfaces/IEventSignupRepository.cs
    - QuestBoard.Repository/Automapper/EntityProfile.cs
    - QuestBoard.Repository/Extensions/ServiceExtensions.cs
    - QuestBoard.Domain/Extensions/ServiceExtensions.cs

key-decisions:
  - "Task 1's checkpoint:decision was answered by the operator before this dispatch (proceed-as-specified) -- recorded here, not re-asked. All four one-way concretizations (table shape, 32-byte Base64Url address, questboard-event-{id} identifier, /feeds/calendar route) shipped exactly as specified."
  - "Feed query rooted at DbContext.EventSignups, not DbContext.Events -- a third, distinct instance of the pinned-membership tenant-safety pattern, since GetUpcomingAcrossGroupsWithSignupsAsync (Phase 82) and GetUpcomingWithSignupsAsync are both rooted at Events and cannot be widened into this shape."
  - "CalendarSubscriptions carries no GroupId and no HasQueryFilter -- verified by an unchanged HasQueryFilter count in QuestBoardContext.cs and a passing fact that runs with ActiveGroupId = null."
  - "CalendarFeedController is deliberately not added to GroupSessionMiddleware's ExemptPathPrefixes -- anonymous requests already pass through untouched, and an authenticated browser hitting the same URL with no active board is sent to the board picker by design."

requirements-completed: [CALFEED-01, CALFEED-02, CALFEED-05, CALFEED-06, CALFEED-07, CALFEED-08]

coverage:
  - id: D1
    description: "Anonymous, cookie-less GET against a minted subscription address returns 200 with Content-Type text/calendar; charset=utf-8"
    requirement: CALFEED-05
    verification:
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Tests/CalendarSubscriptionFeedTests.cs#Feed_ServesSubscribedEvent_ToAnonymousCaller"
        status: pass
    human_judgment: false
  - id: D2
    description: "The response body is a single CRLF-correct VCALENDAR containing exactly one VEVENT for the one event the owner holds a signup row on, with no board selected anywhere"
    requirement: CALFEED-06
    verification:
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Tests/CalendarSubscriptionFeedTests.cs#Feed_ServesSubscribedEvent_ToAnonymousCaller"
        status: pass
    human_judgment: false
  - id: D3
    description: "The feed query is EventSignups-rooted with IgnoreQueryFilters() + a pinned memberGroupIds predicate, plus a second-layer re-check that LogErrors on any surviving foreign row"
    requirement: CALFEED-07
    verification:
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Tests/CalendarSubscriptionFeedTests.cs#Feed_ServesSubscribedEvent_ToAnonymousCaller"
        status: pass
    human_judgment: true
    rationale: "This plan's single automated fact proves the happy path only (one member, one board, one event). It does not exercise a genuine cross-board leak scenario (a second board's event/member) the way Phase 82's AgendaTenantIsolationTests suite does -- that multi-board isolation coverage is explicitly the remit of a later wave in this phase per the plan's own artifact list, not this tracer."
  - id: D4
    description: "The address is 32 cryptographically random bytes, Base64Url-encoded to 43 characters, never Guid.NewGuid()"
    requirement: CALFEED-01
    verification:
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Tests/CalendarSubscriptionFeedTests.cs#Feed_ServesSubscribedEvent_ToAnonymousCaller (mints one subscription and round-trips its Token through a live HTTP fetch)"
        status: pass
    human_judgment: true
    rationale: "No automated test asserts the token's exact byte length/entropy or that two independently minted subscriptions produce different addresses and ids -- the acceptance-criteria grep confirms RandomNumberGenerator.GetBytes(32) is the only generator used and Guid.NewGuid() appears nowhere, but a human should confirm this reasoning is sufficient until a later plan's unit test covers it directly."
  - id: D5
    description: "An unknown address answers 404 Not Found and a revoked address answers 410 Gone"
    requirement: CALFEED-08
    verification: []
    human_judgment: true
    rationale: "Task 3's single fact only exercises the Ok/200 branch, matching the plan's explicit scope (one path only). The NotFound/Revoked switch arms exist in CalendarFeedController and are structurally verified (Status410Gone/NotFound() grep checks passed), but no automated request-level fact drives either branch yet."
  - id: D6
    description: "The feed route lives at /feeds/calendar/{feedToken}.ics on its own [AllowAnonymous] controller, outside GroupSessionMiddleware's exempt-path list, and does not collide with the existing /calendar/... prefix"
    requirement: CALFEED-02
    verification:
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Tests/CalendarSubscriptionFeedTests.cs#Feed_ServesSubscribedEvent_ToAnonymousCaller"
        status: pass
    human_judgment: false
duration: 22min
completed: 2026-09-18
status: complete
---

# Phase 84 Plan 2: Calendar Feed Tracer -- One Subscription, One Board, One Real HTTP Fetch Summary

**Anonymous GET /feeds/calendar/{token}.ics now serves a real, CRLF-correct VCALENDAR for one member's signed-up event, with the CalendarSubscriptions tombstone table, the EventSignups-rooted tenant-safe query, and the hand-rolled ICS writer all wired end to end and proven over real HTTP with no active board.**

## Performance

- **Duration:** 22 min
- **Started:** 2026-09-18T07:46:15Z (previous plan's close-out commit)
- **Completed:** 2026-09-18T08:06:56Z
- **Tasks:** 3 (Task 1 checkpoint answered by the operator before dispatch; Task 2 tracer built; Task 3 tracer proven)
- **Files modified:** 24 (16 created, 6 modified, plus the two migration files and updated model snapshot counted above)

## Accomplishments

- `CalendarSubscriptions` table shipped with `Id`, `UserId`, `Name`, `Token` (unique-indexed), `CreatedAt`, `LastFetchedAt`, `RevokedAt` -- no `GroupId`, no query filter, by design
- A new `EventSignups`-rooted repository read (`GetFeedRowsForUserAsync`) that a signup-less event can never pass through, carrying the same `IgnoreQueryFilters()` + pinned-membership safety shape as Phase 82's agenda query but structurally distinct from it
- `CalendarSubscriptionService.GetFeedAsync` performs the second-layer re-check with `LogError` on any row falling outside the owner's board set, and never resolves `IActiveGroupContext`
- `CalendarFeedWriter` hand-rolls a CRLF-terminated `VCALENDAR`/`VEVENT` for the timed branch, with a `questboard-event-{id}` `UID` that cannot drift with configuration
- `CalendarFeedController` serves the anonymous `.ics` GET at `/feeds/calendar/{feedToken}.ics`, returning `404`/`410`/`200` correctly, deliberately left out of `GroupSessionMiddleware`'s exempt-path list
- A real-HTTP integration fact (`Feed_ServesSubscribedEvent_ToAnonymousCaller`) proves the whole path with no authorization header and no active board selected

## Task Commits

Each task was committed atomically:

1. **Task 2: End-to-end "one subscribed event reaches a calendar client"** - `50636a0a` (feat)
2. **Task 3: Prove the tracer end to end over real HTTP** - `f81690d0` (test)

_Task 1 (`checkpoint:decision`) required no commit -- the operator's `proceed-as-specified` answer was recorded before this dispatch and is documented under Key Decisions above._

**Plan metadata:** committed as part of this SUMMARY.

## Files Created/Modified

- `QuestBoard.Repository/Entities/CalendarSubscriptionEntity.cs` - the token table entity, tombstone-column idiom copied from `EventEntity.CancelledAt`
- `QuestBoard.Repository/Entities/QuestBoardContext.cs` - new `DbSet`, unique `Token` index, non-unique `UserId` index, no query filter
- `QuestBoard.Repository/CalendarSubscriptionRepository.cs` - `GetByTokenAsync`, `MintAsync`, `GetForUserAsync`, `RenameAsync`, `RevokeAsync`, `TouchLastFetchedAsync`
- `QuestBoard.Repository/EventSignupRepository.cs` - new `GetFeedRowsForUserAsync`, rooted at `EventSignups`
- `QuestBoard.Domain/Interfaces/IEventSignupRepository.cs` - new method signature and doc comment
- `QuestBoard.Repository/Automapper/EntityProfile.cs` - `CalendarSubscriptionEntity`/`CalendarSubscription` maps
- `QuestBoard.Repository/Extensions/ServiceExtensions.cs`, `QuestBoard.Domain/Extensions/ServiceExtensions.cs` - DI registrations, including the singleton `ICalendarFeedWriter`
- `QuestBoard.Domain/Enums/CalendarFeedSource.cs`, `CalendarFeedStatus.cs` - the source-namespacing enum and the feed's tri-state result
- `QuestBoard.Domain/Models/CalendarSubscription.cs`, `CalendarFeedEntry.cs`, `CalendarFeedResult.cs`, `EventFeedRow.cs` - the domain shapes
- `QuestBoard.Domain/Interfaces/ICalendarSubscriptionRepository.cs`, `ICalendarSubscriptionService.cs`, `ICalendarFeedWriter.cs` - interface-first contracts for later plans to widen
- `QuestBoard.Domain/Services/CalendarSubscriptionService.cs`, `CalendarFeedWriter.cs` - mint/rename/revoke/feed-read service and the hand-rolled ICS writer
- `QuestBoard.Service/Controllers/CalendarFeedController.cs` - the anonymous `.ics` GET
- `QuestBoard.Repository/Migrations/20260918080027_AddCalendarSubscriptions.cs` (+ `.Designer.cs`, `QuestBoardContextModelSnapshot.cs`) - the schema migration
- `QuestBoard.IntegrationTests/Tests/CalendarSubscriptionFeedTests.cs` - the real-HTTP tracer proof

## Decisions Made

See `key-decisions` in the frontmatter above. The most consequential: the feed query is a genuinely new, third instance of the tenant-safety pattern rooted at `EventSignups` rather than a widening of any existing `Events`-rooted method, and the new table carries no board scope at all by design.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] Adapted the plan's hard-coded verification paths to the actual Windows checkout**
- **Found during:** Task 2 and Task 3
- **Issue:** The plan's `<verify>` blocks specified `cd /mnt/Data/repos/quest-board-dnd && dotnet build ...` / `dotnet test ...` -- a Linux path from a different environment than this Windows checkout (`C:\Repos\quest-board`).
- **Fix:** Ran the equivalent `dotnet build` / `dotnet test` commands directly from the repository root, with no `cd` to a nonexistent path.
- **Files modified:** None (verification-only).
- **Verification:** `dotnet build` and `dotnet test` both ran and passed from the correct root.
- **Committed in:** N/A (no code change).

**2. [Rule 1 - Bug] Fixed a board-name collision in the integration test's own seed data**
- **Found during:** Task 3, first test run
- **Issue:** The test's first draft seeded its own named board at group id 1, which `TestDataHelper.ClearDatabaseAsync` had already seeded as `"EuphoriaInn"` -- the seeding helper's `if (!ctx.Groups.Any(...))` guard skipped renaming it, so the feed correctly rendered `SUMMARY:[EuphoriaInn] ...` and the assertion on the test's own board name failed.
- **Fix:** Moved the test's board to group id 2 (matching the convention every other tenant-isolation suite in this codebase already uses for a board it wants to name itself), leaving group 1 as the untouched default.
- **Files modified:** `QuestBoard.IntegrationTests/Tests/CalendarSubscriptionFeedTests.cs`
- **Verification:** Test passes; `dotnet test QuestBoard.IntegrationTests --filter CalendarSubscriptionFeedTests` is green.
- **Committed in:** `f81690d0` (Task 3 commit)

---

**Total deviations:** 2 auto-fixed (1 blocking path adaptation, 1 bug in test seed data)
**Impact on plan:** Neither affects production code or scope. No scope creep.

## Issues Encountered

None beyond the deviations above.

## User Setup Required

None - no external service configuration required. `dotnet-ef` was already installed and no new NuGet package was added in this plan.

## Next Phase Readiness

- The tracer's proven path (mint → anonymous fetch → correct VCALENDAR body) is committed and green, so plans 84-03 through 84-08 can widen the writer (all-day/escaping/folding/vote suffix), the throttle, the Profile UI, and the retention sweep behind interfaces that already exist rather than changing their shapes.
- Not yet covered by an automated fact, and explicitly deferred to later waves per the plan's own artifact list: the 404/410 status branches, a genuine multi-board leak scenario, and address-distinctness across two mintings -- see coverage `D3`/`D4`/`D5` above for the precise gaps a reviewer should weigh.
- `84-03-T3`'s invariant test (distinct identifiers per source, stability across renders, no configured host in the identifier) has not yet been written -- it is planned for plan 84-03, not this plan.

## Self-Check: PASSED

- `QuestBoard.Repository/Entities/CalendarSubscriptionEntity.cs` - FOUND
- `QuestBoard.Repository/CalendarSubscriptionRepository.cs` - FOUND
- `QuestBoard.Domain/Services/CalendarSubscriptionService.cs` - FOUND
- `QuestBoard.Domain/Services/CalendarFeedWriter.cs` - FOUND
- `QuestBoard.Domain/Interfaces/ICalendarFeedWriter.cs` - FOUND
- `QuestBoard.Service/Controllers/CalendarFeedController.cs` - FOUND
- `QuestBoard.IntegrationTests/Tests/CalendarSubscriptionFeedTests.cs` - FOUND
- `QuestBoard.Repository/Migrations/20260918080027_AddCalendarSubscriptions.cs` - FOUND
- Commit `50636a0a` - FOUND in `git log --oneline --all`
- Commit `f81690d0` - FOUND in `git log --oneline --all`
- `dotnet build` - PASSED (0 errors)
- `dotnet test QuestBoard.IntegrationTests --filter CalendarSubscriptionFeedTests` - PASSED (1/1)
- `dotnet test` (full solution) - PASSED (455 unit + 720 integration, 0 failures)

---
*Phase: 84-calendar-feed-foundation-and-event-subscription*
*Completed: 2026-09-18*
