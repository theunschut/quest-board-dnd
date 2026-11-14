---
phase: 98-characters-contacts-dm-profile-and-players-ui-conversion
plan: 14
subsystem: contacts-details
tags: [ui-conversion, razor, merge, notes-panel, confirm-module, guard-tests]
requires: [98-13]
provides:
  - "Contacts/Details as one view for both layouts (title card, Actions, Description, Notes, back) with the notes as one glass card of scrimmed slips"
  - "wwwroot/css/contact-detail.css (pages layer) and wwwroot/js/contact-notes.js (edit toggle through the hidden attribute)"
  - "ContactDetailsRenderTests (both User-Agents) and the guard facts ContactDetailSheet_HasNoBlurOnSlips and ContactNotesModule_WritesNoMarkupFromStrings"
affects: [98-15, 98-23]
tech-stack:
  added: []
  patterns: [one view for both layouts on the Bootstrap row with a phone gutter fix in the pages layer; note edit toggle through hidden with lazy editor construction; delete confirmation through data-confirm-message and the shared confirm module]
key-files:
  created:
    - QuestBoard.Service/wwwroot/css/contact-detail.css
    - QuestBoard.Service/wwwroot/js/contact-notes.js
    - QuestBoard.IntegrationTests/Tests/ContactDetailsRenderTests.cs
  modified:
    - QuestBoard.Service/Views/Contacts/Details.cshtml
    - QuestBoard.Service/wwwroot/css/contacts.css
    - QuestBoard.Service/wwwroot/css/components.css
    - QuestBoard.IntegrationTests/Tests/ContactCategoryMobileRenderTests.cs
    - QuestBoard.IntegrationTests/Controllers/ContactsTagsDesktopMarkupTests.cs
    - QuestBoard.IntegrationTests/Tests/MarkdownEditorMarkupTests.cs
    - QuestBoard.IntegrationTests/Tests/CharactersConversionGuardTests.cs
    - QuestBoard.IntegrationTests/Tests/UiFoundationLayerGuardTests.cs
  deleted:
    - QuestBoard.Service/Views/Contacts/Details.Mobile.cshtml
    - QuestBoard.Service/wwwroot/css/contact-detail.mobile.css
decisions:
  - "The merged view keeps the approved frames' Bootstrap row (col-lg-4 and col-lg-8) rather than a grid: the DOM order the phone reads is title card, Actions, Description, Notes, back, and the large breakpoint puts the first two left and the next two right with no reordering"
  - "Within the title card the order is portrait, town, sub-location, category line, Hidden badge, tag chips (the order the plan names); the Hidden badge is an icon and the word"
  - "The empty Notes sentence takes the card's own text colour in the page sheet, the same override the Agenda and Availability empty states carry, because the muted tone measures 4.25:1 over the lightest board pixel"
  - "The slip's note text keeps the Markdown headings in the slip's own colour (color: inherit), so a heading a member writes holds the same contrast as the paragraph under it"
metrics:
  tasks: 3
  commits: 4
status: complete
actuals:
  tokens: 21000
  tasks: 3
  commits: 4
---

# Phase 98 Plan 14: Contacts/Details merged with a glass notes panel Summary

Contacts/Details is one view for the desktop and phone layouts. The notes are one `modern-card--aside` Notes card of slips on the scrim, the note edit toggle is one module, the delete questions go through the shared confirm module, and the phone's near-white note panel (1.01:1 on BEFORE, overflowing to a 784px page) is gone.

## The merge (files deleted)

- Deleted: `Views/Contacts/Details.Mobile.cshtml` (with its duplicate inline note script) and `wwwroot/css/contact-detail.mobile.css` (the near-white note panel, grey meta text, important-flag text colours).
- `Details.cshtml` (commit `c2d74206`): `contact-detail-page` wrapper (no `contacts-page`); title card with the `h1`, the portrait (`alt=""`, the name is the heading) or the placeholder tile (`aria-hidden`), town and sub-location lines, the category line with its `fa-tag` marker only when assigned, the Hidden badge with `fa-eye-slash` and the word, and the tag chips for the roles that see them today; Actions (`Edit Contact` `btn-warning`, the show and hide button, `Delete Contact` `btn-danger` with `data-confirm-message`); Description (Markdown or `No description provided.`); Notes; Back. All role conditions are today's (`CanManage` for Actions and tag chips, every viewer sees the note controls).
- Scripts: the editor scripts as before plus `contact-notes.js` and `confirm-submit.js` as modules. No inline script, no `onsubmit`, no `style` attribute.

## The notes panel as built

- One `section#notes-card.card.modern-card.modern-card--aside`; the add-note form (Markdown editor partial unchanged) at the top with `Add Note` as `btn-primary`; the slips in a `ul.notes-list`.
- A slip (`li.notes-slip`) is `--qb-surface-scrim` with `--qb-fg` text and a `--qb-fg-muted` meta line (author, real-instant time through `Html.LocalTime`, `(edited)` when changed), padding and radius from tokens, `overflow-wrap: anywhere`, and no `backdrop-filter`. Markdown headings inside a slip take the slip's colour; the dead heading rule in `contacts.css` is deleted.
- Controls: icon buttons `btn-warning btn-sm` and `btn-danger btn-sm` named `Edit note` and `Delete note` through `aria-label`; the edit form carries `hidden` (Cancel `btn-secondary btn-sm`, Save `btn-success btn-sm`). Sentences byte-identical: `Delete this note? This action cannot be undone.` and `Are you sure you want to delete this contact? This action cannot be undone.`
- Empty state: the shared `.empty-state` with `No Notes Yet` and `Be the first to jot something down about this contact.` verbatim.
- `contact-notes.js`: Edit note removes `hidden` from the form and hides the slip text; Cancel reverses and returns focus to Edit note; one slip open at a time; focus moves into the editor; the editor is built the first time a slip opens (never while hidden) from the globals the shared editor script publishes; no markup from strings, no global of its own, no inline style.

## Removed rules

- `contacts.css`: the `.note-item` background and the `.modern-card-body .note-item h1..h6` ink rule (the 21 lines of the "Notes list (Details page)" block).
- `components.css`: the `.contact-detail-card .note-item` selector in the paper-variable list (it named a class only the retired phone view used).

## Guards (none deleted; logged in tests-renamed.md)

- `ContactCategoryMobileRenderTests.ContactCategoryMobileRender_ContactDetails_...` (same name): asserted the desktop User-Agent did not render `contact-info-value`; now both User-Agents render the merged view with `contact-info-value` and the category name and only the phone has `mobile-layout`.
- `ContactsTagsDesktopMarkupTests.Details_Player_ReceivesNoTagMarkupAtAll` and `Details_DungeonMaster_RendersChipsAndBothTagNames` (same names): now theories over the default client and a phone User-Agent.
- `ContactDetailsCategoryTests` (unchanged, passes) and `ContactsTagsMobileTests.Details_MobileDungeonMaster_RendersTagChips` (unchanged, passes) stay valid against the merged view.
- New: `ContactDetailsRenderTests` (11 theories, 22 cases: one view, DOM order, slips and named controls with hidden edit forms, Add Note primary and above the slips, confirm sentences and no inline code, bare contact, hidden badge, revealed contact, player view, long and unbroken text, real-instant timestamp); `MarkdownEditorMarkupTests.ContactDetails_NoteEditors_RenderEditorHintAndLabelUnderBothUserAgents`; `CharactersConversionGuardTests.ContactDetailSheet_HasNoBlurOnSlips` and `ContactNotesModule_WritesNoMarkupFromStrings`.
- Lists extended: `ConvertedViews`, `ViewSheetLinks`, `ConvertedSheets`, `RetiredFiles`, `PageOnlySheets`, `PhaseSourceFiles` (CharactersConversionGuardTests); `LayeredFiles` and `NoLiteralFiles` gain `contact-detail.css` (UiFoundationLayerGuardTests). `legacy-collision.py` on the view prints nothing.

## Test results

- Unit: 1416 passed, 0 failed, 0 skipped, 1416 total.
- Integration: 2153 passed, 0 failed, 16 skipped, 2169 total (2125 passed before; plus 28 new cases). The last CSS-only change (the empty sentence colour) was re-verified with the guard and render facts (139 passed).
- `TestHostFileWatching.cs` and `QuestBoard.Service/Controllers` are untouched (git diff against PLAN_START is empty).

## Live checks (8310, scratch in `~/qb-ui-review/phase-98/after/98-14/`)

- **Sweep** of `contact-details-full`, `-bare`, `-hidden`, `-player` and `contact-note-edit` under both User-Agents at every width: 20 records, 0 not 200. `readability-compare.py` against BEFORE (`--zero-contrast phase`): 20 compared, 0 failing rows, 0 poster-glyph violations, 0 residual contrast, `ok`.
- **Note contrast on the render** (`measure/report.json`, composited over the lightest board pixel by the sweep's contrast probe): note text 8.05:1 and meta line 7.31:1 on both layouts; the note's Markdown heading 8.05:1. BEFORE the phone note text and heading measured 1.01:1 (parchment on the near-white panel), and its unbroken string pushed the page to scrollWidth 783 to 784; AFTER there is no overflow at 390, 412 or 320. The phone `Edit Contact` label (BEFORE 1.68:1 parchment on amber) is now the dark label of `btn-warning`, with no contrast failure on the row.
- **Look check** (`lookcheck.json`, `--row contact-details-full` against Direction A): desktop 31 properties, 2 intended differences (glass hover value, button border), pass true; phone 31 properties, same 2 intended differences, pass true.
- **Editor keyboard check** (`editor-trap.json`, `--pages contact-details-full`): both User-Agents, Tab and Shift+Tab leave the add-note editor, Escape keeps focus usable, Add Note is reached by Tab (1 press); `ok`.
- **Notes toggle** (`notes-toggle.json`, scratch `notes-toggle-98-14.mjs`, no data written): edit opens the form, hides the slip text, builds one editor and focuses it; opening a second slip closes the first; Cancel hides the form and returns focus to Edit note; Delete note shows exactly the sentence in a confirm and, dismissed, posts nothing. Same on both User-Agents; no console or page errors.
- I viewed the desktop and phone details captures (full, hidden, note edit): gold Cinzel title on the title band, the portrait and lines, the full-width action buttons, the glass Notes card with the add form on top, slips on the scrim with filled icon buttons, the inline edit form with Cancel and Save, the empty state, Back at the bottom.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] The empty Notes sentence failed 4.5:1 on the glass card**
- **Found during:** Task 3 sweep (`p.empty-state-text` at 4.25:1 on the bare and hidden contact rows, all widths)
- **Issue:** the shared empty-state text is the muted tone, which measures under 4.5:1 over the lightest board pixel on a glass card
- **Fix:** `contact-detail.css` sets `.empty-state-text` to the card's own colour, the same override the Agenda and Availability empty states carry
- **Files modified:** `QuestBoard.Service/wwwroot/css/contact-detail.css`
- **Commit:** `62b9e1df`

### Plan interpretations

1. **[Test-first order]** The render facts were written together with the view in Task 1 (the tracer) rather than failing against the old view first; the first run's eight failures were three assertion mistakes in the new test (the slip tag carries a `data-note-id`; the helper's `data-style` attribute contains the text `style="`; the `datetime` attribute is not the first on `time`), fixed in the test, not the view. The guards are proven by explicit forbidden shapes.
2. **[Tracer gate]** No auto flag is set and this sequential run cannot be resumed, so the tracer gate was the targeted tests green plus a live capture of the merged page under both User-Agents before Task 2 (as in the previous plan).
3. **[Column layout]** The plan names a grid through a media query; the approved frames use the Bootstrap row with `col-lg-4` and `col-lg-8`, which gives the same DOM order and columns, so the sheet holds only the phone gutter fix (scoped with `.mobile-layout`, because the phone layout is chosen by User-Agent).
4. **[components.css]** One selector (`.contact-detail-card .note-item`) was removed from `components.css`, which the plan does not list; it named a class that no longer exists.
5. **[Scratch tools and manifest]** `contact-note-edit`'s assertion now names the merged form class too (`.note-edit-form, .notes-edit-form`); `contact-details-full` gained a measure list for the note text, meta line and heading; `allow.json` records that the clicked note's own Edit note and Delete note buttons are now hidden while it is edited (BEFORE the Bootstrap flex class beat the inline display none, so they stayed beside the open editor). `notes-toggle-98-14.mjs` was added. All outside the repository.

## Known Stubs

None.

## Threat Flags

None. No controller, note endpoint, authorization or cross-board guard changed (T-98-35; `ContactsControllerIntegrationTests` green); the module toggles `hidden` and moves focus and builds no markup from strings, with note text rendered server-side by the unchanged Markdown pipeline (T-98-33); one view serves both User-Agents behind the same checks, and the player-view fact runs under both and asserts today's controls only (T-98-34).

## Self-Check: PASSED

- `Details.cshtml`, `contact-detail.css`, `contact-notes.js`, `ContactDetailsRenderTests.cs` exist; `Details.Mobile.cshtml` and `contact-detail.mobile.css` are gone; commits `c2d74206`, `28a6e78d` and `62b9e1df` exist.
- `grep -c "Delete this note? This action cannot be undone." Details.cshtml` prints 1; `grep -c "<script>"` prints 0; both `aria-label` names present.
