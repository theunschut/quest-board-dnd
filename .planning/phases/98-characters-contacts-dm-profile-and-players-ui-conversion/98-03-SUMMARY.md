---
phase: 98-characters-contacts-dm-profile-and-players-ui-conversion
plan: 03
subsystem: review-baseline
tags: [ui-review, playwright, baseline, manifest, before-capture, test-inventory]
requires: [98-01, 98-02]
provides:
  - frozen baseline of the starting commit served on port 8311 (baseline-src, BASELINE_SHA)
  - manifest.json with 54 rows addressed by stable ids, 0 afterOnly
  - BEFORE report and 217 screenshots under both User-Agents, plus crop dialog, editor keyboard and Dungeon Master date records
  - before/summary.md with a BEFORE value per measurable owned audit finding
  - tests-before.txt with the starting test inventory per project
affects: [98-04, all later Phase 98 plans]
tech-stack:
  added: []
  patterns: [manifest rows built from fixtures.json keys; sweep actions setFile, setValue, domClick added; BEFORE captured from the current branch, not reused from Phase 94]
key-files:
  created: []
  modified: []
decisions:
  - "The error rows that would reach a write are produced without one: the Dungeon Master edit page's error state is an over-size picture the page refuses client-side, because the bio editor caps its own length"
  - "ql-avatar points at quest.played (a played one-shot quest whose Adventurers list shows the fallback avatar); the closed campaign quest is the extra row ql-avatar-campaign"
metrics:
  tasks: 3
  commits: 1
status: complete
actuals:
  tokens: 10000
  tasks: 3
  commits: 1
---

# Phase 98 Plan 03: Frozen baseline, manifest, test inventory and BEFORE captures Summary

HEAD at the start of this plan (`961fff7e47698295feb5cf87d289987b997e7e3f`, a descendant of `c781090c`) is frozen in `~/qb-ui-review/phase-98/baseline-src/` and served on 8311; a 54-row manifest of every in-scope page state was captured from it under the desktop and Pixel 7 User-Agents (217 captures, every fixture assert passed), with the crop dialog, editor keyboard and Dungeon Master date records and a BEFORE value per measurable owned audit finding. No repository file was created or changed; the only commit is this summary. No tests were run (only listed).

## BASELINE_SHA

`961fff7e47698295feb5cf87d289987b997e7e3f` in `baseline-src/BASELINE_SHA`. The repository source trees were clean before the archive. The baseline runs from `git archive HEAD` on 8311 with the forced email overrides verified in the listener's environment; a second `run-app.mjs` against the repository tree on 8311 refused (it serves another tree).

## Tasks

1. **Tracer:** baseline frozen and started, `manifest.mjs` rewritten, `chars-index` swept from 8311 under both User-Agents (tracer gate: verified end to end, expansion went ahead). Because the full manifest was written in one file, `manifest.json` already held all rows when the tracer ran; only `chars-index` was swept for the gate.
2. **Full BEFORE:** all 54 rows swept (`--pages phase --before`): 217 captures (desktop 1440 and 320, 768 on `cat-manage`, phone 412 and 390). `before/dm-dates.json` recorded. 8310 stopped (`stop-app.mjs`), 8311 left running. Test inventory recorded.
3. **Records:** `crop-probe.mjs` (10 records), `editor-trap.mjs` (16 records, 8 editors over six pages by two User-Agents), `before/summary.md` written by `tools/before-summary-98.py`.

## Manifest

54 rows, 0 afterOnly. `ql-avatar` is present: `quest.logged` is available (see 98-02). `ql-avatar` captures `quest.played` (QuestLog/Details of the played one-shot quest, p98player on P98 Audit Characters), where the Adventurers list shows the no-picture avatar; `ql-avatar-campaign` captures `quest.logged` on P98 Audit Campaign. Ids: `chars-index`, `chars-index-dm`, `chars-index-empty`; `char-details-{owner,dead,dm,bare}`; `char-create`, `char-create-errors`, `char-edit`, `char-edit-nopic`; `crop-{char-create,char-edit-recrop,contact-create,contact-edit,dm-edit}`; `contacts-index`, `-player`, `-filtered`, `-nomatch`, `-empty`, `contacts-filter-open`; `contact-details-{full,bare,hidden,player}`, `contact-note-edit`, `contact-create`, `-errors`, `-nocat`, `contact-edit`, `-nocat`; `cat-manage`, `cat-manage-empty`, `cat-edit`; `dm-profile-{full,own,bare}`, `dm-editprofile`, `dm-editprofile-errors`; `players-index`, `-nolink`, `-empty`, `-nodm`; `qd-avatar`, `qm-avatar`, `ql-avatar`, `ql-avatar-campaign`; `form-quest-{create,create-errors,edit,followup}`, `form-event-{create,edit}`. `form-event-edit` opens the existing Audit Board event 43 (a Phase 97 review event) by GET only.

## Test inventory (`tests-before.txt`)

- QuestBoard.UnitTests: `count: 1401`
- QuestBoard.IntegrationTests: `count: 2024` (2022 unique names; theory cases list under one name)

## BEFORE headline numbers

- Contrast failures at B-max: 168 of 217 captures carry at least one; the shared parchment-on-glass 4.34 is the most common (see `before/summary.md` per row).
- Horizontal overflow: `cat-manage` desktop 320 (scrollWidth 393); `contacts-*` desktop 320 (scrollWidth 485, five rows); `contact-details-full`, `-player`, `contact-note-edit` on the phone at 412 and 390 (scrollWidth 784 and 783, the unbroken long note string).
- Crop dialog: title 15.52:1, instruction 11.79:1, Discard 6.09, Use This Crop 8.92 on all five hosts and both User-Agents; stage 766x400 desktop, 362x320 phone; Tab reaches only Close, Discard Photo and Use This Crop; arrow keys do not move the selection; no non-drag control.
- Editor keyboard: Tab and Shift+Tab leave the editor, Escape leaves focus usable, and the commit or Add Note button is reached by Tab in all 16 records.
- Dungeon Master dates (`dm-dates.json`): desktop "Dec 04, 2026", empty cell for the undated quest, "Sept 30, 2026"; phone "Dec 04, 2026", "Date TBD", "Sept 30, 2026".
- The no-picture avatar loads `/Characters/GetCroppedPicture/3016` and gets a 404 on `qd-avatar`, `qm-avatar` and `ql-avatar` (the fallback works through an error handler); this console error is the BEFORE baseline for those rows, not a regression.

## BEFORE value per owned audit finding (IDs stay in planning and scratch records)

| Finding | BEFORE |
|---|---|
| A-characters-001 | crop title 15.52, instruction 11.79 on every host: no failure on this branch |
| A-characters-003 | phone Edit Character label 1.68:1 (still failing) |
| A-characters-004 | phone Edit Contact label 1.68:1 (still failing) |
| A-characters-005 | phone Category Name label 4.34:1 (was 1.77) |
| A-characters-006 | Tab leaves the editor on all six pages (the trap is already gone) |
| A-characters-007 | focus order Close, Discard, Use This Crop; selection not movable by keyboard; no non-drag control |
| A-characters-008 | `.form-text` and `small.text-muted` minimum 4.25 desktop; 2.99 and 3.94 phone |
| A-characters-010 | desktop required marker and validation text 3.63:1 (phone 3.36); the Quest form measures 10.05 |
| A-characters-017 | `cat-manage` desktop 320 overflows, scrollWidth 393 |
| A-characters-018 | desktop Retired badge 3.37, class and level lines 3.47 and 3.69; phone dead and retired rows 2.26 and 1.8 |
| A-characters-019 | phone note text 1.01:1 (parchment on the near-white panel, the notes are unreadable) and the page overflows to 784; desktop note heading 1.07:1 |

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] The Dungeon Master edit error row saved the form (five identical re-saves)**
- **Found during:** Task 2
- **Issue:** the plan's `dm-editprofile-errors` assumed an over-long bio would be rejected. The bio editor caps its own length, so the submit posted the unchanged original bio and the app saved it and redirected to the profile. Five such submits ran on the p98dm profile (four sweep captures plus one debug run); the saved values were identical to the existing ones, and the profile still shows the same bio and photo.
- **Fix:** the row now sets a 5.8 MB picture on the file input, which the page refuses client-side (`#fileSizeError` shown) with nothing submitted; `setFile` was added to `sweep.mjs`. The write cannot recur.
- **Files modified:** `~/qb-ui-review/phase-98/tools/manifest.mjs`, `tools/sweep.mjs` (outside the repository)

**2. [Rule 3 - Blocking] Quest edit title field and Playwright click on the phone note button**
- The Quest edit field is named `Quest.Title`, not `Title`; the assert was corrected. At 390 wide Playwright's own scroll-then-click cannot hit the first note's Edit button (the page smooth-scrolls, another element intercepts); a `domClick` action (scroll into view, `el.click()`) was added and the page itself is fine (`elementFromPoint` returns the button).

**3. [Rule 1 - Bug] Phone filter assert and Players no-link assert read hidden or absent text**
- `Clear Filters` sits in the closed offcanvas on the phone (so `innerText` misses it): the assert uses `textContent`. The Players no-link row asserts the Dungeon Master name and the absence of a profile link instead of a heading word the phone twin lacks.

### Plan interpretations and additions

**4.** `ql-avatar` captures `quest.played`, plus the extra row `ql-avatar-campaign` for `quest.logged`, following the 98-02 note that the campaign Quest Log hides the Adventurers list.

**5.** The editor record does not include a note's edit form opened on Contact Details (the add-note editor is recorded); `contact-note-edit` exists as a manifest row for the page state.

**6.** New scratch tools beyond the plan's three: `dm-dates.mjs` and `before-summary-98.py` (outside the repository); fixture files `fixtures-files/test-crop.png` and `too-big.png`.

No repository source file was created or changed.

## Known Stubs

None.

## Threat Flags

None. Captures only issued GET requests, client-side crop dialogs and validator-rejected empty submits (character, contact and Quest create); the one unintended write is deviation 1, now removed. No request to `/feeds/calendar/` (every capture context aborts it) and no email left the machine (8311 runs with forced suppression and SMTP 127.0.0.1:1025).

## Self-Check: PASSED

- Present: `baseline-src/BASELINE_SHA`, `manifest.json`, `tools/{manifest,crop-probe,editor-trap,dm-dates}.mjs`, `before/{report.json,summary.md,crop-probe.json,editor-trap.json,dm-dates.json}`, `before/{desktop,mobile}` (109 and 108 PNGs, one per capture), `tests-before.txt`.
- Task 1 and Task 2 automated checks printed `ok`; the Task 3 check printed `ok` and `clean`; `grep -c "A-characters-0" before/summary.md` is 11.
- `git status` clean for all source trees before this summary; port 8311 answers 200; 8310 is stopped.
