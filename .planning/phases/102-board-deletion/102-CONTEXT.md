# Phase 102: Board deletion - Context

**Gathered:** 2026-11-11
**Status:** Ready for planning

<domain>
## Phase Boundary

A super admin can delete a board that has no members left without getting a server error. Today the
Platform Delete posts to `GroupController.DeleteConfirmed`, which removes only the `Groups` row. Every
board-scoped table that references it with `NoAction` makes SQL Server reject the delete, so the
admin sees a 500. This phase:

- deletes everything scoped to the board in one transaction, and unlinks rows on other boards that
  reach into it;
- puts a confirmation page in front of it that shows what will go;
- adds a board-count column to `/platform/Users`.

The existing guard stays: Delete is only offered, and only accepted, when the board has no members.

</domain>

<decisions>
## Implementation Decisions

### What goes with the board
- **D-01:** All board-scoped content is deleted, with no exceptions for who created it: quests (with
  proposed dates, date votes, player signups, reminder logs), events (with event signups), event
  series, contacts (with notes and images), contact categories, contact tags, shop items, and the
  `UserGroups` rows. Being created by a DM who is still active on other boards does not save a row.
  — **Reversibility:** one-way — deleted rows cannot be recovered once committed.
- **D-02:** Characters on the board are deleted with it, together with their classes and images. A
  character's board is required, and its owners are ex-members who can no longer reach it.
  No schema change to make the board nullable.
- **D-03:** Purchase history (`UserTransactions` whose `ShopItemId` points at the board's shop items,
  including sell/return rows chained by `OriginalTransactionId`) is deleted with the board. Gold is
  not stored anywhere else, so this history is the only record of it, and it is board-local. No
  schema change to make `ShopItemId` nullable.
- **D-04:** Users themselves, DM profiles, trade items and calendar subscriptions are not touched.

### Links from other boards
- **D-05:** Rows on other boards that point into the deleted board are unlinked, not deleted, and
  they do not block the delete. Known cases: a follow-up quest whose `OriginalQuestId` is a quest
  on this board (moved quests make cross-board chains possible) has that link cleared, and a
  player signup on another board that uses a character from this board has its `CharacterId`
  cleared. The researcher must sweep for any other cross-board references and give them the same
  treatment.
- **D-06:** Linked board set: the board's membership in the set is cleared (the existing `SetNull`).
  The set itself is left alone even if one board or none remains; tidying it up is the admin's job
  on the Linked Boards page.

### Confirmation page
- **D-07:** Delete opens a confirmation page that shows, per type, how many rows will be deleted and
  what they are called. Each type has a collapsed `<details>` list of names (quest titles,
  character names, shop item names, contact names, event titles, and so on). Types whose rows have
  no useful name (signups, votes, transactions, reminder logs) show only a count. Types with a
  count of zero are hidden. Rows on other boards that will be unlinked (D-05) are shown separately.
- **D-08:** The Delete button stays disabled until the admin types the board's exact name. The
  server checks the typed name again on POST.
- **D-09:** Recheck at confirm time: members are re-checked, and if any have been added the delete
  is refused with a friendly message. Content added between the preview and the confirm is simply
  deleted too; the preview does not have to match exactly.
- **D-10:** The page follows the Platform convention: `Group/Delete.cshtml` and
  `Delete.Mobile.cshtml` already exist, so both are kept, in the converted Platform style.
- **D-11:** The delete runs in one transaction. Any failure rolls back and shows a friendly error,
  never a 500. Writes to quests and events go through the change tracker (no bulk delete, no raw
  SQL) so the feed revision guard and `FeedRevisionWriteSeamTests` hold.

### Platform Users column
- **D-12:** `/platform/Users` gets a column with the number of boards each user is a member of
  (`UserGroups` rows). The board names are available on hover (`title`) or behind a small expand.
  Desktop and the `.Mobile` twin both get it.
- **D-13:** The column is display only, with no sorting or filtering.

### Claude's Discretion
- Uploaded images: remove whatever would otherwise be orphaned (DB rows, plus files on disk if
  images live there). The researcher checks where character and contact images are stored.
- Users whose session points at the deleted board as their active board (ex-members, or a super
  admin who was viewing it): confirm that the existing stale-board recovery already falls back
  cleanly, and fix it only if it does not.
- Delete order inside the transaction, and whether the work lives in `GroupRepository`/
  `GroupService` or a dedicated service.
- Where the admin lands after a successful delete, and how success is reported (presumably the board
  list with the existing toast/TempData pattern).

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Phase definition
- `.planning/ROADMAP.md` (Phase 102) — goal and scope

### Data model and delete rules
- `QuestBoard.Repository/Migrations/QuestBoardContextModelSnapshot.cs` — every FK and its `OnDelete`;
  the source of truth for which rows block the delete
- `QuestBoard.Repository/Entities/QuestBoardContext.cs` — fluent configuration, query filters,
  `FeedRevisionStamper` hook
- `QuestBoard.Repository/GroupRepository.cs` — `RemoveMemberAsync` shows how to target a board by an
  explicit id with narrowed `IgnoreQueryFilters` queries

### Guarded areas
- `.claude/architecture.md` — the change-tracker rule for `Events`/`Quests`, and the feed revision
  invariants
- `FeedRevisionWriteSeamTests` and the IgnoreQueryFilters allowlist tests in
  `QuestBoard.IntegrationTests` — new board-wide queries must fit both

### Prior pattern
- `.planning/phases/92-move-a-quest-or-event-to-a-linked-board/` — preview-then-confirm page
  for a destructive cross-row change, re-checked at confirm time in one tracked save
- `.planning/phases/91-linked-boards-with-a-shared-calendar/` — linked board sets

### UI
- `.claude/ui-guidelines.md` — card pattern, button layout, `.Mobile.cshtml` twins
- `QuestBoard.Service/Areas/Platform/Controllers/GroupController.cs` — `Delete`/`DeleteConfirmed`,
  `HasMembersAsync` guard
- `QuestBoard.Service/Areas/Platform/Views/Group/Delete.cshtml`, `Delete.Mobile.cshtml`
- `QuestBoard.Service/Areas/Platform/Controllers/UsersController.cs`,
  `Views/Users/Index.cshtml`, `Index.Mobile.cshtml`

</canonical_refs>

<code_context>
## Existing Code Insights

### Delete map from the model snapshot
- **Direct `NoAction` to `Groups` (these cause the 500):** Quests, Characters, Events, EventSeries,
  Contacts, ContactCategories, ContactTags, ShopItems.
- **Transitive `NoAction` that also needs explicit handling:** PlayerSignups → Quest and
  → Character; ReminderLogs → Quest; Events → EventSeries; UserTransactions → ShopItem (required);
  Quests → OriginalQuest (self-reference, no cascade); UserTransactions → OriginalTransaction
  (self-reference).
- **Already cascade:** UserGroups (from Group), ProposedDates → PlayerDateVotes (from Quest and
  PlayerSignup), EventSignups (from Event), CharacterClasses and CharacterImage (from Character),
  ContactNotes and ContactImage (from Contact). Contact → Category is `SetNull`.
- **Groups → LinkedBoardSet** is `SetNull` on the board side; the set is not deleted (D-06).
- **No board link at all:** TradeItems, CalendarSubscriptions, DungeonMasterProfiles.

### Established Patterns
- Board-scoped repositories use an ambient active-board query filter. An operation on a board
  named by an explicit id has to bypass it in narrowed, allowlisted queries re-scoped by that id
  (see `GroupRepository.RemoveMemberAsync`).
- `GroupService.HasMembersAsync` already backs the guard on both GET and POST.
- `GroupService.GetAllWithMemberCountAsync` is a template for the per-user board count.

### Integration Points
- `GroupController.Delete` (GET preview) and `DeleteConfirmed` (POST).
- `UsersController` index plus its view model for the new column.

</code_context>

<specifics>
## Specific Ideas

- Confirmation must make it impossible to delete the wrong board by accident: the typed board name
  combined with a full per-type list of names.
- The Users column exists so a super admin can see who is still linked where while emptying a board
  before deleting it.

</specifics>

<deferred>
## Deferred Ideas

- Sorting or filtering `/platform/Users` by board count (for example, finding users on zero boards).
- Automatically cleaning up linked board sets left with one board or none.

</deferred>

---

*Phase: 102-board-deletion*
*Context gathered: 2026-11-11*
