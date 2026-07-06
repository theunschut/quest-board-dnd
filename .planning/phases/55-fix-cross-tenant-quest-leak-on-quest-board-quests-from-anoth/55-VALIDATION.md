---
phase: 55
slug: fix-cross-tenant-quest-leak-on-quest-board-quests-from-anoth
status: draft
nyquist_compliant: false
wave_0_complete: false
created: 2026-07-06
---

# Phase 55 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit v3.2.2 [VERIFIED: QuestBoard.IntegrationTests.csproj] + `Microsoft.AspNetCore.Mvc.Testing` 10.0.9 `WebApplicationFactory`; `Microsoft.EntityFrameworkCore.InMemory` for unit-level filter tests |
| **Config file** | `QuestBoard.IntegrationTests/QuestBoard.IntegrationTests.csproj`, `QuestBoard.UnitTests/QuestBoard.UnitTests.csproj` (existing, no changes needed) |
| **Quick run command** | `dotnet test --filter FullyQualifiedName~GroupSessionMiddlewareIntegrationTests` / `~GroupPickerControllerIntegrationTests` / `~PlayerSignupRepositoryTests` |
| **Full suite command** | `dotnet test` |
| **Estimated runtime** | ~30-60 seconds (quick filter) / full suite varies |

---

## Sampling Rate

- **After every task commit:** Run the relevant filtered command from the Per-Task Verification Map below
- **After every plan wave:** Run `dotnet test` (full suite)
- **Before `/gsd-verify-work`:** Full suite must be green
- **Max feedback latency:** 60 seconds

---

## Per-Task Verification Map

*Pre-planning draft — rows keyed by CONTEXT.md decision ID until PLAN.md assigns concrete task IDs. The planner/plan-checker should replace the "Decision" column with actual Task IDs once plans exist.*

| Decision | Behavior | Test Type | Automated Command | File Exists | Status |
|----------|----------|-----------|-------------------|-------------|--------|
| D-01/D-02 | SuperAdmin with null `ActiveGroupId` is now redirected/409'd on group-scoped routes, still exempt on `/platform`/`/Error`/GroupPicker/Account | integration | `dotnet test --filter FullyQualifiedName~GroupSessionMiddlewareIntegrationTests` | Partial — existing `SuperAdmin_NoActiveGroup_NotRedirectedByMiddleware` (lines 45-66) must be REWRITTEN to assert the opposite behavior; existing `[Theory]` route list should gain a SuperAdmin-authenticated variant | ⬜ pending |
| D-03 | Fail-closed filter on all 7 entities (Quest, ShopItem, ProposedDate, PlayerDateVote, PlayerSignup, ReminderLog, UserTransaction — expanded from CONTEXT.md's 5 per RESEARCH.md's grep) returns zero rows (not all-groups rows) when `ActiveGroupId` is null | unit | `dotnet test --filter FullyQualifiedName~QuestBoardContextFilterTests` (new) | ❌ Wave 0 — no existing test asserts the fail-closed shape for any of the 7 entities with a null `ActiveGroupId`; `TestActiveGroupContext`/`MutableTestGroupContext` pattern already exists to build these on | ⬜ pending |
| D-04/D-05 | `SelectGroup` POST with a non-member `groupId` returns 404; member `groupId` still succeeds | integration | `dotnet test --filter FullyQualifiedName~GroupPickerControllerIntegrationTests` | ❌ Wave 0 — no existing test posts a `groupId` the caller isn't a member of; existing `SelectGroup_ShouldPersistActiveGroupInSession` test only covers the happy path | ⬜ pending |
| D-06 | Session's `ActiveGroupId` membership re-validated after the interval elapses; removed member is gated out | unit (middleware-level) | new test method against `GroupSessionMiddleware.InvokeAsync` with a fake `HttpContext`/session | ❌ Wave 0 — new test needed; prefer a direct unit test over an integration test given the test harness's session-cookie round-trip limitation | ⬜ pending |

*D-00 and D-07 are investigated-and-ruled-out decisions with no code change (confirmed in 55-CONTEXT.md and 55-RESEARCH.md) — they have no row here by design.*

---

## Wave 0 Requirements

- [ ] `QuestBoard.IntegrationTests/Controllers/GroupSessionMiddlewareIntegrationTests.cs` — rewrite `SuperAdmin_NoActiveGroup_NotRedirectedByMiddleware` to assert the new (opposite) behavior; add SuperAdmin coverage to the existing `[Theory]` protected-route list
- [ ] `QuestBoard.UnitTests/Repository/` — new test file (e.g. `QuestBoardContextFilterTests.cs`) asserting all 7 hardened entity filters return zero rows for a null `ActiveGroupId`, mirroring `PlayerSignupRepositoryTests.cs`'s `TestActiveGroupContext`/`MutableTestGroupContext` pattern
- [ ] `QuestBoard.IntegrationTests/Controllers/GroupPickerControllerIntegrationTests.cs` — new test: authenticated non-member user posts `SelectGroup` for a group they don't belong to, asserts 404
- [ ] New unit test for `GroupSessionMiddleware`'s D-06 re-validation block, constructing a fake `HttpContext` with a controlled session timestamp

---

## Manual-Only Verifications

*None — all phase behaviors have automated verification via the unit/integration test map above.*

---

## Validation Sign-Off

- [ ] All tasks have `<automated>` verify or Wave 0 dependencies
- [ ] Sampling continuity: no 3 consecutive tasks without automated verify
- [ ] Wave 0 covers all MISSING references
- [ ] No watch-mode flags
- [ ] Feedback latency < 60s
- [ ] `nyquist_compliant: true` set in frontmatter

**Approval:** pending
