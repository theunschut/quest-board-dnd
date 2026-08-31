---
phase: 77-availability-overview-page
plan: 05
subsystem: availability-overview-views
tags: [razor-views, mobile-view, css, gap-closure]
status: complete
dependency-graph:
  requires:
    - EventOverviewViewModel.HasMore / NextTake / Take (set by EventsController.Index, 77-03)
  provides:
    - EventOverviewViewModel.CanShowMore
    - Mobile Show More Events control (Index.Mobile.cshtml)
  affects:
    - QuestBoard.Service/Views/Events/Index.cshtml
    - QuestBoard.Service/Views/Events/Index.Mobile.cshtml
tech-stack:
  added: []
  patterns:
    - "Computed get-only view-model property for a presentation-only growth check (HasMore && NextTake > Take)"
    - "Bounds-checked positional indexing with a fallback enum value, applied identically on both surfaces"
key-files:
  created: []
  modified:
    - QuestBoard.Service/ViewModels/EventViewModels/EventOverviewViewModel.cs
    - QuestBoard.Service/Views/Events/Index.cshtml
    - QuestBoard.Service/Views/Events/Index.Mobile.cshtml
    - QuestBoard.Service/wwwroot/css/events-overview.mobile.css
decisions:
  - "CanShowMore lives on the view model as a computed property rather than as inline Razor logic, so both surfaces share one growth check and any future third surface gets it for free"
  - "The mobile paging control's copy, icon, and href construction are copied character-for-character from the desktop control per the plan's explicit prohibition on divergence"
metrics:
  duration: "~25 minutes"
  completed: 2026-08-29
---

# Phase 77 Plan 05: Availability Overview Gap Closure Summary

Wired the mobile availability overview's missing Show More Events control, fixed the paging control's self-referencing-link ceiling bug on both surfaces, made the mobile roster tap-safe, bounded cell indexing against a short cell collection on both surfaces, and gave the mobile legend real styling it was previously missing.

## What Was Built

**Task 1 — Paging growth flag and desktop hardening**
(`QuestBoard.Service/ViewModels/EventViewModels/EventOverviewViewModel.cs`, `QuestBoard.Service/Views/Events/Index.cshtml`)

- Added `EventOverviewViewModel.CanShowMore`, a get-only computed property (`HasMore && NextTake > Take`). This is the fix for CR-01's companion warning: once `Take` is clamped up to `EventsOverviewOptions.MaxTake`, `NextTake` equals `Take` while `HasMore` stays true, so the old `Model.HasMore`-only gate rendered a "Show More Events" link back to the page the reader was already on.
- Rewrote the class-level comment, which previously claimed a consumer (`Take`) that did not exist, and added a short comment above `CanShowMore` explaining the ceiling case.
- The desktop paging block now gates on `Model.CanShowMore` instead of `Model.HasMore`; copy, icon, classes, and href are unchanged.
- Added `@using QuestBoard.Domain.Enums` to `Index.cshtml` and shortened the five fully-qualified `AvailabilityCellState` legend references to match the short form the mobile view already used.
- The per-member cell loop now resolves `var cell = i < row.Cells.Count ? row.Cells[i] : AvailabilityCellState.Empty;` instead of dereferencing `row.Cells[i]` directly, so a future aggregation change that produces a short cell collection degrades to an empty cell rather than throwing mid-render on a page every member uses.

**Task 2 — Mobile paging control, inert roster, real legend styling**
(`QuestBoard.Service/Views/Events/Index.Mobile.cshtml`, `QuestBoard.Service/wwwroot/css/events-overview.mobile.css`)

- Added a Show More Events control to the mobile card list, placed after the `foreach` over `Model.Rows` closes and before the `else` branch closes, gated on `Model.CanShowMore`. The control matches the desktop control's copy (`Show More Events`), icon (`fas fa-chevron-down me-2`), and href construction (`@Url.Action("Index", new { take = Model.NextTake })`) character for character; the only intentional difference is the full-width `d-grid mb-3` wrapper versus the desktop's centred `text-center` wrapper, since a mobile primary action is a full-width tap target.
- Added `onclick="event.stopPropagation();"` to the roster collapse container (`div.collapse.mt-2#roster-@row.EventId`). Previously only the toggle button guarded itself, so tapping a name, a chip, or the whitespace inside an expanded roster bubbled to the card's navigation handler and left the overview page.
- Applied the same bounds-checked cell resolution as the desktop view inside the roster loop.
- Duplicated the three `.legend-card` rules (`height: fit-content`, `padding: 0.75rem !important`, `font-size: 0.75rem; line-height: 1.2`) from `calendar.css` into `events-overview.mobile.css`, since `_Layout.Mobile.cshtml` never loads `calendar.css` and the mobile legend previously rendered with no compact styling at all — following the same duplication policy this file's header comment already documents for the cell-state rules.

## Verification

- `dotnet build QuestBoard.Service/QuestBoard.Service.csproj` — 0 errors (both after Task 1 and after Task 2).
- `dotnet test QuestBoard.IntegrationTests/QuestBoard.IntegrationTests.csproj --filter "FullyQualifiedName~EventsOverview"` — 18 passed, 0 failed (both after Task 1 and after Task 2).
- `dotnet test QuestBoard.UnitTests/QuestBoard.UnitTests.csproj --filter "FullyQualifiedName~EventsOverview"` — 14 passed, 0 failed.
- All acceptance-criteria grep checks from the plan (property presence, `Model.CanShowMore` usage, `@using` line, zero fully-qualified enum refs, `row.Cells.Count` bound, `event.stopPropagation` count of 2, `legend-card` count of 3, zero `method="post"` on either view) match the plan's expected values exactly.
- `grep -c 'IgnoreQueryFilters'` across `EventRepository.cs`, `EventService.cs`, `EventsController.cs` — `0` for all three; no filter bypass introduced.
- `grep -c 'Html.Raw'` across both overview views — `0` for both.
- No requirement, decision, plan, or review-finding identifier appears in any comment or string literal added by this plan (checked by pattern search across all four modified files).

## Deviations from Plan

None — plan executed exactly as written.

## Threat Flags

None — this plan adds no new query, no new endpoint, no new write path, and no `Html.Raw`. The threat register in the plan's frontmatter (T-77-01, T-77-02, T-77-09, T-77-08, T-77-SC) already accounts for the surface this plan touches; the `IgnoreQueryFilters` and `Html.Raw` gates it specifies both came back clean.

## Known Stubs

None.

## Self-Check: PASSED

- FOUND: `QuestBoard.Service/ViewModels/EventViewModels/EventOverviewViewModel.cs` (contains `CanShowMore`)
- FOUND: `QuestBoard.Service/Views/Events/Index.cshtml` (contains `Model.CanShowMore`, `@using QuestBoard.Domain.Enums`, `row.Cells.Count`)
- FOUND: `QuestBoard.Service/Views/Events/Index.Mobile.cshtml` (contains `Model.CanShowMore`, `Model.NextTake`, `Show More Events`, two `event.stopPropagation()` calls, `row.Cells.Count`)
- FOUND: `QuestBoard.Service/wwwroot/css/events-overview.mobile.css` (contains three `legend-card` rules)
- FOUND commit `f65e87c0` in `git log --oneline`
- FOUND commit `8b561e7e` in `git log --oneline`
