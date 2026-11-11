# Phase 102: Board deletion - Research

**Researched:** 2026-11-11
**Domain:** EF Core 10 / SQL Server relational delete ordering in a multi-tenant ASP.NET Core 10 MVC app; Platform-area Razor views (desktop + `.Mobile` twin)
**Confidence:** HIGH (every fact below comes from reading this repository's source this session; no external libraries are involved)

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions

- **D-01:** All board-scoped content is deleted, with no exceptions for who created it: quests (with proposed dates, date votes, player signups, reminder logs), events (with event signups), event series, contacts (with notes and images), contact categories, contact tags, shop items, and the `UserGroups` rows. Being created by a DM who is still active on other boards does not save a row. — **Reversibility:** one-way — deleted rows cannot be recovered once committed.
- **D-02:** Characters on the board are deleted with it, together with their classes and images. A character's board is required, and its owners are ex-members who can no longer reach it. No schema change to make the board nullable.
- **D-03:** Purchase history (`UserTransactions` whose `ShopItemId` points at the board's shop items, including sell/return rows chained by `OriginalTransactionId`) is deleted with the board. Gold is not stored anywhere else, so this history is the only record of it, and it is board-local. No schema change to make `ShopItemId` nullable.
- **D-04:** Users themselves, DM profiles, trade items and calendar subscriptions are not touched.
- **D-05:** Rows on other boards that point into the deleted board are unlinked, not deleted, and they do not block the delete. Known cases: a follow-up quest whose `OriginalQuestId` is a quest on this board (moved quests make cross-board chains possible) has that link cleared, and a player signup on another board that uses a character from this board has its `CharacterId` cleared. The researcher must sweep for any other cross-board references and give them the same treatment.
- **D-06:** Linked board set: the board's membership in the set is cleared (the existing `SetNull`). The set itself is left alone even if one board or none remains; tidying it up is the admin's job on the Linked Boards page.
- **D-07:** Delete opens a confirmation page that shows, per type, how many rows will be deleted and what they are called. Each type has a collapsed `<details>` list of names (quest titles, character names, shop item names, contact names, event titles, and so on). Types whose rows have no useful name (signups, votes, transactions, reminder logs) show only a count. Types with a count of zero are hidden. Rows on other boards that will be unlinked (D-05) are shown separately.
- **D-08:** The Delete button stays disabled until the admin types the board's exact name. The server checks the typed name again on POST.
- **D-09:** Recheck at confirm time: members are re-checked, and if any have been added the delete is refused with a friendly message. Content added between the preview and the confirm is simply deleted too; the preview does not have to match exactly.
- **D-10:** The page follows the Platform convention: `Group/Delete.cshtml` and `Delete.Mobile.cshtml` already exist, so both are kept, in the converted Platform style.
- **D-11:** The delete runs in one transaction. Any failure rolls back and shows a friendly error, never a 500. Writes to quests and events go through the change tracker (no bulk delete, no raw SQL) so the feed revision guard and `FeedRevisionWriteSeamTests` hold.
- **D-12:** `/platform/Users` gets a column with the number of boards each user is a member of (`UserGroups` rows). The board names are available on hover (`title`) or behind a small expand. Desktop and the `.Mobile` twin both get it.
- **D-13:** The column is display only, with no sorting or filtering.

### Claude's Discretion

- Uploaded images: remove whatever would otherwise be orphaned (DB rows, plus files on disk if images live there). The researcher checks where character and contact images are stored.
- Users whose session points at the deleted board as their active board (ex-members, or a super admin who was viewing it): confirm that the existing stale-board recovery already falls back cleanly, and fix it only if it does not.
- Delete order inside the transaction, and whether the work lives in `GroupRepository`/`GroupService` or a dedicated service.
- Where the admin lands after a successful delete, and how success is reported (presumably the board list with the existing toast/TempData pattern).

### Deferred Ideas (OUT OF SCOPE)

- Sorting or filtering `/platform/Users` by board count (for example, finding users on zero boards).
- Automatically cleaning up linked board sets left with one board or none.
</user_constraints>

<phase_requirements>
## Phase Requirements

No requirement IDs are mapped to this phase (ROADMAP says `Requirements: TBD`). Coverage is against the ROADMAP scope bullets and the CONTEXT decisions:

| ID | Description | Research Support |
|----|-------------|------------------|
| D-01..D-04 | What is deleted vs kept | "Delete Map" table below: every FK to a board-owned row, with the action per row type |
| D-05, D-06 | Cross-board unlink, linked set | "Cross-board reach-ins" table: three reach-ins found, two are named in CONTEXT, one (Event to Series) is defensive; linked set needs no code |
| D-07..D-10 | Confirmation page | Preview query design, view/twin pattern, typed-name script, UI pitfalls |
| D-11 | One transaction, friendly error, tracker rule | Single `SaveChangesAsync`, error handling, guard-test analysis |
| D-12, D-13 | Users board column | One grouped query, view-model and both twins |
| Discretion items | Images, active-board session, order, service placement, landing | Each has a recommended resolution with evidence |
</phase_requirements>

## Summary

The 500 is exactly what CONTEXT says: `GroupController.DeleteConfirmed` calls `groupService.RemoveAsync`, which is `BaseRepository.RemoveAsync` (find the `Groups` row, `Remove`, one `SaveChangesAsync`). Eight tables point at `Groups` with `DeleteBehavior.NoAction`, and a further seven NoAction/ClientSetNull links hang off those rows. The fix needs **no migration**: the schema already has the right delete rules for a deliberate, explicit, ordered delete. Do not try to make the Group FKs cascade: that is what the model comments say would create multiple cascade paths on SQL Server.

Recommended design: one new `GroupRepository.DeleteBoardAsync(groupId)` that loads every board-scoped row with `IgnoreQueryFilters()` re-scoped by the explicit id, unlinks the three cross-board reach-ins by setting their FK to null, calls `RemoveRange` for each type, and finishes with **exactly one** `SaveChangesAsync`. One `SaveChanges` is one database transaction on SQL Server, satisfies "one transaction" with no explicit transaction code, goes through the change tracker for `Quests`/`Events` (so `FeedRevisionWriteSeamTests` stays green), and EF Core sorts the DELETE/UPDATE commands by dependency. `GroupRepository.cs` is already on the closed `IgnoreQueryFilters` allowlist and already guarded by `AmbientClockSeamTests` (the code needs no clock), so keeping the work in that file touches no guard list. A preview query shares the same scoped-query helpers so the preview and the delete cannot drift.

Two findings change the plan beyond what CONTEXT assumed. (1) **The existing stale-board recovery does not cover a SuperAdmin**: `GroupSessionMiddleware` skips revalidation for `SuperAdmin`, and even for others it only runs every 5 minutes. A super admin who deletes the board they have selected keeps `ActiveGroupId` pointing at a row that no longer exists, and a quest create on that session would hit an FK violation (a 500). It needs a small fix (clear the actor's own session on delete, plus an existence check for super admins in the middleware). (2) **InMemory does not enforce FKs** and no existing test has any real relational check, which is why the bug shipped. The plan must add an FK-integrity sweep over the EF model for InMemory tests, a model-metadata completeness test that pins the set of board-owned FKs, and a SQL Server-gated test using the existing `ScratchDatabase` pattern.

Images are database blobs (`byte[]` in `CharacterImages` and `ContactImages`), not files. Nothing is stored on disk, so there is no file cleanup; both blob tables cascade from their parent in the database.

**Primary recommendation:** Implement `DeleteBoardAsync` and `GetDeletionPreviewAsync` inside `GroupRepository` (surfaced through the existing `IGroupService`), one `SaveChangesAsync`, no migration; fix the SuperAdmin stale-board gap; and add three layers of tests (InMemory behaviour + FK-integrity sweep, model-metadata completeness, SQL Server-gated real delete).

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|-------------|----------------|-----------|
| Which rows go / are unlinked, delete ordering, atomicity | Repository (EF) | — | Needs `DbContext`, `IgnoreQueryFilters`, tracker; Domain has no EF reference (architecture.md layer rules) |
| Orchestration: member recheck, typed-name check, result status | Domain (`GroupService`) | Repository (authoritative member recheck inside the delete) | Business rule; keeps controller thin; status enum mirrors `MoveStatus` |
| Preview data (counts + names) | Repository (projections) | Domain model `BoardDeletionPreview` | Same scoped queries as the delete |
| Confirmation page, typed-name gating, friendly errors, landing | Service (MVC controller + Razor + one JS module) | — | Presentation only; server re-checks the name |
| Active-board session cleanup | Service (session + middleware) | — | `ActiveGroupId` lives in ASP.NET session, not claims or a user column |
| Users board column | Repository (one grouped query) | Service view model + both views | Single query avoids N+1 |
| Database delete rules | Database / Storage | — | Unchanged; existing FKs already allow an explicit delete |

## Standard Stack

### Core
| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| Microsoft.EntityFrameworkCore (+SqlServer) | 10.0.9 | Change tracker, `IgnoreQueryFilters`, ordered DELETE batches | Already the repo's data layer [VERIFIED: QuestBoard.Repository/QuestBoard.Repository.csproj:10-11] |
| ASP.NET Core MVC 10 / Razor | 10.x | Platform area controller and views | Project constraint: no framework changes [VERIFIED: .claude/project-overview.md] |
| AutoMapper | existing | Not needed for this phase (preview is a direct projection) | — |

### Supporting (tests)
| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| xunit.v3 | 3.2.2 | Test framework (both test projects) | All new tests [VERIFIED: QuestBoard.UnitTests/QuestBoard.UnitTests.csproj:21] |
| FluentAssertions | 8.10.0 | Assertions | All new tests [VERIFIED: csproj:13] |
| NSubstitute | 5.3.0 | Substitutes for `IGroupService` etc. | Controller/middleware unit tests (UnitTests only) [VERIFIED: csproj:16] |
| Microsoft.EntityFrameworkCore.InMemory | 10.0.9 | Behavioural repository tests | Does not enforce FKs; see Validation Architecture [VERIFIED: csproj:14] |
| Microsoft.AspNetCore.Mvc.Testing | 10.0.9 | Integration tests (IntegrationTests only) | Controller/view flow [VERIFIED: IntegrationTests csproj:14] |

### Alternatives Considered
| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| Explicit ordered delete in one `SaveChanges` | Change Group FKs to `Cascade` | Rejected: needs a migration and SQL Server rejects multiple cascade paths (see Pitfall 1); model comments already say so |
| One `SaveChanges` | Explicit `BeginTransactionAsync` + several saves | Works (precedent: `EventSeriesRepository.CreateWithOccurrencesAsync` guards with `Database.IsRelational()`), but InMemory rejects transactions and a single save is already atomic; only needed if a cycle-breaking first save is ever required |
| `ExecuteDeleteAsync` | — | Do not use for `Quests`/`Events`: D-11 and `architecture.md` require the change tracker. The guard test bans only `ExecuteUpdate`/`ExecuteSql` by name, but the rule is stated for writes to `Events` and `Quests` in general |
| Logic in `GroupRepository` | New `BoardDeletionRepository` | A new file that calls `IgnoreQueryFilters` fails `CrossBoardIgnoreQueryFiltersSeamTests` until its path is added to `AllowedCallSites` (deliberate act). Keep it in `GroupRepository.cs` (also not a `partial` file: the allowlist is by file path) |

**Installation:** none. **No new packages.**

**Version verification:** no package is added; existing pins read from the csproj files above.

## Package Legitimacy Audit

No external packages are installed by this phase. Audit not applicable.

**Packages removed due to [SLOP] verdict:** none
**Packages flagged as suspicious [SUS]:** none

## Delete Map (the full FK graph)

Source of truth for every rule: `QuestBoardContext.cs` fluent configuration and `QuestBoardContextModelSnapshot.cs`. Quoted verbatim where the value matters.

### Direct references to `Groups`

| Dependent | FK | Delete rule | Action |
|-----------|----|-------------|--------|
| `UserGroups` | `GroupId` | `Cascade` (`.OnDelete(DeleteBehavior.Cascade)` at QuestBoardContext.cs:465-469) | Guard guarantees none; DB cascades any stragglers |
| `Quests` | `GroupId` | `NoAction` (QuestBoardContext.cs:328-333) | Delete |
| `ShopItems` | `GroupId` | `NoAction` (QuestBoardContext.cs:335-340) | Delete |
| `Characters` | `GroupId` | `NoAction` (QuestBoardContext.cs:342-347; snapshot 1247-1251) | Delete (D-02) |
| `Contacts` | `GroupId` | `NoAction` (QuestBoardContext.cs:349-354) | Delete |
| `ContactCategories` | `GroupId` | `NoAction` (QuestBoardContext.cs:356-364) | Delete |
| `ContactTags` | `GroupId` | `NoAction` (QuestBoardContext.cs:573-580) | Delete |
| `Events` | `GroupId` | `NoAction` (QuestBoardContext.cs:384-389) | Delete |
| `EventSeries` | `GroupId` | `NoAction` (QuestBoardContext.cs:391-396) | Delete |
| `Groups` -> `LinkedBoardSets` | `LinkedBoardSetId` | `SetNull`, on the Group's own row (QuestBoardContext.cs:311-315) | Nothing to do: the link vanishes with the row; the set stays (D-06). The LinkedBoards view already handles an empty set (`set.Boards.Count == 0` at Views/LinkedBoards/Index.cshtml:102) |

### Second-level references (rows hanging off board-owned rows)

| Dependent -> Principal | Delete rule | Evidence | Action |
|------------------------|-------------|----------|--------|
| `ProposedDates` -> Quest | Cascade | QuestBoardContext.cs:137-141 | Remove explicitly (convention: same result with or without DB cascade) |
| `PlayerDateVotes` -> ProposedDate and -> PlayerSignup | Cascade, Cascade | QuestBoardContext.cs:155-165 | Remove explicitly (votes of the board's quests) |
| `PlayerSignups` -> Quest | **NoAction** | QuestBoardContext.cs:143-147 | Delete explicitly **before** quests (EF orders it) |
| `ReminderLogs` -> Quest | **NoAction** | QuestBoardContext.cs:274-279 | Delete explicitly |
| `Quests` -> `OriginalQuest` (self, one-to-one) | `ClientSetNull`; migration adds the FK with no `onDelete` so SQL Server uses NO ACTION | QuestBoardContext.cs:264-272; Migrations/20260616205501_AddFollowUpQuestLink.cs:27-32 | Both ends on this board: deleted together (EF sorts). Follow-up on another board: unlink (below) |
| `PlayerSignups` -> Character (nullable) | **NoAction** | QuestBoardContext.cs:216-220 and 256-262 | Signups of this board's quests are deleted; signups on other boards using this board's characters: unlink (below) |
| `CharacterClasses` -> Character | Cascade | QuestBoardContext.cs:204-208 | Remove explicitly (small rows) |
| `CharacterImages` -> Character (`Id` is the FK) | Cascade | QuestBoardContext.cs:210-214; snapshot 1264-1273 | **Rely on the DB cascade** (blob rows, see Images) |
| `ContactNotes` -> Contact | Cascade | QuestBoardContext.cs:223-227 | Remove explicitly |
| `ContactImages` -> Contact | Cascade (snapshot 1312-1317) | snapshot | **Rely on the DB cascade** (blob rows) |
| `ContactContactTags` join -> Contact and -> Tag | Cascade, Cascade | snapshot 1157-1168 | Rely on the DB cascade (unmapped join table; both ends are deleted) |
| `Contacts` -> `ContactCategories` | `SetNull` | QuestBoardContext.cs:370-375 | Both deleted; EF sorts, no handling needed |
| `EventSignups` -> Event | Cascade | QuestBoardContext.cs:413-417 | Remove explicitly (so the tracked result is identical on every provider) |
| `Events` -> `EventSeries` (nullable) | **NoAction** | QuestBoardContext.cs:398-405 | Events and series deleted together; EF deletes events first |
| `UserTransactions` -> ShopItem | **NoAction**, required | snapshot 1590-1594 | Delete before shop items (EF orders it) |
| `UserTransactions` -> `OriginalTransaction` (self) | no `OnDelete` in snapshot, so default ClientSetNull / NO ACTION in SQL | snapshot 1586-1588; Migrations/20250911152034...:25-30 | Whole chain is deleted together; EF sorts by the self-FK |

### Cross-board reach-ins (rows on OTHER boards that point into the deleted board)

Swept by walking every FK whose principal is a board-owned table:

| Reach-in | Reaches | Found where | Action |
|----------|---------|-------------|--------|
| Follow-up `Quests.OriginalQuestId` on another board -> this board's quest | NO ACTION FK, would block | D-05 named it. `QuestMoveRepository.ApplyAsync` cuts both chain ends when a quest moves (`quest.OriginalQuestId = null; foreach (var followUp in followUps) followUp.OriginalQuestId = null;`), so these should be rare, but data from before Phase 92 or other paths can have them | Load with `IgnoreQueryFilters`, set `OriginalQuestId = null` |
| `PlayerSignups.CharacterId` on another board's quest -> this board's character | NO ACTION FK, would block | D-05 named it. `QuestMoveRepository` clears `CharacterId` on moved signups ("A character belongs to the board it was made on") | Load with `IgnoreQueryFilters`, set `CharacterId = null` |
| `Events.SeriesId` on another board -> this board's series | NO ACTION FK, would block | Not named in CONTEXT. `MoveEligibility.ForEvent` refuses to move any event with a `SeriesId` (`return MoveBlockReason.SeriesOccurrence;`), so this cannot arise through the UI today | **Defensive unlink**: set `SeriesId = null` and `SeriesSlotIndex = null` (both, as `EventSeriesRepository` does when it detaches occurrences; the filtered unique index covers `SeriesId`) |
| `ContactEntity.CategoryId` | `SetNull` at the DB | n/a | No code: database nulls it |
| Everything else (`TradeItems`, `CalendarSubscriptions`, `DungeonMasterProfiles`) | no board FK | grep of `GroupId` in `QuestBoard.Repository/Entities` found only the nine board-owned tables above | Untouched (D-04) |

"Home-page and page selections" are two int columns on the `Groups` row itself (`DisabledPages`, `HomePage`), and "calendar feed rows" are `FeedRevision`/`FeedRevisedAt` columns on `Events` and `Quests` plus the user-owned `CalendarSubscriptions`: all go with the row or are untouched, and nothing separate exists to delete.

## Architecture Patterns

### System Architecture Diagram

```
Platform Index (Delete link only if MemberCount == 0)
        |
        v
GET /platform/Group/Delete/{id}
   GroupController.Delete
        |-- groupService.GetByIdAsync -- null --> redirect Index
        |-- groupService.HasMembersAsync -- true --> TempData["Error"], redirect Index
        v
   groupService.GetDeletionPreviewAsync(id)
        v
   GroupRepository: scoped queries (IgnoreQueryFilters + Where GroupId == id)
        -> per-type {count, names[]}  + unlink list (other-board rows)
        v
   Delete.cshtml / Delete.Mobile.cshtml   (details lists, typed-name input,
                                           button disabled until JS sees an exact match)
        |
        v  POST /platform/Group/Delete/{id}   (antiforgery, SuperAdminOnly)
   GroupController.DeleteConfirmed(id, confirmName)
        v
   groupService.DeleteBoardAsync(id, confirmName)
        |-- group missing ......................... NotFound   -> redirect Index (info toast)
        |-- typed name != group.Name (ordinal) .... NameMismatch -> re-render page with refusal line
        |-- repository: members exist ............. HasMembers -> TempData["Error"], redirect Index
        v
   GroupRepository.DeleteBoardAsync
        load board-scoped rows (IgnoreQueryFilters, narrowed by groupId)
        + unlink rows on other boards (set FK null)
        + RemoveRange per type + Remove(group)
        v  ONE SaveChangesAsync  (FeedRevisionStamper runs; one DB transaction)
        |-- success ..... Deleted -> clear own session if it pointed here,
        |                            TempData["Success"], redirect Index
        |-- DbUpdateException / any failure -> ChangeTracker.Clear(), rethrow
                          -> controller logs, TempData["Error"], redirect back to Delete page
```

### Recommended Project Structure
```
QuestBoard.Domain/
├── Models/BoardDeletion/        # BoardDeletionPreview, BoardDeletionItem, BoardUnlinkItem, BoardDeleteStatus
├── Interfaces/IGroupRepository.cs, IGroupService.cs   # +3 members each
└── Services/GroupService.cs     # thin orchestration (name check, status mapping)
QuestBoard.Repository/GroupRepository.cs                # scoped-query helpers + preview + delete + board-name-by-user (single file)
QuestBoard.Service/
├── Areas/Platform/Controllers/GroupController.cs, UsersController.cs
├── Areas/Platform/Views/Group/Delete.cshtml, Delete.Mobile.cshtml     # rewritten
├── Areas/Platform/Views/Users/Index.cshtml, Index.Mobile.cshtml       # +column / +line
├── ViewModels/PlatformViewModels/BoardDeletePageViewModel.cs, PlatformUserViewModel.cs
├── Middleware/GroupSessionMiddleware.cs                               # SuperAdmin existence check
└── wwwroot/js/platform-board-delete.js, wwwroot/css/platform-group-delete.css (+ rules for the phone sheet)
```

### Pattern 1: Explicit-id board operation with narrowed `IgnoreQueryFilters`
**What:** The ambient filter answers for the session's selected board. A Platform operation on a board named by route id has to bypass it and re-impose scope by that id. `GroupRepository.RemoveMemberAsync` already does this; the delete follows the same shape. [VERIFIED: QuestBoard.Repository/GroupRepository.cs:134-155]
**When to use:** every query in preview and delete. Never an unscoped `IgnoreQueryFilters()`.
**Example (illustrative; property names verbatim from the entity files read this session):**
```csharp
// Each helper is the single definition of "what belongs to this board", used by preview AND delete.
private IQueryable<QuestEntity> BoardQuests(int groupId) =>
    DbContext.Quests.IgnoreQueryFilters().Where(q => q.GroupId == groupId);

private IQueryable<PlayerSignupEntity> BoardSignups(int groupId) =>
    DbContext.PlayerSignups.IgnoreQueryFilters().Where(ps => ps.Quest.GroupId == groupId);

private IQueryable<UserTransactionEntity> BoardTransactions(int groupId) =>
    DbContext.UserTransactions.IgnoreQueryFilters().Where(t => t.ShopItem.GroupId == groupId);
// ... same for ProposedDates, PlayerDateVotes (pdv.ProposedDate.Quest.GroupId), ReminderLogs,
// Events, EventSignups (es.Event.GroupId), EventSeries, Contacts, ContactNotes, ContactCategories,
// ContactTags, Characters, CharacterClasses, ShopItems.
```
Use navigation predicates, not `Contains(idList)`: no parameter-list size limits, no extra round trip.

### Pattern 2: One save, tracked removals, unlinks as property writes
```csharp
public async Task<BoardDeleteStatus> DeleteBoardAsync(int groupId, CancellationToken token = default)
{
    var group = await DbContext.Groups.FirstOrDefaultAsync(g => g.Id == groupId, token);
    if (group == null) return BoardDeleteStatus.NotFound;
    if (await DbContext.UserGroups.AnyAsync(ug => ug.GroupId == groupId, token))
        return BoardDeleteStatus.HasMembers;           // authoritative recheck (D-09)

    // Unlink rows that live on OTHER boards (D-05). Tracked property writes, never raw SQL.
    foreach (var q in await DbContext.Quests.IgnoreQueryFilters()
                 .Where(q => q.GroupId != groupId && q.OriginalQuest!.GroupId == groupId).ToListAsync(token))
        q.OriginalQuestId = null;
    foreach (var s in await DbContext.PlayerSignups.IgnoreQueryFilters()
                 .Where(s => s.Quest.GroupId != groupId && s.Character!.GroupId == groupId).ToListAsync(token))
        s.CharacterId = null;
    foreach (var e in await DbContext.Events.IgnoreQueryFilters()
                 .Where(e => e.GroupId != groupId && e.Series!.GroupId == groupId).ToListAsync(token))
    { e.SeriesId = null; e.SeriesSlotIndex = null; }

    // Remove the board's own rows. Do NOT Include() image navigations: never load blobs.
    DbContext.Set<PlayerDateVoteEntity>().RemoveRange(await BoardVotes(groupId).ToListAsync(token));
    DbContext.PlayerSignups.RemoveRange(await BoardSignups(groupId).ToListAsync(token));
    // ... ProposedDates, ReminderLogs, Quests, EventSignups, Events, EventSeries,
    //     ContactNotes, Contacts, ContactCategories, ContactTags,
    //     CharacterClasses, Characters, UserTransactions, ShopItems
    DbContext.Groups.Remove(group);

    try { await DbContext.SaveChangesAsync(token); }
    catch { DbContext.ChangeTracker.Clear(); throw; }   // same shape as QuestMoveRepository
    return BoardDeleteStatus.Deleted;
}
```
EF Core sorts the batch topologically by the model's FKs between the tracked rows, so dependents (votes, signups, transactions, events) are deleted before their principals and the unlink UPDATEs run before the principal's DELETE. The `FeedRevision` concurrency token adds `WHERE FeedRevision = @orig` to each Event/Quest DELETE; a lost race is retried by the context's own save override and anything else surfaces as an exception that the controller turns into a friendly error. [VERIFIED: QuestBoardContext.cs:76-116, 122-126]

### Pattern 3: Preview = the same scoped queries, projected
Names come from `Select(x => x.Title)` / `.Name`; count-only types use `CountAsync()`. Characters and contacts must be projected to their name, never loaded whole (image blobs are separate rows, but keep the habit). Order each list by name/date for a stable page. Unlink rows are shown separately: the other-board quest title with its board name (`q.Group.Name`), the count of signups that lose a character, and the count of defensive event unlinks (hidden at zero).

### Pattern 4: Controller status mapping (mirrors `MoveStatus` handling in `MoveController`)
`GroupController` already injects `IGroupService` and `ILogger<GroupController>`, so **no constructor change** is needed (two unit tests construct it with `new GroupController(...)` and would break on a new parameter: `GroupControllerEditConcurrencyTests.cs`). Existing friendly-error vocabulary: `TempData["Error"]` / `["Success"]` rendered by the shared toast partial that both Platform layouts include. [VERIFIED: _Layout.Platform.cshtml:60 and _Layout.Platform.Mobile.cshtml:71 render `_Toasts`]
- Name mismatch: re-render the confirm page with a `role="alert"` refusal line (not a toast), keeping the admin on the page.
- Members appeared: `TempData["Error"]` + redirect to Index (same text as today).
- Exception: `logger.LogError`, `TempData["Error"] = "The board could not be deleted and nothing was changed. Try again."`, redirect to the Delete page (board still exists).
- Success: `TempData["Success"]` one sentence naming the board, redirect to Platform `Group/Index` (the board list, where the Delete link already depends on `MemberCount == 0`).

### Anti-Patterns to Avoid
- **`ExecuteDelete`/`ExecuteSql`/`.Attach(`/`.Entry(` for any of this.** `FeedRevisionWriteSeamTests` scans all production source for these shapes. [VERIFIED: QuestBoard.UnitTests/Architecture/FeedRevisionWriteSeamTests.cs:43-54]
- **A second file calling `IgnoreQueryFilters`.** The allowlist is closed. [VERIFIED: CrossBoardIgnoreQueryFiltersSeamTests.cs:19-26]
- **`DateTime.Now/UtcNow/Today` in `GroupRepository.cs`.** `GroupRepository.cs` is on the `AmbientClockSeamTests` guard list. The delete needs no clock (the preview sorts by title). [VERIFIED: `"QuestBoard.Repository/GroupRepository.cs",` appears in both `GuardedRelativePaths` and `BoardClockConsumerPaths` in QuestBoard.UnitTests/Architecture/AmbientClockSeamTests.cs]
- **Using `BaseRepository.RemoveAsync`** (the current bug) or any `Include` chain over `Quests` -> signups -> votes (cartesian blow-up). Use the flat scoped queries.
- **Writing a `span` for visible text inside `.modern-card` on desktop**: `modern-card.css` forces a cream colour with `!important` on spans (STATE.md Phase 91 decision). Use plain text in a `<td>`/`<p>`/`<strong>`.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Delete ordering | A hand-written topological order of 15 RemoveRange calls with save-per-step | EF's own command sorting in one `SaveChangesAsync` | EF sorts by the model FKs, including the self-references; per-step saves lose atomicity |
| Atomicity | Manual `BeginTransaction`/`TransactionScope` | A single `SaveChangesAsync` | One save is one DB transaction; avoids the InMemory "transactions rejected" problem (`EventSeriesRepository` needs an `IsRelational()` guard for exactly that) |
| Friendly-error UI | A new error page or inline script toast | `TempData["Error"/"Success"]` + shared `_Toasts`; `.refusal-line` for in-page refusals | ui-guidelines.md: do not write toast markup |
| Typed-name gating | Inline `onkeyup`/`<script>` | A small ES module under `wwwroot/js` loaded through `@section Scripts` | CSP is strict; no inline handlers in new code (ui-guidelines "No inline code") |
| Membership count | A per-user query in the controller loop | One grouped query | The page already does an N+1 for lockout (`GetLockoutEndAsync`); do not add a second |
| FK enforcement in tests | Hoping InMemory complains | A model-driven FK-integrity sweep + a SQL Server-gated test | InMemory enforces no FKs, which is why this bug shipped |

**Key insight:** The hard part is not the delete, it is knowing nothing was missed. Put the "what belongs to a board" definition in one place (scoped query helpers), reuse it for preview and delete, and pin the set of board-owned FKs in a model-metadata test so a future table with a `GroupId` fails a test instead of a production delete.

## Images (discretionary item 1)

Character and contact images are **database blobs**, not files:
- `CharacterImages`: `public byte[] OriginalImageData { get; set; } = [];` and `public byte[]? CroppedImageData { get; set; }`, keyed by `Id` = the character's id. [VERIFIED: QuestBoard.Repository/Entities/CharacterImageEntity.cs]
- `ContactImages`: identical shape. [VERIFIED: ContactImageEntity.cs]
- `DungeonMasterProfileImages` also blobs, but a DM profile is user-owned and untouched (D-04).
- No file-system image storage exists: a grep of Service/Domain/Repository for `File.Delete|Directory.Delete|WriteAllBytes|FileStream|IWebHostEnvironment|uploads` found nothing relevant. Markdown images in quest/event descriptions are external URLs rendered by `IMarkdownService`; `MarkdownController` only renders a preview.
- Shop items, quests, events and boards have no image columns [VERIFIED: ShopItemEntity.cs, QuestEntity.cs, EventEntity.cs, GroupEntity.cs read in full].

**Resolution:** no file cleanup. Both blob tables cascade from their parent in the database (snapshot `OnDelete(DeleteBehavior.Cascade)`), so deleting the character/contact row removes its image. Do **not** `Include` the images to delete them explicitly: each can be up to 5 MB (`MaxFileSizeBytes = 5 * 1024 * 1024`, ImageValidationService.cs:9) and loading them only to delete them is wasteful. Consequence for tests: InMemory leaves orphan image rows (no DB cascade), so image removal is asserted by the model-metadata test (FK is Cascade) and the SQL Server-gated test, not by an InMemory row count. Document this in the test.

## Active board after the delete (discretionary item 2)

**How it is stored:** in ASP.NET Core **session**: `SessionKeys.ActiveGroupId`, `ActiveGroupName`, `ActiveGroupValidatedAtUtc`; `ActiveGroupContextService.ActiveGroupId` reads `Session.GetInt32(SessionKeys.ActiveGroupId)`. Not a cookie claim and not a user column. [VERIFIED: QuestBoard.Service/Constants/SessionKeys.cs, Services/ActiveGroupContextService.cs]

**What the existing recovery does** (`GroupSessionMiddleware`, lines 131-164): it re-verifies membership via `GetGroupRoleByIdAsync` at most every 5 minutes, and `if (needsRevalidation && !context.User.IsInRole("SuperAdmin"))` excludes super admins entirely. A no-longer-member is cleared and sent to `/groups/pick` (GET) or 409 (non-GET).

**Verdict:**
- *Ordinary users (ex-members):* recovery works, with a window of up to 5 minutes in which the session still names the dead board (reads return nothing). Same bound as for any removed member today; acceptable, not part of this phase.
- *Super admin: recovery does NOT work.* Revalidation is skipped for them, so `ActiveGroupId` stays on the deleted id for the life of the session. `/platform` is an exempt path, so the deleting admin keeps the stale id after the redirect. Reads return nothing and `BoardTypeResolver`/`BoardPageResolver` return null (they handle a missing row), but a write that stamps the active board (for example `quest.GroupId = activeGroupContext.RequireActiveGroupId()` in `QuestController` line 134) would insert a row whose FK points at a deleted `Groups` row, which SQL Server rejects: a 500.

**Recommended fix (small, two parts):**
1. In `DeleteConfirmed`, after a successful delete, if the actor's session `ActiveGroupId` equals the deleted id, remove the three session keys (await `HttpContext.Session.LoadAsync` first; the switcher service does the same for the same reason). Put the removal in one shared helper (an extension on `ISession` or a `ClearAsync` on `IActiveBoardSwitcher`, whose doc says it is "the one place ... permitted to write the three active-board session keys") used by both the middleware and the controller, so there is one list of keys. An extension method avoids a new constructor parameter on `GroupController`.
2. In `GroupSessionMiddleware`, for a `SuperAdmin` whose revalidation is due, check that the group still exists (`IGroupService.GetByIdAsync`, a primary-key lookup once per 5 minutes) and, if not, clear the keys and gate exactly as the no-longer-member path does. This covers other super admins' sessions.

Tests to adjust: `GroupSessionMiddlewareRevalidationTests.SuperAdmin_StaleTimestamp_InvokesNextWithoutMembershipCheck` builds a `ServiceCollection` with only `IUserService` and `IActiveGroupContext`; once the middleware resolves `IGroupService` for super admins that test needs an `IGroupService` substitute that returns an existing group (it keeps its intent: no *membership* check for a super admin). Add two new facts: super admin + deleted group -> redirect/409 and keys cleared; super admin + existing group -> passes and restamps. [VERIFIED: GroupSessionMiddlewareRevalidationTests.cs:28-70, 144-150]

Minor, no action: `AgendaBoardFilter` and `ShowHiddenContacts_{groupId}` session values may name the deleted id; the agenda re-derives scope from the viewer's memberships on every request (STATE.md / middleware comments) so a dead id there only narrows to nothing for a member who also kept other boards; leave as is. [ASSUMED: not traced through AgendaController]

## Common Pitfalls

### Pitfall 1: Trying to fix it in the schema
**What goes wrong:** Changing a Group FK to `Cascade` (or making `Characters.GroupId`/`UserTransactions.ShopItemId` nullable) needs a migration and, for Group -> Contact/ContactCategory/Quest, SQL Server rejects the multiple cascade paths at schema creation. **Evidence:** the model's own comment on `ContactCategoryEntity -> Group`: "a cascade from Group to ContactCategories combined with the category-to-contact path below converges two delete paths onto Contacts, which SQL Server rejects at schema-creation time." [VERIFIED: QuestBoardContext.cs:356-359] **Avoid:** explicit ordered delete; **no migration**.

### Pitfall 2: Believing the green InMemory test
**What goes wrong:** The existing `DeleteGroup_WhenEmpty_PostShouldRedirectToIndex` deletes an *empty* group on InMemory, so it passes while production fails on any group with content. InMemory enforces no FKs and runs no DB cascades. **Avoid:** seed a full graph and run the FK-integrity sweep (below); add the SQL Server-gated test.

### Pitfall 3: The ambient filter silently hides the rows
**What goes wrong:** `Quests`, `ShopItems`, `Characters`, `Contacts`, `ContactCategories`, `ContactTags`, `Events`, `EventSeries` and their dependents all have fail-closed query filters keyed to the session's active board (QuestBoardContext.cs:480-653); with a null or different active board a plain query returns zero rows, the delete "succeeds" for the Group row and then fails on the FK, or worse deletes only some rows. **Avoid:** every query in preview and delete uses `IgnoreQueryFilters()` narrowed by the explicit id, and a test runs the delete with `ActiveGroupId` set to *another* board and to `null`.

### Pitfall 4: Cross-board contamination
**What goes wrong:** An `IgnoreQueryFilters()` query missing its `Where(GroupId == id)` removes other boards' data. **Avoid:** the test "other board's full graph is byte-for-byte unchanged after deleting board A".

### Pitfall 5: Loading blobs
**What goes wrong:** `Include(c => c.ProfileImage)` pulls up to 5 MB per character/contact into the tracker. **Avoid:** do not include image navigations; the DB cascade removes them.

### Pitfall 6: The friendly error must not leave a half-built tracker
**What goes wrong:** After a failed `SaveChangesAsync` the scoped context still tracks the staged deletes; any later save in the same request would retry them. **Avoid:** `ChangeTracker.Clear()` in the catch (the shape `QuestMoveRepository` and `EventMoveRepository` use), then rethrow for the controller to map.

### Pitfall 7: Concurrent recurring-occurrence job
**What goes wrong:** `RecurringOccurrenceTopUpJob` can insert an occurrence for a series between the load and the save, making the series DELETE hit an FK. **Result:** the single transaction rolls back and the admin sees the friendly error; a retry succeeds. Accept and say so; no locking.

### Pitfall 8: Member re-added between recheck and save
**What goes wrong:** the tiny window after the member recheck: a membership added then would be removed silently by the `UserGroups` cascade. **Mitigation:** the repository does the authoritative recheck as its first step, immediately before staging the deletes. Membership is only added through Platform/invite actions by admins, so the window is accepted (D-09 already says the preview need not match exactly).

### Pitfall 9: Pending Hangfire jobs for deleted quests
**What goes wrong:** reminder/finalized-email jobs enqueued for a quest id. **Result:** `SessionReminderJob` logs "quest {QuestId} not found, skipping." and returns, and the other jobs run through `HangfireJobHelper.RunInScopeAsync` with the same lookup. No action. [VERIFIED: SessionReminderJob.cs:26-37]

### Pitfall 10: View conventions for new Platform markup
Both Platform layouts render `Styles` and `Scripts` sections. New markup must have no inline `style`, `<script>` or `on*` handlers, tokens only (no hex, no `!important`), page sheet in `@layer pages`, mobile rules in a `*.mobile.css` with no `@media`. Do not copy the existing Users `onclick="return confirm(...)"` into new code. The existing `platform-group.mobile.css` still contains literals and `!important` (Phase 100 will convert it): add new phone rules in a new `platform-group-delete.mobile.css` sheet that is clean, rather than extending the legacy sheet, and keep existing sheets' contents otherwise untouched because `BoardPageStaticGuardTests` reads `platform-group.mobile.css`. [VERIFIED: ROADMAP Phase 100 scope; platform-group.mobile.css read]

## Code Examples

### Typed-name gate (ES module, loaded via `@section Scripts`)
```javascript
// wwwroot/js/platform-board-delete.js — enables the delete button only on an exact match.
const input = document.querySelector('[data-board-delete-name]');
const button = document.querySelector('[data-board-delete-submit]');
if (input && button) {
    const sync = () => { button.disabled = input.value.trim() !== input.dataset.boardDeleteName; };
    input.addEventListener('input', sync);
    sync();
}
```
The Razor side renders `disabled` on the button by default and the board name HTML-encoded into `data-board-delete-name`. The server remains authoritative: `string.Equals(confirmName?.Trim(), group.Name, StringComparison.Ordinal)` (trim only the typed side; the ordinal match is the "exact name" of D-08).

### Per-type `<details>` row (desktop, inside the card; plain text, no visible span)
```cshtml
@foreach (var item in Model.Preview.Deleted.Where(i => i.Count > 0))
{
    <div class="board-delete-type">
        <p class="mb-1"><i class="fas @item.Icon me-2" aria-hidden="true"></i><strong>@item.Label</strong>: @item.Count</p>
        @if (item.Names.Count > 0)
        {
            <details>
                <summary>Show names</summary>
                <ul>@foreach (var name in item.Names) { <li>@name</li> }</ul>
            </details>
        }
    </div>
}
```
Names are user-authored (quest titles, contact names): Razor encoding is the XSS control; never `Html.Raw`.

### Users column (one query)
```csharp
// GroupRepository: user id -> alphabetical board names. UserGroups and Groups carry no query filter.
var rows = await DbContext.UserGroups
    .Select(ug => new { ug.UserId, ug.Group.Name })
    .ToListAsync(token);
return rows.GroupBy(r => r.UserId)
           .ToDictionary(g => g.Key, g => (IReadOnlyList<string>)g.Select(r => r.Name).OrderBy(n => n).ToList());
```
`UsersController` gains an `IGroupService` parameter (only integration tests build it, through DI) and `PlatformUserViewModel` gains `BoardNames`. Desktop: the count as plain text in a `<td>` with `title="@string.Join(", ", names)"` on the `<td>`; phone: the count plus a small `<details>` listing the names (hover does not exist on touch). A user on zero boards shows `0`.

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| Delete the `Groups` row and hope | Explicit scoped delete, one save, FK-aware tests | This phase | Fixes the 500 and the silent-orphan class of bug |
| Test FKs with InMemory | Model-driven FK sweep + SQL Server-gated test | This phase | InMemory never could catch this |

**Deprecated/outdated:** the `Delete.cshtml`/`Delete.Mobile.cshtml` "Are you sure?" pages (a bare confirmation with no preview) are replaced; the "(only empty groups)" comment on `Delete` is wrong once content can be deleted.

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|---------------|
| A1 | EF Core 10 topologically sorts the batched DELETE/UPDATE commands across all tracked rows, including the two self-referencing FKs, without a cycle-breaking first save | Pattern 2 | A circular-dependency exception at save time. Cycles cannot arise from the UI (one-to-one follow-up links, return rows point at earlier purchases), and the SQL Server-gated test would catch it; fallback is a first save nulling `OriginalQuestId`/`OriginalTransactionId` on the board's own rows inside an `IsRelational()`-guarded explicit transaction |
| A2 | A single multi-command `SaveChangesAsync` is wrapped in one database transaction by the SQL Server provider | Summary / Pattern 2 | If wrong, D-11 needs an explicit `BeginTransactionAsync` (precedent exists in `EventSeriesRepository`). This is documented EF behaviour but was not re-verified against docs in this session |
| A3 | Deleting `Events`/`Quests` rows needs no feed revision change (the stamper only handles `Added` and `Modified`) and the board has no members so none of its entries are in anyone's feed | Summary | A calendar client keeps a stale entry until its next poll; low impact and the rule in D-11 is satisfied by the tracker path regardless |
| A4 | `AgendaBoardFilter`/`ShowHiddenContacts_*` leftovers are harmless | Active board | A confusing "all boards filtered out" page for one user; trivial to fix later |
| A5 | The phone-sheet approach (new `platform-group-delete.mobile.css`) will not trip `MobileCssTests`/`UiFoundation*` guards | Pitfall 10 | A guard test fails and the sheet needs adjusting; run the guards in the first UI task |
| A6 | `ui_phase` is `true` in `.planning/config.json`, so plan-phase may require a `102-UI-SPEC.md` before the view tasks | Open Questions | The workflow's UI gate blocks planning until the operator generates one or waives it |

## Open Questions (RESOLVED)

1. **UI spec gate.** RESOLVED: `102-UI-SPEC.md` exists and is approved (2026-11-11); plans 102-07 (page) and 102-08 (sheets and gate) implement it.
   - What we know: `.planning/config.json` has `ui_phase: true` and `ui_safety_gate: true`; Phase 93 was blocked by exactly this gate (STATE.md).
   - What's unclear: whether the operator wants a UI-SPEC for one confirmation page and one table column.
   - Recommendation: planner asks once; if waived, follow `.claude/ui-guidelines.md` plus the Phase 92 Move page as the visual precedent.
   - RESOLVED: no waiver needed; the UI-SPEC was produced and approved before planning.

2. **Overlap with Phase 100.** RESOLVED: the Delete twins, partial and sheets are built to Phase 100's definition of converted (plans 102-07, 102-08), so Phase 100 only verifies them; the Users views keep their legacy markup apart from the new column, phone block and sheet (plan 102-03) and remain Phase 100's to convert. Phase 100 (not yet planned) will convert `Group/Delete*` and `Users/Index*`. Phase 102 rewrites the Delete twins and edits the Users twins first.
   - Recommendation: build the new Delete markup to Phase 100's "definition of converted" now (so there is nothing to redo), touch only the minimal Users markup (one column / one line), and leave the existing inline `onclick` on Users for Phase 100.

3. **Where does the typed name get checked: service or controller?** RESOLVED: in `GroupService.DeleteBoardAsync(id, confirmName)` returning `BoardDeleteStatus.NameMismatch` (plan 102-01; service facts in plan 102-05). Either works; recommended in `GroupService.DeleteBoardAsync(id, confirmName)` so a status enum carries all outcomes and the unit test is on the service.

4. **Success wording.** RESOLVED: one sentence, `Board "{Name}" was deleted.`, no row counts (set in plan 102-01, kept by plan 102-09). One sentence naming the board; whether to include row counts is a taste call (recommend no: keep to one sentence, the toast persists only 5 s).

## Environment Availability

| Dependency | Required By | Available | Version | Fallback |
|------------|------------|-----------|---------|----------|
| .NET SDK | build/test | yes | 10.0.115 (`dotnet --version`) | — |
| SQL Server on localhost:1433 | optional gated delete test, human UAT | port open (probed read-only) | not queried | Gated test skips when `QUESTBOARD_MIGRATOR_TEST_CONNECTION` is unset; it is not set in this shell |
| `QUESTBOARD_MIGRATOR_TEST_CONNECTION` | gated SQL Server test | no (unset) | — | Test uses `Assert.Skip`, as `MigratorSqlServer` does |

The local database holds real member data. The gated test must create and drop its own uniquely named scratch database (the existing `ScratchDatabase` helper does exactly that) and must never touch the app's database. Per CLAUDE.md: do not start, install or tear down SQL Server; if a UAT step needs it, ask the user.

**Missing dependencies with no fallback:** none.

## Validation Architecture

### Test Framework
| Property | Value |
|----------|-------|
| Framework | xUnit v3 3.2.2, FluentAssertions 8.10.0, NSubstitute 5.3.0, EF InMemory 10.0.9 |
| Config file | `QuestBoard.IntegrationTests/xunit.runner.json` (no parallelism); no extra config for UnitTests |
| Quick run command | `dotnet test QuestBoard.UnitTests --filter "FullyQualifiedName~BoardDeletion"` |
| Full suite command | `dotnet test` |

Run commands per CLAUDE.md: never set `DOTNET_GCHeapHardLimit`; a `Fatal error. Internal CLR error. (0x80131506)` is a flake, re-run once; a locked-file build failure means the user has the debugger running.

### Phase Requirements -> Test Map

| Behaviour | Test Type | Automated Command | File Exists? |
|-----------|-----------|-------------------|-------------|
| D-01/02/03: every board-scoped type is removed (quests, proposed dates, votes, signups, reminder logs, events, event signups, series, contacts, notes, categories, tags, characters, classes, shop items, transactions incl. chained returns, group row) | unit (InMemory) | `dotnet test QuestBoard.UnitTests --filter "FullyQualifiedName~GroupRepositoryBoardDeletionTests"` | ❌ Wave 0 |
| Another board's full graph is unchanged; delete works with active board = other board and = null | unit | same filter | ❌ Wave 0 |
| D-04: users, DM profiles + images, trade items, calendar subscriptions survive | unit | same filter | ❌ Wave 0 |
| D-05: other-board follow-up `OriginalQuestId` cleared and row kept; other-board signup `CharacterId` cleared and row kept; defensive event `SeriesId`/`SeriesSlotIndex` cleared | unit | same filter | ❌ Wave 0 |
| D-06: set row remains, board gone | unit | same filter | ❌ Wave 0 |
| D-09: members present -> `HasMembers`, nothing deleted; unknown id -> `NotFound` | unit | same filter | ❌ Wave 0 |
| D-11: exactly one `SaveChanges` call (count via `context.SavingChanges`) | unit | same filter | ❌ Wave 0 |
| D-11: save failure clears the tracker and rethrows (throw from a `SavingChanges` handler) | unit | same filter | ❌ Wave 0 |
| FK completeness: no non-cascade FK in the model has a dangling row after the delete (model-driven sweep) | unit | same filter | ❌ Wave 0 (needs sweep helper) |
| Model pin: set of FKs referencing `GroupEntity` and the non-cascade FKs of board-owned tables equals the handled list; image/join FKs are `Cascade` | unit | `dotnet test QuestBoard.UnitTests --filter "FullyQualifiedName~BoardOwnedForeignKeys"` | ❌ Wave 0 |
| Preview parity: per-type counts equal rows actually removed; zero-count types absent; names listed for named types, counts only for signups/votes/transactions/reminders | unit | same filter as first row | ❌ Wave 0 |
| D-08: wrong/missing typed name -> `NameMismatch`, nothing deleted | unit (service) | `dotnet test QuestBoard.UnitTests --filter "FullyQualifiedName~GroupServiceBoardDeletion"` | ❌ Wave 0 |
| Controller: exception -> `TempData["Error"]` + redirect, never rethrown; mismatch re-renders; success -> Index + `TempData["Success"]`; own session cleared | unit (controller) | `dotnet test QuestBoard.UnitTests --filter "FullyQualifiedName~GroupControllerBoardDeletion"` | ❌ Wave 0 |
| Middleware: super admin + deleted group -> keys cleared, redirect/409; existing group -> passes; updated existing test | unit | `dotnet test QuestBoard.UnitTests --filter "FullyQualifiedName~GroupSessionMiddlewareRevalidationTests"` | ✅ exists, edit + add |
| D-07/D-10: GET Delete renders counts, collapsed `<details>`, other-board unlink section, disabled button and typed-name input (desktop and phone UA); POST with right name deletes a seeded graph and redirects; POST wrong name keeps the board; members added -> refused | integration | `dotnet test QuestBoard.IntegrationTests --filter "FullyQualifiedName~PlatformBoardDelete"` | ❌ Wave 0 (extend `GroupManagementIntegrationTests` patterns) |
| Existing delete tests still meaningful: `DeleteGroup_WhenEmpty_PostShouldRedirectToIndex` posts no name and would now re-render; update it to post the name and assert the row is gone | integration | `dotnet test QuestBoard.IntegrationTests --filter "FullyQualifiedName~GroupManagementIntegrationTests"` | ✅ exists, edit |
| D-12/D-13: `/platform/Users` desktop and phone show the board count and names (hover/details); user on zero boards shows 0; no sort/filter controls | integration | `dotnet test QuestBoard.IntegrationTests --filter "FullyQualifiedName~UsersControllerIntegrationTests"` | ✅ exists, extend |
| Real SQL Server accepts the delete of a fully populated board and rolls back on failure | integration (gated) | `QUESTBOARD_MIGRATOR_TEST_CONNECTION=... dotnet test QuestBoard.IntegrationTests --filter "FullyQualifiedName~BoardDeletionSqlServer"` | ❌ Wave 0 (skips when unset) |
| No inline style/script/handler in the new views; sheets token-only; scripts under `wwwroot/js` | integration (static) | `dotnet test QuestBoard.IntegrationTests --filter "FullyQualifiedName~UiFoundation"` plus a new fact for the two Delete views | ✅ guards exist; new fact ❌ |
| Guards stay green: `FeedRevisionWriteSeamTests`, `CrossBoardIgnoreQueryFiltersSeamTests`, `AmbientClockSeamTests`, `BoardMoveRepositorySingleSaveTests` | unit (architecture) | `dotnet test QuestBoard.UnitTests --filter "FullyQualifiedName~Architecture"` | ✅ exist |

### How the FK-integrity sweep works (the InMemory gap)
InMemory neither rejects an FK violation nor runs DB cascades. A reusable test helper `AssertNoDanglingNonCascadeForeignKeys(QuestBoardContext)` closes the gap: for every entity type in `context.Model.GetEntityTypes()` (skip shared-type join entities), for every `GetForeignKeys()` whose `DeleteBehavior` is `NoAction`, `Restrict`, `ClientSetNull`, `ClientNoAction` or `ClientCascade`, load the dependent rows (via the generic `DbContext.Set<T>()` through reflection) and assert each non-null FK value still has a principal row. That is precisely what SQL Server would reject. Cascade FKs are skipped because the database, not the test provider, would remove those rows. Run it after the delete in the full-graph test and in the "other board untouched" test. Test code may use `Entry`/`IgnoreQueryFilters` freely; the production-source scans exclude both test projects (`ScannedProductionRoots` are Repository, Domain, Service). [VERIFIED: CrossBoardIgnoreQueryFiltersSeamTests.cs:28-35]

### The model-pin test (completeness guard)
A fact that reads `context.Model.FindEntityType(typeof(GroupEntity)).GetReferencingForeignKeys()` and asserts the exact set `{UserGroups: Cascade; Quests, Characters, Events, EventSeries, Contacts, ContactCategories, ContactTags, ShopItems: NoAction}`, plus a second set for the non-cascade FKs among board-owned tables (`PlayerSignups->Quests`, `ReminderLogs->Quests`, `Quests->OriginalQuest`, `PlayerSignups->Characters`, `Events->EventSeries`, `UserTransactions->ShopItems`, `UserTransactions->OriginalTransaction`) and asserts the image and join FKs are `Cascade`. A future table with a `GroupId` fails this test, with a message saying the board delete must be extended. This works on InMemory (it only reads metadata).

### SQL Server-gated test
`BoardDeletionSqlServerTests` uses `MigratorSqlServer.RequireServerConnectionString()` (skips when `QUESTBOARD_MIGRATOR_TEST_CONNECTION` is unset, fails when `QUESTBOARD_MIGRATOR_TEST_REQUIRED=1`), a `ScratchDatabase`, `UseSqlServer(scratch.ConnectionString)` on `QuestBoardContext` with a `MutableGroupContext`, `Database.EnsureCreated()` (the schema comes from the same model, including every `OnDelete`), seeds one of everything, deletes through `GroupRepository`, and asserts no exception, all rows gone, other board intact. A second fact proves rollback: make the save fail after the deletes are staged (a handler on `SavingChanges` that throws) and check the board and every row are still present. This is the only automated proof of real FK and cascade behaviour (blob cascade, join-table cascade, NO ACTION ordering). The provider is already transitively available (`Microsoft.EntityFrameworkCore.SqlServer` 10.0.9 via the Repository project; `MigratorSqlServer` already uses `Microsoft.Data.SqlClient`). It is optional in CI and runs when the user supplies a connection.

### Sampling Rate
- **Per task commit:** the filtered quick command for the layer touched (`BoardDeletion` unit filter; `PlatformBoardDelete` integration filter).
- **Per wave merge:** `dotnet test QuestBoard.UnitTests` then `dotnet test QuestBoard.IntegrationTests`.
- **Phase gate:** `dotnet test` green, plus one human pass on the local SQL Server (the user's environment, started by them): create a throwaway board with a quest + signup + vote, a character used by a signup on another board, an event + series, a contact with image and tag, a shop item with a purchase and a return; empty its members through Platform; delete; confirm no 500, the board is gone, the other board is intact, the selected-board session recovers, desktop and a phone user agent both render.

### Wave 0 Gaps
- [ ] `QuestBoard.UnitTests/Repository/GroupRepositoryBoardDeletionTests.cs` - behaviour tests and the FK-integrity sweep helper (own private `TestActiveGroupContext`, as other repository tests define theirs)
- [ ] `QuestBoard.UnitTests/Repository/BoardOwnedForeignKeysTests.cs` - model-pin test
- [ ] `QuestBoard.UnitTests/Services/GroupServiceBoardDeletionTests.cs`, `QuestBoard.UnitTests/Controllers/GroupControllerBoardDeletionTests.cs`
- [ ] `QuestBoard.IntegrationTests/Controllers/PlatformBoardDeleteTests.cs` (GET/POST flows, desktop and phone UA using the `Mobile Safari` user-agent header pattern in `AgendaMobileRenderTests`), a Users-column facts block, and a static guard fact for the two Delete views
- [ ] `QuestBoard.IntegrationTests/Tests/BoardDeletionSqlServerTests.cs` - gated
- [ ] Framework install: none

## Security Domain

`security_enforcement` is not set to false in `.planning/config.json`, so this section applies.

### Applicable ASVS Categories

| ASVS Category | Applies | Standard Control |
|---------------|---------|-----------------|
| V2 Authentication | no (unchanged) | existing Identity cookie auth |
| V3 Session Management | yes | Active-board session keys must be cleared when the board is deleted (see Active board); no new session data |
| V4 Access Control | yes | `[Authorize(Policy = "SuperAdminOnly")]` is on `GroupController` and `UsersController` at class level, so Delete GET/POST and the Users page stay super-admin-only [VERIFIED: GroupController.cs:19-20, UsersController.cs:7-8]; the delete takes the board id from the route and re-reads the row, so there is no client-trusted scope |
| V5 Input Validation | yes | The only new input is `confirmName`: compared server-side, ordinal, against the stored name; never used in a query or path. Route id is an int |
| V6 Cryptography | no | none |
| V7 Logging | yes (light) | Log one Information line per delete: acting user id, board id and name, and per-type row counts (no row contents) so a one-way deletion has an audit trail |
| V8 Data Protection | yes | Deletion is the data-protection-friendly direction; it removes user-authored content (notes, characters) with the board, as decided in D-01/D-02 |
| V13/V14 API & config | n/a | no new endpoint; same two actions |

### Known Threat Patterns for ASP.NET Core MVC + EF Core

| Pattern | STRIDE | Standard Mitigation |
|---------|--------|---------------------|
| CSRF on a destructive POST | Tampering | `[ValidateAntiForgeryToken]` stays on `DeleteConfirmed`; `AntiForgeryTokenCoverageTests` already pins POST coverage; keep the form on the tag helper so the token renders |
| Stored XSS via user-authored names in the preview (quest titles, contact names, event titles) | Tampering / Info disclosure | Razor HTML encoding only; never `Html.Raw`; names go into `<li>` text and the board name into a data attribute |
| Tenant leakage / cross-board deletion by a missing scope | Elevation / Tampering | Every `IgnoreQueryFilters()` narrowed by the explicit group id; "other board unchanged" test; no unscoped bypass |
| Accidental deletion of the wrong board | Tampering | Typed exact board name checked server-side, full per-type preview, members recheck (D-08/D-09) |
| Information disclosure to a non-admin | Info disclosure | Preview and delete are only reachable through the `SuperAdminOnly` actions; the preview adds no new route |
| Open redirect | Tampering | Redirects are fixed `RedirectToAction`; the existing `/groups/pick?returnUrl=` handling is untouched |

## Sources

### Primary (HIGH confidence): this repository, read this session
- `QuestBoard.Repository/Entities/QuestBoardContext.cs` (full) - FK delete rules, query filters, save overrides
- `QuestBoard.Repository/Migrations/QuestBoardContextModelSnapshot.cs` (relationship blocks) and `20260616205501_AddFollowUpQuestLink.cs`, `20250911152034_AddOriginalTransactionIdToUserTransactions.cs`
- `QuestBoard.Repository/GroupRepository.cs`, `QuestMoveRepository.cs`, `EventMoveRepository.cs`, `EventSeriesRepository.cs` (transaction guard), `BaseRepository.cs`, `FeedRevisionStamper.cs`, `UserRepository.cs`
- `QuestBoard.Domain/Services/GroupService.cs`, `Interfaces/IGroupService.cs`, `IGroupRepository.cs`, `Extensions/MoveEligibility.cs`
- `QuestBoard.Service/Areas/Platform/Controllers/GroupController.cs`, `UsersController.cs`, views `Group/Delete*.cshtml`, `Group/Index*.cshtml`, `Users/Index*.cshtml`, `LinkedBoards/Index.cshtml`, both Platform layouts, `Middleware/GroupSessionMiddleware.cs`, `Services/ActiveGroupContextService.cs`, `ActiveBoardSwitcherService.cs`, `Constants/SessionKeys.cs`, `Controllers/GroupPickerController.cs`, `MoveController.cs`, `Jobs/SessionReminderJob.cs`
- Guard tests: `FeedRevisionWriteSeamTests`, `CrossBoardIgnoreQueryFiltersSeamTests`, `AmbientClockSeamTests`, `BoardMoveRepositorySingleSaveTests`, `GroupSessionMiddlewareRevalidationTests`, `GroupControllerEditConcurrencyTests`, `GroupManagementIntegrationTests`, `WebApplicationFactoryBase`, `TestDatabase`, `MigratorSqlServer`/`ScratchDatabase`
- `.claude/architecture.md`, `.claude/ui-guidelines.md`, `.planning/ROADMAP.md` (Phases 100, 102), `.planning/STATE.md`, `.planning/config.json`

### Secondary (MEDIUM confidence)
- None used. No external documentation was consulted; EF Core command-ordering and implicit-transaction behaviour (A1, A2) are from established EF Core behaviour and are flagged in the Assumptions Log with a cheap test-based fallback.

### Tertiary (LOW confidence)
- None.

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH - no new packages; existing pins read from csproj files
- Architecture (delete map, ordering, placement): HIGH for the FK graph and guard constraints (read from source); MEDIUM on A1/A2 (EF behaviour, mitigated by tests and a documented fallback)
- Active-board gap: HIGH - the middleware condition and the unit test that pins it were read directly
- Pitfalls: HIGH - each ties to a read source line or guard test

**Research date:** 2026-11-11
**Valid until:** 2026-12-11 (stable; revisit if a new table with a `GroupId` or a new `IgnoreQueryFilters` site lands first, since the allowlist and the model-pin test both depend on the current set)
