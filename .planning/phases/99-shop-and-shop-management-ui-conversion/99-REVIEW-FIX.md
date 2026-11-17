---
phase: 99-shop-and-shop-management-ui-conversion
fixed_at: 2026-11-17T00:00:00Z
review_path: .planning/phases/99-shop-and-shop-management-ui-conversion/99-REVIEW.md
iteration: 1
findings_in_scope: 5
fixed: 5
skipped: 0
status: all_fixed
---

# Phase 99: Code Review Fix Report

**Fixed at:** 2026-11-17
**Source review:** .planning/phases/99-shop-and-shop-management-ui-conversion/99-REVIEW.md
**Iteration:** 1

**Summary:**
- Findings in scope: 5 (WR-01 to WR-05; Info findings left alone by instruction)
- Fixed: 5
- Skipped: 0 (one sub-part of WR-05 deliberately not done, see below)

## Fixed Issues

### WR-01: Sell to Shop has no upper bound on quantity

**Files modified:** `QuestBoard.Domain/Services/ShopService.cs`, `QuestBoard.UnitTests/Services/ShopServiceTests.cs`
**Commit:** 69207a4a
**Applied fix:** `SellItemToShopAsync` now refuses a quantity that would push a limited item's stock past `int.MaxValue`, using an explicit bound check (`quantity > int.MaxValue - item.Quantity`) before anything is changed or recorded. It throws the existing `InvalidOperationException`, which the controller already turns into the TempData error. No arbitrary business cap was added (the review's suggested 99 was not applied, so a legitimate large sell still works). Unlimited items (stock -1) are unaffected. The Purchase path needed no change (it cannot overflow the int stock). Three unit tests added: overflow is refused and changes nothing, filling the stock exactly to `int.MaxValue` is accepted, and an unlimited item stays at -1.

### WR-02: Create form silently drops availability dates after a server validation error

**Files modified:** `QuestBoard.Service/Views/ShopManagement/Create.cshtml`, `QuestBoard.Service/Views/ShopManagement/Create.Mobile.cshtml`, `QuestBoard.IntegrationTests/Controllers/ShopManagementControllerIntegrationTests.cs`
**Commit:** 3c90b903
**Applied fix:** Both Create views now compute `hasAvailabilityWindow` from the model and use it for the checkbox `checked` state and the `disabled` state of both date inputs, mirroring Edit. A fresh GET (no dates) renders exactly as before. The POST handler is untouched. Four integration tests (desktop and phone) cover a refused post with dates (box checked, fields open, dates shown) and without dates (box unchecked, fields disabled); the with-dates tests fail without the view change.

### WR-03: Edit page shows "Pending Review" after a validation error

**Files modified:** `QuestBoard.Service/Controllers/Shop/ShopManagementController.cs`, `QuestBoard.IntegrationTests/Controllers/ShopManagementControllerIntegrationTests.cs`
**Commit:** 192ce651
**Applied fix:** Used the review's "safer" option. In the `Edit` POST invalid-model path the controller now reloads the item and sets `viewModel.Status` from it before re-rendering, so the status badge and the under-review notices reflect the item's real status. No hidden field was added, and the successful-POST path is unchanged. Because both the desktop and the phone view read `Model.Status`, one controller change fixes both twins. Six integration tests (desktop and phone; Published, Archived and Draft) added; the four non-draft cases fail without the fix, and a Draft item still shows "awaiting DM approval".

### WR-04: Admin sees a Publish button on their own Draft items, and the server answers 403

**Files modified:** `QuestBoard.Service/Views/ShopManagement/Index.cshtml`, `QuestBoard.Service/Views/ShopManagement/Index.Mobile.cshtml`, `QuestBoard.IntegrationTests/Tests/ShopRenderTests.cs`
**Commit:** eb0e3ec1
**Applied fix:** Only controls the controller would refuse were hidden. Desktop: the Publish form was removed from "My Items" (every row there is the viewer's own, so it always answered 403); the Items Awaiting Review table, which already excludes the viewer's own items, keeps Publish and Deny. Phone: the merged list now computes `ownItemIds` from `Model.MyItems` and shows Deny and Publish only when the item is not the viewer's own. No view model or controller change was needed. Edit, Delete, Archive, Reopen and View were left as they were. Two integration tests (desktop and phone) assert that the viewer's own draft offers neither Publish nor Deny, that another keeper's draft still offers both, and that Edit remains; they fail without the view change.

### WR-05: The 24-hour return rule lives in two places

**Files modified:** `QuestBoard.Domain/Models/Shop/ShopReturnPolicy.cs` (new), `QuestBoard.Domain/Services/ShopService.cs`, `QuestBoard.Service/ViewModels/ShopViewModels/UserTransactionViewModel.cs`, `QuestBoard.UnitTests/Services/ShopServiceTests.cs`, `QuestBoard.UnitTests/Models/ShopReturnPolicyTests.cs` (new)
**Commit:** d3e04d5f
**Applied fix:** Added a public domain type `ShopReturnPolicy` holding the 24-hour `Window` and `IsWithinReturnWindow(purchasedAtUtc, nowUtc)`. `ShopService.ReturnOrSellItemAsync` and `UserTransactionViewModel.CanReturn` both use it, so the window and the inclusive boundary are defined once. Return-or-Sell semantics are unchanged (still inclusive at exactly 24h; same refund maths). The clock reads stay where they were (a real-instant comparison, not a board calendar day), so the existing guard that pins one `UtcNow` read on the `CanReturn` line, and `AmbientClockSeamTests`, are untouched and pass. Unit tests added for the policy boundary and a service-versus-view-model agreement test around the window.

## Not done (needs a behaviour decision)

The second half of WR-05, closing the gap where a Return slip rendered at hour 23:59 and submitted after hour 24 is silently treated as a half-price sell (and the flash message then says "sold"), was not changed. Closing it means choosing between a confirmation on slips near the limit, a different success message, or honouring the render-time decision on the server, and each alters what a POST does. The user-facing prose "within 24h" in `_ShopPurchaseHistory.cshtml` and the "No returns after 24 hours" flavour line in `Views/Shop/Index.cshtml` are literal text and were also left as they are; they could be derived from `ShopReturnPolicy.Window` if wanted.

## Verification

Ran in the isolated worktree (`.claude/worktrees/rf-99-*`), which was fast-forwarded onto `milestone/v9-rolling-improvements` afterwards. Full `dotnet build`: succeeded, 0 errors. Full test run:
- Unit: 1446 passed, 0 failed (baseline 1433, plus 13 new)
- Integration: 2607 passed, 16 skipped, 0 failed (baseline 2595 passed with 16 skipped, plus 12 new)

---

_Fixed: 2026-11-17_
_Fixer: Claude (gsd-code-fixer)_
_Iteration: 1_
