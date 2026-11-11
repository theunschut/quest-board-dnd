---
phase: 95-quests-and-quest-log-ui-conversion
plan: 18
subsystem: gate-2-review
tags: [gate-2, after-sweep, review-sheet, accessibility-bar, seal-alt, markdown-hint, release-handoff]
requires:
  - phase: 95-17
    provides: the closed, counted area and the merged allow chain used for the AFTER comparison
provides:
  - the AFTER capture of every manifest row under both User-Agents with every probe, compared with BEFORE and with the previous sweep
  - the single Gate 2 review sheet and its self-contained review branch
  - the operator's Gate 2 answers, three gap fixes closed from them, and the recorded follow-ups
affects: [Phase 96 landing page, Phase 97 calendar, Phase 98 characters and contacts, phase verification, release]
tech-stack:
  added: []
  patterns: [a hint carries its own class when page sheets repaint the framework's help-text class with an important flag, decorative images take an empty alt when the link already names the fact, same-data re-sweep of only the affected rows to prove a gap fix]
key-files:
  created: []
  modified:
    - QuestBoard.Service/wwwroot/css/markdown-editor.css
    - QuestBoard.Service/Views/Shared/_MarkdownEditor.cshtml
    - QuestBoard.Service/Views/Quest/Index.cshtml
    - QuestBoard.Service/Views/Quest/Manage.Mobile.cshtml
    - QuestBoard.IntegrationTests/Tests/MarkdownEditorMarkupTests.cs
    - QuestBoard.IntegrationTests/Tests/QuestBoardEmptyStateTests.cs
    - QuestBoard.IntegrationTests/Controllers/QuestManageDmControlsTests.cs
key-decisions:
  - "Gate 2 approved; seals take an empty alt (the link name already carries the status)."
  - "The Markdown hint gets its own class and the foundation's heading tone instead of an exception: it is this phase's own element, and a colour rule on the framework's help-text class cannot beat the foreign phone sheets' important-flagged rules."
  - "The five calendar weekday header entries stand as operator-approved exceptions; their figures are unchanged."
requirements-completed: [D-16, D-18, D-19, D-20, D-24, DoC-5, DoC-6, DoC-7, DoC-10]
duration: about 5 h
completed: 2026-11-11
status: complete
actuals:
  tokens: 2250
  tasks: 3
  commits: 3
---

# Phase 95 Plan 18: AFTER sweep, Gate 2 review sheet and Gate 2 Summary

The whole Quests and Quest Log area was captured AFTER under both User-Agents and held to the accessibility bar with zero hard failures; the operator approved Gate 2 on the single before/after sheet, and three of their answers became one-line gap fixes (Markdown hint contrast, empty seal alt, full "Assistant DM" on phone Manage) that were re-swept and tested green. The plan's own changes are scratch artefacts outside the repository; the three gap commits below are the only repository changes.

Scratch root `QBR=~/qb-ui-review/phase-95`. Review branch `feature/phase-95-gate2-review`, commit `e665b4937dad21921dd3c08c480064ce574d6676` (pushed, kept): `README.md`, `gate2/index.html` and 330 downscaled JPEGs, no symlinks and no localhost links.

## AFTER totals against BEFORE

`node tools/sweep.mjs --pages all --out after/final` on the working tree (port 8000): 50 rows (37 phase, 13 foreign), 2 User-Agents, every width, 216 records (the selftest row captured with `--merge`), all 200, every mobile record `mobileLayout: true`. 100 pairs on the sheet (74 phase, 26 foreign appendix).

| Measure (phase pages, content area) | BEFORE | AFTER |
|---|---|---|
| Contrast failures, desktop (elements below 4.5:1) | 637 | 70, all the excepted calendar weekday header |
| Contrast failures, phone | 378 | 0 |
| Failures outside an exception | n/a | 0 |
| Phone targets under 44px | 111 | 9 (the editor text-area proxy, also in BEFORE) |
| Page states scrolling sideways, desktop / phone | 6 / 0 | 0 / 0 |
| Console and page errors (desktop) | 4 | 4 (character picture 404, unchanged) |
| Tab stops without a focus ring, unnamed controls, poster text beyond its box | n/a | 0, 0, 0 |
| Poster-glyph floor violations | n/a | 0 |

- Against the previous plan's capture of the same data (`pair-compare.py`): 213 of 214 identical in pixels and every probe field, one differs by a single pixel.
- The plan's literal compare against BEFORE still exits 1 (28 failing rows) for exactly the reasons recorded by 95-16 and 95-17: the phone board's renamed controls, the Markdown hint on foreign forms (closed below), and foreign mobile rows whose layout and sheets this phase cannot reach. After the hint fix the hint is the only item of that list that this phase owned.
- Editor Tab probe: focus leaves the editor on every editor form (Quest Create, Edit, CreateFollowUp, EditRecap, Characters, Events, Shop, DM profile) and on Contacts Create and Edit through a separate probe. The poster overflow probe passes over the 40, 90, 160 and 200 character titles and the 41-character DM name at 1440 and 320 (26 posters, 2 at the 16px tier).

## Accessibility bar per page and layout

The per-page table (contrast BEFORE to AFTER, focus ring, unnamed controls, phone targets, sideways scroll, console, editor Tab) for all 74 phase pairs is in `after/final/summary.md` and in section 3 of the sheet. Aggregates:

| Layer of the bar | Result on every phase page, both layouts |
|---|---|
| Contrast 4.5:1 in content | 0 outside exceptions; the only excepted element is the Quest Details calendar weekday header (desktop, 3.76:1) |
| Two-tone focus ring on each interactive kind | drawn on all 22 probes (card link, radio vote, select, button, link button, text input, checkbox, editor, disclosure, danger button, move select), both layouts; frames in `after/final/frames/` |
| 44px phone targets | 0 except the editor text-area proxy (24px high, the box around it is tappable; toolbar buttons are 44x44 where they were 30px wide) |
| No sideways scroll at 320 (desktop) and 390 (phone) | none on any phase page |
| Reduced motion | the poster hover lift and the Details button hover do not move under `prefers-reduced-motion: reduce` (transition 1e-05s), and lift 4px without it |
| Names on icon-only controls, alt text | 0 unnamed controls; 0 images without an alt attribute across 54 page and layout combinations |
| Status never colour alone | poster link names carry the status, badges carry words and icons |
| Top bar at 992, 1200, 1280 (verification only) | 56px bar on both pages; collapsed menu at 992, one row of 7 links at 1200 and 1280, no clipped link, no sideways scroll |

## 390 label measurements (D-16)

| Label | At 390 | At 320 | Result |
|---|---|---|---|
| `Assistant DM` in the Details participant rows | badge 130x29, 1 line | same | fits |
| `Create Follow-Up Quest` on phone Manage | button 324x44, 1 line | 254x44, 1 line | fits |
| `Assistant DM` in the phone Manage participant rows (`Asst. DM` before the gap fix) | 108px badge, name column 208px, 1 line | 108px, name column 138px | fits, changed to the full label |

No slice made an abbreviation exception. The one abbreviation left from before the phase (`Asst. DM` on phone Manage) had never been measured and is fixed below.

## Broken-window measurements, Quests part (register not edited)

- Phone required markers (Quest Create, CreateFollowUp, Edit): minimum contrast 3.36:1 BEFORE, 10.05:1 AFTER (4 markers each).
- 27px quest-title targets: on the phone board all 26 title links are stretched over their card and the smallest link box is 44px high (BEFORE 27px). Fixed for `/quests`; the Platform part of that entry stays open for its own area.

## Gate 2 decision

The operator reviewed the sheet remotely and replied: **approved (seals alt empty)**, with these answers to the ten open items:

| # | Item | Answer | What was done |
|---|---|---|---|
| 1 | Quest Details calendar weekday header at 3.76:1 (Phase 97's `calendar.css`) | accept | Five entries in `contrast-exceptions.json` marked `approved` (operator, Gate 2, 2026-11-11), figures untouched |
| 2 | "Supports Markdown formatting" hint at 4.25:1 on foreign forms | fix now (one-line gap on the shared editor sheet) | Gap fix 1 below |
| 3 | Editor toolbar at 320 as two rows of 44px buttons | accept two rows | Recorded; the sheet comment that claimed one row was corrected in gap fix 1 |
| 4 | Phone Manage departures from the mockup: Delete Quest last, Edit Quest amber when finalized | accept both | Recorded as operator-approved |
| 5 | Status words carried by the card colour on Manage | accept | Recorded |
| 6 | Rewards disclosure never exercised by a fixture | accept with a UAT line | Register entry 32 and the UAT line below |
| 7 | Seal alt text | **seals alt empty** | Gap fix 2 below |
| 8 | Phone Manage `Asst. DM` | full label (one-line gap) | Gap fix 3 below |
| 9 | Character picture 404 on desktop pages (audit A-quests-017, S3) | accept, follow up in the characters phase | Register entry 31 |
| 10 | Editor text-area proxy measured at 24px on phone | accept | Recorded |

The poster-glyph-floor entries and the Poster5 320px waiver (register entry 30) were already decided at Gate 1 and by the operator and stood unchanged: AFTER p1 2.12/1.71, p2 3.5/2.99, p4 2.42/2.25, p5 3.33/3.03 (2.28/2.17 at the 320px window against its own 2.03/1.92 floor), p6 2.69/2.37, all above their floors; `r1` was not drawn on the review fixtures. No rejected pairs, so there is no "Gate 2 rejections" section and the plan does not end gaps-found.

## Gap fixes and their measurements

All three on `milestone/v9-rolling-improvements`, one commit each, hooks on, nothing pushed.

1. **`d1a8e2cc` Markdown hint** (`markdown-editor.css`, `_MarkdownEditor.cshtml`, `MarkdownEditorMarkupTests`). A colour rule on the framework's help-text class worked on every desktop host and on the Quest forms, but four foreign phone sheets (Characters, Shop, DM profile, Contacts) repaint every help line in faded parchment with an important flag that a layered rule cannot beat, and the card colour alone measures only 4.34:1 on their unscrimmed glass. The hint therefore has its own class (`markdown-editor-hint`), styled in `markdown-editor.css` with tokens only (`--qb-fg-heading`, the tone the foundation names for text on cards that are not yet scrimmed). The markup test pins the new class with the same intent (id, sentence, linked to the editor). Measured over the 10 editor hosts x both layouts x 2 widths: the hint is 5.31:1 on every desktop host and on the phone Quest, Events and Recap forms, and 4.93:1 on the phone Characters, Shop, DM profile and Contacts forms (minimum 4.93:1). Hint contrast failures across the 27 re-swept rows: 24 before, 0 after.
2. **`0ee331b1` Seals** (`Quest/Index.cshtml`, `QuestBoardEmptyStateTests`). Both wax seals have `alt=""`; the test now asserts both seals are decorative, the old alt texts are gone, and the link name still carries `finalized, signed up`. The live board shows 7 seals, all `alt=""`, with link names like "The Bell Beneath Greywater Stair, finalized, signed up".
3. **`57bf24e1` Assistant DM** (`Manage.Mobile.cshtml`, `QuestManageDmControlsTests`). `Asst. DM` becomes `Assistant DM`; a new fact seeds a selected Assistant DM and pins the full label on the phone layout.

**Re-check.** `dotnet build`: 0 errors. `dotnet test` (full, after all three commits): unit 1309 passed; integration 1796 passed, 11 skipped (pre-existing), 0 failed (the previous plan's 1795 plus the new fact). A first full run died with the known transient `Internal CLR error (0x80131506)` before running anything and passed unchanged on the single re-run. Re-sweep of 27 rows (editor hosts including the foreign forms, board, Manage, Quest Log recap, 119 captures, both User-Agents, all 200) against `after/final` (`after/gate2-fixes/`): 98 identical; the other 21 are the foreign form rows, differing only by the hint (contrast-failure count down by one or two each, nothing up), and one phone follow-up control key that embeds typed editor text. Poster glyph figures on the board rows are identical to `after/final`; poster-glyph-floor violations 0; residual contrast on phase pages 0.

## Follow-ups recorded

- Register entry 31 (`.planning/WINDOWS.md`, open): the character picture request that answers 404 on Quest Details and Manage (audit A-quests-017, S3), for the characters phase.
- Register entry 32 (open, UAT): "phone: open a quest with Rewards on Manage and expand the disclosure" (no review fixture has Rewards).
- Phone UAT checklist for the phase (carried from the plan's human-check): on a real phone open `/quests`, a Quest Details page with voting, choose Spectator and back, open the character modal, open Quest Create and tab through the description editor, open Quest Manage and expand Description and Rewards, the Quest Log and a log entry, and the Move page as SuperAdmin; confirm the phone layout is served, every control is at least 44px, nothing scrolls sideways at 390, Spectator hides only the voting, the editor lets focus leave, and the disclosures announce expanded or collapsed.
- Unchanged and not Phase 95's: remaining contrast failures on foreign pages (each owned by its later phase); 51 decorative warning icons without `aria-hidden` in the shared calendar partial's day cells (Phase 97).

## Seal-alt answer

Seals keep no alt text: `alt=""` (applied, gap fix 2).

## Release handoff

The phase ships as one release through the existing draft-release `deploy` approval gate, with deployment timing left to the operator. Nothing new was built for release. `docker-publish.yml` publishes the GHCR image on every tag push regardless of that approval, so holding the release holds the production deployment only, not the public image. No tag was created. The only push was the review branch `feature/phase-95-gate2-review`, at the coordinator's request; no application branch was pushed.

## Deviations from Plan

**1. [Process] The plan's literal verify compare still exits 1.** Same cause as 95-17 (recorded above); the python half passes and the semantic comparison against the previous sweep is clean.

**2. [Process] Extra scratch tooling.** `gate2-sheet.mjs` (as the plan says), plus `gate2-extra.mjs`, `gate2-extra2.mjs`, `gate2-alt.mjs`, `gate2-crops.mjs`, `gate2-images.py`, `hint-probe.mjs`, `hint-measure.mjs`. The first `gate2-extra.mjs` run had probe bugs in four sections; the corrected runs produced the figures above.

**3. [Process] Review branch trailer.** The review commit and the three gap commits end with `Co-Authored-By: Claude Sonnet 5.5`, the model that wrote them, not the Opus line the relayed message named (the same choice the Gate 1 branch commit made).

**4. [Rule 2 - Missing critical] Markup change for the hint.** The gap was relayed as a CSS-only change inside `markdown-editor.css`; measurement showed that cannot reach the foreign phone forms, so the partial's class changed and its test moved with it (same intent). Listed under gap fix 1.

**5. [Rule 1 - Bug] Stale comment.** The editor sheet's header comment said all seven toolbar buttons fit one row at 320; they wrap to two (measured). Corrected in gap fix 1.

**6. [Process] The plan's repository-unchanged line.** The plan expected no repository change; the operator's answers added three small gap commits and the planning records.

## Auth gates

None.

## Known Stubs

None.

## Threat Flags

None new. T-95-43: the sheet and captures use fantasy fixture data only and stayed in the scratch folder and on the review branch. T-95-44: the release handoff and GHCR caveat are stated above; no tag was created.

## Self-Check: PASSED

Verified: `gate2/index.html` exists with 100 `data-pair` ids and all 30 finding ids; commits `d1a8e2cc`, `0ee331b1`, `57bf24e1` and review commit `e665b493` exist; the markup, board and Manage tests pass; full build and test green; `contrast-exceptions.json` figures are identical to the pre-approval copy (`logs/contrast-exceptions.pre-gate2.json`); working-tree app left running on port 8000, baseline on 8001.
