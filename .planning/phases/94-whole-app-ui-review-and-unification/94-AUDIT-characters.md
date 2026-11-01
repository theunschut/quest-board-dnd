# Phase 94 Audit: Characters, Contacts, DM profile and Players

Finding IDs `A-characters-nnn`. Audited with the senior-frontend skill in Critic mode against the frozen baseline (milestone commit `b6770322`, served on port 8001). Screenshots, probe output and scratch notes are outside the repository under `~/qb-ui-review/phase-94/`; this report references them by path only and quotes no member name, email address or user-entered text. All data seen is fixture data on the Audit Board.

## Scope

- **Views (13 manifest rows, twins on one row):** Characters Index, Details, Create, Edit; Contacts Index, Details, Create, Edit; ContactCategoryManagement Manage, Edit; DungeonMaster Profile, EditProfile; Players Index. Partials audited through the pages that render them and tagged "shared partial": `_CharacterSelectModal` (rendered only by Quest/Details), `_MarkdownEditor`.
- **Stylesheets (15):** `characters.css`, `characters.mobile.css`, `character-detail.mobile.css`, `character-form.mobile.css`, `contacts.css`, `contacts.mobile.css`, `contact-detail.mobile.css`, `contact-form.mobile.css`, `dm-profile.css`, `dm-profile.mobile.css`, `dm-create.mobile.css`, `dm-editprofile.mobile.css`, `dm-manage.mobile.css`, `players.mobile.css`, `image-crop.css` (counts below).
- **Method:** desktop 1440px and Pixel 7 mobile (real mobile User-Agent, so the `.Mobile.cshtml` twin and mobile layout render) captures from `~/qb-ui-review/phase-94/before/report.json` and `~/qb-ui-review/phase-94/before/desktop|mobile/<row>.png`; the report records contrast failures composited on the B-med, B-99 and B-max boards, interactive inventory, focus stops, `img` without `alt`, console, request and overflow data. Extra live probes written for this audit (baseline app only): crop dialog, keyboard, text spacing, target size, 320px reflow, DM-only controls (`~/qb-ui-review/phase-94/audit/characters/probe1.json`, `probe2.json`, `probe3.json`). Contrast ratios are WCAG relative luminance with translucent surfaces over B-max (`#794317`); text-shadow never counts.
- **Fixtures:** Audit Board only; character 3013, contact 25, category 1, the audit DM and Player accounts. No dead or retired character, no Main character and no contact note exist, so those states are computed or injected in the page (nothing saved) and marked as such.
- **Captured as Player for pages DMs see more of:** the BEFORE sweep ran Contacts/Details as Player; the DM-only buttons were probed separately (finding on "Edit Contact").
- **Known items recorded, not re-discovered:** Contacts category headings are `h2` to dodge the `modern-card` element rule (finding on the ungrouped heading); image crop WCAG 2.5.7 (result below); Cropper.js and Tagify self-hosting is plan 94-12, so only their presentation is audited. No deliberate differences are pre-recorded for this area.

### Measured record per row (BEFORE sweep, `~/qb-ui-review/phase-94/before/report.json`)

Counts exclude navigation and footer chrome (owned by the foundation). `CF` = contrast failures across B-med, B-99 and B-max; overflow columns are 320px desktop reflow and 390px mobile reflow.

| Row | desktop CF | mobile CF | desktop 320 overflow | mobile 390 overflow | `img` without alt | console / page / request errors |
|-----|-----------|-----------|----------------------|---------------------|-------------------|--------------------------------|
| Characters/Index | 4 | 6 | no | no | 0 / 0 | 0 / 0 / 0 |
| Characters/Details | 19 | 14 | no | no | 0 / 0 | 0 / 0 / 0 |
| Characters/Create | 20 | 15 | no | no | 0 / 0 | 0 / 0 / 0 |
| Characters/Edit | 20 | 15 | no | no | 0 / 0 | 0 / 0 / 0 |
| Contacts/Index | 3 | 5 | no | no | 0 / 0 | 0 / 0 / 0 |
| Contacts/Details | 11 | 10 | no | no | 0 / 0 | 0 / 0 / 0 |
| Contacts/Create | 14 | 11 | no | no | 0 / 0 | 0 / 0 / 0 |
| Contacts/Edit | 14 | 11 | no | no | 0 / 0 | 0 / 0 / 0 |
| ContactCategoryManagement/Manage | 7 | 3 | yes | no | 0 / 0 | 0 / 0 / 0 |
| ContactCategoryManagement/Edit | 5 | 3 | no | no | 0 / 0 | 0 / 0 / 0 |
| DungeonMaster/Profile | 35 | 35 | no | no | 0 / 0 | 0 / 0 / 0 |
| DungeonMaster/EditProfile | 10 | 7 | no | no | 0 / 0 | 0 / 0 / 0 |
| Players/Index | 4 | 7 | no | no | 0 / 0 | 0 / 0 / 0 |

Screenshot paths (outside the repository): `~/qb-ui-review/phase-94/before/desktop/<Row>.png` and `~/qb-ui-review/phase-94/before/mobile/<Row>.png` with `<Row>` the manifest id (for example `Contacts-Create`); focus crops under `~/qb-ui-review/phase-94/before/desktop/focus/<Row>/`.

## Index

| View | Desktop file | Mobile file or none | Area phase | Counts by class | Highest severity | Existing empty-state copy | Open operator questions |
|------|--------------|---------------------|------------|-----------------|------------------|---------------------------|-------------------------|
| Characters/Index | `Views/Characters/Index.cshtml` | `Views/Characters/Index.Mobile.cshtml` | 3 | inconsistency 3, cascade-fight 1, twin-content 1, a11y 9, perf 2 | S2 | Desktop and mobile, same wording. My Characters: "No Characters Yet" / "Create your first character to get started!" / action "Create Character". Character Roster: "No Other Characters" / "No other adventurers have created characters yet!". Desktop headings are h4, mobile h5. | 1 |
| Characters/Details | `Views/Characters/Details.cshtml` | `Views/Characters/Details.Mobile.cshtml` | 3 | visual-bug 1, inconsistency 1, cascade-fight 1, twin-drift 1, a11y 7 | S1 | Both layouts: "Not provided" (no sheet link) and "No classes defined". No page-level empty state. | 1 |
| Characters/Create | `Views/Characters/Create.cshtml` | `Views/Characters/Create.Mobile.cshtml` | 3 | visual-bug 2, inconsistency 3, cascade-fight 2, twin-drift 2, twin-content 1, a11y 7, dead-code 1 | S1 | none (form page) | 3 |
| Characters/Edit | `Views/Characters/Edit.cshtml` | `Views/Characters/Edit.Mobile.cshtml` | 3 | visual-bug 2, inconsistency 3, cascade-fight 2, twin-drift 2, twin-content 1, a11y 7 | S1 | none (form page) | 3 |
| Contacts/Index | `Views/Contacts/Index.cshtml` | `Views/Contacts/Index.Mobile.cshtml` | 3 | inconsistency 5, cascade-fight 2, twin-drift 1, twin-content 2, a11y 8, perf 1 | S2 | Both layouts, same wording. No contacts: "No Contacts Yet" / "No one has added a contact yet. DMs can create the first one to start building out the world.". Filter matches nothing: "No contacts match your filters" / "Try selecting different tags." / action "Clear filters" (desktop) and "Clear Filters" (mobile). | 2 |
| Contacts/Details | `Views/Contacts/Details.cshtml` | `Views/Contacts/Details.Mobile.cshtml` | 3 | visual-bug 1, inconsistency 3, cascade-fight 1, twin-drift 2, a11y 9, dead-code 1 | S1 | Both layouts, same wording. "No description provided."; notes: "No Notes Yet" / "Be the first to jot something down about this contact." | 1 |
| Contacts/Create | `Views/Contacts/Create.cshtml` | `Views/Contacts/Create.Mobile.cshtml` | 3 | visual-bug 2, inconsistency 3, cascade-fight 2, twin-drift 2, a11y 8 | S1 | none (form page). Form hint when the board has no categories, both layouts: "No categories yet. Manage Categories to create one." | 3 |
| Contacts/Edit | `Views/Contacts/Edit.cshtml` | `Views/Contacts/Edit.Mobile.cshtml` | 3 | visual-bug 2, inconsistency 3, cascade-fight 2, twin-drift 2, a11y 8 | S1 | none (form page). Same no-categories hint as Contacts/Create, both layouts. | 3 |
| ContactCategoryManagement/Manage | `Views/ContactCategoryManagement/Manage.cshtml` | `Views/ContactCategoryManagement/Manage.Mobile.cshtml` | 3 | inconsistency 4, cascade-fight 1, twin-content 1, a11y 5 | S2 | Both layouts, same wording: "No Categories Yet" / "Add your first category above to start grouping your contacts." | 1 |
| ContactCategoryManagement/Edit | `Views/ContactCategoryManagement/Edit.cshtml` | `Views/ContactCategoryManagement/Edit.Mobile.cshtml` | 3 | visual-bug 1, inconsistency 2, cascade-fight 1, a11y 3 | S1 | none (form page) | 2 |
| DungeonMaster/Profile | `Views/DungeonMaster/Profile.cshtml` | `Views/DungeonMaster/Profile.Mobile.cshtml` | 3 | inconsistency 5, cascade-fight 1, twin-content 1, a11y 7, dead-code 1 | S2 | Both layouts, same wording. Bio: "No bio provided yet."; quests: "No Quests Yet" / "This Dungeon Master hasn't run any quests yet." (desktop heading h4, mobile h5). | 1 |
| DungeonMaster/EditProfile | `Views/DungeonMaster/EditProfile.cshtml` | `Views/DungeonMaster/EditProfile.Mobile.cshtml` | 3 | visual-bug 1, inconsistency 5, cascade-fight 2, twin-drift 1, a11y 6 | S1 | none (form page) | 3 |
| Players/Index | `Views/Players/Index.cshtml` | `Views/Players/Index.Mobile.cshtml` | 3 | inconsistency 3, cascade-fight 1, a11y 5 | S2 | Both layouts, same wording. "No Dungeon Masters Found" / "No dungeon masters are registered yet. Brave souls may register as new quest leaders."; "No Players Found" / "No adventurers have registered yet. Players can register to join quests." | 0 |
| Shared/_CharacterSelectModal | `Views/Shared/_CharacterSelectModal.cshtml` | none (one partial, both layouts) | 3 | visual-bug 1, inconsistency 3 | S1 | none | 0 |
| Shared/_MarkdownEditor | `Views/Shared/_MarkdownEditor.cshtml` | none (one partial, both layouts) | 3 | a11y 1 | S1 | none | 0 |

Counts count each finding once per row it touches, across both layouts. "Open operator questions" counts the standard twin-content questions, the crop and button-row proposals, and the merge proposal for that row (all in the sections below).

## Findings

Ordered S1 first. Fields: ID, page and layout, class, severity, WCAG criterion (a11y), evidence (scratch screenshot path plus selector, and file:line), proposed fix (token or component class), area phase, operator question.

### A-characters-001: Image-crop dialog title and instruction text are unreadable (dark ink on the dark dialog)

- **Page and layout:** Characters/Create (both layouts); Characters/Edit (both layouts); Contacts/Create (both layouts); Contacts/Edit (both layouts); DungeonMaster/EditProfile (both layouts)
- **Class:** visual-bug
- **Severity:** S1
- **WCAG:** 1.4.3
- **Evidence:** `~/qb-ui-review/phase-94/audit/characters/shots/crop-contacts-create-desktop.png` and `crop-contacts-create-mobile.png`; live probe `~/qb-ui-review/phase-94/audit/characters/probe1.json` key `contacts-create-*`: `h5#cropPhotoModalLabel` 1.22:1 and `p.text-muted.small` 1.09:1 (opaque dialog, bar 4.5). Markup `Views/Contacts/Create.cshtml:109-114,137` and the same lines in `Create.Mobile`, `Edit`, `Edit.Mobile` of Contacts, in Characters/Create and Edit (both layouts) and DungeonMaster/EditProfile (both layouts). Cause: the dialog is `modal-content bg-dark text-light`, outside `.modern-card`, so the global dark heading and paragraph colour (`site.css:586-592`, `site.css:632-637`) paints the title and the hint dark brown on `#212529`.
- **Proposed fix:** Render the dialog on `--qb-surface-overlay` with `--qb-fg` inherited from the surface (title in the Heading step), the hint in `--qb-fg-muted`; drop the `bg-dark text-light` utilities.
- **Area phase:** 3 (Characters, Contacts, DM profile, Players)
- **Operator question:** N

### A-characters-002: Character add/change/clear dialog: title and "Select Character" label are unreadable; required marker and Remove button fail

- **Page and layout:** shared partial: Shared/_CharacterSelectModal (both layouts)
- **Class:** visual-bug
- **Severity:** S1
- **WCAG:** 1.4.3
- **Evidence:** `~/qb-ui-review/phase-94/audit/characters/shots/charmodal-desktop.png`, `charmodal-mobile.png`; `~/qb-ui-review/phase-94/audit/characters/probe1.json` keys `charModal-*` (probed on the fixture finalized quest, the only page that renders this partial): `span#characterSelectModalTitle` 1.22:1 (both layouts), `label.form-label` 1.22:1 (desktop), `span.text-danger` 3.41:1 and `button#characterSelectRemoveBtn` (`btn-outline-danger`) 3.41:1 (both). Markup `Views/Shared/_CharacterSelectModal.cshtml:23,25-26,35,46`. Same cause as the crop dialog (`bg-dark text-light` outside `.modern-card`).
- **Proposed fix:** Overlay surface with `--qb-fg`; required marker and Remove in `--qb-danger-text` on the overlay; Remove becomes a filled `btn-danger` per the button-role table.
- **Area phase:** 3 (partial owner); the only host is Quest/Details, which area 1 converts: the two phases agree who edits `_CharacterSelectModal`
- **Operator question:** N

### A-characters-003: "Edit Character" label is parchment on amber (1.29:1) on the mobile Character page

- **Page and layout:** Characters/Details (mobile)
- **Class:** visual-bug
- **Severity:** S1
- **WCAG:** 1.4.3
- **Evidence:** `~/qb-ui-review/phase-94/before/mobile/Characters-Details.png` (the amber button reads as a blank bar); report record `Characters-Details` mobile, `a.btn.btn-warning` 1.29:1. Rule `wwwroot/css/character-detail.mobile.css:82-88` (`.character-detail-card a { color: #F4E4BC !important }`) repaints every anchor in the card, including the anchor-button at `Views/Characters/Details.Mobile.cshtml:102`.
- **Proposed fix:** No element-enumerated colour: the card surface sets `color` once and the button takes `--qb-warning-fg` (near-black on amber); delete the catch-all.
- **Area phase:** 3 (Characters, Contacts, DM profile, Players)
- **Operator question:** N

### A-characters-004: "Edit Contact" label is parchment on amber (1.29:1) on the mobile Contact page for DMs

- **Page and layout:** Contacts/Details (mobile)
- **Class:** visual-bug
- **Severity:** S1
- **WCAG:** 1.4.3
- **Evidence:** `~/qb-ui-review/phase-94/audit/characters/shots/contact-details-dm-mobile.png`; `~/qb-ui-review/phase-94/audit/characters/probe1.json` key `dmDetails-mobile`: `a.btn.btn-warning` 1.29:1. Rule `wwwroot/css/contact-detail.mobile.css:76-83` (same catch-all as the Characters sheet). The BEFORE record `Contacts-Details` was captured as Player, who never sees the button, so the baseline sweep missed it. Markup `Views/Contacts/Details.Mobile.cshtml:65`.
- **Proposed fix:** Same as the Characters mobile fix: surface-inherited colour, `--qb-warning-fg` on the amber fill.
- **Area phase:** 3 (Characters, Contacts, DM profile, Players)
- **Operator question:** N

### A-characters-005: "Category Name" label is dark ink on the glass card (1.77:1) on the mobile Rename Category page

- **Page and layout:** ContactCategoryManagement/Edit (mobile)
- **Class:** visual-bug
- **Severity:** S1
- **WCAG:** 1.4.3
- **Evidence:** `~/qb-ui-review/phase-94/before/mobile/ContactCategoryManagement-Edit.png`; report record `ContactCategoryManagement-Edit` mobile: `label.form-label` 1.77:1 at B-med, colour `rgb(33, 37, 41)`. Markup `Views/ContactCategoryManagement/Edit.Mobile.cshtml:17`; `contacts.mobile.css:176-179` themes `.form-label` only inside `.category-mgmt-add-form`, never inside `.contact-section-card`, so the label falls back to Bootstrap body colour (the global `.form-label` rule `site.css:594-600` is overridden by nothing in this card).
- **Proposed fix:** Surface sets `color: var(--qb-fg)`; labels inherit it. No per-form scoping rule.
- **Area phase:** 3 (Characters, Contacts, DM profile, Players)
- **Operator question:** N

### A-characters-006: Markdown editor traps the keyboard: Tab and Escape never leave it, so the controls after it (Save, Cancel, Add Note) cannot be reached from the keyboard

- **Page and layout:** shared partial: Characters/Create (both layouts); Characters/Edit (both layouts); Contacts/Create (both layouts); Contacts/Edit (both layouts); Contacts/Details (both layouts); DungeonMaster/EditProfile (both layouts); Shared/_MarkdownEditor (both layouts)
- **Class:** a11y
- **Severity:** S1
- **WCAG:** 2.1.2; also 4.1.2 (the editing surface has no accessible name)
- **Evidence:** `~/qb-ui-review/phase-94/audit/characters/probe3.json`: focus placed in the editor on Contacts/Details (DM), four Tab presses and then Escape plus two Tab presses all leave `document.activeElement` on the same `textarea`; its `aria-label`, `aria-labelledby` and title are empty. BEFORE focus record `Contacts-Details` desktop stops 9-15 and `DungeonMaster-EditProfile` stops 11-15 are the same textarea seven and five times in a row (`~/qb-ui-review/phase-94/before/desktop/focus/Contacts-Details/09.png`). Partial `Views/Shared/_MarkdownEditor.cshtml`, rendered at `Views/Contacts/Create.cshtml:82-89`, `Characters/Create.cshtml:119-135`, `Contacts/Details.cshtml:126`, `DungeonMaster/EditProfile.cshtml:51-59` and their twins.
- **Proposed fix:** Configure the editor so Tab moves focus on (indentation by Tab off, or Escape-then-Tab documented and working) and give the editing surface a name from its label; verify with the same probe.
- **Area phase:** 1 (Quests) owns `_MarkdownEditor`; this area supplies the pages that expose it. Flag to both phases
- **Operator question:** N

### A-characters-007: Image crop cannot be moved or resized without dragging, and not at all from the keyboard

- **Page and layout:** Characters/Create (both layouts); Characters/Edit (both layouts); Contacts/Create (both layouts); Contacts/Edit (both layouts); DungeonMaster/EditProfile (both layouts)
- **Class:** a11y
- **Severity:** S2
- **WCAG:** 2.5.7; 2.1.1
- **Evidence:** `~/qb-ui-review/phase-94/audit/characters/probe1.json` keys `contacts-create-desktop`, `contacts-create-mobile`: the selection and its eight handles carry no `tabindex` (Tab order inside the dialog is Close, Discard Photo, Use This Crop, repeating); five ArrowRight and five ArrowDown presses with focus on the selection, and five ArrowLeft with focus on the move handle, change its box by 0px. The dialog text says "Drag to reposition, use the corner handles to resize" (`Views/Contacts/Create.cshtml:137-140`). Existing partial alternative: "Discard Photo" skips the crop and the server centres a square (`Create.cshtml:143`). Handles are 15px wide (`probe1.json` handles) against the 24px desktop target size.
- **Proposed fix:** Add a single-pointer path (nudge and resize buttons) and keyboard support to `image-crop.js` and the dialog. This adds controls, so it needs the operator (D-04); Cropper.js itself is self-hosted by the vendor plan.
- **Area phase:** 3 (Characters, Contacts, DM profile, Players)
- **Operator question:** Y (Adding controls is outside D-04 without an answer: see OQ-7.)

### A-characters-008: Hint and muted text fail 4.5:1 (2.99:1 at B-max)

- **Page and layout:** Characters/Index (both layouts); Characters/Details (both layouts); Characters/Create (both layouts); Characters/Edit (both layouts); Contacts/Details (both layouts); Contacts/Create (both layouts); Contacts/Edit (both layouts); DungeonMaster/Profile (both layouts); DungeonMaster/EditProfile (both layouts); Contacts/Index (mobile); ContactCategoryManagement/Manage (mobile)
- **Class:** a11y
- **Severity:** S2
- **WCAG:** 1.4.3
- **Evidence:** Report records for each row (`~/qb-ui-review/phase-94/before/report.json`): `small.form-text.text-muted`, `p.text-muted`, `.character-member-class`, `.contact-member-town-city`, `.dm-profile-role-label`, `.dm-quest-history-date`, `small.text-muted` all `rgba(244,228,188,0.7)` 2.99:1 at B-max (baseline C3). Rules `modern-card.css:102-108` (desktop) and the per-sheet `.text-muted { color: rgba(244,228,188,.7) !important }` blocks (for example `characters.mobile.css:106-109`).
- **Proposed fix:** Muted text uses `--qb-fg-muted`, a solid colour that measures at least 4.5:1 composited on B-max; no alpha-muted parchment (D-17).
- **Area phase:** 3 (Characters, Contacts, DM profile, Players)
- **Operator question:** N

### A-characters-009: Labels and body text on glass are 4.34:1 at B-max (borderline, below 4.5:1)

- **Page and layout:** Characters/Index (both layouts); Characters/Details (both layouts); Characters/Create (both layouts); Characters/Edit (both layouts); Contacts/Details (both layouts); Contacts/Create (both layouts); Contacts/Edit (both layouts); ContactCategoryManagement/Edit (both layouts); DungeonMaster/Profile (both layouts); DungeonMaster/EditProfile (both layouts); ContactCategoryManagement/Manage (both layouts); Players/Index (both layouts); Contacts/Index (mobile)
- **Class:** a11y
- **Severity:** S2
- **WCAG:** 1.4.3
- **Evidence:** Report records: `label.form-label`, `div.character-section-heading`, `span.players-name`, `p` and similar, parchment `#F4E4BC` on glass 4.34:1 at B-max (baseline C2).
- **Proposed fix:** Resolved at the surface by the foundation (scrim or paper surface so the composite reaches 4.5:1); the area phase adds no page-level override.
- **Area phase:** 3 (Characters, Contacts, DM profile, Players)
- **Operator question:** N

### A-characters-010: Required marker and validation text in `#ff6b6b` fail (1.97:1) on every desktop form

- **Page and layout:** Characters/Create (desktop); Characters/Edit (desktop); Contacts/Create (desktop); Contacts/Edit (desktop); Contacts/Details (desktop)
- **Class:** a11y
- **Severity:** S2
- **WCAG:** 1.4.3; 1.4.1 (the marker is the only cue besides colour)
- **Evidence:** Report records: `span.text-danger` `*` 1.97:1 at B-max (baseline C5), colour from `modern-card.css:114-120`. The validation message spans (`asp-validation-for`, `span.text-danger`) share the colour but are only on screen after a failed submit; not captured in the sweep, same rule.
- **Proposed fix:** Required marker and validation text use `--qb-danger-text` (icon and words for messages, A-10).
- **Area phase:** 3 (Characters, Contacts, DM profile, Players)
- **Operator question:** N

### A-characters-011: Filled button labels fail 4.5:1: amber (2.19), grey secondary (3.49), danger (3.82), success (2.87) and small variants

- **Page and layout:** Characters/Index (both layouts); Characters/Details (both layouts); Characters/Create (both layouts); Characters/Edit (both layouts); Contacts/Details (both layouts); Contacts/Create (both layouts); Contacts/Edit (both layouts); ContactCategoryManagement/Manage (both layouts); ContactCategoryManagement/Edit (both layouts); DungeonMaster/EditProfile (both layouts); Contacts/Index (desktop); DungeonMaster/Profile (desktop)
- **Class:** a11y
- **Severity:** S2
- **WCAG:** 1.4.3
- **Evidence:** Report records: `a.btn.btn-warning` / `button.btn.btn-warning` white on amber 2.19:1 (Create New Character, Create Character, Save Changes, Create Contact, Add Note, Rename, Save Profile); `btn-secondary` Cancel and Back 3.49:1; `btn-danger` Delete 3.82:1; `btn-success` Reactivate or Reveal 2.87:1; `btn-sm` 14px Rename 2.19:1 and Delete 3.82:1 (Manage). Baseline C8 to C11; rules `site.css:682-690` and `modern-card.css` button blocks.
- **Proposed fix:** Roles from the button-role table: `btn-warning` near-black label on amber; secondary, success, danger fills with at least 4.5:1 labels via `--bs-btn-*`.
- **Area phase:** 3 (Characters, Contacts, DM profile, Players)
- **Operator question:** N

### A-characters-012: Badge labels fail 4.5:1 (success 3.60, primary 3.57, secondary 3.72) and CR badges render at 12px

- **Page and layout:** Characters/Details (desktop); DungeonMaster/Profile (desktop)
- **Class:** a11y
- **Severity:** S2
- **WCAG:** 1.4.3
- **Evidence:** Report records `Characters-Details` desktop: `span.badge.bg-success` "Active" 3.60:1, `span.badge.bg-primary` class badge 3.57:1; `DungeonMaster-Profile` desktop: `span.badge.bg-secondary` "CR n" 3.72:1 at 12px (x15). Cause: `.modern-card span` repaints the badge label parchment (`modern-card.css:93`).
- **Proposed fix:** Badge label from `--qb-{role}-fg` on its fill at 14px, 600; surfaces no longer repaint spans.
- **Area phase:** 3 (Characters, Contacts, DM profile, Players)
- **Operator question:** N

### A-characters-013: Quest and Dungeon Master links render in default Bootstrap blue on the parchment table (2.23 to 2.85:1)

- **Page and layout:** DungeonMaster/Profile (desktop); Players/Index (desktop)
- **Class:** a11y
- **Severity:** S2
- **WCAG:** 1.4.3; 1.4.1
- **Evidence:** Report records `DungeonMaster-Profile` desktop (15 quest links) and `Players-Index` desktop (2 DM links): `a` `rgb(13,110,253)` 2.23:1 and 2.85:1 at B-med, composited through the 70% parchment cells (`~/qb-ui-review/phase-94/before/desktop/DungeonMaster-Profile.png`). Links are distinguished only by colour and underline. Markup `Views/DungeonMaster/Profile.cshtml:89`, `Views/Players/Index.cshtml:37`; rule `modern-card.css:297-299`.
- **Proposed fix:** Table links use `--qb-link` chosen for paper cells (at least 4.5:1) with the underline kept.
- **Area phase:** 3 (Characters, Contacts, DM profile, Players)
- **Operator question:** N

### A-characters-014: Ungrouped category heading is muted with `opacity: .75`, which drops it to 3.19:1

- **Page and layout:** Contacts/Index (both layouts)
- **Class:** cascade-fight
- **Severity:** S2
- **WCAG:** 1.4.3
- **Evidence:** Report records `Contacts-Index`: `h2.category-heading-ungrouped` 3.19:1 at B-max (20px 600), mobile `h2.contact-section-heading-ungrouped`. `wwwroot/css/contacts.css:285-288`, `contacts.mobile.css:24-27`. The `h2` element is the known workaround for the `.modern-card` element rules (`modern-card.css:78-90`): the comment at `contacts.css:283-284` says the muting avoids "an !important of its own".
- **Proposed fix:** Heading colour comes from the surface; the ungrouped variant uses `--qb-fg-muted` and italic only. Retire the `h2` workaround once the surface-inherits-colour rule is in place.
- **Area phase:** 3 (Characters, Contacts, DM profile, Players)
- **Operator question:** N

### A-characters-015: Character and contact cards show no visible keyboard focus

- **Page and layout:** Characters/Index (desktop); Contacts/Index (desktop)
- **Class:** a11y
- **Severity:** S2
- **WCAG:** 2.4.7; 1.4.11
- **Evidence:** BEFORE focus records `Characters-Index` stop 10 and `Contacts-Index` stop 9: 1px `rgb(16,16,16)` outline (1.07:1 on the dark card) and `outline: none` plus a 30% gold shadow on `:focus-within` (2.09:1); crop `~/qb-ui-review/phase-94/before/desktop/focus/Characters-Index/10.png` shows no ring. `characters.css:78-83`, `contacts.css:78-83`; the card clips with `overflow: hidden` (`characters.css:22`).
- **Proposed fix:** Two-tone ring (`--qb-focus-inner` and `--qb-focus-outer`) on `.row-nav-link` and the card link, drawn inset or on a wrapper that does not clip.
- **Area phase:** 3 (Characters, Contacts, DM profile, Players)
- **Operator question:** N

### A-characters-016: Tag entry box (Tagify) has no focus indicator

- **Page and layout:** Contacts/Create (desktop); Contacts/Edit (desktop)
- **Class:** a11y
- **Severity:** S2
- **WCAG:** 2.4.7; 1.4.11
- **Evidence:** BEFORE focus record `Contacts-Create` and `Contacts-Edit` desktop stop 14: the `span` "Tags input field" has `indicator: false`. `contacts.css:308-313` sets `--tags-focus-border-color: #ffc107` (gold on the near-white input, about 1.6:1). The mobile twin has the same override (`contact-form.mobile.css:69-74`); the focus probe runs on desktop only, so the mobile ring is unmeasured.
- **Proposed fix:** `.tagify:focus-within` takes the two-tone ring tokens; border colour from `--qb-control-border`.
- **Area phase:** 3 (Characters, Contacts, DM profile, Players)
- **Operator question:** N

### A-characters-017: Manage Categories table overflows the viewport at 320px on the desktop layout

- **Page and layout:** ContactCategoryManagement/Manage (desktop)
- **Class:** a11y
- **Severity:** S2
- **WCAG:** 1.4.10
- **Evidence:** Report record `ContactCategoryManagement-Manage` desktop, `overflow320: true`; `~/qb-ui-review/phase-94/audit/characters/probe1.json` key `overflow320`: `table.table.table-striped` is 353px wide in a 320px viewport (scrollWidth 390). `Views/ContactCategoryManagement/Manage.cshtml:34` has no `table-responsive` wrapper (Players/Index has one).
- **Proposed fix:** Wrap the table in `.table-responsive` (no control changes); the mobile layout is unaffected (390px check clean).
- **Area phase:** 3 (Characters, Contacts, DM profile, Players)
- **Operator question:** N

### A-characters-018: Dead and retired characters are dimmed with `opacity`, which pushes text under 4.5:1

- **Page and layout:** Characters/Index (both layouts)
- **Class:** a11y
- **Severity:** S2
- **WCAG:** 1.4.3
- **Evidence:** Computed with `~/qb-ui-review/phase-94/audit/characters/calc.mjs` (no dead-character fixture exists, so there is no capture): `.character-dead` opacity .5 gives 3.95:1 at B-med and 3.90:1 at B-max for the level line, 3.84 and 3.80 for the gold class line; retired (.7) stays at 6.3:1. `characters.css:85-99`, `characters.mobile.css:39-46`.
- **Proposed fix:** Express the state with a badge and a desaturated image only; text colour stays `--qb-fg`.
- **Area phase:** 3 (Characters, Contacts, DM profile, Players)
- **Operator question:** N

### A-characters-019: Mobile note panel uses grey `#6c757d` small text on a near-white panel (3.7:1) while the desktop panel is dark glass

- **Page and layout:** Contacts/Details (mobile)
- **Class:** a11y
- **Severity:** S2
- **WCAG:** 1.4.3
- **Evidence:** `~/qb-ui-review/phase-94/audit/characters/probe1.json` key `dmDetails-mobile` (synthetic note injected in the page, nothing saved): note `small.text-muted` 3.68:1 at B-med; panel `rgba(255,255,255,0.85)`; computed in `~/qb-ui-review/phase-94/audit/characters/calc.txt` 3.68 and 3.83. `contact-detail.mobile.css:107-115`. Desktop note panel is `rgba(0,0,0,.03)` (`contacts.css:214-216`) with parchment text.
- **Proposed fix:** One note surface for both layouts: `--qb-surface-paper` with `--qb-ink` and `--qb-ink-muted` (both pass), or the glass surface with `--qb-fg`.
- **Area phase:** 3 (Characters, Contacts, DM profile, Players)
- **Operator question:** N

### A-characters-020: Characters/Index: the desktop view has a page heading ("Characters") and the mobile view has none

- **Page and layout:** Characters/Index (both layouts)
- **Class:** twin-content
- **Severity:** S3
- **WCAG:** not applicable
- **Evidence:** `Views/Characters/Index.cshtml:10-13` (h1 on the board), `Index.Mobile.cshtml` has no heading element; section headings are `div.character-section-heading` (`Index.Mobile.cshtml:14,63`). `~/qb-ui-review/phase-94/before/mobile/Characters-Index.png`.
- **Proposed fix:** Answer OQ-1 first; if added, an h1 in the page-title style of the mobile layout.
- **Area phase:** 3 (Characters, Contacts, DM profile, Players)
- **Operator question:** Y

### A-characters-021: Contacts/Index: the desktop view has a page heading ("Contacts") and the mobile view has none when categories exist

- **Page and layout:** Contacts/Index (both layouts)
- **Class:** twin-content
- **Severity:** S3
- **WCAG:** not applicable
- **Evidence:** `Views/Contacts/Index.cshtml:63-66`; the mobile view only renders an `h2` "Contacts" in the no-category branch (`Index.Mobile.cshtml:138`).
- **Proposed fix:** Answer OQ-3 first.
- **Area phase:** 3 (Characters, Contacts, DM profile, Players)
- **Operator question:** Y

### A-characters-022: Contacts/Index: the mobile view has a Filter Tags button (disabled when the board has no tags) and an "Active" badge, the desktop view has neither

- **Page and layout:** Contacts/Index (both layouts)
- **Class:** twin-content
- **Severity:** S3
- **WCAG:** not applicable
- **Evidence:** `Views/Contacts/Index.Mobile.cshtml:79-87` (button, `disabled` when `AvailableTags.Count == 0`, badge when `HasActiveFilters`); desktop shows the filter row only when tags exist (`Index.cshtml:98`) and signals an active filter only through the Clear Filters link (`Index.cshtml:114-119`). Both views offer the same Apply and Clear actions.
- **Proposed fix:** Answer OQ-4 first. Pinned by `ContactsTagsDesktopMarkupTests` for desktop (filter row and no-tags behaviour).
- **Area phase:** 3 (Characters, Contacts, DM profile, Players)
- **Operator question:** Y

### A-characters-023: ContactCategoryManagement/Manage: the desktop view has a page heading ("Manage Categories") and the mobile view has none

- **Page and layout:** ContactCategoryManagement/Manage (both layouts)
- **Class:** twin-content
- **Severity:** S3
- **WCAG:** not applicable
- **Evidence:** `Views/ContactCategoryManagement/Manage.cshtml:10-13`; `Manage.Mobile.cshtml` starts with the add form.
- **Proposed fix:** Answer OQ-5 first.
- **Area phase:** 3 (Characters, Contacts, DM profile, Players)
- **Operator question:** Y

### A-characters-024: Characters/Create and Characters/Edit: the desktop view marks Classes as required, the mobile view does not

- **Page and layout:** Characters/Create (both layouts); Characters/Edit (both layouts)
- **Class:** twin-content
- **Severity:** S3
- **WCAG:** 3.3.2
- **Evidence:** Desktop `Create.cshtml:73` and `Edit.cshtml:75` carry `<span class="text-danger">*</span>` on the Classes label; mobile `Create.Mobile.cshtml:55` and `Edit.Mobile.cshtml:67` do not, although each class select is `required` on both.
- **Proposed fix:** Answer OQ-2 first.
- **Area phase:** 3 (Characters, Contacts, DM profile, Players)
- **Operator question:** Y

### A-characters-025: DungeonMaster/Profile: the mobile view shows "Date TBD" for an undated quest, the desktop view leaves the cell empty

- **Page and layout:** DungeonMaster/Profile (both layouts)
- **Class:** twin-content
- **Severity:** S3
- **WCAG:** not applicable
- **Evidence:** Desktop `Views/DungeonMaster/Profile.cshtml:98-100` renders nothing for a null date; mobile `Profile.Mobile.cshtml:80,95` renders "Date TBD". In the BEFORE capture 13 of 15 desktop rows have an empty Date cell (`~/qb-ui-review/phase-94/before/desktop/DungeonMaster-Profile.png`).
- **Proposed fix:** Answer OQ-6 first.
- **Area phase:** 3 (Characters, Contacts, DM profile, Players)
- **Operator question:** Y

### A-characters-026: Heading structure: no h1 on most pages, mobile section headings are `div`s, desktop levels skip

- **Page and layout:** Characters/Index (both layouts); Characters/Details (both layouts); Contacts/Index (both layouts); Contacts/Details (both layouts); ContactCategoryManagement/Manage (both layouts); ContactCategoryManagement/Edit (both layouts); DungeonMaster/Profile (both layouts); Players/Index (both layouts); Characters/Create (mobile); Characters/Edit (mobile); Contacts/Create (mobile); Contacts/Edit (mobile); DungeonMaster/EditProfile (mobile)
- **Class:** a11y
- **Severity:** S3
- **WCAG:** 1.3.1; 2.4.6
- **Evidence:** Mobile section headings are `div.character-section-heading` (`Characters/Index.Mobile.cshtml:14`, `Details.Mobile.cshtml:49,101`), `div.contact-section-heading` (`Contacts/Details.Mobile.cshtml:64,94,107`), `div.players-section-heading` (`Players/Index.Mobile.cshtml:18,53`). Desktop jumps h2 to h5 to h2 (`DungeonMaster/Profile.cshtml:28,46,67`) and h2 to h4 (`Characters/Details.cshtml:27,65,104`). Page titles are h1 only on Characters/Index and Contacts/Index desktop; form pages use h2, h4 or h5 in the card header.
- **Proposed fix:** Real headings with one h1 per page (title in the card header or beside it), styled by class not by level; mobile `div` headings become headings.
- **Area phase:** 3 (Characters, Contacts, DM profile, Players)
- **Operator question:** N

### A-characters-027: Desktop detail pages use `<label>` for read-only values

- **Page and layout:** Characters/Details (desktop)
- **Class:** a11y
- **Severity:** S3
- **WCAG:** 1.3.1; 4.1.2
- **Evidence:** `Views/Characters/Details.cshtml:109,113,130,151,159`: `label.form-label.fw-bold` with no associated control (the mobile twin uses `span.form-label`). `fw-bold` is weight 700 (spec allows 400 and 600).
- **Proposed fix:** A definition-style pair (`dl`/`dt`/`dd` or labelled text) instead of `label`; weight 600.
- **Area phase:** 3 (Characters, Contacts, DM profile, Players)
- **Operator question:** N

### A-characters-028: Mobile row links are 22px tall, under the 44px mobile target (the row itself is tappable through an inline click handler)

- **Page and layout:** Characters/Index (mobile); Contacts/Index (mobile); Players/Index (mobile)
- **Class:** a11y
- **Severity:** S3
- **WCAG:** 2.5.8 (24px) and D-16 (44px mobile)
- **Evidence:** `~/qb-ui-review/phase-94/audit/characters/probe2.json`: `a.row-nav-link` 105x22 (Characters), 165x22 (Contacts), 93x22 and 171x22 (Players). The whole row carries `onclick="window.location.href=..."` (`Characters/Index.Mobile.cshtml:21,70`, `Contacts/Index.Mobile.cshtml:11`, `Players/Index.Mobile.cshtml:29`) so touch works, but the focusable and announced target is the 22px link.
- **Proposed fix:** `.row-nav-link` stretches over the whole row (`::after` hit area, A-5); the inline handler goes.
- **Area phase:** 3 (Characters, Contacts, DM profile, Players)
- **Operator question:** N

### A-characters-029: Desktop Players table links are 22px tall, under the 24px desktop minimum

- **Page and layout:** Players/Index (desktop)
- **Class:** a11y
- **Severity:** S3
- **WCAG:** 2.5.8
- **Evidence:** `~/qb-ui-review/phase-94/audit/characters/probe2.json` `Players-Index` desktop: `a` 100x22 and 181x22 (not inline in a sentence). `Views/Players/Index.cshtml:37`.
- **Proposed fix:** Table-cell link padding to at least 24px (`--qb-hit-min` rule for desktop).
- **Area phase:** 3 (Characters, Contacts, DM profile, Players)
- **Operator question:** N

### A-characters-030: Icon-only note buttons are named by `title` only

- **Page and layout:** Contacts/Details (both layouts)
- **Class:** a11y
- **Severity:** S3
- **WCAG:** 4.1.2; 2.5.3
- **Evidence:** `Views/Contacts/Details.cshtml:158,164` and `Details.Mobile.cshtml:143,149`: `title="Edit note"` / `title="Delete note"` on icon-only buttons with no `aria-label`. (The reorder buttons on Manage Categories already carry `aria-label`.)
- **Proposed fix:** `aria-label` on both buttons; keep the `title`.
- **Area phase:** 3 (Characters, Contacts, DM profile, Players)
- **Operator question:** N

### A-characters-031: Required marker is red on desktop forms and parchment on mobile forms

- **Page and layout:** Characters/Create (mobile); Characters/Edit (mobile); Contacts/Create (mobile); Contacts/Edit (mobile); Contacts/Details (mobile)
- **Class:** twin-drift
- **Severity:** S3
- **WCAG:** 1.4.1
- **Evidence:** Report records: desktop `span.text-danger` `rgb(255,107,107)`, mobile `span.text-danger` `rgb(244,228,188)` (`Contacts-Create` mobile). Cause: the mobile catch-all `.contact-form-card span:not(.badge)` (`contact-form.mobile.css:22-28`) and `character-form.mobile.css:21-27` repaint the marker, so the cue that a field is required has no colour on phones.
- **Proposed fix:** Same token on both layouts: `--qb-danger-text` plus the words "required" in the label for assistive tech.
- **Area phase:** 3 (Characters, Contacts, DM profile, Players)
- **Operator question:** N

### A-characters-032: Notes panel looks different on the two layouts (dark glass on desktop, near-white paper on mobile)

- **Page and layout:** Contacts/Details (both layouts)
- **Class:** twin-drift
- **Severity:** S3
- **WCAG:** not applicable
- **Evidence:** `contacts.css:214-216` against `contact-detail.mobile.css:85-115`; `~/qb-ui-review/phase-94/audit/characters/shots/contact-details-dm-desktop.png` and `contact-details-dm-mobile.png`.
- **Proposed fix:** One note surface from the surface tokens on both layouts (see the note-panel contrast finding).
- **Area phase:** 3 (Characters, Contacts, DM profile, Players)
- **Operator question:** N

### A-characters-033: Page titles use five different treatments

- **Page and layout:** Characters/Index (both layouts); Contacts/Index (both layouts); Characters/Create (both layouts); Characters/Edit (both layouts); Contacts/Create (both layouts); Contacts/Edit (both layouts); ContactCategoryManagement/Manage (both layouts); ContactCategoryManagement/Edit (both layouts); DungeonMaster/EditProfile (both layouts); Players/Index (both layouts); DungeonMaster/Profile (both layouts)
- **Class:** inconsistency
- **Severity:** S3
- **WCAG:** not applicable
- **Evidence:** h1 on the board with an icon and a custom glow (`characters.css:5-9`, `contacts.css:5-9`: the same rule twice); h2 in the card header (`Characters/Create.cshtml:13`, `ContactCategoryManagement/Manage.cshtml:10`, `DungeonMaster/EditProfile.cshtml:13`); h4 in the card header (`Contacts/Create.cshtml:11`); h5 on mobile forms; no title at all on Players. The UI guideline says the card-header pattern.
- **Proposed fix:** One page-title component (`modern-card-header` + Display step desktop, Heading step mobile).
- **Area phase:** 3 (Characters, Contacts, DM profile, Players)
- **Operator question:** N

### A-characters-034: Form button rows disagree on order and on the `<hr>` the guideline asks for

- **Page and layout:** Characters/Create (both layouts); Characters/Edit (both layouts); Contacts/Create (both layouts); Contacts/Edit (both layouts); ContactCategoryManagement/Edit (both layouts); DungeonMaster/EditProfile (both layouts)
- **Class:** inconsistency
- **Severity:** S3
- **WCAG:** not applicable
- **Evidence:** Characters Create and Edit desktop put submit on the left with no `<hr>` and no `justify-content-between` (`Create.cshtml:137-148`); Contacts Create and Edit and Category Rename desktop put Cancel left and submit right with `<hr>` (`Contacts/Create.cshtml:91-100`); every mobile form puts submit first (`Contacts/Create.Mobile.cshtml:86-93`) except Category Rename mobile, which puts Cancel first (`Edit.Mobile.cshtml:22-29`). `.claude/ui-guidelines.md`: secondary left, primary right, `<hr>` before the row.
- **Proposed fix:** One button-row component (`<hr>`, secondary left, primary right) on desktop; one stacked order on mobile, chosen once.
- **Area phase:** 3 (Characters, Contacts, DM profile, Players)
- **Operator question:** Y (Listed as a relayout: see R-2.)

### A-characters-035: Outline buttons in a "filled, not outline" system

- **Page and layout:** shared partial: Contacts/Index (both layouts); Contacts/Details (both layouts); ContactCategoryManagement/Manage (both layouts); Shared/_CharacterSelectModal (both layouts)
- **Class:** inconsistency
- **Severity:** S3
- **WCAG:** not applicable
- **Evidence:** `Views/Contacts/Index.cshtml:83`, `Index.Mobile.cshtml:66` (Show Hidden), `Contacts/Details.cshtml:158,164` (note edit and delete), `ContactCategoryManagement/Manage.cshtml:51,58` and `Manage.Mobile.cshtml:32,39` (reorder), `_CharacterSelectModal.cshtml:46` (Remove).
- **Proposed fix:** Filled roles per the button-role table (`btn-secondary`, `btn-danger`); toggled state carried by `aria-pressed` styling, not by outline versus filled.
- **Area phase:** 3 (Characters, Contacts, DM profile, Players)
- **Operator question:** N

### A-characters-036: Two amber buttons stack on the mobile Contacts page, so there is no single primary action

- **Page and layout:** Contacts/Index (mobile)
- **Class:** inconsistency
- **Severity:** S3
- **WCAG:** not applicable
- **Evidence:** `Views/Contacts/Index.Mobile.cshtml:74-76` (`+ Contact`, `btn-warning`) and `:79-87` (Filter Tags, `btn-warning` full width). `~/qb-ui-review/phase-94/before/mobile/Contacts-Index.png`.
- **Proposed fix:** `+ Contact` is the one `--qb-action`; Filter Tags becomes `btn-secondary`. Recolour only; no reorder needed.
- **Area phase:** 3 (Characters, Contacts, DM profile, Players)
- **Operator question:** N

### A-characters-037: Mobile sheets repaint text with element-enumerated `!important` catch-alls, which defeat their own intent

- **Page and layout:** Characters/Index (mobile); Characters/Details (mobile); Characters/Create (mobile); Characters/Edit (mobile); Contacts/Index (mobile); Contacts/Details (mobile); Contacts/Create (mobile); Contacts/Edit (mobile); ContactCategoryManagement/Manage (mobile); ContactCategoryManagement/Edit (mobile); DungeonMaster/Profile (mobile); DungeonMaster/EditProfile (mobile); Players/Index (mobile)
- **Class:** cascade-fight
- **Severity:** S3
- **WCAG:** 1.4.3
- **Evidence:** `character-detail.mobile.css:81-88`, `contact-detail.mobile.css:76-83`, `character-form.mobile.css:21-27`, `contact-form.mobile.css:22-28` recolour `p`, `a`, `li`, `span:not(.badge)`; by specificity they also override the sheets' own faded rules: on Characters/Details mobile `p.character-info-value` ("Owned by …") renders full parchment 4.34:1 although `character-detail.mobile.css:54-60` asks for 70% (report record). Per-sheet `!important` counts are in the stylesheet table below (83 across the nine mobile sheets of this area).
- **Proposed fix:** Delete the families; the surface sets `color` and descendants inherit.
- **Area phase:** 3 (Characters, Contacts, DM profile, Players)
- **Operator question:** N

### A-characters-038: Tag-entry override and crop-stage height win with `!important` against inline and element rules

- **Page and layout:** Contacts/Create (both layouts); Contacts/Edit (both layouts); Characters/Create (both layouts); Characters/Edit (both layouts); DungeonMaster/EditProfile (both layouts)
- **Class:** cascade-fight
- **Severity:** S3
- **WCAG:** not applicable
- **Evidence:** `contacts.css:321-329` and `contact-form.mobile.css:85-93` force `color`, `text-shadow` and a `::before` colour with `!important` on `.tagify__input` (to undo `.modern-card span`); `image-crop.css:20` forces the canvas height with `!important` to beat the inline `style="height:..."` in the views (`Contacts/Create.cshtml:118`).
- **Proposed fix:** Move the canvas height to a class (no inline style) and let the Tagify theme read the tokens; no `!important`.
- **Area phase:** 3 (Characters, Contacts, DM profile, Players)
- **Operator question:** N

### A-characters-039: The card and filter stylesheets for Characters and Contacts are the same code under two names

- **Page and layout:** Characters/Index (desktop); Contacts/Index (desktop)
- **Class:** inconsistency
- **Severity:** S3
- **WCAG:** not applicable
- **Evidence:** `characters.css:5-276` and `contacts.css:5-259` are line-for-line copies (grid, card, shimmer, ripple, badges, empty states) with `character` replaced by `contact`; the 15-stylesheet table below shows 304 and 428 lines. Cards use an opaque navy gradient `rgba(20,20,30,.95)` (`characters.css:19`, `contacts.css:19`), a navy reserved for the Shop theme in the colour rules.
- **Proposed fix:** One shared card component on the surface tokens; the two pages differ only in content classes.
- **Area phase:** 3 (Characters, Contacts, DM profile, Players)
- **Operator question:** N

### A-characters-040: Infinite pulse on the Main badge animates `box-shadow`

- **Page and layout:** Characters/Index (desktop)
- **Class:** perf
- **Severity:** S3
- **WCAG:** 2.2.2 (A-7)
- **Evidence:** `characters.css:203-213` (`main-badge-pulse 2s infinite`, `box-shadow` keyframes). Visible only when the viewer has a Main character, which the fixture does not, so there is no capture; the rule is in the global stylesheet list on every page.
- **Proposed fix:** No infinite animation (or only `transform` and `opacity`, reduced-motion honoured); Characters area phase caps it.
- **Area phase:** 3 (Characters, Contacts, DM profile, Players)
- **Operator question:** N

### A-characters-041: Heavy hover effects and `transition: all` on repeated cards and badges

- **Page and layout:** Characters/Index (desktop); Contacts/Index (desktop)
- **Class:** perf
- **Severity:** S3
- **WCAG:** not applicable
- **Evidence:** `characters.css` has 6 `transition: all` (lines 23,125,153,217,231,244), 2 `backdrop-filter: blur(5px)` on per-card badges (171,186) and a 3-shadow hover; `contacts.css` has 8 `transition: all` and 1 `backdrop-filter` (150). Hover moves each card with `translateY(-8px) scale(1.02)` plus a sweeping `::before` and a ripple `::after` per card (`characters.css:29-62,301-303`).
- **Proposed fix:** Hover lift only on interactive cards, `transform` and `opacity` only, no `backdrop-filter` on a repeated item, at most two cheap shadows.
- **Area phase:** 3 (Characters, Contacts, DM profile, Players)
- **Operator question:** N

### A-characters-042: Text under the 14px floor

- **Page and layout:** Characters/Index (desktop); Contacts/Index (both layouts); Contacts/Details (mobile); DungeonMaster/Profile (desktop)
- **Class:** a11y
- **Severity:** S3
- **WCAG:** 1.4.4 (size rule)
- **Evidence:** `characters.css:169,184,199` (0.75rem badges), `contacts.css:148,344` (hidden badge, tag chips), `contacts.mobile.css:121,148` and `contact-detail.mobile.css:131` (0.75rem), `contacts.mobile.css:168` (0.8125rem); Bootstrap `.badge` renders at 12px on `DungeonMaster/Profile` desktop (report record, 15 CR badges).
- **Proposed fix:** `--qb-text-sm` (14px) is the smallest step.
- **Area phase:** 3 (Characters, Contacts, DM profile, Players)
- **Operator question:** N

### A-characters-043: Raw `DateTime.ToString` on the quest history date

- **Page and layout:** DungeonMaster/Profile (both layouts)
- **Class:** inconsistency
- **Severity:** S3
- **WCAG:** not applicable
- **Evidence:** `Views/DungeonMaster/Profile.cshtml:99` `@quest.Date?.ToString("MMM dd, yyyy")` and `Profile.Mobile.cshtml:80,95`; the project rule is `Html.WallClock` for a wall-clock value. The helper's `date` style is `MMM d, yyyy` (`HtmlHelperExtensions.cs:33`), so the day loses its zero padding ("Nov 05" becomes "Nov 5").
- **Proposed fix:** `Html.WallClock(quest.Date.Value, "date")` on both twins; this is a visible format change, confirm at sign-off.
- **Area phase:** 3 (Characters, Contacts, DM profile, Players)
- **Operator question:** N

### A-characters-044: Inline click handlers on mobile rows and inline `confirm(` on delete forms

- **Page and layout:** shared partial: Characters/Index (mobile); Characters/Details (both layouts); Contacts/Index (mobile); Contacts/Details (both layouts); ContactCategoryManagement/Manage (both layouts); DungeonMaster/Profile (mobile); Players/Index (mobile); Shared/_CharacterSelectModal (both layouts)
- **Class:** inconsistency
- **Severity:** S3
- **WCAG:** not applicable
- **Evidence:** Handlers: `Characters/Index.Mobile.cshtml:21,70`; `Contacts/Index.Mobile.cshtml:11`; `Players/Index.Mobile.cshtml:29`; `DungeonMaster/Profile.Mobile.cshtml:71`; `onsubmit="return confirm(...)"` at `Characters/Details.cshtml:89` and its twin `:123`, `Contacts/Details.cshtml:89,161` and twin `:83,146`; delete confirm script `ContactCategoryManagement/Manage.cshtml:105-116` and twin; `_CharacterSelectModal.cshtml:125`. Wording and behaviour stay (copy contract).
- **Proposed fix:** Handlers move to a module that reads `data-` attributes; `confirm(` text and flow unchanged.
- **Area phase:** 3 (Characters, Contacts, DM profile, Players)
- **Operator question:** N

### A-characters-045: Label and button copy differ between twins (text only)

- **Page and layout:** Characters/Details (both layouts); Contacts/Index (both layouts)
- **Class:** twin-drift
- **Severity:** S4
- **WCAG:** not applicable
- **Evidence:** Characters/Details: desktop "Sheet Link", "Class(es)", card "Actions"; mobile "Character Sheet", "Classes", "Owner Actions" (`Details.cshtml:113,130,65`, `Details.Mobile.cshtml:55,66,101`). Contacts/Index: "Manage Categories" against "Categories" (`Index.cshtml:89`, `Index.Mobile.cshtml:72`); "Clear filters" against "Clear Filters". The same capability sits on both sides, so this is not a content difference; area phase keeps each text unless the operator asks for one wording.
- **Proposed fix:** Aligned wording only on request; no change by default (D-14).
- **Area phase:** 3 (Characters, Contacts, DM profile, Players)
- **Operator question:** N

### A-characters-046: Help text class differs on mobile (`form-text` against `form-text text-muted`)

- **Page and layout:** Contacts/Create (mobile); Contacts/Edit (mobile); Characters/Create (mobile); Characters/Edit (mobile); DungeonMaster/EditProfile (mobile)
- **Class:** twin-drift
- **Severity:** S4
- **WCAG:** not applicable
- **Evidence:** `Contacts/Create.Mobile.cshtml:24` `small.form-text` for the image hint and `:51` `small.form-text.text-muted` for the tags hint on the same page; desktop uses `text-muted` on both.
- **Proposed fix:** One hint class (`.form-text`) styled by the surface.
- **Area phase:** 3 (Characters, Contacts, DM profile, Players)
- **Operator question:** N

### A-characters-047: Contact form image column leaves an empty block under the file input; field baselines are uneven

- **Page and layout:** Contacts/Create (desktop); Contacts/Edit (desktop); Characters/Create (desktop); Characters/Edit (desktop)
- **Class:** visual-bug
- **Severity:** S4
- **WCAG:** not applicable
- **Evidence:** `~/qb-ui-review/phase-94/before/desktop/Contacts-Create.png`: the 4-column image cell holds only the file input and a hint while the 8-column cell holds three stacked fields, and the Name and Town rows sit at different heights.
- **Proposed fix:** Align the image block with the field stack on the 8px grid; a restyle within D-04.
- **Area phase:** 3 (Characters, Contacts, DM profile, Players)
- **Operator question:** N

### A-characters-048: Desktop DM pages nest a second `container` and so sit narrower than every other page

- **Page and layout:** DungeonMaster/Profile (desktop); DungeonMaster/EditProfile (desktop)
- **Class:** inconsistency
- **Severity:** S4
- **WCAG:** not applicable
- **Evidence:** `Views/DungeonMaster/Profile.cshtml:9` (`container mt-4`) and `EditProfile.cshtml:8`; `~/qb-ui-review/phase-94/before/desktop/DungeonMaster-Profile.png` (84px side margin against 72px).
- **Proposed fix:** Page width from the layout; no per-view `container`.
- **Area phase:** 3 (Characters, Contacts, DM profile, Players)
- **Operator question:** N

### A-characters-049: Validation summary mode differs

- **Page and layout:** DungeonMaster/EditProfile (both layouts)
- **Class:** inconsistency
- **Severity:** S4
- **WCAG:** not applicable
- **Evidence:** `DungeonMaster/EditProfile.cshtml:21` and twin use `asp-validation-summary="ModelOnly"`; every other form in the area uses `All`.
- **Proposed fix:** Decide once for the form component.
- **Area phase:** 3 (Characters, Contacts, DM profile, Players)
- **Operator question:** N

### A-characters-050: Card images repeat the name that the link already announces

- **Page and layout:** Characters/Index (both layouts); Contacts/Index (both layouts)
- **Class:** a11y
- **Severity:** S4
- **WCAG:** 1.1.1
- **Evidence:** `Characters/Index.cshtml:38,113`, `Contacts/Index.cshtml:18` and the mobile rows (`Index.Mobile.cshtml:24,73`): `alt="@name"` inside a link whose text is the same name.
- **Proposed fix:** `alt=""` where the name is adjacent, as the plan for decorative images.
- **Area phase:** 3 (Characters, Contacts, DM profile, Players)
- **Operator question:** N

### A-characters-051: External character sheet link opens a new tab without telling the user

- **Page and layout:** Characters/Details (both layouts)
- **Class:** a11y
- **Severity:** S4
- **WCAG:** 3.2.5 (advisory)
- **Evidence:** `Characters/Details.cshtml:117`, `Details.Mobile.cshtml:58`: `target="_blank" class="text-break"` with an icon only, no `rel`, user-entered URL.
- **Proposed fix:** Add `rel="noopener noreferrer"` and a visually hidden "(opens in a new tab)".
- **Area phase:** 3 (Characters, Contacts, DM profile, Players)
- **Operator question:** N

### A-characters-052: Create view carries Edit branches that cannot run

- **Page and layout:** Characters/Create (desktop)
- **Class:** dead-code
- **Severity:** S4
- **WCAG:** not applicable
- **Evidence:** `Views/Characters/Create.cshtml:6-7,13-14,19-23,31,139-140` branch on `isEdit` (`Model.Id > 0`), but Edit has its own view (`Edit.cshtml`) and Create is never rendered with an id.
- **Proposed fix:** Remove the branches when the page is converted.
- **Area phase:** 3 (Characters, Contacts, DM profile, Players)
- **Operator question:** N

### A-characters-053: Unused `.badge.bg-purple` rule

- **Page and layout:** DungeonMaster/Profile (desktop)
- **Class:** dead-code
- **Severity:** S4
- **WCAG:** not applicable
- **Evidence:** `wwwroot/css/dm-profile.css:21-25` ("Deadly difficulty badge"); search of all views, scripts and C# found no `bg-purple` use. The sheet is otherwise two photo rules.
- **Proposed fix:** Delete the rule (purple belongs to the `--qb-event` role).
- **Area phase:** 3 (Characters, Contacts, DM profile, Players)
- **Operator question:** N

### A-characters-054: Note-heading colour override that never applies

- **Page and layout:** Contacts/Details (desktop)
- **Class:** dead-code
- **Severity:** S4
- **WCAG:** not applicable
- **Evidence:** `wwwroot/css/contacts.css:218-232` (dark `#1a1a1a` headings in notes, written for a near-white panel). `~/qb-ui-review/phase-94/audit/characters/probe1.json` key `dmDetails-desktop` `note.h`: the computed heading colour is `rgb(244,228,188)` because `.modern-card h2 { color: ... !important }` wins; the desktop panel is `rgba(0,0,0,.03)`. On the mobile twin the equivalent rule does apply (near-white panel).
- **Proposed fix:** Delete the dead rule with the note-surface change.
- **Area phase:** 3 (Characters, Contacts, DM profile, Players)
- **Operator question:** N

### A-characters-055: Inline style attributes, duplicated inline scripts and CDN script tags

- **Page and layout:** shared partial: Characters/Create (both layouts); Characters/Edit (both layouts); Contacts/Create (both layouts); Contacts/Edit (both layouts); Contacts/Details (both layouts); ContactCategoryManagement/Manage (both layouts); DungeonMaster/EditProfile (both layouts); Players/Index (both layouts); DungeonMaster/Profile (both layouts); Shared/_CharacterSelectModal (both layouts)
- **Class:** inconsistency
- **Severity:** S4
- **WCAG:** not applicable
- **Evidence:** Inline `style=`: Characters/Create 4 desktop and 5 mobile, Edit 4 and 6, Contacts/Create 3 and 5, Edit 4 and 6, Details 1 and 1, Manage 2 (table column widths, `Manage.cshtml:37,39`), DM EditProfile 3 and 3, Profile mobile 1 (`:26`), Players mobile 1 (`:31`). Inline `<script>`: add-class and remove-class code duplicated in Characters Create and Edit and both mobile twins (`Create.cshtml:201-241`), note-editor code duplicated in both Contacts/Details twins (`Details.cshtml:211-265`), category delete code in both Manage twins. Cropper.js and Tagify load from jsDelivr per view (`Contacts/Create.cshtml:156,166`); the Tagify stylesheet `<link>` sits inside `@section Scripts`, mid-body (`:163`). The vendor plan self-hosts both; only the markup is audited here.
- **Proposed fix:** Classes for the hidden file input, error box and crop canvas; ES modules for the class list, note editor and delete confirm; vendor URLs from the self-hosted paths.
- **Area phase:** 3 (Characters, Contacts, DM profile, Players)
- **Operator question:** N

## Twin comparison

Procedure (D-13, D-14): from both files of each pair extract form fields (`asp-for`, `name=`), links and buttons (`asp-action`, `href`, `type="submit"`), headed sections (`h1` to `h6` and card blocks) and the `BoardPages` conditions guarding them, then compare the sets. Machine extraction: `~/qb-ui-review/phase-94/audit/characters/inventories.json` (written by `inv.py`) and the comparison `cmp.py`; the summaries below list what each side holds. Classes: identical, layout-only, content-differs, deliberate. No deliberate differences are recorded for this area.

### Characters/Index: content-differs

- **Inventories behind the classification:** Fields: none. Links and buttons: both have `Create New Character` (desktop also in the empty state "Create Character"; mobile same), cards or rows linking to Details. Headed sections: desktop h1 "Characters" plus h3 "My Characters" and "Character Roster"; mobile has no h1 and two `div` headings with the same text. `BoardPages` conditions: none.
- **Result:** The page heading exists only on desktop (OQ-1). Grid of cards against list rows is a rendering difference with the same data (name, classes, level, owner in the roster, Main, Dead, Retired).

### Characters/Details: layout-only

- **Inventories behind the classification:** Fields: hidden `id` in the Retire/Reactivate and Delete forms (both). Links and buttons (both): Edit Character, Retire or Reactivate Character, Delete Character (`confirm(`), Back to Characters, sheet link. Sections (both): portrait card, character information, actions (desktop "Actions" `h5`, mobile "Owner Actions"), same optional blocks (Description, Backstory). `BoardPages`: none.
- **Result:** Same inventory; copy differs in three labels (twin-drift finding, no question). Merge candidate M-1.

### Characters/Create: content-differs

- **Inventories behind the classification:** Fields (both): `OwnerId` (hidden), `ProfilePictureFile`, `CroppedPictureFile`, `Name`, `Level`, `Status`, `Role`, `Classes[i].Class`, `Classes[i].ClassLevel`, `SheetLink`, `Description`, `Backstory`; Add Another Class, Remove, Create Character, Cancel, crop dialog. Desktop alone has `Id` in a dead branch (finding). Headings: desktop h2 card title, mobile h5.
- **Result:** The Classes label carries the required marker on desktop only (OQ-2). Everything else is layout only.

### Characters/Edit: content-differs

- **Inventories behind the classification:** As Create plus hidden `Id`, Re-crop Photo, current-image preview and the Main hint "Setting as Main will change your other characters to Backup"; the Cancel link goes to Details on both.
- **Result:** Required marker on Classes desktop only (OQ-2); otherwise layout only.

### Contacts/Index: content-differs

- **Inventories behind the classification:** Fields: tag filter `tag` checkboxes (inline row on desktop, bottom offcanvas on mobile); hidden `tag` copies in the Show Hidden form (both). Links and buttons: Show Hidden or Hide Hidden (`aria-pressed`), Contacts category management ("Manage Categories" desktop, "Categories" mobile), `Contact` (create), Apply Filters, Clear Filters; cards or rows to Details. Sections: desktop h1 "Contacts", category `h2` blocks; mobile `h2` per category. DM-tier visibility (`ViewerIsDmTier`) guards the toolbar and tags on both. `BoardPages`: none (controller attribute).
- **Result:** The page heading (OQ-3) and the Filter Tags trigger with its disabled state and Active badge (OQ-4) exist on one side only. Offcanvas against inline filter is the same Apply and Clear actions in two containers.

### Contacts/Details: layout-only

- **Inventories behind the classification:** Fields (both): hidden `id` and `contactId`, the note editors (`Text`, `Text_{id}`). Links and buttons (both): Edit Contact, Reveal or Hide Contact, Delete Contact (`confirm(`), Add Note, note edit (icon), note delete (icon, `confirm(`), Save and Cancel in the note editor, Back to Contacts. Sections (both): portrait, actions (DM tier), description, notes. `BoardPages`: none (`CanManage` gates the actions).
- **Result:** Same inventory. Differences: actions card sits in the left column on desktop and above Description on mobile; the tag and category lines swap order; the notes panel is a different surface. Merge candidate M-2.

### Contacts/Create: layout-only

- **Inventories behind the classification:** Fields (both): `ContactImageFile` (`#contactImageInput`), `CroppedPictureFile` (hidden), `Name`, `TownCity`, `SubLocation`, `TagsInput`, `CategoryId` (select, or a disabled select plus the "Manage Categories" link when there are none), `Description` (markdown partial). Links and buttons (both): `Create Contact` (`type="submit"`), `Cancel` (to Index), crop dialog buttons `Discard Photo`, `Use This Crop`, close; hint link to `ContactCategoryManagement/Index`. Headed sections (both): one form card titled "Create New Contact" (desktop `h4` in `.modern-card-header`; mobile `h4` in `.contact-form-card`) and the crop dialog `h5`. `BoardPages` conditions: none in either file.
- **Result:** Differences are layout only: desktop is a two-column grid (image 4 columns beside Name and the Town/Sub-location pair) inside `.modern-card`, mobile is one column inside `.contact-form-card`; button order (desktop Cancel left, submit right with an `<hr>`; mobile submit first, `gap-2`); the crop dialog is `modal-lg` with a 400px stage on desktop and 320px on mobile; the mobile image hint drops `text-muted`. Same field set, same actions, same dialog. Merge candidate M-3.

### Contacts/Edit: layout-only

- **Inventories behind the classification:** Same as Contacts/Create plus hidden `Id`, Re-crop Photo and the current-image preview (both layouts); Cancel goes to Details; Save Changes. Identical apart from layout.
- **Result:** Merge candidate M-4.

### ContactCategoryManagement/Manage: content-differs

- **Inventories behind the classification:** Fields (both): `NewCategory.Name`, anti-forgery tokens. Links and buttons (both): Add Category, Move up and down (`aria-label`), Rename, Delete (`confirm(`), Back to Contacts. Sections: desktop h2 "Manage Categories" card with a table (Order, Name, Contacts, Actions); mobile rows with name and "N contacts". `BoardPages`: none.
- **Result:** The page heading is desktop-only (OQ-5). Table against rows carries the same data and actions.

### ContactCategoryManagement/Edit: layout-only

- **Inventories behind the classification:** Fields (both): `Id` (hidden), `Name`. Buttons: Cancel (Index), Rename. Section: one card titled "Rename Category" (desktop h2 in the header, mobile h2 inside the card). `BoardPages`: none.
- **Result:** Same inventory; desktop centres the card in a 6-column container; mobile order has Cancel first like desktop. Merge candidate M-5.

### DungeonMaster/Profile: content-differs

- **Inventories behind the classification:** Links and buttons: Edit Profile (`CanEdit`), a link per quest to Quest/Details only when `BoardPages` serves Quests (both views evaluate `questsOn`). Sections: photo, name, role label, About, Quest History (desktop table with Title, Date, Difficulty columns; mobile cards with title, CR badge, date). `BoardPages`: `questsOn = Serves(BoardPage.Quests)` in both.
- **Result:** Undated quests show an empty cell on desktop and "Date TBD" on mobile (OQ-6). Everything else is layout only.

### DungeonMaster/EditProfile: layout-only

- **Inventories behind the classification:** Fields (both): `DungeonMasterId` (hidden), `ProfilePictureFile`, `CroppedPictureFile`, `Bio` (markdown, max 2000). Buttons: Re-crop Photo, Back to Profile, Save Profile, crop dialog. Section: one card "Edit DM Profile". `BoardPages`: none.
- **Result:** Same inventory; desktop is two columns, mobile photo above bio; mobile photo is 80px. Merge candidate M-6.

### Players/Index: layout-only

- **Inventories behind the classification:** Links (both): a DM profile link per Dungeon Master when `BoardPages` serves `DungeonMasterProfile` (both views evaluate `dungeonMasterProfileOn`). Sections (both): Dungeon Masters and Registered Players (desktop h2 cards with tables and a Name column header; mobile `div` headings and rows; mobile adds a chevron on linked rows). `BoardPages`: `Serves(BoardPage.DungeonMasterProfile)` in both.
- **Result:** Same inventory; table against rows is a rendering difference, not a merge candidate (the markup differs beyond CSS).

Shared partials: `_CharacterSelectModal` and `_MarkdownEditor` are single partials rendered under both layouts, so they have no twin to compare.

## Operator questions

Standard wording; answers: *Deliberate, record it* or *Add it to the other view*. Removal is never offered (D-04). Until answered, nothing is added or removed.

- **OQ-1** (finding A-characters-020): Characters/Index: the desktop view has the page heading "Characters" and the other does not. Is this deliberate? [Deliberate, record it] [Add it to the other view]
- **OQ-2** (finding A-characters-024): Characters/Create and Characters/Edit: the desktop view has the required marker on the Classes label and the other does not. Is this deliberate? [Deliberate, record it] [Add it to the other view]
- **OQ-3** (finding A-characters-021): Contacts/Index: the desktop view has the page heading "Contacts" and the other does not (when categories exist). Is this deliberate? [Deliberate, record it] [Add it to the other view]
- **OQ-4** (finding A-characters-022): Contacts/Index: the mobile view has the Filter Tags button (disabled when the board has no tags) with an "Active" badge and the other does not. Is this deliberate? [Deliberate, record it] [Add it to the other view]
- **OQ-5** (finding A-characters-023): ContactCategoryManagement/Manage: the desktop view has the page heading "Manage Categories" and the other does not. Is this deliberate? [Deliberate, record it] [Add it to the other view]
- **OQ-6** (finding A-characters-025): DungeonMaster/Profile: the mobile view has the text "Date TBD" for a quest without a date and the other does not. Is this deliberate? [Deliberate, record it] [Add it to the other view]

Two further decisions need the operator because they add or move controls (D-04), not because twins differ:

- **OQ-7** (finding A-characters-007): the crop dialog on five pages can only be adjusted by dragging, with no keyboard path. Adding nudge and resize buttons adds controls. [Approve the addition] [Keep drag-only; record the WCAG 2.5.7 and 2.1.1 exception]
- **OQ-8** (finding A-characters-034 and relayout R-2): the form button rows disagree on order. Aligning them moves the submit and Cancel buttons within each row. [Approve R-2] [Keep each page as it is]

## Merge proposals (D-13)

Qualifying rule: the pair is identical or layout-only, has no deliberate difference, and its mobile differences are CSS-only. Precedent views that already merged: `Events/Create`, `Events/Details`, `Events/Edit`, `Series/Details`. Every proposal is costly to reverse and awaits the operator's explicit approval.

Common to every proposal: (a) CSS loading: the desktop page rules and the mobile sheet fold into one page stylesheet loaded with `@section Styles` from the merged view, which needs the desktop `_Layout.cshtml` and `_Layout.Platform.cshtml` to render the `Styles` section (the foundation adds it; the mobile layouts already render it), and the mobile sheet is removed from the mobile pass; (b) the `.Mobile.cshtml` file is deleted and `MobileViewLocationExpander` stays; (c) the inline `style=` and `<script>` blocks move to files first so one view serves both layouts.

- **M-1 Characters/Details** (Reversibility: costly): Identical inventory (twin comparison above); the mobile markup differs only by class names and by `div` against `h4` headings. Precedent: the four merged views above. MobileViewsTests and other facts that would change: `MobileViewsTests` "Mobile UA on /Characters/Details/{id} renders character-detail-card glass card and links character-detail.mobile.css" (`MobileViewsTests.cs` near line 767): the class and stylesheet link it asserts change to the merged view's.
- **M-2 Contacts/Details** (Reversibility: costly): Identical inventory; same notes script twice; the note surface differs by CSS only once unified. Precedent: the four merged views above. MobileViewsTests and other facts that would change: `ContactDetailsCategoryTests` (the category line on Details) and `ContactsTagsDesktopMarkupTests` (`Details_*`: tag chips and Player visibility). `MobileViewsTests` has no Contacts/Details case, so a mobile-UA test for the merged view would be added.
- **M-3 Contacts/Create** (Reversibility: costly): Layout-only pair (tracer): same fields, buttons and dialog; the only non-CSS difference is the mobile hint class. Precedent: the four merged views above. MobileViewsTests and other facts that would change: `ContactsTagsFormMarkupTests` (`CreateForm_DesktopUserAgent_...` and `CreateForm_MobileUserAgent_...`), `ContactCategoryContrastGuardTests` (the mobile Create surface `.contact-form-card` and the `contact-form.mobile.css` rules it pins; "must wrap its form in .modern-card" for desktop).
- **M-4 Contacts/Edit** (Reversibility: costly): Layout-only pair; same as Create plus Re-crop and Id. Precedent: the four merged views above. MobileViewsTests and other facts that would change: Same tests as M-3; merge Create and Edit together so the shared stylesheet is moved once.
- **M-5 ContactCategoryManagement/Edit** (Reversibility: costly): Identical inventory; the markup differs only in wrapper and class names. Precedent: the four merged views above. MobileViewsTests and other facts that would change: `ContactCategoryManagementControllerIntegrationTests`; `ContactCategoryContrastGuardTests` pins the Manage.Mobile add form rather than Edit, and `MobileViewsTests` has no case for this view.
- **M-6 DungeonMaster/EditProfile** (Reversibility: costly): Identical inventory; desktop two columns against stacked mobile is CSS-only. Precedent: the four merged views above. MobileViewsTests and other facts that would change: `MobileViewsTests` "DM EditProfile renders glass card form on mobile UA" (`GetMobilePage_DmEditProfile_ReturnsSuccessAndMobileLayout`, asserts `dm-editprofile-card-mobile` and `dm-editprofile.mobile.css`).

Not proposed, with the reason: Characters/Create and Characters/Edit (content-differs until OQ-2 is answered; reconsider after); Characters/Index, Contacts/Index, Manage Categories, Players/Index and DungeonMaster/Profile (grid, table, row and offcanvas markup differs beyond CSS, or a content difference is open).

## Relayout proposals (D-04)

Every field, link, button, action and control type stays; nothing collapses behind a toggle. Each is for operator sign-off with its BEFORE screenshot.

- **R-1 (ContactCategoryManagement/Manage, desktop):** wrap the category table in a responsive container so it scrolls inside its card at narrow widths instead of widening the page; columns, buttons and their order unchanged. BEFORE: `~/qb-ui-review/phase-94/before/desktop/ContactCategoryManagement-Manage.png`.
- **R-2 (Characters Create and Edit, Contacts Create and Edit, Category Rename, DM EditProfile; desktop and mobile):** one button row: `<hr>`, secondary (Cancel or Back) left, primary right on desktop; one stacked order on mobile. Buttons move within their row only. BEFORE: `~/qb-ui-review/phase-94/before/desktop/Characters-Create.png`, `~/qb-ui-review/phase-94/before/desktop/Contacts-Create.png`, `~/qb-ui-review/phase-94/before/mobile/Contacts-Create.png`, `~/qb-ui-review/phase-94/before/mobile/ContactCategoryManagement-Edit.png`.
- **R-3 (Contacts/Create and Edit, Characters/Create and Edit, desktop):** align the image block with the field stack so the empty block under the file input goes away (the Name row and the file input share a top edge); fields keep their order. BEFORE: `~/qb-ui-review/phase-94/before/desktop/Contacts-Create.png`.

## Empty-state copy

Verbatim, per page and layout. No copy is rewritten without the operator; each area-phase page keeps its text and takes the `.empty-state` structure.

| Page | Desktop | Mobile |
|------|---------|--------|
| Characters/Index | My Characters: h4 "No Characters Yet", "Create your first character to get started!", button "Create Character". Roster: h4 "No Other Characters", "No other adventurers have created characters yet!" | Same text, h5 headings, same button. |
| Characters/Details | "Not provided" (sheet link), "No classes defined" | Same. |
| Characters/Create | none | none |
| Characters/Edit | none | none |
| Contacts/Index | No contacts: h4 "No Contacts Yet", "No one has added a contact yet. DMs can create the first one to start building out the world." No match: h4 "No contacts match your filters", "Try selecting different tags.", button "Clear filters" | Same text with h5 headings; button "Clear Filters" (capital F). |
| Contacts/Details | "No description provided."; "No Notes Yet", "Be the first to jot something down about this contact." | Same. |
| Contacts/Create | none (form hint: "No categories yet. Manage Categories to create one.") | Same hint. |
| Contacts/Edit | none (same hint) | Same hint. |
| ContactCategoryManagement/Manage | h4 "No Categories Yet", "Add your first category above to start grouping your contacts." | Same text, h5. |
| ContactCategoryManagement/Edit | none | none |
| DungeonMaster/Profile | "No bio provided yet."; h4 "No Quests Yet", "This Dungeon Master hasn't run any quests yet." | Same text, h5 for the quests heading. |
| DungeonMaster/EditProfile | none | none |
| Players/Index | h4 "No Dungeon Masters Found", "No dungeon masters are registered yet. Brave souls may register as new quest leaders."; h4 "No Players Found", "No adventurers have registered yet. Players can register to join quests." | Same text, h5 headings. |

The only twin differences in empty copy are the heading level (h4 against h5) and "Clear filters" against "Clear Filters"; both are in the label-copy finding, not a content difference.

## Area-phase scope notes

**Area phase 3 owns:** `Views/Characters/*` (8), `Views/Contacts/*` (8), `Views/ContactCategoryManagement/*` (4), `Views/DungeonMaster/Profile` and `EditProfile` (4), `Views/Players/Index` (2), the partial `Views/Shared/_CharacterSelectModal.cshtml`, `wwwroot/js/image-crop.js` and its dialog markup, and the stylesheets `characters.css`, `characters.mobile.css`, `character-detail.mobile.css`, `character-form.mobile.css`, `contacts.css`, `contacts.mobile.css`, `contact-detail.mobile.css`, `contact-form.mobile.css`, `dm-profile.css`, `dm-profile.mobile.css`, `dm-editprofile.mobile.css`, `players.mobile.css`, `image-crop.css`.

**Findings: 55 total; S1 6, S2 13.** S1 items are the unreadable crop and character dialogs, the two amber mobile buttons, the dark category label and the editor keyboard trap; they block D-08 on their pages and are the first things the area phase fixes.

**Adjustments to the draft cut:**

- `dm-create.mobile.css` and `dm-manage.mobile.css` are loaded only by `Views/Quest/Create.Mobile.cshtml` and `Manage.Mobile.cshtml`: they belong to the Quests phase (area 1), not here. They are measured below only because the plan listed them.
- `_CharacterSelectModal` is rendered only by Quest/Details (area 1) although the draft cut lists it under area 3: the two phases should agree who edits the partial; the S1 finding for it is carried by whichever converts it first.
- `_MarkdownEditor` is area 1's (cut table) but its keyboard trap blocks six pages in this area: area 3 verifies the fix on its pages.
- `.badge.bg-purple` in `dm-profile.css` is unused; `dm-profile.css` shrinks to the two photo rules.

**A-11 results.**

- **2.5.7 Dragging movements (image crop): fails.** Repositioning and resizing the crop selection are possible only by dragging; no single-pointer alternative exists; arrow keys do nothing (probe `~/qb-ui-review/phase-94/audit/characters/probe1.json`, both layouts). "Discard Photo" is the existing way to skip custom framing: the server centres a square. Finding A-characters-007, OQ-7.
- **1.4.12 Text spacing: passes.** With line-height 1.5, letter-spacing .12em, word-spacing .16em and paragraph spacing 2em forced on every element, none of the 26 area captures (13 rows, both layouts) gained horizontal overflow or clipped text in an `overflow: hidden` container (probe `~/qb-ui-review/phase-94/audit/characters/probe2.json`; the modal, offcanvas, Tagify and CodeMirror internals are excluded). A 1px `scrollWidth` difference on five mobile pages is also present before the spacing is applied and below the sweep's overflow threshold.
- **3.3.7 Redundant entry: no finding.** No form in this area asks the user to re-enter information already given in the same flow (Edit forms are prefilled; Re-crop reuses the stored original).
- **3.3.8 Accessible authentication: not applicable** (no authentication on these pages).
- **2.5.8 Target size (desktop 24px):** Players table links are 22px (finding); crop handles are 15px.
- **Known accepted deviation kept listed:** the EasyMDE toolbar buttons on the mobile forms are 30 to 34px wide against 44px (probe `probe2.json`: `button.bold` 30x44 and siblings, on Characters Create, Contacts Create and DM EditProfile).

**Reflow:** desktop at 320px overflows only on Manage Categories; the mobile layout shows no overflow at 390px on any row (report `overflow390`).

**Images:** `img` without `alt` is 0 across the 26 captures; alt text on card and row images repeats the adjacent name (S4 finding). No console, page or request error was recorded for any row (the 404 for a missing cropped picture seen elsewhere does not occur here).

### Stylesheet metrics (15 area stylesheets)

Automated counts with `~/qb-ui-review/phase-94/audit/characters/cssmetrics.mjs` (comments stripped; rule counts are heuristic, line numbers listed). "Re-themes Bootstrap" counts rules whose selector names a Bootstrap component class; "cascade-fight" counts rules that set a colour, background or border on a Bootstrap colour utility (`!` marks an `!important` rule); "bare-element colour" counts rules that set `color` on a bare element or element list.

| Stylesheet | Lines | Hex literals (distinct) | `rgb()`/`rgba()` | `!important` | Re-themes Bootstrap | Cascade-fight (lines) | Bare-element colour | `transition: all` | Infinite animations | `backdrop-filter` | `text-shadow` | Sizes under 14px |
|------------|-------|-------------------------|------------------|--------------|---------------------|-----------------------|---------------------|-------------------|---------------------|-------------------|---------------|------------------|
| `characters.css` | 304 | 13 (6) | 50 | 0 | 0 | 1 (274) | 4 | 6 | 1 | 2 | 4 | 4 |
| `characters.mobile.css` | 115 | 3 (1) | 17 | 10 | 1 | 1 (107!) | 3 | 0 | 0 | 1 | 6 | 0 |
| `character-detail.mobile.css` | 89 | 4 (1) | 14 | 8 | 3 | 1 (55!) | 2 | 0 | 0 | 1 | 6 | 0 |
| `character-form.mobile.css` | 60 | 2 (1) | 10 | 6 | 5 | 1 (37!) | 3 | 0 | 0 | 1 | 4 | 0 |
| `contacts.css` | 428 | 28 (9) | 46 | 4 | 0 | 1 (209) | 7 | 8 | 0 | 1 | 7 | 2 |
| `contacts.mobile.css` | 198 | 7 (3) | 25 | 11 | 4 | 1 (106!) | 3 | 0 | 0 | 1 | 8 | 3 |
| `contact-detail.mobile.css` | 138 | 6 (2) | 18 | 13 | 4 | 2 (55!,107!) | 4 | 0 | 0 | 1 | 9 | 1 |
| `contact-form.mobile.css` | 94 | 6 (3) | 14 | 12 | 5 | 1 (46!) | 4 | 0 | 0 | 1 | 7 | 0 |
| `dm-profile.css` | 26 | 1 (1) | 3 | 0 | 1 | 0 (none) | 0 | 0 | 0 | 0 | 0 | 0 |
| `dm-profile.mobile.css` | 140 | 4 (1) | 24 | 11 | 1 | 0 (none) | 0 | 0 | 0 | 3 | 8 | 0 |
| `dm-create.mobile.css` | 45 | 2 (1) | 11 | 7 | 4 | 0 (none) | 2 | 0 | 0 | 1 | 5 | 0 |
| `dm-editprofile.mobile.css` | 58 | 2 (1) | 9 | 5 | 3 | 0 (none) | 2 | 0 | 0 | 1 | 4 | 0 |
| `dm-manage.mobile.css` | 71 | 4 (1) | 13 | 10 | 1 | 2 (44!,56) | 2 | 0 | 0 | 1 | 6 | 0 |
| `players.mobile.css` | 76 | 3 (1) | 13 | 7 | 0 | 1 (73!) | 3 | 0 | 0 | 1 | 4 | 0 |
| `image-crop.css` | 23 | 0 (0) | 0 | 1 | 0 | 0 (none) | 0 | 0 | 0 | 0 | 0 | 0 |

Notes: `dm-create.mobile.css` and `dm-manage.mobile.css` are Quests-owned (see adjustments). Infinite animation: `characters.css:203` only. `backdrop-filter` is one per mobile card sheet (not on repeated items) plus 2 on repeated badges in `characters.css` and 1 in `contacts.css`. The 11 mobile sheets each redefine the same glass card (`rgba(255,255,255,.15)`, 15px blur, 12px radius) under a different class name.

### View markup metrics

| View | Inline `style=` | `<style>` | Inline `<script>` | Inline event handlers | `confirm(` | `alert(` |
|------|----------------|-----------|-------------------|-----------------------|-----------|---------|
| Characters/Index | 0 / 0 | 0 / 0 | 0 / 0 | 0 / 2 | 0 / 0 | 0 / 0 |
| Characters/Details | 0 / 0 | 0 / 0 | 0 / 0 | 1 / 1 | 1 / 1 | 0 / 0 |
| Characters/Create | 4 / 5 | 0 / 0 | 2 / 2 | 0 / 0 | 0 / 0 | 0 / 0 |
| Characters/Edit | 4 / 6 | 0 / 0 | 2 / 2 | 0 / 0 | 0 / 0 | 0 / 0 |
| Contacts/Index | 0 / 0 | 0 / 0 | 0 / 0 | 0 / 1 | 0 / 0 | 0 / 0 |
| Contacts/Details | 1 / 1 | 0 / 0 | 1 / 1 | 2 / 2 | 2 / 2 | 0 / 0 |
| Contacts/Create | 3 / 5 | 0 / 0 | 2 / 2 | 0 / 0 | 0 / 0 | 0 / 0 |
| Contacts/Edit | 4 / 6 | 0 / 0 | 2 / 2 | 0 / 0 | 0 / 0 | 0 / 0 |
| ContactCategoryManagement/Manage | 2 / 0 | 0 / 0 | 1 / 1 | 0 / 0 | 1 / 1 | 0 / 0 |
| ContactCategoryManagement/Edit | 0 / 0 | 0 / 0 | 0 / 0 | 0 / 0 | 0 / 0 | 0 / 0 |
| DungeonMaster/Profile | 0 / 1 | 0 / 0 | 0 / 0 | 0 / 1 | 0 / 0 | 0 / 0 |
| DungeonMaster/EditProfile | 3 / 3 | 0 / 0 | 1 / 1 | 0 / 0 | 0 / 0 | 0 / 0 |
| Players/Index | 0 / 1 | 0 / 0 | 0 / 0 | 0 / 1 | 0 / 0 | 0 / 0 |
| Shared/_CharacterSelectModal | 0 | 0 | 1 | 0 | 1 | 0 |

Values are desktop / mobile. Cropper.js is loaded from jsDelivr by the Characters, Contacts and DM profile form views and Tagify by the Contacts forms (plan 94-12).

### Cascade and layer notes for the area phase

- Rules the foundation retires that this area depends on: the global heading, label, `p, li, span, small` and `.text-muted` colour families (`site.css:586-643`), and the in-card element-enumerated cream rules (`modern-card.css:78-120`); every mobile sheet's parchment catch-all above.
- Fights with Bootstrap utilities in this area: the `.text-muted` and `.text-danger` re-colours in each mobile sheet and the `!important` pairs in `modern-card.css:102,114`; the area sheets are unlayered, so they keep winning on their own selectors until converted.
- Rules that will win only because they are unlayered: every selector in the 13 area sheets that is not under a token; the Tagify and `image-crop.css` `!important` blocks; convert them before the layered components arrive on these pages.
- Pages where moving Bootstrap into a layer could change an outcome: none identified from the BEFORE captures; the Players and DM Profile table links (default blue today) and the Manage Categories table width are the two to compare in the AFTER sweep.
