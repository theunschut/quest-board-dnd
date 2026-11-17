---
phase: 99-shop-and-shop-management-ui-conversion
plan: 11
subsystem: shop-ui
tags: [shop, management, edit-form, form-contract, es-module, pricing-guide, view-details, pages-layer, guards]
requires: ["99-10"]
provides:
  - desktop ShopManagement/Edit on the form contract (.form-page, required markers, .form-actions, status alert)
  - shop-item-form.js, the shared item form module bound by data attributes (pricing guide, price buttons, availability toggle)
  - shop-form.css (one pages-layer block, tokens only)
  - a working View Details link (/Shop/Details/{id}) for Draft, Denied and Archived items
affects: [99-12]
tech-stack:
  added: []
  patterns: [module opts in by data-shop-item-form and reaches only that form, hint built with textContent and a created icon, recorded-answer constants pinned by tests]
key-files:
  created:
    - QuestBoard.Service/wwwroot/js/shop-item-form.js
    - QuestBoard.Service/wwwroot/css/shop-form.css
  modified:
    - QuestBoard.Service/Views/ShopManagement/Edit.cshtml
    - QuestBoard.Service/Views/Shared/_MarkdownEditor.cshtml
    - QuestBoard.IntegrationTests/Tests/ShopRenderTests.cs
    - QuestBoard.IntegrationTests/Tests/ShopConversionGuardTests.cs
    - QuestBoard.IntegrationTests/Tests/KeyboardScriptGuardTests.cs
    - QuestBoard.IntegrationTests/Tests/UiFoundationLayerGuardTests.cs
decisions:
  - "Gate 1 item 9 (no): shop-item-form.js adds no client alert check at all; the old document-level form lookup only ever found the navigation Logout form, so the item form never had one. A guard pins the answer as a constant."
  - "Gate 1 item 21 (no): the back control stays 'Back to Management'; a render test pins the answer as a constant."
  - "The module binds only form[data-shop-item-form]; a guard rejects any form lookup by position or on the document."
  - "The hint's empty-rarity text is whatever the server rendered (Edit: 'Select a rarity to see pricing suggestions.'), captured on load and restored, so Create keeps its own longer line without the module holding either."
  - "View Details stays on desktop Edit only; the phone Edit gains no such control (unchanged and deliberate)."
status: complete
actuals:
  tokens: 16000
  tasks: 3
  commits: 3
---

# Phase 99 Plan 11: Desktop Edit Summary

Desktop shop item Edit is now a form-contract page with a View Details link that works, one pricing and availability module in place of the pasted script, required markers, and a status block in the shared vocabulary; the module is proven on this page and ready for Create and the phone forms.

## What changed

- **View Details.** The anchor now points at `/Shop/Details/{id}`; the dead `/ShopManagement/Details/` target is gone. The control stays on desktop Edit. For the editing Dungeon Master `/Shop/Details/{id}` answers 200 for Draft, Denied and Archived items (render tests, and the sweep behaviour probe: `viewDetails` `/Shop/Details/5461`, status 200). The phone Edit has no View Details control, as before.
- **Module.** `shop-item-form.js` holds the five rarity ranges (50-100, 101-500, 501-5000, 5001-50000, 50001-200000) and calculated prices (100, 500, 5000, 50000, 200000) once, in a frozen table. For each `form[data-shop-item-form]` it wires the rarity and type selects to the `#price-suggestion` hint (written with `textContent` and a created icon element, never a markup string), enables `[data-random-price]` and `[data-calculated-price]` when a rarity is chosen (and for Equipment, which clears the hint), computes the hint on load, sets the price from either helper, and toggles the two date inputs from `[data-availability-toggle]` (clearing them on uncheck). It tolerates absent price buttons (the phone has none) and an absent type select. It exposes nothing globally.
- **Hint behaviour (probed in a browser on 8320, POSTs aborted).** Computed on load for the item's rarity (Rare: "Rare items typically cost 501-5,000 gp" and "Tasha's recommended: 5000 gp"). Each of the five rarities shows its own range and recommended price, both buttons stay enabled, calculated sets 100, 500, 5000, 50000, 200000, and random landed inside the range each time (60, 122, 3413, 37492, 171991). Equipment clears the hint (0 children) and leaves both buttons enabled; switching back restores it. The availability toggle enables and clears the dates as before. No page or console errors. The initial server text is "Select a rarity to see pricing suggestions." in a `.form-text.shop-form__hint` with `id="price-suggestion"` and `aria-live="polite"`.
- **Form contract.** `.shop-form.form-page` card with an h1; Item Name, Item Type, Description, Rarity and Price carry the aria-hidden asterisk and a visually hidden " (required)" (accessible names now end in "* (required)"); validation messages come from the validation tag helper (`field-validation-error` appears on a field in error, with the shared 2px danger border and overlay chip); help text has ids and `aria-describedby`; the Draft re-approval hint and review notice are kept. Fields sit on a two-column grid (`shop-form__row`), Availability Window is an h2, and the action row is `.form-actions` with Back to Management (secondary), View Details (`btn-info`) and Save Changes (`btn-primary`).
- **Status block.** `.alert.alert-info` with an h2 Current Status, the shared status badge (Pending Review for a Draft, Available, Archived, Denied), "This item is awaiting DM approval" for a Draft, and "Item ID: #{id}".
- **Inline code gone.** The style block (with its dead legendary-glow copy), the stray closing script tag, the inline script and every `onchange`/`onclick` are removed. The view sets the shop theme and links `shop-pages.css` and `shop-form.css`. `legacy-collision.py` reports no remaining unlayered rule that can restyle the page, so `shop.css` and `legacy-pages.css` needed no edit.
- **Shared editor label.** `_MarkdownEditor.cshtml` now adds the visually hidden " (required)" after its asterisk when the field is required, so Description reads like the other required labels. It affects every form that renders a required editor (a screen-reader-only addition).

## Gate 1 answers applied

- Item 9 `clientValidation.bind = false`: no client alert check written; guard `ShopItemFormModule_BindsOnlyTheItemForm` asserts there is no `alert(`, no editor read and no position-based form lookup, and holds the explicit-yes branch ready (lazy editor read inside the handler, both alert texts) if the answer ever changes.
- Item 21 `backLabel.rename = false`: label stays "Back to Management".
- Item 18 `requiredMarks.accept = true`: markers applied.
- Item 4: no Preview on Edit (nothing added).

## Verification

- Unit tests: 1416 passed, 0 failed.
- Integration tests: 2439 passed, 0 failed, 16 skipped (SQL Server migrator tests), total 2455 (2444 before; +11 new facts and guard cases).
- Sweep on 8320, `mgmt-edit-draft`, `mgmt-edit-published`, `mgmt-edit-denied`, both User-Agents, 12 captures, all 200: `readability-compare.py` against BEFORE with `after/99-11/allow.json` reports `ok` (the draft screenshot was read: navy shop theme, status alert, markers, hint, action row).
- Scope: `git diff` of the controllers and both Create/Edit view models against the plan start is empty; nothing was posted; no purchase was made.

## Deviations from Plan

**1. [Rule 3 - Blocking] Tracer feedback gate and task boundaries**
- The run is not in auto mode and a sequential executor cannot be resumed after a checkpoint, so the tracer slice was not stopped for a human; the whole view was written in one pass (the tracer commit therefore already carries the form-contract markup and `shop-form.css`, so the link to it never 404s), then the page was swept on 8320 and the screenshots read before the final fix commit. Task 2's behaviour facts passed on first run because the view already held the markup; their red state was not observed.

**2. [Rule 2 - Missing critical functionality] `_MarkdownEditor.cshtml` edited**
- Not in the plan's file list. Description's required label could not carry the hidden "(required)" word without a change to the shared partial.

**3. [Rule 1 - Bug] Empty validation chips and a run-on hint**
- **Found during:** the sweep. Giving the message spans a static `field-validation-error` class drew a small dark chip under every field when valid; the two hint lines ran together. **Fix:** the spans carry no static class (the tag helper adds `field-validation-error` itself on error) and the hint has a column gap. **Commit:** 3189d816.

**4. [Rule 3 - Blocking] Tool change: per-User-Agent allow entries**
- `readability-compare.py` accepted one allow entry per row id, which cannot express a rename that happens on desktop only (phone keys are unchanged, so a shared rename breaks the phone lookup). It now also reads `<id>@desktop` / `<id>@mobile`, overriding the plain entry for that User-Agent. Backward compatible; noted in carry-forward.

**5. Phone rows carried contrast.** The editor label's new hidden word repeats its label's existing phone measurement (4.34:1), so the phone rows carry `span.visually-hidden` "(required)" as `contrastCarried`; the phone sheet is converted in the next slice. The phone draft status word changed from Draft to Pending Review through the shared badge (earlier plan), recorded as a rename.

**6. Plan file list.** `shop.css` and `legacy-pages.css` needed no change (no colliding rule); `shop-form.css` omits the Preview-dialog and toolbar rules of the approved draft (Create's, added in the next slice); the draft's empty "Select item type/rarity" options are not added to Edit (the model's enums are never empty).

## Known Stubs

None.

## Threat Flags

None. T-99-27 mitigated (the module reaches forms only through `form[data-shop-item-form]`, pinned by a guard); T-99-28 accepted (server validation unchanged and authoritative; no client check, per the answer); T-99-29 mitigated (the form keeps `asp-action`, so the antiforgery token is emitted; the token guard covers it); T-99-30 mitigated (hint built with `textContent` and a created icon; `ShopModules_WriteNoMarkupFromStrings` covers the module).

## Self-Check: PASSED

Present: `shop-item-form.js`, `shop-form.css`, `Edit.cshtml` (no style element, script or on-event attribute), `after/99-11/` (report.json, allow.json, behaviour.json, probe-pricing-draft.json, captures). Commits found: e359dc10, 754404b8, 3189d816. Both suites green; port 8320 stopped.
