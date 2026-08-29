---
phase: 77-availability-overview-page
plan: 09
subsystem: availability-overview-tests
tags: [integration-tests, mobile-rendering, gap-closure, requirements-ledger]
status: complete
dependency-graph:
  requires:
    - EventOverviewViewModel.CanShowMore (77-05)
    - EventsController.Index clamp and NextTake computation (77-05, 77-07)
  provides:
    - Rendering coverage for Index.Mobile.cshtml under a mobile user agent
    - Fail-capable assertions on the take clamp (ceiling and floor) and the three per-event counts
    - EVTVIEW-01..04 recorded as complete in REQUIREMENTS.md
  affects:
    - QuestBoard.IntegrationTests/Controllers/EventsOverviewControllerIntegrationTests.cs
    - .planning/REQUIREMENTS.md
tech-stack:
  added: []
  patterns:
    - "Per-test User-Agent override via HttpRequestMessage + TryAddWithoutValidation, sent through the same authenticated HttpClient, mirroring LayoutNavigationTests's mechanism"
    - "Batch event seeding in a single DbContext/SaveChangesAsync for high-volume clamp tests"
key-files:
  created: []
  modified:
    - QuestBoard.IntegrationTests/Controllers/EventsOverviewControllerIntegrationTests.cs
    - .planning/REQUIREMENTS.md
decisions:
  - "GetMobileAsync hardcodes the mobile User-Agent internally rather than taking it as a parameter, matching the plan's fixed helper signature; each mobile fact's method name itself contains the literal string 'MobileUserAgent', which is what the plan's grep-based acceptance criteria actually count toward the constant/helper/fact total of 6"
  - "SeedEventsAsync (batch helper) is used only where the plan calls for it (105-event ceiling test, 3-event floor test); the two-event mobile facts kept using the existing single-event SeedEventAsync since they need a returned id or only seed a small, fixed count"
metrics:
  duration: "~45 minutes"
  completed: 2026-08-29
---

# Phase 77 Plan 09: Availability Overview Gap Closure (Test Hardening) Summary

Added four integration facts that render `Index.Mobile.cshtml` under a real mobile user agent, rewrote the `take`-clamp and three-count assertions so they fail when the behaviour they name is removed, and closed out all four availability-overview requirement identifiers in the ledger.

## What Was Built

**Task 1 — Mobile rendering coverage** (`QuestBoard.IntegrationTests/Controllers/EventsOverviewControllerIntegrationTests.cs`)

- Added `MobileUserAgent` (same iPhone UA string `LayoutNavigationTests` declares), a `GetMobileAsync(HttpClient, string)` helper that attaches the header via `TryAddWithoutValidation` and sends through the supplied authenticated client, and a `SeedEventsAsync(string, int)` batch-seeding helper for later high-volume tests.
- Four new facts render `Index.Mobile.cshtml` for the first time in this test suite:
  - `Index_MobileUserAgent_RendersCardList` — asserts the mobile card class, mobile card title class, both seeded titles, and (negatively) the absence of the desktop grid table class.
  - `Index_MobileUserAgent_RendersRosterToggle` — asserts the roster toggle class, the collapse target id built from the event id, and that exactly two `event.stopPropagation()` guards render for one card (toggle button + collapse container).
  - `Index_MobileUserAgent_NoEvents_RendersEmptyState` — asserts the empty-state heading and the absence of the mobile card class.
  - `Index_MobileUserAgent_MoreEventsThanTake_ShowsShowMoreControl` — requests `take=1` against two seeded events and asserts the paging control copy plus a `take=11` query fragment (window + configured page increment).

**Task 2 — Fail-capable clamp and count assertions** (same file)

- `Index_TakeAboveMax_IsClampedAndStillReturnsOk` renamed to `Index_TakeAboveMax_IsClampedToMaxTake`; now seeds 105 events via `SeedEventsAsync`, asserts the rendered row count is exactly 100 (not `<= 100`), and asserts the paging control is absent once the window sits on the ceiling.
- `Index_TakeZeroOrNegative_StillReturnsOk` now seeds 3 events and asserts exactly one rendered row for both the zero and the negative `take` requests.
- `Index_RendersAllThreeCounts` now asserts the three rendered figures verbatim (`<strong>3</strong> Yes`, `(2 confirmed)`, `2 Maybe`) instead of substrings the legend also renders unconditionally.
- `Index_MemberWithNoRowForOneEvent_RendersEmptyCell` no longer captures the second event's id into a variable it immediately discards.

**Task 3 — Requirements ledger** (`.planning/REQUIREMENTS.md`)

- Ticked `EVTVIEW-04` (the only remaining unticked availability-overview checkbox).
- Moved all four `EVTVIEW-0[1-4]` rows in the traceability table from "Not started" to "Complete", matching the checkboxes and the verification report.

## Mutation Verification

Per the plan's constraint, every strengthened or new test was verified to actually fail against the mutation it claims to catch, by applying the mutation, running the specific test, observing the failure, and reverting:

| Test | Mutation applied | Result |
|------|-------------------|--------|
| `Index_TakeAboveMax_IsClampedToMaxTake` | Removed `Math.Clamp` from `EventsController.Index` (`effectiveTake = take ?? options.DefaultTake`) | FAILED — "Expected rowCount to be 100, but found 105" |
| `Index_TakeZeroOrNegative_StillReturnsOk` | Same clamp removal | FAILED — "Expected ... to be 1, but found 0" |
| `Index_TakeAboveMax_IsClampedToMaxTake` (paging-absence half) | Reverted `Index.cshtml`'s gate from `Model.CanShowMore` back to `Model.HasMore` | FAILED — expected NOT to contain "Show More Events" but did |
| `Index_RendersAllThreeCounts` | Changed `_AvailabilityCounts.cshtml`'s confirmed-subset figure from `Model.ConfirmedYesCount` to `Model.YesCount` (folding unconfirmed in) | FAILED — expected to contain "(2 confirmed)" but did not |
| `Index_MobileUserAgent_MoreEventsThanTake_ShowsShowMoreControl` | Removed the paging block from `Index.Mobile.cshtml` | FAILED — expected to contain "Show More Events" but did not |
| `Index_MobileUserAgent_RendersRosterToggle` | Removed `onclick="event.stopPropagation();"` from the roster collapse container | FAILED — "Expected guardCount to be 2, but found 1" |
| `Index_MobileUserAgent_RendersCardList` | Added the desktop grid class (`avail-grid`) onto the mobile card element, simulating a regression to desktop-like markup | FAILED — expected NOT to contain "avail-grid" but did |

All seven mutations were reverted after observing the expected failure, and the full `EventsOverview` filter (22 facts) was re-run clean after each revert. `git diff --name-only` against the wave-start commit confirms only the two files this plan is scoped to (the integration test file and `REQUIREMENTS.md`) differ — no production file was left modified.

## Verification

- `dotnet build QuestBoard.IntegrationTests/QuestBoard.IntegrationTests.csproj` — 0 errors, throughout.
- `dotnet test QuestBoard.IntegrationTests/QuestBoard.IntegrationTests.csproj --filter "FullyQualifiedName~EventsOverviewControllerIntegrationTests"` — 17 passed, 0 failed after Task 1 (13 pre-existing + 4 new).
- `dotnet test QuestBoard.IntegrationTests/QuestBoard.IntegrationTests.csproj --filter "FullyQualifiedName~EventsOverview"` — 22 passed, 0 failed after Task 2 (17 controller facts + 5 tenant isolation facts).
- `dotnet test` over the whole solution — 408/408 unit, 558/558 integration (up from 408/554 before this plan — exactly four higher, matching the four new mobile facts).
- `grep -c 'IgnoreQueryFilters'` across `EventRepository.cs`, `EventService.cs`, `EventsController.cs` — `0` for all three.
- All plan-specified grep acceptance criteria verified programmatically and matched exactly (see Deviations note below on the pre-existing test count).
- `git diff --name-only` against the wave-start commit lists exactly `.planning/REQUIREMENTS.md` and the integration test file.

## Deviations from Plan

### Auto-fixed Issues

None — no bugs, missing functionality, or blocking issues were encountered; every plan action executed as specified.

### Noted Discrepancy (not a deviation, no action taken)

**Plan's expected pre-existing/post-Task-1 test counts did not match the actual repository state.** The plan's acceptance criteria stated "22 passed... (18 pre-existing plus 4 new)" for Task 1 and "27 passed... (22 controller facts after Task 1 plus the 5 tenant isolation facts)" for Task 2's `EventsOverview` filter. The actual pre-existing `EventsOverviewControllerIntegrationTests` count was 13 facts, not 18, so the real totals are 17 after Task 1 and 22 after Task 2 (17 + 5 tenant isolation). This is a miscount in the plan's expected numbers, not a defect in the implementation — the underlying behavioural requirements (build clean, all facts pass, specific grep patterns present/absent at their stated counts) were all met exactly as written. No code or test was changed because of this; it is recorded here for traceability only.

## Threat Flags

None — this plan touches only test code and a planning document. The threat register's `IgnoreQueryFilters` gate (T-77-01) was re-verified clean across all three named production files, and the five tenant isolation facts (`EventsOverviewTenantIsolationTests`) still pass unmodified.

## Known Stubs

None.

## Self-Check: PASSED

- FOUND: `QuestBoard.IntegrationTests/Controllers/EventsOverviewControllerIntegrationTests.cs` (contains `MobileUserAgent`, `GetMobileAsync`, `SeedEventsAsync`, all four `Index_MobileUserAgent_*` facts, `Index_TakeAboveMax_IsClampedToMaxTake`, the strengthened count assertions)
- FOUND: `.planning/REQUIREMENTS.md` (four `EVTVIEW` checkboxes ticked, four traceability rows read "Complete")
- FOUND commit `b46a1302` in `git log --oneline` (Task 1)
- FOUND commit `32f1544a` in `git log --oneline` (Task 2)
- FOUND commit `82245a9c` in `git log --oneline` (Task 3)
