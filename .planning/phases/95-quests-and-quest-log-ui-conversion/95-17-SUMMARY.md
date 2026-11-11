---
phase: 95-quests-and-quest-log-ui-conversion
plan: 17
subsystem: ui-closure
tags: [dead-code, cascade-layers, selector-scan, guard-tests, definition-of-converted, global-stylesheet-list]
requires:
  - phase: 95-16
    provides: the Markdown sheets already per-view, so quests.css was the last area sheet on the desktop global list
provides:
  - the unreferenced quest card and section partials and their view model deleted, with their two closed-ambient-clock entries
  - the quest card, status, wax seal, custom radio vote and vote button rules gone from legacy-pages.css and from the unlayered remainder of quests.css
  - quests.css holding only its @layer pages poster block, linked by the board view alone
  - .container.mt-4 without its important flag, proven pixel-identical on its three consumers
  - every converted area sheet registered with the foundation layer guard, and a final scope fact over the area's views and sheets
  - recomputed counts, all zero
affects: [Gate 2 sheet, Phase 96 landing page, Phase 97 calendar, Phase 98 characters and contacts]
tech-stack:
  added: []
  patterns: [selector scan over views, scripts and code before deleting a rule, same-data two-build sweep to prove a deletion changed nothing]
key-files:
  created: []
  modified:
    - QuestBoard.Service/wwwroot/css/legacy-pages.css
    - QuestBoard.Service/wwwroot/css/quests.css
    - QuestBoard.Service/wwwroot/css/components.css
    - QuestBoard.Service/Views/Shared/_Layout.cshtml
    - QuestBoard.UnitTests/Architecture/AmbientClockSeamTests.cs
    - QuestBoard.IntegrationTests/Tests/QuestsConversionGuardTests.cs
    - QuestBoard.IntegrationTests/Tests/UiFoundationLayerGuardTests.cs
  deleted:
    - QuestBoard.Service/Views/Quest/_QuestCard.cshtml
    - QuestBoard.Service/Views/Quest/_QuestSection.cshtml
    - QuestBoard.Service/ViewModels/QuestViewModels/QuestSectionViewModel.cs
key-decisions:
  - "No operator-facing exception: the container rule loses its important flag cleanly, so ~/qb-ui-review/phase-95/exceptions.json holds no entries."
  - "move-entry.css was already off the global list (95-15 made it per-view), so only quests.css left _Layout.cshtml."
requirements-completed: [D-24, DoC-1, DoC-2, DoC-3, DoC-5, DoC-8, DoC-9, DoC-10]
duration: ~2.5 h
completed: 2026-11-11
status: complete
actuals:
  tokens: 14000
  tasks: 2
  commits: 2
---

# Phase 95 Plan 17: Closure, dead partials and legacy rules gone, the area registered and counted Summary

The dead quest partials and every dead quest, seal, radio and vote rule are deleted on selector-scan evidence, `quests.css` is a single layered poster block linked only by the board, every converted area sheet is on the foundation guard, and the area's recomputed counts are all zero. A same-data sweep of the build before and after this plan shows 201 of 214 captures byte-identical (the other 13 differ only in the port inside one failed-request URL, plus a one-pixel difference), so nothing that was still live was removed.

## What changed

**Task 1 (`6210ace3`), deletions.**
- Repository search over `.cs`, `.cshtml`, `.js`, `.css`, `.json` (planning documents excluded) found the partial card, the section partial and the section view model referenced only by each other and by two entries in `AmbientClockSeamTests`. All three files deleted; exactly those two entries removed (`GuardedRelativePaths` and `RoutedTodayViewTokens`), nothing else in that test changed.
- `$QBR/tools/selector-scan.py` parses every rule of a CSS file (nested at-rules included) and reports, per class or id, the views, partials, scripts and code that use it, with a hint for classes assembled at run time. It was run over all of `legacy-pages.css` and the unlayered part of `quests.css`; the plan's line ranges (1-38, 299-791, 1185-1264) had shifted through the earlier slices, so whole files were scanned instead.

| File | Range deleted (HEAD before) | Scan result |
|---|---|---|
| `legacy-pages.css` | 1-21 `.quest-card`, `.quest-card:hover`, `.quest-status-open/finalized/done` | every class unused |
| `legacy-pages.css` | 287-698: `.fantasy-quest-card` and its hover and four media blocks, `.quest-content/header/top/middle/bottom`, `.challenge-rating`, `.cr-badge`, `.cr-icon`, `.quest-title`, `.quest-description` (and scrollbar rules), `.quest-dm/date/players/signups`, wax seal and signup seal rules, difficulty icon rules, then the custom radio vote UI (`.custom-radio-group`, `.custom-radio-label` and all its vote-state, hover, checked and focus rules) | every class unused. The one name the scan found, `.custom-radio-label`, appears only inside a click-matcher in `site.js` (`e.target.matches(...)`) and no view renders it, so it matched nothing |
| `legacy-pages.css` | 1092-1118 `.vote-btn` (four rules) | unused |
| `quests.css` | 290-419: `.quest-card`, `.quest-status-*`, an empty `.player-list` rule (comments only), `.cr-badge`, `.quest-title`, `.quest-description` and scrollbar rules, `.quest-dm/date/players/signups`, and the small-screen queries | unused (`.player-list` is rendered by Quest Details but the rule had no declarations) |
| `components.css` | `.quest-log-details` (left by the Quest Log plan) | the element exists in no view |

- Kept in `legacy-pages.css` because other areas use them: the Shop rules and keyframes, the purchase history panel, the calendar-subscription rules (Account profile) and `.container.mt-4`. `calendar.css` still has a dead `.quest-title` selector in a media query; it is the calendar phase's file and was left.
- `.container.mt-4` lost its important flag. `$QBR/tools/container-check.py` sweeps `fx-dm-editprofile`, `fx-dm-profile` and `fx-group-picker` (desktop 1440 and 320, phone 412 and 390) and diffs them. Against a capture of the same build with the flag still present all 12 captures are pixel-identical, so the rule is unchanged for its three consumers and no exception is needed. The default reference is that same-build capture, not BEFORE (see Deviations).
- `quests.css` is now 289 lines, opening `@layer pages {`, no important flag, no colour literal; it joined `ConvertedSheets`. New fact `LegacySheet_HoldsNoDeadQuestRules_AndTheSharedContainerRuleIsNotImportant` fails if a retired rule returns or the container rule regains a flag.

**Task 2 (`09d428b7`), global list, guards, counts.**
- Selector scan of `quests.css` and `move-entry.css` over every view: the only classes shared with other pages are Bootstrap's `.container`, `.btn`, `.row` and `.mobile-layout`, each inside a selector scoped by the page's own class (`.quest-board-container`, `.quest-poster`, `.move-page`), so no other page uses a rule. `_Layout.cshtml` no longer links `quests.css`; the board view links it itself. `move-entry.css` was not in the layout at all (95-15 moved it into the Move view), so there was nothing to remove for it.
- `UiFoundationLayerGuardTests`: 14 area sheets added to `LayeredFiles` (layer `pages`) and to `NoLiteralFiles`: `quests.css`, `quest-details.css`, `quest-vote.css`, `quests.mobile.css`, `home.mobile.css`, `quest-form.css`, `dm-create.mobile.css`, `quest-manage.css`, `dm-manage.mobile.css`, `quest-log.css`, `quest-log.mobile.css`, `move-entry.css`, `markdown-content.css`, `markdown-editor.css`. `quest-edit.mobile.css`, `quest-followup.mobile.css` and `move-entry.mobile.css` do not exist (merged away), so they are not listed.
- `QuestsConversionGuardTests.PhaseScope_IsFullyConverted`: lists the 25 scoped view paths and 13 sheet names, requires every one that still exists to be on `ConvertedViews` or `ConvertedSheets` (the exceptions list is empty), and fails if either deleted partial returns. `quests.css` joined the planning-reference scan list.
- `$QBR/tools/counts.py` recomputes the metrics over the 18 existing area views and 15 sheets plus the container rule.

## Recomputed counts

| Metric | ROADMAP start | After this plan |
|---|---|---|
| hex colours in area sheets | 126 | 0 |
| `rgb()` in area sheets | 276 | 0 |
| `!important` in area sheets (including the container rule) | 212 | 0 |
| inline `style=` in area views | 26 | 0 |
| `<style>` elements | 0 | 0 |
| inline `<script>` | 11 | 0 |
| inline event handlers | 59 | 0 |

No approved exceptions were needed (`counts-exceptions.json` holds no entries).

## Verification

- `dotnet build`: 0 errors. `dotnet test` (full, after the final edit): unit 1309 passed; integration 1795 passed, 11 skipped (pre-existing), 0 failed. The unit total is 3 lower than the previous plan's 1312 because `AmbientClockSeamTests` runs one case per listed path and the deleted partial left its lists (two entries removed, 62 cases there pass); no test was deleted.
- Foundation and area guards, the plan's third verify command: 181 passed at Task 1; 119 passed for `QuestsConversionGuardTests|UiFoundation` at Task 2 (including `Layouts_LinkFoundationInCascadeOrder`).
- Helpers and data layers: `git diff --quiet $(cat baseline-src/BASELINE_SHA) HEAD -- HtmlHelperExtensions.cs QuestBoard.Domain QuestBoard.Repository` exits 0; the calendar feed files live in Domain and Repository and are untouched.
- Finding map: the 30 S1 and S2 Quests audit ids are all listed by some plan of the phase (command printed `ok`).
- Sweep `after/95-17` (all 51 manifest rows, phase 162 records and foreign 52, both User-Agents, every width, all 200).
  - **Proof the deletions changed nothing.** A detached worktree of the commit before this plan was built and swept on port 8002 against the same data (`after/95-17-head`), then removed. `pair-compare.py` over all 214 records: 201 identical in pixels and in every probe field; 13 differ only in the port number inside a failed-request URL for a character picture that already returned 404 (identical otherwise), and one capture has a single differing pixel (`details-voting-checked` at 320). No page regressed.
  - **Contrast.** `readability-compare.py --zero-contrast phase --exceptions contrast-exceptions.json`: residual text-contrast failures on phase pages 0, poster-glyph floor violations 0 (the 12 exception entries untouched).
  - **Plan's literal compare against BEFORE still exits 1** for reasons recorded by earlier plans, none from this plan: (a) the phone board's quest links and main region were renamed by the phone board plan (0 problems against the phone board plan's own AFTER); (b) the shared editor's help line `Supports Markdown formatting...` at 4.25:1 on five foreign forms (already recorded in 95-16, owned by those pages' phases); (c) mobile-only `fx-admin-quests` and `fx-calendar` rows, whose layout and sheets this plan cannot reach (the phone layout links neither `legacy-pages.css` nor `quests.css`); the extra rows trace to quests added to the fixtures after the earlier captures (a quest on 30 November appears in the calendar now and not at 95-06); (d) `selftest-asserts`, a row that is only captured when named. The merged allow chain is saved as `after/95-17/allow.json` (it now includes the phone board's separate allow file, which the earlier merge missed).
- Working-tree app restarted on `http://localhost:8000` on this build and left running. Port 8001 untouched.

## Deviations from Plan

**1. [Process] The container check compares against a same-build capture, not BEFORE.** The plan's tool diffs against BEFORE. The BEFORE captures of `fx-dm-editprofile` carry the later editor and Markdown conversions (10 to 46% of the page differs for reasons unrelated to the container rule) and `fx-dm-profile` has no BEFORE on this machine, so a BEFORE diff cannot isolate one rule. I took a capture of the three rows on the same build with the flag present, removed the flag, and diffed: 12 of 12 identical. `container-check.py` uses that capture by default (`--ref` overrides, BEFORE is the fallback).

**2. [Judgement] Line ranges and file lists in the plan were stale.** The ranges in `legacy-pages.css` had shifted through earlier plans, so the whole file was scanned; `move-entry.css` was already per-view. Recorded above.

**3. [Judgement] `components.css` touched.** The 95-14 closure note named a dead `.quest-log-details` rule and the plan's context asked for it to be re-checked; it is referenced nowhere, so it was removed with the comment that mentioned it reworded. It is a one-rule foundation edit outside the plan's file list; the sweep shows no change.

**4. [Process] Acceptance search is not literally empty.** `git grep -e _QuestCard` over the test projects still matches the unrelated test method names `Desktop_QuestCard_QuestOwnerSeesManageLink` and the like (they were in the baseline too), and the new scope fact necessarily names the two deleted partials in order to check they stay gone. Nothing outside the tests references the partials or the view model.

**5. [Process] Plan tools written as the plan says plus two more.** `selector-scan.py`, `container-check.py` and `counts.py`, with `exceptions.json` and `counts-exceptions.json` (both empty). Also `latest-compare.py` and `pair-compare.py` for the sweep proof.

## Auth gates

None.

## Known Stubs

None.

## Threat Flags

None new. T-95-41: a selector scan and a same-data two-build sweep of 214 captures prove the removed rules and the global link matched nothing live. T-95-42: only the deleted partial's two entries were removed from the closed ambient-clock list, in the same commit as the deletion.

## Self-Check: PASSED

Verified: the three deleted files are gone; `quests.css` opens `@layer pages {`; `_Layout.cshtml` has no `quests.css` link; `PhaseScope_IsFullyConverted` exists and passes; commits `6210ace3` and `09d428b7` exist; full build and test green; `counts.py` prints `ok`; the helpers and data-layer diff is empty.
