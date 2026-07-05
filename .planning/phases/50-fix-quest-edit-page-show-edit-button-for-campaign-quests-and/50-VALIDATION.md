---
phase: 50
slug: fix-quest-edit-page-show-edit-button-for-campaign-quests-and
status: draft
nyquist_compliant: false
wave_0_complete: false
created: 2026-07-05
---

# Phase 50 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit v3 (`TestContext.Current.CancellationToken`) + `Microsoft.AspNetCore.Mvc.Testing` (`WebApplicationFactory`) + FluentAssertions (`.Should()`) |
| **Config file** | `QuestBoard.IntegrationTests/QuestBoard.IntegrationTests.csproj`, base fixture at `QuestBoard.IntegrationTests/Helpers/WebApplicationFactoryBase.cs` |
| **Quick run command** | `dotnet test QuestBoard.IntegrationTests --filter "FullyQualifiedName~QuestController|FullyQualifiedName~QuestManage|FullyQualifiedName~QuestEdit"` |
| **Full suite command** | `dotnet test` |
| **Estimated runtime** | ~30-60 seconds (quick), full suite per existing project baseline |

---

## Sampling Rate

- **After every task commit:** Run `dotnet test QuestBoard.IntegrationTests --filter "FullyQualifiedName~QuestController|FullyQualifiedName~QuestManage|FullyQualifiedName~QuestEdit"`
- **After every plan wave:** Run `dotnet test` (full suite)
- **Before `/gsd-verify-work`:** Full suite must be green
- **Max feedback latency:** ~60 seconds (existing integration test infra, no new tooling)

---

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Threat Ref | Secure Behavior | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|------------|-----------------|-----------|-------------------|-------------|--------|
| 50-01-xx | TBD | TBD | D-01/D-02 | — | Campaign Manage page renders "Edit Quest" link before Close/Reopen | integration | `dotnet test --filter "FullyQualifiedName~QuestManage"` asserting `content.Should().Contain("Edit Quest")` on Campaign-board GET `/Quest/Manage/{id}` | ❌ W0 | ⬜ pending |
| 50-01-xx | TBD | TBD | D-03 | — | Campaign Manage page renders "Delete"/"Delete Quest" link after Close/Reopen, wired to `deleteQuest(id)` | integration | same test class, assert `content.Should().Contain("deleteQuest(")` and `Contain("Delete")` | ❌ W0 | ⬜ pending |
| 50-02-xx | TBD | TBD | D-04 | — | Edit page hides CR/PlayerCount/DMSession/ProposedDates for Campaign quests; shows them for OneShot | integration | GET `/Quest/Edit/{id}`: `NotContain(...)` for Campaign, `Contain(...)` for OneShot (regression check) | ❌ W0 | ⬜ pending |
| 50-02-xx | TBD | TBD | D-05 | — | `Edit` GET sets `ViewBag.BoardType`; `Edit` POST validation-failure path also sets it (Pitfall 3 fix — prevents `InvalidCastException`) | integration | (a) GET Edit for Campaign quest returns 200; (b) POST Edit with invalid ModelState (e.g. empty Title) for a Campaign quest returns 200 with validation errors rendered, not an unhandled exception | ❌ W0 | ⬜ pending |
| 50-01-xx / 50-02-xx | TBD | TBD | Mobile parity | — | Same assertions repeated with `MobileUserAgent` header per `MobileViewsTests.cs` pattern | integration | Same test class(es), mobile-user-agent variant of each case above | ❌ W0 | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

*D-06 (sidebar tips left unchanged) requires no test — explicit non-change, not a behavior to verify.*

*Task IDs are TBD pending the planner's actual plan/task numbering — this table's rows map to CONTEXT.md decisions and must be reconciled with real task IDs once PLAN.md files exist.*

---

## Wave 0 Requirements

- [ ] New integration test file (or extend `QuestCloseTests.cs` / `QuestControllerIntegrationTests_Comprehensive.cs`) covering: Campaign Manage page shows Edit+Delete; Edit page hides/shows the 4 fields correctly by board type; Edit POST validation-failure path doesn't throw for Campaign quests (Pitfall 3 regression guard) — covers D-01 through D-05
- [ ] Confirm whether a `SeedOneShotGroupAsync` (or equivalent existing OneShot-default-group fixture) already exists in `TestDataHelper.cs` for the regression-check side of D-04's test map row — if the default seeded group is already OneShot-type, no new helper is needed; verify during Wave 0 rather than assuming
- [ ] Mobile-user-agent variants of the above tests, following the `MobileViewsTests.cs` `GetWithUserAgentAsync` helper pattern

---

## Manual-Only Verifications

*All phase behaviors have automated verification — per RESEARCH.md's Validation Architecture section, every acceptance criterion (button presence/absence, field presence/absence, exception-free POST on invalid ModelState) is source-verifiable via `HttpClient` + `content.Should().Contain(...)`/`NotContain(...)` against rendered HTML, using existing `TestDataHelper.SeedCampaignGroupAsync` and `MobileViewsTests.cs`'s user-agent pattern. No live-browser check is required for this phase.*

---

## Validation Sign-Off

- [ ] All tasks have `<automated>` verify or Wave 0 dependencies
- [ ] Sampling continuity: no 3 consecutive tasks without automated verify
- [ ] Wave 0 covers all MISSING references
- [ ] No watch-mode flags
- [ ] Feedback latency < 60s
- [ ] `nyquist_compliant: true` set in frontmatter

**Approval:** pending — planner must finalize task IDs before sign-off
