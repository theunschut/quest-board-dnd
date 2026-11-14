---
phase: 98-characters-contacts-dm-profile-and-players-ui-conversion
verified: 2026-11-14T15:00:00Z
status: human_needed
score: 12/15 must-haves verified
behavior_unverified: 3
overrides_applied: 0
behavior_unverified_items:
  - truth: "The crop dialog has a non-drag path that meets 2.5.7 (D-01, Q-132): move up, down, left and right, grow, shrink and Reset buttons, arrow keys moving the focused selection, Shift plus an arrow resizing it, the aspect ratio kept, a status line, controls at 24px desktop and 44px phone, and the dialog taking and returning focus"
    test: "On a host form (Characters Create, Contacts Edit, DM Edit Profile), choose a picture, open the dialog, Tab to the selection, press each arrow key and Shift plus each arrow, then press each control button and Reset, then Use This Crop and Discard Photo. Repeat under touch on a real phone, and with a screen reader."
    expected: "Each press moves or resizes the selection by 8px without changing the ratio; Reset re-centres; the status line is announced; focus stays inside the dialog and returns to the file input or Re-crop button on close; touch targets are 44px."
    why_human: "The invariant is runtime selection state and focus order across open, keypress and close. The repo guards (ImageCropDialogRenderTests, the CharactersConversionGuardTests module guard) pin markup, hooks and script text, not behaviour, and no repo test runs a browser. The only behavioural evidence is the scratch Playwright records (behaviour-crop-*.json, ten records, 26 to 28 checks each, 0 failed, both User-Agents, captured after the last change to the module), which live outside the repository."
  - truth: "The Markdown editor no longer traps a keyboard user on the six pages that render it (X-3; A-characters-006): Tab and Shift+Tab leave the field, Escape leaves focus usable"
    test: "On Characters Create and Edit, Contacts Create, Edit and Details (notes), and DM Edit Profile, focus each Markdown editor from the keyboard, press Tab, Shift+Tab and Escape, and confirm the next control is reachable."
    expected: "Tab moves to the next control, Shift+Tab to the previous one, Escape does not leave the user stranded, on desktop and on a phone with a hardware keyboard."
    why_human: "markdown-editor.js binds Tab and Shift-Tab to false and MarkdownEditorMarkupTests pins the six pages under both User-Agents, but that is configuration and markup. The behaviour (focus actually leaves) was only exercised by the scratch editor-trap.json (16 of 16 records pass, six pages by two User-Agents)."
  - truth: "Delete forms still ask before sending, and the Contacts notes panel edit, cancel and delete controls still work after the inline handlers were removed (A-characters-044)"
    test: "On Characters Details, Contacts Details, Contact Categories Manage (both layouts), press Delete, answer No, then Yes; on Contacts Details open Edit note, Cancel, and Delete note."
    expected: "No sends nothing and leaves the page unchanged; Yes deletes; the note editor opens and closes and Delete note asks first."
    why_human: "confirm-submit.js and contact-notes.js are wired through data attributes and module includes, and the views carry the attributes, but no repo test runs the scripts. The confirm box and the note toggles are runtime behaviour."
human_verification:
  - test: "Real phone with touch: the whole area (Characters list, details and forms, Contacts list, details and forms, Contact Categories, DM profile and edit, Players), including dragging the crop selection and using the control row now that the dialog's sheet loads on the phone"
    expected: "Phone layout is served on every page; controls are at least 44px; nothing scrolls sideways; the crop selection drags and the control row works under touch"
    why_human: "Real device, touch and real User-Agent; the sweeps used an emulated Pixel 7 profile. Deferred until after deploy by the operator's standing rule"
  - test: "Screen reader: the crop selection (group, role description, label), the control row and the status line; the Contacts note buttons (Edit note, Delete note); the picture file inputs read their visible labels"
    expected: "Names, roles and the status line are announced sensibly; nothing is announced twice or silent"
    why_human: "Screen-reader output cannot be observed programmatically. Deferred until after deploy by the operator's standing rule"
  - test: "Quest Details, Quest Manage and Quest Log Details in production with a character that has no picture"
    expected: "The placeholder shows, no request is made to /Characters/GetCroppedPicture, and the console is clean"
    why_human: "Production data and a browser console; the repo tests prove the markup carries no picture URL for a character without one"
  - test: "Re-look at the five picture-input hosts after the post-Gate-2 label change (5a6f7c84)"
    expected: "Each Profile Picture, Contact Image and Profile Photo label points at its own input, nothing moves visibly, and the crop dialog still opens from the input"
    why_human: "The fix landed after the v1 Gate 2 approval; the operator approved v2 after the re-captured rows, so this is a confirm-in-production item, not a gap"
  - test: "Run the three behaviour-unverified items above on the deployed build"
    expected: "As listed under behavior_unverified_items"
    why_human: "No repo test exercises these invariants"
gaps: []
deferred:
  - truth: "Desktop navbar at 320 px wraps over the page title (shell finding d2)"
    addressed_in: "A follow-up phase, recorded by the operator at Gate 2"
    evidence: "gate2/answers.json decisions.d2 is follow-up; deferred-items.md. It touches every page and is outside Phase 98's scope by the first gate's answer a4."
---

# Phase 98: Characters, Contacts, DM profile and Players UI conversion Verification Report

**Phase Goal:** Convert characters, contacts, contact categories, the DM profile and the players list, with the image crop dialog and the six approved twin merges, to the foundation, keeping every field and action and fixing the editor keyboard trap and the dialog findings.
**Verified:** 2026-11-14
**Status:** human_needed
**Re-verification:** No, initial verification
**HEAD:** be2e61d2

## Goal Achievement

The goal is achieved in the codebase. All 19 area views and the two shared partials are on the foundation with zero inline style, script or handler, zero hex, `rgb()` and `!important` in the area's 17 stylesheets, and no sheet in the global layout list. The six approved merges are made, and Characters Create and Edit were merged too, on the operator's explicit Gate 1 yes. The crop dialog has its control row and keyboard path, the picture request for a character without a picture is gone from this area and from the Quest pages, and the infinite Main badge animation is gone. What remains is human UAT the operator deferred until after deploy, plus three behaviour-dependent truths that only scratch browser records exercise.

### Observable Truths

| # | Truth | Status | Evidence |
|---|-------|--------|----------|
| 1 | DoC-1: zero inline `style=`, `<style>`, inline `<script>` and inline event handlers in the area's views | VERIFIED | Grep (`style\s*=`, `<style`, `<script`, `\son[a-z]+\s*=`) over `Views/Characters`, `Contacts`, `ContactCategoryManagement`, `Players`, `DungeonMaster`, `_ImageCropDialog`, `_CharacterAvatar` returns only `<script ... src="...">` includes (`character-form.js`, `image-crop.js`, `confirm-submit.js`, `avatar-fallback.js`, the Cropper vendor file). No `confirm(` in any view. `CharactersConversionGuardTests` (27 facts) passes. |
| 2 | DoC-2 and X-2: zero hex, `rgb()` and `!important` in the area's stylesheets; the one infinite animation (the Main badge pulse) is capped | VERIFIED | Grep over all 17 area sheets (`characters`, `characters.mobile`, `character-detail`, `character-form`, `contacts`, `contacts.mobile`, `contact-detail`, `contact-form`, `contact-categories`, `contact-categories.mobile`, `dm-profile`, `dm-profile.mobile`, `dm-profile-form`, `players`, `players.mobile`, `image-crop`, `member-cards`) returns no hex, no `rgb(`, no `!important`, no `infinite`. Each opens exactly one `@layer pages`. `member-cards.css:139` states the Main badge is a still gold glow. All 17 files exist. |
| 3 | DoC-3: the area's stylesheets load through `@section Styles`, not the global list | VERIFIED | Every area view links its own sheets (for example `Characters/Create` loads `character-form.css` and `image-crop.css`; `Contacts/Create` loads `tagify-vendor.css`, `contact-form.css`, `image-crop.css`). Grep of `_Layout*.cshtml` for `characters`, `contacts`, `dm-profile`, `image-crop`, `players`, `member-cards` returns nothing. |
| 4 | DoC-4: every twin aligned, every content difference answered, the approved merges done (Characters/Details, Contacts/Details, Contacts/Create, Contacts/Edit, ContactCategoryManagement/Edit, DungeonMaster/EditProfile, plus Characters/Create and Edit on the Gate 1 yes) | VERIFIED | The 13 retired files (8 views, 5 sheets) show as deletions against the phase baseline (`Characters/Create|Details|Edit.Mobile`, `Contacts/Create|Details|Edit.Mobile`, `ContactCategoryManagement/Edit.Mobile`, `DungeonMaster/EditProfile.Mobile`, plus `character-detail.mobile.css`, `character-form.mobile.css`, `contact-detail.mobile.css`, `contact-form.mobile.css`, `dm-editprofile.mobile.css`). Five twins remain by decision (Characters/Index, Contacts/Index, Manage Categories, DM Profile, Players/Index), each with one `h1`. Characters/Create labels Classes with the required marker on both layouts (Q-51). Phone Contacts keeps `Filter Tags` (Q-53 deliberate). Players carries an `h1` (Gate 1 item 10 yes). DM dates render through `Html.WallClock` with `Date TBD` on both layouts (Q-55). `CharactersTwinAnswersTests` passes. |
| 5 | Q-117, Q-118, Q-119 relayouts re-confirmed with their AFTER | VERIFIED | `gate1/answers.json`: manage table own-card approved, form action row one-row approved, image block approved; `gate2/answers.json` pairs approved on sheet v2. `Manage` loads `contact-categories.css`; the Manage 320 overflow went from scrollWidth 393 to 320 (scratch `after/final`, not re-rendered here). |
| 6 | D-04 and WINDOWS 31: a character without a picture renders the placeholder server-side and makes no `GetCroppedPicture` request, on this area's views and on Quest/Details, Quest/Manage and QuestLog/Details | VERIFIED | `_CharacterAvatar.cshtml` emits the image only when `Model.HasPicture`, otherwise the placeholder alone. It is used by `Quest/Details`, `Quest/Manage`, `QuestLog/Details` and `Characters/Index.Mobile`. Every remaining `GetCroppedPicture` URL (`Characters/Index` lines 42 and 108, `Characters/Edit` line 35) sits behind `HasProfilePicture`. The flag is real data: `CharacterRepository.cs` lines 30, 57, 75 and `QuestRepository.cs` `ApplyCharacterPictureFlagsAsync` project `c.ProfileImage != null` (read-only). No `onerror` in any view. `CharacterAvatarRenderTests`, `CharactersIndexRenderTests`, `CharacterFormRenderTests` pass. Controller unchanged (still 404). WINDOWS 31 is `fixed`. |
| 7 | DoC-5: accessibility bar A-1 to A-11 verified on each page, desktop and mobile | VERIFIED | `~/qb-ui-review/phase-98/after/final/a11y.md`: one row per page and layout; 217 sweep records, 0 not 200, 0 console errors, 0 missing focus rings, 0 obscured stops, 0 horizontal overflow at 320 desktop and 390 phone. Contrast 1012 BEFORE to 12 AFTER, all 12 the Tagify chip probe artefact (painted 7.1:1, accepted e8). Evidence is scratch-harness output; the code facts it cites agree with the repo (labels, `h1` counts, no `infinite`) but I did not re-render the pages. |
| 8 | DoC-6: BEFORE and AFTER screenshots outside the repository; relaid-out pages signed off at Gate 1 and Gate 2 | VERIFIED | `~/qb-ui-review/phase-98/{before,after,gate1,gate2,gate2-v1}` exist; Gate 2 sheet v2 has 227 pairs. `gate1/answers.json` and `gate2/answers.json` both carry `approved: true`; Gate 2 `v2Approval.approved: true`. Binding per the task: both gates operator-approved. |
| 9 | DoC-7: mobile checks listed in the UAT and run under a mobile User-Agent | VERIFIED | Every manifest row ran under both User-Agents (217 records); the crop behaviour records include a Pixel 7 touch profile; the real-phone and screen-reader checks are the UAT list in `98-23-SUMMARY.md`, carried above as human verification. |
| 10 | DoC-8: guard tests rewritten to the same intent, none deleted | VERIFIED | `git diff --diff-filter=D` over the test projects since the phase baseline lists no deleted file. The only four test methods that lost their old signature are `Details_Player_ReceivesNoTagMarkupAtAll` and `Details_DungeonMaster_RendersChipsAndBothTagNames` (now theories over both User-Agents, same names, `ContactsTagsDesktopMarkupTests`) and `CreateForm_Desktop...` and `CreateForm_Mobile...` (merged into one theory over both User-Agents, 98-12). 160 test methods added. |
| 11 | DoC-9: `Html.LocalTime` and `Html.WallClock` untouched; nothing writes to `Events` or `Quests` | VERIFIED | Non-test `.cs` files changed since the phase baseline: `QuestRepository.cs` (read-only `ApplyCharacterPictureFlagsAsync`, no Add, Update, Remove, SaveChanges, ExecuteUpdate, raw SQL or entity-state call) and the new `CharacterAvatarViewModel.cs`. Nothing under `QuestBoard.Domain` or `QuestBoard.Migrator`, no helper, feed or `site.js` change. |
| 12 | DoC-10: at merge, every page still works and reads | VERIFIED | `dotnet build`: 0 errors. Unit 1416 of 1416, integration 2300 passed, 16 skipped, 0 failed (re-run here). Sweep: 217 records all 200. |
| 13 | X-1 and Q-132: the crop dialog meets 2.5.7 with a non-drag path (control row, arrow keys, Shift plus arrow), on the foundation's overlay surface, aspect ratio kept | PRESENT_BEHAVIOR_UNVERIFIED | Present and wired: `Views/Shared/_ImageCropDialog.cshtml` (70 lines) holds the move/grow/shrink/Reset buttons with accessible names, a focusable selection (`tabindex="0"`, `role="group"`, label) and a polite status line; `wwwroot/js/image-crop.js` (391 lines) defines `STEP = 8`, the button actions through `$moveTo` and `$change`, `keydown` handling with Shift to resize, Reset, and reads `data-crop-aspect-ratio` from each of the five hosts; `image-crop.css` sets 24px/44px minimums and no literals; all five hosts load `image-crop.css` and the module. No repo test exercises the selection or focus behaviour. Scratch `behaviour-crop-*.json`: ten records, 26 to 28 checks each, 0 failed, both User-Agents, captured after the last module change (331e7fd8). Routed to human verification. |
| 14 | X-3: the Markdown editor keyboard-trap fix is verified on all six editor pages | PRESENT_BEHAVIOR_UNVERIFIED | `markdown-editor.js` lines 78-85 release `Tab` and `Shift-Tab`; `MarkdownEditorMarkupTests` covers the six pages under both User-Agents. The behaviour that focus leaves is only in scratch `editor-trap.json` (16 of 16 records pass, six pages by two User-Agents, toolbar buttons 44 by 44 on all 16). Routed to human verification. |
| 15 | Delete confirmations and Contacts note controls keep working after the inline handlers went (A-characters-044) | PRESENT_BEHAVIOR_UNVERIFIED | `confirm-submit.js` (12 lines) wires every `form[data-confirm-message]`; the Characters, Contacts and Manage views carry the attribute with a server-rendered sentence; `contact-notes.js` (72 lines) is a module include on Contacts/Details. No repo test runs either script. Routed to human verification. |

**Score:** 12/15 truths verified (3 present, behaviour unverified)

### Deferred Items

| # | Item | Addressed In | Evidence |
|---|------|-------------|----------|
| 1 | Desktop navbar at 320 px wraps over the page title (d2) | A follow-up phase | `gate2/answers.json` decisions.d2 follow-up; `deferred-items.md`. Shell (`_Layout`) finding, out of scope by Gate 1 answer a4. |

### Required Artifacts

| Artifact | Expected | Status | Details |
|----------|----------|--------|---------|
| `Views/Shared/_ImageCropDialog.cshtml` | One shared crop dialog with the control row | VERIFIED | 70 lines, hosted by five forms |
| `Views/Shared/_CharacterAvatar.cshtml` + `ViewModels/Shared/CharacterAvatarViewModel.cs` | Server-side placeholder | VERIFIED | Wired in three Quest views and Characters/Index.Mobile |
| `wwwroot/js/image-crop.js` | Crop module with controls and keys | VERIFIED (behaviour: truth 13) | 391 lines, module, no inline use |
| `wwwroot/js/{character-form,contact-tags,contact-notes,confirm-submit,avatar-fallback,row-link}.js` | Modules replacing inline scripts | VERIFIED | All exist, loaded as includes |
| 17 area stylesheets | Layered, literal-free | VERIFIED | Counts in truth 2 |
| `Characters/{Create,Edit,Details}`, `Contacts/{Create,Edit,Details}`, `ContactCategoryManagement/Edit`, `DungeonMaster/EditProfile` | One view each | VERIFIED | `.Mobile` twins deleted |
| `Characters/_CharacterClassRows.cshtml` | Class-row template partial | VERIFIED | Present, used by the merged form |
| `QuestBoard.IntegrationTests/Tests/CharactersConversionGuardTests.cs` and the render test files | Area guards | VERIFIED | 27 guard facts; render tests for index, details, form, avatar, crop dialog, contact details and index, DM profile, Players, twin answers |
| `.planning/WINDOWS.md` entries 12 and 31 | Closed on measured values | VERIFIED | Both `fixed` with the measurements named |

### Key Link Verification

| From | To | Via | Status | Details |
|------|----|-----|--------|---------|
| Five host forms | `image-crop.js` and `_ImageCropDialog` | `data-crop-input`, `data-crop-output-name`, `data-crop-aspect-ratio="1"`, module include, `image-crop.css` | WIRED | Each host carries the attributes and loads the sheet in `@section Styles` |
| Picture labels | picture inputs | `label for` equal to the input's own id | WIRED | All five hosts; `Host_PictureInputHasAnAccessibleNameFromItsVisibleLabel` covers both layouts |
| Quest views and Characters phone list | `_CharacterAvatar` | `CharacterAvatarViewModel.HasPicture` from the repository flag | WIRED | Flag comes from a real `ProfileImage != null` projection |
| Delete forms | `confirm-submit.js` | `data-confirm-message` | WIRED (behaviour: truth 15) | |
| Area views | their sheets | `@section Styles` | WIRED | No sheet in the global layout |
| `markdown-editor.js` | six editor pages | `_MarkdownEditor` partial | WIRED (behaviour: truth 14) | |

### Data-Flow Trace (Level 4)

| Artifact | Data variable | Source | Real data | Status |
|----------|---------------|--------|-----------|--------|
| Characters list and Quest avatars | `HasProfilePicture` | `CharacterRepository` and `QuestRepository.ApplyCharacterPictureFlagsAsync` project `ProfileImage != null` from the database | Yes | FLOWING |
| DM profile | `HasProfilePicture`, quest `Date` | `DungeonMasterProfileRepository` and `DungeonMasterController` | Yes, dates through `Html.WallClock` | FLOWING |
| Crop hosts | picture file, cropped output | file input to the crop module to the hidden `CroppedPictureFile` input | Scratch behaviour records show the cropped file is produced | FLOWING (scratch evidence) |

### Behavioral Spot-Checks

| Behavior | Command | Result | Status |
|----------|---------|--------|--------|
| Build | `dotnet build QuestBoard.slnx` | 0 errors, 65 warnings (pre-existing xUnit1051 analyser notes) | PASS |
| Unit suite | `dotnet test QuestBoard.UnitTests --no-build` | 1416 passed, 0 failed | PASS |
| Integration suite (EF InMemory) | `dotnet test QuestBoard.IntegrationTests --no-build` | 2300 passed, 0 failed, 16 skipped (SQL Server suites that need the environment flag; the migrator suite passed 11 of 11 at 98-22 per its summary, not re-run here) | PASS |
| No inline code in area views | grep for style, `<style>`, `<script>`, `on*=` | only module and vendor includes | PASS |
| No literals in area sheets | grep hex, `rgb(`, `!important`, `infinite` | none | PASS |
| No debt markers in area files | grep `TBD|FIXME|XXX|TODO|HACK` | only the user-facing copy "Date TBD" (Q-55, required wording) | PASS |
| No planning ids in area source | grep for phase, plan, Q-, A-characters, D- ids over views, sheets, modules | none (a font `unicode-range` in `app.css` only) | PASS |

### Probe Execution

No `probe-*.sh` is declared by this phase. SKIPPED. The scratch Playwright probes (`behaviour-crop`, `editor-trap`, `sweep`) live under `~/qb-ui-review/phase-98/` and were not re-run; their records were read.

### Requirements Coverage

`.planning/REQUIREMENTS.md` lists no requirement for Phase 98 and ROADMAP says "Requirements: TBD". The plan IDs are phase-local: D-01 to D-13 (98-CONTEXT), DoC-1 to DoC-10 (the ten-point Definition of converted) and X-1 to X-3 (the three phase extras, defined in 98-RESEARCH). None is orphaned.

| ID | Meaning | Status | Evidence |
|----|---------|--------|----------|
| D-01 | Crop controls and arrow keys | SATISFIED in code; behaviour is truth 13 (human) | `_ImageCropDialog`, `image-crop.js` |
| D-02 | Crop dialog on the opaque overlay surface | SATISFIED | `CropDialogPartial_UsesTheOverlaySurface` passes; no `bg-dark text-light` |
| D-03 | Crop AFTER mocked at Gate 1 | SATISFIED | `gate1/answers.json` crop approved (version v1, instruction A) |
| D-04 | Server-side picture placeholder | SATISFIED | Truth 6 |
| D-05 | Contacts notes panel on the glass card | SATISFIED | `gate1/answers.json` notesPanel approved; `ContactDetailsRenderTests` pass; contrast 0 failures on contact-details-full |
| D-06 | Static Main badge | SATISFIED | Truth 2 |
| D-07 | Two operator gates from local packages | SATISFIED | Both `answers.json`; no review ref owned by this phase (the orchestrator published the Gate 2 orphan branch, recorded in 98-23) |
| D-08 | Rejected AFTER iterated | SATISFIED | Gate 2 went v1 to v2 after the e7 gap closure; Gate 1 `redrawHistory` empty |
| D-09 | Characters Create/Edit merge only on explicit yes | SATISFIED | `gate1/answers.json` charactersFormMerge explicitYes true; merged |
| D-10 | Linux machine, BEFORE re-captured, senior-frontend | SATISFIED | `before/`, `baseline-src`, `tools` under the scratch root |
| D-11 | Operator checkpoint and fixtures through the app's forms | SATISFIED | `fixtures.json`, `fixtures-check.json`, three p98 accounts and five boards per 98-23 |
| D-12 | Email to the Mailpit sink only | SATISFIED | 98-01 and 98-02 summaries; no email-sending code changed |
| D-13 | Slices on the milestone branch, one release at the end | SATISFIED | Slices on `milestone/v9-rolling-improvements`; no tag made; v6.3.0 hand-off recorded |
| DoC-1 to DoC-10 | Definition of converted | SATISFIED | Truths 1 to 12 |
| X-1 | Crop 2.5.7 | SATISFIED in code; behaviour is truth 13 | |
| X-2 | Infinite animation capped | SATISFIED | Truth 2 |
| X-3 | Editor trap fix verified on six pages | SATISFIED in code and static tests; behaviour is truth 14 | |

### Anti-Patterns Found

| File | Line | Pattern | Severity | Impact |
|------|------|---------|----------|--------|
| `Views/DungeonMaster/Profile.cshtml`, `Profile.Mobile.cshtml` | 97, 18 | "Date TBD" | Info | User-facing copy the operator required (Q-55), not a debt marker |
| `Views/Contacts/Index.Mobile.cshtml` | 98 to 99 | No `h1` on a board without categories (decision d1) | Info | Known debt kept as is at Gate 1 item 10 and Gate 2; not a gap |

No `TBD`, `FIXME`, `XXX`, `TODO`, `HACK` or placeholder debt marker in the area's files. No blocker anti-pattern.

### Human Verification Required

See the frontmatter `human_verification` list. Five items: a real-phone touch pass, a screen-reader pass, the Quest pages in production with a picture-less character, a re-look at the five picture-input hosts after the post-Gate-2 label fix, and a run of the three behaviour-unverified items on the deployed build. The real-phone and screen-reader checks are deferred until after deploy by the operator's standing rule and are not gaps.

### Findings that are not gaps

- **Accepted exceptions (Gate 2).** e1 (eight Cropper handles at 15px, 2.5.8, met through the control row), e3, e4, e5, e6, e8 accepted; e2 retired (toolbar measures 44px); e7 fixed in `5a6f7c84`. d1 kept as is, d2 a follow-up phase, d3 accepted. None is counted against the score.
- **Gate approval provenance (WARNING, recorded, not blocking).** Gate 1 items 7 (Characters Create/Edit merge) and 10 (Players h1) were first answered "approved with recommendations" and recorded as yes after a confirmation reply of "uh yes?" (`gate1/answers.json` provenance). Gate 2 and v2 were approved through the orchestrator session ("gate is approved", then "approved"). This verification relies on the orchestrator's statement that both gates were operator-approved, as instructed. The merge of Characters Create and Edit is costly to reverse, so the operator may want to confirm item 7 in plain words.
- **Post-Gate-2 change.** `5a6f7c84` (five `label for` attributes plus one rendered test) landed after the v1 approval; the sheet was rebuilt as v2 with the 16 re-captured rows and the operator approved v2.
- **Evidence provenance.** Contrast, overflow, focus-ring, target-size and behaviour results come from the scratch harness in `~/qb-ui-review/phase-98/`. I checked that the records exist, agree with the code (the cited fixes are present) and post-date the last change to the crop module, but I did not re-render pages or start an app, per the task.
- **Skipped tests.** The 16 skipped integration tests are SQL Server suites enabled only by an environment switch; 98-22 reports the migrator suite 11 of 11 with the operator's connection.
- **Planning text in the repo.** `.claude/ui-guidelines.md` and `.planning/WINDOWS.md` were edited by this phase; both are planning or guideline files, not source comments.

### Gaps Summary

No gaps. Every roadmap contract item and the Definition of converted hold in the code at HEAD be2e61d2, and both test projects pass (unit 1416, integration 2300 passed with 16 environment-gated skips). Three behaviour-dependent truths (the crop dialog's non-drag path, the editor keyboard trap on six pages, and the delete confirmation and note controls) have present and wired code and static guards, but no repository test runs a browser, so they stand as `human_needed` next to the real-phone and screen-reader UAT the operator deferred until after deploy.

---

_Verified: 2026-11-14_
_Verifier: Claude (gsd-verifier)_
