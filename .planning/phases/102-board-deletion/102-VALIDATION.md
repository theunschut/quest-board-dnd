---
phase: 102
slug: board-deletion
# status lifecycle: draft (seeded by plan-phase) → validated (set by validate-phase §6)
status: draft
nyquist_compliant: false
wave_0_complete: false
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
| Every board-scoped type removed (quests, proposed dates, votes, signups, reminder logs, events, event signups, series, contacts, notes, categories, tags, characters, classes, shop items, transactions incl. chained returns, group row) | D-01/02/03 | unit (InMemory) | `dotnet test QuestBoard.UnitTests --filter "FullyQualifiedName~GroupRepositoryBoardDeletionTests"` | ❌ W0 | ⬜ pending |
| Another board's full graph unchanged; works with active board = other board and = null | D-01 | unit | same | ❌ W0 | ⬜ pending |
| Users, DM profiles + images, trade items, calendar subscriptions survive | D-04 | unit | same | ❌ W0 | ⬜ pending |
| Other-board follow-up `OriginalQuestId` and signup `CharacterId` cleared, rows kept; defensive event `SeriesId`/`SeriesSlotIndex` cleared | D-05 | unit | same | ❌ W0 | ⬜ pending |
| Linked-board set remains, board gone | D-06 | unit | same | ❌ W0 | ⬜ pending |
| Members present → refused, nothing deleted; unknown id → not found | D-09 | unit | same | ❌ W0 | ⬜ pending |
| Exactly one `SaveChanges`; save failure clears tracker and rethrows | D-11 | unit | same | ❌ W0 | ⬜ pending |
| FK-integrity sweep: no dangling non-cascade FK after delete | D-11 | unit | same | ❌ W0 | ⬜ pending |
| Model pin: FKs referencing `GroupEntity` + non-cascade FKs of board-owned tables equal the handled list | D-03 | unit | `dotnet test QuestBoard.UnitTests --filter "FullyQualifiedName~BoardOwnedForeignKeys"` | ❌ W0 | ⬜ pending |
| Preview parity: counts equal rows removed; zero-count types absent; names for named types | D-07 | unit | `GroupRepositoryBoardDeletionTests` filter | ❌ W0 | ⬜ pending |
| Wrong/missing typed name → mismatch, nothing deleted | D-08 | unit (service) | `dotnet test QuestBoard.UnitTests --filter "FullyQualifiedName~GroupServiceBoardDeletion"` | ❌ W0 | ⬜ pending |
| Controller: exception → `TempData["Error"]` + redirect, never 500; mismatch re-renders; success → Index + `TempData["Success"]`; own session cleared | D-10 | unit (controller) | `dotnet test QuestBoard.UnitTests --filter "FullyQualifiedName~GroupControllerBoardDeletion"` | ❌ W0 | ⬜ pending |
| Middleware: super admin with deleted active board → keys cleared; existing board passes | stale-board | unit | `dotnet test QuestBoard.UnitTests --filter "FullyQualifiedName~GroupSessionMiddlewareRevalidationTests"` | ✅ edit + add | ⬜ pending |
| GET Delete renders counts, collapsed names, unlink section, typed-name input (desktop + phone UA); POST right name deletes; wrong name keeps board; members → refused | D-07/D-10 | integration | `dotnet test QuestBoard.IntegrationTests --filter "FullyQualifiedName~PlatformBoardDelete"` | ❌ W0 | ⬜ pending |
| Existing `DeleteGroup_WhenEmpty_PostShouldRedirectToIndex` updated to post the name and assert row gone | D-08 | integration | `dotnet test QuestBoard.IntegrationTests --filter "FullyQualifiedName~GroupManagementIntegrationTests"` | ✅ edit | ⬜ pending |
| `/platform/Users` desktop + phone show board count and names; zero boards → 0 | D-12/D-13 | integration | `dotnet test QuestBoard.IntegrationTests --filter "FullyQualifiedName~UsersControllerIntegrationTests"` | ✅ extend | ⬜ pending |
| Real SQL Server accepts full-board delete and rolls back on failure | D-11 | integration (gated) | `QUESTBOARD_MIGRATOR_TEST_CONNECTION=... dotnet test QuestBoard.IntegrationTests --filter "FullyQualifiedName~BoardDeletionSqlServer"` | ❌ W0 (skips when unset) | ⬜ pending |
| No inline style/script/handler in new Delete views | UI | integration (static) | `dotnet test QuestBoard.IntegrationTests --filter "FullyQualifiedName~UiFoundation"` | ✅ + new fact ❌ | ⬜ pending |
| Guards stay green: `FeedRevisionWriteSeamTests`, `CrossBoardIgnoreQueryFiltersSeamTests`, `AmbientClockSeamTests`, `BoardMoveRepositorySingleSaveTests` | arch | unit | `dotnet test QuestBoard.UnitTests --filter "FullyQualifiedName~Architecture"` | ✅ | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

- [ ] `QuestBoard.UnitTests/Repository/GroupRepositoryBoardDeletionTests.cs` — behaviour tests + FK-integrity sweep helper
- [ ] `QuestBoard.UnitTests/Repository/BoardOwnedForeignKeysTests.cs` — model-pin test
- [ ] `QuestBoard.UnitTests/Services/GroupServiceBoardDeletionTests.cs`
- [ ] `QuestBoard.UnitTests/Controllers/GroupControllerBoardDeletionTests.cs`
- [ ] `QuestBoard.IntegrationTests/Controllers/PlatformBoardDeleteTests.cs` (+ Users-column facts, static guard fact for the Delete views)
- [ ] `QuestBoard.IntegrationTests/Tests/BoardDeletionSqlServerTests.cs` — gated on `QUESTBOARD_MIGRATOR_TEST_CONNECTION`
- Framework install: none

---

## Manual-Only Verifications

| Behavior | Decision | Why Manual | Test Instructions |
|----------|----------|------------|-------------------|
| Real delete on the local SQL Server leaves no 500 and the other board intact | D-10/D-11 | The gated SQL test is optional in CI; the phase gate wants one pass against the user's own DB | Create a throwaway board with a quest + signup + vote, a character used by a signup on another board, an event + series, a contact with image and tag, a shop item with a purchase and a return; empty its members; delete via Platform; confirm board gone, other board intact, selected-board session recovers, desktop and phone UA both render |

---

## Validation Sign-Off

- [ ] All tasks have `<automated>` verify or Wave 0 dependencies
- [ ] Sampling continuity: no 3 consecutive tasks without automated verify
- [ ] Wave 0 covers all MISSING references
- [ ] No watch-mode flags
- [ ] Feedback latency < 180s
- [ ] `nyquist_compliant: true` set in frontmatter

**Approval:** pending
