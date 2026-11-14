---
phase: 98-characters-contacts-dm-profile-and-players-ui-conversion
plan: 22
subsystem: closing-sweep
tags: [ui-conversion, guard-tests, final-sweep, accessibility, exception-record, finding-trace, migrator-suite]
requires: [98-21]
provides:
  - "CharactersConversionGuardTests.PhaseScope_IsFullyConverted, AreaSheets_DoNotRestyleCardTitles and a card-title scanner self-test; the two shared partials and image-crop.css join the converted lists; AreaSheets is the full list of 17 sheets"
  - "BoardPageStaticGuardTests planning-reference scan extended with the two partials and five modules"
  - "counts-after.json (all totals 0 beside the BEFORE totals), the untouched-paths proof, tests-after.txt and tests-renamed.md, contrast-exceptions.json (7 entries and 3 decisions, all pending), after/final (217 records, a11y.md, crop, editor, look-check and focus records), findings-trace.json"
  - "WINDOWS entry 12 fixed on measured contrast; the UI guidelines legacy-sheet list refreshed"
affects: [98-23]
tech-stack:
  added: []
  patterns: [stack-scored contrast checked against painted pixels; exception record with a WCAG criterion and a pending approval per item]
key-files:
  created:
    - .planning/phases/98-characters-contacts-dm-profile-and-players-ui-conversion/deferred-items.md
  modified:
    - QuestBoard.IntegrationTests/Tests/CharactersConversionGuardTests.cs
    - QuestBoard.IntegrationTests/Tests/BoardPageStaticGuardTests.cs
    - .claude/ui-guidelines.md
    - .planning/WINDOWS.md
decisions:
  - "The final sweep is scored with --stack, as every Characters, Contacts, Dungeon Master profile and Players slice was; the figures were checked against real painted pixels (5.95 to 8.09:1 where the single-layer scoring flags 3.27 to 4.25) and the single-layer sweep is kept for comparison"
  - "The Tagify chip text the probe reads at 1.11:1 is a measurement limit (the chip fill is an inset shadow on a pseudo-element); painted 7.1:1; recorded as an exception rather than waved through"
  - "Recorded exception 2 (editor toolbar 30 to 34px) no longer holds: all toolbar buttons measure 44 by 44 px on the six pages under both User-Agents; kept pending so the operator retires it"
  - "MarkdownEditorMarkupTests and MobileViewsTests already covered everything asked and are unchanged"
metrics:
  tasks: 3
  commits: 3
status: complete
actuals:
  tokens: 5500
  tasks: 3
  commits: 3
---

# Phase 98 Plan 22: Closing sweep Summary

The Characters, Contacts, Dungeon Master profile and Players area is guarded as fully converted, every count is zero beside its BEFORE value, the helper, feed and data paths are proven untouched, no test was lost, and the finished build is captured under both User-Agents with the accessibility table, the exception record and the finding trace ready for Gate 2.

## Commits

- `998abb4d` test(98-22): pin the whole area as converted (Task 1, the tracer)
- `5b4e73cb` test(98-22): scan the new modules and partials for planning references and refresh the legacy-sheet note (Task 2)
- `9720649c` docs(98-22): close the required-marker ledger entry on the measured contrast (Task 3)
- the final docs commit carries this summary, the state and roadmap files and `deferred-items.md`

## Task 1: fully converted, counts and untouched paths

`PhaseScope_IsFullyConverted` enumerates the area from disk: every `.cshtml` in `Characters`, `Contacts`, `ContactCategoryManagement` and `Players`, the `Profile*` and `EditProfile*` views of `DungeonMaster`, and the two shared partials (`_ImageCropDialog`, `_CharacterAvatar`); it requires each to be in `ConvertedViews` (and each listed view to still exist), every area sheet to be present and in `ConvertedSheets`, and `UnconvertedByDecision` to be empty. `AreaSheets_DoNotRestyleCardTitles` covers all 17 area sheets with a scanner self-test (`CardTitleScanner_FlagsWhatItForbidsAndAllowsWhatItPermits`). `Layouts_LinkNoAreaSheet` already named all four former global sheets (`characters.css`, `contacts.css`, `dm-profile.css`, `image-crop.css`) and passes. `RetiredFiles` lists all 13 deleted files (8 views, 5 sheets), identical to the diff against `BASELINE_SHA`. The tracer verify (4 guard classes) passed 90 of 90.

Views and sheets: the area is now 21 views (19 pages and 2 partials) and 17 sheets, against 26 views and 13 sheets before (the 8 phone twin views and 5 phone sheets are gone, 3 view files and 9 sheets were added or reshaped).

| Count | BEFORE | AFTER |
|---|---|---|
| inline style attributes in the area's views | 49 | 0 |
| style elements | 0 | 0 |
| inline scripts | 22 | 0 |
| inline event handlers | 11 | 0 |
| hex colours in the area's sheets | 70 | 0 |
| `rgb(` | 243 | 0 |
| important flags | 88 | 0 |
| infinite animations | 1 | 0 |
| utility-colour rules (extra) | 10 | 0 |

Each of the 17 sheets opens exactly one `@layer pages` block. `counts-after.json` records the totals beside BEFORE, the per-file counts and the untouched result.

Untouched paths (from `BASELINE_SHA`, and from the commit before the first phase commit): no change to `HtmlHelperExtensions.cs`, the three feed files, `site.js`, `WallClockMarkupTests.cs`, `LocalTimeMarkupTests.cs`, `TestHostFileWatching.cs`, or anything under `QuestBoard.Domain` or `QuestBoard.Migrator`. Under `QuestBoard.Repository` only `QuestRepository.cs` changed (33 insertions, 2 deletions, no Add, Update, Remove, SaveChanges, ExecuteUpdate, raw SQL, Attach or entity-state call added). Outside Views and wwwroot the only Service file that changed is the new `CharacterAvatarViewModel.cs`. Nothing writes to `Events` or `Quests`.

## Task 2: no test deleted, shared guards, exception record, guideline line

- Inventory (`tests-after.txt`, `tests-renamed.md`): UnitTests 1401 before, 1405 after; IntegrationTests 2022 unique names (2024 listed) before, 2292 (2294) after. Every method name of `tests-before.txt` is present in `tests-after.txt` except `ContactsTagsFormMarkupTests.CreateForm_DesktopUserAgent_RendersTagInputAndPinnedLibrary` and `CreateForm_MobileUserAgent_RendersTagInputAndPinnedLibrary`, merged into one theory over both User-Agents in 98-12 and already logged. Two unit whitespace theory cases list differently only because their parameter text carries a raw line break; both methods are present. No project count dropped.
- `MarkdownEditorMarkupTests` already covers all six editor pages (Characters Create and Edit, Contacts Create and Edit, Dungeon Master profile, Contact details notes) under both User-Agents: nothing to add. `MobileViewsTests`: every area fact asserts a real phone selection or the merged-view shape: unchanged.
- `BoardPageStaticGuardTests`: its list guards the board-page selection feature, not these features; the planning-reference scan (the `CharactersConversionGuardTests` source list) already held the seven new modules and partials. The seven were also added to the board-page scan so both scans cover them (7 new theory cases, none removed).
- `.claude/ui-guidelines.md`: only the unlayered-legacy-sheet list changed. It no longer names the quest and calendar sheets (layered since the quest and events phases); it names `legacy-pages.css`, `mobile.css`, the shop sheets and the admin and platform phone sheets. The area's former global sheets were never named and are all layered.
- Exception record (all `approved.status` pending, each with a WCAG criterion, a measured value and a reason): see "Exceptions recorded for Gate 2".
- Full runs: unit 1416 of 1416; integration 2290 passed, 16 skipped (SQL Server tests that need the flag), 0 failed (2306 total).

## Task 3: the full AFTER, the checks and the ledger

Sweep: `sweep.mjs --pages all --stack` on 8310: 217 records (54 rows, both User-Agents, every listed width), 0 not 200, 0 console or page errors, 0 failed requests, 0 action failures, 0 assert failures. `readability-compare.py` against BEFORE with the merged `allow.json` (40 rows) and `--zero-contrast phase` with the exceptions file: 217 compared, 0 failing rows, 0 poster-glyph violations, 0 residual contrast rows, `ok`.

| Page family | records | in-main contrast failures BEFORE | AFTER | phone small-target records BEFORE | AFTER |
|---|---|---|---|---|---|
| Characters list | 12 | 122 | 0 | 28 | 0 |
| Character details | 16 | 116 | 0 | 0 | 0 |
| Character forms | 16 | 96 | 0 | 12 | 12 |
| Crop dialog hosts | 20 | 88 | 0 | 10 | 11 |
| Contacts list | 24 | 98 | 0 | 27 | 0 |
| Contact details | 20 | 224 | 0 | 10 | 10 |
| Contact forms | 20 | 92 | 12 | 34 | 34 |
| Categories | 13 | 20 | 0 | 0 | 0 |
| Dungeon Master profile | 12 | 76 | 0 | 12 | 0 |
| DM edit profile | 8 | 44 | 0 | 0 | 0 |
| Players | 16 | 36 | 0 | 4 | 0 |
| Foreign (quest and event forms, avatars) | 40 | 0 | 0 | 12 | 12 |
| Total | 217 | 1012 | 12 | 149 | 79 |

Headline numbers:

- Content contrast: 1012 failures BEFORE, 12 AFTER, all 12 the Tagify chip text on `contact-edit` (probe reads 1.11:1; painted 7.1:1, see the exception record).
- Manage Categories at 320 on the desktop layout: BEFORE overflow (scrollWidth 393), AFTER none (scrollWidth 320).
- Crop dialog: title and instruction at 12.98:1 at the lowest of ten records (BEFORE baseline probe 15.52 and 11.79, already above the bar), dialog shown on all ten, no modal-wide failure; five hosts by two User-Agents: 10 of 10 behaviour records pass every check (26 to 28 checks each), keyboard moves the selection, seven non-drag controls.
- Editor keyboard check: 16 of 16 editor records pass (Tab, Shift+Tab, Escape, next control reachable) on all six pages under both User-Agents. Toolbar buttons: 44 by 44 px on every one of 16 toolbars.
- Look check: 20 records (ten page types by two User-Agents) against Direction A: 17 pass outright; the three desktop forms (`char-create`, `contact-create`, `dm-editprofile`) report only `h1.color` (reference parchment, page gold `rgb(255, 193, 7)`), the gold desktop form title approved at the first gate, so the tool prints pass false for that one property and nothing else.
- Phone small targets: 149 BEFORE, 79 AFTER, none new apart from one record on `crop-contact-edit`; all are the Markdown editor's input proxy, the Tagify widget, or two inline links (see exceptions 4, 5 and the inline note).
- Desktop controls under 24px: 133 BEFORE, 90 AFTER (the editor proxy, three Tagify remove buttons, three Dungeon Master quest links and the sheet link); no Players link.
- Reflow: 0 rows with horizontal overflow at 320 desktop or 390 phone, 0 with text-spacing overflow.
- Reduced motion: 0 running animations and 0 transitions over 1ms on every phase row under both User-Agents; 0 infinite animations. 0 focus rings missing over 217 records; 0 focus-obscured stops.
- Text under 14px in main: 69 elements BEFORE, 0 AFTER (smallest badge 14px).
- Focus frames (`focus-frames/`): card, row link and note button on glass; table link and Tagify box on paper; crop selection and crop control on the overlay; 15 frames under both User-Agents, ring present and unclipped on all, and viewed.
- Submit in flight (`inflight.json`): the Characters, Contacts, Dungeon Master and category forms, one per layout, submitted with the POST never sent; the primary button label stays legible at 6.95 to 7.1:1. None of the area's forms opts into the button-locking attribute, so no button disables.
- WINDOWS entry 12: required marker, light `rgb(255, 186, 177)` on the dark chip `rgb(47, 27, 14)`, over the lightest painted pixel: phone 5.71 (`char-create`), 5.71 (errors), 5.90 (`char-edit`), 5.65 (`char-edit-nopic`), 5.88 (`contact-create` and its errors and no-category variants), 5.78 (`contact-edit`), 5.95 (`contact-edit-nocat`); desktop 6.57 or better. Marked fixed; the ledger reason names the values.

Scoring note. Contrast is scored with `--stack` (a card that paints several layers is scored as the stack it is, over the lightest board pixel), the mode the earlier slices used. To check it against reality, `pixel-check.json` hides eight flagged elements' text, screenshots them and scores the real text colour over the lightest painted pixel: 5.95 to 8.09:1 where the single-layer scoring reads 3.27 to 4.25. The single-layer sweep is kept (`after/final-nostack`): it flags 72 rows (368 records) on the Characters and Contacts lists, the Dungeon Master pages and the Dungeon Master form, all elements the pixel check clears.

## Exceptions recorded for Gate 2

Pending, in `~/qb-ui-review/phase-98/contrast-exceptions.json` (`entries`):

1. Eight Cropper handles at 15 by 15 px (re-measured at the final sweep, both User-Agents): WCAG 2.5.8, met only through the equivalent-control allowance (control row and arrow keys).
2. Markdown editor toolbar: the contract's 30 to 34 px no longer holds; measured 44 by 44 px on all 16 toolbars. Recommend retiring.
3. Crop selection ring drawn inset (2 px inside the box, 4 px inset band) because the stage clips: 2.4.7 and 1.4.11.
4. Tagify chips, remove buttons (14 px), typing node (33 px): 2.5.8 and the 44px phone bar; carried from before the phase.
5. Markdown editor input proxy records (24 px phone, 1000 by 16 desktop): sweep artifact of the hidden proxy.
6. Desktop table links 22 px (Dungeon Master quest history, sheet link): 2.5.8 spacing allowance.
7. Picture file inputs whose label does not point at them (four host forms): present before the phase.
8. Tagify chip text, `contact-edit`, both User-Agents (two entries): probe limit, painted 7.1:1.

`decisions`: the phone Contacts page has no `h1` on a board without categories; the desktop navbar at 320 px covers the page title (first-gate item a4, shell finding); the second ring inside the Tagify box (observation). The Dungeon Master date and the Players heading needed no decision entry (both approved at the first gate); no fixture state was unavailable.

## SQL-backed migrator suite

The operator answered yes at the opening checkpoint and supplied the connection through the scratch environment file; it was loaded without printing and the suite run once: `QUESTBOARD_MIGRATOR_TEST_REQUIRED=1 dotnet test QuestBoard.IntegrationTests --filter "FullyQualifiedName~Migrator"`: 11 passed, 0 failed, 0 skipped (35 s). The suite's own scratch databases were the only databases touched; nothing was started, stopped or provisioned, and no connection detail is recorded anywhere.

## Deviations from Plan

### Auto-fixed Issues

None in the product. The plan allows no source change beyond the guard and test files, the guideline line and the ledger.

### Plan interpretations

1. **[Tracer gate]** No auto flag is set and a sequential subagent cannot be resumed, so the tracer gate was the automated re-run of the tracer verify (90 of 90) before Task 2, as in the earlier slices.
2. **[Verify shapes]** The plan's Task 2 inventory check reads indented names and `count:` lines, but the inventory files are not indented and test names themselves contain `count: N`; the equivalent comparison (name sets per project, then method names) was run instead. The Task 3 look-check glob needs every record to pass; three desktop form records fail on `h1.color` only (the approved gold title), checked from their `failures` fields. The behaviour, editor and trap checks read the real list shapes.
3. **[Readability compare]** `--pages phase` is not understood by the tool (it takes ids), so the phase ids were passed explicitly; `--zero-contrast phase` works as documented.
4. **[Exceptions beyond the three]** The sweep found items the contract did not name (exceptions 4 to 8, three decisions); each is recorded with its measurement, reason and pending status instead of being left out of the record.
5. **[Exception 2]** Measured now, the toolbar no longer deviates; the entry was written with the measured value and a retire recommendation rather than copying the old 30 to 34 px.
6. **[Scratch tools]** `counts.py` and `untouched.py` were rewritten for this area; `editor-toolbar`, `tagify-chip`, `pixel-check`, `marker`, `fontsize`, `navbar`, `label-check`, `focus-frames`, `submit-inflight`, `a11y-table` and `findings-trace` scripts are new, all under `~/qb-ui-review/phase-98/tools` outside the repository.

## Known Stubs

None.

## Threat Flags

None. No controller, view model or query changed; `QuestRepository.cs` carries the earlier read-only picture flag only (T-98-51), the inventory comparison accounts for every vanished name (T-98-52), every exception is pending (T-98-53), and the migrator run used only its own scratch databases on the operator's yes (T-98-54).

## Finding trace

Totals: 18 closed, 1 carried (the character select dialog, out of scope), 0 exception among the 19 owned S1 and S2 findings; 35 addressed and 1 carried (the deliberate phone-only filter button) among the 36 S3 and S4 findings; 16 inherited questions done or recorded and one out of scope. The full record is `~/qb-ui-review/phase-98/findings-trace.json`. Finding and question ids live only in planning and scratch files.

### Owned S1 and S2 findings (19)

| Finding | Sev | Owner (plan and task) | Evidence | BEFORE | AFTER | Status |
|---|---|---|---|---|---|---|
| A-characters-001 | S1 | 98-09 Tasks 1 to 3 (drawn in 98-04 Task 1, approved at 98-07 item 1) | after/final/crop-probe.json (ten records, five hosts by two User-Agents) and CropDialogPartial_UsesTheOverlaySurface (green in the full run) | baseline probe: title 15.52:1, instruction 11.79:1 (white on the dark panel; the audit's dark-on-dark reading did not reproduce on the baseline) | title 12.98:1, instruction 12.98:1 at the lowest of ten records, dialog shown on all ten, modal-wide contrast failures 0 | closed |
| A-characters-002 | S1 | Out of scope by Q-135: converted in Phase 95 (95-05) | 95-05-SUMMARY.md; _CharacterSelectModal.cshtml unchanged since BASELINE_SHA (git diff empty) | failing at the audit (Phase 95 owns it) | not changed by this phase | carried |
| A-characters-003 | S1 | 98-11 Task 1 | char-details-owner (phone) in after/98-11 and after/final; CharacterDetailsRenderTests | 36 in-main contrast failures (worst 1.68:1) | 0 in-main contrast failures | closed |
| A-characters-004 | S1 | 98-14 Task 1 | contact-details-full (phone, as p98dm) in after/98-14 and after/final | 36 in-main contrast failures (worst 1.01:1) | 0 in-main contrast failures | closed |
| A-characters-005 | S1 | 98-08 Task 1 | cat-edit (phone) in after/98-08 and after/final | 4 in-main contrast failures (worst 4.34:1) | 0 in-main contrast failures | closed |
| A-characters-006 | S1 | Fixed in Phase 95 (95-04); verified by 98-13, 98-14, 98-15, 98-17 and this plan | after/final/editor-trap.json and MarkdownEditorMarkupTests (six pages by two User-Agents) | baseline already carries the Phase 95 fix: 16 of 16 editor records leave on Tab and Shift+Tab and reach the next control | 16 of 16 records pass (Tab, Shift+Tab, Escape, next control reachable), six pages by two User-Agents | closed |
| A-characters-007 | S2 | 98-09 Tasks 1 to 3 (Q-132; behaviour-checked in 98-04 Task 2) | after/final/behaviour-crop-*.json and ImageCropModule_DrivesTheSelectionThroughItsPublicApi | baseline crop dialog: selection moves by keyboard False, non-drag controls 0 | 10 of 10 behaviour records pass every check; arrow keys move the selection; non-drag controls: Move up, Move down, Move left, Move right, Larger, Smaller, Reset | closed |
| A-characters-008 | S2 | 98-08 Task 1, then every page slice | readability-compare.py --zero-contrast phase on after/final (217 records) | 1012 in-main contrast failures over 54 phase rows | 12 in-main contrast failures, all 12 the Tagify chip probe artifact (painted 7.1:1) recorded as an exception; 0 otherwise | closed |
| A-characters-009 | S2 | The foundation surface; look check 98-06; every page slice | after/final sweep with --stack and pixel-check.json (eight elements over their lightest painted pixel, 5.95 to 8.09:1); lookcheck-*.json (20 records) | labels and body text on glass 4.34:1 at the audit | zero in-main failures; look check 17 of 20 records pass outright and 3 pass but for the approved gold form title | closed |
| A-characters-010 | S2 | 98-08 Task 1; applied in 98-13 Task 1 and 98-17 Task 2 | char-create-errors and contact-create-errors (desktop) in after/final; marker.json (WINDOWS entry 12 measured) | char-create-errors 16 in-main contrast failures (worst 3.63:1); contact-create-errors 10 in-main contrast failures (worst 3.63:1) | char-create-errors 0 in-main contrast failures; contact-create-errors 0 in-main contrast failures; marker 6.57:1 or better desktop, 5.65:1 or better phone | closed |
| A-characters-011 | S2 | Every page slice through the button-role table | after/final sweep zero in-main failures on every phase row | 1012 failures over the phase rows | 12 (Tagify chip artifact only) | closed |
| A-characters-012 | S2 | 98-11 Task 2; 98-20 Task 1 | char-details-owner, char-details-dead, dm-profile-full (desktop) in after/final; fontsize.json | 8 in-main contrast failures over 3 rows (worst 4.25:1); text under 14px: 69 elements over the phase rows (CR, Main, Dead, Retired badges at 12px) | 0 in-main contrast failures over 3 rows; smallest badge 14px; text under 14px: 0 | closed |
| A-characters-013 | S2 | 98-20 Task 1; 98-21 Task 1 | dm-profile-full and players-index (desktop) in after/final | 2 in-main contrast failures over 2 rows (worst 4.25:1) | 0 in-main contrast failures over 2 rows | closed |
| A-characters-014 | S2 | 98-19 Task 1 | contacts-index (both User-Agents) in after/final | 26 in-main contrast failures (worst 1.7:1) | 0 in-main contrast failures | closed |
| A-characters-015 | S2 | 98-18 Task 1; 98-19 Task 1 | focus-frames/focus-frames.json (card, row link on glass); after/98-18 and after/98-19 motion-focus records; a11y.md A-2 and A-3 | no visible focus on character and contact cards (audit) | card (characters, desktop): desktop: ring present, clipped by nothing; card (contacts, desktop): desktop: ring present, clipped by nothing; row link (characters, phone): mobile: ring present, clipped by nothing; row link (contacts, phone): mobile: ring present, clipped by nothing; 0 focus rings missing over 217 records | closed |
| A-characters-016 | S2 | 98-13 Task 2 | focus-frames (Tagify box on paper) and after/98-13 ring record; a11y.md A-2 | tag entry box without a focus indicator (audit) | Tagify box: desktop: ring present, clipped by nothing, mobile: ring present, clipped by nothing; a second ring also draws on the typing node (observation recorded for Gate 2) | closed |
| A-characters-017 | S2 | 98-16 Task 1 (Q-117) | cat-manage desktop 320 in after/98-16 and after/final | overflow at [('desktop', 320, 393)] | overflow at no width; scrollWidth 320 at 320 | closed |
| A-characters-018 | S2 | 98-18 Task 1 | chars-index (both User-Agents; dead, retired and Main-and-Dead fixtures) in after/final; CharactersIndexRenderTests | 50 in-main contrast failures (worst 1.8:1) | 0 in-main contrast failures | closed |
| A-characters-019 | S2 | 98-14 Task 1 (D-05) | note contrast in after/98-14; contact-details-full (phone) in after/final | 36 in-main contrast failures (worst 1.01:1) | 0 in-main contrast failures | closed |

### S3 and S4 findings (36)

| Finding | Owner (plan) | Evidence in the AFTER record | Status |
|---|---|---|---|
| A-characters-020 | 98-18 (Q-50) | the desktop and phone Characters lists both carry the page heading (CharactersTwinAnswersTests) | addressed |
| A-characters-021 | 98-19 (Q-52) | the phone Contacts page carries the h1 when the board has categories; on a board without categories the phone page keeps an h2 (decision recorded for Gate 2) | addressed |
| A-characters-022 | 98-19 (Q-53, deliberate, recorded; 036 recolours the button) | carried by decision: the phone Filter Tags button and Active badge stay phone-only by the operator answer (Q-53, deliberate), the button now recoloured | carried |
| A-characters-023 | 98-16 (Q-54) | both Manage layouts carry the heading (CharactersTwinAnswersTests) | addressed |
| A-characters-024 | 98-17 (Q-51) | one merged Characters form: Classes required on both layouts | addressed |
| A-characters-025 | 98-20 (Q-55) | Date TBD on both layouts (dm-dates.json) | addressed |
| A-characters-026 | 98-11, 98-14, 98-18 to 98-21 | one h1 on every phase page except contacts-index-empty (phone), the recorded decision; heading skips 0 over the sweep | addressed |
| A-characters-027 | 98-11 | no label element in the Characters and Contacts details views (view grep); CharacterDetailsRenderTests | addressed |
| A-characters-028 | 98-18 to 98-21 | phone rows are 44px stretched links; the phone small-target records left are the editor proxy, Tagify and two inline links (a11y.md) | addressed |
| A-characters-029 | 98-21 | Players name link 24px tall (players-measure.json); desktop small controls over the area 133 before, 90 after, none a Players link | addressed |
| A-characters-030 | 98-14 | note buttons carry aria-label (Edit note, Delete note) | addressed |
| A-characters-031 | 98-13 and 98-17 | the required marker is the same overlay chip on both layouts (marker.json) | addressed |
| A-characters-032 | 98-14 (D-05) | one notes panel on both layouts | addressed |
| A-characters-033 | 98-18 to 98-21 (the Players title only on the explicit yes at Gate 1 item 10) | page-title card with the h1 on every page; Players carries it by the Gate 1 yes | addressed |
| A-characters-034 | 98-08, 98-13, 98-15 and 98-17 (Q-118) | one form-actions row with the hr on the merged forms | addressed |
| A-characters-035 | 98-14, 98-16 and 98-19 | filled button roles by the role table; zero in-main contrast failures | addressed |
| A-characters-036 | 98-19 | one primary action on the page: + Contact on both layouts; the phone Apply Filters button sits inside the filter offcanvas on its own surface (view grep) | addressed |
| A-characters-037 | 98-11 and every slice that converts a phone sheet | important flags in the area sheets 88 before, 0 after (counts-after.json) | addressed |
| A-characters-038 | 98-09 and 98-13 | important flags 0; the crop stage height and Tagify override live as custom properties | addressed |
| A-characters-039 | 98-18 and 98-19 | one shared card sheet (member-cards.css) for Characters and Contacts | addressed |
| A-characters-040 | 98-18 (D-06, X-2) | infinite animations 0 over the area (counts-after.json, a11y-extra.json) | addressed |
| A-characters-041 | 98-18 and 98-19 | no transition: all and no heavy hover in the area sheets (guards); the 32 transition: all records are the Tagify widget on contacts forms | addressed |
| A-characters-042 | 98-18, 98-19 and this plan | text under 14px: 69 elements before, 0 after (fontsize.json) | addressed |
| A-characters-043 | 98-20 (Gate 1 item 9) | dates through the date helpers (AreaViews_RenderNoRawDateFormatting); production LANG release check still open | addressed |
| A-characters-044 | 98-11, 98-14, 98-16 and 98-18 to 98-21 | inline handlers 11 before, 0 after; no confirm( call in a view (confirm-submit.js) | addressed |
| A-characters-045 | 98-11 (Gate 1 item 8) | recommended wording on the merged character details | addressed |
| A-characters-046 | 98-13, 98-15 and 98-17 | help text is .form-text on both layouts (merged views) | addressed |
| A-characters-047 | 98-13 and 98-17 (Q-119) | one image column layout on the Contacts forms | addressed |
| A-characters-048 | 98-15 and 98-20 | no nested container in the Dungeon Master views | addressed |
| A-characters-049 | 98-15 | validation summary All on every merged form | addressed |
| A-characters-050 | 98-18 | card images alt empty beside the name; the avatar partial names the picture only when not decorative | addressed |
| A-characters-051 | 98-11 | the sheet link announces (opens in a new tab) | addressed |
| A-characters-052 | 98-17 | the Create view has no Edit branches (merged form with its own tests) | addressed |
| A-characters-053 | 98-11 and 98-14 | no bg-purple rule in any sheet | addressed |
| A-characters-054 | 98-14 | no note-heading override left | addressed |
| A-characters-055 | 98-09, 98-12, 98-13, 98-15, 98-16 and 98-17 | inline style attributes 49 before, 0 after; inline scripts 22 before, 0 after; no CDN script tags (counts-after.json) | addressed |

### Inherited operator questions (17)

| Question | Owner | Evidence | Status |
|---|---|---|---|
| Q-50 | 98-18 | CharactersTwinAnswersTests fact | done |
| Q-51 | 98-17 | CharactersTwinAnswersTests fact | done |
| Q-52 | 98-19 | CharactersTwinAnswersTests fact | done |
| Q-53 | 98-19 | deliberate: phone-only Filter Tags and Active badge, recorded | recorded |
| Q-54 | 98-16 | CharactersTwinAnswersTests fact | done |
| Q-55 | 98-20 | CharactersTwinAnswersTests fact | done |
| Q-97 | 98-11 | merged-view phone fact and RetiredFiles entry | done |
| Q-98 | 98-14 | merged-view phone fact and RetiredFiles entry | done |
| Q-99 | 98-13 | merged-view phone fact and RetiredFiles entry | done |
| Q-100 | 98-13 | merged-view phone fact and RetiredFiles entry | done |
| Q-101 | 98-08 | merged-view phone fact and RetiredFiles entry | done |
| Q-102 | 98-15 | merged-view phone fact and RetiredFiles entry | done |
| Q-117 | 98-16 | cat-manage sweep rows beside the approved Gate 1 frame | done |
| Q-118 | 98-08 with 98-13, 98-15 and 98-17 | sweep rows beside the approved Gate 1 frame | done |
| Q-119 | 98-13 and 98-17 | sweep rows beside the approved Gate 1 frame | done |
| Q-132 | 98-09 and 98-10 | behaviour records (ten of ten pass in after/final) | done |
| Q-135 | Phase 95 | the character select dialog went with Phase 95; unchanged here | out-of-scope |

## Self-Check: PASSED

- The guard file, the deferred items, `counts-after.json`, `findings-trace.json`, `contrast-exceptions.json`, `after/final/report.json`, `a11y.md`, `tests-after.txt` and `focus-frames.json` exist; commits `998abb4d`, `5b4e73cb` and `9720649c` exist.
