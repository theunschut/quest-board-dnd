---
phase: 50-fix-quest-edit-page-show-edit-button-for-campaign-quests-and
plan: 01
subsystem: integration-tests
tags: [testing, tdd-wave-0, campaign-board-type, quest-edit, quest-manage]
dependency-graph:
  requires: []
  provides:
    - "QuestCampaignUiParityTests test class (D-01 through D-05 wave-0 red/green baseline)"
  affects:
    - "QuestBoard.Service/Views/Quest/Manage.cshtml (Plan 02 target)"
    - "QuestBoard.Service/Views/Quest/Manage.Mobile.cshtml (Plan 02 target)"
    - "QuestBoard.Service/Views/Quest/Edit.cshtml (Plan 03 target)"
    - "QuestBoard.Service/Views/Quest/Edit.Mobile.cshtml (Plan 03 target)"
    - "QuestBoard.Service/Controllers/QuestBoard/QuestController.cs Edit GET/POST (Plan 03 target)"
tech-stack:
  added: []
  patterns:
    - "HttpClient + FluentAssertions Contain/NotContain against rendered HTML for board-type-conditional markup"
    - "AntiForgeryHelper.ExtractAntiForgeryTokenAsync + CreateFormContentWithAntiForgeryToken for POST-with-invalid-ModelState regression guard"
key-files:
  created:
    - QuestBoard.IntegrationTests/Controllers/QuestCampaignUiParityTests.cs
  modified: []
decisions:
  - "Both plan tasks (Manage-page tests and Edit-page tests) were authored as a single Write pass into one new file and committed as a single test(50-01) commit, since Task 2 only extends the class Task 1 created and splitting the same file into two artificial commits would not reflect a real intermediate compilable state"
metrics:
  duration: "~25 minutes"
  completed: 2026-07-05
status: complete
---

# Phase 50 Plan 01: Wave-0 Campaign UI-Parity Failing Tests Summary

Authored `QuestCampaignUiParityTests.cs`, a new xUnit integration test class of 7 `[Fact]` tests that pins every observable behavior D-01 through D-05 require (Manage-page Edit/Delete affordances for Campaign quests, Edit-page board-type field visibility, and the Edit-POST invalid-ModelState regression guard for Pitfall 3) — establishing the red/green baseline Plans 02 and 03 must turn green while keeping the two already-passing regression guards green throughout.

## What Was Built

- `QuestBoard.IntegrationTests/Controllers/QuestCampaignUiParityTests.cs` — new test class `QuestCampaignUiParityTests(WebApplicationFactoryBase factory) : IClassFixture<WebApplicationFactoryBase>`, following `QuestCloseTests.cs`'s structural pattern (primary-constructor factory, copied private `AddCampaignGroupMembershipAsync` helper, `SeedCampaignGroupAsync(factory.Services, groupId: 2)`, `factory.TestGroupContext.ActiveGroupId = 2` inside try/finally).

### Test methods created (7 total)

| Test | Requirement | Result at wave-0 close |
|------|-------------|------------------------|
| `CampaignManage_Desktop_RendersEditQuestLink` | D-01/D-02 | FAILED (correct — no Edit link on Campaign Manage row today) |
| `CampaignManage_Desktop_RendersDeleteLinkWiredToDeleteQuest` | D-03 | FAILED (correct — no `deleteQuest(id)` call on Campaign Manage row today) |
| `CampaignManage_Mobile_RendersEditQuestAndDeleteQuestLinks` | D-01/D-03 mobile | FAILED (correct — same gap, mobile view) |
| `CampaignEdit_Desktop_HidesFourOneShotFields` | D-04 | FAILED (correct — Edit.cshtml renders all 4 fields unconditionally today) |
| `OneShotEdit_Desktop_ShowsFourFields` | D-04 regression | PASSED (correct — OneShot Edit already shows all 4 fields; must stay green through Plan 03) |
| `CampaignEdit_InvalidModelState_Returns200_DoesNotThrow` | D-05 / Pitfall 3 | PASSED (correct — today's Edit POST works; this is the regression guard Plan 03 must keep green after adding `ViewBag.BoardType`) |
| `CampaignEdit_Mobile_HidesFourOneShotFields` | D-04 mobile | FAILED (correct — same gap, mobile view) |

This matches the plan's `<verification>` section exactly: 5 tests FAIL by assertion (the wave-0 red target for Plans 02/03), and 2 tests PASS (the regression guards those plans must not break).

## Verification

```
dotnet build QuestBoard.IntegrationTests   -> Build succeeded, 0 errors
dotnet test QuestBoard.IntegrationTests --filter "FullyQualifiedName~QuestCampaignUiParity"
  -> Total: 7, Passed: 2, Failed: 5 (exact expected wave-0 state)
```

No production `.cshtml` or `.cs` file under `QuestBoard.Service/` was modified — test-only plan, as required.

## Deviations from Plan

### Auto-fixed Issues

None — plan executed as written; no bugs, missing functionality, or blocking issues encountered.

### Structural note (not a deviation from behavior, just from task/commit granularity)

**Both tasks landed in a single commit.** Task 1 (Manage-page tests) and Task 2 (Edit-page tests) both target the same single new file (`QuestCampaignUiParityTests.cs`). The plan's own `<files_modified>` frontmatter lists exactly one file for the whole plan. Rather than committing an artificially incomplete/uncompilable intermediate state (Task 1's tests alone, before the `AddCampaignGroupMembershipAsync` extension point Task 2 also reads), the full file was authored and verified in one pass, then committed as one `test(50-01)` commit. Both tasks' acceptance criteria and verify commands were independently confirmed against the final file — the outcome is identical to a two-commit sequence, just delivered as one atomic, always-compilable commit.

## Known Stubs

None — this plan is test-only; no UI or service code was stubbed or wired to empty data.

## Threat Flags

None — no new endpoints, auth paths, or trust-boundary changes. This plan only adds `HttpClient`-driven read/POST assertions against existing, already-authorized routes (`/Quest/Manage/{id}`, `/Quest/Edit/{id}`).

## Self-Check: PASSED

- FOUND: `QuestBoard.IntegrationTests/Controllers/QuestCampaignUiParityTests.cs`
- FOUND: commit `d24e8df` (test(50-01): author failing Campaign UI-parity integration tests)
- Test run confirmed: 7 total, 2 passed, 5 failed — matches plan's expected wave-0 state exactly
