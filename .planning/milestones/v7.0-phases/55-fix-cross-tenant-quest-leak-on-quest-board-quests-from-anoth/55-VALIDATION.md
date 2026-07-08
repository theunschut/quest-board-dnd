---
phase: 55
slug: fix-cross-tenant-quest-leak-on-quest-board-quests-from-anoth
status: final
nyquist_compliant: true
wave_0_complete: true
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

| Task ID | Plan | Wave | Decision | Secure Behavior | Test Type | Automated Command | File Exists | Status |
|---------|------|------|----------|-----------------|-----------|-------------------|-------------|--------|
| 55-01 Task 1 | 01 | 1 | D-03 | Wave-0 RED test: queries for all 7 group-scoped entities assert zero rows when `ActiveGroupId` is null | unit | `dotnet test QuestBoard.UnitTests/QuestBoard.UnitTests.csproj --filter FullyQualifiedName~QuestBoardContextFilterTests` | ✅ new file created by this task | ⬜ pending |
| 55-01 Task 2 | 01 | 1 | D-03 | `HasQueryFilter` hardened to fail-closed (`ActiveGroupId != null && ...`) on Quest/ShopItem/ProposedDate/PlayerDateVote/PlayerSignup/ReminderLog/UserTransaction; `DailyReminderJob`'s `IgnoreQueryFilters` sweep unaffected | unit | `dotnet test QuestBoard.UnitTests/QuestBoard.UnitTests.csproj --filter FullyQualifiedName~QuestBoardContextFilterTests` | ✅ modifies existing file | ⬜ pending |
| 55-02 Task 1 | 02 | 1 | D-01/D-02 | `SuperAdmin_NoActiveGroup_NotRedirectedByMiddleware` rewritten to `SuperAdmin_NoActiveGroup_RedirectsToGroupPick` (inverted assertion); exempt-path check reordered before the SuperAdmin bypass | integration | `dotnet test QuestBoard.IntegrationTests/QuestBoard.IntegrationTests.csproj --filter FullyQualifiedName~GroupSessionMiddlewareIntegrationTests` | ✅ existing file — rewritten | ⬜ pending |
| 55-02 Task 2 | 02 | 1 | D-01/D-02 | `/platform`, `/Error`, GroupPicker, Account routes stay exempt for SuperAdmin; stale `CONCERNS.md` SuperAdmin-sees-all rationale corrected | integration | `dotnet test QuestBoard.IntegrationTests/QuestBoard.IntegrationTests.csproj --filter FullyQualifiedName~GroupSessionMiddlewareIntegrationTests` | ✅ modifies existing files | ⬜ pending |
| 55-03 Task 1 | 03 | 1 | D-04/D-05 | Wave-0 RED test: authenticated non-member posts `SelectGroup` for a foreign `groupId`, expects 404 | integration | `dotnet test QuestBoard.IntegrationTests/QuestBoard.IntegrationTests.csproj --filter FullyQualifiedName~GroupPickerControllerIntegrationTests` | ✅ existing file — extended | ⬜ pending |
| 55-03 Task 2 | 03 | 1 | D-04/D-05 | `SelectGroup` gains `IUserService.GetGroupRoleByIdAsync` membership check; 404 for non-member, unchanged success for member, SuperAdmin bypass preserved | integration | `dotnet test QuestBoard.IntegrationTests/QuestBoard.IntegrationTests.csproj --filter FullyQualifiedName~GroupPickerControllerIntegrationTests` | ✅ modifies existing file | ⬜ pending |
| 55-04 Task 1 | 04 | 2 | D-06 | `SessionKeys` gains a last-verified timestamp key; build only (scaffolding) | build | `dotnet build QuestBoard.Service/QuestBoard.Service.csproj` | ✅ modifies existing file | ⬜ pending |
| 55-04 Task 2 | 04 | 2 | D-06 | Wave-0 RED test: `GroupSessionMiddleware.InvokeAsync` re-checks membership after the interval elapses via a fake `HttpContext`/session, gates out a removed member | unit | `dotnet test QuestBoard.UnitTests/QuestBoard.UnitTests.csproj --filter FullyQualifiedName~GroupSessionMiddlewareRevalidationTests` | ✅ new file created by this task | ⬜ pending |
| 55-04 Task 3 | 04 | 2 | D-06 | Interval-gated re-validation wired into `GroupSessionMiddleware` and `GroupPickerController` (stamps the timestamp on selection); reuses Phase 41's 5-minute interval value | unit | `dotnet test QuestBoard.UnitTests/QuestBoard.UnitTests.csproj --filter FullyQualifiedName~GroupSessionMiddlewareRevalidationTests` | ✅ modifies existing files | ⬜ pending |

*D-00 and D-07 are investigated-and-ruled-out decisions with no code change (confirmed in 55-CONTEXT.md and 55-RESEARCH.md) — they have no row here by design.*

---

## Wave 0 Requirements

- [x] `QuestBoard.IntegrationTests/Controllers/GroupSessionMiddlewareIntegrationTests.cs` — rewritten by 55-02 Task 1; asserts the new (opposite) behavior
- [x] `QuestBoard.UnitTests/Repository/QuestBoardContextFilterTests.cs` — new file, assigned to 55-01 Task 1; covers D-03's 7 hardened entity filters
- [x] `QuestBoard.IntegrationTests/Controllers/GroupPickerControllerIntegrationTests.cs` — extended by 55-03 Task 1; non-member `SelectGroup` posts assert 404
- [x] `QuestBoard.UnitTests/.../GroupSessionMiddlewareRevalidationTests.cs` — new file, assigned to 55-04 Task 2; covers D-06's re-validation block

All Wave 0 gaps are assigned to a specific plan task in the finalized PLAN.md files — no outstanding scaffolding work remains unassigned.

---

## Manual-Only Verifications

*None — all phase behaviors have automated verification via the unit/integration test map above.*

---

## Validation Sign-Off

- [x] All tasks have `<automated>` verify or Wave 0 dependencies
- [x] Sampling continuity: no 3 consecutive tasks without automated verify
- [x] Wave 0 covers all MISSING references
- [x] No watch-mode flags
- [x] Feedback latency < 60s
- [x] `nyquist_compliant: true` set in frontmatter

**Approval:** approved 2026-07-06 (via gsd-plan-checker VERIFICATION PASSED)
