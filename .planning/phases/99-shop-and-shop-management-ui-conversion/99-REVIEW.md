---
phase: 99-shop-and-shop-management-ui-conversion
reviewed: 2026-11-17T00:00:00Z
depth: standard
files_reviewed: 44
files_reviewed_list:
  - CLAUDE.md
  - .claude/architecture.md
  - .claude/ui-guidelines.md
  - QuestBoard.Domain/Interfaces/IShopService.cs
  - QuestBoard.Domain/Services/ShopService.cs
  - QuestBoard.Service/Controllers/Shop/ShopController.cs
  - QuestBoard.Service/ViewModels/ShopViewModels/ShopIndexViewModel.cs
  - QuestBoard.Service/ViewModels/ShopViewModels/ShopItemDisplay.cs
  - QuestBoard.Service/ViewModels/ShopViewModels/ShopItemViewModel.cs
  - QuestBoard.Service/ViewModels/ShopViewModels/UserTransactionViewModel.cs
  - QuestBoard.Service/Views/Shared/_Layout.Mobile.cshtml
  - QuestBoard.Service/Views/Shared/_Layout.cshtml
  - QuestBoard.Service/Views/Shared/_MarkdownEditor.cshtml
  - QuestBoard.Service/Views/Shared/_ShopItemDetailsContent.cshtml
  - QuestBoard.Service/Views/Shared/_ShopManagementDialogs.cshtml
  - QuestBoard.Service/Views/Shared/_ShopManagementStockBadge.cshtml
  - QuestBoard.Service/Views/Shared/_ShopPurchaseHistory.cshtml
  - QuestBoard.Service/Views/Shared/_ShopRarityChip.cshtml
  - QuestBoard.Service/Views/Shared/_ShopStatusBadge.cshtml
  - QuestBoard.Service/Views/Shared/_ShopTypeChip.cshtml
  - QuestBoard.Service/Views/Shop/Details.Mobile.cshtml
  - QuestBoard.Service/Views/Shop/Details.cshtml
  - QuestBoard.Service/Views/Shop/Index.Mobile.cshtml
  - QuestBoard.Service/Views/Shop/Index.cshtml
  - QuestBoard.Service/Views/ShopManagement/Create.Mobile.cshtml
  - QuestBoard.Service/Views/ShopManagement/Create.cshtml
  - QuestBoard.Service/Views/ShopManagement/Edit.Mobile.cshtml
  - QuestBoard.Service/Views/ShopManagement/Edit.cshtml
  - QuestBoard.Service/Views/ShopManagement/Index.Mobile.cshtml
  - QuestBoard.Service/Views/ShopManagement/Index.cshtml
  - QuestBoard.Service/wwwroot/css/base.css
  - QuestBoard.Service/wwwroot/css/legacy-pages.css
  - QuestBoard.Service/wwwroot/css/shop-form.css
  - QuestBoard.Service/wwwroot/css/shop-pages.css
  - QuestBoard.Service/wwwroot/css/shop-pages.mobile.css
  - QuestBoard.Service/wwwroot/js/shop-drawers.js
  - QuestBoard.Service/wwwroot/js/shop-history.js
  - QuestBoard.Service/wwwroot/js/shop-item-form.js
  - QuestBoard.Service/wwwroot/js/shop-item-modal.js
  - QuestBoard.Service/wwwroot/js/shop-item-preview.js
  - QuestBoard.Service/wwwroot/js/shop-item-sell.js
  - QuestBoard.Service/wwwroot/js/shop-management.js
  - QuestBoard.Service/wwwroot/js/shop-merchant.js
findings:
  critical: 0
  warning: 5
  info: 5
  total: 10
status: issues_found
---

# Phase 99: Code Review Report

**Reviewed:** 2026-11-17
**Depth:** standard
**Files Reviewed:** 44 (3 are guidance docs, read for the rules)
**Status:** issues_found

## Summary

The phase replaced the global shop stylesheets with layered page sheets, moved every inline script and handler into modules, and rebuilt the shop and shop-management views and twins on one shared set of partials. The code is clean against the project's own rules. A sweep found no GSD identifiers in source comments, no colour literals, no `!important`, no inline `style` or `<style>` or `<script>`, no `@media` in the phone sheet, and no EF in the Service layer. The dialog fetch, the Markdown preview and the merchant all write with `textContent` or cloned templates, and every attribute that carries user text (`data-item-name`, `data-confirm-message`, `data-deny-name`) goes through Razor encoding. No class removed from `legacy-pages.css` is still used outside the shop views. The 99-22 Draft and Denied gate in `ShopController.Details` is correct for both the page and the dialog.

No blockers. The findings below are one server-side integrity gap next to the quantity hardening this phase added, and a handful of view and script defects, some carried over from the old views but now in rewritten code.

Not re-raised, per instructions: the missing submit lock (WINDOWS 33), the five items in `deferred-items.md`, and the accepted contrast exceptions. I also did not raise the removed client "20 characters" check, the empty dialog body on a failed fetch, or the phone dialog's "Back to Shop" link. The UI spec records each as a Gate 1 default or decision.

## Narrative Findings (AI reviewer)

## Warnings

### WR-01: Sell to Shop has no upper bound on quantity, so a hand-made post can overflow the item's stock

**File:** `QuestBoard.Domain/Services/ShopService.cs:203-208` (also `QuestBoard.Service/Views/Shared/_ShopItemDetailsContent.cshtml:204`)
**Issue:** This phase added `EnsureQuantityIsAtLeastOne` because "a hand-made post can carry any number; the forms' minimum is only a browser hint". The maximum is the same kind of browser hint (`max="99"` on `#sellQuantity`) and is not enforced. `SellItemToShopAsync` computes `item.Quantity += quantity` in unchecked `int` arithmetic. A post with `quantity=2147483647` against an item whose stock is 1 or more wraps the stock to a large negative number. The stock convention (-1 unlimited, 0 sold out, above 0 limited) then misreads the item. Every later stock check sees a negative quantity, `Quantity != 0` is true, and `Quantity <= 5` shows "Only -2147483646 left". The payout `(item.Price / 2) * quantity` is also unbounded. Selling to the shop needs no originating purchase by design, so the payout is not new, but the stock corruption is a data-integrity bug. `ReturnOrSellItemAsync` is safe because it is bounded by the remaining quantity. `PurchaseItemAsync` is safe for limited stock because it checks `item.Quantity < quantity`.
**Fix:** Cap the quantity in the service and use checked arithmetic.
```csharp
private const int MaxQuantityPerTransaction = 99; // matches the form's max

private static void EnsureQuantityIsValid(int quantity)
{
    if (quantity < 1) throw new InvalidOperationException("Quantity must be at least 1.");
    if (quantity > MaxQuantityPerTransaction)
        throw new InvalidOperationException($"Quantity cannot be more than {MaxQuantityPerTransaction}.");
}
// ...
if (item.Quantity >= 0) { item.Quantity = checked(item.Quantity + quantity); }
```
Use the same cap for Purchase when the item is unlimited, since its form also says `max="99"` for unlimited stock.

### WR-02: Create form silently drops availability dates when the page is re-shown after a server validation error

**File:** `QuestBoard.Service/Views/ShopManagement/Create.cshtml:96,105,110` (and `Create.Mobile.cshtml:88,97,102`)
**Issue:** The availability checkbox is always rendered unchecked and both date inputs are always rendered `disabled`. When `Create` POST returns `View(viewModel)` because of a server-side error (`[Url]`, price range, or a stripped description), `asp-for` refills the dates from the model, but the inputs are still disabled. The user sees their dates in greyed-out fields, and the next submit does not post them, so the item is created with no availability window. Edit handles this correctly with `hasAvailabilityWindow`, which makes the two views inconsistent. The old view had the same flaw, but the rewrite already added the correct pattern to Edit and did not carry it to Create.
**Fix:** Mirror Edit in both Create views.
```cshtml
@{ var hasAvailabilityWindow = Model.AvailableFrom != null || Model.AvailableUntil != null; }
<input class="form-check-input" type="checkbox" id="enableAvailabilityWindow" data-availability-toggle
       aria-describedby="enableAvailabilityWindow-help" checked="@hasAvailabilityWindow" />
...
<input asp-for="AvailableFrom" type="datetime-local" class="form-control" disabled="@(!hasAvailabilityWindow)" />
<input asp-for="AvailableUntil" type="datetime-local" class="form-control" disabled="@(!hasAvailabilityWindow)" />
```

### WR-03: Edit page shows "Pending Review" and the under-review notices for any item after a validation error

**File:** `QuestBoard.Service/Views/ShopManagement/Edit.cshtml:21-35,76,138-146` (and `Edit.Mobile.cshtml`)
**Issue:** The status badge, "This item is awaiting DM approval", the rarity warning and the "Item Under Review" alert all read `Model.Status`. Status is not a form field. On the GET it is mapped from the item, but when `Edit` POST fails validation and returns `View(viewModel)`, `Status` is the enum default `Draft`. A Published or Archived item then shows "Pending Review", "awaiting DM approval" and the under-review alert. The phase also changed the draft word from "Draft" to "Pending Review" (`ShopItemDisplay.StatusName`), which makes the wrong state read as a claim about review rather than a label.
**Fix:** Post the status back with a hidden field, or re-read it in the POST failure path.
```cshtml
<input asp-for="Status" type="hidden" />
```
The hidden field is only used for display and the controller never reads it into the entity, so there is no trust issue. A safer fix is for `Edit` POST to reload the item and set `viewModel.Status = item.Status` before returning the view.

### WR-04: Admin sees a Publish button on their own Draft items, and the server answers 403

**File:** `QuestBoard.Service/Views/ShopManagement/Index.cshtml:172-180`, `QuestBoard.Service/Views/ShopManagement/Index.Mobile.cshtml:88-96`
**Issue:** `ShopManagementController.Publish` returns `Forbid()` when `item.CreatedByDmId == currentUser.Id`. In "My Items" the view shows Publish whenever `item.Status == Draft && isAdmin`, which for an admin includes their own drafts. The phone list does the same for every Draft, and also shows the Deny button on the viewer's own Draft items, which the controller refuses with "You cannot deny your own items." The admin gets a bare 403 for Publish. The behaviour is carried over from the old views, but both are rewritten files and the view logic now sits next to a controller rule it contradicts.
**Fix:** Hide the control when the viewer is the creator. The simplest fix is to pass `CurrentUserId` on the management view model and test `item.CreatedByDmId != Model.CurrentUserId` in the four places. On desktop, "My Items" Drafts should show no Publish button at all, because the creator cannot publish their own item.

### WR-05: The 24-hour return rule lives in two places, and the Return button has no guard for the boundary

**File:** `QuestBoard.Service/ViewModels/ShopViewModels/UserTransactionViewModel.cs:9,22` and `QuestBoard.Domain/Services/ShopService.cs:~145`
**Issue:** The window is written once in the view model (`ReturnWindow = 24h`, `<=`) and once in the service (`timeSincePurchase.TotalHours <= 24`). The two are only kept in step by a comment, and the view model reads `DateTime.UtcNow` in a property getter, so it is not testable without waiting. The Return slip never asks for confirmation. A slip rendered at hour 23:59 and submitted after hour 24 is treated by the service as a half-price sell, with no confirmation, and the flash message then reads "sold" with half the refund. The shop-wide rule needs one definition.
**Fix:** Expose the window from the domain, for example `ShopService.ReturnWindow` on `IShopService`, and compute `CanReturn` in the controller from `IBoardClock` or an injected `TimeProvider`. For the boundary, either put the Return button's own `data-confirm-message` on slips close to the limit, or accept the half-price outcome explicitly in the success message.

## Info

### IN-01: Five view-model display helpers are now dead code

**File:** `QuestBoard.Service/ViewModels/ShopViewModels/ShopItemViewModel.cs:52,54,56,66,68,77`
**Issue:** The conversion moved every view onto `ShopItemDisplay` and the chip partials. A search of `Views/` and `Controllers/` finds no reference to `RarityDisplayName`, `TypeDisplayName` (added this phase), `RarityColorClass`, `StatusDisplayName`, `StatusColorClass` or `IsAvailable`. `ShopItemDisplay` was meant to be "the one place", and these properties are a second, unused path, two of which still use the old Bootstrap `text-*` classes.
**Fix:** Delete the unused properties (check the unit tests first, then remove any test that only pins them).

### IN-02: "My Transactions" counts and labels every transaction as a purchase

**File:** `QuestBoard.Service/ViewModels/ShopViewModels/ShopIndexViewModel.cs:21`, `QuestBoard.Service/Views/Shop/Index.cshtml:82`, `QuestBoard.Service/Views/Shared/_ShopPurchaseHistory.cshtml:92`
**Issue:** `UserPurchases` holds purchase, return and sell-to-shop rows. The toggle badge reads "3 purchases" for the screen reader and the cap line reads "Showing latest 15 purchases", while the rows include payouts shown as "(Sold/Returned)". Both the name `ListedPurchaseCount` and the wording are wrong for a mixed list.
**Fix:** Say "transactions" in both strings and rename the property `ListedTransactionCount`.

### IN-03: The page size 12 is hard-coded in two views and the controller

**File:** `QuestBoard.Service/Views/Shop/Index.cshtml:314`, `QuestBoard.Service/Views/Shop/Index.Mobile.cshtml:165`, `QuestBoard.Service/Controllers/Shop/ShopController.cs:~30`
**Issue:** `const int pageSize = 12` in the controller and `* 12` in both views must stay equal. A change to one produces a wrong "Showing 13-24 of N" line with no failure.
**Fix:** Add `PageSize` to `ShopIndexViewModel` (as was done for `PurchaseHistoryLimit`), set it in the controller and read it in the views.

### IN-04: The rarity table is repeated in four places

**File:** `QuestBoard.Service/wwwroot/js/shop-item-form.js:11-17`, `QuestBoard.Service/wwwroot/js/shop-item-preview.js:16-22`, `QuestBoard.Service/Views/ShopManagement/Create.cshtml:52-57`, `QuestBoard.Service/ViewModels/ShopViewModels/ShopItemDisplay.cs:12-32`
**Issue:** The rarity words, keys and the "Very Rare" spelling are written in two scripts, in the Create and Edit selects, and in `ShopItemDisplay`. The phase's stated aim is one spelling per label, but a new rarity needs four edits and a miss shows as a chip with no colour or a button that does nothing.
**Fix:** Render the table once from `ShopItemDisplay` into a `data-` attribute or a `<template>` on the form and have both modules read it. The select options can loop over `Enum.GetValues<ItemRarity>()`.

### IN-05: The Equipment branch in the price helper enables buttons that do nothing

**File:** `QuestBoard.Service/wwwroot/js/shop-item-form.js:52,64-69,77-89`
**Issue:** For Equipment, `enabled = isEquipment || guide !== null`, so the dice and wand buttons are enabled even when no rarity is chosen, and the hint is emptied. Both click handlers need `guideFor(rarity.value)`, so with no rarity a click does nothing, silently. With a rarity chosen, the buttons write a price from the rarity range while the hint stays blank. The rule came from the old Edit script, but the rewrite now applies it to Create too, where the old script did not have it.
**Fix:** Enable the buttons only when `guide !== null`, whatever the type. If Equipment really should not use the rarity guide, disable them for Equipment instead.

---

_Reviewed: 2026-11-17_
_Reviewer: Claude (gsd-code-reviewer)_
_Depth: standard_
