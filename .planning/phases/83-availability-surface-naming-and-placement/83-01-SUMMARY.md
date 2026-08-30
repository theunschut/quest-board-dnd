---
phase: 83-availability-surface-naming-and-placement
plan: 01
subsystem: ui
tags: [razor, bootstrap, authorization, css]

# Dependency graph
requires:
  - phase: 77-availability-overview-page
    provides: the EventsController.Index board-scoped grid this plan renames
  - phase: 82-personal-cross-board-event-agenda
    provides: the My Agenda surface this plan's DM-gated button links to
provides:
  - Board Availability page name (title, heading) on desktop and mobile /Events views
  - Shared .header-subtitle CSS rule reused by every page-header secondary line
  - DM-only gating of the My Agenda cross-link button on the Board Availability header
  - Extended EventsController.Index comment documenting the open-page/gated-links split
affects: [83-02, 83-03, 83-04]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Unscoped .header-subtitle CSS rule shared across card-header and non-card-header page headings"
    - "Hoisted isDm bool computed once per view render via the existing AuthorizationService DungeonMasterOnly idiom, mirroring Agenda's hasOtherBoardRow precedent"

key-files:
  created: []
  modified:
    - QuestBoard.Service/wwwroot/css/modern-card.css
    - QuestBoard.Service/Views/Events/Index.cshtml
    - QuestBoard.Service/Views/Events/Index.Mobile.cshtml
    - QuestBoard.Service/Controllers/Events/EventsController.cs

key-decisions:
  - "Wrote .header-subtitle unscoped rather than as UI-SPEC's drafted .modern-card-header .header-subtitle descendant selector, per the plan's own explicit deviation instruction — the mobile Events/Agenda headings render outside any card-header ancestor, so a scoped selector would silently leave both mobile subtitles unstyled."

requirements-completed: [EVTNAME-01, EVTNAME-02, EVTNAME-06, EVTNAME-07]

coverage:
  - id: D1
    description: "Board Availability page title and card heading read 'Board Availability' on both desktop and mobile, replacing 'Availability Overview'"
    requirement: "EVTNAME-01"
    verification:
      - kind: integration
        ref: "grep -c 'Board Availability' Views/Events/Index.cshtml + Index.Mobile.cshtml (both 1); grep -c 'Availability Overview' (both 0); dotnet test EventsOverviewControllerIntegrationTests, EventsOverviewMobileStyleTests"
        status: pass
    human_judgment: false
  - id: D2
    description: "Header subtitle names the active board and the events scope, with a defined fallback when no board name is in session"
    requirement: "EVTNAME-02"
    verification:
      - kind: integration
        ref: "grep -c 'header-subtitle mb-0' Views/Events/Index.cshtml + Index.Mobile.cshtml (both 1); dotnet build (Razor compiles the interpolation)"
        status: pass
    human_judgment: true
    rationale: "Visual rendering of the subtitle text and its opacity/contrast against the glass-surface header was not screenshot-verified in this session — grep/build confirm the markup and CSS rule exist correctly, but actual on-page legibility is a visual judgment call."
  - id: D3
    description: "My Agenda header button is present for a DM and absent (removed from DOM, not merely hidden) for a Player, on both layouts"
    requirement: "EVTNAME-07"
    verification:
      - kind: integration
        ref: "grep -c 'AuthorizeAsync(User, \"DungeonMasterOnly\")' Views/Events/Index.cshtml + Index.Mobile.cshtml (both 1); dotnet test RowNavigationAccessibilityTests"
        status: pass
    human_judgment: true
    rationale: "The role-flip proof (DM sees it / Player does not) for this specific button is exercised by plan 83-04's dedicated nav test cases, not by this plan's task-level test filter — this plan only proves the markup compiles and the existing page tests stay green."
  - id: D4
    description: "EventsController.Index keeps its open [Authorize]-only attribute set; the comment above it now explains the open-page/gated-links split"
    requirement: "EVTNAME-06"
    verification:
      - kind: integration
        ref: "git diff --stat confined to comment lines; grep -c 'Authorize(Policy' on Index's own attribute (none added); grep -c '^[Authorize]$' = 1; dotnet test EventsOverviewControllerIntegrationTests"
        status: pass
    human_judgment: false

duration: ~15min
completed: 2026-08-30
status: complete
---

# Phase 83 Plan 01: Board Availability Header Rename, Subtitle and DM Gate Summary

**Renamed the board-scoped /Events page from "Availability Overview" to "Board Availability" on both layouts, added a shared `.header-subtitle` CSS rule and board-naming subtitle, DM-gated the My Agenda cross-link button, and extended the controller comment to record why the page itself stays open.**

## Performance

- **Duration:** ~15 min
- **Completed:** 2026-08-30
- **Tasks:** 3/3
- **Files modified:** 4

## Accomplishments
- Added one shared, unscoped `.header-subtitle` CSS rule to `modern-card.css` so both card-header and non-card-header page headings can reuse it without a page-specific duplicate.
- Renamed `Availability Overview` to `Board Availability` on both `Events/Index.cshtml` and `Index.Mobile.cshtml`, and added a subtitle reading `Events on {board}` (or `Events on this board` with no active group name in session).
- Gated the existing `My Agenda` header button behind the `DungeonMasterOnly` authorization idiom on both layouts, removing it from the DOM entirely for a Player rather than merely hiding it.
- Extended the existing comment above `EventsController.Index` to explain, in plain language and without any phase/decision reference, that the page deliberately stays open to every board member while its discoverable links are now Dungeon-Master-only.

## Task Commits

Each task was committed atomically:

1. **Task 1: Add the shared header-subtitle rule to modern-card.css** - `9c88eb9b` (feat)
2. **Task 2: Rename, subtitle and DM-gate the Board Availability header on BOTH layouts** - `b18d3522` (feat)
3. **Task 3: Extend the EventsController.Index comment to record the open-page / gated-links split** - `1ce49a22` (docs)

**Plan metadata:** committed via the final metadata commit step (see below).

## Files Created/Modified
- `QuestBoard.Service/wwwroot/css/modern-card.css` - added the unscoped `.header-subtitle` rule (display/margin-top/font-size/font-weight/opacity, no color declaration)
- `QuestBoard.Service/Views/Events/Index.cshtml` - `Board Availability` title/heading, `header-subtitle` paragraph, `isDm`-gated My Agenda button, `align-items-start` on the header row
- `QuestBoard.Service/Views/Events/Index.Mobile.cshtml` - same four changes in the mobile shape (`h4`, `me-1` icon spacing preserved)
- `QuestBoard.Service/Controllers/Events/EventsController.cs` - extended the existing comment above `Index`; no attribute or behavior change

## Decisions Made
- Followed the plan's explicit, pre-decided deviation from the UI-SPEC's drafted `.modern-card-header .header-subtitle` selector: wrote the rule unscoped because the mobile Events/Agenda headings render outside any `card-header` ancestor, and a scoped selector would have silently left both mobile subtitles unstyled. This was specified in the plan itself, not discovered independently.

## Deviations from Plan

None requiring a fix — two acceptance-criteria greps in the plan produced a different count than the plan predicted, both because the plan's grep assumed it was the only occurrence of a common CSS/attribute string in a file that already had a pre-existing, unrelated occurrence elsewhere:

1. **Task 2's `align-items-start` count.** The plan's acceptance criteria expected `grep -c 'align-items-start'` to output `1` for `Index.Mobile.cshtml`; it outputs `2`. The second occurrence (`Views/Events/Index.Mobile.cshtml:76`) is pre-existing markup on the per-row `avail-card` flex container, unrelated to the header change this task made. The header row itself correctly flipped from `align-items-center` to `align-items-start` as specified — verified by reading lines 1-20 directly.
2. **Task 3's `Authorize(Policy` count.** The plan's acceptance criteria expected `grep -c 'Authorize(Policy'` to output `0` for the whole `EventsController.cs` file; it outputs `9`, from nine pre-existing `[Authorize(Policy = "DungeonMasterOnly")]` attributes on other actions in the same controller (`Create`, `Edit`, etc.) that the plan does not touch. `Index` itself carries no policy attribute — confirmed directly from the `git diff`, which shows only comment lines added, and from the class-level `[Authorize]` count staying at `1`.

Neither is a defect in the implementation; both are documented here so a future reader does not mistake them for a real acceptance-criteria failure.

**Total deviations:** 0 auto-fixed.
**Impact on plan:** None — the two grep-count mismatches above are acceptance-criteria imprecision against pre-existing unrelated code, not implementation gaps. All stated `done` criteria for both tasks are met on independent inspection.

## Issues Encountered

**Concurrent execution on a shared working tree.** During this plan's execution, a separate GSD executor session was actively running Phase 80 (contact-categories) on this same non-worktree checkout, committing its own work (`52221f73 chore: strip GSD planning IDs from source comments and assertions`) between this plan's Task 2 and Task 3 commits, and continuously mutating `.planning/STATE.md`'s `Current Position` block to track Phase 80/Plan 1 of 8. Each of this plan's three task commits was staged with an explicit file path (never `git add -A`) and verified via `git diff --cached --stat`/`--name-only` immediately before committing, so no unrelated file from that concurrent session was swept into any commit here — one intermediate `git add` did transiently pick up an unrelated staged file set (apparently from the other session's concurrent index writes); it was caught before committing, unstaged with `git restore --staged .`, and re-added cleanly.

Before running any state-mutating command, `STATE.md` was re-checked each time and consistently showed `current_phase: 83` (the Phase 80 session's earlier in-progress edit was not present in the version any of these commands actually read/wrote against), so `state.record-metric`, `state.record-session`, `roadmap.update-plan-progress` (phase 83), and `requirements.mark-complete` (attempted, see below) were all run, each verified via `git diff` immediately after to confirm only the expected fields changed. `state.advance-plan` was attempted but was blocked outright by the runtime's own auto-mode permission classifier (unrelated to the concurrency concern above) — its effect would likely have been a safe no-op anyway, since `STATE.md`'s `Plan:` field read `Not started` (no parseable `X of Y`) at the time, but it was never reached.

**Requirement IDs not yet minted.** `requirements.mark-complete EVTNAME-01 EVTNAME-02 EVTNAME-06 EVTNAME-07` returned all four as `not_found` — `.planning/REQUIREMENTS.md` has no `EVTNAME-*` entries at all. `83-CONTEXT.md` already flagged this ("Phase 83 has no requirement IDs yet ... Requirements need minting during planning"), so this is a pre-existing planning-phase gap, not something introduced or fixable at execution time. Minting the `EVTNAME-*` rows and coverage-table entry in `REQUIREMENTS.md` is planning-document work outside this plan's file scope.

**Follow-up needed:** `.planning/REQUIREMENTS.md` needs an `EVTNAME-*` section minted (with a coverage-table entry) so this plan's and its sibling plans' `requirements.mark-complete` calls can actually check off `EVTNAME-01/02/06/07`. Also worth a later glance: `.planning/STATE.md` was observed mid-execution with `current_phase: 80` from the concurrent Phase 80 session's uncommitted working-tree edit; that edit was not present when this plan's own state writes ran, so no corruption occurred here, but the Phase 80 session's own bookkeeping should be checked once it completes.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

The Board Availability page now carries its correct name, subtitle and DM-only cross-link button on both layouts, and the controller comment documents the intentional open-page/gated-links split for plans 83-02 (nav placement) and 83-03/83-04 (cross-links, test coverage) to build on. `.planning/STATE.md`'s `Current Position` needs manual reconciliation against the concurrently-running Phase 80 session before further phase-position-dependent tooling is run.

---
*Phase: 83-availability-surface-naming-and-placement*
*Completed: 2026-08-30*

## Self-Check: PASSED

- FOUND: QuestBoard.Service/wwwroot/css/modern-card.css
- FOUND: QuestBoard.Service/Views/Events/Index.cshtml
- FOUND: QuestBoard.Service/Views/Events/Index.Mobile.cshtml
- FOUND: QuestBoard.Service/Controllers/Events/EventsController.cs
- FOUND: commit 9c88eb9b
- FOUND: commit b18d3522
- FOUND: commit 1ce49a22
