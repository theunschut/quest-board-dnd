---
phase: 75-event-availability-signups
plan: 04
subsystem: ui
tags: [aspnet-core-mvc, razor, event-signup, availability, fetch-api, antiforgery]

# Dependency graph
requires:
  - phase: 75-event-availability-signups (plan 03, sibling wave)
    provides: EventsController.SetAvailability/Withdraw write actions, EventViewModel.Roster/IsOneShotBoard/HasOwnSignup/MyAvailability/SignupCount, EventSignupViewModel
provides:
  - "Events/Details.cshtml as the single availability surface: three answer buttons, a board-type-gated withdraw control, a named Yes/Maybe/No roster, and a signup-count-aware delete confirmation"
  - "setAvailability(eventId, availability) and withdrawAvailability(eventId) fetch functions carrying the antiforgery token, following the established changeVote/revokeSignup idiom from Quest/Details.cshtml"
affects: [75-05]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "A JS handler that only one conditionally-rendered control calls lives inside that same Razor @if block rather than a shared, always-rendered @section Scripts block -- this keeps the control's server-computed visibility observable in the rendered HTML rather than masked by an unconditionally-present function definition"

key-files:
  created:
    - QuestBoard.IntegrationTests/Controllers/EventDetailsAvailabilityRenderTests.cs
  modified:
    - QuestBoard.Service/Views/Events/Details.cshtml

key-decisions:
  - "The withdrawAvailability() function definition was moved out of the shared @section Scripts block and placed inline next to its only caller, inside the same Model.IsOneShotBoard && Model.HasOwnSignup conditional as the withdraw button -- both to avoid a function with no valid caller existing on a page where the control never renders, and because a function that is always present regardless of board type made the control's conditional visibility impossible to assert against the rendered response body."
  - "Followed the plan's locked decisions exactly otherwise: three answer buttons render for any signed-in member on either board type; the roster is visible to every board member (not gated on CanManage); a one-shot board's roster lists only members who actually answered, with no fourth badge and no marker distinguishing an automatic default from a deliberate answer; the delete confirmation reports every signup row, not only answered ones."

patterns-established:
  - "Pattern: co-locate a JS handler with its sole conditionally-rendered caller inside the same Razor visibility check, rather than defining it unconditionally in a shared scripts section, whenever a test or a user needs to observe that control's visibility from the rendered output alone."

requirements-completed: [EVTAVAIL-01, EVTAVAIL-02, EVTAVAIL-03]

coverage:
  - id: D1
    description: "Three answer buttons (Yes/Maybe/No) render for any signed-in board member on either board type, each posting through setAvailability() with the antiforgery token"
    requirement: "EVTAVAIL-01"
    verification:
      - kind: integration
        ref: "EventDetailsAvailabilityRenderTests#Details_SignedInMember_SeesAllThreeAnswerButtons"
        status: pass
    human_judgment: false
  - id: D2
    description: "The withdraw control renders only on a one-shot board where the viewer already holds a signup row, and stays hidden on a campaign board even when the row exists"
    requirement: "EVTAVAIL-02"
    verification:
      - kind: integration
        ref: "EventDetailsAvailabilityRenderTests#Details_OneShotBoard_ViewerHoldsRow_ShowsWithdraw"
        status: pass
      - kind: integration
        ref: "EventDetailsAvailabilityRenderTests#Details_OneShotBoard_ViewerHoldsNoRow_HidesWithdraw"
        status: pass
      - kind: integration
        ref: "EventDetailsAvailabilityRenderTests#Details_CampaignBoard_ViewerHoldsRow_HidesWithdraw"
        status: pass
    human_judgment: false
  - id: D3
    description: "A named roster of plain Yes/Maybe/No badges is visible to every board member (not gated on CanManage), and never leaks the answered-vs-automatic-default marker or the raw timestamp column name"
    requirement: "EVTAVAIL-03"
    verification:
      - kind: integration
        ref: "EventDetailsAvailabilityRenderTests#Details_ThreeMembersAnswered_RosterShowsNamesAndBadges"
        status: pass
      - kind: integration
        ref: "EventDetailsAvailabilityRenderTests#Details_Body_NeverLeaksAnsweredMarkerOrRawTimestampField"
        status: pass
    human_judgment: false
  - id: D4
    description: "The event delete confirmation names the total signup count that will be lost"
    verification:
      - kind: integration
        ref: "EventDetailsAvailabilityRenderTests#Details_DungeonMaster_SeesDeleteConfirmationWithSignupCount"
        status: pass
    human_judgment: false
  - id: D5
    description: "No calendar surface (_Calendar.cshtml, Calendar/Index.cshtml, Calendar/Index.Mobile.cshtml) or Quest view was touched by this plan"
    verification:
      - kind: other
        ref: "git diff --name-only 04973b64..HEAD -- QuestBoard.Service/Views/Shared/_Calendar.cshtml QuestBoard.Service/Views/Calendar/ QuestBoard.Service/Views/Quest/ -> empty; git diff --name-only 04973b64..HEAD -- '*.cshtml' -> exactly Events/Details.cshtml"
        status: pass
    human_judgment: false
  - id: D6
    description: "Real-mobile-device rendering of the buttons and roster (this page has no mobile variant) reads correctly"
    verification: []
    human_judgment: true
    rationale: "75-VALIDATION.md marks this a manual-only verification -- devtools emulation has previously masked a live case of mobile markup never being selected, and native confirm() dialog text is not assertable through the integration harness. Not automatable in this plan."

# Metrics
duration: ~30min
completed: 2026-08-28
status: complete
---

# Phase 75 Plan 04: Event Details Availability Surface Summary

**Events/Details.cshtml gains three Yes/Maybe/No answer buttons, a board-type-gated withdraw control, a named roster, and a signup-count delete confirmation, all wired to the write actions from plan 75-03**

## Performance

- **Duration:** ~30 min
- **Started:** 2026-08-28T07:15:00Z (approx.)
- **Completed:** 2026-08-28T07:47:00Z
- **Tasks:** 3
- **Files modified:** 2 (1 created, 1 modified)

## Accomplishments

- An "Your Availability" card on `Events/Details.cshtml` shows the viewer's current answer (or invites them to answer if they hold no row), followed by three `btn-success`/`btn-warning`/`btn-danger` buttons (Yes/Maybe/No) that post through `setAvailability(eventId, availability)` -- one click both creates and answers, matching the established `changeVote` fetch idiom from `Quest/Details.cshtml`
- A withdraw control (`btn-outline-danger`, native-`confirm()`-gated) renders only when `Model.IsOneShotBoard && Model.HasOwnSignup`; its handler function lives inline beside it rather than in the page's shared scripts section, so a campaign board or a not-yet-answered viewer never has the function defined with nothing to call it
- A "Who's Coming" roster card renders `Model.Roster` as a `table-hover` of member name + plain Yes/Maybe/No badge (`bg-success`/`bg-warning text-dark`/`bg-danger`, matching the quest participant vote badge styling exactly), visible to every signed-in board member and not gated on `Model.CanManage`; an empty roster shows an inviting muted line instead of an empty table
- The event delete confirmation now interpolates `Model.SignupCount` into the native `confirm()` message via a precomputed local string, reporting every signup row on the event (including automatic campaign rows), not only answered ones
- `EventDetailsAvailabilityRenderTests.cs` (7 facts) proves: all three answer buttons render; withdraw shows on a one-shot board when the viewer holds a row; withdraw hides when the viewer holds no row; withdraw hides on a campaign board even when the row exists; a three-member roster shows all three names and all three badge styles; the body never contains the `HasAnswered` flag name or the raw `UpdatedAt` timestamp field name; and a Dungeon Master sees the delete confirmation carrying the live signup count
- No calendar view (`_Calendar.cshtml`, `Calendar/Index.cshtml`, `Calendar/Index.Mobile.cshtml`) or `Quest/*` view was touched -- the only `.cshtml` file this plan changed is `Events/Details.cshtml`

## Task Commits

Each task was committed atomically:

1. **Task 1: Availability card -- three answer buttons, withdraw control, and the two fetch scripts** - `a06ce61` (feat)
2. **Task 2: Roster rendering and the signup-aware delete confirmation** - `57a6d20` (feat)
3. **Task 3: EventDetailsAvailabilityRenderTests -- the controls appear and disappear under the right conditions** - `075a60c` (test, includes the withdraw-script relocation fix below)

**Plan metadata:** pending (docs: complete plan -- committed by this same agent immediately after this file)

## Files Created/Modified

- `QuestBoard.Service/Views/Events/Details.cshtml` - Added the availability card (buttons + withdraw), the roster card, the signup-count delete confirmation, and the `setAvailability`/`withdrawAvailability` fetch scripts
- `QuestBoard.IntegrationTests/Controllers/EventDetailsAvailabilityRenderTests.cs` - New render-behavior test class, 7 facts

## Decisions Made

- Followed the plan's locked decisions exactly: buttons render for any signed-in member on either board type; withdraw is one-shot-only and row-gated; the roster is visible to every board member with no fourth badge and no automatic-default marker; the delete confirmation counts every row.
- Moved `withdrawAvailability()`'s definition from the shared `@section Scripts` block to inline beside its sole caller inside the same `@if (Model.IsOneShotBoard && Model.HasOwnSignup)` block. See deviation below.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Withdraw script relocated so its conditional rendering is actually testable and correct**
- **Found during:** Task 3 (writing `EventDetailsAvailabilityRenderTests`)
- **Issue:** Task 1 placed both `setAvailability` and `withdrawAvailability` unconditionally in the shared `@section Scripts` block, per the plan's instruction to model them directly on the Quest details idiom. That means the string `withdrawAvailability(` (the function *definition*) always appeared in the rendered body regardless of whether the withdraw button itself rendered -- making `Should().NotContain("withdrawAvailability(")` structurally impossible to satisfy on a campaign board or a not-yet-answered viewer, even though the button correctly did not render. This was a genuine bug: a JS function with no valid caller present on the page is dead code that also happens to defeat a black-box test of the control's own visibility.
- **Fix:** Moved the `withdrawAvailability(eventId)` function definition out of `@section Scripts` and placed it inline in a `<script>` tag directly inside the same `@if (Model.IsOneShotBoard && Model.HasOwnSignup)` block as the withdraw button, right after the button markup. `setAvailability` remains in the shared `@section Scripts` block since it is called unconditionally by all three always-rendered answer buttons.
- **Files modified:** `QuestBoard.Service/Views/Events/Details.cshtml`
- **Verification:** All Task 1 acceptance criteria re-verified unchanged (`function setAvailability(` and `function withdrawAvailability(` both present exactly once in the source; `__RequestVerificationToken` count still 2; `method: "DELETE"` still present). `dotnet build` exits 0. `EventDetailsAvailabilityRenderTests` (7 facts) and the full suite (333 unit + 479 integration) pass.
- **Committed in:** `075a60c` (Task 3 commit, alongside the new test file)

---

**Total deviations:** 1 auto-fixed (bug fix, no behavior change to the withdraw control's own visibility rule -- only to where its handler is defined)
**Impact on plan:** No functional or scope impact on the delivered feature. The fix was necessary for the plan's own Task 3 acceptance criteria (`Should().Contain("withdrawAvailability(")` and `Should().NotContain("withdrawAvailability(")` both literally present and both passing) to be simultaneously satisfiable.

## Issues Encountered

None beyond the script-relocation fix noted above.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

- `Events/Details.cshtml` is now the complete, single availability surface this phase's CONTEXT.md called for: answer buttons, withdraw, roster, and delete confirmation all in one place, wired against the exact `EventViewModel`/`EventSignupViewModel` contract plan 75-03 shipped.
- No migration was added or needed -- this plan is view/markup and test only.
- `dotnet build` exits 0; full `dotnet test` is green (333 unit + 479 integration tests passing, 7 new integration tests added, no regressions).
- Plan 75-05's cross-board tenant-isolation tests (`EventAvailabilityTenantIsolationTests.cs`) and the shared `75-VALIDATION.md` sign-off were explicitly out of this plan's file scope and are a sibling plan's responsibility; this plan touched neither file.
- The manual-only mobile-rendering verification from `75-VALIDATION.md` remains outstanding and unautomatable by design -- flagged as human-judgment coverage (D6) above.

---
*Phase: 75-event-availability-signups*
*Completed: 2026-08-28*

## Self-Check: PASSED

- FOUND: `QuestBoard.Service/Views/Events/Details.cshtml`
- FOUND: `QuestBoard.IntegrationTests/Controllers/EventDetailsAvailabilityRenderTests.cs`
- FOUND commit `a06ce61` (Task 1)
- FOUND commit `57a6d20` (Task 2)
- FOUND commit `075a60c` (Task 3)
