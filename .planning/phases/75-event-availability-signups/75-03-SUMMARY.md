---
phase: 75-event-availability-signups
plan: 03
subsystem: api
tags: [aspnet-core-mvc, automapper, event-signup, availability, tenant-isolation]

# Dependency graph
requires:
  - phase: 75-event-availability-signups (plan 01, sibling wave)
    provides: EventSignup domain model, IEventSignupRepository/IEventSignupService, EventEntity.Signups navigation
  - phase: 75-event-availability-signups (plan 02, sibling wave)
    provides: GroupRepository.AddMemberAsync/RemoveMemberAsync membership-synced signup backfill/cleanup
provides:
  - EventsController.SetAvailability and Withdraw write actions, both re-resolving the acting user from User only
  - EventsController.EventIsOnActiveBoard, a second explicit board comparison over the read filter
  - EventsController.Details populates Roster, IsOneShotBoard, HasOwnSignup, MyAvailability, SignupCount from a single roster fetch
  - IEventRepository/IEventService.AddWithCampaignFanOutAsync, an atomic event-plus-fan-out insert used by EventsController.Create on a campaign board
  - EventSignupViewModel and five new EventViewModel members, wired through ViewModelProfile
affects: [75-04, 75-05]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Second explicit board-ownership comparison (EventIsOnActiveBoard) layered over the entity query filter before every signup write, mirroring SeriesIsOnActiveBoardAsync"
    - "Server-side re-resolution of board type on a write (IBoardTypeResolver), never trusting client-rendered control visibility, mirroring QuestController.Close"
    - "Single-DbContext staged mutation (entity.Signups.Add + one SaveChangesAsync) for an atomic parent-plus-children insert, matching CharacterRepository.UpdateWithProfileImageAsync's single-save shape"

key-files:
  created:
    - QuestBoard.Service/ViewModels/EventViewModels/EventSignupViewModel.cs
  modified:
    - QuestBoard.Service/ViewModels/EventViewModels/EventViewModel.cs
    - QuestBoard.Service/Automapper/ViewModelProfile.cs
    - QuestBoard.Service/Controllers/Events/EventsController.cs
    - QuestBoard.Domain/Interfaces/IEventRepository.cs
    - QuestBoard.Domain/Interfaces/IEventService.cs
    - QuestBoard.Domain/Services/EventService.cs
    - QuestBoard.Repository/EventRepository.cs

key-decisions:
  - "SetAvailability and Withdraw take no user, member or signup identifier from route or form input at all -- the acting user comes only from userService.GetUserAsync(User)"
  - "Withdraw and SetAvailability contain no comparison against the event's date or today's date, per planner decision PD-01 -- a past-dated event stays answerable and withdrawable"
  - "Campaign fan-out uses the role-agnostic GetAllGroupMembersAsync, not a player-filtered list, so Dungeon Masters and Admins also get an automatic Yes row"
  - "AddWithCampaignFanOutAsync uses one SaveChangesAsync for the whole event+signups graph, no BeginTransactionAsync call, matching the codebase's existing single-save atomicity pattern"

patterns-established:
  - "Pattern: a write action re-derives its own board-ownership fact from the loaded entity's GroupId rather than trusting the read-side query filter alone, closing the same class of gap SeriesIsOnActiveBoardAsync already closed for series edits"

requirements-completed: [EVTAVAIL-01, EVTAVAIL-02, EVTAVAIL-03]

coverage:
  - id: D1
    description: "A signed-in member posts Yes/Maybe/No via SetAvailability and the answer is recorded for their own userId only, with no user/member/signup identifier accepted from the request"
    requirement: "EVTAVAIL-01"
    verification:
      - kind: other
        ref: "sed -n '/SetAvailability(int id/,/^    }/p;/Withdraw(int id/,/^    }/p' QuestBoard.Service/Controllers/Events/EventsController.cs | grep -Ec 'userId|UserId|signupId' -> 0"
        status: pass
    human_judgment: false
  - id: D2
    description: "SetAvailability and Withdraw both re-verify the loaded event's GroupId against the active board (EventIsOnActiveBoard) before writing, refusing a cross-board write with 404 even if the read filter is weakened"
    requirement: "EVTAVAIL-03"
    verification:
      - kind: other
        ref: "grep -c 'private bool EventIsOnActiveBoard(' QuestBoard.Service/Controllers/Events/EventsController.cs -> 1"
        status: pass
    human_judgment: true
    rationale: "The two-board cross-tenant isolation behavior (posting against another board's event returns 404 and writes no row) is asserted end-to-end by plan 75-05's integration tests, not yet written at the time this plan executed."
  - id: D3
    description: "Withdraw refuses with 400 on anything that is not a one-shot board (including a null board type), re-resolved server-side rather than trusted from the browser"
    requirement: "EVTAVAIL-02"
    verification:
      - kind: other
        ref: "grep -c 'boardType != BoardType.OneShot' QuestBoard.Service/Controllers/Events/EventsController.cs -> 1"
        status: pass
    human_judgment: true
    rationale: "The end-to-end crafted-request-on-campaign-board-refused behavior is asserted by plan 75-05's integration tests, not yet written at the time this plan executed."
  - id: D4
    description: "Details populates Roster, IsOneShotBoard, HasOwnSignup, MyAvailability and computed SignupCount from a single GetRosterForEventAsync call"
    verification:
      - kind: integration
        ref: "QuestBoard.IntegrationTests (EventsController suite, 14 tests) - all pass unchanged after Details rewiring"
        status: pass
    human_judgment: false
  - id: D5
    description: "Creating an event on a campaign board writes the event and one Yes row per member (any role) in a single SaveChangesAsync via AddWithCampaignFanOutAsync; a one-shot board create is unchanged"
    requirement: "EVTAVAIL-01"
    verification:
      - kind: other
        ref: "sed -n '/AddWithCampaignFanOutAsync(Event newEvent/,/^    }/p' QuestBoard.Repository/EventRepository.cs | grep -c 'SaveChangesAsync' -> 1"
        status: pass
      - kind: unit
        ref: "dotnet test (full suite) -> 333 unit + 472 integration passing, no regressions"
        status: pass
    human_judgment: true
    rationale: "The specific fan-out-writes-one-row-per-member-in-one-save behavior has no dedicated unit test in this plan; plan 75-05 owns the isolation/fan-out proof tests referenced in the plan's acceptance criteria."

# Metrics
duration: ~35min
completed: 2026-08-27
status: complete
---

# Phase 75 Plan 03: EventsController Availability Writes and Campaign Fan-Out Summary

**SetAvailability/Withdraw write actions on EventsController with a second explicit board-ownership check, roster/availability state wired into event Details, and an atomic AddWithCampaignFanOutAsync repository path used by Create on a campaign board**

## Performance

- **Duration:** ~35 min
- **Started:** 2026-08-27T13:35:00Z (approx.)
- **Completed:** 2026-08-27T14:10:17Z
- **Tasks:** 3
- **Files modified:** 8 (1 created, 7 modified)

## Accomplishments

- `EventSignupViewModel` (UserId, UserName, Availability) and five new `EventViewModel` members (`Roster`, `IsOneShotBoard`, `HasOwnSignup`, `MyAvailability`, computed `SignupCount`), none of which surface the domain model's answered marker
- `ViewModelProfile` maps `EventSignup -> EventSignupViewModel` and ignores all five server-computed `EventViewModel` members on the forward map, alongside the existing `CanManage` ignore
- `EventsController.Details` now populates the full roster, the viewer's own answer, whether they hold a row, and the board type from one `GetRosterForEventAsync` call
- `EventsController.SetAvailability` (`POST`) and `EventsController.Withdraw` (`DELETE`) are new write actions: the acting user comes only from `userService.GetUserAsync(User)`, the posted `VoteType` is validated with `Enum.IsDefined`, and both re-verify the event's board via the new `EventIsOnActiveBoard` helper before writing -- a second, explicit layer over the read-side query filter
- `Withdraw` re-resolves the board type via `IBoardTypeResolver` and refuses (400) anything that is not `BoardType.OneShot`, including a null result -- fail-closed
- Neither new action contains a date comparison, so a past-dated event remains answerable and withdrawable (planner decision PD-01)
- `IEventRepository`/`IEventService`.`AddWithCampaignFanOutAsync` inserts an event together with one automatic Yes signup per member id in a single `SaveChangesAsync`, using `memberIds.Distinct()` to protect the unique (EventId, UserId) index and leaving the answered-marker column unset on every automatic row
- `EventsController.Create` (POST) now branches on the resolved board type: a campaign board fans out to every member of any role via the role-agnostic `GetAllGroupMembersAsync`; a one-shot board create is byte-for-byte what it was before

## Task Commits

Each task was committed atomically:

1. **Task 1: EventSignupViewModel, EventViewModel availability state, and the ViewModelProfile entry** - `7e72d75` (feat)
2. **Task 2: EventsController — Details roster wiring, SetAvailability, Withdraw** - `8da217a` (feat)
3. **Task 3: Atomic create-with-fan-out repository path and the Create action wiring** - `f1abe4a` (feat)

**Plan metadata:** pending (docs: complete plan — committed by this same agent immediately after this file)

## Files Created/Modified

- `QuestBoard.Service/ViewModels/EventViewModels/EventSignupViewModel.cs` - New view model, deliberately without an answered-marker field
- `QuestBoard.Service/ViewModels/EventViewModels/EventViewModel.cs` - Added Roster, IsOneShotBoard, HasOwnSignup, MyAvailability, computed SignupCount
- `QuestBoard.Service/Automapper/ViewModelProfile.cs` - Added EventSignup->EventSignupViewModel map; ignored the five server-computed EventViewModel members
- `QuestBoard.Service/Controllers/Events/EventsController.cs` - Added IEventSignupService/IBoardTypeResolver dependencies, EventIsOnActiveBoard helper, SetAvailability, Withdraw, Details roster wiring, Create fan-out branch
- `QuestBoard.Domain/Interfaces/IEventRepository.cs` - Added AddWithCampaignFanOutAsync contract
- `QuestBoard.Domain/Interfaces/IEventService.cs` - Mirrored the same contract
- `QuestBoard.Domain/Services/EventService.cs` - Added the pass-through implementation
- `QuestBoard.Repository/EventRepository.cs` - Implemented AddWithCampaignFanOutAsync with a single save

## Decisions Made

- Followed the plan's locked decisions exactly (acting-user resolution, two-layer board check, fail-closed null board type, role-agnostic member fan-out, no date comparison, single-save atomicity with no explicit transaction).
- No new decisions were required beyond what the plan already specified.

## Deviations from Plan

**1. [Comment wording only] Removed the literal word "UpdatedAt" from a code comment inside `AddWithCampaignFanOutAsync`**
- **Found during:** Task 3 acceptance-criteria verification
- **Issue:** The plan's acceptance criteria require `grep -c 'UpdatedAt' <method body>` to output `0`, as a structural guarantee that the fan-out path never touches the answered-marker column. My first draft of the explanatory comment mentioned "UpdatedAt" by name (to explain *why* the column is left at its default), which the grep would have flagged even though the code itself sets no such field.
- **Fix:** Reworded the comment to describe the same fact ("the answered-marker column ... at its default") without naming the column, preserving the explanation while satisfying the literal grep check.
- **Files modified:** `QuestBoard.Repository/EventRepository.cs`
- **Verification:** `sed -n '/AddWithCampaignFanOutAsync(Event newEvent/,/^    }/p' QuestBoard.Repository/EventRepository.cs | grep -c 'UpdatedAt'` now outputs `0`; `dotnet build` and the full test suite still pass.
- **Committed in:** `f1abe4a` (Task 3 commit)

---

**Total deviations:** 1 auto-fixed (comment wording, no behavior change)
**Impact on plan:** No functional or scope impact -- the code always left the answered marker unset; only the comment's wording changed to satisfy the plan's own literal verification check.

## Issues Encountered

None beyond the comment-wording note above.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

- All three write paths this plan opens (`SetAvailability`, `Withdraw`, campaign `Create` fan-out) are in place with the exact signatures plan 75-05's tenant-isolation and roster-rendering tests depend on.
- `EventIsOnActiveBoard` is available as the second-layer check plan 75-05 will assert against a two-group setup.
- No migration was added or needed -- this plan is controller/repository wiring only, no schema change.
- `dotnet build` exits 0; full `dotnet test` is green (333 unit + 472 integration tests passing, no regressions).
- Plan 75-05's dedicated tenant-isolation and roster-render tests were not yet written at the time this plan executed (they are a sibling/later plan's responsibility per the phase's artifact table), so three coverage entries above route to human judgment pending those tests landing.

---
*Phase: 75-event-availability-signups*
*Completed: 2026-08-27*
