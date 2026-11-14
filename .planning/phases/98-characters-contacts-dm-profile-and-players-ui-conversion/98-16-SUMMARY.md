---
phase: 98-characters-contacts-dm-profile-and-players-ui-conversion
plan: 16
subsystem: contact-category-management
tags: [ui-conversion, razor, twins, responsive-table, server-built-confirm, guard-tests]
requires: [98-15]
provides:
  - "ContactCategoryManagement/Manage (desktop) with the category table in .table-responsive inside its own aside card, column widths as classes, one h1, no inline style or script"
  - "ContactCategoryManagement/Manage.Mobile with the page heading Manage Categories, 44px slips and the add form on the title card"
  - "the delete question built in the views into data-confirm-message and shown by the shared confirm module, identical words on both layouts"
  - "wwwroot/css/contact-categories.css and contact-categories.mobile.css (pages layer, tokens only)"
  - "CategoryManageRenderTests, CharactersTwinAnswersTests (PhoneManage_HasThePageHeading), a rewritten contrast fact and a new empty-sentence fact"
affects: [98-17, 98-23]
tech-stack:
  added: []
  patterns: [table scrolls inside its own card instead of widening the page; confirmation sentence built server-side and Razor-encoded into a data attribute; native fragment navigation focuses the tabindex -1 row, so no focus script]
key-files:
  created:
    - QuestBoard.Service/wwwroot/css/contact-categories.css
    - QuestBoard.Service/wwwroot/css/contact-categories.mobile.css
    - QuestBoard.IntegrationTests/Tests/CategoryManageRenderTests.cs
    - QuestBoard.IntegrationTests/Tests/CharactersTwinAnswersTests.cs
  modified:
    - QuestBoard.Service/Views/ContactCategoryManagement/Manage.cshtml
    - QuestBoard.Service/Views/ContactCategoryManagement/Manage.Mobile.cshtml
    - QuestBoard.Service/wwwroot/css/contacts.mobile.css
    - QuestBoard.IntegrationTests/Tests/ContactCategoryContrastGuardTests.cs
    - QuestBoard.IntegrationTests/Tests/CharactersConversionGuardTests.cs
    - QuestBoard.IntegrationTests/Tests/UiFoundationLayerGuardTests.cs
decisions:
  - "Placement used: own-card (own aside card, .table-responsive, columns unchanged, no table card heading, table named by aria-label Manage Categories); the Rename label is kept (not Edit)"
  - "The phone add form keeps the class category-mgmt-add-form (the drawing's category-add-form on the phone is renamed in the sheet) so the existing phone-only fact still holds; the desktop form uses category-add-form"
  - "The empty-state sentence takes the card colour in both category sheets (the muted tone measured 4.25:1), the same override the contact details sheet carries"
  - "No focus script remains: the move redirects carry a fragment and Chromium focuses the tabindex -1 row natively (verified live on both layouts)"
metrics:
  tasks: 3
  commits: 3
status: complete
actuals:
  tokens: 11700
  tasks: 3
  commits: 3
---

# Phase 98 Plan 16: Manage Categories twins Summary

Both Manage Categories twins are converted: the desktop table scrolls inside its own card (no more sideways page scroll at 320), the phone gains its page heading, the delete question is built on the server with the same words, and each page runs on its own layered sheet.

## What changed

- **Desktop view** (commit `dab2bcc0`): `contact-categories-page` wrapper (no `contacts-page`); title card with `h1` Manage Categories and the add form (`btn-primary` Add Category); the table in `.table-responsive` inside a `modern-card--aside` section, `aria-label="Manage Categories"`, headers Order, Name, Contacts, Actions unchanged; widths are classes (`category-col-order` 100px, `category-col-contacts` 112px, `category-col-name` min 192px, actions nowrap); reorder buttons `btn-secondary btn-sm` with the existing group `aria-label` and `Move {name} up/down` names; Rename `btn-warning btn-sm`; Delete `btn-danger btn-sm`; `.empty-state` with the verbatim copy; Back as `btn-secondary`. Inline script and both inline widths are gone (grep: `<script>` 0, `style=` 0, was 2).
- **Phone view** (commit `352785c9`): title card with `h1` Manage Categories (the heading the phone lacked), add form with the label inheriting the card colour, `category-mgmt-row` slips on the scrim with the reorder pair and Rename and Delete controls (the shell's touch floor keeps them at 44px), the same empty state, Back as a full-width `btn-secondary`.
- **Confirmation template** (both views, Razor-encoded into `data-confirm-message`, shown by `confirm-submit.js`): count above zero gives `Delete "{name}"? This will move {n} contact(s) to Ungrouped.` (singular for one), otherwise `Delete "{name}"? It has no contacts to move.` Facts compare the decoded attribute with the old script's template for 0, 1 and 3 contacts on the desktop and the phone, and for a hostile name (the markup never leaves the attribute). Live: the dialog text for the long-name category with no contacts matches on both layouts, dismissed with no POST sent.
- **Rules moved out of the contacts sheets:** from `contacts.mobile.css` the add-form label rule (a literal colour with a text shadow and an important flag), the row rule and the reorder-button 44px rule (all `category-mgmt-*`; grep `category-mgmt` there now prints 0). From `contacts.css` nothing moved: the only `.contacts-page` rules Manage used (`.text-center` icon, heading and muted-text colours) are still used by the Contacts list's own empty state, which converts in a later slice, so they stay.
- **Sheets:** `contact-categories.css` (desktop) and `contact-categories.mobile.css` (phone, no media query), each one `@layer pages` block, tokens only, no important flag; both appended to the guard lists (`LayeredFiles`, `NoLiteralFiles`, `ConvertedSheets`, `PageOnlySheets`), both views to `ConvertedViews` and `ViewSheetLinks`, every changed file to `PhaseSourceFiles`.

## Tests (none deleted; rewrite logged in tests-renamed.md)

- **Rewritten, same name:** `ContactCategoryContrastGuardTests.ContactCategoryContrastGuard_MobileAddFormLabelRule_SetsParchmentColourAndShadow` now asserts the same intent without a literal: the phone label names no rule of its own (no per-form label rule in either phone sheet, any colour is a token), the card sets `color: var(--qb-fg)` once, and that token holds 4.5:1 on the card surface.
- **New:** `CategoryManageRenderTests` (10 facts: desktop title, container, headers, no inline code, widths as classes, roles, confirm sentences for 0/1/3, hostile name, reorder names, empty state; phone heading, rows, form, sentences equal to the desktop, roles and names, empty state), `CharactersTwinAnswersTests.PhoneManage_HasThePageHeading`, and `ContactCategoryContrastGuard_ManageEmptySentence_TakesTheCardColourOnBothSheets`.
- **Suites:** unit 1416 passed, 0 failed, 0 skipped (1416 total); integration 2175 passed, 0 failed, 16 skipped (2191 total), up from 2163 passed. `TestHostFileWatching.cs` and the controllers are untouched (`git diff` against the plan start is empty for both).

## Sweep against BEFORE

- `cat-manage` and `cat-manage-empty` under both User-Agents at every width: 9 captures, 0 not 200. `readability-compare.py` against BEFORE, run with `--zero-contrast` for both rows: 9 compared, 0 failing rows, 0 poster-glyph violations, 0 residual contrast, `ok`.
- **320px desktop, `cat-manage`:** document scroll width 393 before, 320 after (viewport 320, overflow false); the table container scrolls inside its card (scroll width 544 against client width 246). At 1440 the container does not scroll (1246 of 1246). The phone is 390 and 412 wide with no overflow, and the six phone contrast failures BEFORE had are gone.
- `allow.json` records two intended changes: the phone main region's name now starts with the page title, and at 320 desktop the Rename and Delete controls sit off the right edge inside the scroll container (reachable by scrolling the card) instead of widening the page.
- **Look check** (`lookcheck.json`, `--row cat-manage`): desktop against `quest-details.html` 22 properties, phone against `quest-details.mobile.html` 23 properties, each with the two intended differences (glass hover value, button border), pass true both.
- I viewed the desktop, phone and 320 captures: gold Cinzel title on the title band with the add form beneath, the paper table with the wood header in its own glass card, reorder, Rename and Delete controls on the right; on the phone the slips on the scrim with full-width 44px-high controls and the page title. They match the approved drawings.
- Live probe (`tools/manage-live-98-16.mjs`): one `h1` on both layouts, the confirm text above, and a fragment visit focuses the category row on desktop and phone (the old focus script was not needed).

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Empty-state sentence measured 4.25:1 on the glass card**
- **Found during:** Task 3 sweep (`p.empty-state-text` on `cat-manage-empty`, all widths; BEFORE had the same ratio on `p.text-muted`, so the renamed selector counted as new)
- **Fix:** both category sheets set `.empty-state-text` to the card colour (the override the contact details sheet carries); a guard fact pins it.
- **Files modified:** contact-categories.css, contact-categories.mobile.css, ContactCategoryContrastGuardTests.cs
- **Commit:** `544aaa06`

**2. [Rule 3 - Blocking] Tracer gate run autonomously**
- The tracer's verify (the rendered facts for the desktop page, 45 related tests) was re-run and passed before the phone work started; no human checkpoint was raised because the orchestrator drives this plan unattended (the config's auto flags read false, but the instruction was to execute the plan completely).

### Plan adjustments

- The plan said to remove rules from `contacts.css` that only the desktop Manage used; none exist (see above), so `contacts.css` is unchanged.
- The phone add form keeps `category-mgmt-add-form` (the drawing's class name for it was renamed in the sheet) to keep the existing phone-only fact valid.

## Known limitation (out of scope)

At 320px on the desktop layout the fixed navbar wraps and covers the top of the page title (visible in the 320 capture). This is the shell issue recorded at the first gate and is not changed here.

## Known Stubs

None.

## Threat Flags

None. No controller, action, field name or antiforgery token changed (T-98-40); the controller's sixteen category facts stay green. The hostile-name fact and the existing script-escaping fact show the confirmation attribute is encoded (T-98-39).

## Self-Check: PASSED

- Both views and both sheets exist; commits `dab2bcc0`, `352785c9` and `544aaa06` exist.
- `<script>` and `style=` greps print 0 on the desktop view; `category-mgmt` prints 0 in `contacts.mobile.css`; `legacy-collision.py` prints nothing for either view.
- Report, sweep captures, `allow.json` and `lookcheck.json` exist under `after/98-16/`.
