---
phase: 99-shop-and-shop-management-ui-conversion
plan: 21
subsystem: shop-service
tags: [shop, validation, quantity, tampering, tdd]
requires: ["99-18"]
provides:
  - a requested quantity below 1 refused by PurchaseItemAsync, ReturnOrSellItemAsync and SellItemToShopAsync before any repository call
  - unit and integration facts that pin the refusal, the stock convention and the quantity-1 path
affects: [99-19]
tech-stack:
  added: []
  patterns: [one private static guard helper called as the first statement of each quantity-taking service method, crafted-POST integration facts that require exactly 302]
key-files:
  created: []
  modified:
    - QuestBoard.Domain/Services/ShopService.cs
    - QuestBoard.Domain/Interfaces/IShopService.cs
    - QuestBoard.UnitTests/Services/ShopServiceTests.cs
    - QuestBoard.IntegrationTests/Controllers/ShopControllerIntegrationTests.cs
    - QuestBoard.IntegrationTests/Tests/ShopConversionGuardTests.cs
decisions:
  - "The guard is one private static helper called first in each of the three methods, so the check and its message live in one place and sit before every repository read."
  - "The item stock convention (-1 unlimited, 0 sold out, above 0 limited) is untouched; only the requested quantity argument is validated."
  - "No controller change: all three actions already turn the InvalidOperationException into TempData Error and a redirect."
metrics:
  duration: "about 20 minutes of wall clock (sequential single pass)"
  completed: 2026-11-16
status: complete
actuals:
  tokens: 9500
  tasks: 2
  commits: 2
---

# Phase 99 Plan 21: Shop quantity validation Summary

A crafted request with a quantity of 0 or below is now refused with "Quantity must be at least 1." by the shop service before it reads or writes anything, so stock and the transaction ledger can no longer be bent by a hand-made post; the item stock convention is exactly as it was.

## What changed

- **The guard.** `ShopService` has a private static `EnsureQuantityIsAtLeastOne(int quantity)` that throws `InvalidOperationException("Quantity must be at least 1.")` when the quantity is below 1. It is the first statement of `PurchaseItemAsync`, `ReturnOrSellItemAsync` and `SellItemToShopAsync`, before the first repository read, so a refused request reads nothing, changes no stock and records no transaction. A one-line comment says why: a hand-made post can carry any number and the forms' minimum is only a browser hint.
- **Doc comments** in `IShopService.cs` now name a requested quantity below 1 in the throw sentences of Purchase and Return or Sell, and Sell to Shop gained a throw sentence (quantity below 1 or unpublished item).
- **Guard list.** `ShopConversionGuardTests.PhaseSourceFiles` gained `ShopService.cs`, `IShopService.cs`, `ShopServiceTests.cs` and `ShopControllerIntegrationTests.cs` (append-only), so the planning-reference scan covers them.

## Controller reading (nothing changed)

| Action | Catch | Redirect target |
|---|---|---|
| `Purchase` | `catch (InvalidOperationException ex) { TempData["Error"] = ex.Message; ... }` | `Details` with the item id |
| `Sell` | same | `Index` (`/Shop`) |
| `SellToShop` | same | `Details` with the item id |

`_Toasts.cshtml` renders `TempData["Error"]` in both layouts. The integration facts confirm it end to end: each crafted post answers 302 to the expected page, and that page shows the message. The phone's Return and Sell forms post the same hidden `id` and `quantity` fields as desktop, so the phone is covered by the same checks. `git diff` from the plan start over `QuestBoard.Repository` and `QuestBoard.Service` is empty; no `.csproj` changed.

## Stock convention unchanged

`git diff <plan start> -- QuestBoard.Domain/Services/ShopService.cs` removes 0 lines (16 added: the helper with its comment, and three guard calls with a blank line each). The stock checks, sold-out and insufficient-stock messages, 24-hour return rule, refund and sell-price arithmetic are all untouched. Regression facts pin it: quantity 1 on an unlimited item (-1) leaves stock at -1, calls no update and adds one transaction of quantity 1 and price 40; on a limited item (3) it goes to 2 with one update; a sold-out item (0) still answers "This item is sold out."; and the integration control goes 5 to 4 and -1 to -1.

## Facts added

QuestBoard.UnitTests, `ShopServiceTests` (+9 passing rows):

- `PurchaseItemAsync_QuantityBelowOne_IsRefusedBeforeAnyRepositoryCall` (0, -1)
- `ReturnOrSellItemAsync_QuantityBelowOne_IsRefusedBeforeAnyRepositoryCall` (0, -1)
- `SellItemToShopAsync_QuantityBelowOne_IsRefusedBeforeAnyRepositoryCall` (0, -1)
- `PurchaseItemAsync_OneOfAnUnlimitedItem_LeavesStockAtMinusOneAndRecordsThePurchase`
- `PurchaseItemAsync_OneOfALimitedItem_TakesOneFromStock`
- `PurchaseItemAsync_OfASoldOutItem_IsStillRefusedAsSoldOut`

Each refusal asserts the throw and message plus `DidNotReceiveWithAnyArgs` on `GetByIdAsync`, `UpdateAsync`, `GetTransactionWithDetailsAsync`, `GetTransactionsByUserAsync` and `AddAsync`, so a check placed after a repository read fails it.

QuestBoard.IntegrationTests, `ShopControllerIntegrationTests` (+8 passing rows):

- `Post_QuantityBelowOne_IsRefusedWithTheErrorAndChangesNothing` (Purchase, Sell, SellToShop, each with 0 and -1): valid antiforgery token, exactly 302 (never 400), location checked, the landed page shows the message, stock and the member's transaction row count unchanged.
- `Purchase_OfOne_StillGoesThroughAndKeepsTheStockConvention` (stock 5 goes to 4, stock -1 stays -1): 302 to `/Shop`, one Purchase row of quantity 1.

## RED evidence

Before the guard, with the facts in place and the code as it was:

- Unit: all 6 refusal rows failed, each with the wrong message because the guard did not exist: Purchase showed "Item is not available for purchase.", Return or Sell showed "Original purchase transaction not found or does not belong to the user." and Sell to Shop showed "Item is not available for sale." (the repository fakes return nothing, so the old code reached its first repository read). The 3 controls passed.
- Integration: all 6 refusal rows failed. Sell and SellToShop returned a redirect but the landed page did not carry "Quantity must be at least 1."; Purchase redirected to `/Shop` (the success path, a negative or zero purchase was accepted) instead of the item's Details. The 2 quantity-1 controls passed.

After the guard: all 10 unit facts and the 248 filtered integration facts (`ShopControllerIntegrationTests`, `ShopConversionGuardTests`, `ShopRenderTests`) passed.

## Tests

- Unit tests 1433 passed (1424 before, +9).
- Integration tests 2568 passed, 16 skipped (SQL Server migrator tests), 2584 total (2560 passed before, +8).
- Planning-reference scan over the four changed non-guard files prints 0 for each; `PhaseSourceFiles_CarryNoPlanningReferences` passes with the four new entries.

## Untouched

No view, sheet, script, controller, repository, entity, migration, project file or appsettings change; no EF package added; no new ambient clock read; the database was not touched and no app, fixture or purchase tool was run (the stop-app call for port 8320 found nothing running).

## Deviations from Plan

**1. [Rule 3 - Process] One commit instead of a RED commit then a GREEN commit.** The plan asks for a single commit of exactly the five files after the failing facts were seen; the RED run was observed but not committed separately. All five files are in one `feat` commit.

**2. [Note] `IShopService.cs` edits replace two existing doc lines.** The "removes no line" rule applies to `ShopService.cs` (0 removals, shown above); the interface doc comments were reworded as the plan directs.

**3. [Tool quirk] `state.advance-plan` moved STATE to "Plan: 20 of 21"** (it counted by number, 21 being the plan just finished). The next plan to run is 99-19 (Wave 20 holds 99-19 after this Wave 19 plan), so STATE was corrected by hand to "Plan: 19 of 21". ROADMAP ticks 99-21 and shows 19/21.

Otherwise none; plan executed as written.

## For the closing sweep

Server quantity validation on Purchase, Return or Sell, and Sell to Shop is **fixed in this phase** on the operator's answer "20:fix in phase 99", in commit `5b77dddc`. So the quantity row that 99-19 writes into `deferred-items.md` records it as **Resolved in this phase (99-21)**, not as deferred. The item stock convention (-1 unlimited, 0 sold out, above 0 limited) is unchanged and was an explicit must-keep. Old zero- or negative-quantity rows that may already exist are not migrated (no data change by design); the return and sell path already refuses a purchase with nothing remaining before it divides by the purchase quantity.

## Known Stubs

None.

## Threat Flags

None. T-99-60 (crafted quantity) is mitigated by the guard and its facts; T-99-61 (stock convention regression) by the zero-removal diff and the regression facts.

## Self-Check: PASSED

- `QuestBoard.Domain/Services/ShopService.cs` contains the guard; commit `5b77dddc` is in the log; both full suites are green.
