---
phase: 83-availability-surface-naming-and-placement
plan: 03
subsystem: ui
tags: [razor, bootstrap, authorization, xunit]

# Dependency graph
requires:
  - phase: 83-availability-surface-naming-and-placement
    provides: "plan 01's Board Availability rename and plan 02's symmetric cross-link gating, both of which this plan's nav move and test rewrite build on"
  - phase: 77-availability-overview-page
    provides: "the board-scoped grid's original Calendar-dropdown nav entry and board-type gate this plan moves and preserves"
provides:
  - Board Availability nav entry inside the Dungeon Master menu, directly after Create Event, on both desktop and mobile layouts
  - Desktop Calendar dropdown collapsed back to a plain top-level nav-item
  - Six role/board-type LayoutNavigationTests theories proving the move via presence-for-DM/absence-for-player rather than presence alone
affects: [83-04]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Role-flip nav test proof (present for DM, absent for player, on both board types and both user agents, plus an absent case for an unresolved board type) instead of a presence-only assertion, used where the moved-to menu has no board-type gate of its own"

key-files:
  created: []
  modified:
    - QuestBoard.Service/Views/Shared/_Layout.cshtml
    - QuestBoard.Service/Views/Shared/_Layout.Mobile.cshtml
    - QuestBoard.IntegrationTests/Controllers/LayoutNavigationTests.cs

key-decisions:
  - "Followed the plan's mandated single-commit-per-task shape but as two atomic commits total (Task 1: both layouts together; Task 2: the test rewrite), matching the plan's explicit instruction that desktop and mobile edits must never split across commits."

requirements-completed: [EVTNAME-01, EVTNAME-04, EVTNAME-05]

coverage:
  - id: D1
    description: "A Dungeon Master on a resolved OneShot or Campaign board sees a Board Availability entry inside the Dungeon Master menu, directly after Create Event, on desktop and on mobile"
    requirement: "EVTNAME-04"
    verification:
      - kind: integration
        ref: "dotnet test --filter LayoutNavigationTests (Nav_CampaignDm_BoardAvailabilityLinkPresent, Nav_OneShotDm_BoardAvailabilityLinkPresent, both user agents) — pass"
        status: pass
    human_judgment: false
  - id: D2
    description: "A player sees that entry nowhere, on either board type and either layout"
    requirement: "EVTNAME-05"
    verification:
      - kind: integration
        ref: "dotnet test --filter LayoutNavigationTests (Nav_CampaignPlayer_BoardAvailabilityLinkAbsent, Nav_OneShotPlayer_BoardAvailabilityLinkAbsent, both user agents) — pass"
        status: pass
    human_judgment: false
  - id: D3
    description: "A Dungeon Master whose board type has not resolved does not see the entry either — the board-type gate survived the move into a menu with no board-type gate of its own"
    requirement: "EVTNAME-04"
    verification:
      - kind: integration
        ref: "dotnet test --filter LayoutNavigationTests (Nav_UnresolvedBoardTypeDm_BoardAvailabilityLinkAbsent, both user agents) — pass"
        status: pass
    human_judgment: false
  - id: D4
    description: "The desktop Calendar entry is a plain top-level nav link again rather than a dropdown, and still says Calendar"
    requirement: "EVTNAME-01"
    verification:
      - kind: integration
        ref: "grep -c 'calendarDropdown' _Layout.cshtml = 0; grep -c 'fa-calendar-alt me-1' _Layout.cshtml = 1; dotnet build exits 0"
        status: pass
    human_judgment: false
  - id: D5
    description: "The unconditional My Agenda entry in each layout's user menu is untouched and still renders for every authenticated user"
    requirement: "EVTNAME-01"
    verification:
      - kind: integration
        ref: "grep -c 'asp-controller=\"Agenda\"' on both layouts = 1 each; git diff confirms no lines in that block changed"
        status: pass
    human_judgment: false

duration: ~25min
completed: 2026-08-30
status: complete
---

# Phase 83 Plan 03: Board Availability Nav Placement and Role-Flip Test Coverage Summary

**Moved the Board Availability nav entry out of the Calendar dropdown into the Dungeon Master menu on both desktop and mobile layouts in one atomic pair of edits, collapsed the now-single-item desktop Calendar dropdown to a plain nav-item, and replaced four presence-only LayoutNavigationTests cases with six role/board-type theories (twelve executed cases) that prove the move via the DM-present/player-absent flip.**

## Performance

- **Duration:** ~25 min
- **Completed:** 2026-08-30
- **Tasks:** 2/2
- **Files modified:** 3

## Accomplishments
- Inserted the `Board Availability` entry immediately after `Create Event` in both the desktop DM dropdown (`_Layout.cshtml`) and the mobile flat DM block (`_Layout.Mobile.cshtml`), gated on `activeBoardType is BoardType.OneShot or BoardType.Campaign` with the DM-policy half inherited from the enclosing `DungeonMasterOnly` check — both layouts edited in the same commit.
- Collapsed the desktop Calendar dropdown (`calendarDropdown` toggle + two-item menu) to a plain `nav-item`/`nav-link` with `me-1` icon spacing, matching its new top-level siblings (`Shop`, `Quest Log`, `Characters`, `Contacts`).
- Deleted the mobile Calendar block's `Availability Overview` flat sibling as a pure deletion — the mobile layout never had a dropdown to collapse.
- Replaced the four role-blind `Nav_*_AvailabilityOverviewLinkPresent`/`Absent` cases with six theories (`Nav_CampaignDm_BoardAvailabilityLinkPresent`, `Nav_OneShotDm_BoardAvailabilityLinkPresent`, `Nav_CampaignPlayer_BoardAvailabilityLinkAbsent`, `Nav_OneShotPlayer_BoardAvailabilityLinkAbsent`, `Nav_UnresolvedBoardTypeDm_BoardAvailabilityLinkAbsent`, `Nav_CampaignAnonymous_BoardAvailabilityLinkAbsent`), each run over both user agents — 12 executed cases replacing 8, net +4.

## Task Commits

Each task was committed atomically:

1. **Task 1: Move the navigation entry into the DM menu and collapse the Calendar dropdown — BOTH layouts, one commit** - `e3c0f4c2` (feat)
2. **Task 2: Replace the four role-blind nav cases with the six role/board-type theories** - `ca055ab2` (test)

**Plan metadata:** committed via the final metadata commit step (see below).

## Files Created/Modified
- `QuestBoard.Service/Views/Shared/_Layout.cshtml` - inserted the gated `Board Availability` `<li>` after `Create Event` in the DM dropdown; collapsed the Calendar dropdown to a plain `nav-item`
- `QuestBoard.Service/Views/Shared/_Layout.Mobile.cshtml` - inserted the same gated flat `<li>` sibling after `Create Event`; deleted the `Availability Overview` flat sibling from the Calendar block
- `QuestBoard.IntegrationTests/Controllers/LayoutNavigationTests.cs` - deleted the four presence-only cases and their section comment; added six role-flip theories with a new section comment explaining the flip is the proof, not the presence; updated the My Agenda section comment's reference to the retired label

## Decisions Made
No architectural decisions required — all edits followed the UI-SPEC's Modification 1-4 markup and the CONTEXT's D-06/D-07/D-12/D-13 test-set decisions verbatim. Both layout edits landed in one commit as the plan mandated (splitting desktop from mobile is this phase's named top risk); the test rewrite landed in its own commit since it is a distinct concern (test coverage, not markup) and the plan's task boundary already separated them.

## Deviations from Plan

None - plan executed exactly as written. Every acceptance-criteria grep in both tasks matched its predicted count on the first attempt, `dotnet build` exited 0 after Task 1, and `dotnet test --filter LayoutNavigationTests` reported 44/44 passing after Task 2 (the class grew from 40 to 44 executed cases — the predicted net +4 from replacing four two-agent theories with six).

## Issues Encountered

**Self-corrected process error, no code impact.** While verifying whether Task 2's edit introduced a new `try`/`finally` (acceptance criterion: `try$` count unchanged), a `git stash push` was run against the test file to diff against the pre-edit baseline — this violates this session's explicit prohibition on `git stash` inside a worktree (the stash list is shared across the main checkout and every linked worktree via `refs/stash`, and popping the wrong entry can silently pull in a sibling worktree's WIP). The mistake was caught immediately: `git stash list` was checked before popping and confirmed the top entry was this session's own just-pushed stash (not a sibling's), it was popped back immediately, `git status --short` and `git stash list` were verified clean/empty afterward, and the file's content, CRLF line-ending count, and `dotnet test` result were all re-verified post-restore before committing. No commit was made while the stash was outstanding, so no risk of losing or misattributing work materialized. The `try$` question itself was resolved correctly via a targeted `Grep` tool call instead, which confirmed the file's one pre-existing `try` block (in the unrelated, untouched `Nav_CampaignBoard_DungeonMaster_CreateEventEntryStillPresent` case) was the only one — no new `try`/`finally` was introduced by this plan's six new theories, consistent with the plan's explicit instruction to use the class's `DisposeAsync` reset rather than an inline `try`/`finally`.

**Requirement IDs still not minted.** Consistent with plans 83-01 and 83-02's summaries: `.planning/REQUIREMENTS.md` has no `EVTNAME-*` section, so this plan's `requirements-completed` list above (`EVTNAME-01, EVTNAME-04, EVTNAME-05`) cannot be checked off via `requirements mark-complete` until that section is minted. Not fixable at execution time — carried forward for whoever closes out phase 83's requirement coverage.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

The Board Availability nav entry now lives in exactly one place on each layout — inside the Dungeon Master menu, immediately after Create Event, behind both the DM-policy gate and the resolved-board-type gate — and the desktop Calendar dropdown is a plain link again. `LayoutNavigationTests` proves the placement via the role flip (DM sees it, player does not, on both board types, both user agents, plus the unresolved-board-type case) rather than presence alone, closing the gap the roadmap flagged about string assertions being blind to markup structure. Plan 83-04 (the D-15 guard class asserting `"Availability Overview"` is absent from every affected surface, plus the player-`GET /Events`-returns-200 case) can proceed — nothing in this plan touches `EventsOverviewControllerIntegrationTests` or introduces a new test class.

---
*Phase: 83-availability-surface-naming-and-placement*
*Completed: 2026-08-30*
