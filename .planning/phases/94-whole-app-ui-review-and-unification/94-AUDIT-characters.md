# Phase 94 Audit: Characters, Contacts, DM profile and Players

Finding IDs `A-characters-nnn`. Audited with the senior-frontend skill in Critic mode against the frozen baseline (milestone commit `b6770322`, served on port 8001). Screenshots, probe output and scratch notes are outside the repository under `~/qb-ui-review/phase-94/`; this report references them by path only and quotes no member name, email address or user-entered text. All data seen is fixture data on the Audit Board.

> Tracer version: only the **Contacts/Create** row is audited in this revision (the method proof). The full area follows in the same file.

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
| Contacts/Create | 14 | 11 | no | no | 0 / 0 | 0 / 0 / 0 |

Screenshot paths (outside the repository): `~/qb-ui-review/phase-94/before/desktop/<Row>.png` and `~/qb-ui-review/phase-94/before/mobile/<Row>.png` with `<Row>` the manifest id (for example `Contacts-Create`); focus crops under `~/qb-ui-review/phase-94/before/desktop/focus/<Row>/`.

## Index

| View | Desktop file | Mobile file or none | Area phase | Counts by class | Highest severity | Existing empty-state copy | Open operator questions |
|------|--------------|---------------------|------------|-----------------|------------------|---------------------------|-------------------------|
| Contacts/Create | `Views/Contacts/Create.cshtml` | `Views/Contacts/Create.Mobile.cshtml` | 3 | visual-bug 2, inconsistency 3, cascade-fight 2, twin-drift 2, a11y 8 | S1 | none (form page). Form hint when the board has no categories, both layouts: "No categories yet. Manage Categories to create one." | 3 |

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

### A-characters-016: Tag entry box (Tagify) has no focus indicator

- **Page and layout:** Contacts/Create (desktop); Contacts/Edit (desktop)
- **Class:** a11y
- **Severity:** S2
- **WCAG:** 2.4.7; 1.4.11
- **Evidence:** BEFORE focus record `Contacts-Create` and `Contacts-Edit` desktop stop 14: the `span` "Tags input field" has `indicator: false`. `contacts.css:308-313` sets `--tags-focus-border-color: #ffc107` (gold on the near-white input, about 1.6:1). The mobile twin has the same override (`contact-form.mobile.css:69-74`); the focus probe runs on desktop only, so the mobile ring is unmeasured.
- **Proposed fix:** `.tagify:focus-within` takes the two-tone ring tokens; border colour from `--qb-control-border`.
- **Area phase:** 3 (Characters, Contacts, DM profile, Players)
- **Operator question:** N

### A-characters-026: Heading structure: no h1 on most pages, mobile section headings are `div`s, desktop levels skip

- **Page and layout:** Characters/Index (both layouts); Characters/Details (both layouts); Contacts/Index (both layouts); Contacts/Details (both layouts); ContactCategoryManagement/Manage (both layouts); ContactCategoryManagement/Edit (both layouts); DungeonMaster/Profile (both layouts); Players/Index (both layouts); Characters/Create (mobile); Characters/Edit (mobile); Contacts/Create (mobile); Contacts/Edit (mobile); DungeonMaster/EditProfile (mobile)
- **Class:** a11y
- **Severity:** S3
- **WCAG:** 1.3.1; 2.4.6
- **Evidence:** Mobile section headings are `div.character-section-heading` (`Characters/Index.Mobile.cshtml:14`, `Details.Mobile.cshtml:49,101`), `div.contact-section-heading` (`Contacts/Details.Mobile.cshtml:64,94,107`), `div.players-section-heading` (`Players/Index.Mobile.cshtml:18,53`). Desktop jumps h2 to h5 to h2 (`DungeonMaster/Profile.cshtml:28,46,67`) and h2 to h4 (`Characters/Details.cshtml:27,65,104`). Page titles are h1 only on Characters/Index and Contacts/Index desktop; form pages use h2, h4 or h5 in the card header.
- **Proposed fix:** Real headings with one h1 per page (title in the card header or beside it), styled by class not by level; mobile `div` headings become headings.
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

### Contacts/Create: layout-only

- **Inventories behind the classification:** Fields (both): `ContactImageFile` (`#contactImageInput`), `CroppedPictureFile` (hidden), `Name`, `TownCity`, `SubLocation`, `TagsInput`, `CategoryId` (select, or a disabled select plus the "Manage Categories" link when there are none), `Description` (markdown partial). Links and buttons (both): `Create Contact` (`type="submit"`), `Cancel` (to Index), crop dialog buttons `Discard Photo`, `Use This Crop`, close; hint link to `ContactCategoryManagement/Index`. Headed sections (both): one form card titled "Create New Contact" (desktop `h4` in `.modern-card-header`; mobile `h4` in `.contact-form-card`) and the crop dialog `h5`. `BoardPages` conditions: none in either file.
- **Result:** Differences are layout only: desktop is a two-column grid (image 4 columns beside Name and the Town/Sub-location pair) inside `.modern-card`, mobile is one column inside `.contact-form-card`; button order (desktop Cancel left, submit right with an `<hr>`; mobile submit first, `gap-2`); the crop dialog is `modal-lg` with a 400px stage on desktop and 320px on mobile; the mobile image hint drops `text-muted`. Same field set, same actions, same dialog. Merge candidate M-3.

## Operator questions

Standard wording; answers: *Deliberate, record it* or *Add it to the other view*. Removal is never offered (D-04). Until answered, nothing is added or removed.

Contacts/Create has no twin-content question: the pair is layout-only.

## Merge proposals (D-13)

Qualifying rule: the pair is identical or layout-only, has no deliberate difference, and its mobile differences are CSS-only. Precedent views that already merged: `Events/Create`, `Events/Details`, `Events/Edit`, `Series/Details`. Every proposal is costly to reverse and awaits the operator's explicit approval.

Common to every proposal: (a) CSS loading: the desktop page rules and the mobile sheet fold into one page stylesheet loaded with `@section Styles` from the merged view, which needs the desktop `_Layout.cshtml` and `_Layout.Platform.cshtml` to render the `Styles` section (the foundation adds it; the mobile layouts already render it), and the mobile sheet is removed from the mobile pass; (b) the `.Mobile.cshtml` file is deleted and `MobileViewLocationExpander` stays; (c) the inline `style=` and `<script>` blocks move to files first so one view serves both layouts.

- **M-3 Contacts/Create** (Reversibility: costly): Layout-only pair (tracer): same fields, buttons and dialog; the only non-CSS difference is the mobile hint class. Precedent: the four merged views above. MobileViewsTests and other facts that would change: `ContactsTagsFormMarkupTests` (`CreateForm_DesktopUserAgent_...` and `CreateForm_MobileUserAgent_...`), `ContactCategoryContrastGuardTests` (the mobile Create surface `.contact-form-card` and the `contact-form.mobile.css` rules it pins; "must wrap its form in .modern-card" for desktop).

## Relayout proposals (D-04)

Every field, link, button, action and control type stays; nothing collapses behind a toggle. Each is for operator sign-off with its BEFORE screenshot.

- **R-2 (Contacts/Create and Edit desktop):** put the button row in the guideline order (Cancel left, submit right, `<hr>` above). Contacts desktop already follows it; the proposal is to make the other forms match it. BEFORE: `~/qb-ui-review/phase-94/before/desktop/Contacts-Create.png`, `~/qb-ui-review/phase-94/before/mobile/Contacts-Create.png`.

## Empty-state copy

Verbatim, per page and layout. No copy is rewritten without the operator; each area-phase page keeps its text and takes the `.empty-state` structure.

| Page | Desktop | Mobile |
|------|---------|--------|
| Contacts/Create | none (form hint: "No categories yet. Manage Categories to create one.") | Same hint. |

The only twin differences in empty copy are the heading level (h4 against h5) and "Clear filters" against "Clear Filters"; both are in the label-copy finding, not a content difference.

## Area-phase scope notes

(Completed in the full revision.)
