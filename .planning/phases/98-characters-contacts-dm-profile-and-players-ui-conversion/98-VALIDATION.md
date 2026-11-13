---
phase: "98"
slug: "characters-contacts-dm-profile-and-players-ui-conversion"
# status lifecycle: draft (seeded by plan-phase) → validated (set by validate-phase §6)
status: draft
nyquist_compliant: false
wave_0_complete: false
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
| D-01 / X-1 | Crop control row with names; focusable selection; arrows move, Shift+arrow resizes; ratio kept; clamped; Reset | static + integration + Playwright | `~CharactersConversionGuardTests` (crop module facts); `~ImageCropDialogRenderTests`; scratch `crop-behaviour.mjs` | ❌ W0 | ⬜ pending |
| D-02 | Dialog on the opaque overlay surface; no `bg-dark text-light`/`style=`; hosts link `image-crop.css` | static + integration | `~CharactersConversionGuardTests`; `~ImageCropDialogRenderTests` | ❌ W0 | ⬜ pending |
| D-04 | Character without a picture renders the placeholder and no `GetCroppedPicture/{id}` on Quest/Details, Quest/Manage, QuestLog/Details; with one, URL present; repository flag set | integration + unit | `~CharacterAvatarRenderTests`; `~QuestRepository` unit; `~BoardPageCharacterLinksTests` (rewritten) | ❌ W0 | ⬜ pending |
| D-05 | One glass notes surface on both layouts; note text 4.5:1 | static + Playwright | `~CharactersConversionGuardTests`; scratch `contrast.mjs` | ❌ W0 | ⬜ pending |
| D-06 / X-2 | Main badge static; no infinite animation in the area | static | `~CharactersConversionGuardTests.AreaSheets_HaveNoInfiniteAnimation` | ❌ W0 | ⬜ pending |
| D-09 | Characters Create/Edit: merged view under phone UA, or twins keep selection facts | integration | `~MobileViewsTests` | ✅ edit | ⬜ pending |
| DoC-1 | No `style=`, `<style>`, inline script or `on*=` in the area's views | static | `~CharactersConversionGuardTests` | ❌ W0 | ⬜ pending |
| DoC-2 | Zero hex/`rgb()`/named colours/`!important`; each sheet in `@layer pages` | static | `~CharactersConversionGuardTests`; `~UiFoundationLayerGuardTests` | ❌ W0 / ✅ | ⬜ pending |
| DoC-3 | Sheets linked from `@section Styles`; none in the layouts | static | `~CharactersConversionGuardTests`; `~UiFoundationLayerGuardTests` | ❌ W0 / ✅ | ⬜ pending |
| DoC-4 | Twin answers (Q-50, Q-51, Q-52, Q-54, Q-55; Q-53 kept); six merges done | integration | `~MobileViewsTests`, `~ContactsTagsMobileTests`, `~ContactCategoryMobileRenderTests`, `~CharactersTwinAnswersTests` | ❌ W0 / ✅ | ⬜ pending |
| DoC-5 / DoC-7 | Accessibility bar per page under desktop and mobile UA | integration + Playwright | `~MobileViewsTests`; scratch `sweep.mjs`, `contrast.mjs` | ✅ + scratch | ⬜ pending |
| DoC-8 | Guards rewritten to the same intent, none deleted | process | test-count comparison against `tests-before.txt` | process | ⬜ pending |
| DoC-9 | Helpers untouched; nothing writes to `Events`/`Quests`; feed files untouched | unit + diff | `~WallClockMarkupTests`, `~LocalTimeMarkupTests`; `git diff --stat` | ✅ | ⬜ pending |
| DoC-10 | Every page still works | full suite + sweep | both full suites | ✅ | ⬜ pending |
| X-3 | Editor keyboard trap fix verified on six pages | integration + Playwright | `~MarkdownEditorMarkupTests` (extended); scratch editor-trap check | ✅ edit + scratch | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

- [ ] `QuestBoard.IntegrationTests/Tests/CharactersConversionGuardTests.cs` — clone of `EventsConversionGuardTests` over the area's views, partials, sheets and modules (DoC-1..3, D-01/D-02 module facts, D-06, retired files, planning-reference scan)
- [ ] `QuestBoard.IntegrationTests/Tests/CharacterAvatarRenderTests.cs` and a `QuestRepository` unit test for the has-picture flag (D-04)
- [ ] `QuestBoard.IntegrationTests/Tests/ImageCropDialogRenderTests.cs` (D-01, D-02)
- [ ] `QuestBoard.IntegrationTests/Tests/CharactersTwinAnswersTests.cs` (DoC-4)
- [ ] phone-UA cases for merged `Contacts/Details` and `ContactCategoryManagement/Edit`
- [ ] scratch tooling copied from `~/qb-ui-review/phase-97/` and re-pointed to phase-98
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

## Validation Sign-Off

- [ ] All tasks have `<automated>` verify or Wave 0 dependencies
- [ ] Sampling continuity: no 3 consecutive tasks without automated verify
- [ ] Wave 0 covers all MISSING references
- [ ] No watch-mode flags
- [ ] Feedback latency < 120 s
- [ ] `nyquist_compliant: true` set in frontmatter

**Approval:** pending
