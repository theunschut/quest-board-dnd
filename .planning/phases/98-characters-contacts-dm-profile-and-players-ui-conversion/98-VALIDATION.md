---
phase: "98"
slug: "characters-contacts-dm-profile-and-players-ui-conversion"
# status lifecycle: draft (seeded by plan-phase) → validated (set by validate-phase §6)
status: validated
nyquist_compliant: true
wave_0_complete: true
created: "2026-11-13"
---

# Phase 98 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.
> Source: `98-RESEARCH.md` § "Validation Architecture".

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit v3 + FluentAssertions; `Microsoft.AspNetCore.Mvc.Testing`; EF Core InMemory (no database) |
| **Config file** | `QuestBoard.IntegrationTests/xunit.runner.json` |
| **Quick run command (integration)** | `dotnet test QuestBoard.IntegrationTests --filter "FullyQualifiedName~CharactersConversionGuardTests\|FullyQualifiedName~CharacterAvatarRenderTests\|FullyQualifiedName~ImageCropDialogRenderTests\|FullyQualifiedName~CharactersTwinAnswersTests\|FullyQualifiedName~MobileViewsTests\|FullyQualifiedName~ContactCategory\|FullyQualifiedName~ContactsTags\|FullyQualifiedName~ContactDetailsCategoryTests\|FullyQualifiedName~CharactersControllerIntegrationTests\|FullyQualifiedName~ContactsControllerIntegrationTests\|FullyQualifiedName~DungeonMasterControllerIntegrationTests\|FullyQualifiedName~PlayersControllerIntegrationTests\|FullyQualifiedName~MarkdownEditorMarkupTests\|FullyQualifiedName~BoardPageCharacterLinksTests\|FullyQualifiedName~UiFoundation\|FullyQualifiedName~BoardPageStaticGuardTests\|FullyQualifiedName~QuestsConversionGuardTests"` |
| **Quick run command (unit)** | `dotnet test QuestBoard.UnitTests --filter "FullyQualifiedName~QuestRepository\|FullyQualifiedName~CharacterRepository\|FullyQualifiedName~AmbientClockSeamTests\|FullyQualifiedName~WallClock"` |
| **Full suite command** | `dotnet test QuestBoard.UnitTests` then `dotnet test QuestBoard.IntegrationTests` (per project avoids the CLR flake). SQL-backed migrator tests need `QUESTBOARD_MIGRATOR_TEST_REQUIRED=1` and SQL Server at `localhost:1433`; run once at the end only |
| **Browser checks** | Scratch Playwright (bundled Chromium, reused from `~/qb-ui-review/phase-94/tools/node_modules`) under `~/qb-ui-review/phase-98/`; phone checks under a mobile User-Agent with touch |
| **Estimated runtime** | ~20 s quick (after build), ~2-3 min full |

Notes: re-run once on a transient `0x80131506`; never set GC env vars. Integration tests keep `TestHostFileWatching.cs` (no inotify watchers).

---

## Sampling Rate

- **After every task commit:** the quick filters narrowed to the slice's files, plus the scratch probe on the touched pages under both User-Agents.
- **After every plan wave:** full suite, per project.
- **Before `/gsd-verify-work`:** full suite green, `dotnet build` clean, AFTER sweep shows no new contrast failure and no hidden control against BEFORE; `git diff --stat` shows no change to `HtmlHelperExtensions.cs`, `CalendarFeedWriter.cs`, `CalendarSubscriptionService.cs`, `FeedRevisionStamper.cs`.
- **Max feedback latency:** 120 seconds.

---

## Per-Requirement Verification Map

| Req | Behavior | Test Type | Automated Command | File Exists | Status |
|-----|----------|-----------|-------------------|-------------|--------|
| D-01 / X-1 | Crop control row with names; focusable selection; arrows move, Shift+arrow resizes; ratio kept; clamped; Reset | static + integration + Playwright | `~CharactersConversionGuardTests` (crop module facts); `~ImageCropDialogRenderTests`; `~KeyboardScriptGuardTests` (key/button wiring, step, ratio); scratch `crop-behaviour.mjs` (manual) | ✅ | ✅ green |
| D-02 | Dialog on the opaque overlay surface; no `bg-dark text-light`/`style=`; hosts link `image-crop.css` | static + integration | `~CharactersConversionGuardTests`; `~ImageCropDialogRenderTests` | ✅ | ✅ green |
| D-04 | Character without a picture renders the placeholder and no `GetCroppedPicture/{id}` on Quest/Details, Quest/Manage, QuestLog/Details; with one, URL present; repository flag set | integration + unit | `~CharacterAvatarRenderTests`; `~QuestRepository` unit; `~BoardPageCharacterLinksTests` (rewritten) | ✅ | ✅ green |
| D-05 | One glass notes surface on both layouts; note text 4.5:1 | static + Playwright | `~CharactersConversionGuardTests`; scratch `contrast.mjs` | ✅ | ✅ green |
| D-06 / X-2 | Main badge static; no infinite animation in the area | static | `~CharactersConversionGuardTests.AreaSheets_HaveNoInfiniteAnimation` | ✅ | ✅ green |
| D-09 | Characters Create/Edit: merged view under phone UA, or twins keep selection facts | integration | `~MobileViewsTests` | ✅ | ✅ green |
| DoC-1 | No `style=`, `<style>`, inline script or `on*=` in the area's views | static | `~CharactersConversionGuardTests` | ✅ | ✅ green |
| DoC-2 | Zero hex/`rgb()`/named colours/`!important`; each sheet in `@layer pages` | static | `~CharactersConversionGuardTests`; `~UiFoundationLayerGuardTests` | ✅ | ✅ green |
| DoC-3 | Sheets linked from `@section Styles`; none in the layouts | static | `~CharactersConversionGuardTests`; `~UiFoundationLayerGuardTests` | ✅ | ✅ green |
| DoC-4 | Twin answers (Q-50, Q-51, Q-52, Q-54, Q-55; Q-53 kept); six merges done | integration | `~MobileViewsTests`, `~ContactsTagsMobileTests`, `~ContactCategoryMobileRenderTests`, `~CharactersTwinAnswersTests` | ✅ | ✅ green |
| DoC-5 / DoC-7 | Accessibility bar per page under desktop and mobile UA | integration + Playwright | `~MobileViewsTests`; scratch `sweep.mjs`, `contrast.mjs` | ✅ | ✅ green |
| DoC-8 | Guards rewritten to the same intent, none deleted | process | test-count comparison against `tests-before.txt` | process | ✅ green |
| DoC-9 | Helpers untouched; nothing writes to `Events`/`Quests`; feed files untouched | unit + diff | `~WallClockMarkupTests`, `~LocalTimeMarkupTests`; `git diff --stat` | ✅ | ✅ green |
| DoC-10 | Every page still works | full suite + sweep | both full suites | ✅ | ✅ green |
| X-3 | Editor keyboard trap fix verified on six pages | integration + Playwright | `~MarkdownEditorMarkupTests` (extended); `~KeyboardScriptGuardTests` (Tab release pin); scratch editor-trap check (manual) | ✅ | ✅ green |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

- [x] `QuestBoard.IntegrationTests/Tests/CharactersConversionGuardTests.cs` — clone of `EventsConversionGuardTests` over the area's views, partials, sheets and modules (DoC-1..3, D-01/D-02 module facts, D-06, retired files, planning-reference scan)
- [x] `QuestBoard.IntegrationTests/Tests/CharacterAvatarRenderTests.cs` and a `QuestRepository` unit test for the has-picture flag (D-04)
- [x] `QuestBoard.IntegrationTests/Tests/ImageCropDialogRenderTests.cs` (D-01, D-02)
- [x] `QuestBoard.IntegrationTests/Tests/CharactersTwinAnswersTests.cs` (DoC-4)
- [x] phone-UA cases for merged `Contacts/Details` and `ContactCategoryManagement/Edit`
- [x] scratch tooling copied from `~/qb-ui-review/phase-97/` and re-pointed to phase-98
- Framework install: none

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| Gate 1 approval (Q-117/118/119 relayouts, crop dialog, look check vs Direction A, Characters Create/Edit merge proposal) | D-03, D-07, D-08, D-09 | Operator taste gate | `~/qb-ui-review/phase-98/gate1/`; redraw until approved |
| Gate 2 before/after sign-off incl. Quest/Quest Log re-verified for D-04 and the six editor pages | D-07, DoC-6 | Operator taste gate | `~/qb-ui-review/phase-98/gate2/` |
| Crop controls under real touch and a screen reader | D-01, X-1 | Real device / assistive technology | Phase UAT, after deploy |
| Real-phone check of the area pages | DoC-7 | Real touch device | Phase UAT, after deploy |
| Rendered contrast, 24/44px targets, overflow at 320/390 | DoC-5 | Rendered measurement | `~/qb-ui-review/phase-98/after/` |

---

## Manual-Only (runtime behaviour, no repo browser; see 98-UAT.md)

| Behavior | Requirement | Static pin | UAT |
|----------|-------------|-----------|-----|
| Crop drag, arrow/Shift+arrow, buttons, focus return, touch | D-01, X-1 | `KeyboardScriptGuardTests`, `ImageCropDialogRenderTests` | 98-UAT 1, 2, 5 |
| Markdown editor Tab/Shift-Tab leaves field on six pages | X-3 | `KeyboardScriptGuardTests.MarkdownEditor_...`, `MarkdownEditorMarkupTests` | 98-UAT 5 |
| confirm() dialogs and Contacts note toggles | A-characters-044 | `KeyboardScriptGuardTests`, `*RenderTests` data-confirm-message | 98-UAT 5 |
| Quest pages with picture-less character in production | D-04 | `CharacterAvatarRenderTests` | 98-UAT 3 |

---

## Validation Sign-Off

- [x] All tasks have `<automated>` verify or Wave 0 dependencies
- [x] Sampling continuity: no 3 consecutive tasks without automated verify
- [x] Wave 0 covers all MISSING references
- [x] No watch-mode flags
- [x] Feedback latency < 120 s
- [x] `nyquist_compliant: true` set in frontmatter

**Approval:** validated 2026-11-14

---

## Validation Audit 2026-11-14

| Metric | Count |
|--------|-------|
| Gaps found | 3 (partial: X-3 Tab release, D-01/X-1 key and button wiring, confirm/note scripts: markup covered, script behaviour unpinned) |
| Resolved | 3 (static script guards, `KeyboardScriptGuardTests`, 6 facts) |
| Escalated | 0 |

All other requirements were already covered by Wave 0 tests (CharactersConversionGuardTests, CharacterAvatarRenderTests, QuestRepositoryPictureFlagTests, ImageCropDialogRenderTests, CharactersTwinAnswersTests, MarkdownEditorMarkupTests, detail/index/manage render tests). Runtime-only behaviour remains Manual-Only per 98-UAT.md. Final: unit 1416 passed; integration 2308 passed, 16 skipped (SQL-backed), 0 failed.
