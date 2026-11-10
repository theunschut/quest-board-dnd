---
phase: "95"
slug: "quests-and-quest-log-ui-conversion"
# status lifecycle: draft (seeded by plan-phase) → validated (set by validate-phase §6)
status: draft
nyquist_compliant: false
wave_0_complete: false
created: "2026-11-10"
---

# Phase 95 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.
> Source: `95-RESEARCH.md` § "Validation Architecture".

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit v3 3.2.2 + FluentAssertions 8.10.0; `Microsoft.AspNetCore.Mvc.Testing` for integration; EF InMemory (no database) |
| **Config file** | `QuestBoard.IntegrationTests/xunit.runner.json` (`parallelizeAssembly: false`) |
| **Quick run command** | `dotnet test QuestBoard.IntegrationTests --filter "<per-slice expression>"` (see Per-slice sampling) |
| **Full suite command** | `dotnet test` |
| **Browser checks** | scratch Playwright under `~/qb-ui-review/phase-95/` (outside the repo), driving installed Edge via `channel: 'msedge'` |
| **Estimated runtime** | ~15 s per filtered integration run; full suite a few minutes |

---

## Sampling Rate

- **After every task commit:** run the slice's quick command (below)
- **After every plan wave:** run `dotnet test` (full suite)
- **Per slice:** scratch Playwright sweep on the touched pages, desktop and mobile User-Agent, compared against BEFORE (no new contrast failure, no hidden control, no new overflow or console error)
- **Before `/gsd-verify-work`:** full suite green, `dotnet build` 0 errors
- **Max feedback latency:** ~60 s

Transient: re-run once on CLR error `0x80131506`; `FinalizedQuestGameNight_NeverShowsTheUtcPlusTwoShiftedHour` flakes in the first minute of 20:00 UTC.

---

## Per-slice sampling

| Slice | Quick command (per task commit) |
|-------|----------------------------------|
| Editor / form scripts / modal | `dotnet test QuestBoard.IntegrationTests --filter "FullyQualifiedName~UiFoundation\|FullyQualifiedName~MobileViewsTests"` |
| Quest Details + vote patch | `dotnet test QuestBoard.IntegrationTests --filter "FullyQualifiedName~MobileViewsTests\|FullyQualifiedName~LinkedBoard\|FullyQualifiedName~BoardMoveControlTests\|FullyQualifiedName~QuestManageDmControlsTests\|FullyQualifiedName~WallClockUnmovedTests"` + `dotnet test QuestBoard.UnitTests --filter "FullyQualifiedName~AmbientClockSeamTests"` |
| Board | `dotnet test QuestBoard.IntegrationTests --filter "FullyQualifiedName~RowNavigationAccessibilityTests\|FullyQualifiedName~MobileViewsTests\|FullyQualifiedName~BoardPage"` + `dotnet test QuestBoard.UnitTests --filter "FullyQualifiedName~QuestPosterPickerTests"` |
| Forms | `dotnet test QuestBoard.IntegrationTests --filter "FullyQualifiedName~MobileViewsTests\|FullyQualifiedName~QuestCampaignUiParityTests\|FullyQualifiedName~QuestFinalizedEditTests\|FullyQualifiedName~UiFoundationViewLibraryGuardTests"` |
| Manage | `dotnet test QuestBoard.IntegrationTests --filter "FullyQualifiedName~QuestManageDmControlsTests\|FullyQualifiedName~MobileViewsTests\|FullyQualifiedName~BoardMoveControlTests"` |
| Quest Log | `dotnet test QuestBoard.IntegrationTests --filter "FullyQualifiedName~QuestLogControllerIntegrationTests\|FullyQualifiedName~MobileViewsTests\|FullyQualifiedName~WallClockUnmovedTests"` |
| Move | `dotnet test QuestBoard.IntegrationTests --filter "FullyQualifiedName~BoardMove\|FullyQualifiedName~BoardPageStaticGuardTests"` + `AmbientClockSeamTests` |
| Closure / gate | `dotnet test` and `dotnet build` |

---

## Per-Task Verification Map

Filled in by the plans; each task carries its own `<automated>` command and `<fails_when>`.

| Contract point | Behavior | Test type | Automated command | File exists |
|----------------|----------|-----------|-------------------|-------------|
| DoC 1 | no inline style/script/handler in converted views | static guard | `--filter "FullyQualifiedName~QuestsConversionGuardTests"` | ❌ W0 |
| DoC 2 | zero hex/rgb/`!important` in converted sheets | static guard | same | ❌ W0 |
| DoC 3 | page links its own sheet; foreign consumers link theirs | static guard | same | ❌ W0 |
| D-05 | poster pick stable per id, fits, long titles to tall posters | unit | `dotnet test QuestBoard.UnitTests --filter "FullyQualifiedName~QuestPosterPickerTests"` | ❌ W0 |
| D-12/D-13 | editor help text + id wiring; lib files still load | rendered HTML + guard | `--filter "FullyQualifiedName~UiFoundationViewLibraryGuardTests\|FullyQualifiedName~MarkdownEditorMarkupTests"` | guard ✅ / markup ❌ W0 |
| D-14/D-15 | phone signup posts CharacterId + selectedRole; vote marker, DM Controls untouched | integration | `--filter "FullyQualifiedName~MobileViewsTests\|FullyQualifiedName~QuestDetailsSignupFormTests"` | ❌ W0 (new class) |
| D-09 | empty desktop board: visually hidden heading, no poster | integration | `--filter "FullyQualifiedName~QuestBoardEmptyStateTests"` | ❌ W0 |
| Merges | `.Mobile` selection under a real phone UA | integration | `--filter "FullyQualifiedName~MobileViewsTests\|FullyQualifiedName~BoardMoveViewsTests\|FullyQualifiedName~QuestLogControllerIntegrationTests"` | ✅ (rewrite) |
| Dates | no raw `.ToString` date display in converted views | static guard | `QuestsConversionGuardTests` | ❌ W0 |
| Clock list | closed ambient-read list updated for removed files | unit | `--filter "FullyQualifiedName~AmbientClockSeamTests"` | ✅ |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

- [ ] `QuestBoard.IntegrationTests/Tests/QuestsConversionGuardTests.cs` — DoC 1-3 and the dates rule
- [ ] `QuestBoard.UnitTests/Helpers/QuestPosterPickerTests.cs` — D-05
- [ ] `QuestBoard.IntegrationTests/Tests/QuestDetailsSignupFormTests.cs`, `MarkdownEditorMarkupTests.cs`, `QuestBoardEmptyStateTests.cs` — D-09, D-12 to D-15
- [ ] Scratch capture/contrast tooling (outside the repo, built in the first plan)
- Framework install: none

---

## Manual-Only Verifications

| Behavior | Why Manual | Test Instructions |
|----------|------------|-------------------|
| Gate 1: poster mockup, phone Manage mockup, merge proposals | Operator judgement (D-18, D-19) | Operator reviews scratch mockups; approves or asks for a redraw (D-20) |
| Gate 2: before/after review sheet, desktop and phone | Operator sign-off (D-19) | Operator reviews every pair; rejected AFTERs are redrawn until approved |
| Rendered contrast, focus, 44px targets, 320px overflow, editor Tab, poster fit | Rendered behaviour, scratch tooling | Scratch Playwright probes under desktop and mobile User-Agents with touch |

---

## Validation Sign-Off

- [ ] All tasks have `<automated>` verify or Wave 0 dependencies
- [ ] Sampling continuity: no 3 consecutive tasks without automated verify
- [ ] Wave 0 covers all MISSING references
- [ ] No watch-mode flags
- [ ] Feedback latency < 60s
- [ ] `nyquist_compliant: true` set in frontmatter

**Approval:** pending
