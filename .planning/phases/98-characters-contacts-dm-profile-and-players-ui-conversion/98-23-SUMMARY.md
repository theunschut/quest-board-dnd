---
phase: 98-characters-contacts-dm-profile-and-players-ui-conversion
plan: 23
subsystem: gate2-review-and-release-handoff
tags: [ui-review, gate-2, operator-decision, exceptions, gap-closure, accessibility, release-handoff]
requires: [98-22]
provides:
  - "The Gate 2 package (index.html, README.md, answers.json, img/), sheet v2, under ~/qb-ui-review/phase-98/gate2/: 227 before and after pairs, 535 JPEGs, passing the hygiene check; v1 kept in gate2-v1"
  - "gate2/answers.json (operator answers, versions v1 and v2, v2 approval) and contrast-exceptions.json with every entry and decision answered"
  - "The e7 gap closure: the picture file inputs on the five host views have an accessible name (5a6f7c84) with a rendered test under both layouts"
  - "tools/build-gate2.py (phase 98 builder with a v2 mode) and tools/a11y-table-98-23.py"
affects: []
tech-stack:
  added: []
  patterns: [one self-contained sheet with relative JPEG paths and eager images (lazy images made the file:// decode check hang); gap closure re-captures only the affected rows and merges them into the final AFTER record]
key-files:
  created:
    - .planning/phases/98-characters-contacts-dm-profile-and-players-ui-conversion/98-23-SUMMARY.md
  modified:
    - QuestBoard.Service/Views/Characters/Create.cshtml
    - QuestBoard.Service/Views/Characters/Edit.cshtml
    - QuestBoard.Service/Views/Contacts/Create.cshtml
    - QuestBoard.Service/Views/Contacts/Edit.cshtml
    - QuestBoard.Service/Views/DungeonMaster/EditProfile.cshtml
    - QuestBoard.IntegrationTests/Tests/ImageCropDialogRenderTests.cs
    - .planning/phases/98-characters-contacts-dm-profile-and-players-ui-conversion/deferred-items.md
decisions:
  - "Gate 2 approved on sheet v2: every pair approved, e1, e3, e4, e5, e6, e8 and d3 accepted, e2 retired, e7 fixed, d1 kept as is, d2 a follow-up phase, s1 stack scoring, l1 gold form title, c1 dates accepted, fixtures kept"
  - "Production runs with LANG=C and no application culture, so the server formats with the invariant culture; the culture assumption of the Dungeon Master date slice is closed"
  - "The picture input is named by its visible label (label for with the input's own id); the id stays so the crop module is untouched"
metrics:
  tasks: 3
  commits: 2
status: complete
actuals:
  tokens: 60000
  tasks: 3
  commits: 2
---

# Phase 98 Plan 23: Gate 2 and release hand-off Summary

Gate 2 (the operator's sign-off on every before and after pair and every exception of the Characters, Contacts, Dungeon Master profile and Players conversion) was held on a local 227-pair sheet and approved on its second version, after one gap closure (the picture file inputs now have an accessible name); the phase is ready for one release, v6.3.0, through a pull request into main.

## Commits

- `5a6f7c84` fix(98-23): name the picture file inputs through their visible labels (the e7 gap closure)
- the final docs commit carries this summary, the state and roadmap files and the deferred-items record

Tasks 1 and 2 changed no repository file: the sheet, its images and its builder live in the scratch package, never committed.

## Gate 2 decision

- **v1** (`~/qb-ui-review/phase-98/gate2-v1/`): 227 pairs (217 sweep records, 54 manifest rows by both User-Agents at every width, plus 10 crop dialog pairs), each relaid-out page beside its approved Gate 1 frame, the crop, quest-avatar, editors, quest-and-events-forms, look-check, findings, inherited-questions and decisions sections, and the BEFORE statement (BASELINE_SHA 961fff7e, already the v6.0.2 card surface) with the phase's own changes listed separately. Hygiene passed (535 JPEGs, only `index.html`, `README.md`, `img/`; zero local-address strings; opens from file:// with no failed request).
- **Operator answer (v1):** "gate is approved", with e7 answered "Fix (Recommended)", relayed by the orchestrator session. Reply line recorded: `approved; pairs:approved e1:accept e2:retire e3:accept e4:accept e5:accept e6:accept e7:fix e8:accept d1:as-is d2:follow-up d3:accept s1:stack l1:approved c1:accept fixtures:keep`.
- **v2** (`~/qb-ui-review/phase-98/gate2/`): rebuilt after the e7 fix; the 16 re-captured rows are marked on the sheet; every answer is recorded in the decisions table. **The operator approved v2** ("approved", relayed by the orchestrator session after checking it). `gate2/answers.json` holds `approved`, each answer with its source, the fixtures answer, `versions` v1 and v2 and `v2Approval`.
- The package stays local; the orchestrator published the sheet (without `answers.json`) as the orphan review branch `review/phase-98-gate2` (2ddb3c08 for v2; the v1 branch b87f0340 was deleted at the operator's request). This plan pushed nothing.

## The e7 gap closure

The picture file inputs on the four host forms (five views) had no accessible name: each label's `for` named the model property while the input's `id` was overridden, so `input.labels` was empty (64 unnamed-control records over 16 sweep rows). The defect was present before the phase.

- Fix (`5a6f7c84`): each label carries `for` with its input's own id on Characters Create and Edit (`profilePictureInput`), Contacts Create and Edit (`contactImageInput`) and Dungeon Master Edit Profile (`dmProfilePictureInput`). The ids are unchanged, so the crop module and the rest of the page are untouched; nothing moves visibly.
- Test: `ImageCropDialogRenderTests.Host_PictureInputHasAnAccessibleNameFromItsVisibleLabel`, a theory over the five hosts under both User-Agents (10 cases): the input has an id, exactly one label points at it with text, and no label points at the model property name. With the attribute removed from Characters Create the test fails on both layouts.
- Suites: unit 1416 passed; integration 2300 passed, 0 failed, 16 skipped (the SQL-backed migrator suite, enabled only by its environment switch, which passed 11 of 11 at 98-22).
- Re-capture: the 16 rows (11 form rows and 5 crop host rows, 64 captures) merged into the final AFTER record: 217 records, 0 not 200, 0 console, page or failed-request records, unnamed visible controls 64 to 0, every contrast, target, ring and overflow fact equal to v1; screenshots identical or within 19/255 per channel (background noise). `readability-compare.py` against BEFORE (with 16 rename entries for the intended accessible-name change): 217 compared, 0 failing rows, 0 poster-glyph violations, 0 residual contrast rows, `ok`.
- The name probe was re-run on those rows (`iconOnlyNoName` 1 to 0 on all 64 records) and `a11y.md` was regenerated.

## Exceptions

Each item with the operator's answer, source "operator, relayed by the orchestrator session"; `contrast-exceptions.json` records every one with `approved.status`.

| Id | Item | Answer |
|---|---|---|
| e1 | Eight Cropper handles at 15 by 15 px, an exception to WCAG 2.5.8 Target Size (Minimum), met only through the control row as the equivalent control (and the arrow keys) | accepted |
| e2 | Phase 95 Markdown editor phone toolbar at 30 to 34 px (covers only that toolbar, never a control this phase built); measured 44 by 44 px on all 16 toolbars | retired (moved to `retired`) |
| e3 | Crop selection ring drawn inside the selection (2.4.7, 1.4.11) | accepted |
| e4 | Tagify widget targets: remove buttons 14 px, chips 32 px, typing node 33 px | accepted |
| e5 | Markdown editor input proxy records (24 px phone, 1000 by 16 desktop) | accepted |
| e6 | 22 px desktop links (Dungeon Master quest history, character sheet link), 2.5.8 spacing allowance | accepted |
| e7 | Picture file inputs without an accessible name | fix chosen; fixed in `5a6f7c84`; removed from the open entries (moved to `retired` with the fix and its result) |
| e8 | Tagify chip text 1.11:1 in the probe on Contacts Edit (desktop and phone records, one item), painted 7.1:1 | accepted |

Decisions: **d1** the phone Contacts page on a board without categories has no h1: kept as is (approved at Gate 1 item 10), known debt. **d2** the 320 px desktop navbar wrapping over the page title: follow-up phase. **d3** the second ring inside the Tagify box: accepted. **s1** contrast scored with the stack: kept (the single-layer sweep flags 72 rows the pixel check clears at 5.95 to 8.09:1). **l1** the desktop look-check difference: the gold form title, approved. **c1** dates: accepted, and closed (below).

## Owned findings

The 19 S1 and S2 findings of the audit this phase owns (A-characters-001 to A-characters-019), each shown on the sheet with severity, owner, BEFORE and AFTER evidence:

- Closed: A-characters-001 and 003 to 006 (S1) and 007 to 019 (S2). The contrast findings carry the Tagify chip probe limit as the accepted item e8; the crop findings carry e1 and e3; the editor finding carries the retired e2 and the accepted e5.
- Carried: A-characters-002 (S1), the character select dialog, went to Phase 95 by Q-135 and is unchanged here.
- None of the 19 stands as an open exception. The 36 S3 and S4 findings are summarised on the sheet as addressed in passing (with the plan) except A-characters-022, carried by the deliberate phone-only Filter Tags button (Q-53).
- Inherited questions Q-50 to Q-55, Q-97 to Q-102, Q-117 to Q-119 and Q-132 are shown with their outcome and evidence; Q-135 is out of scope.

## c1: dates and the production culture

After the page script runs, `site.js` re-formats wall-clock text in the viewer's own locale (never the timezone); that is how every other wall-clock date already reads. The orchestrator read production over SSH: `LANG=C` and no application culture configured, so the server formats with the invariant culture. The culture assumption of the Dungeon Master date slice is closed; the approved first-paint wording (`Sep`, no leading zero, `Date TBD` on the desktop) does not depend on it.

## Follow-ups and remaining deferred items

- **d2: the 320 px desktop navbar finding**, a shell (`_Layout`) issue out of this phase's scope by Gate 1 answer a4: at 320 px on the desktop layout the brand and menu toggle wrap, the fixed bar is 90 px and covers the top of the page title. Follow-up phase (it touches every page).
- Known debt, accepted at Gate 2: the phone Contacts empty-board heading (d1), the Tagify 14 px remove buttons (e4), the inner Tagify focus ring (d3), the 22 px desktop links (e6).
- `deferred-items.md` marks the picture-input item and the culture check closed.
- The WINDOWS ledger does not list the picture-input item, so no ledger entry changed in this plan.

## Release handoff

The phase is ready for one release, **v6.3.0**, through a pull request from `milestone/v9-rolling-improvements` into `main` and the existing draft-release deploy approval; the timing is the operator's. Every slice is on the milestone branch; the orchestrator pushes it and opens the pull request. This plan opened no pull request, created no tag and pushed nothing.

**Caveat repeated:** `docker-publish.yml` publishes the image on every tag push regardless of the draft-release approval, so a tag must not be pushed before the operator wants the image published.

Release checks: the production culture check is done (c1 closed). Nothing else is outstanding for the release itself.

## Fixtures for the operator

Kept, as decided (answer `fixtures:keep`). Created through the app's own forms; nothing written by SQL.

- Accounts (3): `p98player` (Player), `p98dm` (Dungeon Master), `p98empty` (Dungeon Master of the empty board).
- Boards (5): P98 Audit Characters, P98 Audit Empty, P98 Audit No Categories, P98 Audit No Dungeon Masters, P98 Audit Campaign.
- Their data: 8 characters (a picture or none, Main, Dead, Retired, a long name), 3 contact categories, 6 contacts, 1 long note, the Dungeon Master profiles, and 4 quests (finalized, undated, played, logged). The full list is on the sheet and in `~/qb-ui-review/phase-98/fixtures.json`.

## Phase UAT checklist

To run by the operator after deploy:

- A real phone with touch: the whole area, including dragging the crop selection and the control row now that the dialog's sheet loads on the phone.
- A screen reader: the crop selection, the control row and the status line read sensibly.
- Quest Details, Quest Manage and Quest Log Details in production with a character that has no picture: the placeholder shows, no picture request is made and the console is clean.

## Deviations from Plan

### Auto-fixed Issues

None in the product beyond the operator-chosen gap closure above.

### Plan interpretations

- The plan's tracer gate (an interactive stop after the first pair) was folded into the one checkpoint after Task 2 on the orchestrator's instruction; the tracer pair was viewed and checked before the full sheet was built.
- The phase 97 builder was rewritten for phase 98 (the old copy kept as a backup) and the sheet loads its images eagerly: lazy images made the hygiene tool's file:// decode check hang.
- Gate 2 answers reached the executor through the orchestrator session, which the task brief allows; they are recorded with that source.
- The optional pixel check (`gate-hygiene.py --ocr`) flags 19 images that show the fixture character's sheet link, an address on a reserved test host with fantasy data; accepted as drawn at Gate 1 and left so at Gate 2. The plain-text hygiene check passes with zero hits.

## Known Stubs

None.

## Threat Flags

None. The only source change adds a `for` attribute to five labels.

## Self-Check: PASSED

- The v2 package (`gate2/index.html`, `README.md`, `answers.json`, `img/`), `tools/build-gate2.py`, `contrast-exceptions.json` and `after/final/report.json` exist; commit `5a6f7c84` exists; the hygiene check on `gate2` printed `hygiene ok: 535 jpeg`; `git status` showed no change beyond the planning records of this plan.
