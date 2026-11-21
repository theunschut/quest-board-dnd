---
phase: 103-themed-error-pages
plan: 09
subsystem: cross-board-resolver
tags: [cross-board, deep-link, resolver, record-visibility, tenancy, shop, quest-log, contacts, parity-facts, live-check, tracer]

requires:
  - phase: 103-themed-error-pages
    provides: board-aware resolver, registry access levels, resolver pins, leak-probe (103-07, 103-08)
provides:
  - CrossBoardVisibilityScope (viewer, candidate boards, Dungeon Master tier boards, Admin boards, board now, start of the board's today)
  - ShopItemAvailability.IsVisibleToMembers, the one member-visibility rule shared by Shop/Details and the cross-board query
  - Record-level predicates inside the one membership-pinned query per kind (shop item, quest edit, quest log, character edit, contact)
  - ICrossBoardLinkRepository.ResolveBoardIdAsync(kind, access, id, scope, token) and a resolver that builds the scope from IBoardClock
  - Every reproduction fact enabled (0 skipped in CrossBoardHiddenRecordTests); the Draft pin rewritten
affects: [103-10, 103-18]

actuals:
  tokens: 12000
  tasks: 3
  commits: 2

tech-stack:
  added: []
  patterns:
    - "A record rule is part of the one lookup query, so a hidden record and a nonexistent id end in the same null; there is no exists-but-hidden branch anywhere"
    - "Each rule mirrored from a page (shop visibility, played quest) has a parity fact against the Domain rule it mirrors; a one-character mutation of the played-quest comparison fails the parity theory"

key-files:
  created:
    - QuestBoard.Domain/Models/CrossBoardVisibilityScope.cs
    - QuestBoard.UnitTests/Repository/CrossBoardLinkVisibilityTests.cs
    - .planning/phases/103-themed-error-pages/103-09-SUMMARY.md
  modified:
    - QuestBoard.Domain/Interfaces/ICrossBoardLinkRepository.cs
    - QuestBoard.Domain/Services/CrossBoardLinkResolverService.cs
    - QuestBoard.Domain/Models/Shop/ShopItemAvailability.cs
    - QuestBoard.Repository/CrossBoardLinkRepository.cs
    - QuestBoard.Service/Controllers/Shop/ShopController.cs
    - QuestBoard.UnitTests/Models/ShopItemAvailabilityTests.cs
    - QuestBoard.UnitTests/Services/CrossBoardLinkResolverServiceTests.cs
    - QuestBoard.UnitTests/Architecture/AmbientClockSeamTests.cs
    - QuestBoard.IntegrationTests/Security/CrossBoardHiddenRecordTests.cs
    - QuestBoard.IntegrationTests/Security/CrossBoardAuthorizationBoundaryTests.cs
    - QuestBoard.IntegrationTests/Tests/ErrorPagesGuardTests.cs

key-decisions:
  - "Hidden contacts for Dungeon Master tier viewers (contactToggle dm-tier, settled): a Dungeon Master, Admin or SuperAdmin member of the board is switched to an unrevealed contact; the resolver never reads session state, so the page then applies the show-hidden toggle itself"
  - "SuperAdmin member counts in both the Dungeon Master tier set and the Admin set of every board they belong to; only member boards ever enter the scope"
  - "No predicate where the page has no record rule; the switch comment names Event, EventSeries, ContactCategory and the Member, DungeonMasterTier and AdminForOtherUser rows as checked"

requirements-completed: [D-04, D-05, D-06]

duration: 70min
completed: 2026-11-21
status: complete
---

# Phase 103 Plan 09: Record-visibility resolver Summary

**The cross-board lookup now applies, inside its one membership-pinned query per kind, the rule the target page applies to the record, so a member is no longer switched to a Draft, Denied or scheduled shop item, an unrevealed contact they did not create, or a quest the quest log would refuse. The deep-link leak that phase 99 recorded as known debt is closed and proved live on fixture data; a Dungeon Master of the board and a member who could open the record are still switched.**

## Predicate per kind and access, as built

Every predicate sits in the single `Where` of its kind's query and the projection still selects only the board id. The scope's `DungeonMasterTierGroupIds` is the one set that means "Dungeon Master, Admin or SuperAdmin member of this board".

| Kind | Access | Record rule applied in the query |
|------|--------|----------------------------------|
| ShopItem | ShopItemVisible (Shop/Details) | board in the Dungeon Master tier set, or status Published or Archived and sale start null or at or before the board's now |
| ShopItem | DungeonMasterTier (ShopManagement/Edit) | none beyond the membership pin |
| Quest | QuestOwnerOrAdmin (Quest/Edit, CreateFollowUp) | the quest's Dungeon Master is the viewer, or the board is in the Admin set (a SuperAdmin member counts) |
| Quest | CompletedQuest (QuestLog/Details, EditRecap) | closed, or finalized with a finalized date before the start of the board's today and not a Dungeon Master session |
| Character | CharacterOwnerOrAdmin (Characters/Edit) | owner is the viewer, or the board is in the Admin set |
| Contact | ContactVisible (Contacts/Details) | revealed, or created by the viewer, or the board is in the Dungeon Master tier set |
| Contact | DungeonMasterTier (Contacts/Edit) | none beyond the membership pin |

Checked and left as they are, with a plain comment beside the switch: Event, EventSeries, ContactCategory, Character details, Quest details and Quest/Manage (the Member and DungeonMasterTier rows). None of those pages refuses a record that exists on a board the viewer may open.

## The shared shop rule

`ShopItemAvailability.IsVisibleToMembers(item, boardNow)` is Published or Archived and `HasOpened`. `ShopController.Details` now refuses when the item is null, or is not visible to members and the viewer may not review unreleased items; the private `IsUnreleased` helper is gone. For the four statuses the page's answer is unchanged (Draft, Denied and not-yet-opened refuse a non-reviewer; Published, Archived and an ended window stay open), and every existing Shop integration fact passed untouched. The repository's shop predicate is the EF twin of the same rule.

## Parity and predicate facts (`CrossBoardLinkVisibilityTests`, 31 facts, InMemory, repository constructed directly)

- **Shop parity:** 11 status and window shapes against three board times; a Player-scope viewer resolves exactly the items `IsVisibleToMembers` accepts, and the set is neither empty nor everything.
- **Shop tiers:** a tier viewer resolves every shape; an Admin-only viewer and a SuperAdmin Player member (both as the resolver places them in the tier set) resolve the Draft and the scheduled item; a plain Player resolves neither, nor the Denied item; a board outside the candidates never resolves, whatever the tier.
- **Played-quest parity:** a theory over ten finalized-date offsets around the board's today (finalized and not finalized) agrees with `QuestExtensions.HasBeenPlayed`; mutating the comparison from `<` to `<=` fails the offset-0 row, then reverted. A second fact covers closed quests, an open quest, a game night later today or in the future, and Dungeon Master sessions (closed ones still show).
- **Quest edit, character edit, contact:** owner/creator, Admin and tier rows, and "another Dungeon Master", "another Player" and "Player of a hidden contact" negatives.
- **Resolver scope** (`CrossBoardLinkResolverServiceTests`, 5 new facts, clock substituted): the scope carries viewer, offered boards, board now and the start of today; an Admin-only board is in both elevated sets, a Dungeon Master board only in the tier set, a SuperAdmin Player member's board in both, a plain Player board in neither.
- **Guards:** `AmbientClockSeamTests` now guards `CrossBoardLinkResolverService.cs` and `CrossBoardLinkRepository.cs`; the repository and resolver read board time only from the scope and `IBoardClock`. `CrossBoardIgnoreQueryFiltersSeamTests` passes with its allowed list unchanged. `ErrorPagesGuardTests.PhaseSourceFiles` covers the five new or newly touched phase files.

## Reproduction facts

All seven reproduction facts of `CrossBoardHiddenRecordTests` that 103-03 left skipped are enabled and pass (Draft, Denied, scheduled, unrevealed contact, open quest on the quest log; the quest edit and Contacts-page facts were enabled by 103-07). `grep -c "Skip = "` prints 0, and `pageGateCounts` is yes so no skip remains for the page gate. Each of the five newly enabled facts was run unskipped first and failed against the old query (extra `Set-Cookie`, the queued switch banner), then passed.

Added beyond the plan: a hidden fact for a quest finalized for a later day on the quest log, a visible pin for a played quest on the quest log, and a pin that a Dungeon Master of the board is switched to an unrevealed contact (the page then applies the toggle).

## Rewritten fact

`ResolvedDraftShopItem_SwitchesTheBoardAndLetsThePageDecideForItself` is now `ResolvedDraftShopItem_ForAPlayerOfThatBoard_IsNotSwitchedAndGetsTheAnswerANonexistentIdGets`: same intent (a Player cannot open a Draft), it now compares status, headers and body with the nonexistent id's answer and checks the board and banner did not move. `ResolvedDraftShopItem_ForADungeonMasterOfThatBoard_OpensAfterTheSwitch` is unchanged and green. The rename is recorded in `tests-renamed.md`.

## Leak, BEFORE against AFTER (fixture data, One-Shot active)

BEFORE is the frozen baseline (`before/leak.json`, 8351, 103-03); AFTER is the new tree on 8350 (`after/103-09/leak.json`).

| Viewer | Request | BEFORE (baseline) | AFTER (new tree) |
|--------|---------|-------------------|------------------|
| Player | Draft shop item | 404, board One-Shot to Two, banner follows | 404, stays on One-Shot, no banner, 3264 bytes |
| Player | scheduled shop item | 404, switched, banner follows | 404, stays, no banner, 3264 bytes |
| Player | unrevealed contact | 404, switched, banner follows | 404, stays, no banner, 3264 bytes |
| Player | open quest on the quest log | 404, switched, banner follows | 404, stays, no banner, 3264 bytes |
| Player | future-finalized quest on the quest log | 404, switched, banner follows | 404, stays, no banner, 3264 bytes |
| Player | nonexistent shop id (control) | 404, stays | 404, stays, no banner, 3264 bytes |
| Player | Published, opened shop item | switched | 200, switched to Two, banner on the response |
| Dungeon Master of Two | Draft shop item | switched | 200, switched to Two, banner on the response |

The five hidden records answer with the same status and the same body length as the nonexistent id and move nothing. The plan's assertion printed `ok`. The probe gained a `record-after` case set (viewer per row) because the plan's assertion names cases the earlier sets lacked.

## Deviations from Plan

None - plan executed as written. The tracer's mid-plan human-verify is satisfied by green automated verify plus the live check above, per the operator's checkpoint rule. The extra facts listed under Reproduction facts are additions, not changes.

## Test results

- Unit: 1826 passed, 0 failed (was 1777; 49 new).
- Integration: 3175 passed, 17 skipped, 0 failed (was 3167 and 22 skipped; five reproduction facts enabled, three added).

## Known Stubs

None.

## Threat Flags

None. No new endpoint, auth path or file access; the repository still selects only a board id and the `IgnoreQueryFilters` seam list is unchanged. T-103-31 (existence of hidden records) is mitigated by the in-query predicates with live proof; T-103-32 by the shared shop rule and the parity facts; T-103-33 by reading board time only from `IBoardClock` with both files under the ambient-clock guard.

## Left running

Nothing of this plan: 8350 was started for the live check and is stopped again. The frozen baseline on 8351 is not running (it was down after the reboot and this plan did not need it, because BEFORE comes from the recorded `before/leak.json`); restart it from `baseline-src/` with `run-app.mjs` for the gates. SQL Server was not touched, and no SQL writes were made.

## Self-Check: PASSED

- Present: `CrossBoardVisibilityScope.cs`, `CrossBoardLinkVisibilityTests.cs`, `after/103-09/leak.json`; commits `99043240` and `c129aa26` exist.
- Acceptance: `IsVisibleToMembers` appears in the controller and in `ShopItemAvailability`; `Skip = ` count in `CrossBoardHiddenRecordTests` is 0; `CrossBoardLinkRepository.cs` appears in the clock guard.
