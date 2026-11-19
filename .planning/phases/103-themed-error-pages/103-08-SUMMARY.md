---
phase: 103-themed-error-pages
plan: 08
subsystem: cross-board-resolver
tags: [cross-board, resolver, registry, unit-tests, drift-guard, tenancy, live-check, tracer]

requires:
  - phase: 103-themed-error-pages
    provides: board-aware resolver, registry entries with access and page, route target fill (103-07)
provides:
  - Registry pins - 18 routes, an access and page row per route, and a reflection guard against the controllers' page attributes
  - Route-target pins - both parsers fill kind, access and page for every registered route; malformed, area and unregistered addresses still yield none
  - Resolver unit facts - board ids reaching the repository per access level, SuperAdmin, page availability and the profile rules
  - Live board-level record on fixture data (after/103-08/leak.json) and a fixed banner reading in the probe
affects: [103-09, 103-10, 103-18]

actuals:
  tokens: 26000
  tasks: 3
  commits: 2

tech-stack:
  added: []
  patterns:
    - "Registry page and controller attribute are two statements of one fact; a reflection fact compares them so neither can change alone"
    - "Resolver narrowing pinned by asserting which board ids reach the substituted repository, or that it is not reached at all"

key-files:
  created:
    - QuestBoard.UnitTests/Services/CrossBoardLinkResolverServiceTests.cs
    - .planning/phases/103-themed-error-pages/103-08-SUMMARY.md
  modified:
    - QuestBoard.UnitTests/Helpers/CrossBoardLinkRegistryTests.cs
    - QuestBoard.UnitTests/Helpers/CrossBoardRouteTargetTests.cs
    - QuestBoard.IntegrationTests/Tests/ErrorPagesGuardTests.cs

key-decisions:
  - "No production change: every pin passed against 103-07's resolver and registry, so the audit table and the built behaviour agree"
  - "The drift guard requires the pages named across an action's overloads and its controller to be exactly the registered page (the gate needs every named page) and no overload to be exempt from the gate"

requirements-completed: [D-01, D-04, D-05, D-06]

duration: 35min
completed: 2026-11-19
status: complete
---

# Phase 103 Plan 08: Resolver pins and live board-level proof Summary

**The board-aware resolver of the previous plan is now held by 98 new unit facts (registry, route target, resolver) with a reflection guard that fails when a controller's page attribute and the registry's page drift apart, and the live probe shows a Player of the other board is not switched while a Dungeon Master of it is.** No production file changed; no pin exposed a mismatch with the audit table.

## What is pinned

- **Registry** (`CrossBoardLinkRegistryTests`): still exactly 18 routes; `RouteEntryTable_CoversEveryRegisteredRoute` ties the 18-row table to the registry's key set; `TryGetRouteEntry_EachRegisteredRoute_ResolvesToItsKindAccessAndPage` has a row per route (kind, access, page from the audit table); `EachRegisteredRoute_CarriesThePageItsActionIsGatedBy` reads the page attributes of every overload of the action and of the controller by reflection and requires exactly the registered page, with no overload exempt from the gate. The area case `TryGetRouteEntry` returns nothing for `Platform` and matches for a null or empty area. The existing `TryGetLookupKind` facts are unchanged and green.
- **Route target** (`CrossBoardRouteTargetTests`): for all 18 routes both `TryFromLocalUrl` and `TryFromRouteValues` fill kind, access and page equal to the registry entry; the Quest/Edit fact from the tracer stays. Area, four-segment, non-numeric, missing-id and unregistered-action addresses still yield no target.
- **Resolver** (`CrossBoardLinkResolverServiceTests`, new), substituting the group service and the repository and asserting the board ids that reach the lookup:
  - no memberships: null, no repository read at all (also for a SuperAdmin who is not a member, across four access levels);
  - Player-only board dropped for DungeonMasterTier and QuestOwnerOrAdmin, with no lookup; kept for Member and for the record-rule levels (CharacterOwnerOrAdmin, CompletedQuest, ContactVisible, ShopItemVisible);
  - Dungeon Master and Admin boards kept for DungeonMasterTier; Player B plus Dungeon Master C offers only C;
  - a SuperAdmin Player counts as Admin on that member board for DungeonMasterTier, QuestOwnerOrAdmin and AdminForOtherUser, and only member boards are ever read or offered;
  - page switched off, board type that never offers the page, and the quest log with the quest list switched off are dropped (pageGateCounts yes);
  - profile edit: two shared boards of which one is Admin resolve to that one; no Admin board gives null without a lookup; two shared Admin boards stay ambiguous; profile view drops a board without the profile page before the exactly-one rule, and two eligible boards stay ambiguous.
- `ErrorPagesGuardTests.PhaseSourceFiles` lists the three test files, so the planning-reference scan covers them.

Mutation checks, each reverted: changing a registry page (Shop to Contacts for the shop edit route) fails the table and the drift facts; dropping the SuperAdmin raise in the resolver fails four resolver facts.

## Live board-level check (fixture data, 8350 on the new tree, 8351 frozen baseline for contrast)

`tools/leak-probe.mjs --cases board-level`, results in `after/103-08/leak.json` (baseline in `leak-baseline-8351.json`), One-Shot active before every request:

| Viewer | Request | New tree | Baseline |
|--------|---------|----------|----------|
| Player of the other board | Quest/Edit of the open quest on Two | stays on One-Shot, 8980 bytes | switched to Two, 8984 bytes |
| Player of the other board | ShopManagement/Edit of the published item on Two | stays on One-Shot, 8980 bytes | switched to Two, 8984 bytes |
| Player | nonexistent Quest/Edit and ShopManagement/Edit ids (controls) | stays, 8980 bytes | stays, 8980 bytes |
| Dungeon Master of Two (Player on One-Shot) | ShopManagement/Edit of the Draft item on Two | switched to Two, served 200, 23230 bytes, banner on the response | same |
| Dungeon Master of Two | Quest/Edit of the open quest on Two | switched to Two, served 200, 23756 bytes, banner on the response | same |

The Player's answer is byte-for-byte the length of a nonexistent id's answer on the new tree, so the refusal is indistinguishable from "no such record". The plan's assertion (Player boardAfter equals boardBefore and bannerNext false; Dungeon Master switched) printed `ok`.

**Tooling defect from the previous plan, fixed.** The banner column of `resolver-live-103-07.mjs` read false on every row because the one-shot switch banner is rendered on the page the switch itself serves and that page consumes it, so reading it on the following `/quests` always misses it. `leak-probe.mjs` now records `bannerOnResponse` (read straight after the navigation, true on both Dungeon Master switches, false on every refusal) next to the old `bannerNext` (kept as a control, false throughout). The earlier 103-07 rows are therefore not evidence for the banner but their board columns were sound and agree with this run.

## Deviations from Plan

None - plan executed as written. The plan's verify chains `--cases board-level`, which the probe lacked, so it was added (with a `viewer` field on every row); that is the "extend it with cases if needed" the plan allows. The Player's one-shot banner check in the plan reads `bannerNext`; it stays false on both trees' refusals, and `bannerOnResponse` is the meaningful column.

## Test results

- Unit: 1777 passed, 0 failed (was 1679; 98 new).
- Integration: 3167 passed, 22 skipped, 0 failed (unchanged: this plan added no integration facts, only the guard's file list).

## Known Stubs

None.

## Threat Flags

None. Test files and a local probe only; no production file changed (`git diff` of QuestBoard.Service and QuestBoard.Repository against the plan's start commit is empty).

## Left running

Nothing of this plan: 8350 is stopped; the frozen baseline on 8351 is still running. SQL Server was not touched, and no SQL writes were made.

## Self-Check: PASSED

- Present: `CrossBoardLinkResolverServiceTests.cs`; commits `53e41833` and `37c3eebb` exist; `after/103-08/leak.json` exists.
- Acceptance: `HaveCount(18)` appears in the registry tests; the access theory has 18 rows and the drift fact is a theory over the same 18 rows.
