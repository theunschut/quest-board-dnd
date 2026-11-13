# Phase 98: Characters, Contacts, DM profile and Players UI conversion - Research

**Researched:** 2026-11-13
**Domain:** Server-rendered Razor/CSS/ES-module UI conversion onto the Phase 94 layered foundation (ASP.NET Core 10 MVC), plus a Cropper.js v2 keyboard/button path, a server-side avatar placeholder, six `.Mobile.cshtml` merges and a local-package two-gate review
**Confidence:** HIGH for repo facts, tests, tooling and environment (all read or probed this session); MEDIUM for the crop-control design (the Cropper.js v2 API was read in the vendored file and the official docs, but no browser run was made); the D-09 verdict is a recommendation for Gate 1, not a decision.

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions

Copied from `98-CONTEXT.md` `## Implementation Decisions` (the `<domain>` inherited answers are listed under "Inherited G1 answers" below).

#### Image crop dialog (Q-132)
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

#### Character picture requests (Phase 95 Gate 2 item 9, WINDOWS #31)
- **D-04: Render the placeholder server-side when a character has no picture.** The views know
  whether the character has a picture (through the view model or an equivalent value) and render
  the placeholder avatar directly, so no request to `/Characters/GetCroppedPicture/{id}` is made and
  the inline `onerror` handlers go. This is the audit's proposed fix (A-quests-017). It covers this
  phase's views and, at the image tag only, the views that already converted: `Quest/Details`,
  `Quest/Manage` and `QuestLog/Details`. Those pages are re-verified in both layouts. The controller
  keeps answering 404 for a missing picture. A test proves that a character without a picture renders
  the placeholder and no `GetCroppedPicture` URL. WINDOWS #31 closes when this lands.

#### Contacts/Details notes panel (A-characters-032)
- **D-05: The single notes panel uses the Direction A glass card surface** on both layouts, with
  the opaque scrim as corrected in v6.0.2, not the see-through drift. The phone's near-white paper
  panel goes, and note text meets 4.5:1. It is shown in the Gate 1 look check (D-07).

#### Main character badge
- **D-06: The Main badge becomes static.** The infinite `box-shadow` pulse (`characters.css:203`)
  is removed, and the badge keeps its gold glow as a still style. Nothing in the area animates
  without end.

#### Relayout sign-off and gates
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

#### Twin merges
- **D-09: Re-check Characters/Create and Characters/Edit for a merge once Q-51 is applied.** They
  were held back only by the Classes required-marker difference. Research checks each pair against
  the Phase 94 D-13 criteria (identical or layout-only, no deliberate difference, phone differences
  CSS-only) and lists the tests a merge would change. Any pair that qualifies goes to Gate 1 as a
  proposal and merges only on the operator's explicit yes. Characters/Index, Contacts/Index, Manage
  Categories, Players/Index and DungeonMaster/Profile stay twins (their markup differs beyond CSS,
  or there is a deliberate difference, Q-53).
  - **Reversibility:** costly - a merge touches the view, both layouts' CSS loading and the
    mobile-view tests that prove the `.Mobile` view is selected under a real phone User-Agent.

#### Working baseline and delivery
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

#### Inherited G1 answers (CONTEXT `<domain>`)
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

### Deferred Ideas (OUT OF SCOPE)
None. The discussion stayed within the phase scope.
</user_constraints>

<phase_requirements>
## Phase Requirements

REQUIREMENTS.md carries no IDs for this phase (`Requirements: TBD`; `grep` of REQUIREMENTS.md for "98" finds nothing). Phase-local IDs follow the Phase 95 to 97 convention: the CONTEXT decisions D-01..D-13, the ten-point Definition of converted in ROADMAP Phase 98 (DoC-1..DoC-10, numbered in order), and the three phase extras (X-1 crop dialog meets 2.5.7, X-2 the infinite animation capped, X-3 editor keyboard-trap fix verified on six pages).

| ID | Description | Research Support |
|----|-------------|------------------|
| D-01..D-03 | Crop control row, keyboard, overlay surface, Gate 1 mockup | Section 3 (Cropper.js v2 API, module shape, markup, focus order, tests) |
| D-04 | Server-side avatar placeholder, WINDOWS #31 | Section 4 (the six remaining tags, the repository flag, the test that breaks) |
| D-05 | One glass notes panel | Section 1d, Pitfall 9 |
| D-06 | Static Main badge | Section 1d (finding map), Code Examples |
| D-07, D-08 | Two local-package gates, redraw loop | Section 6 (Gate 1 item list, Gate 2 list, package build) |
| D-09 | Characters Create/Edit merge check | Section 2 (verdict, differences, tests) |
| D-10, D-11, D-12 | Linux tooling, opening checkpoint, fixtures, email | Sections 7, 8, Environment Availability |
| D-13 | Slices on the milestone branch, v6.3.0 | Section 9 (plan table), Project Constraints |
| DoC-1..DoC-10 | Definition of converted | Section 1 (violation inventory), Validation Architecture |
| X-1..X-3 | Crop 2.5.7, no infinite animation, editor trap on six pages | Sections 3, 1d, 7; Validation Architecture |
</phase_requirements>

## Summary

The phase is a conversion, not a feature: 26 view files (49 inline `style=`, 22 inline `<script>` blocks, 11 inline handlers, all re-counted this session and matching the roadmap) and 13 stylesheets (70 hex, 243 `rgb()`, 88 `!important` per the roadmap; my regex counts are in Section 1b) move onto the layered foundation, six approved `.Mobile.cshtml` merges are made, the crop dialog gains a non-drag path, and the Quest pages' avatar tags stop requesting missing pictures. Every claim about current files below was read this session.

**Eight findings change how the CONTEXT and ROADMAP read. Read these first:**

1. **D-04 is smaller than described, and its remaining work sits in the repository layer.** The `onerror` handlers are already gone: Phase 95 replaced them with `avatar-fallback.js` (`grep onerror` over `Views`, `Areas` and `wwwroot/js` finds nothing), and Characters/Index (both), Characters/Edit (both), Characters/Details (both), the DM pages and Contacts/Index already render their placeholder server-side behind `HasProfilePicture` / `HasContactImage` (for example `Views/Characters/Index.cshtml:36` `@if (character.HasProfilePicture)`; `CharactersControllerIntegrationTests.cs:782-792` already proves "without an image renders no `GetCroppedPicture` URL"). What still requests a missing picture are exactly **six `<img>` tags in three Phase 95 views** (`Quest/Details.cshtml:160,295,400`; `Quest/Manage.cshtml:435,552`; `QuestLog/Details.cshtml:94`). They read `participant.Character`, a Domain `Character` whose `HasProfilePicture` is **never populated** on those code paths: `QuestRepository.ProjectWithoutCharacterImages` (`QuestRepository.cs:404-421`) includes `Character` without its image on purpose, and `EntityProfile.cs:101-109` ignores the flag. So D-04 needs a repository change (one scalar query after mapping, the pattern `CharacterRepository.cs:21-32` already uses) before any view can know. Section 4.
2. **One existing test will fail when D-04 lands:** `BoardPageCharacterLinksTests.CharactersOff_ExistingSignupWithCharacter_KeepsNameAndPortraitButOffersNoChange` asserts `body.Should().Contain("/Characters/GetCroppedPicture/")` (`:360`) for a character seeded **without** an image. It must seed an image (same intent: the portrait is kept) or be rewritten to assert the placeholder.
3. **The local SQL Server is down right now.** `ss -ltn` shows nothing on 1433 and `docker ps -a` lists `mssql-dev` as `Exited (137) 16 hours ago`. Per CLAUDE.md the agent must not start it: plan 98-01's operator checkpoint is a hard stop until the operator brings it up. It blocks the app, fixtures and captures, **not** the xUnit suites (integration tests use EF InMemory; the SQL-backed ones skip without `QUESTBOARD_MIGRATOR_TEST_CONNECTION`). No `qb-mailpit` container exists either (starting it is pre-approved, and probably unnecessary, Section 8).
4. **Cropper.js v2.1.1's own `keyboard` attribute must not be used.** It binds a *document-level* keydown handler, moves 1px per arrow, and maps **Delete / Cmd+Backspace to removing the selection** (`$handleKeyDown`, vendored `cropper.min.js`; confirmed by the official CropperSelection docs). The control row and the key handling are written in `image-crop.js` on the public `$moveTo` / `$change` / `$initSelection` API. The selection element also is not focusable today (probe: no `tabindex`, `probe1.json`) and nothing keeps a nudged selection inside the image. Section 3.
5. **`image-crop.css` never loads on the phone layout.** It is linked only by the desktop `_Layout.cshtml:29`; `_Layout.Mobile.cshtml` and the phone views link no crop sheet, so the phone's `touch-action: none` and its 320px stage rule never applied (the phone stage height comes from the inline `style="width:100%;height:320px;"` on the canvas). The shared dialog partial plus `image-crop.css` linked from every host view's `@section Styles` fixes this as a side effect (a behaviour improvement for touch drags that the UAT should confirm on a real phone).
6. **The guard-test list in CONTEXT/ROADMAP is incomplete and partly mis-pointed.** `MobileViewsTests.cs` has **no Contacts facts** (`grep` for `Contact` finds none; its area facts are Characters at the doc comments `:1116`, `:1142`, `:1167`, Players `:1193`, DM profile `:539`, DM EditProfile `:1288`, not "near line 767"). The Contacts phone facts live in `Mobile/ContactsTagsMobileTests.cs` and `Tests/ContactCategoryMobileRenderTests.cs`, neither of which the roadmap lists. Section 1c lists all of them.
7. **Creating the p98 accounts through Admin enqueues a Welcome email** (`AdminController.cs:142` `jobClient.Enqueue<WelcomeEmailJob>`, and `:157` `GroupMembershipAddedEmailJob`). Recipients are `@example.invalid`; `appsettings.Development.json` sets `"SuppressSending": true`, and the scratch `run-app.mjs` starts the app with `ASPNETCORE_ENVIRONMENT=Development`. So D-12 is satisfied by running in Development; no Mailpit is needed unless suppression is switched off. Section 8.
8. **The WINDOWS #28 guard-comment correction is already done** (Phase 95 recorded it; the entry's own resolution text says "the guard comment is corrected"). Nothing to do for it here. 356 important-flagged declarations remained at the Phase 95 close-out across all unconverted sheets; this phase owns 88 of them (roadmap count).

**Primary recommendation:** 17 plans in 16 waves (Section 9): opening checkpoint, fixtures/BEFORE, Gate 1 (held while the gate-independent avatar slice lands), then the crop dialog and a new shared `_ImageCropDialog` partial, the six approved merges (Contacts Create+Edit together), Characters Create/Edit (merged only on the operator's yes), the four remaining twins, cleanup/guards, and a local-package Gate 2. Keep every semantic hook class the guard tests pin (`character-card`, `contact-card`, `contact-member-row`, `character-member-row`, `category-mgmt-row`, `contact-tag-chip`, `contact-filter-row`, `contact-info-value`, `modern-card`, `row-nav-link`) and rewrite only the assertions that key on a deleted file or sheet name.

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|-------------|----------------|-----------|
| "Has a picture" for a signup's character | Database / Storage + API (QuestRepository scalar query) | Frontend Server (Razor renders placeholder or `<img>`) | Only the repository can tell without loading the image bytes (the reason `ProjectWithoutCharacterImages` exists); the view must not guess |
| Picture bytes and 404 for a missing one | API / Backend (`CharactersController.GetCroppedPicture`, unchanged, `:427-437`) | — | D-04 keeps the controller answering 404 |
| Crop selection move/resize/reset, keyboard and button path | Browser (ES module `image-crop.js` on Cropper.js web components) | Frontend Server (renders the dialog partial and `data-*` config) | Pure client interaction; the server never sees the crop geometry, only the resulting file |
| Crop dialog look, control row sizes | Browser / CSS (`image-crop.css` in `@layer pages`, foundation `.modal`) | — | Overlay surface is already a foundation component (`components.css:589-598`) |
| Class add/remove rows, note editing, delete confirmations, tag whitelist | Browser (small ES modules from `data-*`) | Frontend Server (server-rendered `<template>`, messages, whitelist JSON in an attribute) | Replaces inline scripts and handlers; wording and endpoints unchanged |
| Twin selection (desktop vs phone view) | Frontend Server (`MobileViewLocationExpander`, by User-Agent) | — | A merge removes the `.Mobile` file; selection logic is untouched |
| Phone vs desktop presentation of a merged view | Browser / CSS (`@media (max-width: 767.98px)` inside `@layer pages` in the page sheet; `.mobile-layout` rules from the foundation) | — | `.claude/ui-guidelines.md` allows a media query for a view with no twin |
| Stylesheet loading | Frontend Server (`@section Styles` per view) | — | DoC-3; `_Layout.cshtml` already renders the Styles section after its global links (`:30`) |
| Authorization, board-page gating, ownership | API / Backend (existing filters and controller checks) | — | Untouched; `[BoardPage(BoardPage.Characters)]` etc. stay |

## Standard Stack

No new packages. Everything is already vendored under `wwwroot/lib/` with its licence, per `.claude/ui-guidelines.md` "Vendor files".

### Core
| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| ASP.NET Core MVC / Razor | net10.0 [VERIFIED: `QuestBoard.IntegrationTests` references `Microsoft.AspNetCore.Mvc.Testing` 10.0.9, `QuestBoard.IntegrationTests.csproj`] | Views, partials, `.Mobile.cshtml` twins | Project constraint |
| Cropper.js | 2.1.1 [VERIFIED: `wwwroot/lib/cropperjs-2.1.1/cropper.min.js` banner `/*! Cropper.js v2.1.1`] | Crop web components (`cropper-canvas`, `cropper-image`, `cropper-selection`) | Already the app's crop engine (UMD global, custom elements) |
| Tagify | 4.38.0 [VERIFIED: path `wwwroot/lib/tagify-4.38.0`; pinned by `ContactsTagsFormMarkupTests`] | Contact tag entry | Already used by Contacts forms |
| EasyMDE | 2.21.0 [VERIFIED: path `wwwroot/lib/easymde-2.21.0`] | Markdown editor (Phase 95 owns it) | Verified, not changed, on six pages |
| Bootstrap | 5.3.8 self-hosted [VERIFIED: `_Layout.cshtml:15`] | Modal, collapse, grid | Foundation re-themes through `--bs-*` |
| Foundation sheets | in repo | `tokens.css`, `base.css`, `components.css`, `shell.css`, `shell.mobile.css`; page sheets in `@layer pages` | Phase 94 output |
| xUnit v3 + FluentAssertions | xunit.v3 3.2.2, FluentAssertions 8.10.0 [VERIFIED: `QuestBoard.IntegrationTests.csproj` `PackageReference`] | Guard and behaviour tests | Every slice |
| Playwright (scratch only) | 1.64.1 through `~/qb-ui-review/phase-94/tools/node_modules` [VERIFIED: `node -e` printed `1.64.1`] | Captures, probes, behaviour checks | Gate packages and sweeps; `chromium.launch()` |

### Alternatives Considered
| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| Own key handling on a focusable selection | Cropper's `keyboard` attribute | Rejected: document-level, 1px step, Delete removes the selection (finding 4) |
| `$resize('se-resize', d, d)` for grow/shrink | `$change(x, y, w, h)` with centre-preserving arithmetic | `$resize` anchors one corner, so "grow" drifts; `$change` keeps the selection centred and applies the element's `aspect-ratio` itself. Recommended: `$change` |
| `$zoom(delta)` for grow/shrink | `$change` | `$zoom` returns early unless the selection has the `zoomable` attribute (vendored `$zoom`: `if(!this.zoomable||0===t)return this`), and `zoomable` would also enable wheel zoom on the selection (a behaviour change). Not recommended |
| Controller placeholder or 204 (WINDOWS #31 text) | Server-side placeholder in views (D-04, chosen) | Decided in CONTEXT; recorded so the WINDOWS entry is closed with the right fix |
| One shared card sheet for Characters and Contacts | Keep two thin sheets | See Section 1b: a shared `member-cards.css` removes the A-characters-039 duplication; the hook class names stay on the elements |

**Installation:** none. **Version verification:** not applicable (no registry package added; `npm` is not used by the repo).

## Package Legitimacy Audit

| Package | Registry | Age | Downloads | Source Repo | Verdict | Disposition |
|---------|----------|-----|-----------|-------------|---------|-------------|
| (none) | — | — | — | — | — | No external package is installed by this phase |

**Packages removed due to [SLOP] verdict:** none. **Packages flagged as suspicious [SUS]:** none. The Playwright copy under `~/qb-ui-review` already exists and is reached by symlink, never installed (`~/qb-ui-review/phase-97/tools/node_modules -> .../phase-94/tools/node_modules`).

## Architecture Patterns

### System Architecture Diagram

```
GET /Characters/Create | Edit/{id}   GET /Contacts/Create | Edit/{id}   GET /DungeonMaster/EditProfile/{id}   (desktop UA or phone UA)
        |  (after the merges: ONE view serves both UAs; MobileViewLocationExpander still picks .Mobile views for the twins that stay)
        v
 Views/<Area>/<Page>.cshtml
   @section Styles  -> <page>.css (@layer pages)  +  image-crop.css  [+ tagify wrapper for Contacts forms]
   form markup (no style=, no inline script, no on*=)
   Html.RenderPartialAsync("_MarkdownEditor") x N         (Phase 95 editor; Tab is released in markdown-editor.js)
   Html.PartialAsync("_ImageCropDialog")                  (NEW shared partial: modal + stage + control row + instruction text)
   @section Scripts -> cropper.min.js (UMD, defines the web components)
                       image-crop.js      (module; self-initialises from data-crop-* on the file input)
                       <page>.js          (module: class rows from a <template>, tag whitelist, notes, confirm-on-submit)
        |
        v  user picks a file                      user presses a control / arrow key / Shift+arrow
 image-crop.js: prepareImageForCropper -> blob -> cropper-image.src       selection host (tabindex=0) keydown / control-row click
        |                                                                  -> $moveTo / $change, clamped to the image, then a polite status line
        v  "Use This Crop"  ->  cropperSelectionEl.$toCanvas() -> JPEG -> hidden file input CroppedPictureFile -> normal form POST
                                (unchanged server side: Characters/Contacts/DungeonMaster controllers)

Quest/Details | Quest/Manage | QuestLog/Details   (converted in Phase 95; touched at the avatar tag only)
   QuestRepository.GetQuestWithDetailsAsync / GetQuestWithManageViewDetailsAsync
        -> ProjectWithoutCharacterImages (no image bytes)  -> map to Domain Quest
        -> NEW: one scalar query { Id, ProfileImage != null } for the signup characters  -> Character.HasProfilePicture
   Views: Html.PartialAsync("_CharacterAvatar", ...) -> <img src=GetCroppedPicture> + hidden placeholder   (has picture)
                                                      -> visible placeholder span, no request             (no picture)
   avatar-fallback.js stays loaded as the safety net for a picture that fails after the page was rendered
```

### Recommended Project Structure
```
QuestBoard.Service/
├── Views/Shared/_ImageCropDialog.cshtml    # NEW: the one crop dialog (replaces 10 copies in 10 views)
├── Views/Shared/_CharacterAvatar.cshtml    # NEW: avatar-32 image-or-placeholder for the six Quest/QuestLog tags
├── Views/Characters/{Index,Index.Mobile,Details,Create,Edit}.cshtml   # Details merged; Create/Edit merged only on a Gate 1 yes
├── Views/Contacts/{Index,Index.Mobile,Details,Create,Edit}.cshtml     # Details, Create, Edit merged
├── Views/ContactCategoryManagement/{Manage,Manage.Mobile,Edit}.cshtml # Edit merged
├── Views/DungeonMaster/{Profile,Profile.Mobile,EditProfile}.cshtml    # EditProfile merged
├── Views/Players/{Index,Index.Mobile}.cshtml                           # stay twins
├── wwwroot/css/image-crop.css              # @layer pages; stage, control row, phone height class; linked by each host view
├── wwwroot/css/{character-detail,character-form,contact-detail,contact-form,dm-profile-form}.css  # names are a recommendation: merged-view page sheets
├── wwwroot/css/tagify-vendor.css           # NEW wrapper: @import url("../lib/tagify-4.38.0/tagify.css") layer(vendor)  (precedent easymde-vendor.css)
├── wwwroot/js/image-crop.js                # rewritten as a self-contained module (no import; none of the 12 existing modules uses import)
├── wwwroot/js/{character-form,contact-form,contact-notes,category-manage,confirm-submit}.js   # new modules (names are a recommendation)
└── wwwroot/js/contact-tags.js              # becomes a module that reads data-* (today a global initContactTags)
```
Component-to-file mapping for the per-view work is in Section 1.

### Pattern 1: Merged view, one page sheet (established)
**What:** `Events/Edit.cshtml:9-11` links `event-forms.css` from `@section Styles`; one view serves both layouts; the sheet is `@layer pages` and adapts with a media query inside the layer, which `.claude/ui-guidelines.md` allows for a view with no twin ("A view with no twin that has to adapt may use `@media (max-width: 767.98px)` inside `@layer pages`"). Phone sizing of controls (44px) comes from the foundation's `.mobile-layout` rules. The phone's glass-card wrapper classes (`character-form-card`, `contact-form-card`, `dm-editprofile-card-mobile`) disappear in favour of `card modern-card` + `modern-card-header` (Phase 97 precedent: `Agenda/Index.Mobile.cshtml:21` already uses `card modern-card` with `agenda-card` "slips" inside).
**When to use:** the six approved merges (and Characters Create/Edit if Gate 1 says yes).

### Pattern 2: Confirm on submit from `data-confirm-message` (established)
`event-details.js:72-74` wires `form[data-confirm-message]` and calls `confirm(form.dataset.confirmMessage)`; `quest-manage.js:12` does the same for buttons. Phase 98 has five confirm sites (Characters/Details delete, Contacts/Details delete contact and delete note, Manage delete category, plus the twin of each). Recommended: one tiny `confirm-submit.js` module, loaded by the four hosting views, that handles every `form[data-confirm-message]`. Wording stays byte-identical: the strings today are `Are you sure you want to delete this character? This action cannot be undone.` (`Characters/Details.cshtml:93`) and `Delete this note? This action cannot be undone.` (`Contacts/Details.cshtml:~161`); the category message is **built in script from `data-category-name` and `data-contact-count`** (`Manage.cshtml:104-116`), so it moves to the server (the view builds the identical sentence into `data-confirm-message`) with a rendered-text test, because a Razor attribute encodes `"` and `&` safely.

### Pattern 3: Server-rendered `<template>` for the class rows
Characters Create and Edit (both layouts) write the class row with `innerHTML` from a template string inside an inline script (`Create.cshtml:201-241`, four copies). A module must not build markup from strings (the repo pins this for every module: `*Module_WritesNoMarkupFromStrings`). Render one `<template id="class-row-template">` per form with the row markup and an index token in the `name` attributes, and let the module `cloneNode(true)`, rewrite the `name` indexes, and append. This also removes the latent desktop-Edit bug: `Edit.cshtml:204` starts `classIndex` at `@Model.Classes.Count` while the loop renders one empty row when there are none (`Count == 0` gives a duplicate index), whereas the phone file uses `Any() ? Count : 1`.

### Pattern 4: Stretched row link for the phone rows (established)
The phone rows (`Characters/Index.Mobile.cshtml:21,70`, `Contacts/Index.Mobile.cshtml:11`, `Players/Index.Mobile.cshtml:29`, `DungeonMaster/Profile.Mobile.cshtml:72`) carry `onclick="window.location.href=..."` on a `div` and a 22px `a.row-nav-link` (A-characters-028). Use Phase 95/97's pattern: one `a.row-nav-link` per row with `::after { content:""; position:absolute; inset:0 }`, the row `position: relative`, the ring drawn on the row with `:has(.row-nav-link:focus-visible)` (precedents `home.mobile.css:58-62`, `quest-log.css:100-104`, per 97-RESEARCH Pattern 1). Static rows (`players-row no-link`, no link) stay inert.

### Pattern 5: Vendor wrapper for Tagify (established for EasyMDE)
`wwwroot/css/easymde-vendor.css` is one line: `@import url("../lib/easymde-2.21.0/easymde.min.css") layer(vendor);` so the third-party sheet loses to layered page rules instead of beating them. Do the same for `lib/tagify-4.38.0/tagify.css` and retire the `!important` Tagify overrides (`contacts.css:321-329`, A-characters-038) by setting Tagify's own custom properties (`--tag-bg`, `--tag-text-color`, `--tags-border-color`, `--tags-focus-border-color`, already used at `contacts.css:308-313`) from tokens. Today the Tagify `<link>` sits inside `@section Scripts` mid-body (`Contacts/Create.cshtml:163`); it moves to `@section Styles`. `ContactsTagsFormMarkupTests` pins the substring `/lib/tagify-4.38.0/tagify.css` in the page, which a wrapper `@import` would no longer put in the HTML: keep that assertion's intent by asserting the wrapper link (and a new unit-style fact that the wrapper imports that path), as `UiFoundationViewLibraryGuardTests.cs:73,83` does for EasyMDE.

### Anti-Patterns to Avoid
- **Enabling Cropper's `keyboard` attribute** (finding 4).
- **Including `ProfileImage` to learn "has a picture"** on the quest queries: it reloads every image's bytes for every signup, the exact cost `ProjectWithoutCharacterImages` was written to avoid.
- **Renaming hook classes the tests pin** while moving sheets (Section 1c lists them).
- **A per-page `!important` or element-enumerated colour rule** (A-characters-037): the nine phone sheets' catch-alls (`character-detail.mobile.css:81-88`, `contact-detail.mobile.css:76-83`, `character-form.mobile.css:21-27`, `contact-form.mobile.css:22-28`) are deleted, not translated.
- **Editing the crop dialog and the form view in one commit:** the dialog partial lands once for all ten hosts, the form restyle later per view, so each slice stays small and reversible.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Crop geometry, aspect ratio, clamping to a ratio | Own rectangle maths for ratio | `cropper-selection.$change(x, y, w, h)` (applies the element's `aspect-ratio` itself) | Vendored `$change` runs `wt({aspectRatio, width, height}, "cover")` when the ratio is set |
| Focus trap and scroll lock in the dialog | Custom overlay | Bootstrap 5.3.8 modal already used (`#cropPhotoModal`, `data-bs-backdrop="static" data-bs-keyboard="false"`) | Esc stays off by design; only the content changes |
| Dialog surface | `bg-dark text-light` utilities | `.modal` variables in `components.css:589-598` (`--bs-modal-bg: var(--qb-surface-overlay)`) | One opaque overlay surface; the vendor-layer `!important` utilities are what break it today |
| 44px phone targets in the dialog | Per-control sizing | `.mobile-layout :where(.modal, .offcanvas) :where(.btn, .btn-close) { min-width/min-height: var(--qb-hit-min) }` (`components.css:621-624`) | Already applies to every `.btn` in a modal on the phone layout |
| Row focus ring | Per-page `:focus-visible` | `base.css:135` two-tone ring, which already lists `[tabindex]` and `.row-nav-link` | The selection host with `tabindex="0"` gets the ring for free |
| Empty states | Hand-built `text-center py-4` blocks | `.empty-state` (`components.css:772-793`), copy verbatim from the audit table | A-characters empty-state copy is fixed |
| Avatar placeholder | A new component per page | `.avatar-32` / `.avatar-32--placeholder` (`components.css:752-766`) | Exists since Phase 95 |
| Date text | `.ToString("MMM dd, yyyy")` in a view | `Html.WallClock(value, "date")` | DoC-9 and the architecture note: a finalized quest date is a wall-clock value |
| Class-row, note, tag, confirm behaviour | Inline scripts | Small self-contained ES modules from `data-*` | `.claude/ui-guidelines.md` "No inline code" |

**Key insight:** the hard parts (modal behaviour, ratio maths, focus ring, 44px floor, glass card, opaque overlay) already exist in the foundation or in Cropper; the phase writes thin glue and deletes about 1,700 lines of stylesheet and eight view files.

## Runtime State Inventory

This is a UI conversion with file deletions and sheet moves, not a rename or migration. Categories answered explicitly:

| Category | Items Found | Action Required |
|----------|-------------|------------------|
| Stored data | None. `Characters`, `CharacterImages`, `Contacts`, notes and DM profiles are read only by the new repository flag query; nothing writes to `Events` or `Quests` (DoC-9), and the avatar slice adds a read-only query. Fixture rows written through the app's own forms are declared review fixtures (Phase 94 D-25) | none |
| Live service config | None | none |
| OS-registered state | None | none |
| Secrets/env vars | `~/qb-ui-review/phase-98/credentials.json` (mode 600) holds the p98 passwords; never in the repository, never printed | create at 98-02 |
| Build artifacts | Browsers cache `characters.css`, `contacts.css`, `dm-profile.css` and `image-crop.css` by `asp-append-version`; removing the four global links needs no action. The frozen baseline app is built from a copy of the starting commit under `~/qb-ui-review/phase-98/baseline-src` | rebuild baseline at 98-03 |

## 1. Inventory: views, stylesheets, guard tests, findings by slice

### 1a. Views (26 files)

[VERIFIED: python regex scan this session. `style=` counts `\sstyle\s*=`; inline script counts `<script>` without `src`; handlers count `\son[a-z]+\s*=`.] Totals 49 / 22 / 11 equal the roadmap.

| File | Lines | `style=` | inline `<script>` | handlers | Notes |
|------|-------|----------|-------------------|----------|-------|
| `Characters/Index.cshtml` | 164 | 0 | 0 | 0 | already gates the portrait on `HasProfilePicture` (`:36`, `:111`); h1 "Characters"; 6 `transition: all` and a pulse in the sheet |
| `Characters/Index.Mobile.cshtml` | 105 | 0 | 0 | 2 | `onclick="window.location.href=..."` on rows (`:21`, `:70`); no heading (Q-50) |
| `Characters/Details.cshtml` | 177 | 0 | 0 | 1 | `onsubmit confirm(` (`:93`); `<label>` for read values (A-027); `target="_blank"` with no `rel` (A-051) |
| `Characters/Details.Mobile.cshtml` | 140 | 0 | 0 | 1 | merge M-1 (Q-97); deleted |
| `Characters/Create.cshtml` | 247 | 4 | 2 | 0 | dead `isEdit` branches (A-052); class-row script with `innerHTML`; dialog `:151-199`, init `:245` |
| `Characters/Create.Mobile.cshtml` | 226 | 5 | 2 | 0 | D-09 candidate |
| `Characters/Edit.cshtml` | 249 | 4 | 2 | 0 | Re-crop; `classIndex` bug (`:204`) |
| `Characters/Edit.Mobile.cshtml` | 238 | 6 | 2 | 0 | D-09 candidate |
| `Contacts/Index.cshtml` | 172 | 0 | 0 | 0 | stays; inline filter row |
| `Contacts/Index.Mobile.cshtml` | 165 | 0 | 0 | 1 | `onclick` on rows (`:11`); no heading when categories exist (Q-52); Filter Tags (Q-53 deliberate) |
| `Contacts/Details.cshtml` | 271 | 1 | 1 | 2 | notes script `:211-265`; `style="display: none;"` on `.note-edit-form`; two `onsubmit confirm(`; M-2 |
| `Contacts/Details.Mobile.cshtml` | 252 | 1 | 1 | 2 | merge M-2 (Q-98); duplicate notes script; deleted |
| `Contacts/Create.cshtml` | 167 | 3 | 2 | 0 | `initContactTags(` with `@Html.Raw(Json.Serialize(...))` inline (`:165`); M-3 |
| `Contacts/Create.Mobile.cshtml` | 159 | 5 | 2 | 0 | merge M-3 (Q-99); deleted |
| `Contacts/Edit.cshtml` | 180 | 4 | 2 | 0 | M-4 |
| `Contacts/Edit.Mobile.cshtml` | 171 | 6 | 2 | 0 | merge M-4 (Q-100); deleted |
| `ContactCategoryManagement/Manage.cshtml` | 125 | 2 | 1 | 0 | `style="width: 100px;"` / `110px` on `<th>` (`:37,:39`); no `table-responsive` (Q-117); delete-confirm script `:104-116` |
| `ContactCategoryManagement/Manage.Mobile.cshtml` | 97 | 0 | 1 | 0 | same confirm script (`:76-`); no heading (Q-54) |
| `ContactCategoryManagement/Edit.cshtml` | 43 | 0 | 0 | 0 | M-5 (Q-101); 6-col centred card |
| `ContactCategoryManagement/Edit.Mobile.cshtml` | 31 | 0 | 0 | 0 | merge M-5; deleted; links `contacts.mobile.css` |
| `DungeonMaster/Profile.cshtml` | 125 | 0 | 0 | 0 | `@quest.Date?.ToString("MMM dd, yyyy")` (`:103`); empty Date cell (Q-55); nested `container` (A-048) |
| `DungeonMaster/Profile.Mobile.cshtml` | 112 | 1 | 0 | 1 | `style="color: rgba(...)"` (`:27`); `onclick` (`:72`); two `ToString` with `?? "Date TBD"` (`:81`, `:96`) |
| `DungeonMaster/EditProfile.cshtml` | 138 | 3 | 1 | 0 | M-6; `container mt-4` / `col-lg-8 mx-auto`; `asp-validation-summary="ModelOnly"` (A-049) |
| `DungeonMaster/EditProfile.Mobile.cshtml` | 132 | 3 | 1 | 0 | merge M-6 (Q-102); deleted |
| `Players/Index.cshtml` | 102 | 0 | 0 | 0 | no page title (A-033); table links 22px (A-029) |
| `Players/Index.Mobile.cshtml` | 76 | 1 | 0 | 1 | `style="color: rgba(244,228,188,0.5)"` on the chevron (`:31`); `onclick` (`:29`); `players-row no-link` when the DM page is off |

After the six approved merges the area has **20 view files** (18 if both D-09 pairs merge). The new partials `_ImageCropDialog` and `_CharacterAvatar` join the guarded set.

### 1b. Stylesheets (13)

[VERIFIED: regex counts this session; `hex` counts distinct occurrences of `#rgb(a)`, comments included.]

| Sheet | Lines | hex | `rgb(a)` | `!important` | Loaded by today | Fate |
|-------|-------|-----|----------|--------------|-----------------|------|
| `characters.css` | 303 | 13 | 50 | 0 | global `_Layout.cshtml:26` | page sheet for Characters/Index (or thin, if the shared card moves out); loaded from `@section Styles`; 6 `transition: all`, 2 `backdrop-filter` on per-card badges, the infinite pulse at `:203` |
| `contacts.css` | 426 | 28 | 46 | 4 | global `:27` | same; line-for-line twin of `characters.css` (A-039); Tagify overrides `:300-329`; dead note-heading rule `:218-232` (A-054) |
| `dm-profile.css` | 25 | 1 | 3 | 0 | global `:28` | two photo rules; delete the unused `.badge.bg-purple` (A-053) |
| `image-crop.css` | 22 | 0 | 0 | 1 | global `:29` (desktop only) | keep, layered, linked by every host view; the stage height becomes a class, not an inline style, so the `!important` at `:20` goes |
| `characters.mobile.css` | 114 | 2 | 17 | 10 | `Characters/Index.Mobile` | twin stays; rewrite |
| `character-detail.mobile.css` | 88 | 3 | 14 | 8 | `Characters/Details.Mobile` | **deleted by M-1** |
| `character-form.mobile.css` | 59 | 1 | 10 | 6 | `Characters/Create.Mobile`, `Edit.Mobile` | deleted if D-09 merges, else rewritten |
| `contacts.mobile.css` | 197 | 6 | 25 | 11 | `Contacts/Index.Mobile`, `ContactCategoryManagement/Manage.Mobile`, `.../Edit.Mobile` | stays (Index and Manage stay twins); loses the Edit rules with M-5 |
| `contact-detail.mobile.css` | 137 | 5 | 18 | 13 | `Contacts/Details.Mobile` | **deleted by M-2** |
| `contact-form.mobile.css` | 93 | 5 | 14 | 13 | `Contacts/Create.Mobile`, `Edit.Mobile` | **deleted by M-3/M-4** |
| `dm-profile.mobile.css` | 139 | 3 | 24 | 11 | `DungeonMaster/Profile.Mobile` | twin stays; rewrite |
| `dm-editprofile.mobile.css` | 57 | 1 | 9 | 5 | `DungeonMaster/EditProfile.Mobile` | **deleted by M-6** |
| `players.mobile.css` | 75 | 2 | 13 | 7 | `Players/Index.Mobile` | twin stays; rewrite |
| **Total** | 1,735 | 70 | 243 | 89 (text count; roadmap 88) | | |

Result: 13 sheets become about 9 to 10 (merged-view page sheets are new names; four phone sheets die). `dm-create.mobile.css` and `dm-manage.mobile.css` belong to Phase 95 and are not touched. No class defined in `characters.css`, `contacts.css`, `dm-profile.css` or `image-crop.css` is used by a view outside the area (python scan of every view under `Views` and `Areas` against each sheet's class set; the only outside hit is the generic name `modern-card-body`, which `contacts.css` uses only as the parent in `.modern-card-body .note-item ...` rules, so it is scoped by `.note-item`). **Real consumers per sheet** [VERIFIED: same scan]: `characters.css` is used only by `Characters/Index.cshtml` (16 of its classes); the other desktop Characters views (Create, Edit, Details) take nothing from it. `contacts.css` is used by `Contacts/{Index, Details, Create, Edit}`, `ContactCategoryManagement/{Manage, Edit}` (desktop `.contacts-page`); `dm-profile.css` by `DungeonMaster/Profile` and both EditProfile files; `image-crop.css` by the ten crop hosts. Note the phone views' class names overlap the desktop sheets' names, but the phone layout never loads those sheets. So removing the four global links is safe once each consumer links its own sheet.

**Consolidation recommendation (discretion, A-characters-039):** extract the shared card grid (card, image, placeholder, badge row, hover lift, empty state) into one `@layer pages` sheet (for example `member-cards.css`) and keep `character-*` / `contact-*` hook class names on the elements, adding one shared class beside them (for example `class="character-card member-card"`). The two area sheets then shrink to their few unique rules. Tests pin `contact-card` (`ContactsTagsMobileTests.cs:84`), `contact-member-row`, `character-member-row`, so the hook names stay.

### 1c. Guard and markup tests that read the area, and how each is rewritten to the same intent

[VERIFIED: files read this session; line numbers are from grep or reads. None may be deleted (DoC-8).]

| Test file (project path) | Reads | Pins (intent to keep) | Needed change |
|--------------------------|-------|-----------------------|---------------|
| `QuestBoard.IntegrationTests/Mobile/MobileViewsTests.cs` (1,728 lines) | rendered phone pages | **DM Profile** `MobileDmProfile_MobileUserAgent_RendersGlassCardLayout` (`:539`): `dm-profile-header-card`, `dm-profile.mobile.css`. **Characters** `MobileCharacters_..._RendersListRows` (`:1096`, `characters.mobile.css`); `GetMobilePage_CharacterDetails_...` (doc `:1116`: `character-detail-card`, `character-detail.mobile.css`); `..._CharacterCreate_...` (`:1142`) and `..._CharacterEdit_...` (`:1167`): `character-form-card`, `character-form.mobile.css`. **Players** `GetMobilePage_PlayersIndex_...` (`:1193`: `players-section-card`, `players.mobile.css`). **DM EditProfile** `GetMobilePage_DmEditProfile_...` (`:1288`: `dm-editprofile-card-mobile`, `dm-editprofile.mobile.css`). **There are no Contacts facts here.** | Twins that stay (Characters Index, Players, DM Profile): update class and sheet names to the converted markup. Merged views (Characters Details, DM EditProfile, and Create/Edit if D-09): keep the "phone UA answers 200 with the phone layout (`mobile-layout` body class), the page's own content and its page sheet" shape that `GetMobilePage_QuestEdit_...` (`:1219-1245`) already uses for the merged Quest Edit, instead of asserting a deleted class |
| `Tests/ContactCategoryContrastGuardTests.cs` (262 lines, 6 facts, read in full) | rendered `/ContactCategoryManagement` and `/Contacts/Create` under both UAs; `contacts.mobile.css`, `components.css`, `contact-form.mobile.css`, `tokens.css` | (1) phone Manage label sits inside `.category-mgmt-add-form`, desktop does not render it; (2) `.category-mgmt-add-form .form-label {` rule in `contacts.mobile.css` has `#f4e4bc` and `text-shadow`; (3) `.form-text a {` in `components.css` uses `var(--qb-link)` with underline and passes 4.5:1; (4) `.contact-form-card .form-text a {` in `contact-form.mobile.css` has `#f4e4bc`; (5) the card surface rule keeps `--bs-danger-rgb: var(--qb-danger-text-rgb)` and `.header-subtitle` holds 4.5:1; (6) the zero-category helper link renders inside a `form-text <small>` on both layouts and `modern-card` / `contact-form-card` wrap it | Facts 2 and 4 pin literals and a deleted sheet: rewrite to "the phone add-category label inherits the surface colour (no per-form colour rule, token contrast >= 4.5:1)" and "the `.form-text a` rule serves both layouts" (fact 3 already does). Fact 6: after M-3/M-4 both UAs render the same view, so assert the helper link inside `form-text` for both UAs and the `modern-card` wrapper once. Facts 1, 3 (as is) and 5 stay |
| `Controllers/ContactsTagsFormMarkupTests.cs` (161 lines, 5 facts) | `/Contacts/Create`, `/Contacts/Edit/{id}` under both UAs | `id="TagsInput"`, `/lib/tagify-4.38.0/tagify.min.js`, `/lib/tagify-4.38.0/tagify.css`, no `@yaireo/tagify@` CDN, **`initContactTags(`** (`:55`, `:73`); edit prefill `value="quest giver, shopkeeper"`; player tier refused on both UAs; suggestion list holds only own-board tags (`shopkeeper` present, `another-board-tag` absent) | The inline `initContactTags(` text disappears: assert the module link `contact-tags.js` and the whitelist attribute instead (`shopkeeper` still appears inside the encoded attribute, `another-board-tag` still absent). The two UA-specific facts become one view under two UAs. Tagify CSS: see Pattern 5 |
| `Controllers/ContactsTagsDesktopMarkupTests.cs` (235 lines) | `/Contacts`, `/Contacts/Details/{id}` (default UA) | `contact-filter-row`, `name="tag"`, `contact-tag-chip`, empty-branch copy, `<input type="hidden" name="tag" value="{id}" />` | Index facts unchanged (twin stays). Details facts (`:60`, `:81`) stay valid on the merged view; add the same two under the phone UA to prove one view serves both |
| `Mobile/ContactsTagsMobileTests.cs` (253 lines) **(not in CONTEXT's list)** | `/Contacts` and `/Contacts/Details` under the phone UA | `Filter Tags`, `contactFilterOffcanvas`, `disabled>`, `badge bg-dark ms-1">Active`, desktop `contact-card` vs phone absence (`:82-84`); `Details_MobileDungeonMaster_RendersTagChips` (`:111`) | Index facts unchanged except class names if the sheet consolidation renames them (do not). Details fact stays valid after M-2 |
| `Tests/ContactCategoryMobileRenderTests.cs` (126 lines) **(not in CONTEXT's list)** | `/ContactCategoryManagement`, `/Contacts`, `/Contacts/Details` under both UAs | Manage: phone has `category-mgmt-row`, desktop does not; Index: phone `contact-member-row`; **Details (`:103-124`): phone `contact-info-value` + category name, desktop `NotContain("contact-info-value")`** | The Details fact **breaks on M-2 by construction** (the desktop UA will render the merged markup): rewrite to "the phone UA renders the merged Details with the category name and `mobile-layout`; the desktop UA renders the same view". Manage and Index facts stay |
| `Controllers/ContactDetailsCategoryTests.cs` (79 lines, 3 facts) | `/Contacts/Details/{id}` (default UA) | assigned category name shows; unassigned shows no "Ungrouped" and no `fa-tag`; angle brackets escaped | Unchanged intent; keep the `fa-tag` marker class on the merged markup |
| `Controllers/ContactCategoryManagementControllerIntegrationTests.cs` (439 lines, 16 facts) | controller POST/authorization/duplicate-name; `NotContain("<script>alert('xss')</script>")` | gates and the duplicate-name refusal | Behaviour unchanged. Add a phone-UA GET of `/ContactCategoryManagement/Edit/{id}` (merged M-5 has no mobile case today) |
| `Tests/UiFoundationLayerGuardTests.cs` (715 lines) | `LayeredFiles` and `NoLiteralFiles` arrays; layouts' link order; "firstLegacy" must exist | every listed sheet opens `@layer <name> {`, no `!important` outside reduced-motion and visually-hidden, no colour literal | **Append** each converted sheet to both arrays; in the same commit **remove** the names of sheets a merge deletes (the array fact fails on a listed missing sheet by design, `RequiredCssFiles`). `Layouts_LinkFoundationInCascadeOrder` still has `legacy-pages.css` and `shop.css` after the four global links leave, so it holds |
| `Tests/MarkdownEditorMarkupTests.cs` (109 lines) | `/Quest/Create` both UAs, `/Characters/Create` desktop | editor `aria-describedby`, `data-editor-label`, hint sentence, token meta | Extend to the six editor pages under both UAs (X-3, static half) |
| `Tests/BoardPageStaticGuardTests.cs` | planning-reference scan of listed files incl. `Players/Index(.Mobile)`, `DungeonMaster/Profile(.Mobile)`, `dm-profile.mobile.css` | no GSD ids in source | Missing files are skipped by `PresentFiles()`; keep every comment free of ids |
| `Controllers/CharactersControllerIntegrationTests.cs` (27 facts) | `/Characters`, create/edit/delete flows | `Index_CharacterWithImage_RendersPortraitEndpoint` (`:744-775`), `Index_CharacterWithoutImage_DoesNotRenderPortraitEndpoint` (`:777-792`); `Edit Character` button text (`:353`) | Unchanged; the existing Index proof is the model for D-04's new Quest-side proof |
| `Controllers/BoardPageCharacterLinksTests.cs` | `/Quest/Details` | `:360` `body.Should().Contain("/Characters/GetCroppedPicture/")` for a seeded character with **no image** | **Breaks under D-04** (finding 2): seed a `CharacterImageEntity` or assert the placeholder |
| `Tests/QuestsConversionGuardTests.cs` | Quest views, sheets, `avatar-fallback.js` (planning-reference scan only) | no inline code, no ids in the Quest views | The avatar edits in `Quest/Details`, `Quest/Manage`, `QuestLog/Details` must keep these facts green: no `style=`, no handler, no id in comments |
| `Controllers/ContactsControllerIntegrationTests.cs` (66 facts), `DungeonMasterControllerIntegrationTests.cs` (21), `PlayersControllerIntegrationTests.cs` (1) | rendered pages, default UA | `No categories yet.` (`:1362`,`:1379`), `No bio provided yet.` (`:91`), controller behaviour | Copy is verbatim in the conversion; unchanged |
| `Security/BoardPageCoverageTests.cs:68`, `Middleware/CrossBoardRouteCoverageTests.cs:252`, `Controllers/BoardPageGateMatrixTests.cs:232`, `UnitTests/Helpers/CrossBoardLinkRegistryTests.cs:96` | route tables naming `Characters/GetCroppedPicture` | the route still exists and is gate-exempt | Unchanged: D-04 does not touch the controller |

**New guard (Wave 0):** `QuestBoard.IntegrationTests/Tests/CharactersConversionGuardTests.cs`, a clone of `EventsConversionGuardTests.cs` (761 lines, about 25 facts: `AreaViews_RenderNoRawDateFormatting`, `ConvertedViews_CarryNoInlineStyleScriptOrHandler`, `ConvertedSheets_OpenThePagesLayer`, `_HaveNoImportantFlag`, `_HaveNoColourLiteral`, `ConvertedViews_LinkTheirSheets`, `PhaseScope_IsFullyConverted`, `Layouts_LinkNoAreaSheet`, `RetiredFiles_AreGone`, `*Module_WritesNoMarkupFromStrings`, `AreaSheets_DoNotRestyleCardTitles`, `PhaseSourceFiles_CarryNoPlanningReferences`, scanner self-test), with `RetiredFiles` listing the eight possibly deleted `.Mobile` views and four sheets.

### 1d. The 55 findings by owning slice

[Source: `94-AUDIT-characters.md`, all 55 read.] Findings the foundation already fixed are "verify only" (008, 009, 010, 011 button roles, 012 badge labels once spans stop being repainted).

| Slice (Section 9) | Findings |
|-------------------|----------|
| Avatar placeholder (98-04) | A-quests-017 (D-04), WINDOWS #31 |
| Crop dialog (98-06) | 001, 007 (the 2.5.7 failure), 038 (stage height as a class), 055 (crop part: hidden-input and error-box classes, CDN script tags already self-hosted) |
| Characters/Details merge (98-07) | 003 (amber phone button), 012, 027, 037, 044, 045, 051; D-06 is Index |
| Contacts Create+Edit merge (98-08) | 010, 016 (Tagify focus), 031, 034 (R-2), 038, 046, 047 (R-3), 055 |
| Contacts/Details merge (98-09) | 004, 019, 030, 032, 035, 044, 054; D-05 |
| DM EditProfile merge (98-10) | 034, 046, 047, 048, 049, 055 |
| Category Edit merge + Manage (98-11) | 005, 017 (R-1), 023 (Q-54), 035, 044, 055 |
| Characters Create/Edit (98-12) | 024 (Q-51), 031, 034, 046, 047, 052, 055 |
| Characters/Index (98-13) | 015, 018, 020 (Q-50), 026, 028, 033, 039, 040 (D-06), 041, 042, 050 |
| Contacts/Index (98-14) | 014, 015, 021 (Q-52), 022 (Q-53 deliberate; 036 recolour only), 028, 035, 039, 041, 042 |
| Players + DM Profile (98-15) | 013, 025 (Q-55), 028, 029, 033, 043 (date helper), 044, 048, 053 |
| Cross-cutting, verify in the sweep (98-16) | 008, 009, 026 (one h1 per page), 042, 006 (editor trap, X-3) |

The plan numbers in this table are the research proposal of Section 9. The final plan set renumbered and split the slices; the binding finding-to-plan trace is the "Audit finding trace" section of the closing plan (98-22), and each owning plan cites its findings by id.

## 2. D-09: Characters Create and Edit against the Phase 94 D-13 criteria

[VERIFIED: both pairs diffed line by line this session (`diff` of whitespace-stripped files) and `Create.cshtml` against `Edit.cshtml`.]

Criteria: identical or layout-only, no deliberate difference, phone differences CSS-only.

**Create vs Create.Mobile.** After Q-51 (the phone Classes label gains the required marker) the field set and actions are identical: `OwnerId` (hidden), `ProfilePictureFile`, hidden `CroppedPictureFile`, `Name`, `Level`, `Status`, `Role`, `Classes[i].Class` / `.ClassLevel`, `SheetLink`, `Description`, `Backstory`, Add Another Class, Remove, Create Character, Cancel, the crop dialog. Every remaining difference is presentational:

| Aspect | Desktop | Phone | Becomes CSS-only by |
|--------|---------|-------|---------------------|
| Title | `h2` in `.modern-card-header` (`:13`) | `h5` in `.character-form-card` then `<hr style=...>` | One card header (`h1`/`h2`, the phone gets the 20px step from the foundation, `components.css`/Phase 94 page-title rule) |
| Image + fields | two columns (`col-md-4` / `col-md-8`) | one column | Bootstrap grid classes stack below `md`; R-3/Q-119 aligns the image block anyway |
| Class row | `col-md-5/4/3`, Remove in the third column | `row g-2` three `col-12`, Remove `w-100` | `col-12 col-md-5` etc. plus a phone rule for the Remove width |
| Help text | `form-text text-muted` | `form-text` | one class, styled by the surface (A-046) |
| Button row | submit left, Cancel right, `d-flex gap-2`, no `<hr>` | same order with an inline-styled `<hr>` | Q-118 approves one row: `<hr>`, Cancel left, primary right on desktop; one stacked order on the phone (`flex-direction: column-reverse` keeps DOM order = desktop order) |
| Dialog | `modal-lg`, stage 400px | no `modal-lg`, stage 320px | stage height is a class + media query (D-02 partial); `modal-lg` only applies from 992px in Bootstrap anyway |
| Dead code | `isEdit` branches (A-052) | none | deleted |

**Edit vs Edit.Mobile.** Same, plus Re-crop Photo, the current-image thumbnail (inline `style="max-width: 200px;"` on both, becomes a class), the Main hint text and Cancel to Details. **Two differences are not CSS:**
1. `placeholder` attributes: the phone Edit has `placeholder="Enter character name"` and `placeholder="https://..."`; the desktop Edit has none (Create has both on both layouts). A merge must pick one; the proposal takes Create's placeholders for the merged Edit (a small visible addition on desktop Edit) and flags it at Gate 1.
2. `classIndex` initial value (Pattern 3): the phone is correct, desktop is off by one when a character has no classes (not reachable through the UI because Classes is required, but the merge removes the discrepancy).

**Verdict: both pairs qualify** once Q-51 is applied and the placeholder choice is accepted: no deliberate difference remains (the audit records none for this area), and every other difference is a wrapper, a grid class, a help-text class, an inline style or button order, all expressible as one markup plus CSS. **Recommendation: propose both merges at Gate 1** (as one proposal "Characters Create and Edit", like M-3/M-4, so the shared `character-form.css` moves once), with the cost list below and the default "merge only on an explicit yes". If the answer is no, the plan keeps the twins and still converts them with the same shared partials (`_ImageCropDialog`, the class-row template, the form button row).

**Cost and benefit.** Deletes `Create.Mobile.cshtml` (226 lines), `Edit.Mobile.cshtml` (238) and `character-form.mobile.css` (59); removes the third and fourth copy of the class-row script; fixes the placeholder and index drift. Reversibility is costly (D-09).

**Tests a merge would change:** `MobileViewsTests` `GetMobilePage_CharacterCreate_...` (doc `:1142`) and `GetMobilePage_CharacterEdit_...` (doc `:1167`), which assert `character-form-card` and `character-form.mobile.css`, become the `GetMobilePage_QuestEdit_...` shape (phone UA answers 200, `mobile-layout`, posts to `/Characters/Create` or `/Characters/Edit/{id}`, "Create Character"/"Save Changes", the merged sheet). `MarkdownEditorMarkupTests.CharacterCreate_DesktopUserAgent_...` gains a phone twin. `UiFoundationLayerGuardTests` arrays and the new `CharactersConversionGuardTests` lists drop `character-form.mobile.css`. `CharactersControllerIntegrationTests` and the cross-board route tests are UA-agnostic and unchanged.

## 3. D-01: driving the crop selection with buttons and keys

### 3a. Current structure (read in full)

`wwwroot/js/image-crop.js` is 328 lines, a **classic script** with four top-level functions that become globals: `prepareImageForCropper` (`:7`), `extractCroppedBlob` (`:47`, calls `cropperSelectionEl.$toCanvas()` at `:48`), `setCroppedFileInput` (`:58`) and `initImageCrop(config)` (`:80`). It expects these ids in the page: `cropPhotoModal`, `cropperImageEl`, `cropperSelectionEl`, `cropConfirmBtn`, `cropCancelBtn` and a hidden `input[type="file"][name="CroppedPictureFile"]`; `#fileSizeError` is optional and is shown with `style.display` (`:142`, `:153`). Config keys: `fileInputId`, `hiddenCroppedInputName`, `aspectRatio` (default 1, applied with `cropperSelectionEl.setAttribute('aspect-ratio', ...)` at `:118`), `maxFileSizeBytes`, `allowedTypes`, `recropButtonId`, `originalImageUrl`. The modal-open sequence waits for `shown.bs.modal` and then runs `cropperImageEl.$center('contain')` and `cropperSelectionEl.$initSelection(true, true)` (`:197-198`) because Cropper sizes against a 0x0 hidden canvas; that is exactly the "re-centre at the default size" Reset needs. "Use This Crop" re-extracts at click (`confirmBtn`), "Discard Photo" clears the file input (`cancelBtn`).

**Pages and aspect ratio.** All ten call sites pass `aspectRatio: 1` [VERIFIED: grep of `initImageCrop(` over `Views`]: Characters Create (`:245`, `.Mobile :224`), Characters Edit (`:247`, `.Mobile :236`), Contacts Create (`:159`, `.Mobile :151`), Contacts Edit (`:172`, `.Mobile :163`), DM EditProfile (`:136`, `.Mobile :130`). The selection markup hard-codes `aspect-ratio="1"` too. The dialog markup is copied ten times (starts: `Characters/Create.cshtml:151`, `Create.Mobile:130`, `Edit:153`, `Edit.Mobile:142`, `Contacts/Create:106`, `Create.Mobile:98`, `Edit:119`, `Edit.Mobile:110`, `DungeonMaster/EditProfile:83`, `EditProfile.Mobile:77`); only the stage height (`400px` desktop, `320px` phone, inline `style` on `cropper-canvas`) and `modal-lg` differ.

### 3b. Cropper.js v2.1.1 API (vendored file and official docs)

[VERIFIED: `wwwroot/lib/cropperjs-2.1.1/cropper.min.js` read this session. CITED: https://fengyuanchen.github.io/cropperjs/v2/api/cropper-selection.html]

- `$move(dx, dy = dx)` calls `$moveTo(this.x + dx, this.y + dy)`; `$moveTo(x, y)` returns early unless `movable` (the markup has `movable resizable`).
- `$resize(action, offsetX = 0, offsetY = 0, aspectRatio = this.aspectRatio)` needs `resizable`; `action` is one of `n-resize`... `sw-resize`; it moves one side or corner (a corner anchor, so it is not a centred grow).
- `$change(x, y, width = this.width, height = this.height, aspectRatio = this.aspectRatio)`: when an aspect ratio is set, `width`/`height` are recomputed with `wt({aspectRatio, width, height}, "cover")`; unless `precise`, all four are rounded; it fires a cancelable, bubbling `change` event with `detail {x, y, width, height}` and applies nothing if cancelled.
- `$zoom(scale, x?, y?)` returns `this` immediately unless the `zoomable` attribute is set.
- `$reset()` goes back to `$initialSelection`, recorded by `$initSelection`; `$initSelection(centre, fromScratch)` recomputes from `initial-coverage` (0.8 in the markup) and the aspect ratio.
- `keyboard` attribute (default false): when true it binds `$handleKeyDown` on the **document**; Arrow keys `$move(±1)`, `+`/`-` `$zoom(±0.1)`, **Delete and Cmd+Backspace remove the selection**, and it ignores keys while an input or textarea has focus. Not used (finding 4).
- No built-in boundary: the docs' "Limit boundaries" example cancels the `change` event in application code. Today a dragged selection can leave the image, so nothing constrains a nudged one either.

### 3c. Recommended design

1. **`image-crop.js` becomes a self-contained ES module** (no `import`, like every existing module). It self-initialises from the DOM: for each `input[type=file][data-crop-input]` it reads `data-crop-output-name` (the hidden field name, today `CroppedPictureFile`), `data-crop-aspect-ratio` (`1`), and for the re-crop button `data-crop-original-url`. The ten inline `initImageCrop({...})` calls and the global functions go. `bootstrap` and the Cropper custom elements are globals from classic scripts that load first, so a deferred module can use them. `#fileSizeError` toggles `hidden` instead of `style.display`.
2. **One shared partial `Views/Shared/_ImageCropDialog.cshtml`** replaces the ten dialog copies. It carries: the opaque modal (no `bg-dark text-light`, no `btn-close-white`, the foundation `.modal` variables apply), the stage with a class-sized canvas, the control row, a visually hidden `role="status"` line, the instruction text and the two footer buttons with their current ids and wording. Hosts link `image-crop.css` from `@section Styles` and the module from `@section Scripts`. There is no `.Mobile` twin of the partial.
3. **Focus and names.** Give the selection host `tabindex="0"`, `role="group"`, `aria-roledescription="crop selection"` (assumption, Section Assumptions A3) and an `aria-label` such as "Crop selection. Arrow keys move it, Shift plus arrow keys resize it". The base layer's two-tone ring already covers `[tabindex]` (`base.css:135`). Tab order inside the dialog: Close, selection, control row (up, down, left, right, larger, smaller, Reset), Discard Photo, Use This Crop. The control row is `role="group" aria-label="Adjust crop"` with real `<button type="button" class="btn btn-sm btn-secondary">` elements, a Font Awesome arrow icon (`aria-hidden`) and a visible short word or a visually hidden name. 24px on desktop is met by `.btn-sm` (about 31px); 44px on the phone is met by the foundation rule `components.css:621-624`.
4. **Keys on the selection host only** (a `keydown` listener on `#cropperSelectionEl`, never the document): `ArrowLeft/Right/Up/Down` move by a step; with `Shift` they resize (`ArrowUp`/`ArrowRight` grow, `ArrowDown`/`ArrowLeft` shrink); `preventDefault()` only for handled keys; ignore events with `Ctrl`, `Alt` or `Meta`. Because Arrow keys do nothing on a focused `<button>`, scoping the listener to the host keeps the buttons unaffected.
5. **Step and bounds.** Step 8px (the 8px grid; the stage is 320 to 400px wide) per press, `Shift` unchanged. Compute the image's rectangle in canvas coordinates (`cropperImageEl.getBoundingClientRect()` minus `canvas.getBoundingClientRect()`) and clamp x/y to `[left, right - width]`, `[top, bottom - height]`; clamp grow to the largest rectangle of the ratio that fits the image; clamp shrink to a minimum (suggest 32 stage px, an assumption to confirm with Gate 1's mock). Grow and shrink call `$change(x - d/2, y - d/2, w + d, h + d)` so the centre is kept and the element's `aspect-ratio` is applied by Cropper itself.
6. **Reset** runs the existing `cropperImageEl.$center('contain'); cropperSelectionEl.$initSelection(true, true);`.
7. **Announce.** After each change, write one short sentence to the `role="status"` element with `textContent` (for example "Selection 224 by 224 pixels, left 40, top 24"), throttled so a held key does not flood a screen reader.
8. **No new endpoint, no new field.** "Use This Crop" and the hidden-input population stay as they are; the controls only move the selection, so server code and validation are unchanged. Dismissing with the Close button after nudging does not save the nudge, the same as dragging today (the code comment says the hidden input already holds the default centred crop).
9. **Instruction text (D-02):** keep the sentence "We'll use a square crop — if you skip this, we'll center one automatically." and change only the drag sentence, for example "Drag to reposition, use the corner handles or the buttons below to move and resize; with the selection focused, arrow keys move it and Shift plus arrow keys resize it." Exact wording goes on the Gate 1 mock (D-02).
10. **Reduced motion:** no animation is added; nothing in the dialog animates beyond Bootstrap's own fade.

**Gate 1 crop frame (D-03):** desktop (modal-lg, 400px stage) and phone (390 wide, 320px stage, controls on two rows at 44px), showing the control row, the focus ring on the selection and the Tab order numbers; the instruction text variants.

### 3d. Tests for the crop work

- Static (Wave 0): the partial contains no `bg-dark`, no `text-light`, no `style=`; the selection element has no `keyboard` attribute; `image-crop.js` has no `innerHTML`/`outerHTML`/`insertAdjacentHTML`, no `document.addEventListener('keydown'`, no `style.` assignment, and calls `$moveTo` and `$change`; every host view renders `_ImageCropDialog`, links `image-crop.css` from `@section Styles`, and has no `initImageCrop(`; `image-crop.css` opens `@layer pages {` with no `!important`.
- Integration: each of the five host pages (both UAs) renders the dialog with the control row names and the footer buttons `cropCancelBtn` and `cropConfirmBtn`.
- Browser (scratch Playwright, evidence stored): a behaviour check modelled on `~/qb-ui-review/phase-94/audit/characters/probe1.mjs` (which already opens the dialog with `setInputFiles('#...', test-crop.png)`, waits for `#cropPhotoModal.show`, and measures the selection): each control moves or resizes by the step, the ratio stays 1, the selection never leaves the image, Reset restores the first box, arrow and Shift+arrow work with the selection focused, Tab order matches the mock, no horizontal scroll at 320 and 390. Real touch and screen-reader passes go to the UAT (D-13).

## 4. D-04: every request for `/Characters/GetCroppedPicture/`, what the models expose, the cleanest way

[VERIFIED: grep of `GetCroppedPicture` over `QuestBoard.Service` this session, plus reads of each site.]

| Site | Gated on a flag today? | Action |
|------|------------------------|--------|
| `Characters/Index.cshtml:38,113`, `Index.Mobile.cshtml:24,73` | yes, `character.HasProfilePicture` (`:36`, `:111`, `:20`, `:72`); else `.character-placeholder` / `.character-member-placeholder` | none (already server-side). Proof test exists (`CharactersControllerIntegrationTests.cs:744-792`) |
| `Characters/Edit.cshtml:31`, `Edit.Mobile.cshtml:28` | yes, inside `@if (Model.HasProfilePicture)` | none; inline `style="max-width: 200px;"` becomes a class (DoC-1) |
| `Characters/Create.cshtml:34` | inside `Model.HasProfilePicture && isEdit`, **dead** (Create never has an id) | deleted with A-052 |
| `Characters/Details(.Mobile)` | uses `GetProfilePicture` (the original), gated on `HasProfilePicture` (`:21`, `:20`) | none |
| DM profile pages | `GetDMProfilePicture`, gated on `Model.HasProfilePicture` (`Profile.cshtml:19`, `Profile.Mobile.cshtml:18`, `EditProfile.cshtml:27`, `EditProfile.Mobile.cshtml:30`) | none. The `DungeonMasterController.GetCroppedPictureAsync` mentioned in CONTEXT is the **service/repository method** (`DungeonMasterProfileService.cs:69`); the action is `GetDMProfilePicture` (`DungeonMasterController.cs:160-175`) and it also answers 404 when absent. Contacts use `HasContactImage` (`Contacts/Index.cshtml:16`) |
| `Quest/Details.cshtml:160,295,400` | **no**: the `<img>` is unconditional, followed by a `hidden` placeholder span that `avatar-fallback.js` reveals when the image errors | change |
| `Quest/Manage.cshtml:435,552` | **no**, same pattern | change |
| `QuestLog/Details.cshtml:94` | **no**, same pattern (`alt=""`, decorative) | change |

There are no `onerror` attributes anywhere (grep over `Views`, `Areas`, `wwwroot/js` returned nothing); the fallback is `wwwroot/js/avatar-fallback.js`, loaded by `Quest/Details.cshtml:978`, `Quest/Manage.cshtml:827` and `QuestLog/Details.cshtml:17`.

**What the models expose.** The three Quest views bind Domain models directly (`@model PlayerSignup`, `@model Quest`, `QuestLogDetailsViewModel { Quest Quest }`), so `participant.Character` is `QuestBoard.Domain.Models.Character`, which already has `public bool HasProfilePicture { get; set; }` with the comment "Lets list/detail views show a placeholder-or-photo state without pulling the image bytes" (`Character.cs:16-17`). It is populated only by `CharacterRepository` (`:21-32`, `:50-60`, `:75-78`, scalar `c.ProfileImage != null`); the entity-to-model map ignores it (`EntityProfile.cs:109`, "Computed separately by a scalar query in the repository"). `QuestRepository.GetQuestWithDetailsAsync` (`:84-90`) and `GetQuestWithManageViewDetailsAsync` (`:114-119`), the only loaders behind these three pages (`QuestController.Details` GET `:319`, Manage `:962`, `QuestLogController.Details` `:38`), go through `ProjectWithoutCharacterImages` (`:404-421`), which includes `PlayerSignups.Character.Classes` but not `ProfileImage`. So `HasProfilePicture` is `false` for every signup character today.

**Cleanest way (recommended).**
1. **Repository:** after `Mapper.Map<Quest>`, in those two methods only, collect the distinct `Character.Id`s of the signups and run one scalar query `DbContext.Characters.Where(c => ids.Contains(c.Id)).Select(c => new { c.Id, HasImage = c.ProfileImage != null })`, then set `HasProfilePicture` on each mapped character (a private helper in `QuestRepository`, mirroring `CharacterRepository.cs:21-32`). This stays in `QuestBoard.Repository` (EF only there), is read-only (the "writes to Events and Quests through the change tracker" rule is not engaged), and loads no image bytes. Add a unit test in `QuestBoard.UnitTests/Repository` (InMemory, like `CharacterRepositoryTests.cs:267-304`).
2. **Views:** a shared partial `Views/Shared/_CharacterAvatar.cshtml` (model: character id, name, has-picture, decorative flag, extra class) renders, for a picture: the existing `<img class="avatar-32 ...">` plus the hidden placeholder sibling so `avatar-fallback.js` keeps working; for no picture: the placeholder span, **not hidden**, `aria-hidden="true"` with the person icon, and **no `<img>` and no URL**. The six tags become one partial call each. `QuestLog/Details` passes `decorative: true` (today `alt=""`).
3. **Keep `avatar-fallback.js`** loaded in the three views as the safety net for a picture deleted or failing after the page rendered. It is harmless, and removing it edits three more Phase 95 view regions for no gain. CONTEXT's "the inline `onerror` handlers go" is already true.
4. **Tests (new `CharacterAvatarRenderTests`):** a signup character without an image renders `avatar-32--placeholder` (not `hidden`) and no `GetCroppedPicture/{id}`; with an image renders the URL; for `/Quest/Details/{id}` (both UAs where the desktop view is the one that renders it), `/Quest/Manage/{id}` and `/QuestLog/Details/{id}`; plus a negative guard that no view under `Views/Quest` or `Views/QuestLog` contains `Url.Action("GetCroppedPicture"` outside the partial. Reuse `BoardPageCharacterLinksTests.SeedFinalizedQuestAsync` (it already takes `signupHasCharacter` and a name) and `CharactersControllerIntegrationTests.cs:744-775` for seeding a `CharacterImageEntity`.
5. **Gate 2 re-verification:** the Quest and Quest Log pages in both layouts with one character without a picture (fixture D-11). The phone `Quest/Details.Mobile`, `Quest/Manage.Mobile` render no portraits (only the desktop views request the URL), so the check there is "unchanged".

WINDOWS #31 closes when the repository flag, the partial and the proof tests land; its resolution text should say "fixed in the views, not the controller".

## 5. Layout plumbing: `@section Styles`, the global list, and how Phases 96/97 moved sheets

[VERIFIED: `_Layout.cshtml:15-30,294`, `_Layout.Mobile.cshtml:11-22,249`, `_Layout.Platform.cshtml:16`, `_Layout.Platform.Mobile.cshtml:17`, `_Layout.GroupPicker.cshtml:16`.]

- Desktop `_Layout.cshtml` links, in order, `app.css`, `tokens.css`, `base.css`, `components.css`, `shell.css`, the Font Awesome v4 shim, then `legacy-pages.css`, `shop.css`, **`characters.css`, `contacts.css`, `dm-profile.css`, `image-crop.css`** (`:24-29`), then `@await RenderSectionAsync("Styles", required: false)` (`:30`) and scripts at `:294`. So a view's `@section Styles` loads after the global list; the four area sheets simply come out of the list and into each view's section.
- Phone `_Layout.Mobile.cshtml` links `app`, `tokens`, `base`, `components`, `shell`, `shell.mobile`, the shim, `mobile.css`, then renders `Styles` (`:22`). It never linked any of the four area sheets, which is why `image-crop.css` was absent on the phone (finding 5).
- The Platform layouts and the group picker layout already render `Styles` as well; none of this phase's pages use them.
- **Pattern from Phases 96/97:** each converted view links its own sheet from `@section Styles` (`Events/Edit.cshtml:9-11`; `EventsConversionGuardTests.ConvertedViews_LinkTheirSheets` pins the view-to-sheet table, `Layouts_LinkNoAreaSheet` forbids the area's sheets in the two main layouts). A sheet leaves the global list in the same slice in which its **last** consumer links it itself, so every intermediate state is usable (D-13, Phase 94 D-08): `image-crop.css` after the crop slice (all five hosts link it), `dm-profile.css` after DM Profile and EditProfile (the merge slice and the DM Profile slice), `characters.css` after Characters/Index (its only consumer), `contacts.css` after the last of Contacts Index, Details, Create, Edit and the two category pages. The final cleanup plan removes whatever is left and asserts the layouts link none of the four.
- `UiFoundationLayerGuardTests.Layouts_LinkFoundationInCascadeOrder` requires "firstLegacy" to exist after the foundation: `legacy-pages.css` and `shop.css` remain after the four links go, so it still holds (the check reads `links` that are not foundation names).

## 6. Phase 97's plan structure, and what Phase 98's equivalents need differently

[VERIFIED: `97-01/02/03/05/17-PLAN.md` frontmatter, `<tasks>` and `must_haves` read this session; `97-VALIDATION.md`; `~/qb-ui-review/phase-97/ENVIRONMENT.md`, `ORCHESTRATOR-OVERRIDES.md`, `EXECUTOR-BRIEF.md`.]

**Phase 97 shape.** `type: execute` plans with `autonomous`, `wave`, `depends_on`, `must_haves.truths`, typed tasks (`tracer`, `auto`, `checkpoint:human-verify gate="blocking-human"`, `checkpoint:decision gate="blocking-human"`):
- **97-01 (wave 1, not autonomous):** one operator sitting (prior phase finished and the checkout free, SQL at 1433 confirmed, accounts reused, the review-ref pushes approved); a tracer task (scratch root, copied tooling re-pointed, one capture of the first page under both User-Agents); a task recording the handover facts and the environment note.
- **97-02 (wave 2):** fixtures through the app's own forms (tracer: one fixture end to end into `fixtures.json` and a manifest row, probes, sweep, BEFORE record; then every remaining fixture, the **frozen baseline** copy of HEAD on the second port with `BASELINE_SHA`, `tests-before.txt` from `dotnet test --list-tests`; then the full manifest and the BEFORE capture of every row under both User-Agents).
- **97-03 and 97-05 (wave 3 and its follow-up):** Gate 1 scratch mockups built the way they ship (tracer frame, all frames, behaviour module mockup with recording), the numbered items list with recommendations and defaults, planner assumptions on the sheet, the package, a `checkpoint:decision` (approve with answers, or redraw named frames, v2, v3), answers written to `gate1/answers.json` with fixed keys every later slice checks in its precondition.
- **97-04, 97-06..97-13 slices:** behaviour-only slices first while Gate 1 held, shared partials before their consumers, then the page slices, the riskiest piece (the calendar module) last; each slice moves its own sheet link out of the global list when its views link it themselves.
- **97-14 cleanup and guards, 97-15/16 fit and a11y sweeps, 97-17 Gate 2:** the whole sheet of before/after pairs, the exceptions and questions section, a `checkpoint:decision`, release handoff.

**What Phase 98 does differently:**
1. **No review ref and no push.** Both gates are local packages under `~/qb-ui-review/phase-98/gate1/` and `gate2/` (`index.html`, `README.md`, `img/`) (D-07). Drop `publish-review.sh` and every `commit-tree`/`update-ref`/`push` task; keep its stage-tree assertion as a hygiene check on the local package: no `credentials.json`, no `localhost`/`127.0.0.1` in any HTML, no symlinks, the sheet opens from `file://` with zero failed requests, fantasy fixture data only (the p98 accounts are `@example.invalid`). Phase 97's own overrides did the same after the fact (`ORCHESTRATOR-OVERRIDES.md` item 3 and section 9).
2. **Branch and checkout.** Slices commit directly on `milestone/v9-rolling-improvements` in `/mnt/Data/repos/quest-board-dnd` (D-13), not on `feature/phase-97` in a worktree. The status at the start is clean (git status snapshot) and the only other worktree, `.claude/worktrees/shop-create-fix-23fe1a` (detached at `40e7f1f1`), is Phase 97 leftover: never touched. Executors stage files explicitly, never push (the orchestrator pushes the milestone branch after each green wave, per the project memory), and stop their own app before `dotnet build/test` (shared `bin/obj`).
3. **Ports.** Nothing listens on 8000, 8001, 8300 or 8301 now (`ss -ltn`, this session). Recommend **8310** (working tree) and **8311** (frozen baseline) so Phase 97's leftover defaults cannot collide; 98-01 re-checks with `ss -ltnp` (never `pgrep -f` loops) and re-points the tool defaults.
4. **Fixtures are per D-11, not the Audit Board.** New boards ("P98 Audit Characters", "P98 Audit Empty") created by the `superadmin` role through `/platform/Group/Create`, memberships through `/platform/Group/Members/{id}` (the split `fixtures.mjs:221-250` already uses); the three p98 accounts through Admin > Create user as `register97.mjs` does for p97 (adapt to `register98.mjs`). BEFORE is captured on the p98 boards from the **frozen baseline at the current HEAD**, so each page state exists in both apps (they share the one database).
5. **The look check (D-07)** is new. Build each Gate 1 mock on the foundation sheets **copied from the repository at execution time** (record the source commit in `site/PROVENANCE.txt`, as 97-03 did, at or after `c781090c`, the v6.0.2 card surface), render it beside `~/qb-ui-review/phase-94/mockups/direction-a/png/quest-details-1440.png`, `quest-details-390.png`, `quest-form-1440.png`, `quest-form-390.png` (and `quest-form--edit-*`), and **also measure it**: load Direction A's static `quest-details.html` and the mock in Playwright and diff computed `background-color`, `backdrop-filter`, `border-color`, the scrim band on `.modern-card-header` and the title colour of `.modern-card`, `h1/h2`. A numeric equality check is what prevents a repeat of the v6.0.2 drift. Parchment tables are allowed where they match those renders.
6. **Gate 1 items list** (numbered, each with a recommendation and the default that applies if unanswered, as 97-05 did):
   1. Crop dialog, desktop and phone, control row, Tab order, instruction wording (D-01..D-03). Default: Section 3c.
   2. Q-117 Manage table in its `.table-responsive` container, desktop at 320 (R-1).
   3. Q-118 form button row on every form page, desktop and phone. Recommend the shape Phase 95 already ships for Quest forms (`.mobile-layout .quest-form-actions`: one row, Cancel fixed at left, primary fills; `quest-form.css:101-113`) rather than a stack; fall back to `flex-direction: column-reverse` only if a label does not fit at 320.
   4. Q-119 image block aligned with the field stack (Characters and Contacts Create/Edit, desktop).
   5. D-05 notes panel on both layouts (glass card with the opaque scrim; notes as slips without per-note blur, Pitfall 9).
   6. The look check: Characters card grid, Contacts list, Players table, DM profile, a detail page with the notes panel.
   7. **Characters Create and Edit merge proposal (D-09)**: verdict in Section 2, default "no merge unless an explicit yes", with the placeholder decision attached.
   8. The merged detail pages' wording and order: Characters/Details labels ("Sheet Link" / "Character Sheet", "Class(es)" / "Classes", "Actions" / "Owner Actions"; A-characters-045 says keep unless asked, but a merged view must show one) and the DOM order of the portrait, actions and info cards (Pitfall 10). Recommend the form labels' words ("Character Sheet", "Classes") and "Actions".
   9. A-characters-043: the DM quest date goes through `Html.WallClock(..., "date")`, a visible change from "Nov 05" to "Nov 5", and Q-55's "Date TBD" on desktop. Accept or reject.
   10. Heading additions (Q-50, Q-52, Q-54, and the Players page that has none, A-033) use the existing page words; the empty-state copy stays verbatim from the audit table.
7. **Gate 2** (D-07): one sheet, every before/after pair, desktop and phone: the 26 BEFORE rows against their AFTER, the Quest/Quest Log pages for D-04 (a signup character without a picture, both layouts), the six editor pages with the keyboard-trap check (Section 7), and an **explicit accept/reject list** for any contrast or target-size exception. Expected entries: the eight crop handles stay 15px but the control row is an equivalent control on the same dialog (WCAG 2.5.8 "equivalent" exception, Assumption A5); the EasyMDE phone toolbar buttons at 30 to 34px (the known accepted deviation recorded in the audit A-11 results); anything the final sweep finds. Record exceptions in `contrast-exceptions.json` as Phase 97 did, never as a silent scope cut.
8. **Opening checkpoint (98-01)** content: SQL Server at 1433 is **down** (Environment Availability): the operator brings it up, the agent never does; port pair free; Phase 97's frozen baseline stopped; no other executor in this checkout; the p98 accounts approved; Mailpit confirmed unnecessary because the app runs in Development (Section 8); `senior-frontend` skill loaded for the front-end plans; scratch root `~/qb-ui-review/phase-98/` created with tools copied from `phase-97/tools` and re-pointed, `tools/node_modules` symlinked to `~/qb-ui-review/phase-94/tools/node_modules` (never `npm install`).

## 7. Tooling on this Linux machine

[VERIFIED: directory listings, `node`, `python3`, `dotnet`, `docker`, `ss` this session.]

- **Playwright 1.64.1** reached by symlink: `~/qb-ui-review/phase-97/tools/node_modules -> ~/qb-ui-review/phase-94/tools/node_modules`; `node -e` printed `1.64.1`. Browsers in `~/.cache/ms-playwright`: `chromium_headless_shell-1243`, `-1248`, `webkit-2359`, `-2370`, `ffmpeg-1011`, `-1013`. Use bundled Chromium through `chromium.launch()` (no `channel`, no Edge). Node v24.19.0, Python 3.14.4, .NET SDK 10.0.115.
- **Phase 94 assets (read-only inputs):** `~/qb-ui-review/phase-94/before/{desktop,mobile}/<Row>.png` and `before/report.json` for the 13 area rows (`Characters-Index` ... `Players-Index`, all present); `audit/characters/` (`probe1.mjs`/`probe1.json` crop dialog probes, `probe2`, `probe3` editor-trap probe, `test-crop.png`, `calc.mjs`, `shots/`); `mockups/direction-a/png/` (`quest-details-{1440,820,390}.png`, `quest-details--maxtitle-*`, `quest-form-*`, `quest-form--edit-*`, plus the `*-report.json` files) and the static HTML/CSS beside them; `fixtures.json`/`credentials.json` are Phase 94's and are **not** reused for p98.
- **Reusable Phase 97 scripts** (`~/qb-ui-review/phase-97/tools/`, 139 files; copy and re-point, never edit in place): `run-app.mjs` (starts `dotnet run` in Development on a port, pid and log under `logs/`, refuses to stop anything it did not start; default root is `phase-97`, so set `QBR` or edit), `stop-app.mjs`, `login.mjs` (`ROOT`), `audit-shoot.mjs` (`stateFor(role, port)`, `BOARD_TZ`), `register97.mjs` (the model for `register98.mjs`: Admin > Create user, passwords to `credentials.json` mode 600, never stdout), `fixtures.mjs` + `fixlib.mjs` (board creation and membership), `manifest.mjs`, `sweep.mjs`, `probes.mjs`, `contrast.mjs`, `readability-compare.py`, `pair-compare.py`, `overflow-compare.mjs`, `a11y-probe-97-16.mjs` / `a11y-focus-97-16.mjs` / `a11y-reduced-97-16.mjs` / `a11y-table-97-16.py` (the A-1..A-11 facts), `ed95-16.mjs` and `tb95-16.mjs` (EasyMDE keyboard-trap and toolbar-size checks on `/Characters/Create`, `/Contacts/Details/{id}`, `/Quest/Create`, both UAs: the model for the six-page X-3 check), `modal-probe.mjs`, `hint-probe.mjs`, `build-gate1.py` / `build-gate2.py` / `gate2-sheet.mjs` / `gate2-crops.mjs` (sheet builders), `date-lines.mjs`. **Do not use** `publish-review.sh`.
- **senior-frontend skill:** `~/.claude/skills/senior-frontend/` holds `SKILL.md`, `references/` and `scripts/` (listed this session; the description is the Designer, Builder, Critic skill for hand-built server-rendered UIs under a strict CSP). D-10 requires invoking it for mockups, gate sheets and the view and CSS conversion.
- **Contrast and A-11 method:** translucent surfaces are measured over the lightest board pixel `#794317` (audit method; `ContactCategoryContrastGuardTests.cs` `LightestBoard`), via `WcagContrast.TextOn(...)` in unit tests and `contrast.mjs` in the browser.
- **Test commands never set GC variables**; a `0x80131506` crash is a flake, rerun once per project. With 256 inotify instances in use by other processes, a mass failure can be an `inotify` limit (STATE note); the repo's `TestHostFileWatching.cs` already stops hosts using watchers (commit `8151f21b`).

## 8. Email triggers in this area

[VERIFIED: `grep -i "email|mail|IEmail|Enqueue"` over `CharactersController`, `ContactsController`, `ContactCategoryManagementController`, `DungeonMasterController`, `PlayersController` returned **nothing**.] The five controllers of the area send no email. The only trigger this phase can fire is **creating the p98 accounts through Admin**: `AdminController.CreateUser` enqueues `WelcomeEmailJob` for a new account (`AdminController.cs:142`) and `GroupMembershipAddedEmailJob` when an existing account is added to a board (`:157`); a stranded account added to a board (`:176`) and the resend action (`:413`) also enqueue a Welcome email. Recipients are `qb-audit-p98*@example.invalid` (undeliverable by construction) and `appsettings.Development.json` carries `"SuppressSending": true` with the comment "Skips the real SMTP send while running locally"; `run-app.mjs` starts the app with `ASPNETCORE_ENVIRONMENT=Development`. So D-12 holds without Mailpit as long as the app is started that way. If an executor ever sets `EmailSettings__SuppressSending=false` to look at a message, the SMTP settings must point at Mailpit (`qb-mailpit`, `docker run -d --rm --name qb-mailpit -p 127.0.0.1:1025:1025 -p 127.0.0.1:8025:8025 axllent/mailpit`, pre-approved), never at `appsettings.json`'s relay `192.168.6.13`. No `qb-mailpit` container exists now (`docker ps -a`).

## 9. Slice order, boundaries, plan count and waves

Phases 95 to 97 ran almost one plan per wave because plans share `UiFoundationLayerGuardTests`, `MobileViewsTests` and one checkout. D-03 (the crop AFTER is a mock shown at Gate 1 before any of it is built) means the crop slice cannot precede Gate 1, so CONTEXT's "crop dialog and placeholder first" is read as: the gate-independent placeholder slice lands while Gate 1 is held; the crop slice is the first slice after the gate. Recommended: **17 plans in 16 waves**.

| Plan | Wave | Autonomous | Content | Depends on |
|------|------|-----------|---------|------------|
| 98-01 | 1 | no | Opening checkpoint (Section 6 item 8); scratch root, copied tooling, a tracer capture of `/Characters` under both UAs; `senior-frontend` loaded | — |
| 98-02 | 2 | yes | `register98.mjs` p98 accounts, the P98 boards, every D-11 fixture through forms, `fixtures.json`, manifest (BEFORE rows for the 13 pages and their states, both UAs), frozen baseline build on the second port with `BASELINE_SHA`, `tests-before.txt`, probes | 01 |
| 98-03 | 3 | no | **Gate 1** local package and hold (Section 6 item 6); `checkpoint:decision`, redraw loop (D-08); answers to `gate1/answers.json` with fixed keys | 02 |
| 98-04 | 3 | yes | **D-04 avatar slice**: repository flag + unit test, `_CharacterAvatar`, six tags, `CharacterAvatarRenderTests`, the `BoardPageCharacterLinksTests:360` rewrite, WINDOWS #31 text. Writes only repository, three views and tests (98-03 writes only scratch) | 02 |
| 98-05 | 4 | yes | **Shared pieces**: the `.form-actions` component (per the approved Q-118 shape) in `components.css`, the stretched-row-link rule, `confirm-submit.js`, the `CharactersConversionGuardTests` skeleton, `UiFoundationLayerGuardTests` array hooks | 03 (Q-118 approved) |
| 98-06 | 5 | yes | **Crop dialog**: `_ImageCropDialog`, module rewrite with the control row and keys, `image-crop.css` layered, all ten hosts swapped (dialog and init only), `image-crop.css` leaves the global list, static and integration guards, Playwright behaviour check | 03 (D-03 approved), 05 |
| 98-07 | 6 | yes | **M-1 Characters/Details** merge, `character-detail.css`, confirm via data attribute, labels and order per Gate 1 item 8 | 05 |
| 98-08 | 7 | yes | **M-3 + M-4 Contacts Create and Edit** merged together: one shared form sheet, `tagify-vendor.css`, `contact-tags.js` module, zero-category hint on both UAs, Q-119 | 05, 06 |
| 98-09 | 8 | yes | **M-2 Contacts/Details** merge: notes module, D-05 surface, `aria-label`s, confirm attributes, A-054 deletion | 05 |
| 98-10 | 9 | yes | **M-6 DM EditProfile** merge, `dm-profile.css` link moved for it | 05, 06 |
| 98-11 | 10 | yes | **M-5 ContactCategoryManagement/Edit** merge and **Manage** (twin): `.table-responsive`, no `style=` on `<th>`, category-manage module and server-built confirm text, Q-54 heading, `contacts.mobile.css` rewrite | 05 |
| 98-12 | 11 | yes | **Characters Create/Edit**: merged if Gate 1 said yes, else converted as twins with the same shared pieces; class-row `<template>` module; Q-51 | 03, 06 |
| 98-13 | 12 | yes | **Characters/Index** (both): stretched row link, Q-50 heading, D-06 static badge, A-018 dead/retired badge and desaturated image, shared card sheet, `characters.css` leaves the global list | 05 |
| 98-14 | 13 | yes | **Contacts/Index** (both): Q-52 heading, Q-53 recolour only, row link, shared card sheet, `contacts.css` leaves the global list | 05, 13 |
| 98-15 | 14 | yes | **Players/Index** (both) and **DM Profile** (both): title, row link, Q-55 "Date TBD", `Html.WallClock`, nested container, `dm-profile.css` leaves the global list | 05, 10 |
| 98-16 | 15 | yes | **Cleanup and guards**: remove whatever remains of the four global links, finish `CharactersConversionGuardTests` lists and `UiFoundationLayerGuardTests` arrays, rewrite the remaining guards of Section 1c, full sweep both UAs (contrast, targets, overflow at 320 desktop and 390 phone, focus, reduced motion), the six-page editor-trap check, exceptions file, test-count comparison against `tests-before.txt` | 04..15 |
| 98-17 | 16 | no | **Gate 2** local package, `checkpoint:decision` (approve every pair and the exceptions, or name rejected pairs for a gap-closure loop), D-04 pages re-verified, release handoff (v6.3.0 through a PR into `main`; the `deploy` environment approval and the GHCR caveat from the roadmap) | 16 |

Why this order: behaviour-only work first while Gate 1 is held; shared pieces before their consumers (05 before 07 to 15); the crop partial before the form merges so each form is touched for its dialog once; Contacts Create and Edit together so their sheet moves once (Q-97 to Q-102 note); the four plain twins last because their risk is lowest and they share the card sheet. Each slice moves a sheet link out of the global list when its last consumer links it itself (Section 5), so every intermediate state is usable (D-13).

This table is the research proposal. The final plan set has 23 plans in 22 waves (fixtures split from the frozen baseline and BEFORE capture, Gate 1 split into two drawing packages and a sheet, and the crop, Contacts form and Characters list work split into more slices); the binding plan list and waves are in ROADMAP.md § Phase 98.

## Common Pitfalls

### Pitfall 1: Learning "has a picture" by including the image
**What goes wrong:** `ThenInclude(c => c.ProfileImage)` on the quest query loads every portrait's bytes per request. **Why:** `ProjectWithoutCharacterImages` exists precisely to avoid that. **How to avoid:** one scalar `ProfileImage != null` query keyed by the signups' character ids, set on the mapped models. **Warning signs:** quest page memory or response time grows with portraits; a query log shows `OriginalImageData` selected.

### Pitfall 2: Cropper's `keyboard` attribute
**What goes wrong:** document-level handler, 1px steps, Delete removes the selection, keys act even when focus is elsewhere in the page. **How to avoid:** never set `keyboard`; handle keys on the focused selection host. **Warning signs:** the static guard sees `keyboard` on `cropper-selection`.

### Pitfall 3: A selection that leaves the image
**What goes wrong:** buttons and arrow keys push the box past the picture edge and the JPEG gets black bands. **How to avoid:** clamp in the control path (Section 3c.5); optionally also cancel `change` for drags (a behaviour change, show at Gate 1). **Warning signs:** `$toCanvas` output with transparent or black edges in the Playwright check.

### Pitfall 4: Measuring on the hidden modal
**What goes wrong:** `$center`/`$initSelection`/bounds computed while the canvas is 0x0. **How to avoid:** only after `shown.bs.modal` (the existing code's sequence at `image-crop.js:183-215`); recompute bounds at each press, not at load.

### Pitfall 5: Renaming a hook class a test pins
`contact-card`, `contact-member-row`, `character-member-row`, `category-mgmt-row`, `contact-tag-chip`, `contact-filter-row`, `contact-info-value`, `fa-tag`, `contactFilterOffcanvas`, `badge bg-dark ms-1">Active`. **How to avoid:** add shared classes beside them, never replace; run the quick filter after each view.

### Pitfall 6: Merge tests that prove the wrong thing
`ContactCategoryMobileRenderTests` Details (`:103-124`) asserts the desktop UA does **not** render `contact-info-value`. After M-2 that is false by design. Rewrite to the merged shape; do not weaken it to nothing. The same holds for the Characters Create/Edit and DM EditProfile `MobileViewsTests` facts.

### Pitfall 7: Unlayered vendor CSS beats layered page rules
Tagify's stylesheet is unlayered, so any layered rule loses to it on equal ground, which is why `contacts.css:321-329` carries `!important`. Wrap it in the vendor layer (Pattern 5) and set Tagify's custom properties from tokens. **Warning signs:** the guard finds `!important` or a colour literal in the contact sheet; the tag box shows Tagify's stock grey.

### Pitfall 8: `hidden` on the cropped-file input
Replacing `style="display:none"` with the `hidden` attribute or `d-none` keeps the input in the form and `DataTransfer` assignment still works (it is the same element, only not rendered). Do **not** use `disabled` (it would drop the file from the POST).

### Pitfall 9: The notes surface (D-05)
The desktop note panel is `rgba(0,0,0,.03)` with a dead heading rule (`contacts.css:214-232`, A-054); the phone is `rgba(255,255,255,.85)` with grey `#6c757d` text at 3.68:1 (`contact-detail.mobile.css:107-115`, A-019). v6.0.2 found that blur on every repeated non-interactive card is the see-through drift, and `agenda.mobile.css`'s header comment states the fix: "A slip is not a card of its own, so it takes no panel blur however many repeat." So: one Notes `card modern-card` (scrim, glass, title band), each note a slip on `--qb-surface-scrim` with `--qb-fg` text and `--qb-fg-muted` meta, no `backdrop-filter`; the notes' Markdown headings take the surface colour.

### Pitfall 10: DOM order versus the desktop two-column layout
Desktop Characters/Details has the actions card in the left column under the portrait; the phone shows portrait, information, actions. Contacts/Details desktop: left column portrait and actions, right column description and notes; the phone shows portrait, actions, description, notes. For Contacts/Details one DOM order (portrait, actions, description, notes, back) serves both with a two-column grid. For Characters/Details a single DOM order cannot match both visual orders; options: (a) DOM = phone order and desktop grid areas move actions under the portrait (focus order then differs from visual order for owners only), (b) DOM = desktop order (the phone gains "actions above information" for owners). Put both on the Gate 1 sheet; recommend (a).

### Pitfall 11: `.mobile-layout` is chosen by User-Agent, not width
A merged view must hold at 320px on the desktop UA (no phone shell rules) and at 390px on the phone UA. The Manage table overflow (A-017) was found at exactly the desktop-UA 320px case.

### Pitfall 12: Admin account creation and the shared database
Creating the p98 accounts adds Audit Board members if done on the Audit Board (Phase 97 did the same with p97). Do not change any existing `qb-audit-*` membership, password or flag (D-11). The local database holds real member data: only clearly named fixtures, only through the app's forms, no SQL writes.

## Code Examples

Sketches written from the files cited above; every name below appears in those files.

### Repository flag for signup characters (new, `QuestBoard.Repository`)
```csharp
// After Mapper.Map<Quest>(entity) in GetQuestWithDetailsAsync and GetQuestWithManageViewDetailsAsync.
// Same scalar-query pattern as CharacterRepository: no image bytes are selected.
private async Task ApplyPictureFlagsAsync(Quest quest, CancellationToken token)
{
    var ids = quest.PlayerSignups.Where(s => s.Character != null).Select(s => s.Character!.Id).Distinct().ToList();
    if (ids.Count == 0) return;

    var flags = await DbContext.Characters
        .Where(c => ids.Contains(c.Id))
        .Select(c => new { c.Id, HasImage = c.ProfileImage != null })
        .ToDictionaryAsync(x => x.Id, x => x.HasImage, token);

    foreach (var character in quest.PlayerSignups.Where(s => s.Character != null).Select(s => s.Character!))
        character.HasProfilePicture = flags.GetValueOrDefault(character.Id);
}
```
Source pattern: `CharacterRepository.cs:21-32` (`imageFlags` and `GetValueOrDefault`). Names `PlayerSignups` and `Character` are as used by `QuestRepository.cs:404-421` and `Quest/Manage.cshtml:431-436`.

### `_CharacterAvatar.cshtml` (new)
```cshtml
@model CharacterAvatarModel   @* Id, Name, HasPicture, Decorative, CssClass *@
@if (Model.HasPicture)
{
    <img src="@Url.Action("GetCroppedPicture", "Characters", new { id = Model.Id })" alt="@(Model.Decorative ? "" : Model.Name)" class="avatar-32 @Model.CssClass" />
    <span class="avatar-32 avatar-32--placeholder @Model.CssClass" hidden aria-hidden="true"><i class="fas fa-user" aria-hidden="true"></i></span>
}
else
{
    <span class="avatar-32 avatar-32--placeholder @Model.CssClass" aria-hidden="true"><i class="fas fa-user" aria-hidden="true"></i></span>
}
```
Classes from `components.css:752-766`; the hidden sibling is what `avatar-fallback.js` reveals (`showPlaceholder`).

### Crop controls on the public API (new, inside `image-crop.js`)
```js
const STEP = 8;

function imageBounds(canvas, image) {
    const c = canvas.getBoundingClientRect();
    const i = image.getBoundingClientRect();
    return { left: i.left - c.left, top: i.top - c.top, right: i.right - c.left, bottom: i.bottom - c.top };
}

function nudge(selection, canvas, image, dx, dy) {
    const b = imageBounds(canvas, image);
    const x = Math.min(Math.max(selection.x + dx, b.left), b.right - selection.width);
    const y = Math.min(Math.max(selection.y + dy, b.top), b.bottom - selection.height);
    selection.$moveTo(x, y);
}

function resizeBy(selection, canvas, image, delta) {
    const b = imageBounds(canvas, image);
    const w = Math.min(Math.max(selection.width + delta, 32), b.right - b.left, b.bottom - b.top);
    const d = w - selection.width;
    // $change applies the element's aspect-ratio itself, so only the width is decided here.
    selection.$change(selection.x - d / 2, selection.y - d / 2, w, w);   // ratio is 1 on every page; derive h = w / ratio when another ratio is added
}

function reset(canvasImage, selection) {
    canvasImage.$center('contain');
    selection.$initSelection(true, true);   // the sequence image-crop.js:197-198 already runs
}
```
Source: API per the vendored `cropper.min.js` and the CropperSelection docs (Section 3b). After a resize the box should be re-clamped inside the image (a final `nudge(selection, canvas, image, 0, 0)`). Keys: `keydown` on the selection host only; the module writes the status sentence with `textContent`.

### Static Main badge (D-06, `@layer pages`, no animation)
```css
@layer pages {
  .characters-page .main-badge {
    box-shadow: 0 2px var(--qb-space-sm) color-mix(in srgb, var(--qb-accent) 50%, transparent);
  }
}
```
The current rule is `.characters-page .main-badge` at `characters.css:191-204` (`box-shadow: 0 2px 8px rgba(255, 193, 7, 0.5), 0 0 15px rgba(255, 193, 7, 0.3);` and `animation: main-badge-pulse 2s ease-in-out infinite;`) with its `@keyframes main-badge-pulse` at `:206-213`; the badge is `<div class="main-badge">` in `Characters/Index.cshtml:60`. The animation line and the keyframes are deleted; `--qb-accent` exists in `tokens.css`. The still glow keeps the gold as a shadow only, which the colour rules allow for an ornament, and the gradient fill literals move to tokens.

### DM Profile quest date (A-043, Q-55)
```cshtml
@if (quest.Date.HasValue) { @Html.WallClock(quest.Date.Value, "date") } else { <text>Date TBD</text> }
```
`Html.WallClock(value, style)` and the style `date` (`"MMM d, yyyy"`, `HtmlHelperExtensions.cs:33`); `QuestSummaryViewModel.Date` is `DateTime?` mapped from `FinalizedDate` (`ViewModelProfile.cs:133-134`), a wall-clock value.

### Confirm on submit (shared module)
```js
document.querySelectorAll('form[data-confirm-message]').forEach(function (form) {
    form.addEventListener('submit', function (event) {
        if (!confirm(form.dataset.confirmMessage)) { event.preventDefault(); }
    });
});
```
Pattern: `event-details.js:72-74`. Wording comes from the view, byte-identical to today's strings.

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| Per-view `modal-content bg-dark text-light`, drag-only crop, 10 copies of the dialog | One partial on the foundation overlay surface with a control row | this phase | WCAG 1.4.3 and 2.5.7 for the dialog (A-characters-001, 007) |
| Inline `onerror` on portraits | Phase 95: `avatar-fallback.js`; this phase: no request at all | Phase 95, then D-04 | Console 404s on Quest pages stop (WINDOWS #31) |
| Eleven phone sheets each redefining a glass card | `card modern-card` with scrimmed slips; one page sheet per merged view | Phase 94 foundation, Phases 95 to 97 | `!important` catch-alls (A-037) fall away |
| Inline `<script>` + `innerHTML` templates + `confirm(` | ES modules from `data-*`, `<template>`, `data-confirm-message` | Phases 95 to 97 | DoC-1 |

**Deprecated/outdated in this area:** the global `initImageCrop` and `initContactTags` functions; `ui-guidelines.md` still lists `calendar.css` among unlayered legacy sheets (Phase 97 converted it) and will want a one-line update for the area's sheets when Phase 98 closes.

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|---------------|
| A1 | `cropper-image.getBoundingClientRect()` reflects the image's CSS transform, so the bounds arithmetic in Section 3c.5 is right | Section 3 | Clamp is off by the image offset; the Playwright check must assert the selection stays inside the rendered image before the controls ship |
| A2 | A step of 8px per press and a 32px minimum selection are acceptable | Section 3c | Too slow or too coarse; adjust at Gate 1 with the mock |
| A3 | `role="group"` plus `aria-roledescription` and an `aria-label` on the `cropper-selection` host is announced usefully by screen readers | Section 3c.3 | A different pattern (for example `role="application"` or a labelled slider pair) may be needed; the screen-reader pass is a UAT item (D-13) |
| A4 | Tagify's own CSS custom properties are enough to theme it without `!important` once its sheet sits in the vendor layer | Pattern 5 | A few `:where()` overrides in the contact sheet; none may use `!important` |
| A5 | WCAG 2.2 SC 2.5.8 "Equivalent" exception covers the 15px crop handles because the control row does the same job at passing sizes | Section 6 item 7 | The operator can reject the exception at Gate 2; then the handles are enlarged |
| A6 | The operator accepts adding Create's placeholders to the merged Edit, and one wording per merged detail page | Sections 2, 6 | Gate 1 items 7 and 8 exist to ask |
| A7 | Commit buttons on the converted forms follow the button-role table (`btn-primary`, as `Quest/Create.cshtml:107`, `Events/Create.cshtml:113` do) while "Edit Character" and "Edit Contact" entry buttons stay `btn-warning` with the dark label | Section 6 item 3 | The audit's A-characters-011 only demands readable labels; if the operator wants amber commit buttons kept, the label colour fix alone applies |
| A8 | Ports 8310 and 8311 are free when 98-01 runs | Section 6 | 98-01 re-checks with `ss -ltnp` |
| A9 | The operator can bring SQL Server up at the opening checkpoint | Environment | The phase cannot capture or create fixtures until then |
| A10 | The visible change "Nov 05" to "Nov 5" on the DM quest history is acceptable (A-characters-043 says "confirm at sign-off") | Section 6 item 9 | Reject at Gate 1; then the date stays a raw format and DoC-9 needs an exception |
| A11 | Hold-to-repeat on the pointer buttons is not required for 2.5.7 (one click per step satisfies a single-pointer alternative) | Section 3 | Add press-and-hold repeat if Gate 1 finds the steps too slow |

## Open Questions (RESOLVED)

1. **Should Characters Create and Edit merge?** What we know: both pairs qualify after Q-51 with one non-CSS delta (two placeholders). What is unclear: the operator's preference (the default is "no merge unless explicit yes"). Recommendation: propose both at Gate 1 (Section 2); plan 98-12 carries both outcomes.
   RESOLVED: decided at Gate 1 item 7 (98-07) with default no merge; a merge happens only on the operator's explicit yes (`charactersFormMerge.merge`), and the Characters Create and Edit plan (98-17) carries both outcomes.
2. **Which wording and which DOM order for the merged detail pages?** (Section 6 item 8, Pitfall 10.) Recommendation: form-label wording; DOM = phone order for Characters/Details, one order for Contacts/Details.
   RESOLVED: decided at Gate 1 item 8 (98-07) with default the form-label wording (`Character Sheet`, `Classes`, `Actions`) and the phone DOM order; applied by the Characters/Details plan (98-11) and the Contacts/Details plan (98-14).
3. **Phone form button row: one row (Phase 95 Quest-form shape) or a stack?** Recommendation: the one-row shape; stack only if it does not fit at 320.
   RESOLVED: decided at Gate 1 item 3 (98-07) with default one row on both layouts, the stack only if a label does not fit at 360px (`formActions.phone`); implemented once in `components.css` by 98-08.
4. **Where are the p98 accounts first created?** D-11 says through Admin with the Audit Board admin login, which puts them on the Audit Board, and the P98 boards then get them through Platform > Group > Members. Recommendation: do exactly that (the Phase 97 split) and record that the p98 accounts are also Audit Board members.
   RESOLVED: settled in 98-01 (the operator approves the accounts and boards at the opening checkpoint) and 98-02 (created through Admin with the Audit Board admin login, then added to the P98 boards through Platform > Group > Members); the Audit Board membership is planner assumption P1, shown at Gate 1 for the operator to confirm or correct.
5. **No `98-UI-SPEC.md` exists.** Phase 97 had one (the design contract with the Gate 1 items, the Copywriting Contract and planner assumptions); `workflow.ui_phase` is true in `.planning/config.json`. Recommendation: run `/gsd-ui-phase 98` before planning or fold the Section 6 list into 98-03; the research already supplies the item list and the copy rule (no copy change without the operator).
   RESOLVED: superseded by `98-UI-SPEC.md`, which now exists and holds the design contract, the Gate 1 items (1 to 15) with their defaults, the Copywriting Contract and the planner assumptions.
6. **Players/Index has no page title today (A-characters-033).** Adding one is not covered by Q-50..Q-55. Recommendation: add the card-header title using the existing page word "Players", listed at Gate 1 item 10 so it is an explicit yes.
   RESOLVED: decided at Gate 1 item 10 (98-07) with default not added: the Players `h1` is added only on the operator's explicit yes (`headings.playersH1`); without it the page keeps its `h2` section titles and the missing heading becomes a Gate 2 decision item. Implemented by the Players plan (98-21).
7. **Should `avatar-fallback.js` stay?** Recommendation: yes (Section 4), a one-line decision for the planner.
   RESOLVED: keep `avatar-fallback.js` loaded on the three Quest views as the safety net for a picture that fails after render; decided in the tracer plan (98-05) and shown at Gate 1 as planner assumption P4.

## Environment Availability

[VERIFIED: probed this session. Nothing was started, stopped or provisioned.]

| Dependency | Required By | Available | Version | Fallback |
|------------|------------|-----------|---------|----------|
| .NET SDK | build, tests | yes | 10.0.115 | — |
| Node.js | scratch Playwright tooling | yes | v24.19.0 | — |
| Playwright + bundled Chromium | captures, probes, behaviour checks | yes, by symlink | 1.64.1 (`chromium_headless_shell-1243/1248` present) | none needed; never `npm install` |
| Python 3 | gate sheet builders, comparers | yes | 3.14.4 | — |
| Docker | Mailpit only, if ever needed | yes (`docker ps -a` answered) | — | — |
| SQL Server `localhost:1433` | running the app: fixtures, BEFORE/AFTER captures, probes | **no**: nothing listens on 1433; container `mssql-dev` is `Exited (137) 16 hours ago` | — | **Stop at 98-01 and tell the operator.** The agent never starts, installs or provisions it (CLAUDE.md local database contract) |
| xUnit suites (unit and integration) | every slice's verification | yes, need no database | EF InMemory (`WebApplicationFactoryBase.cs:65` `options.UseInMemoryDatabase(...)`); SQL-backed tests skip without `QUESTBOARD_MIGRATOR_TEST_CONNECTION` and fail only when `QUESTBOARD_MIGRATOR_TEST_REQUIRED=1` (`MigratorSqlServer.cs:16-17,38-45`) | none needed |
| Mailpit (`qb-mailpit`) | only if email suppression is ever switched off | not running, no container | `axllent/mailpit` pull on first start (pre-approved) | stay in Development with `SuppressSending: true` |
| Ports 8000, 8001, 8300, 8301 | working tree and frozen baseline | free now (`ss -ltn`) | — | use 8310 / 8311 |
| `senior-frontend` skill | D-10 | yes (`~/.claude/skills/senior-frontend/SKILL.md`) | — | — |
| `rip` MCP | symbol navigation | not exposed in this session | — | `grep` and file reads were used |

**Missing dependencies with no fallback:** SQL Server at 1433 (blocks 98-01 onward for anything that runs the app).
**Missing dependencies with fallback:** Mailpit (not needed in Development).

**On the integration suite and SQL Server** (the question asked): CLAUDE.md's statement still holds. Every web test uses the InMemory provider; only the migrator, board-deletion and shop-item SQL tests need a server and they skip otherwise. Phase 96/97 ran with `QUESTBOARD_MIGRATOR_TEST_REQUIRED=1` at the end only; that run is optional for this phase and cannot happen while the server is down.

## Validation Architecture

### Test Framework
| Property | Value |
|----------|-------|
| Framework | xUnit v3 3.2.2 + FluentAssertions 8.10.0; `Microsoft.AspNetCore.Mvc.Testing` 10.0.9; EF Core InMemory 10.0.9 [VERIFIED: `QuestBoard.IntegrationTests.csproj`] |
| Config file | `QuestBoard.IntegrationTests/xunit.runner.json` (`parallelizeAssembly: false`, `parallelizeTestCollections: false`) [VERIFIED] |
| Quick run command (integration) | `dotnet test QuestBoard.IntegrationTests --filter "FullyQualifiedName~CharactersConversionGuardTests\|FullyQualifiedName~CharacterAvatarRenderTests\|FullyQualifiedName~MobileViewsTests\|FullyQualifiedName~ContactCategory\|FullyQualifiedName~ContactsTags\|FullyQualifiedName~ContactDetailsCategoryTests\|FullyQualifiedName~CharactersControllerIntegrationTests\|FullyQualifiedName~ContactsControllerIntegrationTests\|FullyQualifiedName~DungeonMasterControllerIntegrationTests\|FullyQualifiedName~PlayersControllerIntegrationTests\|FullyQualifiedName~MarkdownEditorMarkupTests\|FullyQualifiedName~BoardPageCharacterLinksTests\|FullyQualifiedName~UiFoundation\|FullyQualifiedName~BoardPageStaticGuardTests\|FullyQualifiedName~QuestsConversionGuardTests"` (filter syntax cited from the Phase 97 validation file; not run here, a build would write `bin/obj`) |
| Quick run command (unit) | `dotnet test QuestBoard.UnitTests --filter "FullyQualifiedName~QuestRepository\|FullyQualifiedName~CharacterRepository\|FullyQualifiedName~AmbientClockSeamTests\|FullyQualifiedName~WallClock"` |
| Full suite command | `dotnet test QuestBoard.UnitTests` then `dotnet test QuestBoard.IntegrationTests` (per project avoids the CLR flake); counts at the end of Phase 97: unit 1412, integration 2029 (`97-VALIDATION.md`); re-recorded in `tests-before.txt` at 98-03 |
| Browser checks | scratch Playwright under `~/qb-ui-review/phase-98/` (bundled Chromium), phone checks under `devices['Pixel 7']` with touch; evidence stored, not in the suite (the repo has no JS test runner) |
| Estimated runtime | about 20 s quick after build, 2 to 3 min full (Phase 97 figures) |

### Phase Requirements to Test Map
| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| D-01 | Control row present with names; selection focusable; keys and Shift+arrow; ratio kept; clamped; Reset | static + integration + Playwright | `...~CharactersConversionGuardTests` (crop module and partial facts); `...~ImageCropDialogRenderTests`; scratch `crop-behaviour.mjs` (modelled on `probe1.mjs`) | ❌ Wave 0 / scratch |
| D-02 | Dialog has no `bg-dark text-light`/`style=`; title and text readable; hosts link `image-crop.css` | static + integration | `...~CharactersConversionGuardTests`; contrast via scratch `contrast.mjs` | ❌ Wave 0 |
| D-03 | Mock approved before build | human gate | Gate 1 package | n/a |
| D-04 | Signup character without a picture: placeholder, no `GetCroppedPicture/{id}` (Details, Manage, QuestLog); with one: URL present; repository flag set | integration + unit | `...~CharacterAvatarRenderTests`; `dotnet test QuestBoard.UnitTests --filter "FullyQualifiedName~QuestRepository"`; `...~BoardPageCharacterLinksTests` (rewritten `:360`) | ❌ Wave 0 |
| D-05 | One glass notes surface on both layouts; text 4.5:1 | static + Playwright | `...~CharactersConversionGuardTests` (no `rgba(255,255,255` panel); `ContactCategoryContrastGuardTests`-style token ratio; scratch `contrast.mjs` | ❌ Wave 0 / scratch |
| D-06 | Main badge has no animation; area has no infinite animation | static | `...~CharactersConversionGuardTests.AreaSheets_HaveNoInfiniteAnimation` | ❌ Wave 0 |
| D-07, D-08 | Gate 1 and Gate 2 approvals, redraw loop | human gate | local packages | n/a |
| D-09 | If merged: phone UA gets the merged Characters Create/Edit; if not, twins keep selection facts | integration | `...~MobileViewsTests` (rewritten) | exists, edit |
| D-10..D-12 | Tooling, fixtures, email | process | `fixtures.json` verify script; no mail asserted | manual |
| D-13 | Slices on the milestone branch, suite green per slice | process | full suite per wave | n/a |
| DoC-1 | No `style=`, `<style>`, inline script or `on*=` in the area's views | static | `...~CharactersConversionGuardTests.ConvertedViews_CarryNoInlineStyleScriptOrHandler`, `PhaseScope_IsFullyConverted` | ❌ Wave 0 |
| DoC-2 | Zero hex/`rgb()`/named colours/`!important`; each sheet in `@layer pages` | static | `...~CharactersConversionGuardTests.ConvertedSheets_*`; `...~UiFoundationLayerGuardTests` after appending | exists, edit |
| DoC-3 | Sheets linked from `@section Styles`; none in the layouts | static | `...~CharactersConversionGuardTests.ConvertedViews_LinkTheirSheets`, `Layouts_LinkNoAreaSheet`; `UiFoundationLayerGuardTests.Layouts_LinkFoundationInCascadeOrder` | ❌ Wave 0 / exists |
| DoC-4 | Twin answers present: phone headings (Q-50, Q-52, Q-54), phone Classes required (Q-51), desktop "Date TBD" (Q-55); Q-53 kept; merges done | integration | `...~MobileViewsTests`, `...~ContactsTagsMobileTests`, new `...~CharactersTwinAnswersTests` | ❌ Wave 0 |
| DoC-5 | Accessibility bar per page both UAs: contrast, focus ring, names, 24/44px, 320 desktop and 390 phone overflow, reduced motion | Playwright sweep | scratch `sweep.mjs`, `contrast.mjs`, `a11y-*-97-16.mjs` copies; results in `after/` | manual + evidence |
| DoC-6 | BEFORE/AFTER stored outside the repo; relaid-out pages signed off | human gate | Gate 2 sheet | n/a |
| DoC-7 | Mobile checks under a mobile UA with touch | integration + Playwright | existing mobile-UA facts + scratch | exists + manual |
| DoC-8 | Guards rewritten, none deleted | process | test-count comparison against `tests-before.txt` (per project, `dotnet test --list-tests`) | process |
| DoC-9 | `Html.LocalTime`/`WallClock` untouched; nothing writes to `Events`/`Quests`; feed files untouched | static + unit | `git diff --stat` shows no change to `HtmlHelperExtensions.cs`, `CalendarFeedWriter.cs`, `CalendarSubscriptionService.cs`, `FeedRevisionStamper.cs`; `~WallClockMarkupTests`, `~LocalTimeMarkupTests` unchanged | exists |
| DoC-10 | Every page still works | full suite + sweep | both full suites | exists |
| X-1 | Crop dialog meets 2.5.7 per Q-132 | static + Playwright + UAT | as D-01; real-touch and screen-reader pass in the phase UAT | ❌ / UAT |
| X-2 | The one remaining infinite animation capped | static | as D-06 | ❌ Wave 0 |
| X-3 | Editor keyboard trap fixed on the six pages | integration (markup) + Playwright | `...~MarkdownEditorMarkupTests` extended to Characters Create/Edit, Contacts Create/Edit/Details, DM EditProfile under both UAs; scratch `ed95-16.mjs` copy: Tab and Shift+Tab leave the editor, focus reaches the Save/Cancel/Add Note controls | exists, edit + scratch |
| Merge facts | Contacts/Details and Category Edit have a phone-UA case; Contacts Create/Edit render the tag input and library under both UAs | integration | `...~ContactCategoryMobileRenderTests`, `...~ContactsTagsFormMarkupTests`, `...~ContactCategoryManagementControllerIntegrationTests` | exists, edit |

### Sampling Rate
- **Per task commit:** the quick filters narrowed to the files the task touched, plus the scratch probe on the touched pages under both UAs.
- **Per wave merge:** both full suites, per project.
- **Phase gate:** full suites green, `dotnet build` clean, AFTER sweep shows no new contrast failure and no hidden control against BEFORE, `git diff --stat` shows no change to the helper and feed files, then `/gsd-verify-work`.
- **Max feedback latency:** about 120 seconds.

### Wave 0 Gaps
- [ ] `QuestBoard.IntegrationTests/Tests/CharactersConversionGuardTests.cs`: clone of `EventsConversionGuardTests` over the area's views, partials, sheets and modules (Section 1c), with the modules' `WritesNoMarkupFromStrings` facts, the crop module facts (no `keyboard`, no document-level key listener, uses `$moveTo`/`$change`), `AreaSheets_HaveNoInfiniteAnimation`, `RetiredFiles_AreGone`, the planning-reference scan
- [ ] `QuestBoard.IntegrationTests/Tests/CharacterAvatarRenderTests.cs` (D-04) and a `QuestRepository` unit test for the flag in `QuestBoard.UnitTests/Repository`
- [ ] `QuestBoard.IntegrationTests/Tests/ImageCropDialogRenderTests.cs`: the five host pages under both UAs render the dialog, the control-row names, the footer buttons, the stage class and no `bg-dark`
- [ ] `QuestBoard.IntegrationTests/Tests/CharactersTwinAnswersTests.cs`: Q-50, Q-51, Q-52, Q-54 headings, Classes required on the phone, "Date TBD" on desktop DM Profile, `Html.WallClock` rendering on both DM Profile layouts
- [ ] phone-UA cases for the merged `Contacts/Details` and `ContactCategoryManagement/Edit` (do not exist today)
- [ ] scratch tooling copied and re-pointed in plan 98-01 (`run-app`, `login`, `audit-shoot`, `register98`, `fixtures`, `sweep`, `probes`, `contrast`, `crop-behaviour`, the editor-trap check, the gate sheet builders)
Framework install: none (xUnit and Playwright already present).

## Security Domain

`security_enforcement` is not set to false in `.planning/config.json` (the file has no such key, only `nyquist_validation: true`), so it is enabled.

### Applicable ASVS Categories
| ASVS Category | Applies | Standard Control |
|---------------|---------|-----------------|
| V2 Authentication | no (no auth change) | existing |
| V3 Session Management | no | existing |
| V4 Access Control | yes | `[BoardPage(BoardPage.Characters)]`, `Contacts`, `Players`, `DungeonMasterProfile` gates and the owner-or-admin check (`CharactersController.CanManageCharacterAsync`) stay untouched; the new repository query runs through the group-filtered `Characters` set; `GetCroppedPicture` stays `[BoardPageExempt]` and unchanged |
| V5 Input Validation / output encoding | yes | Razor encodes every value; confirm messages and the tag whitelist go into attributes (never `Html.Raw` inside an attribute); modules use `textContent`/`cloneNode`, never `innerHTML` |
| V6 Cryptography | no | none added |
| V12 Files | yes | upload validators (`MaxFileSize` 5 MB, `AllowedExtensions` jpg/jpeg/png/gif, `CharacterViewModel.cs`) and the client pre-check stay; the crop output is a canvas JPEG blob; no new endpoint |
| V13 CSRF | yes | forms keep `asp-action` antiforgery; no new POST; the editor token meta (`_QuestFormScripts.cshtml`) is unchanged |

### Known Threat Patterns for this stack
| Pattern | STRIDE | Standard Mitigation |
|---------|--------|---------------------|
| Reverse tabnabbing through a user-entered sheet link that opens in a new tab with no `rel` (`Characters/Details.cshtml:121`, `Details.Mobile.cshtml:59`, A-051) | Spoofing | add `rel="noopener noreferrer"` and a visually hidden "(opens in a new tab)"; the `[Url]` attribute on `SheetLink` limits schemes |
| DOM XSS in the crop status line or the note editor module | Tampering | text via `textContent`; no `innerHTML`; static guard per module |
| Tag whitelist JSON in an attribute carrying a hostile tag name | Tampering | rely on Razor attribute encoding (`ContactsControllerIntegrationTests.cs:1138` and `ContactsTagsDesktopMarkupTests.cs:219-233` already assert escaping); do not use `Html.Raw` in the attribute |
| Cross-board data exposure through the avatar flag query | Information disclosure | query the group-filtered `DbContext.Characters`, never `IgnoreQueryFilters`; the flag is a boolean |
| Review package leaks credentials or member data | Information disclosure | stage-tree hygiene assertion on the local package; `@example.invalid` fixtures only |
| Fixture writes to a database holding real member data | Tampering | only p98-named fixtures, only through the app's forms, never SQL |

## Project Constraints (from CLAUDE.md)

- Never commit to `main`; all work on `milestone/v9-rolling-improvements` (current branch). The milestone branch is pushed by the orchestrator, not executors.
- SQL Server at `localhost:1433` is the operator's: never install, start, provision or tear down; never edit `appsettings.json`; on Linux never set GC-constraining env vars; re-run once on `0x80131506`.
- No GSD ids (requirement ids, phase or plan numbers, review ids, planning document names) in source, CSS, XML docs or string literals. Guards already scan listed files; the new guard includes the scan. This file's own tags are fine under `.planning/`.
- Before editing any Razor view read `.claude/ui-guidelines.md`: card pattern, filled buttons, `<hr>` then the row, tokens only, no inline code, dates through the helpers, twins.
- Layer rules: Service to Domain to Repository; EF only in `QuestBoard.Repository` (the avatar flag query belongs in `QuestRepository`, not the Service project); writes to `Events` and `Quests` go through the change tracker (this phase writes none); `CalendarFeedWriter.cs`, `CalendarSubscriptionService.cs`, `FeedRevisionStamper.cs` are untouched.
- Server-side "what day is it" reads go through `IBoardClock`; wall-clock values render via `Html.WallClock`, real instants via `Html.LocalTime` (the DM quest date is a wall-clock value; note timestamps are real instants and already use `Html.LocalTime`).
- Never cite a fixed member count for the board (project memory); credentials never printed; email only to Mailpit; real member data in the local database.
- `.claude/rip-navigation.md`: prefer RIP MCP for symbol questions; it was not exposed in this session, so file reads and grep were used (no RIP gap notice was emitted because no RIP tool was available to try).

## Sources

### Primary (HIGH confidence)
- Repo files read this session: `98-CONTEXT.md`, `98-DISCUSSION-LOG.md`, `ROADMAP.md` Phase 98 section, `94-AUDIT-characters.md` (all 876 lines), `97-RESEARCH.md`, `97-VALIDATION.md`, `97-01/02/03/05/17-PLAN.md` (frontmatter, task and must_haves lines), `STATE.md` (head and tail), `WINDOWS.md` entries 11 to 14, 28, 31, `.claude/ui-guidelines.md`, `.claude/architecture.md`, `CLAUDE.md`, `.planning/config.json`
- Source: all 26 area views (scans, diffs and targeted reads), `image-crop.js` (full), `avatar-fallback.js`, `contact-tags.js` (head), `markdown-editor.js` (Tab fix), `event-details.js` and `row-link.js` (heads), the four area global sheets and `image-crop.css`, `easymde-vendor.css`, `app.css`, `components.css` (modal, row-nav-link, avatar, empty-state), `tokens.css` (token existence), `quest-form.css` (button row), `players.mobile.css`/`agenda.mobile.css` (heads), `_Layout.cshtml`, `_Layout.Mobile.cshtml`, `QuestRepository.cs`, `CharacterRepository.cs`, `EntityProfile.cs`, `Character.cs`, `CharacterViewModel.cs`, `CharactersController.cs` (picture actions), `DungeonMasterController.cs`, `AdminController.cs` (email enqueue), `HtmlHelperExtensions.cs` (style table), `ViewModelProfile.cs:133-134`, `appsettings.Development.json`
- Tests read: `ContactCategoryContrastGuardTests` (full), `EventsConversionGuardTests` (head, test list, scope facts), `UiFoundationLayerGuardTests` (head, layer and legacy facts), `MobileViewsTests` (area facts), `ContactsTagsFormMarkupTests`, `ContactsTagsDesktopMarkupTests`, `ContactsTagsMobileTests`, `ContactCategoryMobileRenderTests`, `ContactDetailsCategoryTests`, `MarkdownEditorMarkupTests`, `BoardPageStaticGuardTests` (list), `BoardPageCharacterLinksTests` (`:330-375`), `CharactersControllerIntegrationTests` (`:735-800`), `MigratorSqlServer.cs`, `WebApplicationFactoryBase.cs`
- `~/qb-ui-review/phase-97/` (`ENVIRONMENT.md`, `ORCHESTRATOR-OVERRIDES.md`, `EXECUTOR-BRIEF.md`, tool listing, `run-app.mjs`, `register97.mjs`, `ed95-16.mjs`, `tb95-16.mjs`), `~/qb-ui-review/phase-94/` (before set, audit/characters probes, mockups/direction-a/png listing)
- Vendored `wwwroot/lib/cropperjs-2.1.1/cropper.min.js` (methods `$move`, `$moveTo`, `$resize`, `$zoom`, `$change`, `$reset`, `$initSelection`, `$handleKeyDown`, attribute list)
- Cropper.js v2 CropperSelection API: https://fengyuanchen.github.io/cropperjs/v2/api/cropper-selection.html (methods, `change` event, `keyboard` attribute, boundary handling)

### Secondary (MEDIUM confidence)
- Phase 95 to 97 precedent as recorded in their research and plan files (merged-view sheet pattern, stretched row link, `data-confirm-message`, vendor wrapper)

### Tertiary (LOW confidence)
- Assistive-technology behaviour of a labelled custom element host (A3) and the WCAG 2.5.8 equivalent-control exception (A5): training knowledge, to be confirmed in the UAT and at Gate 2

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH, no new packages; every library version read from its vendored path.
- Architecture: MEDIUM to HIGH, the merge, partial and module patterns have Phase 95 to 97 precedents; the crop control module is new code written against a read API but not run.
- Pitfalls: HIGH, each is tied to a file and line read this session.
- Environment: HIGH (probed), with the SQL Server state a live blocker.

**Research date:** 2026-11-13
**Valid until:** about 2026-12-11, or sooner if another phase edits `components.css`, `shell.mobile.css`, `MobileViewsTests.cs`, `UiFoundationLayerGuardTests.cs` or `_Layout*.cshtml` (re-read those before planning each affected slice), or when the operator brings SQL Server up and the baseline app starts.

