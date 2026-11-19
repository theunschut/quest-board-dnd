---
phase: 103-themed-error-pages
plan: 07
subsystem: cross-board-resolver
tags: [cross-board, deep-link, resolver, tenancy, role-gating, page-gate, integration-tests, tracer]

requires:
  - phase: 103-themed-error-pages
    provides: hidden-record reproduction facts and the oracle comparison helper (103-03, 103-05)
provides:
  - CrossBoardAccess (closed list of eight access levels) and CrossBoardViewerBoard (role, board type, disabled pages per member board)
  - Resolver signature ResolveAsync(kind, access, page, id, userId, viewerIsSuperAdmin, token) narrowing the viewer's own boards by page and role
  - Registry of the same 18 routes with CrossBoardRouteEntry (kind, access, page), TryGetRouteEntry, and CrossBoardRouteTarget Access and Page
  - Both callers (deep-link middleware and picker skip) on one route rule
  - Enabled facts: Quest/Edit, page gate, profile edit (hidden and visible); the rewritten boundary fact
affects: [103-08, 103-09, 103-10, 103-18]

actuals:
  tokens: 21000
  tasks: 3
  commits: 3

tech-stack:
  added: []
  patterns:
    - "Hidden and nonexistent share one null: the resolver narrows the viewer's own boards before the membership-pinned query, with no branch for a record that exists but may not be opened"
    - "A SuperAdmin flag that only raises the role on boards the viewer already belongs to"

key-files:
  created:
    - QuestBoard.Domain/Enums/CrossBoardAccess.cs
    - QuestBoard.Domain/Models/CrossBoardViewerBoard.cs
    - .planning/phases/103-themed-error-pages/103-07-SUMMARY.md
  modified:
    - QuestBoard.Domain/Interfaces/ICrossBoardLinkResolver.cs
    - QuestBoard.Domain/Interfaces/ICrossBoardLinkRepository.cs
    - QuestBoard.Domain/Services/CrossBoardLinkResolverService.cs
    - QuestBoard.Repository/CrossBoardLinkRepository.cs
    - QuestBoard.Service/Helpers/CrossBoardLinkRegistry.cs
    - QuestBoard.Service/Helpers/CrossBoardRouteTarget.cs
    - QuestBoard.Service/Middleware/CrossBoardDeepLinkMiddleware.cs
    - QuestBoard.Service/Controllers/GroupPickerController.cs
    - QuestBoard.IntegrationTests/Security/CrossBoardHiddenRecordTests.cs
    - QuestBoard.IntegrationTests/Security/CrossBoardAuthorizationBoundaryTests.cs
    - QuestBoard.IntegrationTests/Tests/ErrorPagesGuardTests.cs

key-decisions:
  - "pageGateCounts is yes: a board that does not offer the route's page counts as a refusal for the switch (BoardPagePolicy.IsAvailable), so no named deviation is recorded"
  - "The SuperAdmin flag is a deliberate signature change; it only counts the viewer as Admin on boards they are already a member of, and membership is read first and stays mandatory"
  - "Board-level narrowing only: roles for DungeonMasterTier and QuestOwnerOrAdmin, Admin for AdminForOtherUser, page availability for every route; record rules (ownership, shop status and window, hidden contacts, unplayed quests) are left to the next plan"

requirements-completed: [D-01, D-04, D-05, D-06]

duration: 60min
completed: 2026-11-19
status: complete
---

# Phase 103 Plan 07: Board-aware cross-board resolver Summary

**pageGateCounts: yes (operator answer).** The resolver now narrows the viewer's own boards by whether the board offers the route's page and whether the viewer's role there can open it, through both callers, so a Player of the target board is no longer switched to a Dungeon Master page and gets the answer a nonexistent id gets.

## Access table as built

Page availability narrows every route. The role column is the extra board-level narrowing; "record" means the record-level rule is not applied here.

| Route | Access | Page | Role narrowing now |
|-------|--------|------|--------------------|
| Quest/Details | Member | Quests | none |
| Quest/Edit, Quest/CreateFollowUp | QuestOwnerOrAdmin | Quests | Dungeon Master, Admin or SuperAdmin on the board (ownership is a record rule) |
| Quest/Manage | DungeonMasterTier | Quests | Dungeon Master, Admin or SuperAdmin |
| QuestLog/Details, QuestLog/EditRecap | CompletedQuest | QuestLog | none (record rule) |
| Events/Details | Member | Events | none |
| Events/Edit | DungeonMasterTier | Events | Dungeon Master tier |
| Characters/Details | Member | Characters | none |
| Characters/Edit | CharacterOwnerOrAdmin | Characters | none (record rule) |
| Contacts/Details | ContactVisible | Contacts | none (record rule) |
| Contacts/Edit | DungeonMasterTier | Contacts | Dungeon Master tier |
| Shop/Details | ShopItemVisible | Shop | none (record rule) |
| ShopManagement/Edit | DungeonMasterTier | Shop | Dungeon Master tier |
| Series/Details | Member | Events | none |
| ContactCategoryManagement/Edit | DungeonMasterTier | Contacts | Dungeon Master tier |
| DungeonMaster/Profile | Member | DungeonMasterProfile | none; page filter applied before the exactly-one rule |
| DungeonMaster/EditProfile | AdminForOtherUser | DungeonMasterProfile | Admin or SuperAdmin; applied before the exactly-one rule |

Still 18 routes and 8 lookup kinds (`RegisteredRoutes_HasExactly18Entries` unchanged and green). Membership of the target board stays mandatory for everyone, SuperAdmin included.

## What changed

- **Resolver** (`CrossBoardLinkResolverService`): reads memberships fresh, returns null with none, reads the new projection, keeps boards where `BoardPagePolicy.IsAvailable(type, disabled, page)` holds and the role meets the access level (SuperAdmin counts as Admin on member boards), returns null when none is left, then runs the existing membership-pinned id query (or the shared-board query for the profile kind) on the narrowed set. No branch exists for exists-but-hidden; the same null serves both.
- **Repository** (`GetViewerBoardsAsync`): one projection over the viewer's membership rows restricted to the given ids, selecting ids, role, board type and disabled-page mask only. `ResolveBoardIdAsync` keeps its signature. It is the only file of this plan calling `IgnoreQueryFilters` (mentions in `CrossBoardLinkRepository.cs` went from 9 to 10; zero in the resolver).
- **Registry and route target**: values became the internal record `CrossBoardRouteEntry(Kind, Access, Page)`; `TryGetLookupKind` keeps its signature, `TryGetRouteEntry` is new; `CrossBoardRouteTarget` keeps `Kind` and `Id` first and gains `Access` and `Page`, filled by both `TryFromRouteValues` and `TryFromLocalUrl`.
- **Callers**: the middleware passes `routeTarget.Access`, `routeTarget.Page` and `context.User.IsInRole("SuperAdmin")`; the picker passes `target.Access`, `target.Page` and its `isSuperAdmin`. The picker still falls straight through on null.
- **Signature**: `ResolveAsync(kind, access, page, id, userId, viewerIsSuperAdmin, token)`. It carries the SuperAdmin flag because the flag is the only way the role-gated pages can treat a SuperAdmin the way the page itself does, and it can only raise the role on boards the viewer already belongs to.
- **Comments** rewritten in plain language in the resolver interface, the resolver service, the repository header, the picker and the middleware guard-order note.

## Facts

- Enabled and passing: the Quest/Edit fact (viewer Dungeon Master on the active board, Player on the target); the contact-on-a-board-without-the-Contacts-page fact (pageGateCounts yes).
- New: profile edit hidden (Player on the target board gets a nonexistent id's answer) and profile edit visible (Admin on the target board is switched and served 200).
- Rewritten: `DungeonMasterOnActiveBoard_PlayerOnTargetBoard_IsRefusedTheQuestEditPage` is now `DungeonMasterOnActiveBoard_PlayerOnTargetBoard_IsNotSwitchedAndGetsTheAnswerANonexistentIdGets` (recorded in `tests-renamed.md`). Its class comment now says the resolver offers only boards whose role can open the page.
- Kept green unchanged: `PlayerOnActiveBoard_DungeonMasterOnTargetBoard_ReachesTargetBoardsQuestEditPage`, all `CrossBoardRouteCoverageTests` (every seeded target board is a One-Shot board that offers every page, so no re-seed was needed), the three pins, `RegisteredRoutes_HasExactly18Entries`.
- The record-level hidden facts (draft, denied and scheduled shop item, unrevealed contact, open quest on the quest log) are still skipped with their reasons; they are the next plan's work.
- `ErrorPagesGuardTests.PhaseSourceFiles` now lists the resolver, registry, route target, both callers, the repository and the boundary and coverage tests.
- No test was deleted: comparing every fact name in the CrossBoard classes with `BASELINE_SHA`, the only absent names are the two recorded as renamed.

## Live check

The new tree on 8350 against the frozen baseline on 8351, as the fixture Player with One-Shot active (`audit/resolver-live-8350.json`, `audit/resolver-live-8351.json`, tool `tools/resolver-live-103-07.mjs`): a link to a hidden shop item's management page, a quest manage page, a quest edit page and a contact edit page on the member's other board left the board unchanged on 8350 and answered with the same response length as a nonexistent id (8980 bytes); on the baseline each switched to the other board. A member-level link (`/Quest/Details`) still switched on both. 8350 was stopped afterwards for the test run. The tracer's human-verify is satisfied by green automated verify plus this live check.

## What follows

103-08 pins each access level, the registry and the route-target fill at unit level, the page attribute drift guard, and proves the board-level rules live on p103 data. 103-09 adds the record-level rules (shop status and window, hidden contacts, unplayed quests, ownership) and enables the five still-skipped facts.

Reversibility: costly. Reverting reopens the leak and touches the resolver, its repository query and the guard facts.

## Test results

- Unit: 1679 passed, 0 failed.
- Integration: 3167 passed, 22 skipped, 0 failed (was 3163 and 24: two facts enabled, two added).

## Deviations from Plan

None. The plan's pageGateCounts no-branch (a named deviation for Gate 2) did not apply, because the answer is yes. `deviations.json` was not touched. The plan said the route coverage might need a re-seed; it did not.

## Known Stubs

None.

## Threat Flags

None. No new endpoint, auth path or file access; the new repository projection selects ids, role, board type and disabled pages only, inside the one file allowed to bypass the filters.

## Left running

Nothing: 8350 is stopped; the frozen baseline on 8351 is still running. SQL Server was not touched.

## Self-Check: PASSED

- Present: both new Domain files, the rewritten resolver, registry and route target; commits `3c7a1f9f` and `76964f29` exist.
- `git diff --quiet HEAD` on GroupService.cs, GroupRepository.cs, IGroupService.cs, IGroupRepository.cs exits 0; `IgnoreQueryFilters` in the resolver service: 0.
