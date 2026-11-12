---
phase: 102
slug: board-deletion
# status lifecycle: draft (seeded by plan-phase) → validated (set by validate-phase §6)
status: validated
nyquist_compliant: true
wave_0_complete: true
created: 2026-11-11
---

# Phase 102 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.
> Derived from `102-RESEARCH.md` → `## Validation Architecture`.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit v3 3.2.2, FluentAssertions 8.10.0, NSubstitute 5.3.0, EF Core InMemory 10.0.9 |
| **Config file** | `QuestBoard.IntegrationTests/xunit.runner.json` (no parallelism) |
| **Quick run command** | `dotnet test QuestBoard.UnitTests --filter "FullyQualifiedName~BoardDeletion"` |
| **Full suite command** | `dotnet test` |
| **Estimated runtime** | ~180 seconds (full), ~30 seconds (filtered) |

Never set `DOTNET_GCHeapHardLimit`; a `Fatal error. Internal CLR error. (0x80131506)` is a flake — re-run once. A locked-file build failure means the debugger is running.

---

## Sampling Rate

- **After every task commit:** the filtered command for the layer touched (`BoardDeletion` unit filter; `PlatformBoardDelete` integration filter)
- **After every plan wave:** `dotnet test QuestBoard.UnitTests` then `dotnet test QuestBoard.IntegrationTests`
- **Before `/gsd-verify-work`:** full suite green
- **Max feedback latency:** 180 seconds

---

## Per-Task Verification Map

Task IDs are filled in by the plans; behaviours and commands below are the contract each plan's `<verify>` must point at.

| Behaviour | Decision | Test Type | Automated Command | File Exists | Status |
|-----------|----------|-----------|-------------------|-------------|--------|
| Every board-scoped type removed (quests, proposed dates, votes, signups, reminder logs, events, event signups, series, contacts, notes, categories, tags, characters, classes, shop items, transactions incl. chained returns, group row) | D-01/02/03 | unit (InMemory) | `dotnet test QuestBoard.UnitTests --filter "FullyQualifiedName~GroupRepositoryBoardDeletionTests"` | ✅ | ✅ green |
| Another board's full graph unchanged; works with active board = other board and = null | D-01 | unit | same | ✅ | ✅ green |
| Users, DM profiles + images, trade items, calendar subscriptions survive | D-04 | unit | same | ✅ | ✅ green |
| Other-board follow-up `OriginalQuestId` and signup `CharacterId` cleared, rows kept; defensive event `SeriesId`/`SeriesSlotIndex` cleared | D-05 | unit | same | ✅ | ✅ green |
| Linked-board set remains, board gone | D-06 | unit | same | ✅ | ✅ green |
| Members present → refused, nothing deleted; unknown id → not found | D-09 | unit | same | ✅ | ✅ green |
| Exactly one `SaveChanges`; save failure clears tracker and rethrows | D-11 | unit | same | ✅ | ✅ green |
| FK-integrity sweep: no dangling non-cascade FK after delete | D-11 | unit | same | ✅ | ✅ green |
| Model pin: FKs referencing `GroupEntity` + non-cascade FKs of board-owned tables equal the handled list | D-03 | unit | `dotnet test QuestBoard.UnitTests --filter "FullyQualifiedName~BoardOwnedForeignKeys"` | ✅ | ✅ green |
| Preview parity: counts equal rows removed; zero-count types absent; names for named types | D-07 | unit | `GroupRepositoryBoardDeletionTests` filter | ✅ | ✅ green |
| Wrong/missing typed name → mismatch, nothing deleted | D-08 | unit (service) | `dotnet test QuestBoard.UnitTests --filter "FullyQualifiedName~GroupServiceBoardDeletion"` | ✅ | ✅ green |
| Controller: exception → `TempData["Error"]` + redirect, never 500; mismatch re-renders; success → Index + `TempData["Success"]`; own session cleared | D-10 | unit (controller) | `dotnet test QuestBoard.UnitTests --filter "FullyQualifiedName~GroupControllerBoardDeletion"` | ✅ | ✅ green |
| Middleware: super admin with deleted active board → keys cleared; existing board passes | stale-board | unit | `dotnet test QuestBoard.UnitTests --filter "FullyQualifiedName~GroupSessionMiddlewareRevalidationTests"` | ✅ | ✅ green |
| GET Delete renders counts, collapsed names, unlink section, typed-name input (desktop + phone UA); POST right name deletes; wrong name keeps board; members → refused | D-07/D-10 | integration | `dotnet test QuestBoard.IntegrationTests --filter "FullyQualifiedName~PlatformBoardDelete"` | ✅ | ✅ green |
| Existing `DeleteGroup_WhenEmpty_PostShouldRedirectToIndex` updated to post the name and assert row gone | D-08 | integration | `dotnet test QuestBoard.IntegrationTests --filter "FullyQualifiedName~GroupManagementIntegrationTests"` | ✅ | ✅ green |
| `/platform/Users` desktop + phone show board count and names; zero boards → 0 | D-12/D-13 | integration | `dotnet test QuestBoard.IntegrationTests --filter "FullyQualifiedName~UsersControllerIntegrationTests"` | ✅ | ✅ green |
| Real SQL Server accepts full-board delete and rolls back on failure | D-11 | integration (gated) | `QUESTBOARD_MIGRATOR_TEST_CONNECTION=... dotnet test QuestBoard.IntegrationTests --filter "FullyQualifiedName~BoardDeletionSqlServer"` | ✅ (3 facts; run with the connection set) | ✅ green |
| No inline style/script/handler in new Delete views | UI | integration (static) | `dotnet test QuestBoard.IntegrationTests --filter "FullyQualifiedName~UiFoundation"` | ✅ (+ BoardDeletionStaticGuardTests) | ✅ green |
| Guards stay green: `FeedRevisionWriteSeamTests`, `CrossBoardIgnoreQueryFiltersSeamTests`, `AmbientClockSeamTests`, `BoardMoveRepositorySingleSaveTests` | arch | unit | `dotnet test QuestBoard.UnitTests --filter "FullyQualifiedName~Architecture"` | ✅ | ✅ green |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

- [x] `QuestBoard.UnitTests/Repository/GroupRepositoryBoardDeletionTests.cs` — behaviour tests + FK-integrity sweep helper
- [x] `QuestBoard.UnitTests/Repository/BoardOwnedForeignKeysTests.cs` — model-pin test
- [x] `QuestBoard.UnitTests/Services/GroupServiceBoardDeletionTests.cs`
- [x] `QuestBoard.UnitTests/Controllers/GroupControllerBoardDeletionTests.cs`
- [x] `QuestBoard.IntegrationTests/Controllers/PlatformBoardDeleteTests.cs` (+ Users-column facts, static guard fact for the Delete views)
- [x] `QuestBoard.IntegrationTests/Tests/BoardDeletionSqlServerTests.cs` — gated on `QUESTBOARD_MIGRATOR_TEST_CONNECTION`
- Framework install: none

---

## Manual-Only Verifications

| Behavior | Decision | Why Manual | Test Instructions |
|----------|----------|------------|-------------------|
| Real delete on the local SQL Server leaves no 500 and the other board intact | D-10/D-11 | The gated SQL test is optional in CI; the phase gate wants one pass against the user's own DB | Create a throwaway board with a quest + signup + vote, a character used by a signup on another board, an event + series, a contact with image and tag, a shop item with a purchase and a return; empty its members; delete via Platform; confirm board gone, other board intact, selected-board session recovers, desktop and phone UA both render |

---

## Validation Sign-Off

- [x] All tasks have `<automated>` verify or Wave 0 dependencies
- [x] Sampling continuity: no 3 consecutive tasks without automated verify
- [x] Wave 0 covers all MISSING references
- [x] No watch-mode flags
- [x] Feedback latency < 180s
- [x] `nyquist_compliant: true` set in frontmatter

**Approval:** validated 2026-11-11 (automated map fully green; the manual-only row is tracked in `102-UAT.md`)

## Validation Audit 2026-11-11

Every behaviour in the map was re-run by filter at HEAD `34ef3589` (after the code review fixes); all green.
SQL Server-gated facts ran against the local server with `QUESTBOARD_MIGRATOR_TEST_REQUIRED=1` (3 passed,
0 skipped: full delete, rollback, and the member-race added by the WR-01 fix). The D-07 "zero-count types
absent" behaviour is pinned by `PlatformBoardDeleteTests` (no "0 events" row) and `BoardDeletePageViewModelTests`.

| Metric | Count |
|--------|-------|
| Gaps found | 0 |
| Resolved | 0 |
| Escalated | 0 |
| Manual-only | 1 (real delete on the user's SQL Server through the UI — `102-UAT.md`) |
