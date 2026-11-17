---
phase: 99-shop-and-shop-management-ui-conversion
plan: 12
subsystem: shop-ui
tags: [shop, management, create-form, phone-forms, form-contract, es-module, guards]
requires: ["99-11"]
provides:
  - desktop ShopManagement/Create on the form contract and shop-item-form.js
  - phone Create.Mobile and Edit.Mobile on the same contract and module (no price helpers, no Preview)
  - shop-management-create.mobile.css and shop-management-edit.mobile.css retired
affects: [99-13]
tech-stack:
  added: []
  patterns: [all three item forms bind through form[data-shop-item-form], phone forms reuse the desktop markup minus the helper buttons, stacked action row only where the labels do not share a row at 360px]
key-files:
  created: []
  modified:
    - QuestBoard.Service/Views/ShopManagement/Create.cshtml
    - QuestBoard.Service/Views/ShopManagement/Create.Mobile.cshtml
    - QuestBoard.Service/Views/ShopManagement/Edit.Mobile.cshtml
    - QuestBoard.IntegrationTests/Tests/ShopRenderTests.cs
    - QuestBoard.IntegrationTests/Tests/ShopConversionGuardTests.cs
    - QuestBoard.IntegrationTests/Mobile/MobileViewsTests.cs
  deleted:
    - QuestBoard.Service/wwwroot/css/shop-management-create.mobile.css
    - QuestBoard.Service/wwwroot/css/shop-management-edit.mobile.css
decisions:
  - "Gate 1 item 9 (no): no client alert check runs on any item form. Logout on an empty desktop Create form is no longer intercepted."
  - "Gate 1 item 21 (no): the exit stays Cancel on Create (both layouts) and Back to Management on phone Edit; the existing recorded-answer constant pins both."
  - "Phone headings stay Add New Item and Edit Item (today's words, and the fixtures' expected phone heading); the plan text said Edit Shop Item, which is the desktop heading."
  - "The phone Edit action row uses the shared stacked fallback: at 360px and even 390px Back to Management and Save Changes cannot share a row (the commit label wraps to 44px wide). Create fits in one row (Cancel 109px, Create Item 177px at 360px) and stays one row."
  - "No phone-specific rules were needed in shop-pages.mobile.css: the shared form-contract and phone shell rules already give 44px full-width actions and a single column, so the phone forms link that sheet but add nothing to it."
status: complete
actuals:
  tokens: 20000
  tasks: 3
  commits: 4
---

# Phase 99 Plan 12: Create and phone forms Summary

All three remaining item forms (desktop Create, phone Create, phone Edit) now run on the form contract and the one shared module that desktop Edit proved; the pasted pricing scripts, three style blocks and two phone sheets are gone.

## What changed

- **Desktop Create.** `.shop-form.form-page` card with an h1, fields in today's order, required markers (aria-hidden asterisk plus a visually hidden " (required)") on Item Name, Item Type, Description, Rarity and Price, validation spans from the tag helper (the static `text-danger` is gone, so a field in error gets `field-validation-error` and the `input-validation-error` danger border), the price group with the two helper buttons on `data-random-price` / `data-calculated-price` (disabled until a rarity is chosen), the Availability Window h2 and `data-availability-toggle`, then `<hr>` and a `.form-actions` row with Cancel, Preview (`btn-info`, `fa-eye`, no behaviour until the Preview slice) and Create Item (`btn-primary`). Theme set, `shop-pages.css` and `shop-form.css` linked, module loaded. The style block (including the dead legendary-glow copy and the dead `#item-preview` rule), the inline script and every on-event attribute are removed. Create keeps its own longer starting hint; the module captures whatever the server wrote and restores it.
- **Phone Create and Edit.** Same card, rows and markers as desktop, in one column, with a plain price input (no helper buttons, deliberate), no Preview, and the module tolerating the absent buttons. Both start the hint on "Select a rarity to see pricing suggestions." Phone Edit's status block is the shared info alert with the shared badge (Draft reads Pending Review) and a readable "Item ID: #n" line in the alert's own colour; the availability checkbox label inherits the card colour. Create's action row is one row (Cancel left, Create Item filling the rest, both 44px); Edit's uses the stacked fallback with Save Changes on top.
- **Retired.** `shop-management-create.mobile.css` and `shop-management-edit.mobile.css` are deleted and no view or test references them. Their colour, text-shadow, glow and `!important` rules are not translated (the foundation form rules replace them). The deferred help-text and small-text literal rules go with them.
- **Guards.** `ConvertedViews` and `ViewSheetLinks` include Create, Create.Mobile and Edit.Mobile; `RetiredFiles` includes the two phone sheets; `PhaseSourceFiles` lists the touched views.

## Logout and validation against BEFORE

- **Logout.** BEFORE: clicking Logout on an empty desktop Create form raised "Please fill in all required fields." and the Logout request was never sent (the page's check bound to the first form on the page, which is the navbar's Logout form); AFTER: no alert, the Logout request is sent (aborted by the probe), `logoutIntercepted` is false.
- **Item form validation (Gate 1 item 9, no).** BEFORE: the item form itself never showed a client alert (its check was on the wrong form), so a 19-character description simply posted and the server validated; AFTER: still no client alert on the item form (`itemFormAlertText` null, POST sent and aborted), the server stays the only check and an invalid post re-renders each message on its field (a render test covers both layouts, and that nothing is saved).

## Gate 1 answers applied

- Item 9 `clientValidation.bind = false`: no client check.
- Item 21 `backLabel.rename = false`: Cancel (Create, both layouts) and Back to Management (phone Edit) unchanged.
- Item 18 `requiredMarks.accept = true`: markers on all five fields on all three views.
- Item 4: Preview on Create only, desktop only, no behaviour yet.
- A8 (stack phone actions where the label does not fit): applied to phone Edit only, by measurement.

## Verification

- Unit tests: 1416 passed, 0 failed.
- Integration tests: 2450 passed, 0 failed, 16 skipped (SQL Server migrator tests) in the full run; the final commit adds two facts (an invalid Create post on each layout), and the ShopRenderTests and ShopConversionGuardTests classes re-ran green afterwards (124 passed).
- Sweep on 8320, `mgmt-create`, `mgmt-create-errors`, `mgmt-edit-draft`, `mgmt-edit-published`, `mgmt-edit-denied`, both User-Agents, 20 captures, all 200. `readability-compare.py` against BEFORE with `after/99-12/allow.json` (per-User-Agent entries) reports `ok`: the only differences are the accessible-name renames from the required marker and the shared status word.
- Browser probes on 8320 (POSTs aborted, nothing saved): desktop Create hint and helpers for all five rarities (calculated 100/500/5000/50000/200000, random inside each range, Equipment clears the hint, switching back restores it), availability toggle enables and clears the dates, no page errors; phone Create at 320, 360 and 390px has no horizontal scroll, Edit's stacked buttons are 44px high and full width. `shop-behaviour.mjs`: `logoutBinding.logoutIntercepted` false.
- Scope: controllers, `CreateShopItemViewModel` and `EditShopItemViewModel` are unchanged against the plan start (empty diff).

## Deviations from Plan

**1. [Rule 3 - Blocking] Tracer feedback gate and task boundaries.** The run is not in auto mode and a sequential executor cannot be resumed after a checkpoint, so the tracer was not stopped for a human. The desktop Create behaviour facts were written first and observed failing (4 red), the view was written, the tracer was committed, and the working slice was then proven in a real browser (helpers, hints, availability) before the phone views were started. The phone facts were written after the phone views, so their red state was not observed.

**2. [Rule 3 - Blocking] Probe tool fixes.** `shop-behaviour.mjs` clicked the old `onclick="previewItem()"` button and then filled the form on the page that Logout had navigated away from. It now finds Preview by its text and reloads Create after the Logout click. Both are scratch-tool changes outside the repository; the BEFORE data is unaffected.

**3. [Rule 1 - Heading] Phone heading words.** The plan said `Edit Shop Item` for the phone Edit h1; today's phone word, the fixtures' expected phone heading and the approved drawing say `Edit Item`, and no copy rename was approved, so the phone keeps `Edit Item`.

**4. Plan file list.** `shop.css`, `legacy-pages.css` and `shop-pages.mobile.css` needed no change: `legacy-collision.py` reported no colliding rule for any of the three views, and the phone forms need no phone-only rules. `shop-form.css` gained no Preview-dialog rules (the dialog belongs to the Preview slice). The module and `Create`'s native `required` on Price are unchanged.

**5. [Rule 2 - Missing coverage] Extra test.** An integration fact posts an invalid Create form on both layouts and asserts the message sentence on its field, the `input-validation-error` border class and that nothing is saved (the sweep's empty-submit action cannot show server messages because native `required` stops the submit first, as it did before).

## Tests renamed

Two `MobileViewsTests` facts keep their names and intent with new assertions (phone layout, a form with `data-shop-item-form`, `shop-form.css`, `shop-pages.mobile.css`); recorded in `tests-renamed.md`. None deleted.

## Known Stubs

Desktop Create's Preview button has no behaviour yet (as before, where it called an undefined function); the Preview slice wires it.

## Threat Flags

None. T-99-58 mitigated (the module binds only `form[data-shop-item-form]`; the probe shows Logout proceeds on an empty Create form). T-99-59 mitigated (all three forms keep `asp-action`, so the antiforgery token is emitted; the invalid-post fact reads the token from the rendered Create form on both layouts and posts it).

## Self-Check: PASSED

Present: the three views (no style element, inline script or on-event attribute), `after/99-12/` (report.json, allow.json, behaviour.json, probe-pricing-create.json, captures). The two phone sheets are gone. Commits found: d1fff6ea, 94a9904e, 9b0a1e81. Both suites green; port 8320 stopped, 8321 left running.
