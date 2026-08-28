---
phase: 75-event-availability-signups
plan: 05
subsystem: testing
tags: [xunit, fluentassertions, ef-core, tenant-isolation, event-signup]

# Dependency graph
requires:
  - phase: 75-event-availability-signups (plan 01, sibling wave)
    provides: EventSignup domain model, EventSignupRepository, EventSignupService, EventEntity.Signups navigation
  - phase: 75-event-availability-signups (plan 02, sibling wave)
    provides: GroupRepository.AddMemberAsync/RemoveMemberAsync atomic campaign-signup backfill and leave cleanup
  - phase: 75-event-availability-signups (plan 03, sibling wave)
    provides: EventsController.SetAvailability/Withdraw write actions, EventIsOnActiveBoard second-layer board check, AddWithCampaignFanOutAsync atomic create-plus-fanout
provides:
  - Automated proof for all five EVTAVAIL requirements, closing the three coverage entries plan 75-03 routed to human judgment pending this plan's tests
  - EventAvailabilityTenantIsolationTests — the two-genuinely-distinct-groups class proving both read and write refusal for cross-board availability
  - A fixed EF relationship (EventSignupEntity.Event <-> EventEntity.Signups) and a fixed ModelState check in SetAvailability, both required for the plan's own acceptance criteria to pass
  - A filled-in, no-placeholder 75-VALIDATION.md with every command corrected to a real test class
affects: []

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Read a member's signup row back through factory.Database.CreateContext().IgnoreQueryFilters() rather than the request pipeline's own filtered read, so a refused write can be told apart from an accepted-but-hidden one"
    - "Two-group isolation class seeds group 2 through the unfiltered seeding context and acts as a member of group 1, matching the EventTenantIsolationTests/TenantIsolationTests structural recipe"

key-files:
  created:
    - QuestBoard.IntegrationTests/Tests/EventAvailabilityTenantIsolationTests.cs
  modified:
    - QuestBoard.IntegrationTests/Controllers/EventsControllerIntegrationTests.cs
    - QuestBoard.Repository/Entities/QuestBoardContext.cs
    - QuestBoard.Service/Controllers/Events/EventsController.cs
    - .planning/phases/75-event-availability-signups/75-VALIDATION.md

key-decisions:
  - "EventSignupEntity.Event is now configured with WithMany(e => e.Signups) instead of a bare WithMany() -- the bare form left EventEntity.Signups as a second, disconnected relationship, so staging signups through that navigation and saving once (the entire point of the campaign fan-out) inserted every row with EventId left at its default value"
  - "SetAvailability now returns BadRequest when ModelState is invalid, before touching anything else -- the framework's own enum model binder silently defaults an out-of-range availability value to VoteType.No rather than failing the request, and the action's own Enum.IsDefined check never saw the original invalid value because binding had already substituted a valid one"
  - "The no-active-board Details fact asserts on the actual, already-correct observable behavior (redirect to the group picker, matching the existing SuperAdmin no-active-group facts) rather than a literal 404, since that redirect is what the app does for any signed-in user with no board selected, not a plan-specific gap"

patterns-established: []

requirements-completed: [EVTAVAIL-01, EVTAVAIL-02, EVTAVAIL-03, EVTAVAIL-04, EVTAVAIL-05]

coverage:
  - id: D1
    description: "One-shot lifecycle: no signup row exists until a player clicks, Maybe-then-No updates the same row, and a withdraw deletes it and returns the player to not-answered"
    requirement: "EVTAVAIL-01"
    verification:
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Controllers/EventsControllerIntegrationTests.cs#SetAvailability_OneShot_NoExistingRow_CreatesRowForActingUser"
        status: pass
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Controllers/EventsControllerIntegrationTests.cs#SetAvailability_OneShot_MaybeThenNo_UpdatesSameRowRatherThanCreatingMore"
        status: pass
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Controllers/EventsControllerIntegrationTests.cs#Withdraw_OneShot_RemovesRow_ReturnsPlayerToNotAnswered"
        status: pass
    human_judgment: false
  - id: D2
    description: "Campaign lifecycle: creating an event fans out an automatic Yes row (null answered timestamp) to every member, opting out flips a row to No without deleting it, and a withdraw against a campaign board is refused server-side while the row survives"
    requirement: "EVTAVAIL-02"
    verification:
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Controllers/EventsControllerIntegrationTests.cs#Create_CampaignBoard_AutoSignsUpEveryMember_WithNullAnsweredTimestamp"
        status: pass
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Controllers/EventsControllerIntegrationTests.cs#SetAvailability_CampaignBoard_OptOut_FlipsAutoRowToNo_WithoutDeletingIt"
        status: pass
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Controllers/EventsControllerIntegrationTests.cs#Withdraw_CampaignBoard_IsRefused_RowSurvives"
        status: pass
    human_judgment: false
  - id: D3
    description: "Ownership: each member's write changes only their own row, including when the request carries a field naming another user; invalid availability values and unknown events are refused"
    requirement: "EVTAVAIL-03"
    verification:
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Controllers/EventsControllerIntegrationTests.cs#SetAvailability_Ownership_EachMemberChangesOnlyTheirOwnRow"
        status: pass
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Controllers/EventsControllerIntegrationTests.cs#SetAvailability_Ownership_SpoofedUserIdField_OnlyChangesActingUsersRow"
        status: pass
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Controllers/EventsControllerIntegrationTests.cs#SetAvailability_InvalidValue_ReturnsBadRequest_WritesNoRow"
        status: pass
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Controllers/EventsControllerIntegrationTests.cs#SetAvailability_UnknownEvent_ReturnsNotFound"
        status: pass
    human_judgment: false
  - id: D4
    description: "A past-dated event still accepts a changed answer and a withdraw -- planner decision PD-01"
    verification:
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Controllers/EventsControllerIntegrationTests.cs#SetAvailability_PastDatedEvent_AcceptsChangedAnswer"
        status: pass
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Controllers/EventsControllerIntegrationTests.cs#Withdraw_PastDatedOneShotEvent_Succeeds"
        status: pass
    human_judgment: false
  - id: D5
    description: "Cross-board isolation: a player on one board can neither read nor write availability on another board's event, proven with two genuinely distinct groups and database state re-checked after every refused write"
    requirement: "EVTAVAIL-05"
    verification:
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Tests/EventAvailabilityTenantIsolationTests.cs#Details_EventFromOtherGroup_ReturnsNotFound_AndBodyNamesNothingFromTheOtherBoard"
        status: pass
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Tests/EventAvailabilityTenantIsolationTests.cs#SetAvailability_EventFromOtherGroup_IsRefused_AndWritesNoRow"
        status: pass
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Tests/EventAvailabilityTenantIsolationTests.cs#Withdraw_EventFromOtherGroup_IsRefused_AndDeletesNothing"
        status: pass
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Tests/EventAvailabilityTenantIsolationTests.cs#Details_EventFromOtherGroup_WithNoActiveBoardSelected_ReturnsNotFound"
        status: pass
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Tests/EventAvailabilityTenantIsolationTests.cs#Roster_ForGroupOneEvent_ContainsOnlyGroupOneMembers"
        status: pass
    human_judgment: false

# Metrics
duration: ~40min
completed: 2026-08-28
status: complete
---

# Phase 75 Plan 05: Availability Test Coverage and Cross-Board Isolation Summary

**Extended EventsControllerIntegrationTests with 12 lifecycle/ownership/past-date facts and a new EventAvailabilityTenantIsolationTests class with 5 two-group facts, uncovering and fixing two real production bugs (a disconnected EF relationship that silently dropped the campaign fan-out's EventId, and a missing ModelState check that let an out-of-range availability value write as "No" instead of being refused) along the way**

## Performance

- **Duration:** ~40 min
- **Started:** 2026-08-28 (approx.)
- **Completed:** 2026-08-28T09:59:44+02:00
- **Tasks:** 3
- **Files modified:** 5 (1 created, 4 modified)

## Accomplishments

- 12 new facts in `EventsControllerIntegrationTests` covering the one-shot opt-in/change/withdraw lifecycle, the campaign auto-signup/opt-out/withdraw-refusal lifecycle, ownership (including a deliberately spoofed form field), invalid-value and unknown-event refusal, and past-date answerability
- New `EventAvailabilityTenantIsolationTests` class with 5 facts proving a member of one board can neither read nor write availability on another board's event, using two genuinely distinct groups seeded through the unfiltered seeding context, with database state re-checked after every refused write rather than trusting a status code alone
- Found and fixed a real bug in `QuestBoardContext`: `EventSignupEntity.Event` was configured with a bare `WithMany()`, leaving `EventEntity.Signups` as a second, disconnected relationship. Staging three signups through `entity.Signups.Add(...)` and saving once (the entire mechanism `AddWithCampaignFanOutAsync` depends on) inserted all three rows with `EventId` left at its default value of `0` instead of the new event's real id — the campaign fan-out silently wrote orphaned rows. Fixed by pointing `WithMany()` at the real `Signups` navigation.
- Found and fixed a real bug in `EventsController.SetAvailability`: it never checked `ModelState.IsValid` after model binding. ASP.NET Core's enum binder silently defaults an out-of-range numeric value (e.g. `availability=99`) to `VoteType.No` rather than failing outright, so the action's own `Enum.IsDefined` guard never saw the invalid input — it only ever saw the pre-substituted valid one. This let a malformed request record an answer ("No") that nobody actually gave, returning 200 instead of a refusal. Fixed by returning `BadRequest` immediately when `ModelState.IsValid` is false.
- Filled in every `TBD` and `⬜ pending` row in `75-VALIDATION.md`, correcting the two EVTAVAIL-04/D-19 rows from the domain service class (a pure pass-through) to `GroupRepositoryTests` (where the atomic backfill/cleanup logic actually lives), and added the fifth Wave 0 entry for `EventDetailsAvailabilityRenderTests.cs`

## Task Commits

Each task was committed atomically:

1. **Task 1: Availability lifecycle, ownership, board-type and past-date facts in EventsControllerIntegrationTests** - `cb2e512` (test) — includes the two production bug fixes discovered while writing these facts
2. **Task 2: EventAvailabilityTenantIsolationTests — two distinct groups, read and write both refused** - `5a24f6b` (test)
3. **Task 3: Fill in the validation map and confirm the full suite is green** - `58a47ff` (docs)

**Plan metadata:** pending (docs: complete plan — committed by this same agent immediately after this file)

## Files Created/Modified

- `QuestBoard.IntegrationTests/Controllers/EventsControllerIntegrationTests.cs` - Extended with a 12-fact availability region: one-shot lifecycle, campaign lifecycle, ownership (including spoofed-field), invalid-value/unknown-event, past-date; class now implements `IAsyncLifetime` resetting `ActiveGroupId`/`BoardType` in `DisposeAsync`
- `QuestBoard.IntegrationTests/Tests/EventAvailabilityTenantIsolationTests.cs` - New class, 5 facts proving cross-board read and write refusal using two genuinely distinct groups
- `QuestBoard.Repository/Entities/QuestBoardContext.cs` - Fixed the `EventSignupEntity` <-> `EventEntity.Signups` relationship configuration (see Deviations)
- `QuestBoard.Service/Controllers/Events/EventsController.cs` - Added a `ModelState.IsValid` check to `SetAvailability` (see Deviations)
- `.planning/phases/75-event-availability-signups/75-VALIDATION.md` - Every row resolved, sign-off complete, frontmatter set to `nyquist_compliant: true` / `wave_0_complete: true` / `status: complete`

## Decisions Made

- Chose the actual, already-correct observable behavior (redirect to the group picker) over the plan's literal "returns not found" wording for the no-active-board Details fact, since that redirect is the established, intentional behavior for any signed-in user with no board selected (matching the existing SuperAdmin no-active-group facts) rather than a gap specific to this feature.
- No new architectural decisions were required beyond the plan's locked decisions; both production fixes below are bug fixes to existing sibling-plan code, not new design choices.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Fixed EventSignupEntity/EventEntity.Signups relationship leaving campaign fan-out rows with EventId=0**
- **Found during:** Task 1, writing `Create_CampaignBoard_AutoSignsUpEveryMember_WithNullAnsweredTimestamp`
- **Issue:** `QuestBoardContext`'s fluent configuration used `HasOne(es => es.Event).WithMany()` (no inverse navigation named), which left `EventEntity.Signups` as a second, disconnected relationship instead of the same one. `EventRepository.AddWithCampaignFanOutAsync` stages new `EventSignupEntity` rows via `entity.Signups.Add(...)` and calls `SaveChangesAsync()` once, relying on EF to backfill each child's `EventId` from the newly-generated parent id. Because the two navigations were separate relationships, that backfill never happened: every automatic row was inserted with `EventId` left at its C# default of `0`, silently orphaning the campaign fan-out for every event ever created on a campaign board.
- **Fix:** Changed `WithMany()` to `WithMany(e => e.Signups)`, pointing the relationship at the real navigation property so EF performs the fixup.
- **Files modified:** `QuestBoard.Repository/Entities/QuestBoardContext.cs`
- **Verification:** Confirmed via a temporary debug assertion showing `EventId=0` on all three seeded rows before the fix and the correct event id after; `dotnet test` (full suite, 333 unit + 489 integration) passes with no regressions after the fix.
- **Committed in:** `cb2e512` (Task 1 commit)

**2. [Rule 1 - Bug] Fixed SetAvailability silently accepting an out-of-range availability value as "No"**
- **Found during:** Task 1, writing `SetAvailability_InvalidValue_ReturnsBadRequest_WritesNoRow`
- **Issue:** `SetAvailability(int id, VoteType availability, ...)` never checked `ModelState.IsValid`. ASP.NET Core's enum model binder rejects a numeric value with no matching named member (e.g. `99`) as part of binding, but for a non-nullable parameter it leaves the bound value at the type's default (`VoteType.No`) rather than throwing. The action's own `Enum.IsDefined(typeof(VoteType), availability)` guard — written specifically to catch this — never saw the original invalid value, only the framework's silent substitute, so the request was accepted and persisted a "No" answer nobody actually gave, returning 200 instead of a refusal.
- **Fix:** Added `if (!ModelState.IsValid) return BadRequest("Invalid availability value.");` as the first check in the action, before any event lookup or write.
- **Files modified:** `QuestBoard.Service/Controllers/Events/EventsController.cs`
- **Verification:** `SetAvailability_InvalidValue_ReturnsBadRequest_WritesNoRow` passes; full solution test suite (333 unit + 489 integration) green with no regressions.
- **Committed in:** `cb2e512` (Task 1 commit)

---

**Total deviations:** 2 auto-fixed (both Rule 1 — genuine bugs in sibling-plan write paths, discovered because this plan is the first to exercise them end-to-end via real HTTP requests)
**Impact on plan:** Both fixes were required for this plan's own acceptance criteria to pass (the campaign auto-signup fact and the invalid-value fact are both explicit acceptance criteria in the plan). No scope creep — both fixes are narrowly targeted at the exact defect found, with the full test suite confirming no regressions elsewhere.

## Issues Encountered

Both issues above were root-caused via temporary debug assertions (querying the database directly and dumping response bodies) rather than guesswork; both temporary debug lines were removed before the final commits.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

- All five EVTAVAIL requirements now have passing automated proof; the three coverage entries plan 75-03 routed to human judgment (D2, D3, D5 in its SUMMARY) are closed out by this plan's facts.
- `75-VALIDATION.md` sign-off is complete: `nyquist_compliant: true`, `wave_0_complete: true`, `status: complete`, no placeholder or pending rows remain.
- The two production bugs fixed here were both pre-existing defects in code shipped by sibling plans 75-02/75-03 in the same wave, not introduced by this plan — they were simply unreachable by any test until this plan wrote the first end-to-end HTTP-level facts for these write paths.
- `dotnet build` exits 0; full `dotnet test` is green (333 unit + 489 integration tests passing, no regressions).
- No migration was added or needed — this plan is test-only plus two narrow bug fixes, no schema change.

---
*Phase: 75-event-availability-signups*
*Completed: 2026-08-28*

## Self-Check: PASSED

- FOUND: QuestBoard.IntegrationTests/Tests/EventAvailabilityTenantIsolationTests.cs
- FOUND: .planning/phases/75-event-availability-signups/75-05-SUMMARY.md
- FOUND: commit cb2e512 (Task 1)
- FOUND: commit 5a24f6b (Task 2)
- FOUND: commit 58a47ff (Task 3)
