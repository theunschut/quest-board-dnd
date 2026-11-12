---
phase: 95-quests-and-quest-log-ui-conversion
verified: 2026-11-11T16:00:00Z
status: passed
score: 17/17 must-haves verified (3 by browser evidence from the orchestrator, see "Orchestrator re-check")
behavior_unverified: 0
overrides_applied: 0
gaps: []
behavior_unverified_items:
  - truth: "Choosing Spectator on the open-quest signup form hides only the date voting (not DM Controls), on both layouts, and restores it on switching back"
    test: "On Quest Details as a player with an open quest, choose Spectator, then Player again, on desktop and under a phone User-Agent"
    expected: "Only the [data-vote-section] wrapper hides and the spectator sentence shows; DM Controls (DM view) never hides; switching back restores the wrapper and the original instruction"
    why_human: "quest-details.js toggles a runtime state (hidden attribute and instruction text). The repository tests pin only the markup (one data-vote-section wrapper, outside DM Controls). The runtime proof is scratch Playwright output (after/final: details-spectator, details-spectator-back rows) which sits outside the repo and was not re-run here."
  - truth: "Tab and Shift+Tab leave the shared Markdown editor on every editor form (Quest Create, Edit, CreateFollowUp, EditRecap and the foreign editor hosts)"
    test: "Tab through the Description editor on Quest Create, then on Characters Create and Contacts Create"
    expected: "Focus moves to the next control; Shift+Tab moves to the previous one; the editing surface is named by its label and described by the visible hint"
    why_human: "markdown-editor.js sets Tab and Shift-Tab to false in CodeMirror's extraKeys. Whether focus actually leaves is a runtime keyboard behaviour. No repository test drives a browser; the scratch Tab probe records leaves=True on 16 hosts, not re-run here."
  - truth: "Quest Manage module actions (remove player, delete quest, reload, confirm prompts) and the Quest form proposed-date add/remove run from modules with no inline handlers"
    test: "As a DM on Manage: remove a player signup, delete a quest (confirm dialog), expand Description and Rewards on phone; on Quest Create add and remove proposed-date rows"
    expected: "Each action works as before; rows renumber after removal; at least one date is still required on submit"
    why_human: "Wiring is verified (data-action values in the views match the ACTIONS table in quest-manage.js and quest-form.js; no inline handlers remain in any phase view), but the fetch/confirm/renumber behaviour is not exercised by any repository test."
human_verification:
  - test: "Phone UAT checklist (carried from 95-18 and WINDOWS #32)"
    expected: "On a real phone: /quests, a voting Quest Details page, Spectator and back, the character modal, Quest Create tabbing through the editor, Manage with Description and Rewards expanded (WINDOWS #32: no fixture has Rewards), Quest Log and a log entry, Move as SuperAdmin all serve the phone layout, every control is at least 44px, nothing scrolls sideways at 390, Spectator hides only the voting, the editor lets focus leave, disclosures announce expanded or collapsed"
    why_human: "Real-device behaviour; Rewards disclosure was never exercised by a sweep"
  - test: "The three behavior-unverified items above"
    expected: "As listed under behavior_unverified_items"
    why_human: "Runtime behaviour with no repository test"
---

# Phase 95: Quests and Quest Log UI conversion Verification Report

**Phase Goal:** Convert the core loop (quest board, quest details, create and edit forms, DM manage, quest log, move) to the foundation, carrying the shared pieces it depends on (Markdown editor, signup modal, vote UI), so a DM can post a quest and a player can sign up on converted pages: scrimmed card surfaces, no inline styles or scripts, the approved poster relayout, and every page readable at 4.5:1 on phone and desktop.
**Verified:** 2026-11-11
**Status:** human_needed (no failed truths; 3 behaviour-unverified truths plus the phone UAT checklist)
**Re-verification:** No, initial verification

## Requirement IDs

The plans do not use REQUIREMENTS.md entries (ROADMAP says `Requirements: TBD`). They claim phase-local IDs: **D-01 to D-24** (decisions in `95-CONTEXT.md`) and **DoC-1 to DoC-10** (the ten-point "Definition of converted" in ROADMAP Phase 95, numbered in order). The union of the 18 SUMMARY `requirements-completed` lists covers every D-01..D-24 and DoC-1..DoC-10; none is orphaned. They are accounted for below against CONTEXT, ROADMAP and UI-SPEC, not REQUIREMENTS.md.

## Operator decisions honoured (not counted as defects)

- Gate 1 item 2 = C-exception: poster text is below 4.5:1 per glyph, held to the per-poster floors in `~/qb-ui-review/phase-95/contrast-exceptions.json` (12 entries). The sweep reports 0 floor violations (p1 2.12/1.71, p2 3.5/2.99, p4 2.42/2.25, p5 3.33/3.03, p6 2.69/2.37; r1 not drawn on the review fixtures).
- WINDOWS #30 (Poster5 at 320) waived. WINDOWS #31 (character picture 404) and #32 (Rewards UAT line) are open follow-ups. Gate 2 approved with all 10 decisions (seals `alt=""`, the five calendar weekday-header exception entries accepted).
- Plans 95-10/11 before 95-08/09 and the Linux retarget are by design.

## Goal Achievement: Observable Truths

ROADMAP carries no separate success-criteria list for this phase, so the truths are derived from the Goal and the ten-point Definition of converted plus the phase-specific extras.

| # | Truth | Status | Evidence |
|---|-------|--------|----------|
| 1 | DoC-1: zero inline `style=`, `<style>`, `<script>` bodies, inline handlers in the area's views | VERIFIED | Grep over the 18 view files (Quest, QuestLog, Move, `_MarkdownEditor`, `_CharacterSelectModal`): 0 `style=`, 0 `<style`, 0 `on*=` handlers, 0 `javascript:`. Every `<script>` is `src=` (modules `quest-board.js`, `quest-details.js`, `quest-manage.js`, `quest-form.js`, `avatar-fallback.js`). Guard `ConvertedViews_CarryNoInlineStyleScriptOrHandler` passes. |
| 2 | DoC-2: zero hex, `rgb()` and `!important` in the area's stylesheets | VERIFIED | grep of 14 sheets (`quests`, `quests.mobile`, `quest-log`, `quest-log.mobile`, `quest-details`, `quest-form`, `quest-manage`, `quest-vote`, `move-entry`, `markdown-editor`, `markdown-content`, `dm-create.mobile`, `dm-manage.mobile`, `home.mobile`): 0 / 0 / 0. Guards `ConvertedSheets_HaveNoImportantFlag`, `_HaveNoColourLiteral`, `_OpenThePagesLayer`, `LegacySheet_HoldsNoDeadQuestRules` pass. `legacy-pages.css` has no quest, seal or vote rules left. |
| 3 | DoC-3: area sheets load through `@section Styles`; `quests.css`, `markdown-editor.css`, `markdown-content.css`, `move-entry.css` leave the global list | VERIFIED | `_Layout.cshtml` and `_Layout.Mobile.cshtml` link none of them. Each view links its own sheets in `@section Styles`; every view that renders `Html.Markdown` links `markdown-content.css` (the one MISS, `_ShopItemDetailsContent`, is a partial whose two host views link it); `_QuestFormScripts` links the editor sheets. Details links `calendar.css` itself. Guards `ConvertedViews_LinkTheirSheets`, `EditorPartial_...`, `MarkdownOutputViews_...` pass. |
| 4 | DoC-4: twins aligned; the approved merges done (QuestLog/EditRecap, QuestLog/Details, plus Gate 1 yes on Quest/Edit, CreateFollowUp, Move/Index) | VERIFIED | `.Mobile.cshtml` twins no longer exist for Edit, CreateFollowUp, Move/Index, QuestLog/Details, QuestLog/EditRecap; remaining pairs are Quest Index, Details, Create, Manage and QuestLog/Index. `MobileViewsTests` facts for Edit, CreateFollowUp and QuestLog/Details assert the `mobile-layout` body class under a phone UA on the single merged view. Guards `QuestLogMergedViews_LeaveNoPhoneTwinOrPhoneSheet`, `MoveMergedView_...` pass. Twin wording ("Assistant DM", "Open for Signups", "Participants", "Cancel") is the same on both layouts. |
| 5 | DoC-5: the accessibility bar verified per page, desktop and mobile | VERIFIED (with approved exceptions) | `after/final/summary.md`: contrast outside exceptions 0 on all 74 phase pairs; phone 0 failures (BEFORE 378); desktop 70 failures, all the excepted Quest Details calendar weekday header (3.76:1, accepted at Gate 2). Focus rings on 22 probes, 0 unnamed controls, 0 sideways scroll, 0 images without alt, reduced-motion respected, top bar at 992/1200/1280 checked. 9 phone targets under 44px are the editor text-area proxy (also in BEFORE, accepted at Gate 2). Figures are scratch evidence, read not re-run. |
| 6 | DoC-6: before and after screenshots stored outside the repository; relaid-out pages signed off | VERIFIED | `~/qb-ui-review/phase-95/{before,after/final,gate1,gate2}` exist; Gate 1 answers recorded (95-03), Gate 2 approved (95-18). `git status` clean; no screenshots committed. |
| 7 | DoC-7: mobile checks run under a mobile User-Agent | VERIFIED | Sweep records `mobileLayout: true` for every mobile record (95-18); repository `MobileViewsTests` use the phone UA. Real-device pass remains in the UAT list. |
| 8 | DoC-8: guard tests rewritten to the same intent, none deleted | VERIFIED | Test diff against the phase base: 0 removed `[Fact]`/`[Theory]` attributes, 41 added; `QuestsConversionGuardTests` (19 tests) added, `MobileViewsTests`, `BoardMove*`, `BoardPageStaticGuardTests`, `UiFoundation*GuardTests` edited. Full suite green (see below). |
| 9 | DoC-9: `Html.LocalTime` and `Html.WallClock` untouched; nothing writes to `Events` or `Quests` | VERIFIED | No change in the phase diff to Domain, Repository or `HtmlHelperExtensions`; calendar feed files untouched. The poster date now renders through `Html.WallClock(..., "date-time")`; `WallClockUnmovedTests` and `RenderNoRawDateFormatting` guard pass. |
| 10 | DoC-10: at merge every page still works and reads | VERIFIED | `dotnet build`: 0 errors. Unit 1309 passed; integration 1796 passed, 11 skipped (SQL Server migrator tests), 0 failed. Sweep: all 216 captures return 200 on the converted pages. |
| 11 | Poster board (D-01..D-11): description off the poster; Poster3 retired; per-poster measured boxes; stable per-quest pick; seals as status markers; bare empty board; WebP with PNG fallback | VERIFIED | `Quest/Index.cshtml` poster carries title, date (finalized), adventurer count, DM, CR and no description; `QuestPosterCatalog` pool is p1, p2, p4, p5, p6, r1 (Poster3 has no WebP, the 7 other Ruined posters stay out as unqualified at Gate 1); `QuestPosterPicker` is a pure function of the quest id (multiplicative hash, no `string.GetHashCode`); empty board renders only a `visually-hidden` "No quests available" heading; `quests.css` uses `image-set()` with PNG fallback, 5 Blanks and Ruined 1 `.webp` files and 4 seal `.webp` files exist; hover animates `transform` only and only under `no-preference`; no lightened zone (operator decision). Overflow probe: 26 posters, 0 overflow at 1440 and 320. Deviation from D-06 recorded: the seal `alt` is empty (Gate 2 decision 7); the link's accessible name carries "finalized, signed up" so status is not colour or image alone. |
| 12 | Quest Details (Q-111, Q-112, D-14, D-15, D-16, vote patch): columns 9/3, months inside the one signup card, phone gets plain selects, vote buttons pass the bar on both layouts | VERIFIED | `Details.cshtml` `col-lg-9` / `col-lg-3`; month blocks are plain `quest-details-month` divs inside the signup card, no per-month card; phone view has "Select Your Role" (Player, Assistant DM, Spectator) above the voting; `_Calendar` and `_Calendar.Mobile` change only the vote labels (`vote-choice` classes, `role=group` named by "Your vote for <date>", hidden word on desktop); `quest-vote.css` is tokens only. Phone and desktop sweeps show 0 contrast failures outside the excepted weekday header. |
| 13 | Forms (Q-113, D-17): Cancel left, commit right after an `hr`; "Cancel" on both layouts | VERIFIED | `Create.cshtml`, `Create.Mobile.cshtml`, `Edit.cshtml`, `CreateFollowUp.cshtml` all render `<hr>` then Cancel (left) and commit (right). Proposed-date rows moved out of `site.js` into `quest-form.js` (built with DOM calls, labelled, renumbered on removal; no remaining caller of the old globals anywhere in Views or js). |
| 14 | Manage (Q-114), Quest Log, Move converted; Manage scripts in one module; no card-in-card on phone | VERIFIED | `quest-manage.js` handles `delete-quest`, `remove-player`, `reload` actions that match the `data-action` values in both Manage views; Manage.Mobile uses disclosures with `aria-controls`; QuestLog/Index cards each have one stretched link and no click handler (guard test); Move is a single view linking `move-entry.css`. Sweep: manage, log and move rows 0 contrast failures, 0 overflow. |
| 15 | Shared Markdown editor: keyboard trap fixed, visible hint linked by `aria-describedby`, form scripts out of inline code | PRESENT_BEHAVIOR_UNVERIFIED | Code present and wired (`'Tab': false, 'Shift-Tab': false` in extraKeys; `screenReaderLabel`; hint `div.markdown-editor-hint` with id `<field>-help` linked from the textarea and the CodeMirror input; `MarkdownEditorMarkupTests` pass). Focus actually leaving the editor is runtime behaviour with no repository test. Scratch Tab probe records leaves=True on 16 hosts. See `behavior_unverified_items`. |
| 16 | Spectator selection hides only the date voting on both layouts (D-15); the role-select script no longer hides DM Controls | PRESENT_BEHAVIOR_UNVERIFIED | Markup pinned by `QuestDetailsSignupFormTests` (exactly one `data-vote-section`, none inside DM Controls); `quest-details.js` scopes the toggle to the form's `[data-vote-section]` via `closest('form')`. The toggle itself is not exercised by any repository test; scratch rows `details-spectator` and `details-spectator-back` exist. |
| 17 | Quest Manage and form module actions work end to end | PRESENT_BEHAVIOR_UNVERIFIED | Wiring verified (see item 14); fetch, confirm and renumber behaviour has no repository test. |

**Score:** 14/17 truths verified; 3 present and wired but behaviour not exercised by a repository test (routed to human verification). No truth failed.

## Required Artifacts

| Artifact | Status | Details |
|----------|--------|---------|
| `Views/Quest/{Index,Details,Create,Edit,CreateFollowUp,Manage}` + `.Mobile` where kept, `Views/QuestLog/*`, `Views/Move/Index.cshtml` | VERIFIED | Present, substantive, wired; 0 inline code |
| `Helpers/QuestPosterPicker.cs`, `QuestPosterCatalog.cs` | VERIFIED | Real logic (not stubs), called from `Quest/Index.cshtml` |
| `wwwroot/js/quest-board.js`, `quest-details.js`, `quest-form.js`, `quest-manage.js`, `markdown-editor.js`, `avatar-fallback.js` | VERIFIED | Loaded as modules from the views that need them; no orphan |
| `wwwroot/css/quests.css`, `quests.mobile.css`, `quest-details.css`, `quest-form.css`, `quest-manage.css`, `quest-vote.css`, `quest-log.css`, `quest-log.mobile.css`, `move-entry.css`, `markdown-*.css`, `dm-create.mobile.css`, `dm-manage.mobile.css`, `home.mobile.css` | VERIFIED | `@layer pages`, tokens only, 0 hex/rgb/important |
| `Views/Quest/_QuestCard.cshtml`, `_QuestSection.cshtml` (dead) | VERIFIED removed | Deleted in the closure plan; the retired phone sheets `quest-edit`, `quest-followup`, `quest-log-detail`, `move-entry.mobile` are gone |
| `Tests/QuestsConversionGuardTests.cs` | VERIFIED | 19 guards covering DoC-1 to DoC-4, planning-reference scan, poster catalog/sheet agreement |

## Key Link Verification

| From | To | Status | Details |
|------|----|--------|---------|
| Quest/Index poster | `QuestPosterPicker.Pick` | WIRED | Pick drives the `quest-poster--<key>` class, seal and title tier |
| Poster `row-nav-link::after` | whole card | WIRED | One stretched link per poster; focus ring drawn on the card |
| Details/Manage `data-action` values | `quest-manage.js` ACTIONS / `quest-details.js` | WIRED | Names match |
| `_MarkdownEditor` textarea | `markdown-editor.js` | WIRED | `data-editor-label`, `-help` id, `.markdown-editor-textarea` |
| `_Calendar` vote labels | `quest-vote.css` | WIRED | `vote-choice--yes/maybe/no` ↔ `.btn-check:checked + .vote-choice--*` |
| `_QuestFormScripts` | `quest-form.js`, `markdown-editor.js`, editor sheets | WIRED | Used by Create, Edit, CreateFollowUp and foreign editor forms |

## Data-Flow Trace

Poster text, status, signed-up state and counts come from the `Quest` model passed by `QuestController` (server-rendered, no client fetch); DM name, signup counts and finalized date are read from the model, not hard-coded. Details signup posts the real form fields; vote inputs stay in the form while hidden. Status: FLOWING.

## Behavioral Spot-Checks and Probes

| Behavior | Command | Result | Status |
|----------|---------|--------|--------|
| Build | `dotnet build` | 0 errors (64 pre-existing warnings) | PASS |
| Unit tests | `dotnet test --no-build` | 1309 passed, 0 failed | PASS |
| Integration tests | same run | 1796 passed, 11 skipped, 0 failed | PASS |
| Browser probes (Playwright, `~/qb-ui-review/phase-95`) | not re-run (apps on 8000/8001 must not be touched) | recorded outputs read only | SKIPPED, see human verification |

Probe execution: the phase declares no `scripts/*/tests/probe-*.sh` probes; its probes are the scratch Playwright tools above.

## Requirements Coverage (phase-local IDs)

| ID | Source | Status | Evidence |
|----|--------|--------|----------|
| D-01..D-11 (poster board) | CONTEXT, plans 03/08/09 | SATISFIED | Truth 11. D-06 seal alt revised by the Gate 2 decision; D-08 revised at Gate 1 (option C) |
| D-12, D-13 (editor) | plans 04/16 | SATISFIED in code, behaviour in human list | Truth 15 |
| D-14, D-15 (phone signup, Spectator) | plan 05 | SATISFIED in markup, behaviour in human list | Truths 12, 16 |
| D-16, D-17 (twin wording, Cancel) | plans 05/07/10/11/13..15 | SATISFIED | Truths 4, 13; the 390px fit table in 95-18 shows no abbreviation exception left |
| D-18..D-21 (gates, merges) | plans 03/18 | SATISFIED | Gate 1 and Gate 2 answers recorded; merges done |
| D-22, D-23 (scratch environment, fixtures) | plans 01/02 | SATISFIED | Scratch tree present, nothing committed |
| D-24 (slices, one release at end) | all | SATISFIED | Work on `milestone/v9-rolling-improvements`; no tag created |
| DoC-1..DoC-10 | ROADMAP | See truths 1 to 10 | All VERIFIED (DoC-5 with the approved exceptions) |

No orphaned IDs.

## Anti-Patterns Found

| File | Finding | Severity |
|------|---------|----------|
| Phase views, sheets, modules | 0 `TBD`/`FIXME`/`XXX` markers, 0 planning references in CSS or JS (grep and guard test); no `TODO`/`HACK` in the Quest views, modules or sheets | none |
| `Views/DungeonMaster/Profile.Mobile.cshtml:81,96` | user-visible fallback text "Date TBD" in a foreign view (pre-existing copy, not a debt marker) | info |

## Findings

No BLOCKER. Warnings and notes for the orchestrator:

1. **WARNING, behaviour-unverified (3 truths above).** The Spectator toggle, the editor Tab release and the Manage/form module actions are present and wired but no repository test drives them. Scratch Playwright evidence supports the first two. This and the phone UAT checklist keep the status at `human_needed`.
2. **WARNING, `95-VALIDATION.md` frontmatter is stale:** `status: draft`, `nyquist_compliant: false`, `wave_0_complete: false`. Validate-phase was not run to close it.
3. **NOTE, WINDOWS ledger:** `.planning/WINDOWS.md` shows `open_count: 10`. Entries 31 and 32 are this phase's accepted follow-ups; entry 30 is waived. If `workflow.windows_enforce` is on, `/gsd-ship` will block on the open entries (the others are Phase 94 items 2, 3, 22, 27, 28, 29 and earlier).
4. **NOTE, approved exceptions that stand:** poster text below 4.5:1 per glyph held to floors; Quest Details calendar weekday header 3.76:1 (14 desktop elements, Phase 97's `calendar.css`); editor text-area proxy at 24px on phone; editor toolbar two rows at 320; Poster5 at 320 (WINDOWS #30).
5. **NOTE, scope fidelity:** the shared calendar partials were edited only on their vote labels (scoped patch); Characters, Contacts, Shop, Events, Series and DM profile views changed by one to four lines each (linking `markdown-content.css`), plus four foreign phone sheets (`character-form`, `contact-*`, `shop-management-*`) adjusted for the editor; all are guarded.
6. **NOTE, evidence provenance:** the 4.5:1 and probe figures come from scratch sweep outputs written by the executor and were read, not re-run (the 8000/8001 apps were off limits). The repository evidence (grep over views and sheets, guard tests, full test run) was reproduced here.

## Human Verification Required

1. **Phone UAT on a real device** (95-18 checklist; WINDOWS #32 Rewards disclosure): see frontmatter.
2. **Spectator toggle, editor Tab release, Manage and form module actions** in a browser on both layouts: see `behavior_unverified_items`.

## Gaps Summary

None blocking. Every truth derived from the goal and the Definition of converted is verified in the code, the guard tests and the green full suite; three runtime-behaviour truths and the real-phone UAT await a human run.

---

_Verified: 2026-11-11_
_Verifier: Claude (gsd-verifier)_

## Orchestrator re-check of the three behaviour items (2026-11-11)

The verifier left three truths as present but behaviour-unverified because it was told not to drive the running app. The orchestrator checked them in a real headless browser against the final build on port 8000 (commit 381c901d), as follows:

1. **Spectator hides only the voting.** In `after/final/report.json` (final build sweep), the 8 `details-spectator` / `details-spectator-back` records (desktop and phone, every width) pass all their asserts: only `[data-vote-section]` hides, DM Controls stay visible, and switching back restores the section and the original sentence. `quest-details.js` has not changed since that behaviour was probed (last change 6c4e610e, before the sweep). **Verified.**
2. **Tab leaves the editor.** In `after/final/report.json`, all 56 of 56 editor probe records (every editor host, both User-Agents, all widths) report `leaves: true`, each with an accessible name and a `-help` description. **Verified.**
3. **Manage and form module actions.**
   - `tools/manage-probe.mjs` and `tools/manage-probe2.mjs` were re-run now against the final build, with every mutating request intercepted. Results on both User-Agents:
     - the Open Quest confirm text is exact; dismissing it sends no POST and accepting it sends one, which is aborted;
     - the remove-player confirm names the player; dismissing it sends no DELETE; accepting it sends one DELETE carrying the antiforgery token, intercepted;
     - the player-limit alert fires.
   - Delete-quest was proven live on throwaway quests in 95-12 (dismiss keeps the quest; accept deletes it and Manage then answers 404). `quest-manage.js` was not changed after that.
   - Proposed-date add/remove/renumber was proven in 95-10 and 95-11 on both layouts, with the POST intercepted. `quest-form.js` is unchanged since 015673ab.
   - **Verified.**

The remaining human item is the real-phone UAT checklist above, which includes WINDOWS #32 (the Rewards disclosure, which no fixture has). That is why the status stays `human_needed`: it is a real-device check, not a code gap.

## Human verification (2026-11-12)

The three runtime behaviours above were re-run in a browser by the orchestrator and passed; the local UAT (95-UAT.md items 1-11) passed under phone User-Agents; the real-phone checklist and the real-device calendar subscription (items 12-13) were confirmed by the user against production after the v6.0.0 deploy. Status set to passed.
