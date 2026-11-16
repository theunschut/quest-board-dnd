---
phase: 99-shop-and-shop-management-ui-conversion
plan: 10
subsystem: shop-ui
tags: [shop, item-details, emblem-tile, wall-clock, whole-picture, pages-layer, razor, guards]
requires: ["99-09"]
provides:
  - _ShopItemDetailsContent as the single details body for the dialog and the page on both layouts
  - emblem tile (144px desktop, 120px phone, whole at any text size) in place of the grey 250px block
  - shop-pages.mobile.css (layered phone sheet); shop-details.mobile.css retired
  - wall-clock availability dates, heading levels by context, filled info reference link with rel noopener noreferrer
  - guards for inline code, sheet links, retired file, raw dates, the whole-image rule and the tile
  - whole-picture.mjs live mode (--row, --dialog) and lookcheck-99.mjs live mode
affects: [99-11, 99-12, 99-19]
tech-stack:
  added: []
  patterns: [one partial for four renderings, tile sized by a minimum only, scanners with self-tests as guards]
key-files:
  created:
    - QuestBoard.Service/wwwroot/css/shop-pages.mobile.css
  modified:
    - QuestBoard.Service/Views/Shared/_ShopItemDetailsContent.cshtml
    - QuestBoard.Service/Views/Shop/Details.cshtml
    - QuestBoard.Service/Views/Shop/Details.Mobile.cshtml
    - QuestBoard.Service/Views/Shop/Index.Mobile.cshtml
    - QuestBoard.Service/wwwroot/css/shop-pages.css
    - QuestBoard.IntegrationTests/Tests/ShopRenderTests.cs
    - QuestBoard.IntegrationTests/Tests/ShopConversionGuardTests.cs
    - QuestBoard.IntegrationTests/Tests/UiFoundationLayerGuardTests.cs
    - QuestBoard.IntegrationTests/Mobile/MobileViewsTests.cs
  deleted:
    - QuestBoard.Service/wwwroot/css/shop-details.mobile.css
decisions:
  - "Details.Mobile renders the desktop partial inside its own wrapper (title card on the page, item-details__head in the dialog, Back to Shop kept in both because phoneDetails.dropDialogBack is false)"
  - "The Denied and Archived sentence 'This item is not currently available for purchase.' is shown (deniedArchivedNotice.show is true); the test reads the answer as a constant"
  - "The Item Properties list holds the type chip and the rarity chip, so a details body now shows two rarity chips (beside the emblem and in the list), as the approved drawing does"
  - "Sell to Shop is a centred section under a rule, not a card, as the approved drawing draws it; the other three sections are modern-card--aside"
status: complete
actuals:
  tokens: 16000
  tasks: 3
  commits: 3
---

# Phase 99 Plan 10: Details Body Summary

The item details are now one converted body for the dialog and the page on both layouts: an emblem tile instead of the grey block, wall-clock availability dates, sections that are real headings, and a phone page that gains everything the desktop page had (Sell to Shop, Availability, Item Properties, the D&D Beyond link, the notices and the stock max).

## What changed

- **One body, four renderings.** `_ShopItemDetailsContent` holds everything; `Details.cshtml` and `Details.Mobile.cshtml` are wrappers (title card with the h1 on the page, `item-details__head` with an h3 in the dialog). The phone page previously showed only name, description, price and a bare quantity field; it now shows the same sections as desktop, including Sell to Shop with its confirmation, the draft-review notice, the out-of-stock alert, the sign-in branch, and `Max: N available` or `Unlimited stock available` under the quantity (the phone field is now `quantity` like the desktop one).
- **Emblem tile.** `div.emblem.emblem--{rarity}` holding an aria-hidden type icon (`fa-hammer`, `fa-magic`, `fa-scroll`, `fa-box`) from `ShopItemDisplay.TypeIcon`. Sized by `min-block-size` only (144px; 120px with a 48px glyph on the phone), flex centred, scrim fill, 2px rarity border, glyph in the rarity `-text` token. No aspect-ratio, object-fit, overflow, literal height or style attribute; a guard pins that.
- **Dates.** The three availability lines go through `Html.WallClock` (`date-compact` then `date`), reading Nov 5. `HtmlHelperExtensions` and the controllers are untouched; no raw date format remains in the shop views.
- **Headings and controls.** Page: item name h1, sections h2. Dialog: item name and sections h3 (no h1, h2, h5 or h6). Reference link is `btn btn-info` with `target="_blank" rel="noopener noreferrer"`; Purchase Item `btn-primary`; Sell to Shop `btn-warning`; the page's Back to Shop `btn-secondary` with the foundation two-tone ring (recorded: 2px dark outline at 2px offset plus a 2px gold inner stroke).
- **Image rule.** `.item-details-content .markdown-content img { max-inline-size: 100%; block-size: auto; }`, pinned by `ShopSheets_HoldTheWholeImageRule`.
- **Status matrix.** Shop/Details answers 200 for Draft, Denied and Archived for the editing Dungeon Master, page and dialog, both User-Agents (tested). No controller change was needed.
- **Sheets.** `shop-pages.css` grew the details and tile rules from the approved v1 draft; `shop-pages.mobile.css` (one `@layer pages` block, tokens only) holds the 120px tile, 44px inputs and buttons and the full-width Back to Shop; `Shop/Index.Mobile.cshtml` links it because it hosts the phone dialog. `shop-details.mobile.css` is deleted. `legacy-collision.py` lists nothing for the partial or either Details view, so no old unlayered rule needed deleting (the only old rules touching these classes are `#denyModal`/`#addCharacterModal`-scoped).

## Gate 1 answers applied

- `emblem`: approved v1, 144px desktop, 120px phone, existing icon map.
- `wallClockDates.accept`: availability dates read Nov 5 (the one visible wording change).
- `deniedArchivedNotice.show` true: Denied and Archived items say 'This item is not currently available for purchase.' in the purchase area.
- `phoneDetails`: `sharedPartial` true; `dropDialogBack` false, so the phone dialog keeps its Back to Shop.
- Legendary tile and chip use the amber `--qb-warning` family, as 99-08 recorded.

## Whole-picture results

`whole-picture.mjs` gained a live mode (`--row <id>` for a page, `--dialog <id>` for the details dialog opened from the shop grid; paging until the card appears). 16 records in `~/qb-ui-review/phase-99/after/99-10/whole-*.json`: `details-wideimage` and `details-longname`, each as page and as dialog, desktop and phone, with and without the text-spacing stress.

- Every glyph inside its tile (16 of 16); tiles 144px tall on desktop and 120px on the phone at their minimum, `overflow` visible, `aspect-ratio` auto, `object-fit` fill, no style attribute, even with text spacing.
- The wide image (1600px natural) renders at the container width and keeps its ratio everywhere: page 1196px (desktop) and 296px (phone), dialog 716px and 312px; never wider than its container.
- No horizontal scroll in any record. The 67-character name wraps (overflow-wrap) in the title and the dialog head on both layouts.
- Note: the dialog shell on `Shop/Index` is not this plan's file, and its body is not `modal-dialog-scrollable` today, so a tall dialog is taller than the viewport and the modal overlay scrolls it (nothing is clipped). Making the dialog body scroll itself belongs with the Index conversion.

## Look check and focus ring

`lookcheck-99.mjs` gained a live mode (`--live --port N --out file`). On the live pages: `look-details-desktop` 33 rows, 0 different; `look-details-phone` 29 rows, 1 different, the intended `phone-navy`. The two dialog frames differ only because the host `/Shop` page is still the oak, unthemed shell with its unconverted item cards (backdrop, card, overlay and the commit button's blue versus brown); nothing inside the details body differs. That closes with the Index conversion, not here.

## Dialog date note

`site.js` re-words wall-clock dates only on load, so dates in the dialog (fetched after load) keep the server wording (Nov 5). On a full page the client re-formats them in the viewer's locale (the sweep shows `15 Nov`). Accepted, as the plan states.

## Tests renamed

Recorded in `~/qb-ui-review/phase-99/tests-renamed.md`:
- `MobileViewsTests.GetMobilePage_ShopDetails_ReturnsSuccessAndMobileLayout`: same name, asserts `mobile-layout`, the item name, `item-details-content` and both sheets instead of the old glass card class and `shop-details.mobile.css`.
- `ShopRenderTests.ShopDetails_PageAndDialog_RenderOneChipForTheItemsRarity` became `..._RenderTheItemsRarityChipBesideTheEmblemAndInTheProperties` (two chips; phone dialog case added); `ShopDetails_VeryRareItem_...` expects the two chips.

New facts: sections and stock max on both layouts, unlimited, draft, sold-out, emblem tile with no style attribute and the glyph per type, wall-clock window lines (full, from-only, until-only), the bare item, the status matrix 200, the Denied and Archived notice, heading levels, button and link roles, the phone dialog's Back to Shop; guards for inline code, sheet links, retired file, raw dates, the whole-image rule and tile declarations, each scanner with a self-test.

## Verification

- Unit tests: 1416 passed, 0 failed.
- Integration tests: 2428 passed, 0 failed, 16 skipped (SQL Server migrator tests), total 2444 (2396 before; +32 new facts). Run again after the last CSS commit.
- Sweep on 8320 of the 15 rows (`details-*`, `shop-modal`, `shop-modal-wizard`), both User-Agents, 60 captures, all 200: `readability-compare.py` against BEFORE with `after/99-10/allow.json` reports 0 failing rows, `ok`.
- Scope: `HtmlHelperExtensions.cs` and the controllers unchanged (scope-ok); nothing was posted; no purchase was made (the fresh-purchase tool reported 18 purchases younger than 20 hours and made none).

## Deviations from Plan

**1. [Rule 3 - Blocking] Tracer feedback gate met by an automated probe**
- The run is not in auto mode and a sequential executor cannot be resumed after a checkpoint, so as in earlier slices the committed tracer was swept on 8320 (4 rows, 16 captures, `after/99-10/tracer/`) and the screenshots read before the second task.

**2. [Rule 1 - Bug] Muted label colours missed 4.5:1**
- **Found during:** Task 3 sweep. The property labels and the hint and window lines used `--qb-fg-muted` (4.1:1 at the lightest board pixel); the window line was a new contrast failure against BEFORE. **Fix:** they use the card text colour. **Commit:** 7ac246f0.

**3. [Rule 3 - Blocking] The guard lists the plan names did not exist in `ShopConversionGuardTests`**
- `ConvertedViews`, `ViewSheetLinks` and `RetiredFiles` live in the other area guards; they were created in the shop guard (with `Shop/Index` and `Index.Mobile` in `ViewSheetLinks` since they host the dialog). `MobileViewsTests.cs` was added to `PhaseSourceFiles` and passes the planning-reference scan.

**4. [Rule 3 - Blocking] `lookcheck.mjs` could not run live against the shop**
- Its selector map file is absent and it targets the quest Direction A pages, so the live look check was added to `lookcheck-99.mjs`, the tool that produced the signed-off shop look records.

**5. Process note.** The behaviour facts were written after the implementation in the same slice rather than being run red first; so their red state against the old views was not observed; each assertion names markup the old views did not render.

## Known Stubs

None.

## Threat Flags

None. T-99-23 mitigated (`Html.Markdown` unchanged, images held to their column by the image rule); T-99-24 mitigated (`rel="noopener noreferrer"`, pinned by a render test); T-99-25 and T-99-26 accepted as planned.

## Self-Check: PASSED

Present: `shop-pages.mobile.css`, `_ShopItemDetailsContent.cshtml`, `Details.cshtml`, `Details.Mobile.cshtml`; `shop-details.mobile.css` absent; `after/99-10/` (report.json, allow.json, whole-*.json, lookcheck-live.json, tracer, focus-back-to-shop). Commits found: cc4c935d, 21ce7431, 7ac246f0. Both suites green; port 8320 stopped.
