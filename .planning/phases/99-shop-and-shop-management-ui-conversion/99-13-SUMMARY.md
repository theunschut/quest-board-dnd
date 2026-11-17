---
phase: 99-shop-and-shop-management-ui-conversion
plan: 13
subsystem: shop-ui
tags: [shop, management, create-form, preview-dialog, es-module, guards]
requires: ["99-12"]
provides:
  - desktop ShopManagement/Create Preview button opens a live card preview dialog
  - shop-item-preview.js (card built from the form's values, excerpt from the existing Markdown preview endpoint)
  - the .shop-card block in shop-pages.css (head, name, excerpt, foot, price, stock with --unlimited/--soldout/--low, action), reused by the Shop/Index slice
affects: [99-14]
tech-stack:
  added: []
  patterns: [dialog card cloned from a server-rendered template and filled with textContent and classList, server answer parsed into an inert document and read as text only, only the latest answer applied]
key-files:
  created:
    - QuestBoard.Service/wwwroot/js/shop-item-preview.js
  modified:
    - QuestBoard.Service/Views/ShopManagement/Create.cshtml
    - QuestBoard.Service/wwwroot/css/shop-pages.css
    - QuestBoard.Service/wwwroot/css/shop-form.css
    - QuestBoard.IntegrationTests/Tests/ShopRenderTests.cs
    - QuestBoard.IntegrationTests/Tests/ShopConversionGuardTests.cs
    - QuestBoard.IntegrationTests/Tests/KeyboardScriptGuardTests.cs
decisions:
  - "Gate 1 item 4 (no-edit): Preview on Create only. Desktop Edit renders no Preview button, dialog or module; a recorded-answer constant (PreviewOnEdit = false) in ShopRenderTests pins it, and the phone Create keeps no Preview."
  - "The replica uses the new card classes (modern-card shop-card), never the item-card hook, so the old global shop sheet cannot style it."
  - "A blank rarity removes the chip from the replica (Gate 1 A9: blank Preview hides the chip); a blank name reads Unnamed item and a blank description No description yet."
  - "The Preview button gained id previewButton (the focus-return target); the readability compare records the one control-key rename."
status: complete
actuals:
  tokens: 5200
  tasks: 3
  commits: 3
---

# Phase 99 Plan 13: Preview Summary

The dead Preview button on desktop Create now opens a Bootstrap dialog holding the card a shopper would see, built in the browser from the form's current values; the excerpt comes from the existing Markdown preview endpoint, nothing is saved and no endpoint was added.

## What changed

- **Create.cshtml.** The Preview button carries `data-bs-toggle="modal"`, `data-bs-target="#itemPreviewModal"` and `id="previewButton"`. After the form card sits the dialog: `modal fade`, `modal-dialog-centered`, `aria-labelledby="itemPreviewTitle"`, an `h2` "Item Preview", a named Close, the note "This is how the card will look in the shop. Nothing is saved until you create the item.", an empty `data-preview-host`, a footer Close (`btn-secondary`), and a `template[data-preview-card]` holding the card (`modern-card shop-card`, a chip, name, excerpt, price and stock slots, and a non-interactive `span.btn.btn-primary.shop-card__action` with `aria-hidden="true"`). The module loads as an ES module in the Scripts section. No inline script, style or on-event attribute.
- **shop-item-preview.js.** On `show.bs.modal` it reads name, rarity, price, quantity and description (the Markdown editor's value read inside the handler, falling back to the textarea), clones the template into the host (replaceChildren), fills slots with `textContent`, sets the chip's rarity class, applies the four stock rules (-1 Unlimited, none left Sold Out, 1 to 5 `N left`, 6 or more `N in stock`) and shows the raw-text excerpt at once. It then posts the description to `/markdown/preview` with the page's token, parses the answer with `DOMParser`, joins the top-level blocks' text with spaces, cuts at 120 characters with `...` and swaps it in when the answer belongs to the latest opening; a failed request keeps the raw text. It never submits the item form and makes no other request. No `innerHTML`, `outerHTML`, `insertAdjacentHTML` or `document.write`.
- **CSS.** `.shop-card` block with `__head`, `__name`, `__excerpt`, `__foot`, `__price`, `__stock` (`--unlimited`, `--soldout`, `--low`) and `__action` added to the pages layer of `shop-pages.css` for the Shop/Index slice to reuse; `.preview-note` and `.preview-card` (replica width 23rem, controls inert) added to `shop-form.css`. Tokens only, no `!important`, no `aspect-ratio` or `object-fit`.
- **Guards and facts.** Seven new render facts (button data attributes, dialog structure, template classes and no live control, module load, no Preview on the phone, Edit per the recorded answer on two statuses), two keyboard-guard facts (`ShopPreviewModule_BuildsTheCardFromATemplate`, `ShopPreviewModule_UsesTheStockWordsAndRulesOfTheRealCard`), and `shop-item-preview.js` appended to `ShopModules` and `PhaseSourceFiles`. The existing Create action-row fact and the no-old-preview-rule fact were adjusted to the new button markup and the module's file name.

## Gate 1 answers applied

- Item 4 (`no-edit`): Preview on Create only. **Edit has no Preview** (`itemPreviewModal` count on Edit.cshtml is 0).
- A9: a blank rarity hides the chip in the replica.
- Approved drawing: `mockups/v1/preview/` (`preview.version` v1); the legendary chip is amber (item 16), carried by the existing `.badge-rarity.rarity-legendary` rule.

## Verification

- Unit tests: 1416 passed, 0 failed.
- Integration tests: 2461 passed, 0 failed, 16 skipped (SQL Server migrator tests).
- `preview-probe.mjs` on 8320 (`after/99-13/preview.json`), every POST except the Markdown preview aborted:
  - **Filled** (the long-name fixture, Legendary, price 1500.5, quantity 3, a Markdown description): name is the long name in full, chip classes `badge badge-rarity rarity-legendary` with the word `Legendary`, price `1500.50 gp`, stock `3 left` (`shop-card__stock--low`), Purchase `aria-hidden="true"`, no live control in the replica.
  - **Excerpt before the endpoint answered** (raw text): `# Frostmarket Lantern Oil A **warm** lantern oil sold by the board's merchants. It burns clean in wind and rain, and it ...`
  - **Excerpt after** (server plain text): `Frostmarket Lantern Oil A warm lantern oil sold by the board's merchants. It burns clean in wind and rain, and it carrie...`
  - **Blank form**: `Unnamed item`, `No description yet.`, no chip, `0.00 gp`, `1 left` (the form's own defaults), and no preview request was sent.
  - **Focus**: enters the dialog; Tab and Shift+Tab stay inside (the document body is the only wrap stop, as drawn); Esc and the footer Close both return focus to Preview.
  - **Latest answer only**: with a slow first request and a quick second, the second's text is shown. **Failure**: a 500 keeps the raw text.
  - **Requests seen**: the only non-static requests after load were `POST /markdown/preview` (5 across the whole probe); `otherRequests` is empty, no static request appeared after opening, `savePosted` is false, no page errors.
- Sweep on 8320: `mgmt-create` and `mgmt-create-preview` (desktop, 1440 and 320), 6 captures, all 200, no action or assert failure, no overflow, no console or page error, 0 contrast failures over 56 tested nodes in the open dialog. `readability-compare.py` against BEFORE with `after/99-13/allow.json` reports `ok`; the one difference added for this plan is the Preview button's control key gaining its id.
- Scope: `git diff` of `QuestBoard.Service/Controllers` against the plan start is empty (no controller, route or endpoint change).

## Deviations from Plan

**1. [Rule 3 - Blocking] Tracer feedback gate and task boundaries.** The run is not in auto mode and a sequential executor cannot be resumed after a checkpoint, so the tracer was not stopped for a human. The facts were written first and observed failing (5 red), the tracer was committed, and the working slice was then proven in a real browser (probe above) before the guard facts and the full suites.

**2. [Rule 3 - Blocking] Scratch-tool changes.** The manifest row `mgmt-create-preview` had no actions; it now fills the form (including the Markdown editor, through a new `fillEditor` sweep action) and opens Preview, with two asserts. Both tools live outside the repository.

**3. Existing facts adjusted.** The Create action-row fact now allows the data attributes on the Preview button, and the "no old preview rule" checks look for `id="item-preview"` and `#item-preview` rather than the substring, which the new module's file name contains.

**4. Read of the type field dropped.** The plan lists the type among the values read; the card shows no type, so the module does not read it.

## Known Stubs

None. The Preview button is fully wired.

## Threat Flags

None. T-99-31 mitigated (values written with `textContent`; the server HTML is parsed into a detached document and only its text is read; the guard facts forbid markup-from-strings). T-99-32 mitigated (the request carries the page's verification token header; the controller is unchanged). T-99-33 mitigated (the replica holds no link, form or live button; the probe saw no POST to ShopManagement).

## Self-Check: PASSED

Present: `shop-item-preview.js`, the dialog and template in `Create.cshtml`, `after/99-13/` (preview.json, report.json, allow.json, two screenshots, sweep captures), `tools/preview-probe.mjs`. Commits found: 59c0d17c, e0c8edc5. Both suites green; port 8320 stopped, 8321 left running.
