---
phase: 83-availability-surface-naming-and-placement
verified: 2026-08-30T10:27:41Z
status: passed
score: 7/7 must-have truths verified (across all 4 plans)
behavior_unverified: 0
overrides_applied: 0
---

# Phase 83: Availability Surface Naming and Placement Verification Report

**Phase Goal:** The two availability surfaces say what they are and sit where the people who need them will look — the cross-board personal view and the board-scoped grid stop competing for the same reader.
**Verified:** 2026-08-30T10:27:41Z
**Status:** passed
**Re-verification:** No — initial verification

## Goal Achievement

### Observable Truths

| # | Truth | Status | Evidence |
|---|-------|--------|----------|
| 1 | Board Availability page renamed on desktop + mobile, with subtitle naming board and events scope (D-02, D-03) | VERIFIED | `Views/Events/Index.cshtml:6,20` and `Index.Mobile.cshtml:6,21` both read `ViewData["Title"] = "Board Availability"` and render `<p class="header-subtitle mb-0">Events on @subtitleBoardName</p>`; fallback `"this board"` when `SessionKeys.ActiveGroupName` is empty |
| 2 | My Agenda keeps its name, gains matching subtitle (D-04) | VERIFIED | `Views/Agenda/Index.cshtml:6,26` and `Index.Mobile.cshtml:6,25` — title unchanged, subtitle `"Upcoming events across all your boards"` on both layouts |
| 3 | Board Availability's My Agenda header button is DM-only; My Agenda's return button to Board Availability is DM-only (D-06/D-10/D-11) | VERIFIED | Both `Events/Index*.cshtml` wrap the button in `@if (isDm)`; both `Agenda/Index*.cshtml` wrap their return button the same way — all four confirmed by direct file read |
| 4 | Calendar cross-link to Board Availability renders DM-only on both layouts; adjacent My Agenda button stays unconditional (D-08) | VERIFIED | `Calendar/Index.cshtml:16-24` and `Index.Mobile.cshtml:34-42` — the Board Availability anchor is wrapped in the `DungeonMasterOnly` check, the My Agenda anchor sits outside it, unchanged |
| 5 | Board Availability's nav entry sits inside the DM menu, directly after Create Event, behind both the DM policy and the resolved-board-type gate, on both layouts (D-06/D-07) | VERIFIED | `_Layout.cshtml:104-114` and `_Layout.Mobile.cshtml:84-96` — entry is the immediate sibling after `Create Event`, nested inside `activeBoardType is BoardType.OneShot or BoardType.Campaign`, inside the enclosing `DungeonMasterOnly` block |
| 6 | Desktop Calendar dropdown collapses to a plain nav-item (D-05) | VERIFIED | `_Layout.cshtml:180-185` — plain `<li class="nav-item">` with `fa-calendar-alt me-1`, no dropdown markup, no `calendarDropdown` id anywhere in the file |
| 7 | `EventsController.Index` keeps no authorization gate; a player still gets 200 with the full grid (D-09) | VERIFIED (code + behavioral test) | `EventsController.cs:14,38` — bare `[Authorize]` class attribute, no policy on `Index`; `EventsOverviewControllerIntegrationTests.Index_PlayerWithoutDmRole_ReturnsOkAndRendersGrid` explicitly authenticates as Player-only and asserts 200 + rendered grid + seeded content — **test executed and passed** |

**Score:** 7/7 truths verified, 0 present-but-behavior-unverified

### Required Artifacts

| Artifact | Expected | Status | Details |
|----------|----------|--------|---------|
| `QuestBoard.Service/wwwroot/css/modern-card.css` | `.header-subtitle` rule + fix for the card-header cascade override | VERIFIED | Unscoped `.header-subtitle` rule (5 declarations, no color) plus `.modern-card-header .header-subtitle` override rule added in commit `43a8f052` restating `color: #1a1a1a`, light text-shadow, `font-weight: 400`, all `!important`, scoped only to the card-header ancestor so mobile headers (outside `.modern-card`) are unaffected |
| `QuestBoard.Service/Views/Events/Index.cshtml` + `.Mobile.cshtml` | Renamed title, subtitle, DM-gated button | VERIFIED | Confirmed by direct read; both layouts identical in structure |
| `QuestBoard.Service/Views/Agenda/Index.cshtml` + `.Mobile.cshtml` | Subtitle, DM-gated return button | VERIFIED | Confirmed by direct read; both layouts identical in structure |
| `QuestBoard.Service/Views/Calendar/Index.cshtml` + `.Mobile.cshtml` | Renamed, DM-gated cross-link | VERIFIED | Confirmed by direct read; My Agenda button untouched in both |
| `QuestBoard.Service/Views/Shared/_Layout.cshtml` + `.Mobile.cshtml` | Moved nav entry, collapsed Calendar dropdown | VERIFIED | Confirmed by direct read; My Agenda unconditional entry untouched in both |
| `QuestBoard.Service/Controllers/Events/EventsController.cs` | Extended comment, unchanged attribute set | VERIFIED | Bare `[Authorize]`, no policy, comment documents open-page/gated-links split with no planning references |
| `QuestBoard.IntegrationTests/Controllers/LayoutNavigationTests.cs` | 6 new role/board-type theories, retired label gone | VERIFIED | All 6 methods present and confirmed passing (12 executed cases across both user agents) |
| `QuestBoard.IntegrationTests/Controllers/CalendarButtonStyleTests.cs` | 2 DM-seeded cases + 1 player-absent theory | VERIFIED | Confirmed by direct read and passing test run (4 executed cases) |
| `QuestBoard.IntegrationTests/Controllers/EventsOverviewControllerIntegrationTests.cs` | +1 player-reachability case | VERIFIED | `Index_PlayerWithoutDmRole_ReturnsOkAndRendersGrid` present, passing, existing cases byte-identical |
| `QuestBoard.IntegrationTests/Controllers/StaleAvailabilityOverviewLabelGuardTests.cs` | New guard class, 3 theories x 2 user agents | VERIFIED | File exists, 6 executed cases, all passing |
| `.planning/REQUIREMENTS.md` | EVTNAME-01..07 defined, coverage reconciled | VERIFIED | 7 definitions + 7 traceability rows present; coverage block reads 82/82 (adjusted upward from the plan's expected 67 due to a concurrent session's CONTACTCAT mint landing in between — both counters agree with each other and with their own tables) |
| `.planning/ROADMAP.md` | Phase 83 entry resolved, no TBD, coverage reconciled | VERIFIED | Requirements/Plans lines resolved, all 4 plans listed under Wave 1/Wave 2, no `TBD` anywhere in the entry, 7 new coverage rows, 82/82 line, 2026-08-30 scope-note amendment intact |

### Key Link Verification

| From | To | Via | Status | Details |
|------|-----|-----|--------|---------|
| `_Layout.cshtml` DM dropdown | `EventsController.Index` | `asp-controller="Events" asp-action="Index"`, nested inside `DungeonMasterOnly` + `activeBoardType` gate | WIRED | Confirmed sibling-of-`Create Event` placement, correct nesting |
| `_Layout.Mobile.cshtml` DM block | `EventsController.Index` | same, flat sibling | WIRED | Confirmed identical structure to desktop |
| `Calendar/Index.cshtml`/`.Mobile.cshtml` | `EventsController.Index` | DM-gated anchor, label `Board Availability` | WIRED | Confirmed; adjacent My Agenda anchor unconditional |
| `Agenda/Index.cshtml`/`.Mobile.cshtml` | `EventsController.Index` | DM-gated return button | WIRED | Confirmed |
| `Events/Index.cshtml`/`.Mobile.cshtml` | `AgendaController.Index` | DM-gated My Agenda button | WIRED | Confirmed |
| `.header-subtitle` (CSS) | Both `<p class="header-subtitle mb-0">` elements | shared unscoped rule + card-header-scoped override | WIRED | Confirmed rule present in `modern-card.css`; override commit `43a8f052` correctly scoped to `.modern-card-header .header-subtitle` so mobile (no card-header ancestor) is unaffected |

### Behavioral Spot-Checks / Test Execution

| Behavior | Command | Result | Status |
|----------|---------|--------|--------|
| Full build | `dotnet build` | 6 projects, 0 errors, 20 (pre-existing, unrelated NU1608) warnings | PASS |
| Phase-relevant integration suites | `dotnet test --filter "LayoutNavigationTests\|CalendarButtonStyleTests\|EventsOverviewControllerIntegrationTests\|StaleAvailabilityOverviewLabelGuardTests\|EventsOverviewMobileStyleTests\|RowNavigationAccessibilityTests\|AgendaControllerIntegrationTests\|AgendaMobileRenderTests\|AgendaTenantIsolationTests\|CalendarControllerIntegrationTests\|CalendarBoardTypeScopeTests\|CalendarHorizonBannerTests"` | 143 passed, 0 failed | PASS |
| Guard class case count | `dotnet test --filter StaleAvailabilityOverviewLabelGuardTests` (verbose) | 6/6 passed, one per (surface × user agent) | PASS |
| Retired string absent from source | `grep -rl "Availability Overview" --include=*.cshtml --include=*.cs --include=*.css .` (excluding `.planning`) | Only `StaleAvailabilityOverviewLabelGuardTests.cs` (the guard asserting its absence) | PASS |
| Planning-reference leak scan | `grep -E "Phase 83\|D-[0-9]+\|EVTNAME"` across all 14 phase-touched source/test files | 0 matches | PASS |
| Debt-marker scan | `grep -E "TBD\|FIXME\|XXX"` across all 14 phase-touched files | 0 matches | PASS |

### Requirements Coverage

| Requirement | Source Plan(s) | Description | Status | Evidence |
|-------------|----------------|-------------|--------|----------|
| EVTNAME-01 | 83-01, 83-02, 83-03, 83-04 | "Board Availability" everywhere, retired label gone | SATISFIED | All rename sites confirmed; guard class proves it across 3 surfaces x 2 UAs |
| EVTNAME-02 | 83-01 | Board Availability subtitle with board name + fallback | SATISFIED | Confirmed in both layouts |
| EVTNAME-03 | 83-02 | My Agenda matching subtitle | SATISFIED | Confirmed in both layouts |
| EVTNAME-04 | 83-03 | Nav entry in DM menu after Create Event, both gates | SATISFIED | Confirmed in both layouts; 6 role-flip theories passing |
| EVTNAME-05 | 83-02, 83-03 | Every discoverable route DM-only | SATISFIED | Nav entry + Calendar cross-link both confirmed DM-gated |
| EVTNAME-06 | 83-01, 83-02 | Cross-buttons DM-only both directions; My Agenda menu entry stays unconditional | SATISFIED | Confirmed both header buttons gated; unconditional menu entries in both layouts untouched |
| EVTNAME-07 | 83-01, 83-04 | Page itself keeps no gate, proven by automated case | SATISFIED | Bare `[Authorize]` confirmed; `Index_PlayerWithoutDmRole_ReturnsOkAndRendersGrid` passing |

No orphaned requirements — the roadmap's Phase 83 Requirements line lists exactly EVTNAME-01..07, and this is the complete set cited across all 4 plans' frontmatter.

### Anti-Patterns Found

None. No debt markers (`TBD`/`FIXME`/`XXX`), no placeholder copy, no empty implementations, and no planning-reference leaks were found in any of the 14 phase-touched files.

### Code Review Warning — Verified Fixed

`83-REVIEW.md` (2026-08-30) found one Warning (WR-01): the new unscoped `.header-subtitle` rule set no `color`/`text-shadow`, so on desktop the pre-existing `.modern-card p` rule (higher specificity, `!important`) silently repainted both subtitles in the heading's gold with a heavy shadow instead of the intended muted `#1a1a1a` treatment. Mobile was unaffected (headers sit outside `.modern-card` there).

**Fix verified present and correctly scoped.** Commit `43a8f052` adds:

```css
.modern-card-header .header-subtitle {
    color: #1a1a1a !important;
    text-shadow: 1px 1px 2px rgba(255, 255, 255, 0.8) !important;
    font-weight: 400 !important;
}
```

This is scoped to `.modern-card-header .header-subtitle` — i.e. only applies where the subtitle sits inside a card header (the two desktop views). The mobile headers (`Events/Index.Mobile.cshtml`, `Agenda/Index.Mobile.cshtml`) render outside any `.modern-card-header` ancestor and are correctly left untouched by this override, matching the commit message's stated intent ("scoped to the card header so the mobile headers, which sit directly on the page background, are unaffected"). `dotnet build` passes with this change in place.

### Human Verification Required

None. All must-have truths resolve to concrete, testable evidence in the codebase (grep-confirmed markup, passing automated tests exercising the exact role-gating and page-reachability behaviors, and a code-review finding whose fix was independently re-verified). The CSS specificity fix is a deterministic, mechanically-checkable change (selector scope, exact property values) rather than a subjective visual judgment call, so it does not require a screenshot-based human check to close.

### Gaps Summary

No gaps. Every must-have truth from all four plans (83-01 through 83-04) is backed by direct file evidence and, where behavioral, by a passing automated test that was executed during this verification (not merely claimed in a SUMMARY.md). The one code-review Warning found during the phase was fixed in a follow-up commit and that fix was independently re-verified here — correctly scoped, builds clean. The ledger (`.planning/REQUIREMENTS.md` and `.planning/ROADMAP.md`) is closed: all 7 EVTNAME identifiers are defined and traced, both coverage counters reconcile with their own tables at 82/82, all four plans are listed under their wave headings, no `TBD` remains in the Phase 83 entry, and the 2026-08-30 scope-note amendment survives unchanged. Note: `.planning/ROADMAP.md` was already LF-only before this phase touched it (confirmed via `git show <pre-phase-commit>:.planning/ROADMAP.md`), so its current LF state is a pre-existing condition, not a regression introduced by this phase's edits.

---

_Verified: 2026-08-30T10:27:41Z_
_Verifier: Claude (gsd-verifier)_
