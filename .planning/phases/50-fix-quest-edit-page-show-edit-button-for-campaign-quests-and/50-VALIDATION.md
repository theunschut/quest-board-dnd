---
phase: 50
slug: fix-quest-edit-page-show-edit-button-for-campaign-quests-and
status: approved
nyquist_compliant: true
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
| 50-01-T1 | 01 | 1 | D-01/D-02/D-03 | — | Wave-0 failing tests: `CampaignManage_Desktop_RendersEditQuestLink`, `CampaignManage_Desktop_RendersDeleteLinkWiredToDeleteQuest` | integration | `dotnet test --filter "FullyQualifiedName~QuestCampaignUiParity"` | ✅ | ⬜ pending |
| 50-01-T2 | 01 | 1 | D-04/D-05 | — | Wave-0 tests: `CampaignEdit_Desktop_HidesFourOneShotFields`, `OneShotEdit_Desktop_ShowsFourFields`, `CampaignEdit_InvalidModelState_Returns200_DoesNotThrow`, `CampaignEdit_Mobile_HidesFourOneShotFields` | integration | `dotnet test --filter "FullyQualifiedName~QuestCampaignUiParity"` | ✅ | ⬜ pending |
| 50-02-T1 | 02 | 2 | D-01/D-02/D-03 | T-50-01 | Desktop Campaign Manage row renders Edit Quest (`btn-primary`) before Close/Reopen and Delete (`btn-danger`, `deleteQuest(id)`) after | integration | `dotnet test --filter "FullyQualifiedName~CampaignManage_Desktop"` | ✅ | ⬜ pending |
| 50-02-T2 | 02 | 2 | D-01/D-03 mobile | T-50-01 | Mobile Campaign Manage row renders Edit Quest (`btn-secondary` per Pitfall 1) and Delete Quest (`deleteQuest(id)`) | integration | `dotnet test --filter "FullyQualifiedName~CampaignManage_Mobile"` | ✅ | ⬜ pending |
| 50-03-T1 | 03 | 2 | D-05 | T-50-02, T-50-03 | `Edit` GET sets `ViewBag.BoardType`; `Edit` POST resolves `boardType` before `ModelState` check and sets `ViewBag.BoardType` on the validation-failure path (Pitfall 3 fix) | integration | `dotnet test --filter "FullyQualifiedName~CampaignEdit_InvalidModelState"` stays green | ✅ | ⬜ pending |
| 50-03-T2 | 03 | 2 | D-04 | — | `Edit.cshtml`/`Edit.Mobile.cshtml` hide CR/PlayerCount/DMSession/ProposedDates for Campaign, show for OneShot; desktop tips sidebar unchanged (D-06); mobile `HasExistingSignups` banner deliberately left ungated (Pitfall 2) | integration | `dotnet test --filter "FullyQualifiedName~QuestCampaignUiParity"` full class green | ✅ | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

*D-06 (sidebar tips left unchanged) requires no test — explicit non-change, verified as an acceptance criterion of 50-03-T2 rather than a standalone test.*

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

- [x] All tasks have `<automated>` verify or Wave 0 dependencies
- [x] Sampling continuity: no 3 consecutive tasks without automated verify
- [x] Wave 0 covers all MISSING references (50-01 authors the full failing-test baseline)
- [x] No watch-mode flags
- [x] Feedback latency < 60s
- [x] `nyquist_compliant: true` set in frontmatter

**Approval:** approved 2026-07-05 — plan-checker VERIFICATION PASSED, all D-01 through D-06 covered
