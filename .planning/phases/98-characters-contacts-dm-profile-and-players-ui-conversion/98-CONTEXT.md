# Phase 98: Characters, Contacts, DM profile and Players UI conversion - Context

**Gathered:** 2026-11-13
**Status:** Ready for planning

<domain>
## Phase Boundary

Convert the Characters, Contacts, contact categories, DM profile and Players pages, with the image
crop dialog (`wwwroot/js/image-crop.js` and its dialog markup), to the Phase 94 foundation
(Direction A "refined glass"). Every field and action stays, the six approved twin merges are made,
the dialog findings are fixed, and the Phase 95 editor keyboard-trap fix is verified on the six
pages that render the editor. The scope, counts, stylesheet list, `@section Styles` moves, guard
tests, deferred items and the ten-point "Definition of converted" with its phase-specific extras
are fixed by `.planning/ROADMAP.md` § "Phase 98" and are not repeated here.

Every content question for this area was answered at the Phase 94 G1 sign-off (2026-11-01) and is
inherited as recorded in `94-AUDIT.md` § "Operator questions for sign-off":

- Q-50, Q-51, Q-52, Q-54, Q-55 "add it to the other view": the phone Characters/Index, Contacts/Index
  and ContactCategoryManagement/Manage get their page heading (the contract's 20px phone page
  title); the phone Characters Create and Edit mark Classes as required; desktop
  DungeonMaster/Profile shows "Date TBD" for an undated quest.
- Q-53 deliberate: the phone Contacts/Index keeps its Filter Tags button with the "Active" badge;
  desktop keeps the inline filter row. Recorded, not changed. (A-characters-036 still recolours that
  button to `btn-secondary` so `+ Contact` is the one primary action.)
- Q-97 to Q-102: the six merges (Characters/Details, Contacts/Details, Contacts/Create,
  Contacts/Edit, ContactCategoryManagement/Edit, DungeonMaster/EditProfile) are approved and are made
  here. Merge Contacts Create and Edit together so the shared sheet moves once.
- Q-117 to Q-119: the Manage table's responsive container, the one form button row, and the image
  block aligned with the field stack, approved in principle and re-confirmed here with their AFTER.
- Q-132: the crop dialog gains a non-drag path (nudge and resize controls); shape decided in D-01.
- Q-135: `Shared/_CharacterSelectModal` went with Phase 95 and is out of scope here.

No capability is added: listing, viewing, creating, editing and deleting characters and contacts,
notes, tags, categories, the DM profile and the players list keep working exactly as today.

</domain>

<decisions>
## Implementation Decisions

### Image crop dialog (Q-132)
- **D-01: Buttons and arrow keys.** A compact control row under the image: move up, down, left and
  right, grow, shrink, and Reset (re-centre at the default size). Each control is at least 24px on
  desktop and 44px on the phone, with a visible label or an accessible name. Arrow keys also move the
  focused selection, and Shift plus an arrow resizes it. The selection and the control row are in the
  dialog's Tab order. The existing "Discard Photo" (server centres a square) and "Use This Crop" stay
  as they are. The aspect ratio each page uses today is kept, and grow and shrink keep it.
- **D-02: The crop dialog renders on the foundation's opaque overlay surface**, with the title and
  the instruction text readable (A-characters-001). The `bg-dark text-light` utilities go. The
  instruction text is reworded only as far as needed to mention the new controls next to dragging;
  the exact wording is shown at Gate 1.
- **D-03: The crop dialog AFTER is a scratch mockup shown at Gate 1** before any of it is built,
  desktop and phone, with the control row and its focus order.

### Character picture requests (Phase 95 Gate 2 item 9, WINDOWS #31)
- **D-04: Render the placeholder server-side when a character has no picture.** The views know
  whether the character has a picture (through the view model or an equivalent value) and render
  the placeholder avatar directly, so no request to `/Characters/GetCroppedPicture/{id}` is made and
  the inline `onerror` handlers go. This is the audit's proposed fix (A-quests-017). It covers this
  phase's views and, at the image tag only, the views that already converted: `Quest/Details`,
  `Quest/Manage` and `QuestLog/Details`. Those pages are re-verified in both layouts. The controller
  keeps answering 404 for a missing picture. A test proves that a character without a picture renders
  the placeholder and no `GetCroppedPicture` URL. WINDOWS #31 closes when this lands.

### Contacts/Details notes panel (A-characters-032)
- **D-05: The single notes panel uses the Direction A glass card surface** on both layouts, with
  the opaque scrim as corrected in v6.0.2, not the see-through drift. The phone's near-white paper
  panel goes, and note text meets 4.5:1. It is shown in the Gate 1 look check (D-07).

### Main character badge
- **D-06: The Main badge becomes static.** The infinite `box-shadow` pulse (`characters.css:203`)
  is removed, and the badge keeps its gold glow as a still style. Nothing in the area animates
  without end.

### Relayout sign-off and gates
- **D-07: Two operator gates, both reviewed from a local package. No review branch or ref is
  created or pushed.** Gate 1, early, under `~/qb-ui-review/phase-98/gate1/` (index.html, README.md,
  img/), drawn as scratch mockups and approved before any of it is built:
  - the Q-117 Manage Categories table in its responsive container, including desktop at 320px;
  - the Q-118 form button row on every form page of the area, desktop and phone;
  - the Q-119 image block aligned with the field stack on the Characters and Contacts Create and
    Edit pages;
  - the crop dialog with its control row (D-01 to D-03);
  - a **look check**: one mockup per page type, desktop and phone. That means the Characters card
    grid, the Contacts list, the Players table, the DM profile, and a detail page with the D-05 notes
    panel. Each sits side by side with the Phase 94 Direction A renders
    (`~/qb-ui-review/phase-94/mockups/direction-a/png/quest-details-*`, `quest-form-*`) so glass
    opacity, the card scrim and title colours match them and cannot drift (the v6.0.2 lesson).
    Parchment tables are allowed where they match those renders;
  - any Characters Create or Edit merge proposal that qualifies (D-09).

  Gate 2, at the end, under `~/qb-ui-review/phase-98/gate2/`: one review sheet with every
  before/after pair, desktop and phone. It includes the Quest and Quest Log pages re-verified for
  D-04, the six editor pages with the keyboard-trap check, and any contrast or target-size exception
  as an explicit accept/reject item.
- **D-08: A rejected AFTER is iterated until the operator approves it.** The phase waits on it; plan
  for a possible redraw loop at either gate.

### Twin merges
- **D-09: Re-check Characters/Create and Characters/Edit for a merge once Q-51 is applied.** They
  were held back only by the Classes required-marker difference. Research checks each pair against
  the Phase 94 D-13 criteria (identical or layout-only, no deliberate difference, phone differences
  CSS-only) and lists the tests a merge would change. Any pair that qualifies goes to Gate 1 as a
  proposal and merges only on the operator's explicit yes. Characters/Index, Contacts/Index, Manage
  Categories, Players/Index and DungeonMaster/Profile stay twins (their markup differs beyond CSS,
  or there is a deliberate difference, Q-53).
  — **Reversibility:** costly — a merge touches the view, both layouts' CSS loading and the
  mobile-view tests that prove the `.Mobile` view is selected under a real phone User-Agent.

### Working baseline and delivery
- **D-10: Run on the Linux machine that ran Phases 94 to 97**, reusing `~/qb-ui-review/phase-94`:
  the Playwright install and tools, the Direction A renders and the BEFORE set's paths. BEFORE is
  re-captured from the current branch into `~/qb-ui-review/phase-98/`. Front-end work (mockups,
  gate sheets, view and CSS conversion) invokes the `senior-frontend` skill.
- **D-11: The first plan opens with an operator checkpoint**, as in 94-01 and 95 to 97. The operator
  confirms SQL Server at `localhost:1433`, which the agent never provisions. The executor then creates
  this phase's own fixtures through the app's own forms (Phase 94 D-25). The local database holds real
  member data, so every fixture is clearly named and nothing else is touched:
  - new accounts `qb-audit-p98player@example.invalid`, `qb-audit-p98dm@example.invalid` and
    `qb-audit-p98empty@example.invalid`, created through Admin. Their passwords go only in
    `~/qb-ui-review/phase-98/credentials.json` (mode 600) and are never printed. The Audit Board
    admin login is reused for creating them; no existing `qb-audit-*` account's memberships,
    passwords or data change;
  - new boards named for the phase, for example "P98 Audit Characters" for the populated pages and
    "P98 Audit Empty" for the empty states;
  - characters: a main one, a dead one, a retired one, some with a picture and some without, long
    names, with and without a sheet link and classes;
  - contacts: categorised and uncategorised, tagged, hidden, with and without a picture,
    description and notes, plus a board with no categories (the form hint);
  - a DM profile with a bio and a picture, and one without, with a finalized quest and an undated
    quest ("Date TBD", Q-55);
  - players and DMs for Players/Index, and the empty states of every page as listed in
    `94-AUDIT-characters.md` § "Empty-state copy";
  - one character without a picture on a quest, for D-04's Quest and Quest Log re-verification.

  Fixtures are kept after the phase, as in Phase 97.
- **D-12: Any email the phase triggers goes to the Mailpit sink only** (`qb-mailpit`; starting it
  is pre-approved), never the real relay. The local database holds real addresses.
- **D-13: Work lands in slices on `milestone/v9-rolling-improvements` in this checkout, with one
  release at the end** (v6.3.0 through a PR into `main`) once both gates have passed. Each slice
  leaves every page usable and the suite green (Phase 94 D-08). The real-phone and screen-reader
  checks, including the crop dialog's controls under touch and with a screen reader, are listed in
  the phase UAT and may run after deploy, as in Phases 96 and 97.

### Claude's Discretion
- How the "has picture" value reaches the views (view-model property, a shared partial for the
  avatar, or an equivalent), and the placeholder's look, provided no request is made for a missing
  picture and the existing `onerror` handlers go.
- How the crop controls drive Cropper.js v2 (its selection element's own keyboard and `$move` /
  `$resize` APIs, or equivalent), the step size per press, and the module structure of
  `image-crop.js`, within `.claude/ui-guidelines.md` § "No inline code".
- Ports: a free pair. 8301 may still be running Phase 97's frozen baseline, so check before using it.
- Dead and retired character styling (A-characters-018): a badge and a desaturated image, with text
  at full contrast, as the audit proposes.
- How `characters.css` and `contacts.css` (the same code under two names, A-characters-039) and the
  mobile sheets consolidate into the merged views' page sheets, within the roadmap's
  `@section Styles` rules.
- Slice order, slice boundaries and the plan count. A sensible order: the crop dialog and the
  picture placeholder (shared by many pages), then the six approved merges, then the remaining
  twins, with the gated relayouts after Gate 1.

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Phase scope and inherited answers
- `.planning/ROADMAP.md` § "Phase 98: Characters, Contacts, DM profile and Players UI conversion":
  scope, counts, stylesheet list, `@section Styles` moves, guard tests to rewrite, inherited
  decisions, approved merges and relayouts with BEFORE paths, Definition of converted and the phase
  extras (crop dialog 2.5.7, capped animation, editor trap verified on six pages).
- `.planning/phases/94-whole-app-ui-review-and-unification/94-AUDIT.md` § "Operator questions for
  sign-off" (Q-50 to Q-55, Q-97 to Q-102, Q-117 to Q-119, Q-132, Q-135 with their G1 answers),
  § "Sign-off record (G1, 2026-11-01)", § "Relayout list" and § "Merge proposals".
- `.planning/phases/94-whole-app-ui-review-and-unification/94-AUDIT-characters.md`: all 55 findings
  (A-characters-001 to 055), the twin comparison, merge proposals M-1 to M-6 (with the note that
  Characters Create and Edit are reconsidered after OQ-2/Q-51), relayout proposals R-1 to R-3,
  empty-state copy (verbatim; no copy changes without the operator), area-phase scope notes, A-11
  results and the stylesheet and markup metrics.
- `.planning/phases/94-whole-app-ui-review-and-unification/94-CONTEXT.md`: D-04 (relayout rules),
  D-07 (release per phase), D-08 (usable between merges), D-12 (inline code), D-13 to D-15 (twins,
  merges, mobile verification), D-16 and D-17 (accessibility bar, signature colours), D-25 (fixture
  writes).
- `.planning/phases/94-whole-app-ui-review-and-unification/94-UI-SPEC.md`: the design contract the
  foundation implements (tokens, components, contrast bar, Direction A values).
- `~/qb-ui-review/phase-94/mockups/direction-a/` (outside the repository): the Direction A
  mockups and renders (`png/quest-details-*`, `png/quest-form-*`, `png/quest-form--edit-*`) that the
  D-07 look check is matched against. This area has no render of its own.
- `~/qb-ui-review/phase-94/before/` and `~/qb-ui-review/phase-94/audit/characters/` (probe1.json,
  probe2.json, shots): the audit's evidence for the crop dialog and the area.

### Phase 95 to 97 handovers
- `.planning/phases/95-quests-and-quest-log-ui-conversion/95-CONTEXT.md` D-12 and D-13 (editor
  keyboard trap fix and visible Markdown hint, verified here) and D-18 to D-24 (process decisions
  this phase mirrors).
- `.planning/phases/95-quests-and-quest-log-ui-conversion/95-18-SUMMARY.md` § "Gate 2 decision"
  item 9, `95-UAT.md` item 11, and `.planning/WINDOWS.md` entry 31: the character picture 404 (D-04).
  The same summary records the `markdown-editor-hint` class and the 4.93:1 hint on the phone
  Characters, DM profile and Contacts forms.
- `.planning/phases/94-whole-app-ui-review-and-unification/94-AUDIT-quests.md` A-quests-017: the
  proposed server-side placeholder (D-04).
- `.planning/WINDOWS.md` entries 12 (phone required asterisk at 3.36:1 on Characters and Contacts
  forms) and 28 (dead `!important` overrides each area phase deletes; correct the guard comment in
  `UiFoundationLayerGuardTests` if this is the first phase to touch it).
- `.planning/phases/96-account-and-pickers-ui-conversion/96-CONTEXT.md` and
  `.planning/phases/97-events-series-calendar-and-agenda-ui-conversion/97-CONTEXT.md`: the gate,
  fixture and delivery pattern mirrored here; Phase 97 D-11/D-15/D-16 for the local-package gates.

### UI system and project rules
- `.claude/ui-guidelines.md`: tokens and layers, components, colour, accessibility checklist, mobile
  twins, no inline code (`@section Styles` / `@section Scripts`, ES modules), vendor files, dates.
- `.claude/architecture.md` § "Time and the board clock": `Html.LocalTime` and `Html.WallClock`.
  A-characters-043's raw quest-history date moves to the helper that matches what the value is,
  without changing the helpers.
- `CLAUDE.md`: branching, no planning IDs in source or CSS comments, local database contract.

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- Foundation sheets `wwwroot/css/tokens.css`, `base.css`, `components.css`, `shell.css`,
  `shell.mobile.css`: tokens, layers and components, including the opaque modal/overlay surface, the
  v6.0.2 card scrim, `.modern-card--interactive` and the button-row and empty-state components.
- `wwwroot/js/image-crop.js` (328 lines) already drives Cropper.js v2 web components
  (`#cropperImageEl`, `#cropperSelectionEl`) with an `aspect-ratio` attribute from page config; the
  controls in D-01 build on its selection element.
- The Phase 96 `AccessDenied` overflow fix and the Phase 97 responsive patterns are available for
  the Manage table container (Q-117).
- `_MarkdownEditor` with the Phase 95 Tab fix and `markdown-editor-hint` class: verified on the six
  editor pages here, not changed.

### Established Patterns
- `/Characters/GetCroppedPicture/{id}` is requested from `Characters/Index(.Mobile)`,
  `Characters/Create`, `Characters/Edit(.Mobile)`, `Quest/Details` (3 places), `Quest/Manage`
  (2 places) and `QuestLog/Details`, mostly with an inline `onerror` fallback (D-04).
  `DungeonMasterController` has its own `GetCroppedPictureAsync` path for DM photos.
- Inline `style="max-width: 200px;"` on the current-image thumbnails of the Characters forms,
  inline click handlers on phone rows and inline `confirm(` on delete forms (A-characters-044), and
  duplicated notes scripts on the Contacts/Details twins.
- `MobileViewLocationExpander` selects `.Mobile.cshtml` views by User-Agent. The six merges delete
  the `.Mobile` files and need the desktop and Platform layouts to render `@section Styles`.

### Integration Points
- Guard tests to rewrite to the same intent (none deleted): the Characters, Contacts and DM
  EditProfile facts of `MobileViewsTests`, `ContactCategoryContrastGuardTests`,
  `ContactsTagsFormMarkupTests`, `ContactsTagsDesktopMarkupTests`, `ContactDetailsCategoryTests`,
  and `ContactCategoryManagementControllerIntegrationTests`. Merged views without a mobile-UA case
  (Contacts/Details, ContactCategoryManagement/Edit) gain one.
- `characters.css`, `contacts.css`, `dm-profile.css` and `image-crop.css` leave the global list in
  `_Layout.cshtml`, and every page that opens the crop dialog loads `image-crop.css` itself.
- D-04 touches `Quest/Details`, `Quest/Manage` and `QuestLog/Details` (converted in Phase 95) at the
  avatar tag only.

</code_context>

<specifics>
## Specific Ideas

- The operator expects answers given in earlier phases to carry over. The gate, fixture and
  delivery decisions here are taken from Phases 95 to 97 for that reason.
- The look check (D-07) exists because v6.0.2 had to correct glass-panel drift: panels too
  see-through and the wrong title colours. Every AFTER in this phase is held to the Direction A
  renders, not to an approximation of them.

</specifics>

<deferred>
## Deferred Ideas

None. The discussion stayed within the phase scope.

</deferred>

---

*Phase: 98-characters-contacts-dm-profile-and-players-ui-conversion*
*Context gathered: 2026-11-13*
