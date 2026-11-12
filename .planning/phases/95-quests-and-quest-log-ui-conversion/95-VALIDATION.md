---
phase: "95"
slug: "quests-and-quest-log-ui-conversion"
# status lifecycle: draft (seeded by plan-phase) → validated (set by validate-phase §6)
status: validated
nyquist_compliant: true
wave_0_complete: true
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
| **Browser checks** | scratch Playwright under `~/qb-ui-review/phase-95/` (outside the repo), reusing the Phase 94 `playwright` 1.64.1 install and its headless Chromium |
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

| Contract point | Behavior | Test type | Automated command | File | Status |
|----------------|----------|-----------|-------------------|------|--------|
| DoC 1 | no inline style/script/handler in converted views | static guard | `--filter "FullyQualifiedName~QuestsConversionGuardTests"` | `Tests/QuestsConversionGuardTests.cs` | ✅ COVERED |
| DoC 2 | zero hex/rgb/`!important` in converted sheets | static guard | same | same | ✅ COVERED |
| DoC 3 | page links its own sheet; foreign consumers link theirs | static guard | same | same | ✅ COVERED |
| DoC 4 | twins aligned; merged views leave no phone twin | static guard + integration | `QuestsConversionGuardTests`, `MobileViewsTests` | `Mobile/MobileViewsTests.cs` | ✅ COVERED |
| DoC 5 | accessibility bar (contrast, focus, 44px, overflow) | rendered browser | scratch Playwright | n/a | MANUAL-ONLY |
| DoC 6, 7 | before/after screenshots; phone UA sweeps | rendered browser | scratch Playwright | n/a | MANUAL-ONLY (phone UA selection COVERED by `MobileViewsTests`) |
| DoC 8 | guards rewritten, none deleted | diff review | verifier diff (0 removed facts) | n/a | COVERED (verification) |
| DoC 9 | `LocalTime`/`WallClock` untouched, no writes to Events/Quests | guard | `WallClockUnmovedTests`, `AmbientClockSeamTests`, `QuestsConversionGuardTests` (raw date rule) | existing | ✅ COVERED |
| DoC 10 | every page still works | full suite | `dotnet test` | all | ✅ COVERED |
| D-01, D-02, D-04 | description off poster; Poster3 retired; pool fits | unit + integration | `QuestPosterPickerTests`, `QuestBoardEmptyStateTests` | `UnitTests/Helpers/QuestPosterPickerTests.cs` | ✅ COVERED |
| D-03 | measured box per poster | unit | `QuestPosterPickerTests` | same | ✅ COVERED (fit on screen MANUAL-ONLY) |
| D-05 | pick stable per id, fits, long titles to tall posters | unit | `--filter "FullyQualifiedName~QuestPosterPickerTests"` | same | ✅ COVERED |
| D-06 | seals as status markers, link names the status | integration | `QuestBoardEmptyStateTests` | `Tests/QuestBoardEmptyStateTests.cs` | ✅ COVERED |
| D-07, D-08, D-11 | mockup switchers; lightened zone; mockup before code | operator gate | Gate 1 record | n/a | MANUAL-ONLY |
| D-09 | empty desktop board: hidden heading, no poster | integration | `QuestBoardEmptyStateTests` | same | ✅ COVERED |
| D-10 | WebP beside PNG for every poster and seal; CSS `image-set` | integration/unit | `QuestFormExitRowAndPosterAssetTests.EveryPosterAndSeal_HasItsPngAndItsWebpOnDisk`, `QuestPosterPickerTests.Catalog_*` | `Tests/QuestFormExitRowAndPosterAssetTests.cs` | ✅ COVERED (added in audit) |
| D-12 | Tab leaves the editor | rendered browser | scratch Tab probe (56/56 `leaves: true`) | n/a | MANUAL-ONLY |
| D-13 | visible hint, id wiring, lib files still load | rendered HTML + guard | `MarkdownEditorMarkupTests`, `UiFoundationViewLibraryGuardTests` | `Tests/MarkdownEditorMarkupTests.cs` | ✅ COVERED |
| D-14 | phone signup posts CharacterId + selectedRole, plain selects above voting | integration | `QuestDetailsSignupFormTests` | `Tests/QuestDetailsSignupFormTests.cs` | ✅ COVERED |
| D-15 | one vote wrapper, outside DM Controls | integration | same | same | ✅ COVERED (the hide toggle itself MANUAL-ONLY: Playwright `details-spectator` rows) |
| D-16 | twin wording | integration | `MobileViewsTests` (Join/Participants/status copy) | existing | ⚠ PARTIAL: key phrases pinned on the phone layout, no cross-layout wording comparison; remaining is copy review, accepted |
| D-17 | Cancel left, commit right after an `hr`, same targets, both layouts | integration | `--filter "FullyQualifiedName~QuestFormExitRowAndPosterAssetTests"` | `Tests/QuestFormExitRowAndPosterAssetTests.cs` | ✅ COVERED (added in audit; CreateFollowUp covered by `MobileViewsTests` render) |
| D-18..D-21 | operator gates, merges | operator gate + guard | Gate 1/2 records; merged-view guards | n/a | MANUAL-ONLY (merges COVERED by guards) |
| D-22, D-23 | scratch environment, fixtures | process | n/a | n/a | MANUAL-ONLY |
| D-24 | slices on the milestone branch | process | git log | n/a | MANUAL-ONLY |
| Quest Manage / Move / Quest Log | DM controls, move, log render | integration | `QuestManageDmControlsTests`, `BoardMove*`, `QuestLogControllerIntegrationTests`, `MobileViewsTests` | existing | ✅ COVERED |
| Clock list | closed ambient-read list | unit | `AmbientClockSeamTests` | existing | ✅ COVERED |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

- [x] `QuestBoard.IntegrationTests/Tests/QuestsConversionGuardTests.cs` — DoC 1-3 and the dates rule
- [x] `QuestBoard.UnitTests/Helpers/QuestPosterPickerTests.cs` — D-05
- [x] `QuestBoard.IntegrationTests/Tests/QuestDetailsSignupFormTests.cs`, `MarkdownEditorMarkupTests.cs`, `QuestBoardEmptyStateTests.cs` — D-09, D-12 to D-15
- [x] Scratch capture/contrast tooling (outside the repo, built in the first plan)
- Framework install: none

---

## Manual-Only Verifications

| Behavior | Why Manual | Test Instructions |
|----------|------------|-------------------|
| Spectator toggle, Tab leaving the editor, Manage/form module actions at runtime | Browser runtime behaviour | Evidence: `after/final/report.json` (8 spectator records, 56/56 editor probes `leaves: true`) and the manage probes re-run by the orchestrator (95-VERIFICATION.md re-check) |
| Phone UAT (incl. Rewards disclosure, WINDOWS #32) | Real device | 95-18 checklist; still open |
| Gate 1: poster mockup, phone Manage mockup, merge proposals | Operator judgement (D-18, D-19) | Operator reviews scratch mockups; approves or asks for a redraw (D-20) |
| Gate 2: before/after review sheet, desktop and phone | Operator sign-off (D-19) | Operator reviews every pair; rejected AFTERs are redrawn until approved |
| Rendered contrast (poster text held to the Gate 1 item 2 poster-glyph floors in contrast-exceptions.json; every other surface 4.5:1), focus, 44px targets, 320px overflow, editor Tab, poster fit | Rendered behaviour, scratch tooling | Scratch Playwright probes under desktop and mobile User-Agents with touch |

---

## Validation Sign-Off

- [x] All tasks have `<automated>` verify or Wave 0 dependencies
- [x] Sampling continuity: no 3 consecutive tasks without automated verify
- [x] Wave 0 covers all MISSING references
- [x] No watch-mode flags
- [x] Feedback latency < 60s
- [x] `nyquist_compliant: true` set in frontmatter

**Approval:** validated 2026-11-11

---

## Validation Audit 2026-11-11

| Metric | Count |
|--------|-------|
| Contract points audited | 29 |
| COVERED | 19 |
| PARTIAL | 1 (D-16 cross-layout wording, accepted) |
| MISSING (automatable) | 0 after audit |
| Manual-only | 9 (rendered contrast/focus/44px/overflow/poster fit, Tab release, Spectator toggle, module actions, gates, phone UAT) |
| Gaps found and filled | 2 (D-17 exit row on both layouts; D-10 poster and seal assets on disk) |
| Escalated | 0 |

Tests added: `QuestBoard.IntegrationTests/Tests/QuestFormExitRowAndPosterAssetTests.cs` (5 cases). One iteration fixed a wrong test expectation (route `/quests`); no implementation changes.
