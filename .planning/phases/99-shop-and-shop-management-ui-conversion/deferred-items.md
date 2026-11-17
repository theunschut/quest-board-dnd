# Phase 99 deferred items

Found by the closing sweep (99-19) or earlier in the phase and not changed. Each sweep finding is also an entry in `~/qb-ui-review/phase-99/contrast-exceptions.json`, pending Gate 2. None was fixed in 99-19, whose scope is measurement, records and the ledger only.

## Resolved in this phase

| Item | Where | Resolution |
|---|---|---|
| Server quantity validation on Purchase, Return or Sell, and Sell to Shop | `ShopService.PurchaseItemAsync`, `ReturnOrSellItemAsync`, `SellItemToShopAsync` | A requested quantity below 1 is refused with "Quantity must be at least 1." before any repository call; commit `5b77dddc` (plan 99-21, see its SUMMARY). The operator answered Gate 1 item 20 "fix in phase 99", so quantity validation is **not deferred**. The item stock convention (-1 unlimited, 0 sold out, above 0 limited) is untouched, and old rows with a zero or negative quantity that may already exist were not migrated (no data change by design). |

## Deferred

| Item | Where | Measured | Why deferred |
|---|---|---|---|
| Any member can open a Draft item's Details by id | `ShopController.Details` (`/Shop/Details/{id}` and the dialog's `isModal` form) | the action loads the item by id and renders it without checking its status or the viewer's role | an authorisation change to a controller, outside a UI conversion; the conversion kept the action as it was |
| Markdown images in item descriptions can act as a tracking pixel | item description rendering (`markdown-content` on Details and the dialog) | a description may hold an image whose address is any host; the viewer's browser requests it when the page is shown (the wide-image fixture proves images render whole inside the column) | a content-policy decision (allow-list or proxy) for the whole site's Markdown, not this area's views |
| Dialog dates keep the server wording until the page reloads | the details dialog on Shop/Index | the dialog body is the server partial fetched on open, so a date written in it keeps the wording the server produced until the page is reloaded (recorded by the plan; not re-measured in the sweep) | follows the wall-clock rule (no conversion is wrong); a reload shows the same text. Accepted at Gate 1 (wall-clock dates) |
| Shop forms do not opt in to the submit lock | Purchase, Return, Sell, Sell to Shop, Create and Edit forms | with the POST held back the primary button stays enabled and keeps its label (`after/final/inflight-99-19.json`); no shop view carries `data-disable-on-submit`, at the baseline or now | one attribute per form is source work this plan does not do; Gate 2 decision `decision-99-no-submit-lock`, and a ledger entry (33) keeps it visible to the ship gate |
| `QuestsConversionGuardTests` message still says the wide-container rule serves "the DM profile pages" | `QuestBoard.IntegrationTests/Tests/QuestsConversionGuardTests.cs` | only the group picker layout renders `.container.mt-4` (a DM profile test asserts the profile does not) | raised by 99-18; outside the shop area's files, a one-line message fix for a later cleanup |
| Phone Create has a 24 px "Description" region and Edit at 320 px under text spacing overshoots by 1 px | the shared Markdown editor and the Edit action row | 302 by 24 px on the phone Create rows; scrollWidth 321 against 320 on the three desktop Edit rows (same as BEFORE) | inherited from the shared editor, unchanged by this phase; recorded as `exception-99-editor-inherited` and `exception-99-text-spacing-320-edit` |
| Merchant toast Close is 21 by 21 px | the merchant toast on desktop Shop/Index | 21 px at 1440 and 320 (21 px at 1440 BEFORE too) | the framework's close icon button; `exception-99-toast-close-21px`, Gate 2 decides |

## Not a deferral

Items the plan asked about that turned out to need nothing: the sweep found no contrast, lost-control, overflow, small-phone-target, missing-focus-ring or console-error regression against BEFORE (129 rows compared, 0 failing, 0 residual contrast).
