---
phase: 76-recurring-event-series
plan: 14
subsystem: ui
tags: [aspnetcore-mvc, razor, board-type-scoping, navigation, calendar]

# Dependency graph
requires:
  - phase: 76-recurring-event-series
    provides: the horizon banner and cancelled-occurrence chip on the calendar (76-10 and earlier plans in this phase)
provides:
  - Campaign boards can reach the calendar from the main navigation on both desktop and mobile layouts
  - CalendarController excludes quests from the load entirely when the active board is a campaign board
  - Automated coverage locking the superseded navigation clause's replacement and the new campaign scope
affects: [76-15, future-nav-decisions]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Board-type-aware read scoping done at the load, not the render: a controller resolves the board type once and skips a data fetch entirely for the excluded case, rather than fetching and filtering downstream"
    - "Superseding one clause of a locked navigation decision: replace the guarding test with a fact asserting the new rule (never delete), leave the sibling facts byte-for-byte unchanged, and prove the count of untouched conditions in the same file as an acceptance criterion"

key-files:
  created:
    - QuestBoard.IntegrationTests/Controllers/CalendarBoardTypeScopeTests.cs
  modified:
    - QuestBoard.IntegrationTests/Controllers/LayoutNavigationTests.cs
    - QuestBoard.Service/Controllers/QuestBoard/CalendarController.cs
    - QuestBoard.Service/Views/Shared/_Layout.cshtml
    - QuestBoard.Service/Views/Shared/_Layout.Mobile.cshtml

key-decisions:
  - "Superseded the calendar clause of the Phase 37 NAV-01 decision (commit f7a31fa9): a campaign board now shows the Calendar nav entry, because the calendar carries this phase's two campaign-relevant read surfaces. The other four campaign restrictions (shop, manage shop, edit my profile, players) and the anonymous-visitor rule are unchanged and re-verified green."
  - "Quest exclusion happens by never calling GetQuestsForCalendarAsync on a campaign board, not by filtering a fetched list, so a quest can never leak through the shared _Calendar.cshtml partial or a future caller."
  - "An unresolved board type is deliberately excluded from the events-only exclusion, matching one-shot's both-kinds behaviour, per IBoardTypeResolver's contract that null is its own state."

requirements-completed: [EVTRECUR-03]

coverage:
  - id: D1
    description: "A campaign board member reaches the calendar from the main navigation on desktop and mobile, without knowing a URL"
    requirement: "EVTRECUR-03"
    verification:
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Controllers/LayoutNavigationTests.cs#Nav_CampaignDm_CalendarLinkPresent"
        status: pass
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Controllers/LayoutNavigationTests.cs#Nav_CampaignPlayer_CalendarLinkPresent"
        status: pass
    human_judgment: false
  - id: D2
    description: "A campaign board's calendar shows events and never a campaign quest, whether reached through navigation or by address"
    requirement: "EVTRECUR-03"
    verification:
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Controllers/CalendarBoardTypeScopeTests.cs#Calendar_CampaignBoard_DesktopAgent_RendersEventsWithoutQuests"
        status: pass
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Controllers/CalendarBoardTypeScopeTests.cs#Calendar_CampaignBoard_MobileAgent_RendersEventsWithoutQuests"
        status: pass
    human_judgment: false
  - id: D3
    description: "A one-shot board's calendar and navigation are unchanged, and a logged-out visitor still sees no calendar entry on either board type"
    requirement: "EVTRECUR-03"
    verification:
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Controllers/CalendarBoardTypeScopeTests.cs#Calendar_OneShotBoard_DesktopAgent_RendersQuestsAndEvents"
        status: pass
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Controllers/CalendarBoardTypeScopeTests.cs#Calendar_OneShotBoard_MobileAgent_RendersQuestsAndEvents"
        status: pass
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Controllers/LayoutNavigationTests.cs#Nav_CampaignAnonymous_CalendarLinkAbsent"
        status: pass
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Controllers/LayoutNavigationTests.cs#Nav_Anonymous_CalendarLinkAbsent"
        status: pass
    human_judgment: false
  - id: D4
    description: "The four other campaign navigation restrictions (shop, manage shop, edit my profile, players) stay green, and cross-board isolation on the calendar surfaces is not weakened"
    verification:
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Controllers/LayoutNavigationTests.cs (4 sibling facts)"
        status: pass
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Tests/EventSeriesTenantIsolationTests.cs (12 facts)"
        status: pass
    human_judgment: false

duration: ~30min
completed: 2026-08-28
status: complete
---

# Phase 76 Plan 14: Campaign calendar navigation and quest exclusion Summary

**Campaign boards regain the Calendar nav entry on both layouts and the calendar becomes an events-only surface on campaign boards — closing a live quest-leak in `CalendarController`, which previously carried no board-type gate at all.**

## Performance

- **Duration:** ~30 min
- **Completed:** 2026-08-28
- **Tasks:** 3
- **Files modified:** 4 (1 created, 4 modified — `LayoutNavigationTests.cs` modified, `CalendarBoardTypeScopeTests.cs` created, `CalendarController.cs`, `_Layout.cshtml`, `_Layout.Mobile.cshtml` modified)

## Accomplishments
- Replaced the Phase 37 `Nav_CampaignDm_CalendarLinkAbsent` fact with `Nav_CampaignDm_CalendarLinkPresent`, and added `Nav_CampaignPlayer_CalendarLinkPresent` and `Nav_CampaignAnonymous_CalendarLinkAbsent`, superseding only the calendar clause of NAV-01 while leaving the four sibling campaign restrictions and the anonymous rule untouched and green.
- Added `CalendarBoardTypeScopeTests.cs` with five facts proving the campaign calendar renders events without quests (desktop and mobile), the one-shot calendar is unchanged, and an unresolved board type behaves like one-shot rather than campaign.
- `CalendarController.Index` now resolves the active board type once and skips the quest load entirely on a campaign board, leaving `CalendarViewModel.Quests` at its safe empty default — fixing a live leak where the campaign calendar, already reachable by direct URL, rendered campaign quests.
- Widened the calendar nav entry's board-type condition in both `_Layout.cshtml` and `_Layout.Mobile.cshtml` from `activeBoardType == BoardType.OneShot` to `activeBoardType is BoardType.OneShot or BoardType.Campaign`, leaving the authentication half of each condition and all four other board-type-gated entries untouched.

## Task Commits

Each task was committed atomically, RED before GREEN:

1. **Task 1: Replace the superseded navigation fact and add the board-type calendar scope tests** - `219a9f00` (test)
2. **Task 2: Make the calendar an events-only surface on campaign boards** - `5e69507c` (feat)
3. **Task 3: Show the calendar navigation entry on campaign boards in both layouts** - `e6f2b3c0` (feat)

_TDD RED/GREEN transition:_ Task 1's commit left `Nav_CampaignDm_CalendarLinkPresent`, `Nav_CampaignPlayer_CalendarLinkPresent`, `Calendar_CampaignBoard_DesktopAgent_RendersEventsWithoutQuests`, and `Calendar_CampaignBoard_MobileAgent_RendersEventsWithoutQuests` failing against pre-existing code (29 total in the combined filter: 23 passing, 6 failing across both InlineData variants of the two navigation facts plus the two calendar facts). Task 2's commit turned the two calendar facts green. Task 3's commit turned the two navigation facts green, closing the loop with all 28 facts in the combined `CalendarBoardTypeScopeTests|LayoutNavigationTests` filter passing.

## Files Created/Modified
- `QuestBoard.IntegrationTests/Controllers/CalendarBoardTypeScopeTests.cs` - New: five facts locking board-type-aware quest exclusion on the calendar
- `QuestBoard.IntegrationTests/Controllers/LayoutNavigationTests.cs` - Replaced `Nav_CampaignDm_CalendarLinkAbsent` with `Nav_CampaignDm_CalendarLinkPresent`; added `Nav_CampaignPlayer_CalendarLinkPresent` and `Nav_CampaignAnonymous_CalendarLinkAbsent`
- `QuestBoard.Service/Controllers/QuestBoard/CalendarController.cs` - Added `IBoardTypeResolver` dependency; `Index` skips the quest load on a campaign board
- `QuestBoard.Service/Views/Shared/_Layout.cshtml` - Widened the calendar nav entry's board-type condition to an explicit two-member pattern match
- `QuestBoard.Service/Views/Shared/_Layout.Mobile.cshtml` - Same widening, mirrored for the mobile offcanvas nav

## Decisions Made

**Decision supersession — NAV-01 (Phase 37, milestone v6.0), calendar clause only.** Phase 37 (commit `f7a31fa9`) decided the calendar entry is hidden on a campaign board, alongside the shop, manage-shop, edit-my-profile and players entries, and its own threat register (`T-37-04`) recorded this as cosmetic only — the underlying controller was never board-type gated, and gating it was explicitly out of scope at the time. Phase 76 gives the calendar two campaign-relevant read surfaces (the DM horizon banner and the cancelled-occurrence chip) that did not exist when that decision was made, changing its premise. This plan supersedes **only** the calendar clause: `Nav_CampaignDm_CalendarLinkAbsent` is replaced (not deleted) by `Nav_CampaignDm_CalendarLinkPresent`, which asserts the new rule. NAV-02 (shop), NAV-04 (manage shop), NAV-05 (edit my profile), NAV-06 (players), and the anonymous-visitor rule are all unchanged and re-verified green as part of this plan's own acceptance criteria. What Phase 37 left cosmetic, this plan makes real for the one route affected: the controller gains the board-type-aware quest load Phase 37 declined to add. The supersession record itself (ROADMAP.md, REQUIREMENTS.md) is written by plan 76-15, not this plan; the archived Phase 37 artifacts under `.planning/milestones/v6.0-phases/` were not touched.

- Quest exclusion lands in `CalendarController.Index`, never in the shared `Views/Shared/_Calendar.cshtml` partial — that partial has six call sites, five of which build their own view model to render a per-quest date picker and must keep doing so unmodified. The controller-level exclusion (never fetching quests, rather than fetching and hiding them) is structurally incapable of touching those five sites.
- The nav condition and the controller's exclusion both use explicit pattern matching (`is BoardType.OneShot or BoardType.Campaign`, `!= BoardType.Campaign`) rather than a not-null test, per `IBoardTypeResolver`'s own contract that an unresolved board type is a distinct state, not a default to collapse onto either board type.

## Deviations from Plan

None — plan executed exactly as written. All acceptance-criteria grep checks and test filters specified in the plan passed as specified; no auto-fixes, no architectural questions, no blockers.

## Issues Encountered

None.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

- EVTRECUR-03's second gap (campaign boards unable to reach the calendar) is closed; combined with 76-10's prior work, campaign DMs can now discover and act on the rolling-window horizon banner and cancelled-occurrence chip through normal navigation.
- `dotnet test QuestBoard.UnitTests` passes 385/385; `dotnet test QuestBoard.IntegrationTests` passes 522/522 in this worktree (the plan's target of "513 plus facts added by this plan and by 76-13" will resolve once both worktrees merge).
- Plan 76-15 still needs to write the NAV-01 supersession record into `.planning/ROADMAP.md` and `.planning/REQUIREMENTS.md` — this plan intentionally left those files untouched per its own scope boundary.

---
*Phase: 76-recurring-event-series*
*Completed: 2026-08-28*
