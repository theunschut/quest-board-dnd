---
phase: 99-shop-and-shop-management-ui-conversion
plan: 24
subsystem: shop-server-rules
tags: [shop, authorization, review-rule, adminonly-policy, management-lists, tdd]
requires: ["99-23"]
provides:
  - one shared review rule (ShopItemReview) for Create and Edit; an edit never publishes, and a Published or Archived item goes back to review only when an edit raises it from Common or Uncommon into Rare or above (amended by the operator after the first build)
  - a truthful Edit toast that says when the item went back to review
  - ShopManagement Delete refuses another keeper's item to a Dungeon Master who is not an Admin; Admins and SuperAdmins keep every right through the AdminOnly policy
  - both management lists offer Delete only where the server accepts it, with conditions that state the rule
affects: [v6.4.0 release hand-off]
tech-stack:
  added: []
  patterns:
    - a small public static domain rule over plain enum values, shared by every caller, with a unit fact per cell (the ShopItemAvailability shape)
    - Admin rights judged only by the AdminOnly policy (SuperAdmin bypass inside the handler), ownership judged in the controller from the current user
key-files:
  created:
    - QuestBoard.Domain/Models/Shop/ShopItemReview.cs
    - QuestBoard.UnitTests/Models/ShopItemReviewTests.cs
  modified:
    - QuestBoard.Service/Controllers/Shop/ShopManagementController.cs
    - QuestBoard.Service/Views/ShopManagement/Index.cshtml
    - QuestBoard.Service/Views/ShopManagement/Index.Mobile.cshtml
    - QuestBoard.IntegrationTests/Controllers/ShopManagementControllerIntegrationTests.cs
    - QuestBoard.IntegrationTests/Tests/ShopRenderTests.cs
    - QuestBoard.IntegrationTests/Tests/ShopConversionGuardTests.cs
    - .planning/phases/99-shop-and-shop-management-ui-conversion/99-SECURITY.md
    - .planning/phases/99-shop-and-shop-management-ui-conversion/deferred-items.md
    - .planning/phases/99-shop-and-shop-management-ui-conversion/99-VALIDATION.md
decisions:
  - "An edit never publishes: Draft and Denied stay Draft at any rarity (operator decision \"Fix first\")."
  - "A Published or Archived item goes back to Draft only when the edit raises it from a rarity that needs no review to one that does (NeedsReview(old) false, NeedsReview(new) true); kept at Rare or above, or lowered, it keeps its status (operator decision \"Only on a rarity raise\", replacing the first build's any-edit-at-Rare-or-above rule)."
  - "T-99-89 (the content of an already reviewed Rare-or-above item can change while it stays on sale) accepted by the operator (AR-99-18)."
  - "Create keeps auto-publishing Common and Uncommon; Create and Edit read one rule, ShopItemReview.NeedsReview, so they cannot drift apart (T-99-88 accepted)."
  - "A Dungeon Master who is not an Admin deletes only an item they created, and only while Draft or Denied; Admin rights come only from the AdminOnly policy (SuperAdmin bypass included), never a role-name check."
  - "T-99-87 (a Dungeon Master publishing their own edit of another keeper's item) accepted pending the operator's choice: closing it needs a who-sent-to-review column and migration or taking Edit off other keepers' items."
metrics:
  duration: "about 20 minutes of wall clock (19:27Z to 19:47Z, sequential single pass)"
  completed: 2026-11-17
status: complete
actuals:
  tokens: 50500  # first build 14500; rule amendment about 36000 (code diff 34109 chars, docs diff about 110000 chars, /4)
  tasks: 4
  commits: 6  # first build 4; rule amendment 2 (59c37604 and its docs commit)
---

# Phase 99 Plan 24: Shop edit-review and delete-ownership gaps Summary

An edit can no longer put a shop item on sale: one domain rule (`ShopItemReview`) now gives Create and Edit their status, an item that an edit raises from Common or Uncommon into Rare or above goes back to another Dungeon Master's review wherever it was (the operator's amended rule), the toast says so, and a Dungeon Master who is not an Admin can delete only their own Draft or Denied item while Admins and SuperAdmins keep every right through the AdminOnly policy.

## 1. The Edit rule, as built (amended)

Who edits does not matter: any Dungeon Master, Admin or SuperAdmin may still edit any item, as the lists offer. "Review-free" is Common or Uncommon (`NeedsReview` false), "reviewed" is Rare, Very Rare or Legendary (`NeedsReview` true). The rule as it stands after the operator's amendment (see "Rule change after operator review" below):

| Edited from | review-free -> review-free | review-free -> reviewed (a raise) | reviewed -> reviewed (kept or moved within) | reviewed -> review-free (a lowering) | Toast on the landing page |
|---|---|---|---|---|---|
| Draft | Draft (was: Published) | Draft | Draft | Draft (was: Published) | "Item updated and resubmitted for review!" |
| Denied | Draft, reason and date cleared (was: Published) | Draft, cleared | Draft, cleared | Draft, cleared (was: Published) | "Item updated and resubmitted for review!" |
| Published | Published | Draft (was: stayed Published) | Published (first build: Draft) | Published | Published: "Item updated successfully!"; Draft: "Item updated and sent back for review. It is off sale until another Dungeon Master publishes it." |
| Archived | Archived | Draft (was: stayed Archived, one Reopen from sale) | Archived (first build: Draft) | Archived | Archived: "Item updated successfully!"; Draft: the sent-back sentence |
| Create, for comparison | Published at once (unchanged) | Draft (unchanged) | | | "Item created successfully!" (unchanged) |

Named cases (Published or Archived): Common -> Uncommon keeps the status; Uncommon -> Rare and Common -> Legendary go to Draft; Rare -> Legendary and Legendary -> Rare keep it; Legendary -> Common keeps it. A Draft or Denied item is a Draft after every one of them.

**Invariant.** An item can be on sale, or archived and so one Reopen from sale, at a rarity that needs review only if a Dungeon Master other than its creator published it at such a rarity. An edit that raises an item into review makes a high-rarity item nobody reviewed at that level, so it goes back to review. An edit that keeps or lowers an already reviewed item does not; its content can change while it stays on sale (T-99-89, accepted by the operator).

`ShopItemReview` (`QuestBoard.Domain/Models/Shop/ShopItemReview.cs`):
- `NeedsReview(rarity)`: true for everything except Common and Uncommon, so a rarity added later needs review by default.
- `StatusOnCreate(rarity)`: Draft when it needs review, otherwise Published.
- `StatusAfterEdit(current, oldRarity, newRarity)`: Published and Archived become Draft when the edit raises the item into review (`!NeedsReview(oldRarity) && NeedsReview(newRarity)`, the private `RaisedIntoReview`) and otherwise keep their status; every other status (Draft, Denied, any future value) gives Draft.

`ShopManagementController`: Create sets `Status = ShopItemReview.StatusOnCreate(...)` and its separate auto-publish block is gone (behaviour unchanged). Edit POST keeps the invalid-form path, the load, the user check and the field copy unchanged; it remembers the previous status and rarity from the stored item before the posted values are copied over (amended), clears a Denied item's reason and date, sets the status from `StatusAfterEdit(previousStatus, previousRarity, item.Rarity)`, saves through `UpdateAsync` (tracked entity, SaveChanges), and picks the toast from the before and after statuses. The controller names no rarity (`ItemRarity.Common|Uncommon` count 0).

**Tensions, flagged for the operator.**
- **Create against Edit.** Create still auto-publishes Common and Uncommon, but Edit never publishes. A Draft or Denied item edited to Common waits for review, while the same item created fresh goes on sale at once. Deleting one's own Draft and creating it again reaches the Create result, but only with review-free content (T-99-88, accepted, AR-99-17).
- **Strictness, as amended: only a raise into review goes back to review.** The first build sent any saved edit at Rare or above back to review, a restock, a price change or a typo fix on a Published Rare item included. The operator's rule "Only on a rarity raise" ends that: a restock or typo fix on a Rare item no longer goes back to review; only raising a Published or Archived item from Common or Uncommon to Rare or above takes it off sale or moves it into the review queue. The remaining residual is that a reviewed Rare-or-above item's content can be changed by any Dungeon Master while it stays on sale; nothing records which fields a reviewer approved, so it is not narrowed further. Accepted risk, decided by the operator (T-99-89, AR-99-18). The toast says when an item went back to review; a notice before the save would be new wording and stays deferred.
- **Residual T-99-87.** A Dungeon Master can still publish their own change to another keeper's item: B raises A's Common item to Legendary (now Draft), then publishes it as the "second" Dungeon Master, because `Publish` refuses only the creator. The rarity-raise amendment does not change this (the raise still goes to Draft, B's Publish is still accepted); B editing A's already reviewed Rare item stays on sale with no Publish at all, which is T-99-89. The own-item route is closed. Closing this one needs either a record of who sent the item to review (a new column and migration, with `Publish` and `Deny` refusing that Dungeon Master too) or taking Edit off other keepers' items, which the operator's rule keeps. Accepted pending the operator's choice (AR-99-16), an unticked Sign-Off line in 99-SECURITY.md and a deferred-items.md row.

## 2. The Delete matrix and the list controls

| Viewer | Own Draft | Own Denied | Own Published | Own Archived | Another keeper's Draft / Denied | Another keeper's Published / Archived |
|---|---|---|---|---|---|---|
| Dungeon Master (not Admin) | deleted | deleted | refused, "Cannot delete items that have been published." | refused, same | refused, "You can only delete your own items." (was: deleted) | refused, "You can only delete your own items." (was: the published message) |
| Admin of the board | deleted | deleted | deleted | deleted | deleted | deleted |
| SuperAdmin (AdminOnly bypass) | deleted | deleted | deleted | deleted | deleted | deleted |

`ShopManagementController.Delete`: the one `AuthorizeAsync(User, "AdminOnly")` call stays the only source of Admin rights (`IsInRole` count 0). When it does not succeed, the controller loads the current user (`Challenge()` when missing), refuses another keeper's item with `TempData["Error"] = "You can only delete your own items."` (the Deny refusal shape), then keeps the existing status refusal. The delete is `shopService.RemoveAsync` as before (FindAsync, Remove, SaveChanges).

**Why the rule sits in the domain and Delete in the controller.** The review rule is pure data over two enums and is needed by both Create and Edit, so it lives in `QuestBoard.Domain` next to `ShopItemAvailability` and is unit-tested without a host. The Delete rule depends on the AdminOnly policy, which resolves the active board's role with the SuperAdmin bypass in `AdminHandler`, a Service-layer authorization handler the Domain cannot see, so ownership is judged in the controller beside that one policy call.

**List controls (rendered output unchanged).** Delete is offered to a Dungeon Master only on their own Denied item; an Admin or SuperAdmin gets it on every listed item that is not a Draft (own Denied, Published and Archived; other keepers' Published and Archived). Neither list has ever offered Delete on a Draft, the viewer's own included, though the server accepts the creator's: a hidden-but-accepted control, left as it was. Other keepers' Denied items are listed nowhere.
- Desktop `Index.cshtml:271`, the All Other Published Items table: `@if (isAdmin)` (the dead Draft branch, which would have offered a non-admin a refused Delete, is gone). My Items and the review table are unchanged.
- Phone `Index.Mobile.cshtml:101`: `@if (isAdmin ? item.Status != ItemStatus.Draft : (item.Status == ItemStatus.Denied && ownItemIds.Contains(item.Id)))`, with the `ownItemIds` comment extended. View diff: desktop +1/-1, phone +3/-2.

## Facts added and changed

| File | Fact | Cases |
|---|---|---|
| `ShopItemReviewTests.cs` (new) | `NeedsReview_IsFalseOnlyForCommonAndUncommon` | 5 |
| | `StatusOnCreate_PublishesOnlyContentThatNeedsNoReview` | 5 |
| | `StatusAfterEdit_NeverPublishesAndSendsBackOnlyAnItemRaisedIntoReview` (was `StatusAfterEdit_NeverPublishesAndSendsContentThatNeedsReviewBackToReview`, 20 rows; now status x old x new rarity) | 100 |
| `ShopManagementControllerIntegrationTests.cs` | `Create_Post_DungeonMaster_StoresItemOnTheActiveBoard` extended from 2 to 5 rows | +3 |
| | `Edit_Post_ByTheItemsCreator_LeavesTheStatusTheReviewRuleGives` (status, name, rarity, cleared denial, toast and absence of the success sentence for a Draft result; amended rows: 6 each for Draft and Denied, 11 each for Published and Archived) | 34 (first build 24) |
| | `Edit_Post_OnAnotherKeepersItem_IsSavedButNeverPublishedByTheEdit` (Dungeon Master, Admin, SuperAdmin x 12: stored and new rarity now both vary) | 36 (first build 24) |
| | `EditThenPublish_ByTheItemsCreator_NeverPutsAnItemRaisedIntoReviewOnSale` (was `..._NeverPutsAnUnreviewedItemOnSale`; 3 creators x 4 chains, adding Published Uncommon to Rare and Archived Common to VeryRare) | 12 (first build 6) |
| | `Delete_Post_FollowsWhoCreatedTheItemAndWhetherItHasBeenOnSale` (viewer x owner x status) | 24 |
| `ShopRenderTests.cs` | `ShopManagementList_BothLayouts_OfferDeleteOnlyWhereTheServerAcceptsIt` | 6 |
| | `ShopManagementList_BothLayouts_EveryEditLinkOpensForTheViewer` | 6 |
| `ShopConversionGuardTests.cs` | `PhaseSourceFiles` gains `ShopItemReview.cs` and `ShopItemReviewTests.cs` | (guard scope) |

Helpers: a public nested `Viewer` enum, `SeedViewerAsync(handle, Viewer)` beside the unchanged `SeedViewerAsync(handle, bool)`, `SeedItemAsync` gains an optional rarity (default Rare, every existing call unchanged) and stores a denial reason and date for a Denied item, `PostEditAsync` (token from the Edit page, post, redirect to `/ShopManagement`, decoded landing page), `ReadItemAsync`. In `ShopRenderTests`, `SeedManagementBoardAsync(handle, admin)` is now a wrapper over `SeedManagementBoardAsViewerAsync(handle, ManagementViewer)`, which returns the client, the viewer and the keeper; every existing call seeds what it did before. Totals: first build 30 unit and 93 integration cases; after the amendment 110 unit and 121 integration cases.

## RED evidence and bite checks (first build; the amended rule's evidence is under "Rule change after operator review")

**Task 1 RED.** Unit: `QuestBoard.UnitTests` did not compile, `CS0103: The name 'ShopItemReview' does not exist in the current context` at `ShopItemReviewTests.cs` lines 17, 28 and 59. Integration filter `~ShopManagementControllerIntegrationTests`: 30 failed, 57 passed, exactly the expected set: 12 creator rows (Draft and Denied to Common and Uncommon: found Published; Published and Archived at Rare and above, the two stored-Common upgrades included: found Published or Archived), 12 other-keeper rows (Draft and Denied to Common, Published and Archived to Legendary, for each of the three viewers), and all 6 chains ("because the creator cannot approve their own edited item, but found Published"). The five Create rows passed before and after.

**Task 1 bite 1** (`StatusAfterEdit` answers Published for a Draft or Denied item at a review-free rarity): unit 4 failed (Draft and Denied to Common and Uncommon); integration 10 failed (the 4 matching creator rows, the 6 Draft/Denied-to-Common other-keeper rows). The first chain did **not** fail at first: its second edit, to Legendary, already sent the item back to Draft under the other half of the rule, so the chain only asserted the end state. The chain fact was tightened to assert Draft after every edit (the plan's truth "after any chain of edits"); re-run under bite 1, the 3 Draft-to-Common-to-Legendary chains failed and the 3 Published chains passed. Recorded as a deviation below.

**Task 1 bite 2** (Published and Archived keep their status at any rarity): unit 6 failed (Published and Archived at Rare, Very Rare, Legendary); integration 17 failed (8 creator rows: Published and Archived at Rare and above plus the two stored-Common upgrades; 6 other-keeper Legendary rows; the 3 Published-Common-to-Legendary chains). Both bites restored from a copy and checked identical.

**Task 2 RED.** `Delete_Post_FollowsWhoCreatedTheItemAndWhetherItHasBeenOnSale`: 4 failed, 20 passed, exactly the Dungeon Master's four rows on another keeper's item: Draft and Denied showed "Item deleted successfully!" (the item was deleted), Published and Archived showed "Cannot delete items that have been published." instead of the ownership message. Every Admin and SuperAdmin row passed already.

**Task 2 bite 1** (creator check removed): the same 4 Dungeon Master rows failed. **Bite 2** (policy result forced to failed): 12 failed, the Admin and SuperAdmin rows on another keeper's item (4 statuses each) and on their own Published and Archived items; their own Draft and Denied rows passed through the non-admin path. This proves the AdminOnly policy, its SuperAdmin bypass included, is what grants Admin rights. Restored from a copy and checked identical.

**Task 3.** Both list theories were added and run before any view edit: 12 passed (the pin of today's lists). After the two condition changes the same 12 pass, and the existing management list facts stay green. Bite (Delete offered on every row of both lists, all three conditions set to true): all 6 Delete cases failed, the Dungeon Master on both layouts as expected and also the Admin and SuperAdmin cases (they gained Delete on Drafts). Restored from a copy; `git diff` on the views showed only the intended condition and comment lines.

## Build and suites (first build)

- `dotnet build` (solution): the first run hit `Fatal error. Internal CLR error. (0x80131506)`; the one re-run succeeded with 0 errors. 73 warnings, all pre-existing (NU1608/NU1902/NU1903 package advisories, xUnit1051 and CS8602/CS8604/CS0028 in untouched files); none in a file this plan touched.
- `dotnet test QuestBoard.UnitTests`: **1519 passed, 0 skipped, 0 failed** (1489 baseline + 30).
- `dotnet test QuestBoard.IntegrationTests`: **2742 passed, 16 skipped, 0 failed** (2649 baseline + 93; the 16 skipped are the SQL Server suites only).
- After the rule amendment: see "Rule change after operator review" (unit 1599, integration 2770 passed, 16 skipped).
- Task verify filters: Task 1 unit 43 passed, integration 312 passed; Task 2 integration 293 passed; Task 3 integration 519 passed (including `ShopConversionGuardTests.PhaseSourceFiles_CarryNoPlanningReferences` over the two appended files).

## Records (first build)

- **99-SECURITY.md:** T-99-81 to T-99-88 registered after T-99-80, with file and line evidence and test names from the committed code, each ending "opened at planning by 99-24; closed by" its fix commit: T-99-81, T-99-82, T-99-85 by 14ce7ed9; T-99-83, T-99-84 by 014994d2; T-99-86 by 050167a1 (all `closed`); T-99-87 and T-99-88 `closed (accepted)`. AR-99-16 (T-99-87) and AR-99-17 (T-99-88) added. The residual note now starts "**Resolved by 99-24 (operator decision "Fix first"):**", keeps its text, and ends with one sentence naming both rules, the fix commits, T-99-81 to T-99-86 and the residual T-99-87. Sign-Off: the follow-up line ticked and reworded; new unticked line for the operator's choice on T-99-87. Frontmatter, header, audit trail and metrics untouched (owned by the secure re-run).
- **deferred-items.md:** two rows in "Resolved in this phase" (the Edit review bypass, the cross-keeper Delete); four rows in Deferred (T-99-87 own-edit publish, Create versus Edit, no pre-save notice on the Edit page, an Admin's Delete of an item with purchases probably refused by SQL Server: `UserTransactions.ShopItemId` is a required foreign key with `NoAction`, not measured because InMemory does not enforce it and the SQL suites are skipped); one sentence appended to the Gate 2 paragraph.
- **99-VALIDATION.md:** two `✅ green` rows, Req "D-11 (operator decision)", citing `~ShopItemReviewTests` and `~ShopManagementControllerIntegrationTests`, and `~ShopManagementControllerIntegrationTests` and `~ShopRenderTests`. `nyquist_compliant: true` kept. No row of the map is partial, red or flaky.
- **WINDOWS:** `windows status` lists open 2, 3, 5, 28, 32 and waived 9, 18, 21, 24, 25, 27, 30, 33; none is about shop Edit, Delete or the review rule. Ledger left untouched.

## Rule change after operator review

**What changed.** The first build (14ce7ed9) sent a Published or Archived item back to Draft on any edit that left it at Rare, Very Rare or Legendary, whether the rarity was raised or kept, so a restock or a typo fix on a reviewed Rare item took it off sale. After reviewing it the operator changed that one rule to "Only on a rarity raise": a Published or Archived item goes back to Draft only when the edit raises its rarity from one that needs no review (`NeedsReview(old) == false`, Common or Uncommon) to one that does (`NeedsReview(new) == true`). Kept at Rare or above, or lowered, it keeps its status. Everything else stands: Draft and Denied always become Draft (a Denied item's reason and date cleared), Create is unchanged, `NeedsReview` stays the shared rule, the Delete rule and both list views are untouched, and the three toasts are unchanged.

**Built in 59c37604.**
- `ShopItemReview.StatusAfterEdit(current, oldRarity, newRarity)`: Published and Archived become Draft only `when RaisedIntoReview(oldRarity, newRarity)` (`!NeedsReview(old) && NeedsReview(new)`), otherwise keep `current`; everything else gives Draft. The doc comment explains the trade in plain language.
- `ShopManagementController.Edit` POST reads `previousStatus` and `previousRarity` from the stored item before the posted values are copied over (`ShopManagementController.cs:142-144`) and passes both to the rule (`:165`). The toast block (`:169-173`) is unchanged. Every write still goes through `shopService.UpdateAsync` onto the tracked entity.
- Tests, no GSD ids in names: the unit theory is now `StatusAfterEdit_NeverPublishesAndSendsBackOnlyAnItemRaisedIntoReview`, 100 explicit rows (4 statuses x 5 old x 5 new rarities, generated from an independent statement of the rule and written out); the creator theory has 34 rows, the other-keeper theory 36 (stored rarity added as a parameter), the chain theory, renamed `EditThenPublish_ByTheItemsCreator_NeverPutsAnItemRaisedIntoReviewOnSale`, 12 (two raise chains added). The named cases are all covered on Published and Archived: Common to Uncommon (kept), Uncommon to Rare (Draft), Rare to Legendary (kept), Legendary to Common (kept), plus Common to Rare, Common to Legendary, Uncommon to VeryRare, Rare to Rare, Legendary to Rare, Rare to Uncommon.

**Failing-first evidence (amendment RED).** The tests were changed first; `StatusAfterEdit` then got its three-argument signature with the first build's behaviour (the old rarity ignored) and the controller passed the stored rarity, so only the rule itself differed.
- Unit filter `~ShopItemReviewTests`: 18 failed, 92 passed, of 110. Exactly the Published and Archived rows for the 9 pairs Rare/VeryRare/Legendary to Rare/VeryRare/Legendary (expected Published or Archived, found Draft).
- Integration filter `~ShopManagementControllerIntegrationTests`: 12 failed, 127 passed, of 139. The 6 creator rows Published and Archived at Rare to Rare, Rare to Legendary and Legendary to Rare, and the 6 other-keeper rows Published and Archived Rare to Legendary for the three viewers; each showed "Expected ((ItemStatus)item.Status) to be ItemStatus.Published (or Archived), but found ItemStatus.Draft". Every raise, lowering, Draft and Denied row and all 12 chains passed under both rules, as expected.
- GREEN: unit `~ShopItemReviewTests|~ShopItemAvailabilityTests` 123 passed; integration `~ShopManagementControllerIntegrationTests|~ShopRenderTests|~BoardPageGateMatrixTests` 376 passed.

**Bite checks on the amended rule** (each applied to the GREEN file, the unit and controller filters run, then restored from a copy and checked identical with `diff`):
1. `StatusAfterEdit` answers Published for a Draft or Denied item at a review-free new rarity: unit 20 failed (Draft and Denied, every old rarity, to Common or Uncommon); integration 15 failed (6 creator rows Draft and Denied to Common or Uncommon, 6 other-keeper Draft and Denied Rare to Common rows, the 3 Draft-to-Common-to-Legendary chains).
2. Published and Archived always keep their status: unit 12 failed (every raise row); integration 23 failed (8 creator raise rows, 6 other-keeper Common to Legendary rows, the 9 Published and Archived chains).
3. Send back on any rarity increase (`newRarity > oldRarity`) instead of a raise into review: unit 8 failed (Common to Uncommon, Rare to VeryRare, Rare to Legendary, VeryRare to Legendary, Published and Archived); integration 10 failed (creator Common to Uncommon and Rare to Legendary, Published and Archived; other-keeper Rare to Legendary for each viewer).
4. Controller reads the old rarity after the posted values are copied over (`item.Rarity` passed twice): unit 0 failed (the rule is unchanged), integration 23 failed, the same set as bite 2. This pins that the stored rarity, not the form's, decides.

**T-99-87 re-checked.** Unchanged: Dungeon Master B raising A's Common item to Legendary still sends it to Draft (other-keeper rows Published and Archived Common to Legendary, all three viewers), and B's Publish is still accepted because `Publish` refuses only the creator. What is new is the neighbouring case: B editing A's already reviewed Rare item keeps it on sale with no Publish at all. That is the accepted residual T-99-89, not a change to T-99-87.

**Build and suites after the amendment.**
- `dotnet build` (solution): 0 errors; no warning in a touched file (the remaining warnings are the pre-existing NU1608/NU1902/NU1903 advisories and two CS8602 in untouched files). No 0x80131506 flake this time. No GC environment variable set.
- `dotnet test QuestBoard.UnitTests`: **1599 passed, 0 skipped, 0 failed** (1519 + 80: the unit theory grew from 20 to 100 rows).
- `dotnet test QuestBoard.IntegrationTests`: the first full run, which reached the wall-clock suite at 2026-11-17 20:00:12Z, failed 1 test outside the shop: `WallClockUnmovedTests.FinalizedQuestGameNight_NeverShowsTheUtcPlusTwoShiftedHour` asserts the page never contains "20:00", and the quest's real `CreatedAt` rendered as `title="2026-11-17 20:00 UTC"`. A time-of-day collision, not caused by this change; recorded in deferred-items.md. The full re-run a minute later: **2770 passed, 16 skipped, 0 failed** (2742 + 28: creator +10, other-keeper +12, chains +6; the 16 skipped are the SQL Server suites only).

**Records changed with the amendment.**
- `99-24-PLAN.md`: a "Rule change after the first build" note; must-haves, the rule table (now status x old rarity class x new rarity class), behaviour, RED, GREEN, the four bite checks, counts, threat model (T-99-81, T-99-82, T-99-85 and T-99-87 reworded, T-99-89 added), verification and success criteria; the strictness tension and its deferred row rewritten.
- `99-SECURITY.md`: T-99-81 to T-99-85 and T-99-87 rows corrected to the amended rule and to the line numbers of 59c37604 (T-99-85 now cites `ShopManagementController.cs:169-173`; the earlier `:165-171` was off by two, the block was `:167-171` at 14ce7ed9); T-99-89 registered as `closed (accepted)`; AR-99-18 added (accepted by the operator); the residual note's resolution sentence and the Sign-Off line updated. No new audit section, and the frontmatter, audit trail and metrics are untouched (the separate secure re-run owns them).
- `deferred-items.md`: the resolved Edit row states the amended rule and cites 59c37604; the T-99-87 row and the pre-save notice row reworded to a raise; a new by-operator-decision row for the reviewed-content residual (T-99-89, AR-99-18); a found row for the wall-clock time-of-day flake; one sentence appended to the Gate 2 paragraph.
- `99-VALIDATION.md`: the Edit rule row states the amended rule and both operator decisions; still `✅ green`, `nyquist_compliant: true` kept.

## Operator UAT

- As a Dungeon Master, edit your own Rare Draft to Common: it stays Pending Review, and the toast reads "Item updated and resubmitted for review!".
- Raise a published Uncommon item to Rare: it leaves the shop, the toast reads "Item updated and sent back for review. It is off sale until another Dungeon Master publishes it.", and it appears in the other Dungeon Masters' review table.
- Fix a typo or restock a published Legendary item, or lower it to Rare: it stays on sale and the toast reads "Item updated successfully!".
- As a Dungeon Master, neither the phone nor the desktop list offers Delete on another keeper's item; an Admin still deletes anything.

## Release note

The fixes ride the v6.4.0 hand-off, whose pull request the operator has not opened yet. No push, pull request or tag was made.

## Deviations from Plan

**1. [Rule 1 - Test gap] Chain fact tightened to assert Draft after every edit**
- **Found during:** Task 1 bite check 1
- **Issue:** with only bite 1 applied, the Draft-to-Common-to-Legendary chain still passed, because the second edit (to Legendary) sends the item back to Draft under the other half of the rule; the fact asserted only the end state, so it did not pin "an edit never publishes a Draft" on its own.
- **Fix:** the chain now asserts Draft after the Draft-to-Common edit and after the last edit, before the creator's Publish. Under bite 1 the 3 Draft chains fail; under bite 2 the 3 Published chains fail. Folded into the Task 1 commit.
- **Files modified:** `QuestBoard.IntegrationTests/Controllers/ShopManagementControllerIntegrationTests.cs`
- **Commit:** 14ce7ed9

Otherwise none: the plan was executed as written. No service, repository, entity, migration, csproj, sheet, script or `appsettings.json` changed; every write goes through the existing service calls onto a tracked entity, then SaveChanges; no role-name check in the controller or the views; no planning reference in source.

## Known Stubs

None.

## Threat Flags

None: the plan's threat model covers every surface touched (Edit and Delete POST, the two list conditions).

## Commits

- 14ce7ed9 fix(99-24): an edit never publishes a shop item and edited high-rarity content goes back to review
- 014994d2 fix(99-24): a Dungeon Master who is not an Admin deletes only their own draft or denied shop item
- 050167a1 fix(99-24): both shop management lists offer Delete only where the server accepts it
- 59c37604 fix(99-24): a published or archived shop item goes back to review only when an edit raises it into a reviewed rarity
- the docs commit "docs(99-24): record the rarity-raise review rule" (this SUMMARY, the plan, 99-SECURITY.md, deferred-items.md, 99-VALIDATION.md)

## Self-Check: PASSED

Found: `ShopItemReview.cs`, `ShopItemReviewTests.cs`, this SUMMARY; commits 14ce7ed9, 014994d2, 050167a1 and 59c37604 are on `milestone/v9-rolling-improvements`.
