---
phase: 98-characters-contacts-dm-profile-and-players-ui-conversion
plan: 13
subsystem: contacts-forms
tags: [ui-conversion, razor, form-contract, tagify-theme, merge, guard-tests]
requires: [98-12]
provides:
  - "Contacts/Create and Contacts/Edit as one view each for both layouts, on the form contract (form-page wrapper, h1 title, required marker with hidden spoken text, one-row form-actions with Cancel left and btn-primary right)"
  - "wwwroot/css/contact-form.css (pages layer): the picture block and the Tagify theme from tokens, with the two-tone focus ring on the tag box"
  - "contacts.css without the important-flag Tagify overrides"
  - "ContactsFormMergedViewTests and a Tagify theme guard"
affects: [98-14, 98-15, 98-23]
tech-stack:
  added: []
  patterns: [Tagify themed through its own custom properties from tokens; focus ring drawn on the wrapper with :focus-within because the focused node is a bare typing span]
key-files:
  created:
    - QuestBoard.Service/wwwroot/css/contact-form.css
    - QuestBoard.IntegrationTests/Controllers/ContactsFormMergedViewTests.cs
  modified:
    - QuestBoard.Service/Views/Contacts/Create.cshtml
    - QuestBoard.Service/Views/Contacts/Edit.cshtml
    - QuestBoard.Service/wwwroot/css/contacts.css
    - QuestBoard.IntegrationTests/Tests/ContactCategoryContrastGuardTests.cs
    - QuestBoard.IntegrationTests/Tests/MarkdownEditorMarkupTests.cs
    - QuestBoard.IntegrationTests/Tests/CharactersConversionGuardTests.cs
    - QuestBoard.IntegrationTests/Tests/UiFoundationLayerGuardTests.cs
    - QuestBoard.IntegrationTests/Tests/UiFoundationViewLibraryGuardTests.cs
  deleted:
    - QuestBoard.Service/Views/Contacts/Create.Mobile.cshtml
    - QuestBoard.Service/Views/Contacts/Edit.Mobile.cshtml
    - QuestBoard.Service/wwwroot/css/contact-form.mobile.css
decisions:
  - "Tag chips are the wood action fill with the action label colour (--qb-action / --qb-action-fg), the typing area is ink on the paper field, the suggestion list uses the same paper and wood through the library's dropdown properties"
  - "The focus ring sits on the tag wrapper (:focus-within) with the same outline, offset and gold band as every other control, measured unclipped on both layouts"
  - "Edit keeps its three fields without placeholders, as drawn in the approved contact-edit frames"
  - "Field messages and the summary use the validation tag helpers with no text-danger class, as the Quest forms and the category rename form do"
metrics:
  tasks: 3
  commits: 2
status: complete
actuals:
  tokens: 14000
  tasks: 3
  commits: 2
---

# Phase 98 Plan 13: Contacts Create and Edit merged onto the form contract Summary

Contacts Create and Edit are each a single view for the desktop and phone layouts on the form contract and one shared sheet that themes Tagify from tokens, with the four important-flag Tagify overrides gone and every guard that read the retired phone files rewritten to the same intent.

## The two merges

- **Contacts/Create** (commit `01c70224`, the tracer): one view; `contact-form-page form-page` wrapper (no `contacts-page`), page-title card with an `h1`, the approved default image block (the file input level with the Name field, hint under it), fields in the original order, Name required marker on the overlay chip with a visually hidden ` (required)`, hints in `.form-text`, the no-categories hint verbatim with the Manage Categories link, the Markdown editor partial, the crop partial and its data attributes, `#TagsInput` with `data-whitelist`, `hr` and `form-actions` (Cancel `btn-secondary` left, Create Contact `btn-primary` right). Styles section links `tagify-vendor.css`, `contact-form.css`, `image-crop.css`; scripts unchanged. Deleted `Create.Mobile.cshtml`.
- **Contacts/Edit** (commit `7a27cdae`): same structure plus the hidden Id, the current picture thumbnail as a class (`img-thumbnail form-picture-thumb` in `form-picture-current`) under the file input and its hint (never an inline size), `Re-crop Photo` `btn-warning btn-sm` with its crop data attribute, Save Changes `btn-primary`, Cancel back to Details. Deleted `Edit.Mobile.cshtml` and `contact-form.mobile.css`.
- Gate 1 answers applied: `imageBlock` default variant; `formActions` one-row (no stack modifier on these forms); form title gold (foundation default); exit label Cancel.

## contact-form.css and removed contacts.css rules

- `contact-form.css` (one `@layer pages` block, tokens only, no important flag): `.form-picture-current` and `.form-picture-thumb` (square, max 200px); the `.tagify` custom properties (chips `--qb-action` with `--qb-action-fg`, hover `--qb-action-hover`, border `--qb-control-border`, typing colour `--qb-ink`, placeholder `--qb-ink-muted`, invalid and remove tints by `color-mix` from `--qb-danger`); `.tagify:focus-within` with the shared two-tone ring; `.tagify__dropdown` properties (paper fill, ink text, wood primary); a phone-layout minimum height for the tag box. The 98-12 tag module and vendor wrapper are unchanged.
- Removed from `contacts.css` (the whole "tag entry widget" block, all four important flags; count 4 to 0): `.contacts-page .contact-tags-input .tagify` (custom properties with colour literals), `.contacts-page .contact-tags-input .tagify__input` (`color` and `text-shadow` important), `.contacts-page .contact-tags-input .tagify__input::before` (same). They were already inert once the forms stopped using `contacts-page`. No other contacts.css rule was form-only.

## Guards (old assertion to new assertion; none deleted; logged in tests-renamed.md)

- `ContactCategoryContrastGuardTests.ContactCategoryContrastGuard_MobileFormTextLinkRule_SetsParchmentColourWithUnderlineIntact`: read the phone sheet's `.contact-form-card .form-text a` rule; now reads the `.form-text a` rule in `components.css` that serves both layouts (`var(--qb-link)`, no `text-decoration: none`, 4.5:1) and asserts the retired phone sheet is gone.
- `...ZeroCategoryHelperLink_RendersOnBothDesktopAndMobileCardSurfaces`: asserted `contact-form-card` on the phone; now both User-Agents render the hint inside `.form-text` within a `modern-card`, the phone wrapper class is absent and `mobile-layout` appears only on the phone.
- `CharactersConversionGuardTests.ContactTagsModule_ReadsItsDataFromTheInput` and `UiFoundationViewLibraryGuardTests.TagifyVendorWrapper_ImportsTheVendoredSheetIntoTheVendorLayer`: view loops cover the two merged views.
- Lists extended: `ConvertedViews`, `ViewSheetLinks`, `ConvertedSheets`, `RetiredFiles`, `PageOnlySheets`, `PhaseSourceFiles`; `LayeredFiles` and `NoLiteralFiles` gain `contact-form.css`.
- New: `ContactsFormMergedViewTests` (Create and Edit render the same contract under both User-Agents, Edit without a picture offers no thumbnail or re-crop, no-categories hint on both forms under both User-Agents), `MarkdownEditorMarkupTests.ContactForms_RenderEditorHintAndLabelUnderBothUserAgents` (Create and Edit under both), `CharactersConversionGuardTests.TagifyTheme_LivesInTheContactFormSheetAsCustomProperties`.
- The 98-12 `ContactsTagsFormMarkupTests` theory and the player-refusal fact pass against the merged views unchanged.

## Test results

- Unit: 1416 passed, 0 failed, 0 skipped, 1416 total.
- Integration: 2125 passed, 0 failed, 16 skipped, 2141 total (2110 before; plus 15 new cases).
- `TestHostFileWatching.cs` and `QuestBoard.Service/Controllers` are untouched (git diff against PLAN_START is empty).

## Live checks (8310, scratch in `~/qb-ui-review/phase-98/after/98-13/`)

- **Sweep** of `contact-create`, `contact-create-errors`, `contact-create-nocat`, `contact-edit`, `contact-edit-nocat`, `crop-contact-create`, `crop-contact-edit` under both User-Agents at every width: 28 records, 0 not 200. `readability-compare.py` against BEFORE with `allow.json`: 28 compared, 0 failing rows, 0 poster-glyph violations, `ok`.
- **Allowed differences** (`allow.json`): the Name field's accessible name is now `Name * (required)` on all seven rows (a renamed control, none lost); on `crop-contact-edit` at 412 wide an editor text line behind the open crop dialog now fits on one 24px line because the merged phone form is wider (same content, not a control; the compare tool gained a `smallCarried` allow key for it).
- **Look check** (`lookcheck.mjs --row contact-create`, `lookcheck.json`): phone against `quest-form.mobile.html`, 16 properties, pass, only the two intended differences. Desktop against `quest-form.html`: the two intended differences plus `h1.color` (reference parchment, page gold `rgb(255, 193, 7)`): the approved gold form title, so the tool reports pass false for that one property only.
- **Tagify focus ring** (`ring/tagify-ring.json`, `tagify-ring-98-13.mjs`, screenshots in `ring/`): Create and Edit under both User-Agents draw `outline: solid 2px rgb(26, 15, 8)` and a `0 0 0 2px rgb(255, 193, 7)` gold band on the tag wrapper, and no `overflow` ancestor clips the 4px reach.
- **Editor keyboard check** (`editor-trap.json`): Create and Edit under both User-Agents: Tab and Shift+Tab leave the editor, Escape keeps focus usable, the commit button is reachable (2 tabs; BEFORE was 2 on desktop and 1 on the phone, where the old phone view put the commit first; the phone exit now comes first by Gate 1).
- I viewed the desktop and phone Create and Edit captures: gold Cinzel title on the band, wood chips on the paper tag box, picture block level with the Name field, the 200px thumbnail with Re-crop under the hint, one action row with Cancel left and the wood commit right (phone: Cancel at natural width, commit filling).

## Deviations from Plan

### Auto-fixed Issues

None in the product.

### Plan interpretations

1. **[Verify script shape]** The plan's last editor check indexes `editor-trap.json` as an object; the tool writes a list of records. I evaluated the same three conditions (plus Escape) over the list: all four records pass.
2. **[Tracer gate]** No auto flag is set and this sequential run cannot be resumed, so the tracer gate was the automated re-run of the tracer's targeted tests plus a live capture of the merged Create under both User-Agents before Edit was changed; both were green.
3. **[Phone media query]** The plan's draft names a media query for the phone; the phone layout is chosen by User-Agent, so the one phone rule is scoped with `.mobile-layout` instead of a width query.
4. **[Scratch tools]** `readability-compare.py` gained an optional `smallCarried` allow key (scratch tool, outside the repository); `tagify-ring-98-13.mjs` was added.
5. **[Test file added]** `ContactsFormMergedViewTests.cs` (not in the plan's file list) holds the render facts for the merged views.

## Known Stubs

None.

## Threat Flags

None. No controller, tag parsing, validator or POST field changed (T-98-32); one view serves both User-Agents behind the same controller gates and the player-refusal fact still passes on both (T-98-62); the views keep the Razor-encoded `data-whitelist`, with no inline script, no style attribute and no raw output (T-98-63).

## Self-Check: PASSED

- `Create.cshtml`, `Edit.cshtml`, `contact-form.css` exist; the two phone views and `contact-form.mobile.css` are gone; commits `01c70224` and `7a27cdae` exist.
- `grep -c "!important" contacts.css` prints 0 (4 at PLAN_START); no style attribute or inline script in either view; `data-whitelist` present once in each.
- `legacy-collision.py` prints nothing for both views.
