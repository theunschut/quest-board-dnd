---
phase: 98-characters-contacts-dm-profile-and-players-ui-conversion
plan: 06
subsystem: gate1-package-b
tags: [ui-review, mockups, look-check, notes-panel, badges, manage-categories, character-details, scratch]
requires: [98-04]
provides:
  - lookcheck.mjs: computed-style equality of a mock or a live page against the Phase 94 Direction A mockups, with the three intended differences (tools/lookcheck.mjs, mockups/v1/lookcheck-map.json)
  - 41 package B frames with probes (png/report.json) and frames.json entries pairing each with its BEFORE and, for look-check frames, its Direction A render
  - 18 lookcheck.json entries, all passing; look/pairs.html shows each look-check frame beside its Direction A render with the equality table
  - draft page sheets in the shape they would ship, which the slices port (member-cards.css, characters.css, characters.mobile.css, contacts.css, contacts.mobile.css, contact-detail.css, players.css, players.mobile.css, dm-profile.css, dm-profile.mobile.css, contact-categories.css, contact-categories.mobile.css, character-detail.css, badge-alt.css)
affects: [98-07, Gate 1 sheet, the Characters, Contacts, Players, DM profile, Manage Categories and detail slices]
tech-stack:
  added: []
  patterns: [one markup rendered under the saved desktop shell or the saved phone shell, built from saved baseline pages by tools/build-look-mockups.py; stretched-link targets measured on the host row or card; look check by selector map per role with a cross-reference where the phone mockup has no counterpart]
key-files:
  created: []
  modified: []
decisions:
  - "Manage Categories: the table card has no heading (no new string is invented); the table carries aria-label with the page title words"
  - "DM profile on desktop: the profile card spans the page, About and Quest History sit under it (the baseline had the profile card in a narrow left column)"
  - "Notes: the add and edit fields are drawn as plain form-control textareas; the Markdown editor is unchanged by this area and is not redrawn"
metrics:
  tasks: 3
  commits: 1
status: complete
actuals:
  tokens: 26000
  tasks: 3
  commits: 1
---

# Phase 98 Plan 06: Gate 1 package B Summary

The look check (every page type on both layouts, measured against the Direction A renders), the Notes panel, the static Main badge and the Dead and Retired treatment, the Manage Categories table in its responsive container, the merged Characters/Details in both DOM orders, and the Dungeon Master and Players details are drawn as scratch mockups from real p98 fixture data on the foundation copy at `3b25a4df` (a descendant of `c781090c`, `site/PROVENANCE.txt`). No repository file was created or changed; the only commit is this summary. No repository tests were run (the plan has none).

Everything is under `~/qb-ui-review/phase-98/`. Mocks and sheets: `mockups/v1/look/{characters,contacts,players,dm,contact-details,badges}/`, `mockups/v1/manage/`, `mockups/v1/character-details/`; frames in `mockups/v1/png/` (41 new, `report.json`, `direction-a/` copies of the four reference renders); `mockups/v1/lookcheck.json`, `lookcheck-map.json`, `lookcheck-negative-control.json`, `look/pairs.html`, `manage/measure.json`, `frame-helpers.css` (frame-only captions, not shipping). New tools in `tools/`: `lookcheck.mjs`, `lookcheck-package-b.sh`, `build-look-mockups.py`, `register-look-frames.py` (with `register-look-frames.d.py`), `manage-measure-98.mjs`, `build-look-pairs.py`. Data came from pages saved from 8311 only, as `p98player`, `p98dm` and `p98empty`, on the boards "P98 Audit Characters", "P98 Audit Empty" and "P98 Audit No Categories" (`look/saved/`, antiforgery values blanked, fantasy text only). 8310 was never used.

## Tasks

1. **Tracer (Characters card grid, desktop 1440):** `lookcheck.mjs` and its selector map written first; the Characters mock built from the Dungeon Master roster (Main and Dead stacked, a long name that wraps, characters with no picture, Dead and Retired); `member-cards.css` and `characters.css` in shippable shape. Both static assertions printed `ok`. Tracer gate: config has no auto mode (`workflow._auto_chain_active` and `workflow.auto_advance` are both false), but this sequential subagent cannot be resumed and its orchestrator presents Gate 1, so the gate was the automated re-verification of the tracer's verify (passed) and expansion went ahead, as 98-02 to 98-04 did. A negative control proves the check discriminates: the same mock with the glass at 0.05 and a white `h1` fails on `titleCard.backgroundImage`, `asideCard.backgroundImage` and `h1.color` (`lookcheck-negative-control.json`).
2. **The other look-check mocks, notes, badges, empties:** Characters phone; Contacts (desktop, phone, filter states, the phone Active badge, empties); Players (desktop, phone, empties); DM profile (full and bare, both layouts, BEFORE and AFTER dates beside); contact details with the Notes card (rest, edit state, delete confirm, both layouts); the two Main badge treatments; Dead and Retired; the Characters, Contacts and Players empties. Task 2 verify printed `ok`.
3. **Manage Categories and merged Characters/Details:** Manage at 1440, 768, 320, in the title card variant, at 390 and empty; Characters/Details option a and option b under both User-Agents, plus a bare character. Task 3 verify printed `ok` three times.

## Look-check results (`lookcheck.json`; every entry passes)

Each entry compares 29 to 31 computed properties (card `background-image`, border colour and width, radius, shadow, `backdrop-filter`; band `background-color`, bottom border and padding; `h1` and aside-title colour, family, size and weight; the repeated item's `backdrop-filter`) against `quest-details.html` (desktop) or `quest-details.mobile.html` (phone), and also holds the card to the literal glass 0.12 over scrim 0.42 and `blur(12px)`, and the repeated item to `none`. The Direction A phone mockup has no card with a title band, so a phone mock's aside band and title are read from the desktop render, which is the same 20px body face.

| Entry | Ref | Result | Differences (all intended) |
|---|---|---|---|
| look-characters-1440, -390 | quest-details, .mobile | pass | glass hover value 0.15 against 0.18; button border transparent against `--qb-border-strong` (desktop only) |
| look-contacts-1440, -390 | same | pass | same |
| look-players-1440, -390 | same | pass | glass hover (no button on the desktop mock) |
| look-dm-1440, -390, look-dm-bare-1440, -390 | same | pass | same |
| look-contact-details-1440, -390 | same | pass | same |
| manage-1440, manage-phone-390 | same | pass | same; the table card has no header, so its band and title are not drawn |
| chardetails-a-1440, chardetails-a-390 | same | pass | same |

The header subtitle difference was never exercised: neither side has a `.header-subtitle` on these pages. Title card, section card and band measured equal everywhere: glass 0.12 over scrim 0.42, 1px `rgba(255,255,255,0.3)` border, 12px radius, `--qb-shadow-rest`, `blur(12px)` on the page-title and section cards, none on cards in a list or nested; band `rgba(26,15,8,0.42)`, 1px bottom border, `16px 24px`; desktop `h1` `rgb(255,193,7)` Cinzel 28px 600; phone `h1` parchment Cinzel 20px 600; aside title body face 20px 600. `look/pairs.html` shows 16 frames beside `quest-details-1440.png` or `quest-details-390.png` with these tables. Two further entries (`form-ref-char-create-parchment-1440`, `form-ref-char-create-phone-390`) hold the 98-04 form mocks against the Quest form renders; see below.

Run against the 98-04 form mocks: Characters Create with the parchment title equals `quest-form.html` and the phone form equals `quest-form.mobile.html` (both pass). The default gold desktop title does not equal the Quest form's parchment `h1` (`h1.color`), which is exactly Gate 1 item 6, so that run is not kept as an entry (the two entries above are the passing ones).

## Frames (41; every entry has its BEFORE manifest id in `frames.json`)

`look-characters-1440`, `look-characters-390`, `look-contacts-1440`, `look-contacts-390`, `contacts-filter-active-1440`, `contacts-filter-active-390`, `look-players-1440`, `look-players-390`, `look-dm-1440`, `look-dm-390`, `look-dm-bare-1440`, `look-dm-bare-390`, `look-contact-details-1440`, `look-contact-details-390`, `notes-edit-state-1440`, `notes-edit-state-390`, `notes-delete-confirm-1440`, `notes-delete-confirm-390`, `badge-gold`, `badge-parchment-ring`, `dead-retired`, `empty-characters-1440`, `empty-characters-390`, `empty-contacts-1440`, `empty-contacts-390`, `empty-players-1440`, `empty-players-390`, `manage-1440`, `manage-768`, `manage-320`, `manage-titlecard-1440`, `manage-titlecard-320`, `manage-phone-390`, `manage-empty-1440`, `manage-empty-390`, `chardetails-a-1440`, `chardetails-a-390`, `chardetails-b-1440`, `chardetails-b-390`, `chardetails-bare-1440`, `chardetails-bare-390`. Probe result over all 41: every phone frame `mobileLayout` true, no overflow at 390, no target under 44px; no desktop overflow; 0 contrast failures; 0 missing focus rings; no console errors, page errors or failed requests.

## Notes panel (D-05)

One `.modern-card.modern-card--aside` Notes card with the glass surface and scrim on both layouts; the add-note form at the top (`Add Note` is `btn-primary`); each note a `notes-slip` on `--qb-surface-scrim` with no blur; icon-only `Edit note` (`btn-warning btn-sm`) and `Delete note` (`btn-danger btn-sm`) named by `aria-label`; the inline edit form shown by removing `hidden`, `Cancel` `btn-secondary btn-sm`, `Save` `btn-success btn-sm`; a note with a long unbroken string wraps inside the slip (no overflow at 390). Delete confirm is `data-confirm-message="Delete this note? This action cannot be undone."`; the frame lists the sentence.

Contrast measured on the rendered slip (same on desktop and phone): note text **9.88:1**, meta line **8.97:1**, a heading inside a note **11.21:1**, against BEFORE's phone figure of about 3.7:1 (grey `#6c757d` on near-white). The near-white phone panel and grey text are gone.

## Badges and Dead/Retired (D-06)

Main drawn static: `--qb-accent` fill, `--qb-accent-ink` label (11.55:1), star icon and the word, a still glow, no animation and no keyframes, beside a parchment ring on the overlay fill (`--qb-fg`, 12.98:1) in `badge-alt.css`. Dead (`bg-secondary`, skull) and Retired (`bg-info`, moon) carry icon and word; the portrait is `grayscale(1)` and `grayscale(0.5)` on the image only, text at full contrast, no opacity. A Main and Dead character shows both badges stacked top left (Varek Duskmantle the Undying). Badges carry a 1px light edge because a neutral badge on the neutral placeholder tile otherwise has the tile's own colour. The baseline has no retired character with a picture, so the half-grey portrait is shown on an illustration card (the Main character's picture labelled as such in the frame).

## Manage Categories (`manage/measure.json`)

| Frame | Document scroll width / viewport | Table container scroll / client | Table |
|---|---|---|---|
| manage-1440 | 1440 / 1440 | 1246 / 1246 | fits |
| manage-768 | 768 / 768 | 646 / 646 | fits |
| **manage-320** | **320 / 320** | **540 / 246** | scrolls inside its card |
| manage-titlecard-1440 | 1440 / 1440 | 1246 / 1246 | fits |
| **manage-titlecard-320** | **320 / 320** | **540 / 246** | scrolls inside the title card |
| manage-phone-390 | 390 / 390 | no table | category rows are slips, every control at least 44px |

Columns `Order`, `Name`, `Contacts`, `Actions` and their buttons unchanged; Order 100px and Contacts 112px are classes; the actions column does not wrap. The phone shows the added `Manage Categories` heading. The delete confirm sentence is built into `data-confirm-message` (`Delete "{name}"? This will move {n} contact(s) to Ungrouped.` or `...It has no contacts to move.`).

## Characters/Details Tab orders (frames `chardetails-a-*`, `-b-*`; each frame lists its order)

- **Option a** (phone order in the document: title, information, Actions, Back; the desktop grid places Actions under the portrait): Character Sheet link, Edit Character, Retire Character, Delete Character, Back to Characters. The one owner-only difference between visual and focus order: on the desktop grid the three action buttons sit left of the sheet link but the sheet link takes focus first. A viewer with no Actions card sees no difference.
- **Option b** (desktop order in the document: title, Actions, information, Back): Edit Character, Retire Character, Delete Character, Character Sheet link, Back to Characters. Visual and focus order agree on the desktop; on the phone the Actions card now precedes the information, where today the phone shows information first.

Wording drawn: `Character Sheet`, `Classes`, `Actions`; `dl` for the read-only values; `Not provided` shown on the bare character; the sheet link carries `target="_blank" rel="noopener noreferrer"` and a visually hidden "(opens in a new tab)" (the mitigation for T-98-16, checked by the Task 3 verify).

## Dungeon Master and Players

Finalized dates go through the wall-clock `date` format; BEFORE text is quoted from `before/dm-dates.json` beside AFTER in the frame. Quote: desktop BEFORE `Dec 04, 2026`, an empty cell, `Sept 30, 2026`; phone BEFORE `Dec 04, 2026`, `Date TBD`, `Sept 30, 2026`; AFTER on both layouts `Dec 4, 2026`, `Date TBD`, `Sep 30, 2026`. The format table uses the invariant culture, so the visible change has two parts, not one: the leading zero goes, and `Sept` becomes `Sep` (the old line used the host culture's abbreviation). The Players mocks show the new `Players` heading; the phone Characters, Contacts and Manage mocks show their added headings in the existing desktop words.

## Class and sheet names the slices port

Page wrappers: `characters-page`, `contacts-page` (Contacts/Index only), `contact-detail-page`, `character-detail-page`, `dm-profile-page`, `players-page`, `contact-categories-page`. Shared classes: `member-grid`, `member-card` (beside `character-card` and `contact-card`), `member-row` (beside `character-member-row`, `contact-member-row`), `badge-main`, `member-card--dead`, `member-card--retired`, `member-row--dead`, `member-row--retired`, `notes-slip`, `players-row` (and `no-link`), `players-name-link`, `dm-quest-row`, `category-mgmt-row`. Sheets: `member-cards.css` (grid, card, phone row, Main badge, Dead and Retired; linked by every page that shows a member, including `Characters/Details` for the Main badge), `characters.css`, `characters.mobile.css`, `contacts.css`, `contacts.mobile.css`, `contact-detail.css`, `players.css`, `players.mobile.css`, `dm-profile.css`, `dm-profile.mobile.css`, `contact-categories.css`, `contact-categories.mobile.css`, `character-detail.css`, `badge-alt.css` (only if the gold is turned down). All fourteen start with one `@layer pages {` block, hold tokens only, no colour literal, no important flag, no transition, no keyframes, no animation; the card lift is the foundation's own and sits under `prefers-reduced-motion: no-preference`.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] The focus-ring and target probes did not understand a stretched link**
- **Found during:** Task 2 (first full run)
- **Issue:** the probe measured the 22px link box instead of the whole row its `::after` covers, and looked for a ring on the link instead of the card or row, so every card and row link was flagged.
- **Fix:** `tools/probes.mjs` now takes the host row or card box as the target when the link's `::after` is absolutely positioned, and counts a ring drawn on `.member-card`, `.member-row`, `.players-row`, `.dm-quest-row` or `.contact-filter-check-label`. The checks are on measured values, not assumed. Scratch tool only.

**2. [Rule 1 - Bug] The contact-detail row pulled the phone page 4px wider than the screen**
- **Found during:** Task 2 (overflow 394 against 390)
- **Fix:** `contact-detail.css` sets the row gutter to the phone padding under `.mobile-layout`, the same fix the shell applies to a root row. Frames re-shot, no overflow.

**3. [Rule 1 - Bug] A visually hidden table caption was flagged as 1.05:1 text**
- **Found during:** Task 3
- **Fix:** the table is named with `aria-label` instead of a hidden caption.

### Plan interpretations and findings

**4. The Manage edit action is `Rename`, not `Edit`.** The plan and UI-SPEC say `Edit` `btn-warning btn-sm`, but the existing label is `Rename` (`fa-pen`). Drawn as `Rename`, role unchanged, in line with the no-copy-change rule.

**5. The Manage table card has no title.** The spec names an `h2` "its existing words" for the table card, and no existing words exist for it. Drawn headerless with `aria-label`, no new string. Operator may name a heading.

**6. Phone Contacts keeps the existing label `Categories`.** Only the heading is added; the desktop button keeps `Manage Categories`.

**7. Merged details disagree on a fourth label.** Besides the sheet link, classes and actions labels, the owner line differs (`Owned by {name}` on the phone, the bare name with an icon on the desktop). Drawn as `Owned by`. The status badge word is kept as `Main Character` (the existing Details wording, in the Main badge style); the Index badge word stays `Main`.

**8. DM profile desktop layout.** The profile card is full width above the two columns; the baseline kept it in a narrow left column. Drawn this way so the title card matches every other page.

**9. Add and edit fields are plain textareas.** The Markdown editor is unchanged by this area and is not part of the drawing; its toolbar is not shown.

**10. `No classes defined` is not drawn.** Classes are required, so no fixture lacks them; the empty is listed in the copy contract and unchanged.

**11. `--filter ""` re-shot every frame once.** The 63 package A frames and their report entries were regenerated by the same tool; the report matches 98-04 (the one known host-page frame with the baseline's own failures, `crop-filesize-error-phone-390`).

**12. Observed, not in scope.** On the desktop layout at 320px the fixed navbar wraps to two lines and covers the top of the page title on the Manage frames (the layout, not this plan's sheets). Gate 2's 320 sweep will see it on every page.

## Decisions Gate 1 will need

1. Manage Categories: table in its own card (default, headerless) or in the title card; whether the table card gets a heading and what it says.
2. Main badge: gold (default) or the parchment ring; Dead and Retired treatment as drawn.
3. Dates: the DM date changes in two ways (`Nov 05` style no leading zero, and `Sept` becomes `Sep`); accept or refuse.
4. Players page `h1` `Players`: shown, but per the spec it is added only on an explicit yes.
5. Characters/Details: DOM order option a (default) or b; the fourth label disagreement (`Owned by`); `Main Character` against `Main` in the badge.
6. DM profile desktop layout (full-width profile card, drawn) against the old left column.
7. Phone Contacts: `Filter Tags` as `btn-secondary` with a green `Active` badge; the `Categories` label kept.
8. Notes: the slip and the panel as drawn (default). Separately, Gate 1 item 6 stands as the spec words it: a gold desktop form title differs from the Quest form's parchment one by design.

## Known Stubs

None. Frame-only helpers (`frame-helpers.css`, the notes in the pages that hold captions, `look/pairs.html`) are labelled and are not part of the shipping shape.

## Threat Flags

None. No repository file, endpoint or auth path changed; pages were read from 8311 only, as the three p98 accounts on three P98 boards (T-98-15); the Characters/Details mock carries the safe external link form (T-98-16); nothing was installed (T-98-SC).

## Self-Check: PASSED

- Task 1, Task 2 and Task 3 automated checks printed `ok`; all fourteen draft sheets pass the layer, literal, important, transition, keyframes and animation checks; no `style=`, handler, `<style>`, `localhost` or email in any mock page; no planning reference in any sheet.
- 18 entries in `lookcheck.json`, all `pass: true`; 41 package B frames exist in `png/` with report entries; `frames.json` names every required frame and records `scrollWidth` equal to `viewportWidth` on the four desktop Manage frames; `manage-320` container scroll 540 over client 246.
- `git -C /mnt/Data/repos/quest-board-dnd status --porcelain` was empty before this summary was written. 8311 and 8310 were already running and were not started or stopped here; the throwaway servers were closed.
