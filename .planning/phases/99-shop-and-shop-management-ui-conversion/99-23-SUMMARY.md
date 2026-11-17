---
phase: 99-shop-and-shop-management-ui-conversion
plan: 23
subsystem: shop-server-rules
tags: [shop, ef-core, automapper, change-tracker, authorization, board-clock, availability-window, tdd]
requires: ["99-22"]
provides:
  - limited-stock Return and Sell from the purchase history refund and restock again (shop domain-to-entity maps leave navigations to the store)
  - ShopManagement Archive accepts only a Published item, for every role
  - one on-sale rule (ShopItemAvailability) judged on IBoardClock.Now for the shop list, Purchase, Sell to Shop and Details
affects: [v6.4.0 release hand-off]
tech-stack:
  added: []
  patterns:
    - domain-to-entity AutoMapper maps ignore navigations so BaseRepository.UpdateAsync writes only own columns and foreign keys over a tracked entity
    - a shared domain rule handed the board-local time by its caller (no clock of its own), the QuestExtensions/MoveEligibility shape
    - repository list queries filtered on a handed board-local time through one private query helper
key-files:
  created:
    - QuestBoard.Domain/Models/Shop/ShopItemAvailability.cs
    - QuestBoard.UnitTests/Services/ShopServiceStockWriteTests.cs
    - QuestBoard.UnitTests/Models/ShopItemAvailabilityTests.cs
    - QuestBoard.UnitTests/Repository/ShopRepositoryAvailabilityTests.cs
    - QuestBoard.IntegrationTests/Tests/ShopAvailabilityWindowTests.cs
  modified:
    - QuestBoard.Repository/Automapper/EntityProfile.cs
    - QuestBoard.Service/Controllers/Shop/ShopManagementController.cs
    - QuestBoard.Domain/Services/ShopService.cs
    - QuestBoard.Domain/Interfaces/IShopRepository.cs
    - QuestBoard.Repository/ShopRepository.cs
    - QuestBoard.Service/Controllers/Shop/ShopController.cs
    - QuestBoard.Service/ViewModels/ShopViewModels/ShopItemDetailsViewModel.cs
    - QuestBoard.Service/ViewModels/ShopViewModels/ShopItemViewModel.cs
    - QuestBoard.Service/Automapper/ViewModelProfile.cs
    - QuestBoard.Service/Views/Shared/_ShopItemDetailsContent.cshtml
    - QuestBoard.Service/Views/Shop/Details.cshtml
    - QuestBoard.IntegrationTests/Tests/ShopFormPostEffectTests.cs
    - QuestBoard.IntegrationTests/Controllers/ShopManagementControllerIntegrationTests.cs
    - QuestBoard.IntegrationTests/Tests/ShopRenderTests.cs
    - QuestBoard.IntegrationTests/Tests/ShopConversionGuardTests.cs
    - QuestBoard.UnitTests/Services/ShopServiceTests.cs
    - QuestBoard.UnitTests/Architecture/AmbientClockSeamTests.cs
    - .planning/phases/99-shop-and-shop-management-ui-conversion/deferred-items.md
    - .planning/phases/99-shop-and-shop-management-ui-conversion/99-VALIDATION.md
decisions:
  - "Limited-stock Return/Sell fix sits in EntityProfile, not BaseRepository or ShopService: the ShopItem->ShopItemEntity map ignores CreatedByDm, Group and Transactions, and UserTransaction->UserTransactionEntity ignores User, ShopItem and OriginalTransaction; every write stays on the change tracker."
  - "ShopManagement Archive accepts only Published, for every role (DM and Admin alike); Reopen unchanged. Rejected: Reopen restoring Draft for never-published items (needs a publication record, i.e. a new column and migration)."
  - "One on-sale rule: Published and inside the optional window, both ends inclusive, judged on IBoardClock.Now (board-local). The shop list moves from an ambient UTC comparison onto the board clock with it."
  - "Shop/Details: an item whose sale has not started is judged like a Draft (DungeonMasterOnly policy, else the missing-id 404); a closed item stays open like Archived. Neither shows Purchase or Sell to Shop."
  - "Sell to Shop follows the same rule as Purchase (refused outside the window); Return and Sell from the purchase history keep no status or window check."
metrics:
  duration: "about 25 minutes of wall clock (18:26Z to 18:51Z, sequential single pass)"
  completed: 2026-11-17
status: complete
actuals:
  tokens: 31000
  tasks: 5
  commits: 5
---

# Phase 99 Plan 23: Stock, review and availability gaps Summary

Limited-stock Return and Sell from the purchase history refund and restock again because the shop domain-to-entity maps no longer copy navigations over tracked entities; ShopManagement Archive refuses anything not Published, closing the Archive-then-Reopen review bypass; and one availability rule, judged on the board clock, now decides the shop list, Purchase, Sell to Shop and Shop/Details. All three were done on the operator's decision "Fix + 2 gaps".

## 1. Limited-stock Return and Sell (the validation audit's blocker)

**Root cause.** `ShopService.ReturnOrSellItemAsync` loads the purchase with `GetTransactionWithDetailsAsync` (Include ShopItem, then CreatedByDm) and the buyer's history with `GetTransactionsByUserAsync`. Both leave rows tracked in the request's `QuestBoardContext`, and EF relationship fix-up puts those purchases into the tracked item's `Transactions`. `repository.GetByIdAsync` is `FindAsync`, so it returns that same tracked entity, mapped to a domain item whose `Transactions` holds the purchases. `BaseRepository.UpdateAsync` then calls `Mapper.Map(model, entity)` over the tracked entity, and the `ShopItem -> ShopItemEntity` map copied navigations by name: it cleared the tracked `Transactions` and refilled it with untracked copies. Clearing severed the tracked purchases from a required relationship (`ShopItemId` non-nullable, `NoAction`), so `SaveChanges` threw "The association between entity types 'ShopItemEntity' and 'UserTransactionEntity' has been severed". The `Sell` action catches `InvalidOperationException` as a refusal, so EF's message became the toast. Unlimited items skip `UpdateAsync`, which is why they worked.

**Why the fix sits in the map.** The fault is in what a mapped domain model is allowed to overwrite, not in any caller. `PurchaseItemAsync`, `SellItemToShopAsync` and the management actions (Edit, Publish, Archive, Deny, Reopen) share the `GetByIdAsync` then `UpdateAsync` pattern and worked only because nothing in their requests had loaded the item's purchases first. Changing `BaseRepository` would have touched every repository; a raw or bulk update, `Attach` or a hand-set state would leave the change tracker, which `.claude/architecture.md` forbids. So, in `EntityProfile` only, with the same shape as the Quest and PlayerSignup precedents in that file:

- `ShopItem -> ShopItemEntity` ignores `CreatedByDm`, `Group` and `Transactions`;
- `UserTransaction -> UserTransactionEntity` ignores `User`, `ShopItem` and `OriginalTransaction`;
- scalars and foreign keys (`CreatedByDmId`, `GroupId`, `ShopItemId`, `UserId`, `OriginalTransactionId`) still map, the enum casts stay, and the entity-to-domain maps are unchanged, so reads still load navigations. Every creation site sets foreign keys only, so no write depended on mapping a navigation.

`opt.Ignore()` count in `EntityProfile.cs`: 30 at PLAN_START, 36 after (+6).

## 2. Archive only from Published (review bypass)

**Rule as built.** `ShopManagementController.Archive`, after the existing not-found check, refuses an item whose status is not Published: `TempData["Error"] = "Only published items can be archived."` and a redirect to `Index`, the shape `Reopen` already uses. No creator check (any Dungeon Master may still take any published item off sale, as the All Other Published Items table offers) and no role-name check (an Admin gets the same rule). `Reopen`, the service and the views are unchanged. The controller diff only adds lines (0 removed).

**Reasoning.**
- It makes the server match what every list has always offered: desktop My Items and All Other Published Items, and the phone list, show Archive only on Published rows and Reopen only on Archived rows. Archived has always meant "taken off sale after being published", and 99-22's Details rule keeps Archived open to members because such items were on sale once; this rule makes that true by construction. The only thing it removes is a hand-made post.
- With Archive limited to Published, Reopen to Published is always a restoration of a reviewed item (or an auto-published Common/Uncommon one), so Reopen needs no change.
- Admin: same rule. Publish already refuses an Admin's own item, so an Admin archiving and reopening their own Draft would be the same bypass. Admin keeps Delete of any status.
- **Rejected alternative:** Reopen restoring Published only for previously published items, Draft otherwise. That needs a record of earlier publication the schema does not have (a new column and migration), and until then an archived Draft would still show to members under the Archived rule.
- **Not widened:** editing an Archived item and reopening it publishes the edit without review, which is no more than editing a Published item already does (Edit keeps Published). Existing semantics.
- **Accepted residual:** an Archived row archived from Draft or Denied before this fix can only come from a hand-made post, cannot be told apart, and would still reopen to Published.

## 3. One availability rule on the board clock

**The rule.** `QuestBoard.Domain/Models/Shop/ShopItemAvailability.cs`, a public static class shaped like `ShopReturnPolicy`:
- `HasOpened(ShopItem item, DateTime boardNow)`: no start, or start at or before `boardNow`;
- `IsOnSale(ShopItem item, DateTime boardNow)`: Published, opened, and no end or end at or after `boardNow`.

It holds no clock; its caller hands it `IBoardClock.Now`, the board's local wall-clock time, because the window values are the board-local `datetime-local` values a Dungeon Master typed.

**Correction: the Index was not on the board clock.** `ShopRepository`'s three list queries compared those wall-clock values with an ambient UTC read. Reusing "the Index rule" therefore meant moving the Index onto the board clock too, otherwise the list and the by-id check would disagree by the board's UTC offset around every boundary. Visible effect: a windowed item now goes on and off the list at the board-local time the Dungeon Master typed, instead of one to two hours late in Europe/Amsterdam.

**Wiring.**
- `ShopService` takes `IBoardClock boardClock` (appended to the primary constructor; DI already registers it). `PurchaseItemAsync` and `SellItemToShopAsync` refuse an item that is missing or not `ShopItemAvailability.IsOnSale(item, boardClock.Now)` with their existing messages ("Item is not available for purchase." / "Item is not available for sale."), after the quantity check and before any change. `GetPublishedItemsAsync`, `GetItemsByTypeAsync` and `GetPagedPublishedItemsAsync` hand `boardClock.Now` to the repository.
- `IShopRepository`: the three list methods take `DateTime boardNow` before the token; summaries reworded to "whose availability window includes the given board-local time".
- `ShopRepository`: one private static `OnSale(query, boardNow)` helper (Published, start null or `<= boardNow`, end null or `>= boardNow`) used by all three; the repository reads no clock.
- `IShopService` and its callers are unchanged.

**Sell to Shop decision.** Refused outside the window, as Purchase is: the shop trades an item in both directions only while it is on sale. Accepting would pay out gold and raise the stock of a listing the board does not show.

**Return and Sell from the history stay open.** `ReturnOrSellItemAsync` keeps no status or window check (a one-line comment says why: what a member bought can always be handed back), which is the "expired items must stay reachable after purchase" constraint 99-22 recorded. The 24-hour window and its posting edge are unchanged.

**Details (page and dialog), consistent with 99-22.**

| Item state at the board's now | Plain member | `DungeonMasterOnly` policy (creating DM, another DM, Admin, SuperAdmin) |
|---|---|---|
| Published, in window or no window | 200, Purchase and Sell to Shop forms (unchanged) | same |
| Sale not started (start after now), any status | 404, identical status and body to a missing id | 200, notice "This item is not currently available for purchase.", no buy or sell form (the View Details link never dead-ends) |
| Sale ended (end before now) | 200 like Archived, the notice, no buy or sell form (a refused purchase lands here with its toast) | same |
| Draft / Denied / Archived | unchanged from 99-22 | unchanged |

`ShopController` takes `IBoardClock boardClock`, reads `boardClock.Now` once in `Details`, treats an item as unreleased when Draft, Denied or `!HasOpened`, keeps the one policy call (`AuthorizeAsync(User, "DungeonMasterOnly")` count 1, `IsInRole` count 0) and the one shared `NotFound()`, and sets `ShopItemDetailsViewModel.IsOnSale` from the rule. `ViewModelProfile` ignores `IsOnSale` on the details map. `_ShopItemDetailsContent.cshtml` (now `@model ShopItemDetailsViewModel`, as is `Details.cshtml`, matching its phone twin) shows the Purchase form when on sale with stock, keeps the Draft notice, shows out of stock when on sale with stock 0, the existing not-available notice for every remaining case, and the Sell to Shop block only when on sale. No wording, class, markup, sheet or script was added. `ShopItemViewModel.IsAvailable` (an unused third copy of the rule on the UTC clock) was removed, and with it the `ShopItemViewModel.cs` entry in `ShopConversionGuardTests.AllowedViewModelClockReads`, so the scanner now refuses any UTC read in that file.

## Facts added and re-pointed

| File | Facts | Cases |
|---|---|---|
| `QuestBoard.UnitTests/Services/ShopServiceStockWriteTests.cs` (new) | limited-stock Return or Sell (2 h / 80 gp, 30 h / 40 gp); unlimited control; Purchase after the buyer's history was read; Sell to Shop after the same read; repository update after the purchase+item+creator load writes only name and stock | 6 |
| `QuestBoard.IntegrationTests/Tests/ShopFormPostEffectTests.cs` | four limited-stock rows of `HistorySlipForm_SubmittedAsRendered_RefundsTheRemainingQuantityAndRestocks` un-skipped; skip-reason constant deleted | 4 un-skipped (class: 10 passed, 0 skipped) |
| `QuestBoard.IntegrationTests/Controllers/ShopManagementControllerIntegrationTests.cs` | Archive refusal (DM/Admin x Draft/Denied/Archived); Archive-then-Reopen bypass (DM/Admin x Draft/Denied); Published still archives (DM/Admin x own/other keeper's); archived-through-the-post item reopens | 6 + 4 + 4 + 1 = 15 |
| `QuestBoard.IntegrationTests/Tests/ShopRenderTests.cs` | management lists offer Archive only on Published and Reopen only on Archived, and on every such item (desktop/phone x DM/Admin) | 4 |
| `QuestBoard.UnitTests/Models/ShopItemAvailabilityTests.cs` (new) | the rule at board-local 2026-09-06 00:30: no window; start at now; start +1 min; start at midnight; end at now; end 00:29 and 23:59 the day before; Draft/Denied/Archived in window; start after end | 13 |
| `QuestBoard.UnitTests/Services/ShopServiceTests.cs` | `CreateService` now returns a `FakeBoardClock` too; Purchase and Sell to Shop refused before the start and after the end (nothing updated or recorded); accepted with start, end, or both exactly at now; Return or Sell still refunds on a closed-window item and on an Archived item; the three list calls reach the repository with exactly `clock.Now` | 2 + 3 + 2 + 3 + 2 + 1 = 13 |
| `QuestBoard.UnitTests/Repository/ShopRepositoryAvailabilityTests.cs` (new) | the three list queries return exactly the ids `IsOnSale` accepts, at three board times around the boundaries (12 items across statuses); a later time changes the set | 3 + 1 = 4 |
| `QuestBoard.UnitTests/Architecture/AmbientClockSeamTests.cs` | guarded paths `ShopItemAvailability.cs`, `ShopService.cs`, `ShopRepository.cs`, `ShopController.cs`; `RealInstantExemptions` for `ShopService.cs` (`TransactionDate`, `DeniedAt`); board-clock consumers `ShopService.cs`, `ShopController.cs`; new fact `ShopItemAvailability_TakesTheBoardLocalTimeRatherThanResolvingOne`. Additions only (0 lines removed) | +4 guarded rows, +2 consumer rows, +1 fact |
| `QuestBoard.IntegrationTests/Tests/ShopAvailabilityWindowTests.cs` (new) | pinned at 2026-09-05T22:30Z, board Europe/Amsterdam (board 00:30 on the 6th, UTC still the 5th), seven items (no window; opens 00:30; opens 00:00; opens 00:31; closes 00:30; closed 00:29; closed 23:59 on the 5th). Index per layout; member Details on desktop page, phone page and dialog with missing-id parity for the scheduled item; reviewer theory (creator, other DM, Admin, SuperAdmin x page, dialog); Purchase and Sell to Shop for every item with refused-post landings; history Return on the item closed last night | 2 + 1 + 8 + 1 + 1 = 13 |
| `QuestBoard.IntegrationTests/Tests/ShopRenderTests.cs` (re-pointed) | `ShopDetails_HalfOpenWindow_RendersTheMatchingLine`: the "Available starting" row (start 2026-12-01, still ahead) is now viewed by the item's own Dungeon Master (`SeedEditablePageAsync(..., Published)` plus `UpdateItemAsync`); the "Available until" row keeps the member. Assertions unchanged | 2 (re-pointed) |
| `QuestBoard.IntegrationTests/Tests/ShopConversionGuardTests.cs` | `AllowedViewModelClockReads` shrank to the return window only, comment and `because` text reworded; `PhaseSourceFiles` grew by the 14 files this plan created or edited | guard only |

New tests: unit +43 (6 + 13 + 13 + 4 + 7 guard rows/fact), integration +32 (15 + 4 + 13), and 4 formerly skipped integration cases now run.

## RED evidence and bite checks

**Task 1 RED (before the map change).**
- Unit, `ShopServiceStockWriteTests`: 5 failed, 1 passed. Failing: `ReturnOrSell_OnLimitedStock_RecordsTheRefundAndPutsTheQuantityBackOnTheShelf` (2 h/80 and 30 h/40), `Purchase_AfterTheBuyersHistoryWasReadInTheSameRequest_TakesTheStock`, `SellToShop_AfterTheSellersHistoryWasReadInTheSameRequest_AddsTheStock`, `UpdatingAnItem_AfterItsPurchaseAndCreatorWereLoaded_WritesOnlyTheItemsOwnColumns`, each with `System.InvalidOperationException: The association between entity types 'ShopItemEntity' and 'UserTransactionEntity' has been severed, but the relationship is either marked as required or is implicitly required because the foreign key is not nullable.` The unlimited control passed. The Purchase and Sell to Shop facts did fail before the fix, confirming the shared path.
- Integration, `ShopFormPostEffectTests` with the skips removed: 4 failed, 6 passed. The four limited-stock history rows (desktop and phone, Return 2 h and Sell 30 h) failed on the error toast, which read "The association between entities 'ShopItemEntity' and 'UserTransactionEntity' with the key value '{ShopItemId: 1}' has been severed, ...".

**Task 2 RED (before the Archive check).** 10 failed, 24 passed: all 6 `Archive_Post_AnItemThatIsNotPublished_IsRefusedAndKeepsItsStatus` cases (landing page lacked the refusal) and all 4 `ArchiveThenReopen_OnTheCreatorsOwnUnreviewedItem_NeverPublishesIt` cases ("expected Draft/Denied, but found Published"). Still-works, Reopen and the 4 list cases passed.
**Task 2 bite check** (status check disabled with `false &&`, then restored from a copy; controller diff verified as additions only): the same 10 cases failed, 20 passed in the management class; restored filter green (30/30).

**Task 3 RED.** The unit project did not compile: `ShopItemAvailability` did not exist (13 references in `ShopItemAvailabilityTests`, 1 in `ShopRepositoryAvailabilityTests`) and the repository and `ShopServiceTests` calls had no overload taking `boardNow` (CS1501). The guard would also have failed on `ShopRepository.cs`, which held 6 `DateTime.UtcNow` reads at PLAN_START.

**Task 4 RED (after Task 3, before the Details change).** `ShopAvailabilityWindowTests`: 9 failed, 4 passed. Failing: all 8 reviewer cases (200 with "Purchase Item" and no notice) and the member Details fact (the scheduled item answered 200, not 404). Passing already: both Index cases, Purchase and Sell to Shop, and the history Return.
**Task 4 bite check 1** (window term dropped from `IsUnreleased`, restored from a copy): the member Details fact failed ("an item whose sale has not started is hidden from a member (desktop page), but found 200"); 12 others passed.
**Task 4 bite check 2** (`ShopService` judging against `boardClock.Now.AddHours(-2)`, the UTC equivalent at the pinned summer instant, restored from a copy): 3 failed. Both Index cases listed the wrong set for "Lantern Opening Now" and "Lantern Opened At Midnight" (missing) and "Lantern Closed A Minute Ago" and "Lantern Closed Last Night" (shown); the Purchase and Sell to Shop fact refused the two opening items and moved stock for the two closed items (their refusals redirected to `/Shop` as successes instead of to Details). `git diff` confirmed neither bite was left behind before the commit.

## Build and suites

- `dotnet build` (repository root): Build succeeded, 0 errors; the 73 warnings are pre-existing, none in a file this plan touched.
- `dotnet test QuestBoard.UnitTests`: **1489 passed, 0 skipped, 0 failed** (baseline 1446, +43).
- `dotnet test QuestBoard.IntegrationTests`: **2649 passed, 16 skipped, 0 failed** (baseline 2613 passed, 20 skipped; +32 new, +4 un-skipped; the 16 skipped are the SQL Server suites only). No `0x80131506` flake occurred.

## Records

- **deferred-items.md.** Resolved: "Return and Sell from the purchase history fail on an item with limited stock" (new row, root cause, fix, 4 cases green, `7a323074`), "Archive accepts any status, and Reopen publishes any archived item" (`c53e7e59`) and "The availability window is not enforced by id" (`b6c80468`, `fd84daad`), each naming 99-23 and "Fix + 2 gaps". Deferred: the cross-board deep-link row widened to items whose sale has not started and marked known debt by operator decision ("Fix + 2 gaps", 99-23); new known-debt row for the 24-hour Return edge; two new rows "not asked (found by 99-23)": EF's tracking error reaching the toast through the refusal catch, and the availability line showing only the date while the rule compares the typed time. One sentence appended to the Gate 2 paragraph.
- **99-VALIDATION.md.** D-05 row `✅ green` (command cell still names `~ShopFormPostEffectTests`); two new `D-11 (operator decision)` rows, Archive only from Published and the availability window on the board clock, both `✅ green`; the Manual-Only operator-decision row answered in place; "Resolved by 99-23" under Gap 2 with the root cause, `7a323074` and the suite counts; sign-off line ticked without the blocked note; approval "validated"; frontmatter `nyquist_compliant: true` (no Per-Task Verification Map row is partial, red or flaky).
- **WINDOWS.** `windows status` shows no open or waived entry about limited-stock refunds, Archive or Reopen, or the availability window (open: 2, 3, 5, 28, 32; waived: 9, 18, 21, 24, 25, 27, 30, 33). The ledger was left untouched.

## Operator UAT

- As a player, Return or Sell a fresh purchase of a limited-stock item from the purchase history: the refund is credited and the stock goes back up, with no error toast.
- As a Dungeon Master, a Draft row offers no Archive; Archive on a published item, then Reopen, restores it to Published.
- As a player, a scheduled item's `/Shop/Details/{id}` is 404 until its start (board-local time), then it is listed and buyable; as a Dungeon Master it opens before the start with "This item is not currently available for purchase." and no buy or sell form.

## Release note

The fixes ride the v6.4.0 hand-off, whose pull request the operator has not opened yet. No push, pull request, tag or release was made by this plan.

## Deviations from Plan

None in behaviour. Process notes:
- Task 3's RED was produced by parking the already-written rule file in the scratchpad during the run and restoring it for GREEN, so the compile failure is genuine.
- `ShopAvailabilityWindowTests` wraps the Index and Purchase/Sell to Shop loops in an `AssertionScope`, so bite check 2 reports every wrong item rather than stopping at the first.
- `ViewModelProfile.cs` is a CRLF file; the edit was re-applied preserving its line endings after a first pass would have normalised them (caught before committing; the committed diff is 3 insertions, 1 deletion).

## Known Stubs

None.

## Threat Flags

None: no new endpoint, auth path or schema change. The register's mitigations (T-99-70 to T-99-76) are in place and the accepted items (T-99-77 to T-99-80) are recorded in deferred-items.md.

## Self-Check: PASSED

All five created source and test files and this SUMMARY exist; fix commits 7a323074, c53e7e59, b6c80468 and fd84daad are in the branch history; no planning reference appears in any source file this plan touched.
