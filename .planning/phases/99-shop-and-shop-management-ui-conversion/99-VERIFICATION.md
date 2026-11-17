---
phase: 99-shop-and-shop-management-ui-conversion
verified: 2026-11-17T18:00:00Z
status: human_needed
score: 19/24 must-haves verified
behavior_unverified: 5
overrides_applied: 0
behavior_unverified_items:
  - truth: "Sell to Shop works in the fetched details dialog and on the full Details page alike: the running total follows the quantity and the native confirmation asks before the post, also for an item name holding quotes (D-05, A-shop-017)"
    test: "On desktop and phone Shop/Index open an item's details dialog, change the Sell to Shop quantity, press Sell, answer Cancel and then OK; repeat on /Shop/Details/{id} directly, and once with an item whose name has an apostrophe."
    expected: "The total updates on every quantity change; the confirmation reads 'Are you sure you want to sell {n}x {name} to the shop for {total} gp?'; Cancel posts nothing; OK posts and lands with the success toast; no console error."
    why_human: "shop-item-sell.js is a delegated document listener and the partial carries data-sell-form, data-item-name, data-unit-price and data-sell-quantity, but no repository test runs a browser. KeyboardScriptGuardTests.ShopSellModule_ConfirmsOnSubmitAndUpdatesTheTotal reads the script's text only. The behaviour is evidenced only by the scratch records behaviour.json and click-selltoshop-{dialog,page}-{desktop,mobile}.json (confirm shown, total right, 302, no page errors)."
  - truth: "The merchant is a real, focusable button in a sticky dock clear of the footer; pressing it shows one of the same 20 quotes in a toast that stays while hovered or focused and hides itself, announced through a polite live region; the bob runs three cycles and never under reduced motion (G1-O2, D-01, D-02, D-08)"
    test: "On desktop Shop/Index Tab to 'Talk to the merchant', press Enter several times, hover the toast and Tab into its Close, scroll to the bottom of the page, then reload with the OS reduced-motion setting on."
    expected: "Only one toast at a time; the quote changes; the toast stays while hovered or focused and hides about six seconds after leaving; the button never covers the footer; a screen reader announces each quote once; the button bobs three times and stops, and does not move at all under reduced motion."
    why_human: "Toast lifetime, hover-hold, live-region announcement and animation are runtime behaviour. The markup (button, aria-label, templates, live region) and the 3-cycle animation inside prefers-reduced-motion: no-preference are present, and the 20 quotes match the baseline word for word, but the only behavioural evidence is the scratch merchant.json (holdsOnHover, hidesAfterLeave, oneToast, liveRegionMatches, clearOfFooter all true)."
  - truth: "Preview on desktop ShopManagement/Create opens a live card preview dialog built in the browser from the form's current values, saving nothing (D-03, A-shop-027)"
    test: "On desktop ShopManagement/Create fill name, rarity, description, price and stock, press Preview; then press Preview on a blank form and with a long name and a Legendary rarity; close the dialog with Escape."
    expected: "The dialog shows the Shop/Index card with the form's values (excerpt from the Markdown preview), focus moves in and returns to Preview on close, and no item is created."
    why_human: "shop-item-preview.js clones a server-rendered card template and calls the existing /markdown/preview endpoint; KeyboardScriptGuardTests pins its text. No repository test runs it; only the scratch Gate 2 frames and the 99-13 records show it working."
  - truth: "The details dialog on Shop/Index (both layouts) is filled by a module that clears the body, sets aria-busy and ignores stale responses, and the desktop My Transactions toggle opens and closes its pane (A-shop-034, Q-120 as redrawn)"
    test: "On desktop and phone Shop/Index open one item's dialog, close it quickly and open another; on desktop press My Transactions twice; on the phone open the My Transactions drawer and the Filter & Sort drawer."
    expected: "The dialog never shows the previous item's body; the pane toggles with aria-expanded following it and no hidden tab stop when closed; the drawers open, trap focus and close."
    why_human: "shop-item-modal.js, shop-history.js and shop-drawers.js are wired by data attributes and module includes; KeyboardScriptGuardTests reads their text. Request ordering and disclosure state are runtime behaviour no repository test exercises."
  - truth: "Every native confirmation and dialog that replaced an inline handler still works: Publish, Archive, Reopen and Delete on both management lists, the Sell row on the purchase history, the Deny dialog filled from data attributes, the three Bulk Actions notices, and the shared pricing module on the four item forms (D-05, A-shop-041, A-shop-042, A-shop-046)"
    test: "On both management layouts press Publish, Archive, Reopen and Delete and answer No then Yes; open Deny on a Draft and check the item name and the form target; press each Bulk Actions button; on Create and Edit (both layouts) change the rarity, press the dice and calculator buttons and toggle availability."
    expected: "No sends nothing, Yes acts; the sentences are unchanged; the Deny dialog names the right item and posts to /ShopManagement/Deny/{id}; Bulk Actions shows its old notice; the price hint, random and calculated prices and availability fields behave as before."
    why_human: "The sentences are byte-identical to the baseline in markup (checked here) and confirm-submit.js, shop-management.js and shop-item-form.js are loaded as modules, but no repository test runs the scripts."
human_verification:
  - test: "Real phone with touch: the whole shop area (Shop/Index with the My Transactions and Filter & Sort drawers, Return and Sell, the details dialog, Shop/Details, and ShopManagement Index, Create and Edit)"
    expected: "Phone layout served everywhere; controls at least 44px; nothing scrolls sideways; Return and Sell work in the drawer"
    why_human: "Real device and touch; the sweeps used an emulated Pixel 7 profile. Listed in the 99-20 Phase UAT and deferred until after deploy by the operator's standing rule"
  - test: "Screen reader: the merchant announcement (desktop) and the dialogs (details, Preview, Deny, Bulk Actions, the Sell confirmation)"
    expected: "Each quote is announced once; dialog names, headings and the confirmation are read sensibly"
    why_human: "Screen-reader output cannot be observed programmatically. Listed in the 99-20 Phase UAT"
  - test: "Production culture check of the availability date wording ('Nov 5', Gate 1 item 11, t6)"
    expected: "Availability dates on production read as accepted at Gate 1 and Gate 2"
    why_human: "Depends on the production host culture; listed in the 99-20 Phase UAT"
  - test: "Decide on the three server findings 99-22 recorded after Gate 2 and that were never put to the operator: the availability window is not enforced by id (Details, PurchaseItemAsync, SellItemToShopAsync); Archive accepts any status and Reopen publishes any archived item (a Dungeon Master can archive their own Draft by a crafted POST and reopen it as Published without review, which also makes it visible to members); a deep link to a Draft on another of the member's boards switches the board before the 404"
    expected: "Each is accepted as known debt or scheduled as a follow-up; none was introduced by this phase (ShopManagementController and the middleware are unchanged since the phase baseline)"
    why_human: "Operator decision: these are pre-existing authorization and workflow rules outside a UI conversion, recorded in deferred-items.md as 'not asked'"
  - test: "Run the five behaviour-unverified items above on the deployed build"
    expected: "As listed under behavior_unverified_items"
    why_human: "No repository test exercises these invariants"
gaps: []
---

# Phase 99: Shop and Shop management UI conversion Verification Report

**Phase Goal:** Convert the shop and shop management pages to the foundation in the navy shop theme, relocating the floating merchant button and fixing the hidden Return and Sell buttons, the dead Preview and View Details controls and the sell confirmation script.
**Verified:** 2026-11-17
**Status:** human_needed
**Re-verification:** No, initial verification
**HEAD:** 4d7c389b (phase baseline for diffs: 59df0cf8, the scratch `BASELINE_SHA`)

## Goal Achievement

The goal is achieved in the codebase. All ten shop and shop management views and the details partial (plus six new shop partials) are on the foundation, with no inline style, `<style>` block, inline script or inline event handler. The three new layered sheets (`shop-pages.css`, `shop-pages.mobile.css`, `shop-form.css`) hold no hex, no `rgb()`, no `!important` and no `infinite`. `shop.css` and the five old phone sheets are deleted, and `legacy-pages.css` is down to one six-line rule. The theme reaches `<html data-theme="shop">` through `ViewData["Theme"]` on both layouts.

The four functional breaks are fixed in code:
- Return and Sell are ordinary visible buttons with tokens.
- Sell to Shop runs from a delegated module.
- Preview opens a dialog.
- View Details points at `/Shop/Details/{id}`.

The merchant is a real button in a sticky dock. Plans 99-21 (quantity below 1 refused) and 99-22 (Draft and Denied Details answer 404 to plain members) are in place and tested. I ran build and tests myself: 0 errors, unit 1433 of 1433, integration 2595 passed with 16 skipped. What remains:
- human UAT the operator deferred until after deploy
- five behaviour-dependent truths that only the scratch browser records exercise
- an operator decision on three pre-existing server findings recorded after Gate 2

### Observable Truths

| # | Truth | Status | Evidence |
|---|-------|--------|----------|
| 1 | DoC-1: zero inline `style=`, `<style>`, inline `<script>` and inline event handlers in the area's views | VERIFIED | Grep (`style\s*=`, `<style`, `\son[a-z]+\s*=`) over `Views/Shop`, `Views/ShopManagement` and the seven `Views/Shared/_Shop*.cshtml` returns nothing. Every `<script` is `type="module" src=...`. No `confirm(`, `alert(` or `innerHTML` in any of them. `ShopConversionGuardTests` (`PhaseScope_IsFullyConverted` and the inline-code facts) passes. |
| 2 | DoC-2 and the phase extra: zero hex, `rgb()` and `!important` in the area's sheets; the ten endless animations removed or capped | VERIFIED | `shop-pages.css`, `shop-pages.mobile.css` and `shop-form.css` each have 0 hex, 0 `rgb(`, 0 `!important`, 0 `infinite`, 0 `transition: all`, and exactly one `@layer`. The only animation is `.shop-merchant { animation: merchant-bob 1.5s ease-in-out 3 }` inside `prefers-reduced-motion: no-preference` (`shop-pages.css:727-731`). No other sheet outside `tokens.css` names a shop, merchant or rarity selector. `legacy-pages.css` is 6 lines with one unflagged rule. |
| 3 | DoC-3: the area's sheets load through `@section Styles`; `shop.css` leaves the global list; the theme sits on `<html data-theme="shop">`, never on body | VERIFIED | `_Layout.cshtml` lost its `shop.css` link (diff). Grep of `_Layout*.cshtml` for `shop` is empty. Both layouts emit `data-theme` on `<html>` from `ViewData["Theme"]`, HTML-encoded. All ten shop views set `ViewData["Theme"] = "shop"`. `tokens.css:187` holds `[data-theme='shop']`. `shop.css`, `shop.mobile.css`, `shop-details.mobile.css` and the three `shop-management-*.mobile.css` are deleted. |
| 4 | DoC-4: every twin aligned and every content difference answered (Q-56 to Q-75); no merges | VERIFIED | Q-56: both Index layouts render `_ShopPurchaseHistory`. Q-58: phone Previous, Next and "Showing x–y of n items". Q-59: three empty states on both layouts. Q-61: desktop Active badge and Clear Filters. Q-63 to Q-66: `Details.Mobile` renders the shared `_ShopItemDetailsContent`, with Sell to Shop, Availability, notices, `max` and "Max: N available". Q-71: one mapping, `ShopItemDisplay` (Pending Review, Available), and no `.Replace("Published"` remains. Q-72: phone "No Items Yet" with Create First Item. Q-75: hint on both Edit views. The deliberate differences hold: Q-60 no merchant on the phone; Q-62 Buy on the phone and Purchase on desktop; Q-73 Preview on desktop Create only; Q-74 View Details on desktop Edit only. Each of the ten views has exactly one `h1`. No twin was deleted. |
| 5 | Q-120 and Q-121 relayouts re-confirmed with their AFTER | VERIFIED | `gate1/answers.json` history and filterCard items are approved on v2. The operator asked for the v2 redraw: a collapsible My Transactions pane, collapsed by default, with a drawer on the phone; and a full-width filter card. This replaces the roadmap's literal "under the item grid" (see Findings). As built, `.history-pane` is an in-flow grid column, not fixed, and stacks above the items below 768px. The filter card is a single `#shop-filter-form`. Gate 2 approved `q` (Q-120 and Q-121 as built). |
| 6 | Return and Sell are visible, keyboard-reachable buttons that post (A-shop-002) | VERIFIED | `_ShopPurchaseHistory.cshtml` renders exactly one Return (`btn-success`) or Sell (`btn-warning`) per slip. Each uses `asp-controller`/`asp-action`, so it carries a token. No `opacity: 0` or `visibility: hidden` rule names them, and no `return-sell` rule remains in any sheet. Passing tests: `HistoryRowForm_CarriesAnAntiforgeryTokenAndPostsToARedirect`, `MobileShopIndex_MobileUserAgent_RendersPurchaseHistoryWithReturnAndSell` and `ShopPostForms_EachCarryAnAntiforgeryToken`. `CanReturn` moved from inline view code to `UserTransactionViewModel`, with the same 24-hour real-instant rule as the baseline view. |
| 7 | The roadmap extra: Return and Sell verified with a purchase made through the Purchase form on the audit board | VERIFIED | Scratch `after/final/click-return-{desktop,mobile}.json` and `click-sell-{desktop,mobile}.json` record status 302, the row reading "(Fully Returned/Sold)" after Return, and the Sell sentence. They were captured 2026-11-17, after the last view or script change (be79fc56, 2026-11-16). These are operator-run scratch records, not repository tests. |
| 8 | View Details on desktop Edit points at `/Shop/Details/{id}` and opens for the editing Dungeon Master (D-04, A-shop-026) | VERIFIED | `Edit.cshtml:155` is `href="/Shop/Details/@Model.Id"`, and no `ShopManagement/Details` target remains in any view. `ShopManagementEdit_Desktop_ViewDetailsPointsAtTheShopDetailsPage` and `ShopDetails_EditingDungeonMaster_GetsAnOkPageForEveryStatus` pass, after 99-22 too. |
| 9 | The merchant is relocated (G1-O2) as a focusable button with an accessible name, in placement a | VERIFIED (markup) | `Index.cshtml:357-358` has `<button type="button" class="shop-merchant" aria-label="Talk to the merchant" data-merchant-button>` in `.shop-merchant-dock`. The polite `role="status"` live region is server-rendered. The 20 quotes sit in `template[data-merchant-quotes]`, identical to the baseline's 20 strings (diffed here). `gate1/answers.json` gives `merchant.placement: a`. The behaviour is truth 20. |
| 10 | Status vocabulary (D-06): Pending Review for Draft and Available for Published, through one mapping | VERIFIED | `ShopItemDisplay.cs:37-38` holds the mapping, used through `_ShopStatusBadge`. `ShopItemDisplayTests` pass. |
| 11 | Rarity chips (D-09): one chip on every page and layout, legendary amber per Gate 1 item 16 | VERIFIED | `_ShopRarityChip.cshtml`; `.badge-rarity.rarity-legendary` on `--qb-warning`/`--qb-warning-fg` (`shop-pages.css:40-43`). `legendaryChip.colour: amber`, ui-guidelines gold list unchanged. Render tests over all five rarities pass. |
| 12 | Emblem tile and whole images (D-07): no crop, no fixed height, availability dates through `Html.WallClock` | VERIFIED | `.emblem` uses a min block size and flex centring. No `aspect-ratio` or `object-fit` in any shop sheet. `_ShopItemDetailsContent.cshtml:78-86` uses `Html.WallClock`. History dates use `Html.LocalTime`. |
| 13 | DoC-5: accessibility bar A-1 to A-11 verified on each page, desktop and phone | VERIFIED | `~/qb-ui-review/phase-99/after/final/a11y.md`: one row per page and layout, every row showing 0 contrast failures, 0 missing rings, 0 obscured stops, no sideways scroll, 0 infinite animations and one `h1`. Gate 2 records 140 AFTER records with 0 errors. The exceptions e1 and e3 were accepted and e2 acknowledged as inherited. This is scratch-harness evidence; I checked that it agrees with the code but did not re-render. |
| 14 | DoC-6: BEFORE and AFTER outside the repository; relaid-out pages signed off | VERIFIED | `~/qb-ui-review/phase-99/{before,after,gate1,gate2}` exist. `gate1/answers.json` has `status: approved` (2026-11-16, sheet v2). `gate2/answers.json` has `approved: true`, `versions: [gate2-v1]` and 23 answers. |
| 15 | DoC-7: mobile checks listed in the UAT and run under a mobile User-Agent | VERIFIED | Click and sweep records carry the Pixel 7 User-Agent with `mobileLayout: true`. The real-phone and screen-reader checks are in the 99-20 Phase UAT and carried here as human verification. |
| 16 | DoC-8: guard tests rewritten to the same intent, none deleted | VERIFIED | `git diff --diff-filter=D` over both test projects since 59df0cf8 is empty. Only one test method name disappeared: `MobileShopIndex_MobileUserAgent_OmitsPurchaseHistoryPanel`, renamed to `..._RendersPurchaseHistoryWithReturnAndSell` and recorded in the scratch `tests-renamed.md` with the Q-56 flip. 188 test methods were added. |
| 17 | DoC-9: `Html.LocalTime`/`Html.WallClock` untouched; nothing writes to `Events` or `Quests`; calendar feed files untouched | VERIFIED | Non-test `.cs` changes since the baseline: `ShopController.cs`, `ShopService.cs`/`IShopService.cs` (the quantity guard) and four shop view models. Nothing under `QuestBoard.Repository`. No `HtmlHelperExtensions.cs`, `CalendarFeedWriter.cs`, `CalendarSubscriptionService.cs` or `FeedRevisionStamper.cs` change. |
| 18 | DoC-10: at merge every page still works and reads | VERIFIED | `dotnet build`: 0 errors, 67 warnings (xUnit1051 analyser notes). Unit 1433 passed, 0 skipped. Integration 2595 passed, 16 skipped (the SQL Server migrator, board-deletion and shop-item suites that need the environment flag), 0 failed. Both runs are mine. |
| 19 | Deferred items landing here are closed: the literal-colour shop alerts and required-mark rules, WINDOWS 12 and 22 for the shop, WINDOWS 28's shop share | VERIFIED | `shop.css` and `shop-management-edit.mobile.css` are deleted, and the new sheets have 0 literals. WINDOWS 12 and 22 are `fixed`. Entry 28's reason records the shop closure: important 152 to 0, hex 189 to 0, rgb 209 to 0, handlers 40 to 0, endless animations 16 to 0. It stays open only for other phases' sheets. |
| 20 | Merchant behaviour (toast, announcement, hover hold, motion) | PRESENT_BEHAVIOR_UNVERIFIED | See behavior_unverified_items. `shop-merchant.js` (67 lines) writes with `textContent` only, keeps one toast and uses Bootstrap autohide with a 6s delay. Scratch `merchant.json` shows every check true. |
| 21 | Sell to Shop confirmation and total in the dialog and on the page (A-shop-017) | PRESENT_BEHAVIOR_UNVERIFIED | `shop-item-sell.js` (38 lines) uses delegated `input` and `submit` listeners and reads the name through `dataset`. It is loaded on both Index layouts and both Details layouts. Scratch `behaviour.json` and the `click-selltoshop-*` records pass. |
| 22 | Preview dialog (D-03, A-shop-027) | PRESENT_BEHAVIOR_UNVERIFIED | `Create.cshtml:122-138` has the Preview button (`data-bs-target="#itemPreviewModal"`) and the modal with `h2` Item Preview. `shop-item-preview.js` is 154 lines. No new endpoint. |
| 23 | Details dialog loader, history disclosure and phone drawers | PRESENT_BEHAVIOR_UNVERIFIED | `shop-item-modal.js` (69), `shop-history.js` (16) and `shop-drawers.js` (25) are wired by data attributes. Pinned by text-only guards. |
| 24 | Management confirmations, Deny dialog, Bulk Actions notices and the pricing module | PRESENT_BEHAVIOR_UNVERIFIED | The four confirm sentences are byte-identical to the baseline on both layouts (diffed here). `data-deny-url`/`data-deny-name` feed `shop-management.js`. `shop-item-form.js` (115) is bound by `data-shop-item-form` on all four forms. |

The two plans the operator requested after Gate 2 add these must-haves. Both are VERIFIED by repository tests and support truths 6, 8 and 18; they are shown separately and not counted again in the 24:

| Plan | Truth | Status | Evidence |
|------|-------|--------|----------|
| 99-21 | A requested quantity below 1 is refused by Purchase, Return/Sell and Sell to Shop before any repository call, with "Quantity must be at least 1.", and the stock convention is unchanged | VERIFIED | `ShopService.EnsureQuantityIsAtLeastOne` runs first in all three methods. Passing tests: `PurchaseItemAsync_`, `ReturnOrSellItemAsync_` and `SellItemToShopAsync_QuantityBelowOne_IsRefusedBeforeAnyRepositoryCall` (unit), and `Post_QuantityBelowOne_IsRefusedWithTheErrorAndChangesNothing` (integration). |
| 99-22 | A plain member gets 404 for a Draft or Denied item on the page and in the dialog, the same as a missing id; reviewers (creating DM, other DM, board Admin, SuperAdmin) still open it; Published and Archived are unchanged | VERIFIED | `ShopController.Details` checks `IsUnreleased(item.Status) && !AuthorizeAsync(User, "DungeonMasterOnly")` before the `isModal` branch, with no role-name check. Passing tests: `Details_DraftOrDeniedItem_IsNotFoundForAPlainMember`, `..._OpensForItsCreatingDungeonMaster`, `..._OpensForAnotherDungeonMasterAnAdminOrASuperAdmin`, `Details_PublishedOrArchivedItem_StillOpensForAPlainMember` and `Details_RefusedItem_AnswersExactlyLikeAMissingId`. |

**Score:** 19/24 truths verified (5 present, behaviour unverified)

### Required Artifacts

| Artifact | Expected | Status | Details |
|----------|----------|--------|---------|
| `Views/Shop/{Index,Details}[.Mobile].cshtml`, `Views/ShopManagement/{Index,Create,Edit}[.Mobile].cshtml` | Ten converted views | VERIFIED | Each sets the theme and links its sheets in `@section Styles` |
| `Views/Shared/_ShopItemDetailsContent.cshtml` | One details body for dialog and page, both layouts | VERIFIED | Rendered by both Details views and the fetched dialog |
| `Views/Shared/_ShopPurchaseHistory`, `_ShopRarityChip`, `_ShopStatusBadge`, `_ShopTypeChip`, `_ShopManagementStockBadge`, `_ShopManagementDialogs` | Shared partials | VERIFIED | Each used by both layouts |
| `wwwroot/css/shop-pages.css`, `shop-pages.mobile.css`, `shop-form.css` | Layered, literal-free sheets | VERIFIED | 858, 186 and 89 lines; counts in truth 2 |
| `wwwroot/js/shop-{item-sell,item-modal,item-preview,item-form,merchant,management,history,drawers}.js` | Modules replacing the inline scripts | VERIFIED (behaviour: truths 20-24) | All loaded as `type="module"`; none builds markup from strings |
| `ViewModels/ShopViewModels/ShopItemDisplay.cs` | One status and type vocabulary | VERIFIED | Unit-tested |
| `QuestBoard.IntegrationTests/Tests/ShopConversionGuardTests.cs`, `ShopRenderTests.cs` | Area guards and render tests | VERIFIED | 1228 and 2368 lines, all passing |

### Key Link Verification

| From | To | Via | Status | Details |
|------|----|-----|--------|---------|
| Ten shop views | both layouts | `ViewData["Theme"] = "shop"` emitted as `data-theme` on `<html>` | WIRED | `tokens.css` `[data-theme='shop']` block |
| `_ShopItemDetailsContent` sell form | `shop-item-sell.js` | `data-sell-form`, `data-item-name`, `data-unit-price`, `data-sell-quantity` | WIRED (behaviour: truth 21) | Module on Index (both) and Details (both) |
| Purchase history Sell row, management forms | `confirm-submit.js` | `data-confirm-message` | WIRED (behaviour: truths 7, 24) | Loaded on both Index and both management Index layouts, the only views with the attribute |
| Merchant button | `shop-merchant.js` | `data-merchant-button`, `template[data-merchant-quotes]`, `template[data-merchant-toast]`, `[data-merchant-live]` | WIRED (behaviour: truth 20) | Desktop Index only (Q-60) |
| Create Preview button | `#itemPreviewModal`, `shop-item-preview.js` | `data-bs-toggle="modal"` | WIRED (behaviour: truth 22) | |
| Edit View Details | `ShopController.Details` | `href="/Shop/Details/{id}"` | WIRED | Policy lets the editing DM through for every status |
| `ShopController.Details` | `DungeonMasterOnly` policy | `IAuthorizationService.AuthorizeAsync` | WIRED | Only for Draft and Denied |

### Data-Flow Trace (Level 4)

| Artifact | Data variable | Source | Real data | Status |
|----------|---------------|--------|-----------|--------|
| Purchase history | `Model.UserPurchases`, `CanReturn`, `SellRefund` | `ShopController.Index` from the user transaction repository; `CanReturn` compares the stored UTC purchase instant with now (24h) | Yes | FLOWING |
| Item grid and details | `ShopIndexViewModel.Items`, item fields | `IShopService` queries with the board query filter | Yes | FLOWING |
| Status badge | `ItemStatus` | entity status through `ShopItemDisplay` | Yes | FLOWING |
| Merchant toast | quote text | server-rendered template (20 fixed quotes) | Static by design | FLOWING (static content, intended) |

### Behavioral Spot-Checks

| Behavior | Command | Result | Status |
|----------|---------|--------|--------|
| Build | `dotnet build` | 0 errors, 67 warnings | PASS |
| Unit suite | `dotnet test` (unit project) | 1433 passed, 0 failed, 0 skipped | PASS |
| Integration suite | `dotnet test` (integration project, EF InMemory) | 2595 passed, 0 failed, 16 skipped | PASS |
| No inline code in shop views | grep `style=`, `<style`, `on*=`, `confirm(`, `alert(`, `innerHTML` | none; scripts are module includes | PASS |
| No literals in shop sheets | grep hex, `rgb(`, `!important`, `infinite`, `transition: all` | 0 in all three | PASS |
| Merchant quotes unchanged | diff of the baseline array against the template | 20 = 20, identical | PASS |
| Management confirm sentences unchanged | baseline `confirm('...')` against `data-confirm-message` | identical, both layouts | PASS |
| No planning ids or debt markers in changed source | grep for D-, Q-, A-shop, WINDOWS, Phase, Gate, DoC, plan ids; TBD, FIXME, XXX, TODO, HACK | none | PASS |

### Probe Execution

No `probe-*.sh` exists in the repository or is declared by the phase. SKIPPED. The scratch Playwright tools under `~/qb-ui-review/phase-99/tools/` were not re-run (the brief rules out running the app); their records were read.

### Requirements Coverage

`.planning/REQUIREMENTS.md` has no Phase 99 IDs; ROADMAP says "Requirements: TBD". As 99-01 recorded, the requirement IDs are D-01 to D-16 (99-CONTEXT) and DoC-1 to DoC-10. None is orphaned.

| ID | Meaning | Status | Evidence |
|----|---------|--------|----------|
| D-01 | Merchant placement picked at Gate 1, focusable button | SATISFIED | Truth 9; placement a |
| D-02 | Merchant toast accessible, same quotes, module, text insertion | SATISFIED in code; behaviour is truth 20 | |
| D-03 | Preview dialog from form values | SATISFIED in code; behaviour is truth 22 | |
| D-04 | View Details to `/Shop/Details/{id}` | SATISFIED | Truth 8 |
| D-05 | Return and Sell visible; sell module; native confirms through `confirm-submit.js` | SATISFIED | Truths 6, 7; behaviour is truths 21, 24 |
| D-06 | One status vocabulary | SATISFIED | Truth 10 |
| D-07 | Emblem tile | SATISFIED | Truth 12 |
| D-08 | Endless animations stop except a capped merchant bob | SATISFIED | Truth 2 |
| D-09 | Rarity chips, legendary amber | SATISFIED | Truth 11 |
| D-10 | Phone purchase history | SATISFIED as redrawn (drawer) at Gate 1 | Truth 5 |
| D-11, D-12 | Two local gates; redraw loop | SATISFIED | Gate 1 v2 after one redraw; Gate 2 v1 |
| D-13 | Linux machine, BEFORE re-captured | SATISFIED | Scratch `before/`, `baseline-src/` |
| D-14 | Operator checkpoint; fixtures through the app's forms | SATISFIED | 99-01, 99-02; fixtures kept (`fixtures:keep`) |
| D-15 | Email to Mailpit only | SATISFIED | run-app forces suppression; no email code changed |
| D-16 | Slices on the milestone branch, one release at the end | SATISFIED | Branch `milestone/v9-rolling-improvements`; no tag; v6.4.0 handoff in 99-20 |
| DoC-1 to DoC-10 | Definition of converted | SATISFIED | Truths 1-4, 13-18 |

### Anti-Patterns Found

| File | Line | Pattern | Severity | Impact |
|------|------|---------|----------|--------|
| `Views/Shop/Index.cshtml` | 92 | History pane rendered with `hidden`; only `shop-history.js` opens it | Info | Without JavaScript the desktop Return and Sell are unreachable. The operator chose collapsed-by-default at Gate 1, and the site already relies on scripts (Bootstrap dialogs) |
| `Views/Shared/_ShopItemDetailsContent.cshtml` | 10 | `halfPrice` uses current-culture `ToString("N0")`, while the history Sell sentence uses `InvariantCulture` and `shop-item-sell.js` uses the browser locale | Info | Cosmetic digit-grouping differences only |
| `ViewModels/ShopViewModels/UserTransactionViewModel.cs` | 22 | `DateTime.UtcNow` | Info | A real-instant elapsed-time check moved out of the baseline view. It is not a board-day read, and `AmbientClockSeamTests` does not list the file (unchanged) |

No `TBD`, `FIXME`, `XXX`, `TODO` or `HACK` marker in a changed file. No blocker anti-pattern.

### Human Verification Required

See the frontmatter `human_verification` list. Five items:
- a real-phone touch pass
- a screen-reader pass
- the production culture check of availability dates
- an operator decision on three pre-existing server findings recorded after Gate 2
- a run of the five behaviour-unverified items on the deployed build

The first three are the 99-20 Phase UAT, deferred until after deploy by the operator's standing rule. They are not gaps.

### Findings that are not gaps

- **Q-120 wording versus what was built.** The roadmap asks for the purchase history "under the item grid". At Gate 1 the operator asked for a collapsible side pane instead, collapsed by default and a drawer on the phone, and approved it as v2. Gate 2 approved "Q-120 as built". The roadmap itself delegates the final form to the re-confirmation at the gate. The pane is in-flow, not fixed, and stacks below 768px, so the intent "reachable at every width" holds. ROADMAP's Q-120 text is now stale.
- **Gate 2 decisions.** e1 (toast Close 21px) and e3 (1px overshoot at 320 under text spacing) were accepted. e2 (inherited editor) was acknowledged. d1 was accepted, waiving WINDOWS 33, the submit lock, as the operator instructed; the waiver is recorded in the ledger (`waived`). d2 to d4 were accepted, t1 to t11 accepted, and `df` deferred. None is counted against the score.
- **Pre-existing server findings recorded after Gate 2 (WARNING, operator decision requested).** 99-22 found three rules it did not change and never put to the operator:
  - The availability window is not enforced by id on Details, Purchase and Sell to Shop.
  - `Archive` accepts any status and `Reopen` publishes any archived item. I confirmed this in `ShopManagementController.cs:202-214` and `ShopService.ArchiveItemAsync`. The UI offers Archive only on Published items, so this needs a crafted POST by a Dungeon Master. Through it, an unreviewed Draft can reach members as Archived or as Published, which sidesteps the second-DM review and partly undoes 99-22's intent.
  - A deep link to another board's Draft switches the member's board before the 404.

  None was introduced by this phase: the controller, service methods and middleware involved are unchanged since the baseline, apart from the 99-21 guard and the 99-22 check. They are outside a UI conversion's scope, so they are not gaps. They are listed under human verification so the operator decides on them explicitly.
- **Evidence provenance.** Contrast, overflow, focus, target-size, click-through and merchant results come from the scratch harness in `~/qb-ui-review/phase-99/`. I confirmed the records exist, agree with the code, and post-date the last view or script change. 99-21 and 99-22 changed only the service and controller after the sweep; they are covered by repository tests and do not affect the swept Published-item paths. I did not re-render pages or start an app, per the brief.
- **Skipped tests.** The 16 skipped integration tests are the SQL Server suites gated by an environment switch: the migrator, board deletion and shop item suites.
- **Planning text.** `.claude/ui-guidelines.md` gained the theme opt-in line. It is a guideline file, not a source comment.

### Gaps Summary

No gaps. Every roadmap contract item, the phase extras, the Definition of converted, and the two operator-requested plans (99-21, 99-22) hold in the code at HEAD 4d7c389b. Both test projects pass on my run (unit 1433, integration 2595 with 16 environment-gated skips).

Five behaviour-dependent truths have present and wired code and text-level guards, but no repository test runs a browser:
- Sell to Shop in the dialog and on the page
- the merchant toast and announcement
- the Preview dialog
- the dialog loader and drawers
- the management confirmations, Deny dialog and pricing module

They stand as `human_needed` beside the real-phone, screen-reader and culture UAT the operator deferred until after deploy, and beside one operator decision on three pre-existing server rules recorded after Gate 2.

---

_Verified: 2026-11-17_
_Verifier: Claude (gsd-verifier)_
