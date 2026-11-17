---
phase: 99-shop-and-shop-management-ui-conversion
plan: 22
subsystem: shop-authorization
tags: [shop, authorization, draft, information-disclosure, tdd]
requires: ["99-20"]
provides:
  - Shop/Details answers 404 (page and dialog) for Draft and Denied items unless the DungeonMasterOnly policy succeeds on the active board
  - integration facts for the full access matrix, missing-id parity and the cross-board deep link judged on the landed board
affects: [v6.4.0 release hand-off]
tech-stack:
  added: []
  patterns: [imperative policy check through IAuthorizationService in a controller action, asked only for the statuses that need it, sharing the missing-id NotFound return]
key-files:
  created: []
  modified:
    - QuestBoard.Service/Controllers/Shop/ShopController.cs
    - QuestBoard.IntegrationTests/Controllers/ShopControllerIntegrationTests.cs
    - QuestBoard.IntegrationTests/Tests/ShopRenderTests.cs
    - QuestBoard.IntegrationTests/Security/CrossBoardAuthorizationBoundaryTests.cs
    - QuestBoard.IntegrationTests/Tests/ShopConversionGuardTests.cs
    - .planning/phases/99-shop-and-shop-management-ui-conversion/deferred-items.md
decisions:
  - "Draft and Denied shop items open on Shop/Details (page and dialog) only through the DungeonMasterOnly policy on the active board (DM or Admin of that board, or SuperAdmin); everyone else gets NotFound, identical to a missing id."
  - "The refusal is NotFound, never Forbid: the cookie scheme turns Forbid into a 302 to /Account/AccessDenied, which would differ from the missing-id answer and reveal that the id exists."
  - "The check lives in ShopController (Service layer) through the policy, not in the domain, so the per-board role and the SuperAdmin bypass stay in DungeonMasterHandler; ShopManagementController.Delete is the precedent."
metrics:
  duration: "about 11 minutes of wall clock (17:00Z to 17:11Z, sequential single pass)"
  completed: 2026-11-17
status: complete
actuals:
  tokens: 9500
  tasks: 3
  commits: 3
---

# Phase 99 Plan 22: Draft details access gap Summary

A signed-in member who is not a Dungeon Master or Admin of the active board (and not a SuperAdmin) now gets a 404 for a Draft or Denied shop item on `/Shop/Details/{id}` and in the `?isModal=true` dialog, byte-identical to a missing id, while every reviewer who could open those items before still can and members keep Published and Archived exactly as they were.

## The rule as built

| Status | Who may open Details (page and dialog) | Everyone else |
|---|---|---|
| Published | any signed-in member of the active board (unchanged) | n/a |
| Archived | any signed-in member of the active board (unchanged) | n/a |
| Draft | the `DungeonMasterOnly` policy: a Dungeon Master or Admin of the active board, or a SuperAdmin | 404, identical to a missing id |
| Denied | the same policy as Draft | 404, identical to a missing id |
| any status, another board | nobody through this board: the shop item query filter makes the id null, so 404 (unchanged). A deep link into another board the viewer belongs to switches first, then the rule above is judged on that board | 404 |

`ShopController.Details` now reads `if (item == null || (IsUnreleased(item.Status) && !await MayReviewUnreleasedItemsAsync())) return NotFound();`, before mapping and before the `isModal` branch, so page and dialog share one check and one not-found return. `IsUnreleased` is Draft or Denied; `MayReviewUnreleasedItemsAsync` is `(await authorizationService.AuthorizeAsync(User, "DungeonMasterOnly")).Succeeded`. The policy is asked only for those two statuses, so Published and Archived cost no extra role query. `IAuthorizationService` was appended to the primary constructor (DI already provides it). A plain-language comment gives the why. `IsInRole` count in the controller: 0; `AuthorizeAsync(User, "DungeonMasterOnly")` count: 1.

**Refusal answer.** `NotFound()`, the answer a missing id and another board's id already give; there is no `UseStatusCodePages`, so the body is empty. Not `Forbid()`: the cookie scheme turns it into a 302 to `/Account/AccessDenied` (`Program.cs`), which differs from the missing-id answer and which the dialog fetch would see as `response.redirected`. The dialog script already treats a non-ok answer as failure (body empty, `aria-busy` cleared), matching the recorded `dialogLoadError.show: false`; no view or script changed.

**Where and why.** Roles are per board and resolved by `DungeonMasterHandler` (SuperAdmin bypass, then the active board's `GroupRole`). Copying that into `QuestBoard.Domain` would fork the policy and lose the bypass, so the check is an imperative policy call in the Service-layer controller, as `ShopManagementController.Delete` already does with `AdminOnly`.

**Correction to the starting reason for keeping Archived open.** Purchase-history slips do not link to Details (`_ShopPurchaseHistory.cshtml` writes the item name as plain text). The real member paths to an Archived item are: `Purchase` and `SellToShop` redirecting to `Details/{id}` with the error toast when the item was archived mid-visit; links saved or shared while the item was on sale; and the approved notice "This item is not currently available for purchase.".

## RED evidence (Task 1)

With the two new theories in place and the controller unchanged, the four `Details_DraftOrDeniedItem_IsNotFoundForAPlainMember` cases failed, each "Expected 404 ... but found HttpStatusCode.OK {value: 200}": (Draft, page), (Draft, dialog), (Denied, page), (Denied, dialog). The four `Details_DraftOrDeniedItem_OpensForItsCreatingDungeonMaster` cases passed (the positive control). After the check: all 8 pass.

## Bite checks (Task 2)

1. **Creator-only rule** (temporary: refuse unless the viewer is `item.CreatedByDmId`): all 12 `Details_DraftOrDeniedItem_OpensForAnotherDungeonMasterAnAdminOrASuperAdmin` cases failed, dm/admin/superadmin x Draft/Denied x page/dialog. The other 16 Details facts passed.
2. **Archived also refused** (temporary: `IsUnreleased` includes Archived): `Details_PublishedOrArchivedItem_StillOpensForAPlainMember` (Archived, page) and (Archived, dialog) failed; the other 26 passed.

Restored after each; `git diff --quiet HEAD -- QuestBoard.Service/Controllers/Shop/ShopController.cs` exited 0 before the Task 2 commit, and that commit does not touch the controller.

## Facts added

`ShopControllerIntegrationTests` (+26 cases):

- `Details_DraftOrDeniedItem_IsNotFoundForAPlainMember` (4: Draft/Denied x page/dialog): exactly 404, body without the item name.
- `Details_DraftOrDeniedItem_OpensForItsCreatingDungeonMaster` (4): 200, item name present.
- `Details_DraftOrDeniedItem_OpensForAnotherDungeonMasterAnAdminOrASuperAdmin` (12): item made by a separate keeper; viewer is another DM, a board Admin, or a SuperAdmin who is only a Player on board 1 (so the policy, not the group role, decides); 200 and the item name.
- `Details_PublishedOrArchivedItem_StillOpensForAPlainMember` (4): 200 and the name; the Published page also has "Purchase Item", the Archived page also has "This item is not currently available for purchase.".
- `Details_RefusedItem_AnswersExactlyLikeAMissingId` (2: page/dialog): the same member GETs a Draft id and the largest seeded id plus 1000; both 404 and the two bodies are equal strings.

`CrossBoardAuthorizationBoundaryTests` (+1):

- `ResolvedDraftShopItem_ForADungeonMasterOfThatBoard_OpensAfterTheSwitch`: viewer is a Player on board A and a Dungeon Master on board B; after selecting board A, the navigation GET to the board-B Draft answers 200 with the banner naming board B and "currently under review"; the follow-up navigation does not show board A. The Player and DM facts now share `SeedViewerAndDraftShopItemOnTargetBoardAsync(suffix, roleOnBoardB)`.

`ShopConversionGuardTests.PhaseSourceFiles`: appended `QuestBoard.Service/Controllers/Shop/ShopController.cs` and `QuestBoard.IntegrationTests/Security/CrossBoardAuthorizationBoundaryTests.cs`.

## Facts re-pointed

- `ShopRenderTests.ShopDetailsPage_DraftItem_ShowsBothReviewSentencesOnBothLayouts` (4 cases): now seeded with `SeedEditablePageAsync` (the item's own DM) instead of a plain member; still asserts both review sentences and no `data-sell-form`.
- `ShopRenderTests.ShopDetails_NotAvailableNotice_FollowsTheRecordedAnswer` (5 cases): the two Denied rows are viewed by the item's own DM via `SeedEditablePageAsync`; the Archived and Published rows keep the plain member. Assertions unchanged (notice on Denied and Archived, not on Published).
- `CrossBoardAuthorizationBoundaryTests.ResolvedDraftShopItem_SwitchesTheBoardAndLetsThePageDecideForItself`: the Player of board B deep-linked to the board-B Draft now gets 404 with no `BoundaryDraftItem{suffix}` in the body. The follow-up check was adjusted (see Deviations): the first follow-up page must name board B, and a second navigation keeps the original "does not contain EuphoriaInn" assertion.

Unchanged and still green: `ShopRenderTests.ShopDetails_EditingDungeonMaster_GetsAnOkPageForEveryStatus`, `ShopRenderTests.ShopManagementEdit_Desktop_ViewDetailsPointsAtTheShopDetailsPage`, `ShopControllerIntegrationTests.Details_WithInvalidItemId_ShouldReturn404`, and the Published-item Details facts in `MobileViewsTests`, `CrossBoardOracleParityTests` and `CrossBoardRouteCoverageTests`.

## Tests

- Task 1 filter (ShopControllerIntegrationTests, ShopRenderTests, CrossBoardAuthorizationBoundaryTests): 211 passed.
- Task 2 filter (plus CrossBoardOracleParityTests, CrossBoardRouteCoverageTests, ShopConversionGuardTests, ShopManagementControllerIntegrationTests, BoardPageStaticGuardTests, MobileViewsTests): 471 passed.
- **Unit (full): 1433 passed, 0 skipped, 0 failed** (unchanged from 99-21).
- **Integration (full): 2595 passed, 16 skipped (SQL Server migrator tests), 0 failed, 2611 total** (2568 passed at 99-19 and 99-20, +27: the 26 controller cases and the 1 deep-link fact).
- Planning-reference grep over `ShopController.cs`, `ShopControllerIntegrationTests.cs`, `ShopRenderTests.cs` and `CrossBoardAuthorizationBoundaryTests.cs`: 0 each. `git diff --name-only <plan start> -- QuestBoard.Service QuestBoard.Domain QuestBoard.Repository` prints only `ShopController.cs`; no `.csproj` change.

## deferred-items.md and WINDOWS

- "Any member can open a Draft item's Details by id" moved from Deferred to Resolved in this phase, citing fix commit `fd6e799e`, plan 99-22, the Gate 2 `df:defer` and the go-ahead "go, fix the draft access first". One sentence appended to the Gate 2 paragraph.
- Three rows added under Deferred, each "not asked (found after Gate 2, by 99-22)": the availability window not enforced by id (Details, `PurchaseItemAsync`, `SellItemToShopAsync`); Archive accepting any status and Reopen publishing any archived item (`ShopManagementController.Archive` / `Reopen`); a deep link to a Draft on another of the member's boards switching the board before the 404 (`CrossBoardDeepLinkMiddleware` / `CrossBoardLinkRepository`), now noting that the one-shot switch notice then shows on the member's next page.
- WINDOWS: `windows status` lists no open or waived entry about Shop Details access to Draft or Denied items (entry 33 on the same view is the waived, unrelated submit lock). `.planning/WINDOWS.md` untouched, no entry added.

## Operator UAT

As a player, open `/Shop/Details/{id}` for a Draft item: 404. As a Dungeon Master of that board: the page opens, including from the Edit page's View Details link.

## Release note

The fix rides the v6.4.0 hand-off (the operator's pull request is not opened yet). No push, pull request or tag was made in this plan.

## Deviations from Plan

**1. [Rule 1 - Test fact] The re-pointed Player deep-link fact needed a second follow-up navigation.** With the plan's literal follow-up kept, `/quests` after the 404 contained "EuphoriaInn": the board had switched (board B is the active board in the nav), but the bodiless 404 never rendered the one-shot switch notice, so the notice, with its "Switch back to EuphoriaInn" link, appeared on the next page instead. The fact now asserts that first follow-up names board B (the switch held), then makes a second navigation and keeps the original `NotContain("EuphoriaInn")` assertion. Intent kept; the carried-over notice is recorded in the third new deferred row. Commit `fd6e799e`.

**2. [Rule 3 - Process] RED was observed, not committed separately.** As in 99-21, the plan commits the four Task 1 files together, so the failing facts and the check are in one commit.

**3. [Process] Tracer gate.** Task 1 is a tracer; the config is not in auto mode, but the caller asked for all three tasks in one run, so the tracer's verify was re-run green (211 passed) and execution continued instead of returning a checkpoint.

**4. [Refactor within scope] Shared seed helper** in `CrossBoardAuthorizationBoundaryTests` for the Player and DM Draft deep-link facts, so the new fact differs only by the board-B role.

**5. [Tool quirk] `state.advance-plan` answered `last_plan` and left STATE at "Plan: 21 of 21"**, and `state.update-progress` found no progress field to update. STATE's Current Position (Plan: 22 of 22, Status, Last activity) and `last_activity_desc` were corrected by hand, as 99-20 did. `roadmap.update-plan-progress 99` ticked 99-22 and set 22/22.

## Known Stubs

None.

## Threat Flags

None. T-99-64 (member reads Draft/Denied) and T-99-65 (existence oracle) are mitigated by the check and the refusal and parity facts; T-99-66 (reviewer regression) by the policy-only check and the 12 reviewer cases with bite check 1; T-99-67 to T-99-69 accepted and recorded.

## Self-Check: PASSED

- `ShopController.cs` contains the policy check; commits `fd6e799e` and `31349891` are in the log; both full suites are green.
