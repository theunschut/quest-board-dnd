---
phase: 52-add-dead-status-to-characterstatus-enum
plan: 01
subsystem: ui
tags: [razor, css, enum, guild-members, bootstrap, font-awesome]

# Dependency graph
requires: []
provides:
  - CharacterStatus.Dead = 2 enum member
  - Dead card/row dimming CSS (desktop .character-dead, mobile .guild-member-row.dead)
  - Dead badge CSS (.dead-badge, desktop floating badge)
  - 3-way status badge (Dead/Retired/Active) on Details.cshtml and Details.Mobile.cshtml
  - Retire/Reactivate toggle button hidden when Status == Dead (Details views, both platforms)
  - Independent Dead badge + card/row class on Index.cshtml and Index.Mobile.cshtml
affects: []

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Extend a two-value status enum to three values by adding fresh, parallel CSS rule blocks rather than composing/extending the existing rule"
    - "Badge sites with a true if/else (Active gets an explicit badge) become if/else-if/else with the new value checked first"
    - "Badge sites with a single-condition if/no-else (Active renders no badge) get an independent second sibling if, never converted to if/else-if, to avoid introducing a new Active badge"

key-files:
  created: []
  modified:
    - QuestBoard.Domain/Enums/CharacterStatus.cs
    - QuestBoard.Service/wwwroot/css/guild-members.css
    - QuestBoard.Service/wwwroot/css/guild-members.mobile.css
    - QuestBoard.Service/Views/GuildMembers/Details.cshtml
    - QuestBoard.Service/Views/GuildMembers/Details.Mobile.cshtml
    - QuestBoard.Service/Views/GuildMembers/Index.cshtml
    - QuestBoard.Service/Views/GuildMembers/Index.Mobile.cshtml

key-decisions:
  - "Wrote fresh .character-dead/.dead-badge CSS rules rather than composing from .character-retired, per RESEARCH.md Open Question 1"
  - "Used the plan's pre-approved CSS values verbatim (opacity 0.5, rgba(33,37,41,*), grayscale(60%)) rather than re-deriving new ones"
  - "No [Display] attribute added to the enum — Dead renders as the raw member name via Html.GetEnumSelectList, matching Active/Retired convention"

patterns-established:
  - "Pattern 1: Three-way status enum badge extension — check the new value first in an if/else-if chain when the existing badge site has an else branch, or add an independent sibling if when it doesn't"

requirements-completed: []  # Unmapped backlog item — ROADMAP.md Phase 52 lists Requirements: TBD, no requirement IDs exist

# Metrics
duration: 15min
completed: 2026-07-06
---

# Phase 52 Plan 01: Add Dead status to CharacterStatus enum Summary

**Added CharacterStatus.Dead = 2 with dark/grayscale card and badge styling wired through Create/Edit dropdowns (auto-populated), Details/Index status badges (desktop + mobile), and a Details-page guard that hides the Retire/Reactivate toggle for Dead characters while keeping Delete available.**

## Performance

- **Duration:** ~15 min
- **Started:** 2026-07-06T09:29:00+02:00
- **Completed:** 2026-07-06T09:31:13+02:00
- **Tasks:** 3 completed
- **Files modified:** 7

## Accomplishments
- `CharacterStatus.Dead = 2` added as a third enum member; Create/Edit Status dropdowns auto-populate it via `Html.GetEnumSelectList<CharacterStatus>()` with zero markup changes
- Details.cshtml and Details.Mobile.cshtml render a dark `bg-dark`/`fa-skull` "Dead" badge (3-way if/else-if/else, Dead checked first) and hide the Retire/Reactivate toggle form entirely when `Status == Dead`, while the Delete form remains available
- Index.cshtml and Index.Mobile.cshtml render an independent Dead badge (`.dead-badge` desktop, `bg-dark` badge mobile) as a sibling `if` alongside the existing Retired `if` — Active characters still render no badge at all, preserving existing behavior
- Both Index views apply a Dead-specific card/row class (`character-dead` desktop, ` dead` mobile modifier) via a nested ternary checking Dead before Retired
- New CSS in `guild-members.css` (`.character-dead`, `.dead-badge`) and `guild-members.mobile.css` (`.guild-member-row.dead`) is visually distinct from Retired: lower opacity (0.5 vs 0.7), charcoal border/background (`rgba(33,37,41,*)` vs Retired's mid-gray `rgba(108,117,125,*)`), plus a `grayscale(60%)` filter Retired doesn't have

## Task Commits

Each task was committed atomically:

1. **Task 1: Add Dead enum value and Dead CSS styling** - `c4f8262` (feat)
2. **Task 2: Add Dead badge and hide toggle button on Details views** - `6736b54` (feat)
3. **Task 3: Add independent Dead badge and Dead card/row class on Index views** - `a6c9ea6` (feat)

## Files Created/Modified
- `QuestBoard.Domain/Enums/CharacterStatus.cs` - Added `Dead = 2` member after `Retired = 1`
- `QuestBoard.Service/wwwroot/css/guild-members.css` - Added `.character-dead`/`.character-dead:hover` card rules and `.dead-badge` floating badge rule
- `QuestBoard.Service/wwwroot/css/guild-members.mobile.css` - Added `.guild-member-row.dead` modifier rule
- `QuestBoard.Service/Views/GuildMembers/Details.cshtml` - 3-way status badge chain; toggle-button form wrapped in `@if (Model.Status != CharacterStatus.Dead)`
- `QuestBoard.Service/Views/GuildMembers/Details.Mobile.cshtml` - Same 3-way badge/toggle-guard pattern, mobile markup
- `QuestBoard.Service/Views/GuildMembers/Index.cshtml` - Independent Dead badge `if` + nested card-class ternary, both MyCharacters and OtherCharacters sections
- `QuestBoard.Service/Views/GuildMembers/Index.Mobile.cshtml` - Independent Dead badge `if` + nested row-class ternary, both sections

## Decisions Made
- Followed the plan's explicit instruction to write fresh CSS rule blocks rather than compose/extend `.character-retired`, keeping Retired and Dead rules independently readable
- Used the plan's pre-approved CSS values verbatim (no re-derivation): `.character-dead` at opacity 0.5 / `rgba(33,37,41,0.6)` border / `grayscale(60%)`; `.dead-badge` at `rgba(33,37,41,0.95)` background matching `.retired-badge`'s positioning/padding/font exactly
- Left `isRetired` local variable and the Active↔Retired toggle-button flip logic completely untouched in both Details views, per the plan's explicit constraint

## Deviations from Plan

None - plan executed exactly as written. All read_first line numbers matched the actual source files exactly (no drift since PATTERNS.md was generated), so no adaptation was needed.

## Issues Encountered

None.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

- All 3 tasks complete; solution builds clean (`dotnet build`: 6 projects, 0 errors, 0 warnings)
- Full test suite green: 169 unit tests + 303 integration tests, all passing (472/472)
- `CharacterStatus_CastRoundTrips` dynamic enum test confirmed to cover the new `Dead` value automatically, no test changes needed
- Confirmed zero-change touchpoints were verified untouched: no EF Core migration created, `GuildMembersController.cs`, `CharacterRepository.cs`, `QuestController.cs`, and `EntityProfileEnumCastTests.cs` all unmodified (git diff --stat shows only the 7 planned files changed)
- Manual/smoke verification (setting a character to Dead via Edit form, viewing Details/Index badges in a live browser) was not performed — this codebase has no automated Razor-view-rendering harness per RESEARCH.md, and per plan scope this is a small phase where manual smoke testing is listed as a follow-up verification step for the orchestrator/user rather than the executor
- No blockers for the orchestrator's post-wave merge

---
*Phase: 52-add-dead-status-to-characterstatus-enum*
*Completed: 2026-07-06*
