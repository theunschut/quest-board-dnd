# Phase 91: Linked Boards with a Shared Calendar - Research

**Researched:** 2026-10-15
**Domain:** ASP.NET Core 10 MVC multi-tenant calendar (EF Core 10.0.9 / SQL Server), cross-tenant read-only projection, Razor desktop + `.Mobile` twins
**Confidence:** HIGH on codebase facts (all opened this session), MEDIUM on SQL translation of the new window predicates (InMemory cannot prove it, SQL Server not reachable from this sandbox)

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions

- **D-01: Same-type links only, enforced server-side.** Confirmed against the real case that started this phase: the operator's existing board and the new one-shot board are both One-Shot. `BoardType` cannot change after creation (Edit shows it disabled), so a same-type group cannot later become mixed. Enforcement lives on the server path that adds a board to a group, not only in the form's option list. On a Campaign-type group the existing events-only rule still holds: a Campaign board's calendar never loads quests, its own or a linked board's.
- **D-02: A named link group; a board belongs to at most one.** The super admin creates a group with a name and adds boards to it. Every board in a group sees every other board in it, so links are transitive by construction and there is no "A-B and B-C, does A see C" case. The operator first answered "you decide" by an early click and then chose the named group explicitly on re-ask; record it as the operator's choice, not Claude's. Rejected: pairwise links. Reversibility: costly - the schema ships as an EF Core migration (a group table plus a board-to-group reference).
- **D-03: Deleting a board removes it from its group automatically.** Delete stays one confirm, exactly as today (Platform Delete is already only allowed for boards with no members). No "unlink first" step.
- **D-04: A group left with one board (or none) is kept and shown as inactive.** It stays on the Linked boards page with its name so the super admin can add a board back without recreating it. A group of one adds nothing to any calendar. Removing a group is an explicit action on the page.
- **D-05: A dedicated "Linked boards" page in the Platform area, under `SuperAdminOnly`.** Lists every group with its boards, its state (active / inactive) and its board type; supports create, rename, add a board, remove a board and delete the group. New page with its `.Mobile.cshtml` twin, using the Platform layouts. The board picker when adding a board offers only boards of the group's type that are not already in another group; the server re-checks both rules.
- **D-06: A non-member of linked board B sees B's entries in full, as plain text, not clickable.** Title, time, B's board name and (for quests) the DM's name. No link, because a link would 404 for them under Phase 87's rules. Deliberate divergence from Phase 82 D-15 / Phase 87 D-10: the super admin's act of linking *is* the authorization.
- **D-07: A linked quest carries title, time, board name and DM name, and nothing else.** No building-key warning, no finalized tick derived from signups, no votes, no roster, no signup state. The read is a **narrow projection** that never loads signups, votes or players. The existing `ProjectForCalendar` must not be reused for linked boards. A linked event carries title, date, start time label and board name.
- **D-08: For a member of board B, B's entry is clickable and goes through the Phase 87 board switch.** The link is the ordinary `/Quest/Details/{id}` or `/Events/Details/{id}` URL; `CrossBoardDeepLinkMiddleware` switches the session to B and shows the banner. Nothing new is built for this. Membership of each linked board is read fresh from `UserGroups` on every request, never from session or claims. No SuperAdmin branch.
- **D-09: Linked quests follow the board's own quest rules.** A finalized quest on its chosen date; an open quest on every proposed date, with the same proposed / finalized distinction.
- **D-10: Cancelled events from a linked board are hidden.** On its own board's calendar it stays visible, struck through, exactly as today.
- **D-11: `/calendar` shows linked entries on both layouts.** Desktop renders through the shared `_Calendar` partial; `Calendar/Index.Mobile.cshtml` builds its own agenda list from `GetCalendarDays()` and must be changed separately. A mobile day with only linked entries still gets a day section.
- **D-12: The Quest Details date-vote calendar also shows linked quests and events, read-only.** Desktop (`Quest/Details.cshtml`, three `_Calendar` call sites): linked entries appear on their days in the grid. Mobile (`Quest/Details.Mobile.cshtml` via `_Calendar.Mobile.cshtml`, a list of this quest's own proposed dates): each proposed date that coincides with a linked entry gets an "Also that day: *Board B* - *Title*, *time*" line under its date label. A linked entry never renders a vote control, a vote indicator or any form input; it must render through a path that cannot reach that markup at all, not one that relies on ids never colliding. The board's **own** events stay off the Details page as today.
- **D-13: Unchanged by linking:** the DM series-runway warning stays per-board; the phone calendar feed stays membership-based and `CalendarFeedWriter.cs`, `CalendarSubscriptionService.cs` and `FeedRevisionStamper.cs` are not touched; My Agenda and the Events availability overview are untouched.
- **D-14: Linked entries are muted, dashed, and carry a board-name line.** Lighter background and a dashed left border, keeping the proposed (yellow) / finalized (green) border colour for quests and the event styling for events. The board name sits as a small line under the title, where the DM name sits on an own quest. Rejected: "[Board]" title prefix; colour per board.
- **D-15: The page states which boards it is showing.** Desktop: the existing Legend card on `Calendar/Index.cshtml` gains a "Linked boards" section listing each linked board beside a muted swatch. Mobile: an "Also showing: Board B, Board C" line under the month navigation bar. Nothing renders on an unlinked board or a group of one.
- **D-16: Within a day, own entries come first, then linked.** Own entries keep today's order (events, then quests); linked entries follow.
- **D-17: One narrowed, filter-bypassing read per entity, pinned to the linked board ids.** The linked set is the other boards in the active board's group, read fresh per request (never cached in session). The query uses `IgnoreQueryFilters()` with `linkedGroupIds.Contains(x.GroupId)` in the predicate, following Phase 82 D-14's shape, and lives in a file on the `CrossBoardIgnoreQueryFiltersSeamTests` allowlist. After materialization every row's `GroupId` is re-checked against the linked set and the read fails closed (Phase 82 D-16), with a comment that says plainly both layers read the same set. The read is pure: no write to `Events` or `Quests`.
- **D-18: The integration tests must prove at least:** (1) an unlinked board is unchanged; (2) linked boards see each other and an outside board does not (A and B in one group, C not); (3) removing a board from the group takes effect on the next request; (4) member vs non-member rendering; (5) no roster or vote data leaks; (6) cancelled linked events absent, open linked quests on their proposed dates; (7) linked entries on Quest Details carry no vote input; (8) server-side enforcement on the Platform page (non-SuperAdmin refused; other-type board and already-in-another-group board refused even when posted directly). Reset `ActiveGroupId` to `1` in `DisposeAsync` because `WebApplicationFactoryBase.TestGroupContext` is a shared singleton.

### Claude's Discretion

- **Entity, table, service and repository names**, as long as they do not reuse the `CrossBoardLink*` prefix (Phase 87's deep-link resolver owns it).
- **How the group's board type is held**: stored on the group at creation, or derived from its first board - as long as the server rejects a mismatched join and an empty group cannot accept a board of the wrong type.
- **Whether the linked read goes into existing allowlisted repositories** (`QuestRepository.cs`, `EventRepository.cs`) **or a new dedicated class** added to the allowlist (adding a file to the allowlist has to be argued for in the test's comment).
- **How linked entries reach the view model**: separate collections on `CalendarViewModel` / `CalendarDay`, or a flag on a shared entry type. The "defaults to empty on purpose" convention must carry over.
- **Where the Linked boards page is reached from** (header button beside "Create" and "Users" on the Platform board index matches the Users page; a board-index marker is optional).
- **Whether the group name is ever shown to players** (admin-facing by default).
- **Exact copy**, **page layout and form flow** (inline add/remove vs separate edit page), within `.claude/ui-guidelines.md`.
- Test structure beyond the mandatory cases in D-18.

### Deferred Ideas (OUT OF SCOPE)

- **Move a quest or event to a linked board (super admin, from its Details page)** - a cross-board *write*; its own phase after 91 (Phase 92).
- **Own-board events on the Quest Details date-vote calendar** - would change unlinked boards; a natural small follow-up.
- Out of this phase also: the phone calendar feed, the DM series-runway warning, My Agenda, the Events availability overview.
</user_constraints>

<phase_requirements>
## Phase Requirements

No requirement IDs are mapped to this phase (`Requirements: TBD` in the roadmap). Coverage is derived from CONTEXT.md decisions D-01..D-18 and the roadmap goal; the planner may mint IDs or cite decision IDs in plan frontmatter.

| Decision | Research Support (section) |
|----------|----------------------------|
| D-01, D-02, D-04, D-05 | Schema + set repository/service design, Platform page pattern |
| D-03 | Column-on-`Groups` design makes board deletion automatic (Schema section) |
| D-06, D-08 | Viewer-membership flag + Phase 87 registry/middleware analysis |
| D-07, D-09, D-10, D-17 | Narrow linked reads, window predicates, re-check layer |
| D-11, D-12, D-14, D-15, D-16 | View-model / partial / mobile slotting map |
| D-13 | Guard-test section (feed files untouched) |
| D-18 | Validation Architecture |
</phase_requirements>

## Summary

The phase is mostly plumbing around three existing mechanisms: (1) the fail-closed tenant query filters, crossed only through allowlisted `IgnoreQueryFilters()` call sites; (2) the view model + `_Calendar` partial + separate mobile agenda that render `CalendarViewModel`; (3) Phase 87's registry/middleware, which already makes `/Quest/Details/{id}` and `/Events/Details/{id}` switch a member's board. Nothing in (3) needs to change. The new work is a small schema (a link-group table plus a nullable reference on `Groups`), a Platform CRUD page copying `GroupController`, two narrow filter-bypassing projections placed in the two already-allowlisted repositories, a thin Domain service that composes them with a fresh membership read and an in-memory re-check, and additive, default-empty view-model/markup for linked entries.

Two findings shape the design. First, **put the group reference as a nullable `LinkedBoardSetId` column on `Groups`** (proposed name): "a board belongs to at most one group" becomes true by construction, D-03 (delete a board -> leaves its group) needs no code, and neither new table is tenant-filtered so the set repository needs **no** `IgnoreQueryFilters` and the allowlist test stays at five files. Second, **the linked read must go into `QuestRepository.cs` / `EventRepository.cs`, not `CrossBoardLinkRepository.cs`**: that file's header comment promises "nothing here ever selects entity data" and Phase 87 D-11's one-class preference is about id-to-board lookups, whereas these reads select title/date/DM-name rows. No allowlist edit is needed, which removes the main way to trip the confinement test.

The risks are all "silent green" risks: the InMemory provider cannot prove SQL translation of the new predicates, several existing tests assert the *absence* of `calendar-event`/`calendar-events` markup on Quest Details, and `FeedRevisionWriteSeamTests` bans `.Update(`/`.Entry(`/`ExecuteUpdate` in *all* production code (not only Events/Quests writers), which constrains how the new set repository may write.

**Primary recommendation:** Add `LinkedBoardSetEntity` + `Groups.LinkedBoardSetId` (SetNull), a `LinkedBoardSetRepository`/`Service` (no query-filter bypass), `GetLinkedQuestsForCalendarAsync` / `GetLinkedEventsForCalendarAsync` in the existing allowlisted repositories as flat projections over a month window, a `LinkedCalendarService` that adds the viewer-membership flag and the fail-closed re-check, separate `LinkedQuestOnDay`/`LinkedEventOnDay` collections on `CalendarViewModel`/`CalendarDay` rendered by a new form-free partial, plus a `LinkedBoardsController` in `Areas/Platform` copying `GroupController`.

## Project Constraints (from CLAUDE.md)

- Never commit to `main`; work is on `milestone/v9-rolling-improvements` (current branch). Researcher does not commit (orchestrator does).
- EF packages only in `QuestBoard.Repository`; Service -> Domain -> Repository one-way. Schema changes ship as EF Core migrations, auto-applied on startup. Add with `dotnet ef migrations add <Name> --project ../QuestBoard.Repository` from `QuestBoard.Service/` (Phase 88 ran it as `ASPNETCORE_ENVIRONMENT=Testing dotnet ef migrations add ... --project ../QuestBoard.Repository`; never hand-write Designer/snapshot). [CITED: .claude/architecture.md; .planning/phases/88-calendar-feed-times-anchored-to-the-board-timezone/88-05-PLAN.md:170]
- Writes to `Events`/`Quests` only via the change tracker; this phase writes neither. `IBoardClock` for "today"; `AmbientClockSeamTests` closed list. `CalendarFeedWriter.cs`, `CalendarSubscriptionService.cs`, `FeedRevisionStamper.cs` are high-risk: do not touch.
- No GSD/planning IDs (`D-06`, `Phase 91`, `RESEARCH.md`...) in source comments, XML docs or string literals. Comments explain *why* in plain language.
- Read `.claude/ui-guidelines.md` before any Razor view (done): `modern-card` / `modern-card-header` / `modern-card-body`, `<hr>` before button row, filled buttons with FontAwesome `me-2`, `d-flex justify-content-between` (cancel left, primary right), `.Mobile.cshtml` twins, `Html.LocalTime` / `Html.WallClock` never raw `DateTime.ToString` in views.
- Local DB: do not install/start/provision SQL Server; tests use EF InMemory. If SQL is unreachable, tell the user (it is unreachable from this sandbox right now: `localhost:1433` refused).
- Linux: never set GC env vars; `0x80131506` CLR error is a flake - rerun once. Build failures from locked files = debugger running; ask user.
- Prefer RIP MCP for symbol navigation: **not available in this session** (no `mcp__rip__*` tools); fell back to `grep`/`Read`. Notice per rip-navigation.md: RIP gap - tools not exposed to this agent; fallback = grep + Read.
- No `.claude/skills/` or `.agents/skills/` directory exists. A `senior-frontend` skill is registered in the harness (hand-built Razor UI); view tasks may load it.

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|-------------|----------------|-----------|
| Link-group CRUD + same-type/at-most-one rules | API / Backend (Domain service + Platform controller) | Database (FK, unique name index) | Rules must hold when the form is bypassed (D-01/D-05) |
| Group-of-board storage | Database / Storage | - | Nullable FK on `Groups`; deletion semantics via FK |
| Resolve active board's linked boards | API / Backend (Domain service) | Database | Fresh per request from `Groups` (D-17); `Groups` is unfiltered |
| Cross-board quest/event read | Database / Storage (Repository) | API / Backend (re-check) | Only place `IgnoreQueryFilters` may live; re-check is second layer |
| Viewer membership flag per linked board | API / Backend (Domain via `IGroupService.GetGroupsForUserAsync`) | - | Fresh `UserGroups` read per request (D-08) |
| Placing entries on days, own-first ordering | Frontend Server (view model `CalendarViewModel`) | - | Existing single place stored dates meet the grid |
| Linked chip / agenda row / legend / "Also that day" markup | Frontend Server (Razor partials, desktop + mobile) | Browser (CSS only) | No JS needed; read-only |
| Board switch on member click | Frontend Server (existing `CrossBoardDeepLinkMiddleware`) | - | Unchanged; plain `<a>` is enough |

## Standard Stack

No new libraries. Everything below is already in the solution. [VERIFIED: QuestBoard.Repository/QuestBoard.Repository.csproj:10-16 -> `Microsoft.EntityFrameworkCore` Version="10.0.9", `Microsoft.EntityFrameworkCore.SqlServer` Version="10.0.9", `Microsoft.EntityFrameworkCore.Design` Version="10.0.9"]

### Core
| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| EF Core (+SqlServer, Design) | 10.0.9 | New entity, FK, migration, narrow projections | Already the data layer |
| ASP.NET Core MVC / Razor | .NET 10 (SDK 10.0.112 present) | Platform controller, partials | Already the UI stack |
| xUnit v3 3.2.2 + FluentAssertions 8.10.0 | - | Tests | [VERIFIED: QuestBoard.UnitTests/QuestBoard.UnitTests.csproj:13-21] |
| NSubstitute 5.3.0 | - | Unit-test mocks (UnitTests only) | [VERIFIED: same file] |
| Microsoft.EntityFrameworkCore.InMemory 10.0.9 | - | Integration + repository unit tests | [VERIFIED: IntegrationTests csproj:15] |
| Microsoft.AspNetCore.Mvc.Testing 10.0.9 | - | `WebApplicationFactory` | [VERIFIED: IntegrationTests csproj:14] |

### Alternatives Considered
| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| Nullable `LinkedBoardSetId` on `Groups` | Join table `(GroupId unique, SetId)` | Join table needs a unique index on GroupId to enforce "at most one", plus cascade config; the column gets both free and D-03 is automatic. Column recommended. |
| Reads in existing `QuestRepository`/`EventRepository` | New allowlisted class | New file forces an allowlist edit + a why-comment in the seam test and a "five -> six" message change; no benefit since the reads need `DbContext.Quests/Events` anyway. |
| Separate linked collections on view model | One `QuestOnDay` with an `IsLinked` flag | A flag keeps linked entries inside the loop that owns vote markup (`isDetailsPage && questOnDay.Quest.Id == currentQuestId`), violating D-12's "cannot reach that markup". Separate types recommended. |

**Installation:** none (`dotnet restore` only). Local tool: `.config/dotnet-tools.json` pins `dotnet-ef` 9.0.6 while EF runtime is 10.0.9; run `dotnet tool restore` first and expect a tools-older-than-runtime warning (Phase 88 used it successfully). [VERIFIED: .config/dotnet-tools.json; `dotnet ef --version` -> "Run dotnet tool restore"]

## Package Legitimacy Audit

This phase installs no external packages, so no `package-legitimacy` check was run. **Packages removed (SLOP): none. Flagged (SUS): none.**

## Architecture Patterns

### System Architecture Diagram

```
Super admin (Platform, SuperAdminOnly)
   |  POST create/rename/add/remove/delete  (antiforgery)
   v
LinkedBoardsController --> LinkedBoardSetService --> LinkedBoardSetRepository --> [LinkedBoardSets] <-FK SetNull- [Groups.LinkedBoardSetId]
                              (type match + at-most-one re-checked here, not only in the picker)

Viewer GET /calendar  or  GET /Quest/Details/{id}      (active board A from session)
   |
   v
CalendarController / QuestController
   |-- own reads (unchanged): GetQuestsForCalendarAsync / GetEventsForCalendarAsync   [ambient query filter = board A]
   |-- LinkedCalendarService.GetAsync(activeBoardId, viewerUserId, window)
   |        1. Groups: A.LinkedBoardSetId -> other boards in same set (unfiltered table) ; none/<2 => return empty, NO bypass read
   |        2. IGroupService.GetGroupsForUserAsync(viewer) -> member flag per linked board (fresh UserGroups)
   |        3. QuestRepository.GetLinkedQuestsForCalendarAsync(linkedIds, window)   [IgnoreQueryFilters + Contains(GroupId)]  (skipped if set type = Campaign)
   |        4. EventRepository.GetLinkedEventsForCalendarAsync(linkedIds, window)   [IgnoreQueryFilters + Contains(GroupId) + CancelledAt == null]
   |        5. in-memory re-check GroupId in linkedIds, drop + LogError if not (same set both layers)
   v
CalendarViewModel { Quests, Events (own)  +  LinkedQuests, LinkedEvents, LinkedBoards (default empty) }
   |  GetCalendarDays(): own QuestsOnDay/EventsOnDay (unchanged) + LinkedEventsOnDay/LinkedQuestsOnDay
   v
Desktop: Calendar/Index.cshtml -> _Calendar.cshtml (day cell: own events, own quests, THEN _LinkedDayEntries partial) + Legend "Linked boards"
Mobile : Calendar/Index.Mobile.cshtml agenda (own events, own quests, THEN linked rows; day shown if only linked) + "Also showing:" line
Details: _Calendar.cshtml grid (3 call sites) + _Calendar.Mobile.cshtml "Also that day" line under each own proposed date
   |
   v
Entry for linked board B:   viewer member of B ? <a href=/Quest/Details/{id}> : plain <div> (no href, no onclick)
   member click --> CrossBoardDeepLinkMiddleware (unchanged) switches session to B + banner
```

### Recommended Project Structure (proposed new names; none start with `CrossBoardLink`)

```
QuestBoard.Repository/
├── Entities/LinkedBoardSetEntity.cs            # new; table "LinkedBoardSets"
├── Entities/GroupEntity.cs                     # + int? LinkedBoardSetId, nav
├── Entities/QuestBoardContext.cs               # + DbSet, unique Name index, FK SetNull
├── LinkedBoardSetRepository.cs                 # no IgnoreQueryFilters needed
├── QuestRepository.cs                          # + GetLinkedQuestsForCalendarAsync (already allowlisted)
├── EventRepository.cs                          # + GetLinkedEventsForCalendarAsync (already allowlisted)
├── Automapper/EntityProfile.cs                 # + explicit Ignore of the new Group members
└── Migrations/2026101xxxxxxx_AddLinkedBoardSets(.Designer).cs + snapshot (generated)
QuestBoard.Domain/
├── Models/LinkedBoardSet.cs, LinkedBoard.cs, LinkedQuestEntry.cs, LinkedEventEntry.cs, LinkedCalendarData.cs
├── Interfaces/ILinkedBoardSetRepository.cs, ILinkedBoardSetService.cs, ILinkedCalendarService.cs
│              + two methods on IQuestRepository / IEventRepository
└── Services/LinkedBoardSetService.cs, LinkedCalendarService.cs
QuestBoard.Service/
├── Areas/Platform/Controllers/LinkedBoardsController.cs
├── Areas/Platform/Views/LinkedBoards/Index.cshtml + Index.Mobile.cshtml
├── ViewModels/PlatformViewModels/LinkedBoards*ViewModel.cs
├── ViewModels/CalendarViewModels/LinkedQuestOnDay.cs, LinkedEventOnDay.cs (+ CalendarViewModel/CalendarDay edits)
├── Views/Shared/_LinkedDayEntries.cshtml (+ .Mobile twin only if the mobile Details line is built as a partial)
└── wwwroot/css/calendar.css, calendar.mobile.css, platform-group.mobile.css (or a new platform-linked-boards.mobile.css)
```

### Pattern 1: Schema - column on `Groups`, set carries the type

**What:** a `LinkedBoardSets` table (`Id`, `Name` nvarchar(100) unique, `BoardType` int, `CreatedAt`), and `Groups.LinkedBoardSetId int NULL` FK with `OnDelete(SetNull)`. The set's `BoardType` is stored at creation (Claude's-discretion choice made here): an empty set cannot accept a wrong-type board, no "first board decides" edge case, and the picker can filter by `set.BoardType`.

Existing conventions to copy (verbatim, opened this session):

- `GroupEntity` today [VERIFIED: QuestBoard.Repository/Entities/GroupEntity.cs:6-24]:
  ```csharp
  [Table("Groups")]
  public class GroupEntity : IEntity
  ...
      public int BoardType { get; set; }

      public virtual ICollection<UserGroupEntity> UserGroups { get; set; } = [];
  ```
  `Group` domain model has `Id`, `Name`, `CreatedAt`, `BoardType` only [VERIFIED: QuestBoard.Domain/Models/Group.cs:5-11]. Keep the new members off `Group`.
- `GroupEntity` has **no** `HasQueryFilter` (only `HasIndex(g => g.Name).IsUnique()`) [VERIFIED: QuestBoardContext.cs:295-298]; `GroupRepository` states the same: "GroupEntity carries no query filter, so this read is correct regardless of which board the caller has selected" [VERIFIED: GroupRepository.cs:71-73 comment]. `UserGroups` carries no filter either [VERIFIED: CrossBoardLinkRepository.cs:51 comment "Membership rows carry no query filter today"].
- Every other `Group` FK is `NoAction`, except `UserGroup -> Group` which is `Cascade` [VERIFIED: QuestBoardContext.cs:305-310, 441-446]. The new FK points *from* `Groups` *to* `LinkedBoardSets`, so it cannot create a cascade cycle; `SetNull` is the right behaviour for "delete the group" (D-04 explicit group delete un-links its boards).
- Enum `BoardType` [VERIFIED: QuestBoard.Domain/Enums/BoardType.cs:3-7]:
  ```csharp
  public enum BoardType
  {
      OneShot = 0,
      Campaign = 1
  }
  ```
- Recent-migration conventions [VERIFIED: Migrations/20260703113120_AddBoardTypeToGroup.cs; 20260918080027_AddCalendarSubscriptions.cs; 20260930161610_AddFeedEntryRevisions.cs]: UTF-8 BOM, `using Microsoft.EntityFrameworkCore.Migrations;` (+`using System;` when `DateTime` columns appear), `#nullable disable`, `namespace QuestBoard.Repository.Migrations { /// <inheritdoc /> public partial class X : Migration`, `CreateTable` with `.Annotation("SqlServer:Identity", "1, 1")`, `type: "datetime2"`, `type: "nvarchar(60)", maxLength: 60`, named `PK_`/`FK_` constraints, `CreateIndex`, and a symmetrical `Down`. Newest existing id is `20260930161610_AddFeedEntryRevisions`; the new one must sort after it (e.g. `2026101…_AddLinkedBoardSets`). Snapshot file is `QuestBoardContextModelSnapshot.cs` (generated). The snapshot's `GroupEntity` block currently ends with `b.HasKey("Id"); b.HasIndex("Name").IsUnique(); b.ToTable("Groups");` [VERIFIED: QuestBoardContextModelSnapshot.cs:632-657].
- Migration guard tests that will run against the new migration [VERIFIED: QuestBoard.UnitTests/Migrator/MigrationPreflightTests.cs:30-52, QuestBoard.Migrator/MigrationRunner.cs:124-126]: `ShippedModel_HasNoPendingModelChanges` (`context.Database.HasPendingModelChanges()` must be false, so the generated migration/snapshot must match the entity config exactly) and `ShippedMigrations_NoneNeedToRunOutsideATransaction` (forbidden text regex `ALTER\s+DATABASE|FULLTEXT|MEMORY_OPTIMIZED`, and no suppressed transaction). Pure `CreateTable`/`AddColumn`/`AddForeignKey` passes.

Proposed entity (new code, new names):
```csharp
[Table("LinkedBoardSets")]
public class LinkedBoardSetEntity : IEntity
{
    [Key][DatabaseGenerated(DatabaseGeneratedOption.Identity)] public int Id { get; set; }
    [Required][StringLength(100)] public string Name { get; set; } = string.Empty;
    public int BoardType { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public virtual ICollection<GroupEntity> Boards { get; set; } = [];
}
// GroupEntity: public int? LinkedBoardSetId { get; set; }  + [ForeignKey] public virtual LinkedBoardSetEntity? LinkedBoardSet { get; set; }
// Context: HasIndex(s => s.Name).IsUnique();  HasOne(g => g.LinkedBoardSet).WithMany(s => s.Boards).HasForeignKey(g => g.LinkedBoardSetId).OnDelete(DeleteBehavior.SetNull);
```
AutoMapper: `CreateMap<Group, GroupEntity>` currently maps only `BoardType` explicitly [VERIFIED: EntityProfile.cs:210-215]; `BaseRepository.UpdateAsync` does `Mapper.Map(model, entity)` onto a tracked entity [VERIFIED: BaseRepository.cs:63-69], so a Platform rename goes through it. Unmapped destination members are left untouched today (`UserGroups` is the precedent), but add explicit `.ForMember(d => d.LinkedBoardSetId, o => o.Ignore()).ForMember(d => d.LinkedBoardSet, o => o.Ignore())` so a rename can never null a board's link. [ASSUMED: AutoMapper default leaves unmatched destination members untouched - consistent with how `UserGroups` survives rename today, not separately tested]

**InMemory caveats that affect tests:** InMemory does not enforce unique indexes (the existing controller relies on a DB exception for duplicate group names, untestable in InMemory) and applies `SetNull` only to tracked dependents. So: (a) do a service-level `AnyAsync(name)` pre-check *and* keep the unique index; (b) in `DeleteSetAsync`, load the set with `Include(s => s.Boards)` (tracked) then `Remove`, so both InMemory and SQL Server un-link the boards. [ASSUMED: InMemory SetNull-only-for-tracked behaviour is standard EF Core behaviour; verify in the first repository test]

### Pattern 2: The allowlist and how a new cross-board read is admitted

Current closed list [VERIFIED: QuestBoard.UnitTests/Architecture/CrossBoardIgnoreQueryFiltersSeamTests.cs:16-23]:
```csharp
    private static readonly string[] AllowedCallSites =
    [
        "QuestBoard.Repository/EventSignupRepository.cs",
        "QuestBoard.Repository/EventRepository.cs",
        "QuestBoard.Repository/GroupRepository.cs",
        "QuestBoard.Repository/QuestRepository.cs",
        "QuestBoard.Repository/CrossBoardLinkRepository.cs",
    ];
```
Scan scope [VERIFIED: same file:29-34, 36]: roots `QuestBoard.Repository`, `QuestBoard.Domain`, `QuestBoard.Service`, `*.cs` only, `obj`/`bin` skipped, banned token `private const string BannedCallShape = "IgnoreQueryFilters";`, comments (`//`, `/* */`, `@* *@`) stripped first. The assertion is set **equality** (`BeEquivalentTo(AllowedCallSites)`), so a new file containing the token anywhere outside a comment fails, and a listed file without it also fails (`AllowlistedFile_ExistsAndActuallyContainsTheCall`).

**Admission recipe:** put both new reads in `QuestRepository.cs` and `EventRepository.cs` (both already listed and already contain real calls: `EventRepository.cs:172`, `QuestRepository.cs:270, 293`) -> the seam test needs **no edit** and stays green. Hazards to brief the planner on: (1) no *other* new production `.cs` file may contain the substring `IgnoreQueryFilters` outside a comment - including a string literal, an identifier, or a log/exception message (e.g. a Domain service message "...IgnoreQueryFilters..." fails); `///` doc comments are fine; (2) the new set repository/service need no bypass because `Groups` and `LinkedBoardSets` are unfiltered, so do not add one "to be safe"; (3) if the planner nevertheless chooses a new class, the test's `AllowedCallSites`, its "five" wording in the comment/because-message, and a why-comment all have to change in the same plan.

The Phase 82 shape to copy [VERIFIED: QuestBoard.Repository/EventRepository.cs:160-185]:
```csharp
var entities = await DbContext.Events
    .IgnoreQueryFilters()
    .Where(e => memberGroupIds.Contains(e.GroupId) && e.Date >= today && e.CancelledAt == null)
    ...
    .AsNoTracking()
    .ToListAsync(token);
```
with the comment explaining that stepping outside the filter disables it for the whole query including navigations, which is safe because every row hangs off a pinned `GroupId`. Quest's Phase 82-era bypass `GetFeedQuestsForUserAsync` (QuestRepository.cs:277-300) pins `oneShotGroupIds.Contains(q.GroupId)` similarly. **Do not** copy `GetQuestsForTomorrowAllGroupsAsync` (bare bypass, no group predicate; QuestRepository.cs:267-274) - Phase 82 CONTEXT calls it "explicitly not a precedent".

Proposed linked reads (flat projection - never `Include`, never `ProjectForCalendar`):
```csharp
// QuestRepository.cs (new names)
public async Task<IList<LinkedQuestEntry>> GetLinkedQuestsForCalendarAsync(
    IReadOnlyCollection<int> linkedGroupIds, DateTime fromInclusive, DateTime toExclusive, CancellationToken token = default)
{
    return await DbContext.Quests
        .IgnoreQueryFilters()
        .Where(q => linkedGroupIds.Contains(q.GroupId)
            && ((q.IsFinalized && q.FinalizedDate >= fromInclusive && q.FinalizedDate < toExclusive)
                || (!q.IsFinalized && q.ProposedDates.Any(pd => pd.Date >= fromInclusive && pd.Date < toExclusive))))
        .AsNoTracking()
        .Select(q => new LinkedQuestEntry
        {
            QuestId = q.Id, GroupId = q.GroupId, Title = q.Title,
            DungeonMasterName = q.DungeonMaster.Name,
            IsFinalized = q.IsFinalized, FinalizedDate = q.FinalizedDate,
            ProposedDates = q.ProposedDates.Where(pd => pd.Date >= fromInclusive && pd.Date < toExclusive).Select(pd => pd.Date).ToList()
        })
        .ToListAsync(token);
}
// EventRepository.cs
//   .IgnoreQueryFilters().Where(e => linkedGroupIds.Contains(e.GroupId) && e.CancelledAt == null && e.Date >= from && e.Date < to)
//   .OrderBy(e => e.Date).ThenBy(e => e.StartTime).ThenBy(e => e.Id).AsNoTracking().Select(e => new LinkedEventEntry { EventId = e.Id, GroupId = e.GroupId, Title = e.Title, Date = e.Date, StartTime = e.StartTime })
```
Entity members used here exist [VERIFIED: QuestEntity.cs:11-72 `Title`, `DungeonMasterId`, `FinalizedDate`, `IsFinalized`, `GroupId`, `DungeonMaster`, `ProposedDates`; ProposedDateEntity.cs `Date`; EventEntity.cs `Date` (DateOnly), `StartTime` (TimeOnly?), `CancelledAt`, `GroupId`]. The window is a performance guard (own reads fetch every row and filter in the view model - EventRepository.cs:13-25 comment); it is optional but recommended, and is the part InMemory cannot prove (see Pitfall 1). If the planner prefers strict parity with the own reads (no window), drop the date predicates and keep the rest unchanged.

The Domain-layer re-check (copy the Phase 82 shape [VERIFIED: QuestBoard.Domain/Services/EventService.cs:92-114]): after materializing, `rows.Where(r => linkedGroupIds.Contains(r.GroupId))`, `LogError` if the count differs, with a comment stating both layers read the same `linkedGroupIds` so it catches a dropped predicate or bad `Contains` translation, not a wrong set. `EventService` is internal and logs via `ILogger<EventService>` - mirror with `ILogger<LinkedCalendarService>`.

### Pattern 3: Platform "Linked boards" page - copy `GroupController`

[VERIFIED: QuestBoard.Service/Areas/Platform/Controllers/GroupController.cs:15-18]
```csharp
[Area("Platform")]
[Authorize(Policy = "SuperAdminOnly")]
public class GroupController(
```
`SuperAdminOnly` = `policy.RequireRole("SuperAdmin")` [VERIFIED: Program.cs:90-91]. Route: `platform/{controller=Group}/{action=Index}/{id?}` [VERIFIED: Program.cs:367-369], so the new controller is `/platform/LinkedBoards/...`; `/platform` is a `GroupSessionMiddleware` exempt prefix [VERIFIED: GroupSessionMiddleware.cs:64], so no active board is needed.

Copy: constructor-injected services, `TempData["Success"|"Error"|"Warning"]` + `RedirectToAction`, every POST `[HttpPost][ValidateAntiForgeryToken]`, `ModelState` checks, "not found -> redirect to Index" (GroupController.cs:43-50, 54-62). Platform views: `_ViewImports` pulls `QuestBoard.Domain.Enums`, `Models`, `Extensions` (so `Html.LocalTime`), `ViewModels.PlatformViewModels`; layout `_Layout.Platform` [VERIFIED: Areas/Platform/Views/_ViewImports.cshtml, _ViewStart.cshtml]. Index card pattern [VERIFIED: Areas/Platform/Views/Group/Index.cshtml:5-23]: `card modern-card` / `card-header modern-card-header d-flex justify-content-between align-items-center` / `h2.mb-0` with FontAwesome icon / `d-flex gap-2` button group, table with `table-responsive`, `badge bg-info text-dark` Campaign vs `badge bg-primary` One-Shot. Mobile twin pattern [VERIFIED: Group/Index.Mobile.cshtml]: `@section Styles { <link href="~/css/platform-group.mobile.css" ...> }`, `platform-group-card-mobile`, per-row `group-card-mobile`, `btn-sm` buttons; inline destructive POSTs use `onsubmit="return confirm('...')"` (Group/Members.Mobile.cshtml) - reuse for "remove board" / "delete group". `platform-group.mobile.css` states "No @media queries" and is "exclusively loaded by Areas/Platform/Views/Group/*.Mobile.cshtml" - a new view folder can link the same file (classes are generic) or a new `platform-linked-boards.mobile.css`.

Entry point: add a "Linked Boards" button beside Create/Manage Users in `Group/Index.cshtml` and `Group/Index.Mobile.cshtml` (button groups at Index.cshtml:12-19 and Index.Mobile.cshtml:12-19). Optional: show the set name in the board table by adding the name to the `GroupWithMemberCount` projection.

Server-side rules (D-01/D-05), in `LinkedBoardSetService`/repository, returning a result enum the controller maps to TempData (do not rely on the picker):
- `AddBoard(setId, groupId)`: set exists, board exists, `board.BoardType == set.BoardType` else `TypeMismatch`, `board.LinkedBoardSetId == null` else `AlreadyLinked` (covers "in another set" and "already in this set"). Mutate the tracked entities and `SaveChangesAsync` - **never** `.Update(`/`.Attach(`/`.Entry(`/`ExecuteUpdate`: `FeedRevisionWriteSeamTests` bans those in every production `.cs` outside `Migrations/`, with `AllowedWriteSites = []` [VERIFIED: FeedRevisionWriteSeamTests.cs:19-23, 43-56].
- `Create(name, boardType)`: name required <=100, `Enum.IsDefined`, unique (pre-check + index). View model pattern: `[Required]`, `[StringLength(100)]`, `[EnumDataType(typeof(BoardType))]` as in `GroupCreateViewModel` [VERIFIED: ViewModels/PlatformViewModels/GroupCreateViewModel.cs:5-17]; board type is never bound from a later form (`[BindNever]` precedent: GroupEditViewModel.cs).
- Active/inactive (D-04): `Boards.Count >= 2`.
- A race between two concurrent super-admin adds is accepted (last write wins on a single column); note, do not engineer around it.

### Pattern 4: Calendar data -> view model -> markup (exact slots)

Facts [all VERIFIED this session]:
- `CalendarController.Index`: reads board type via `boardTypeResolver.GetBoardTypeAsync`, `includeQuests = activeBoardType != BoardType.Campaign` (CalendarController.cs:48), `allQuests = includeQuests ? await questService.GetQuestsForCalendarAsync(token) : []` (51-53), `allEvents = await eventService.GetEventsForCalendarAsync(token)` (54), builds `CalendarViewModel { Year, Month, Quests, Events, CanManage }` (57-64), `SeriesBelowRunway` only when `CanManage` (68-71). Controller is `[Authorize]`, is on `AmbientClockSeamTests` guarded + board-clock lists: **no `DateTime.Now/Today/UtcNow`**; compute the month window from `selectedYear`/`selectedMonth` (already validated 1..12 and 1900..2100).
- `QuestController.Details` (QuestController.cs:310-385): `allQuests = await questService.GetQuestsForCalendarAsync(token)` (351), one `CalendarViewModel { Year, Month, Quests = allQuests.ToList() }` per month with a proposed date for this quest (353-363), stored in `ViewBag.CalendarMonths` (366), `ViewBag.CurrentQuestId = id` (368), `ViewBag.IsDetailsPage = true`. Also on the guarded lists. Add one linked read for the window [first day of earliest month, first day after latest month) and distribute onto each month model. Skip the linked read when there are no months. `Details` has no `[Authorize]` attribute; an anonymous or board-less request has `ActiveGroupId == null` -> the linked service must return empty without throwing (do **not** use `RequireActiveGroupId()`, which throws - ActiveGroupContextExtensions.cs:14-17).
- `CalendarViewModel` [VERIFIED: ViewModels/CalendarViewModels/CalendarViewModel.cs]: `Quests`, `Events` default `new()` with the "defaults to empty on purpose" comment (10-17), `SeriesBelowRunway`, `CanManage`; `GetCalendarDays()` builds each `CalendarDay` with `QuestsOnDay = GetQuestsForDate(date)`, `EventsOnDay = GetEventsForDate(date)` (51-60). `GetQuestsForDate` finalized rule requires a proposed date whose `.Date` equals `FinalizedDate.Date` ("chosenProposedDate != null", lines 80-96) and open quests emit one entry per proposed date (99-112); `GetEventsForDate` is the single DateOnly<->DateTime conversion point ("No other file may convert between them", 125-129). `CalendarDay` (CalendarDay.cs) has `QuestsOnDay`, `EventsOnDay` default `[]`. `EventOnDay.TimeLabel` returns `HH:mm` or `"All day"` (EventOnDay.cs) - "Both the desktop chip and the mobile agenda entry read this one property".
- `_Calendar.cshtml`: day cell body is `<div class="day-cell-items">` (line 35) containing `calendar-events` block (42) then `quest-events` block (58); each quest chip is `div.quest-event ... @(isDetailsPage && questOnDay.Quest.Id == currentQuestId ? "current-quest" : "")` (61) with `data-quest-id`, `data-proposed-date-id`; vote markup lives inside `@if (isDetailsPage && questOnDay.Quest.Id == currentQuestId)` (102). **Slot for linked entries: after the `quest-events` block, still inside `day-cell-items`** (D-16 order). Do not add them inside either existing loop.
- `Calendar/Index.Mobile.cshtml`: line 9 `var agendaDays = Model.GetCalendarDays().Where(d => !d.IsEmpty && (d.QuestsOnDay.Any() || d.EventsOnDay.Any())).ToList();` -> **must also include the linked collections or a linked-only day vanishes (D-11)**. Events loop at 94, quests loop at 105; rows are clickable via `onclick="window.location.href=..."` plus an inner `<a class="row-nav-link">`. Slot: after the quests loop inside `agenda-day-section`. Non-member linked rows must have **neither** `onclick` nor an anchor; member rows should copy the anchor+onclick pair (the accessibility test requires a focusable link on mobile rows: RowNavigationAccessibilityTests.cs header comment). "Also showing:" line goes right after the month-navigation `div.calendar-month-nav` (lines 20-33).
- `Calendar/Index.cshtml` Legend: `<!-- Legend -->` at line 70, `card modern-card legend-card`; each legend row is `<div class="d-flex align-items-center mb-2"><div class="legend-item proposed me-2"></div><small>Proposed</small></div>` (existing pattern; swatch classes `proposed|finalized|event|cancelled` defined in calendar.css ~450-473). Add a conditional "Linked boards" block with a `legend-item linked` swatch (new CSS rule) per board name.
- `_Calendar.Mobile.cshtml` (used only by Details mobile - `Html.PartialAsync("_Calendar", ...)` resolves to the `.Mobile` twin under a mobile UA via `MobileViewLocationExpander`, which yields the `.Mobile.cshtml` location first then the plain one [VERIFIED: ViewExpanders/MobileViewLocationExpander.cs:28-35]): loops `Model.GetCalendarDays()` -> `day.QuestsOnDay`, and only for `isDetailsPage && questOnDay.Quest.Id == currentQuestId` renders `div.calendar-date-entry-mobile` with `div.calendar-date-label-mobile` then vote rows. **Slot for "Also that day": inside that `div.calendar-date-entry-mobile`, directly after the date-label `div`** and before the vote rows, reading `day.LinkedQuestsOnDay`/`day.LinkedEventsOnDay`. These calendars live inside the vote `<form>` (Details.Mobile.cshtml:155-159, 194-197) - the line must be text/anchor only.
- Desktop Details: three `@await Html.PartialAsync("_Calendar", calendarMonth)` call sites at Details.cshtml:605, 649, 697 (the third sets `ViewData["UpdateVoteIndexLookup"]` first). All three get linked entries for free once `_Calendar.cshtml` renders them; each call site's `calendarMonth` models are created only in `QuestController.Details`.
- Event/quest class names: **linked markup must not contain the substrings `calendar-event` or `calendar-events`**, because `EventCalendarPartialTests` asserts `content.Should().NotContain(EventBlockClass)` (= "calendar-events") and `NotContain(EventChipClass)` (= "calendar-event") on Quest Details [VERIFIED: Controllers/EventCalendarPartialTests.cs:15-16, 74-76]. Those facts are for an unlinked board so they stay green anyway, but a `linked-*` prefix keeps them meaningful. Do not reuse `.quest-event` either: its CSS adds `padding-right: 30px`, hover min-height growth, `cursor: pointer` and hover-only display of DM/time (calendar.css:116-135, 202-220) and it is the class the vote CSS keys on (calendar.css:379-424, 532-620). Style new `linked-*` classes explicitly.

Proposed Domain/View models (new names; flat, no vote/signup members at all):
```csharp
// Domain
public sealed class LinkedBoard { public int Id; public string Name; public bool ViewerIsMember; }
public sealed class LinkedQuestEntry { QuestId, GroupId, Title, DungeonMasterName, IsFinalized, FinalizedDate, IReadOnlyList<DateTime> ProposedDates, BoardName, ViewerIsMember }
public sealed class LinkedEventEntry { EventId, GroupId, Title, Date (DateOnly), StartTime (TimeOnly?), BoardName, ViewerIsMember }
// Service view models: LinkedQuestOnDay { Entry, DateTime When, bool IsFinalized }, LinkedEventOnDay { Entry, TimeLabel => "HH:mm" | "All day" }
// CalendarViewModel: LinkedQuests, LinkedEvents, LinkedBoards  (all default empty, with the same "on purpose" comment)
// CalendarDay: LinkedQuestsOnDay, LinkedEventsOnDay (default [])
```
Time text: compute in the view-model class (like `EventOnDay.TimeLabel`), not `DateTime.ToString` in a view (ui-guidelines). Rendered `HH:mm` of a wall-clock value is not a timezone conversion. The linked quest day rule must mirror `GetQuestsForDate` exactly, including "finalized needs a proposed date on the finalized day" (D-09: "follow the board's own quest rules"); pin parity with a unit test over the same matrix as the own rule.

D-06/D-08 markup: member -> `<a href="@Url.Action("Details","Quest",new{id})">` (and `"Details","Events"` for events) - the plain ordinary URL; non-member -> a non-interactive element (`div`/`span`), `cursor: default`, no `href`, no `onclick`, no `data-quest-id`, no `data-proposed-date-id`. Board name line: `<div class="linked-entry-board">@Entry.BoardName</div>`; DM name where own chips put it.

### Pattern 5: Phase 87 deep-link behaviour (nothing to build)

[VERIFIED: QuestBoard.Service/Helpers/CrossBoardLinkRegistry.cs:35, 44]
```csharp
        [DefaultRoute<QuestController>(nameof(QuestController.Details))] = CrossBoardLookupKind.Quest,
        [DefaultRoute<EventsController>(nameof(EventsController.Details))] = CrossBoardLookupKind.Event,
```
`CrossBoardDeepLinkMiddleware` order [VERIFIED: Middleware/CrossBoardDeepLinkMiddleware.cs:41-111]: unauthenticated pass-through; non-GET/HEAD pass-through; route not in registry pass-through; `IsRealTopLevelNavigation` (needs `Sec-Fetch-Dest: document` and `Sec-Fetch-Mode: navigate`, rejects prefetch hints) else pass-through; no active board pass-through; `resolver.ResolveAsync(kind, id, userId)`; null -> same path as a nonexistent id; target already active -> pass-through; else capture previous board, `switcher.SwitchAsync`, set `TempData[TempDataKeys.BoardSwitchTargetName]`/`BoardSwitchPreviousGroupId`/`BoardSwitchPreviousGroupName`, continue in the same request. The resolver reads membership fresh via `groupService.GetGroupsForUserAsync(userId)` and pins `memberGroupIds` inside `CrossBoardLinkRepository`'s id lookup (no entity data) [VERIFIED: Domain/Services/CrossBoardLinkResolverService.cs:16-45; CrossBoardLinkRepository.cs:78-88]. It has no SuperAdmin branch by signature. Consequences for this phase:

1. A member of B clicking a linked entry in a real browser navigation is switched to B and lands on the Details page with the banner - an ordinary `<a>` is all that is needed. Prefetch/`fetch` requests do not switch; that is intended.
2. A non-member gets a 404 from the action (query filter) - exactly why D-06 renders plain text. The viewer-membership flag therefore must come from the same source the resolver uses (`IGroupService.GetGroupsForUserAsync(userId)`), read fresh per request, so "link rendered" and "link works" cannot disagree.
3. The registry test `RegisteredRoutes_HasExactly18Entries` [VERIFIED: QuestBoard.UnitTests/Helpers/CrossBoardLinkRegistryTests.cs:50-52] stays green because nothing is added to the registry. The new Platform route is area-keyed and absent from it, correctly.

### Anti-Patterns to Avoid
- **Reusing `ProjectForCalendar`** (QuestRepository.cs:352-363 eagerly includes `PlayerSignups -> Player`, `ProposedDates -> PlayerVotes -> PlayerSignup -> Player`, `DungeonMaster`) for linked quests: loads roster/votes of another board (D-07).
- **Mapping linked rows through AutoMapper to `Quest`/`Event`**: puts a `Quest` with `PlayerSignups`/`ProposedDates.PlayerVotes` shape into the view model, one `Quest.Id == CurrentQuestId` check from vote markup.
- **Iterating boards with `SetGroupId` inside the request** (Phase 82 D-14 rejected it: `ActiveGroupContextService` is scoped and mutating it rewrites the ambient board for the rest of the request) [CITED: .planning/phases/82-personal-cross-board-event-agenda/82-CONTEXT.md D-14].
- **Caching the linked set in session** (D-17 forbids; stale after a super-admin edit).
- **Calling the bypass read when there is nothing linked**: unlinked boards must keep zero extra cross-board reads. (Phase 82 deliberately called its query unconditionally for the empty case to catch predicate regressions, but there the empty set is a *no-rights* viewer; here an empty linked set is the normal unlinked case, so short-circuit and prove it with `DidNotReceive`.)
- **Hiding a linked-only day on mobile** by leaving the `agendaDays` filter at its current form.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Switch the viewer to board B on click | A new link/redirect/`SelectGroup` call | Plain `<a href="/Quest/Details/{id}">` + existing `CrossBoardDeepLinkMiddleware` | Handles guards, banner, prefetch safety, membership pin |
| Viewer membership | Session/claims flag or a new membership query | `IGroupService.GetGroupsForUserAsync(userId)` (fresh per request) | Same source the resolver uses; AgendaController precedent |
| Day placement rules | A second finalized/proposed implementation with different edge cases | Mirror `GetQuestsForDate` and pin parity in a unit test | D-09 "same rules"; drift = clash missed |
| Wall-clock text | `TimeZoneInfo` conversion or `DateTime.Now` | Server-side `TimeLabel` string like `EventOnDay` | Floating local times must never convert |
| Antiforgery on new POSTs | Manual token handling | `[ValidateAntiForgeryToken]` per action | `AntiForgeryTokenCoverageTests` reflection sweep fails otherwise |
| Migration SQL | Hand-written Designer/snapshot | `dotnet ef migrations add` | `HasPendingModelChanges` guard |

**Key insight:** the novelty is the *composition*, not any new mechanism; every piece (bypass shape, re-check layer, fresh membership, deep link switch, Platform CRUD) has a close precedent to copy, and the main danger is accidentally changing behaviour for unlinked boards.

## Runtime State Inventory

Not a rename/refactor/migration phase (greenfield schema addition). Omitted deliberately. One data note: existing `Groups` rows get `LinkedBoardSetId = NULL`, which is exactly "unlinked", so no data migration or backfill is needed.

## Common Pitfalls

### Pitfall 1: InMemory green, SQL Server red
**What goes wrong:** The window predicate (`q.ProposedDates.Any(...)` inside a `||` with a finalized branch) or nested `.Where(...).Select(...).ToList()` projection fails to translate on SQL Server while every InMemory test passes. Phase 82's own isolation test header lists the same blind spot (empty-collection `Contains`, filter bypass interacting with navigation-scoped filters generates no join in memory) [CITED: QuestBoard.IntegrationTests/Tests/AgendaTenantIsolationTests.cs:12-27 comment].
**Why:** the shared harness is InMemory (LINQ-to-Objects). Also `linkedGroupIds.Contains` with an empty list is only List.Contains in memory.
**How to avoid:** keep predicates simple (no `?:` in `Where`, `Contains` over a materialized list), short-circuit the empty set before the query, and add a **manual local-SQL-Server verification task** (as Phase 88 did: `88-VALIDATION.md` line 76 records a read-only local SQL check) - SQL Server is not reachable from this sandbox, and per project rules the agent must not provision it, so this is a user-run checkpoint.
**Warning signs:** `InvalidOperationException ... could not be translated` only in production/local-SQL runs.

### Pitfall 2: The navigation-scoped filters under `IgnoreQueryFilters`
**What goes wrong:** `ProposedDateEntity`'s filter is `pd.Quest.GroupId == ActiveGroupId` [VERIFIED: QuestBoardContext.cs:467-475]. Without the bypass applying to the *whole* query, a projection through `q.ProposedDates` of a linked quest returns zero dates (silent: quest present, no dates).
**Why:** each entity has its own filter; `IgnoreQueryFilters()` is per query, and applies to navigations inside it.
**How to avoid:** a test with a real linked quest that has proposed dates must assert the entry lands on its date (D-18 #2/#6), not only that the title exists.

### Pitfall 3: `IgnoreQueryFilters` substring anywhere in a new file
**What goes wrong:** `IgnoreQueryFilters_IsConfinedToTheAllowlist` fails the build because a new Domain/Service/Repository `.cs` mentions the token in code or a string (comments are stripped; strings are not).
**How to avoid:** keep the token to the two existing repository files; say "ambient board filter bypass" in prose elsewhere.

### Pitfall 4: Seam-test side effects on guarded files
**What goes wrong:** `CalendarController.cs`, `QuestController.cs`, `QuestRepository.cs`, `GroupRepository.cs`, `Quest/Details.cshtml`, `Quest/Details.Mobile.cshtml` are on `AmbientClockSeamTests`' guarded list (no `DateTime.Today/Now/UtcNow`; the quest views must keep the `ViewBag.BoardToday` token) [VERIFIED: AmbientClockSeamTests.cs:18-43, 73-103]. A "helpful" `DateTime.Today` for the window or in an edited view fails it. Details views also must keep `ViewBag.BoardToday`.
**How to avoid:** derive the window from the selected `Year`/`Month`; edit Details views only via the shared partials where possible.

### Pitfall 5: `FeedRevisionWriteSeamTests` constrains the new repository
**What goes wrong:** any production `.cs` using `.Update(`, `.UpdateRange(`, `.Attach(`, `.AttachRange(`, `.Entry(`, `.TrackGraph(`, `ExecuteUpdate`, `ExecuteSql`, or `.State =` fails the build (the scan covers all three production projects except `Migrations/`, `AllowedWriteSites = []`).
**How to avoid:** load tracked entities and assign properties; use `DbSet.Add`/`Remove` and `SaveChangesAsync`. (`DbSet.Add(` / `.Remove(` are not in the banned list.)

### Pitfall 6: Phase 87 gating rejects anything but a real navigation
**What goes wrong:** an integration test that GETs a linked entry's URL with a plain `HttpClient` sees a 404 for a member and concludes the link is broken. The middleware needs `Sec-Fetch-Dest: document` + `Sec-Fetch-Mode: navigate` and the `CrossBoardWebApplicationFactory` (session-driven active board); `IBoardTypeResolver` stays bound to `MutableGroupContext` there [VERIFIED: Helpers/CrossBoardWebApplicationFactory.cs:21-30; Middleware/CrossBoardDeepLinkMiddlewareTests.cs `NavigationGet` helper].

### Pitfall 7: `agendaDays` filter and day sections on mobile
Covered under Pattern 4; add an explicit test: a month where B has a quest and A has nothing renders a day section on the mobile page.

### Pitfall 8: Details page also reached by finalized quests and Campaign boards
`QuestController.Details` builds `CalendarMonths` unconditionally; the finalized summary page does not render the calendar partial (EventCalendarPartialTests comment: "a finalized quest's Details page ... never calls the calendar partial"). A linked read there is wasted work but harmless. On a Campaign set, `includeQuests` must be false for the *linked* quests while own quests behave as today.

### Pitfall 9: Unique-name and case sensitivity differ between InMemory and SQL
Group/set name uniqueness is only enforced by SQL Server (case-insensitive by default collation); tests in InMemory cannot see it -> service pre-check. [ASSUMED: default SQL Server collation is case-insensitive for the production database; not verified against the deployed instance]

### Pitfall 10: Mobile CSS conventions
`calendar.mobile.css` / `platform-group.mobile.css` carry "No @media queries" rules; `MobileCssTests` exists for mobile.css integrity. Add new mobile rules without media queries, keep touch targets (`min-height: 44px` convention in mobile.css) for any tappable member row [CITED: QuestBoard.IntegrationTests/Mobile/MobileCssTests.cs header].

## Code Examples

### Linked-board lookup (no bypass; `Groups` is unfiltered)
```csharp
// Source: pattern from GroupRepository.GetAllWithMemberCountAsync (Groups read with no filter)
var setId = await DbContext.Groups.Where(g => g.Id == activeGroupId)
    .Select(g => g.LinkedBoardSetId).FirstOrDefaultAsync(token);
if (setId is null) return [];
var set = await DbContext.LinkedBoardSets.Where(s => s.Id == setId)
    .Select(s => new { s.BoardType }).FirstAsync(token);
var others = await DbContext.Groups
    .Where(g => g.LinkedBoardSetId == setId && g.Id != activeGroupId && g.BoardType == set.BoardType)
    .OrderBy(g => g.Name)
    .Select(g => new LinkedBoard { Id = g.Id, Name = g.Name })
    .ToListAsync(token);
// the extra BoardType equality is belt-and-braces if data were edited outside the app
```

### Linked service skeleton
```csharp
public async Task<LinkedCalendarData> GetAsync(int? activeGroupId, int viewerUserId, DateTime from, DateTime to, CancellationToken token = default)
{
    if (activeGroupId is not { } active) return LinkedCalendarData.Empty;
    var (boards, setBoardType) = await setRepository.GetLinkedBoardsForAsync(active, token);
    if (boards.Count == 0) return LinkedCalendarData.Empty;      // unlinked: no cross-board read at all

    var memberIds = (await groupService.GetGroupsForUserAsync(viewerUserId, token)).Select(g => g.Id).ToHashSet();
    var ids = boards.Select(b => b.Id).ToList();

    var quests = setBoardType == BoardType.Campaign ? [] : await questRepository.GetLinkedQuestsForCalendarAsync(ids, from, to, token);
    var events = await eventRepository.GetLinkedEventsForCalendarAsync(ids, from, to, token);
    // second layer: same ids as the query; catches a lost predicate, not a wrong set
    ...drop rows whose GroupId is not in ids, LogError on mismatch, stamp BoardName/ViewerIsMember...
}
```

### Day placement (add to `CalendarViewModel`, parity with the own rule)
```csharp
// finalized: only on FinalizedDate.Date AND only if a proposed date exists on that day (same as GetQuestsForDate)
// open: one entry per proposed date on this day
// events: already CancelledAt == null from the query; sort by StartTime ?? TimeOnly.MinValue like GetEventsForDate
```

### Test seeding recipe (closest existing tests)
```csharp
// Source pattern: AgendaTenantIsolationTests.cs SeedBoardAsync/SeedEventAsync/SeedMembershipAsync, CalendarBoardTypeScopeTests.SeedEventAndQuestAsync
await using var ctx = factory.Database.CreateContext();           // active board null => fine for inserts
ctx.Groups.Add(new GroupEntity { Id = 2, Name = "B", CreatedAt = DateTime.UtcNow, BoardType = (int)BoardType.OneShot, LinkedBoardSetId = setId });
// quest with ProposedDates: TestDataHelper.CreateTestQuestAsync(..., groupId: 2) + TestDataHelper.CreateProposedDateAsync(...)
// switch the viewed board:  factory.TestGroupContext.ActiveGroupId = 1;  GET /Calendar?year=Y&month=M   (+ mobile UA header for the mobile twin)
// DisposeAsync: factory.TestGroupContext.ActiveGroupId = 1; factory.TestGroupContext.BoardType = BoardType.OneShot;
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| Per-board `SetGroupId` iteration for cross-board reads | One query, `IgnoreQueryFilters()` + id set pinned in predicate + in-memory re-check | Phase 82 | The shape this phase reuses |
| Cross-board link 404 for members | Auto-switch via registry/middleware | Phase 87 | Member click needs no new code |
| Calendar reads fetch all rows, month filter in the view model | (this phase) window the *linked* read, leave own reads unchanged | Phase 91 | Keeps unlinked behaviour byte-identical |

**Deprecated/outdated:** nothing to deprecate. Note `.planning/codebase/TESTING.md` still says CI runs .NET 8.0.x (analysis dated 2026-07-02) while the solution is .NET 10 - do not rely on it for versions.

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|---------------|
| A1 | AutoMapper leaves unmatched destination members untouched on `Mapper.Map(model, trackedEntity)` (so a rename does not clear `LinkedBoardSetId`); mitigated by explicit `Ignore` | Pattern 1 | A rename silently unlinks a board; caught by a rename-keeps-link test |
| A2 | InMemory applies `SetNull`/cascade only to tracked dependents, so `DeleteSetAsync` must load boards tracked | Pattern 1 | A set delete test passes/fails for the wrong reason |
| A3 | The production SQL Server collation is case-insensitive, so `Name == x` uniqueness pre-check behaves like the index | Pitfall 9 | Two sets differing by case could coexist; low impact |
| A4 | The window predicate (`||` of finalized-range and `ProposedDates.Any`) and nested projection translate on SQL Server via EF Core 10 | Pattern 2 / Pitfall 1 | Runtime translation error on the real DB; needs the manual SQL check; fallback = drop the window or fetch then filter in memory |
| A5 | DM-only sessions (`DungeonMasterSession`) are shown on linked calendars exactly as on the own calendar (own `GetQuestsForCalendarAsync` does not filter them), i.e. visible to every viewer incl. non-members | Pattern 4 | A DM-only session on B becomes visible to non-members of B; confirm with the operator (D-06 says full entries, but DM-only was not discussed) |
| A6 | A finalized linked quest with no proposed date on its finalized day is hidden, mirroring the own rule | Pattern 4 | A booked date might not show; mirror is the stated D-09 intent |
| A7 | On a Campaign link set, linked *events* still show on Quest Details; only linked quests are suppressed | Pitfall 8 | Minor copy/UX difference |
| A8 | `dotnet-ef` local tool 9.0.6 can still scaffold against EF 10.0.9 (Phase 88 did so; warning only) | Standard Stack | Need `dotnet tool update` / use a global 10.x tool |

## Open Questions

1. **Window the linked reads or fetch everything (parity with own reads)?**
   - Known: own reads fetch all rows and the view model filters the month. D-17 only requires pinning to linked ids.
   - Unclear: whether translation risk (A4) outweighs payload savings.
   - Recommendation: window by month (simple predicates), with a manual local-SQL checkpoint; fall back to unwindowed if that check fails.
2. **DM-only sessions of a linked board (A5):** confirm they should show to non-members. Default recommended: mirror own-calendar behaviour, flag in the plan so the operator can veto.
3. **Board-name line visibility in a cramped desktop cell:** own chips show DM name and time only on hover (calendar.css:182-216 `display: none` until `:hover`). The approved sketch shows the board-name line always. Recommend always visible for linked chips (it *is* the marker); leave exact treatment to the UI contract (`ui_phase` is enabled in `.planning/config.json`).
4. **Show group (link-set) name to players?** Default: no; `LinkedBoard` carries only board id/name.
5. **Should the board index show a "linked" marker?** Optional; if done, extend the `GroupWithMemberCount` projection in `GroupRepository` (GroupRepositoryTests exist to extend).

## Environment Availability

| Dependency | Required By | Available | Version | Fallback |
|------------|------------|-----------|---------|----------|
| .NET SDK | build/test | Yes | 10.0.112 | - |
| `dotnet-ef` (local tool) | migration scaffold | Needs `dotnet tool restore` | manifest pins 9.0.6 (runtime EF 10.0.9) | `dotnet tool update`/global 10.x tool |
| SQL Server `localhost:1433` | manual migration + translation check (not needed for tests) | No (connection refused from sandbox) | - | Do not provision (project rule). Ask the user to run the local-SQL checkpoint (as in Phase 88); all automated tests use InMemory |
| RIP MCP | symbol navigation | No tools exposed to this agent | - | grep + Read (used) |

**Missing dependencies with no fallback:** none for automated work.
**Missing dependencies with fallback:** SQL Server for the translation/migration check -> user-run checkpoint.

## Validation Architecture

### Test Framework
| Property | Value |
|----------|-------|
| Framework | xUnit v3 3.2.2 + FluentAssertions 8.10.0; NSubstitute 5.3.0 (unit); EF InMemory 10.0.9; `Microsoft.AspNetCore.Mvc.Testing` 10.0.9 |
| Config file | `QuestBoard.IntegrationTests/xunit.runner.json` (`parallelizeAssembly: false`, `parallelizeTestCollections: false`); unit project has none |
| Quick run command | `dotnet test QuestBoard.UnitTests --filter "FullyQualifiedName~Linked"` |
| Full suite command | `dotnet test` (solution root) |
| Notes | Never set GC env vars; `0x80131506` = flake, rerun once; locked-file build failure = debugger running, ask user |

### Phase Requirements -> Test Map (derived from decisions; no REQ IDs)
| Decision | Behavior | Test Type | Automated Command | File Exists? |
|----------|----------|-----------|-------------------|-------------|
| D-01 | Add board of other type refused (service + posted directly); empty set refuses wrong type | unit (repo/service) + integration | `dotnet test QuestBoard.UnitTests --filter "FullyQualifiedName~LinkedBoardSetRepositoryTests"`; `... IntegrationTests --filter "FullyQualifiedName~LinkedBoardsPlatform"` | No, Wave 0 |
| D-02 | Board in at most one set; second add refused; set sees all others | unit | same repo tests | No, Wave 0 |
| D-03 | Delete a board in a set -> succeeds, set remains, other boards intact | integration (+unit) | `... --filter "FullyQualifiedName~LinkedBoardsPlatform"` | No, Wave 0 |
| D-04 | Set with <2 boards listed inactive and adds nothing to a calendar | integration | `... --filter "FullyQualifiedName~LinkedBoardCalendar"` | No, Wave 0 |
| D-05 | Non-SuperAdmin refused (GET/POST); create/rename/add/remove/delete work; picker lists only same-type unassigned | integration | `... --filter "FullyQualifiedName~LinkedBoardsPlatform"` | No, Wave 0 |
| D-06 | Non-member sees B's entry in full as plain text, no `href`, no `onclick`; incl. SuperAdmin non-member | integration (desktop + mobile UA) | `... --filter "FullyQualifiedName~LinkedBoardCalendar"` | No, Wave 0 |
| D-07 | No signup/vote/player names/DM-only fields beyond DM name leak; repo projection loads no signups | integration + unit (shape) | same + `FullyQualifiedName~LinkedCalendarRead` | No, Wave 0 |
| D-08 | Member gets `/Quest/Details/{id}` link; real navigation switches board and shows banner | integration (Cross harness, `Sec-Fetch-*` headers) | `... --filter "FullyQualifiedName~LinkedBoardDeepLink"` | No, Wave 0 |
| D-09/D-10 | Finalized on chosen date, open on every proposed date; cancelled linked event absent, own cancelled still struck through | unit (view model parity) + integration | `... --filter "FullyQualifiedName~CalendarViewModelLinked"` | No, Wave 0 |
| D-11 | Mobile `/calendar` shows linked entries and a linked-only day section | integration (mobile UA) | `... LinkedBoardCalendar` | No, Wave 0 |
| D-12 | Details desktop grid shows linked entries; mobile "Also that day" line; no vote input from linked; own events still absent on Details | integration + static guard | `... --filter "FullyQualifiedName~LinkedBoardQuestDetails"`; `~LinkedBoardStaticGuard` | No, Wave 0 |
| D-13 | Feed files untouched; no write to Events/Quests; revision unchanged | existing guard tests + diff check | `dotnet test --filter "FullyQualifiedName~Architecture"`; `git diff --stat -- QuestBoard.Domain/Services/CalendarFeedWriter.cs QuestBoard.Domain/Services/CalendarSubscriptionService.cs QuestBoard.Repository/FeedRevisionStamper.cs` is empty | Yes (existing) |
| D-14/D-15/D-16 | Muted/dashed class + board-name line; legend "Linked boards" only when linked; "Also showing" on mobile; own before linked in a day | integration (markup order via `IndexOf`) + unit (ordering) | `... LinkedBoardCalendar`, `CalendarViewModelLinked` | No, Wave 0 |
| D-17 | Bypass confined to allowlist; pinned predicate; re-check drops + logs foreign row; empty set -> no read | existing seam test + unit (service) + unit (repo, active board A reads B only for linked ids, never C) | `... --filter "FullyQualifiedName~CrossBoardIgnoreQueryFiltersSeamTests"`; `~LinkedCalendarServiceTests` | Seam test: Yes; new: No, Wave 0 |
| D-18 #1 | Unlinked board unchanged; no foreign title/board name on `/calendar` or Details | integration | `... LinkedBoardCalendar` (also existing `TenantIsolationTests`, `CalendarBoardTypeScopeTests`, `EventCalendarPartialTests` stay green) | New: No; existing: Yes |
| D-18 #2 | A,B linked, C not: A shows B not C; B shows A | integration | same | No, Wave 0 |
| D-18 #3 | Remove board from set -> next request shows nothing | integration | same | No, Wave 0 |
| D-18 #4-#7 | see D-06, D-07, D-09/D-10, D-12 rows | integration | same | No, Wave 0 |
| D-18 #8 | Server-side refusal (type, already linked, non-SuperAdmin) | integration + `AntiForgeryTokenCoverageTests` | `... LinkedBoardsPlatform`; `... AntiForgeryTokenCoverageTests` | New: No; coverage test: Yes |
| Schema | No pending model changes; migration has no forbidden text | unit | `dotnet test QuestBoard.UnitTests --filter "FullyQualifiedName~MigrationPreflightTests"` | Yes (existing) |
| Translation | Window predicate + projection translate on SQL Server | manual | user-run `dotnet ef database update` + load `/calendar` on a linked pair (not automatable here) | Manual checkpoint |

### Closest existing tests to model new ones on
- Isolation shape + helpers (`SeedBoardAsync`, `SeedEventAsync`, `SeedMembershipAsync`, `RemoveAllMembershipsAsync`, `DisposeAsync` reset): `QuestBoard.IntegrationTests/Tests/AgendaTenantIsolationTests.cs` (and `EventTenantIsolationTests`, `TenantIsolationTests`).
- Calendar render with desktop/mobile UA, quest + proposed date seeding recipe (set `FinalizedDate` on the tracked row): `QuestBoard.IntegrationTests/Controllers/CalendarBoardTypeScopeTests.cs`.
- Details with a proposed date so the calendar partial actually renders, and negative class assertions: `Controllers/EventCalendarPartialTests.cs`; mobile vote list: `Mobile/MobileViewsTests.cs` (~line 340).
- Deep-link chain, `NavigationGet` helper, two-board seed: `Middleware/CrossBoardDeepLinkMiddlewareTests.cs` on `CrossBoardWebApplicationFactory`.
- Platform SuperAdmin/deny matrix: `Controllers/PlatformAreaIntegrationTests.cs`, `Controllers/GroupManagementIntegrationTests.cs` (uses `CreateAuthenticatedSuperAdminClientAsync`, posts `Name`/`BoardType` form data).
- Repository unit tests against InMemory with a private `TestActiveGroupContext`: `QuestBoard.UnitTests/Repository/GroupRepositoryTests.cs`, `QuestBoardContextFilterTests.cs`.
- Service re-check/logging test with a hand-rolled recording logger and `NSubstitute` repository: `QuestBoard.UnitTests/Services/CrossBoardAgendaTests.cs`.
- Static source guard reading views from disk (repo-root walk): `Tests/CalendarSubscriptionStaticGuardTests.cs`.

### Sampling Rate
- **Per task commit:** `dotnet test QuestBoard.UnitTests --filter "FullyQualifiedName~Linked"` plus the touched integration class.
- **Per wave merge:** `dotnet test` (full solution; includes `Architecture`, `MigrationPreflightTests`, `AntiForgeryTokenCoverageTests`).
- **Phase gate:** full suite green + the manual local-SQL checkpoint recorded before `/gsd-verify-work`.

### Wave 0 Gaps
- [ ] `QuestBoard.UnitTests/Repository/LinkedBoardSetRepositoryTests.cs` - D-01..D-05, rename keeps link
- [ ] `QuestBoard.UnitTests/Repository/LinkedCalendarReadTests.cs` - pinned predicate, window, cancelled hidden, no signup/vote load, never returns an unlinked board's row
- [ ] `QuestBoard.UnitTests/Services/LinkedCalendarServiceTests.cs` - empty -> no read, Campaign set -> no quest read, foreign row dropped + logged, membership flag
- [ ] `QuestBoard.UnitTests/ViewModels/CalendarViewModelLinkedTests.cs` - placement parity, own-before-linked, default-empty
- [ ] `QuestBoard.IntegrationTests/Tests/LinkedBoardCalendarTenantIsolationTests.cs` - D-18 #1-#6 on both layouts
- [ ] `QuestBoard.IntegrationTests/Tests/LinkedBoardQuestDetailsTests.cs` - D-12 / D-18 #7
- [ ] `QuestBoard.IntegrationTests/Controllers/LinkedBoardsPlatformIntegrationTests.cs` - D-05 / D-18 #8 / D-03
- [ ] `QuestBoard.IntegrationTests/Middleware/LinkedBoardDeepLinkTests.cs` - D-08 chain
- [ ] `QuestBoard.IntegrationTests/Tests/LinkedBoardStaticGuardTests.cs` - linked partials contain no `<input`, `<form`, `DateVotes`, `vote-`; linked model types expose no vote/signup/player members (reflection); no `CrossBoardLink` prefix on new types
- [ ] No framework install needed (xUnit/FluentAssertions/NSubstitute present).

## Security Domain

`security_enforcement` is not set to `false` in `.planning/config.json` [VERIFIED: config read], so this section applies.

### Applicable ASVS Categories
| ASVS Category | Applies | Standard Control |
|---------------|---------|-----------------|
| V2 Authentication | no (existing cookie auth unchanged) | - |
| V3 Session Management | yes (active board from session; membership revalidation) | Existing `GroupSessionMiddleware`; linked set read fresh, never cached in session |
| V4 Access Control | **yes** | `[Authorize(Policy = "SuperAdminOnly")]` on the Platform controller; server-side type / at-most-one enforcement; membership read fresh from `UserGroups`; no SuperAdmin branch |
| V5 Input Validation | yes | `[Required]`, `[StringLength(100)]`, `[EnumDataType]`/`Enum.IsDefined`, id existence checks; Razor encoding for board/quest/event names (no `Html.Raw`, no `Html.Markdown` on titles) |
| V6 Cryptography | no | - |
| V8 Data Protection | **yes** | Narrow projection: no signups/votes/players/descriptions; D-06 explicitly authorizes only title/time/board/DM name |
| V13/CSRF | yes | `[ValidateAntiForgeryToken]` on every new POST; covered by `AntiForgeryTokenCoverageTests` |

### Known Threat Patterns for this stack
| Pattern | STRIDE | Standard Mitigation |
|---------|--------|---------------------|
| Cross-tenant read leak via filter bypass | Information disclosure | Predicate pinned to server-derived linked ids + in-memory re-check + allowlist confinement test; ids never taken from the request |
| IDOR on add/remove/rename (set id, board id posted directly) | Tampering / Elevation | SuperAdmin-only policy; service re-validates existence, type, current assignment; ignore client-side picker |
| Mixed-type link by crafted POST | Tampering | Server compares `board.BoardType` to `set.BoardType` (not only form options) |
| CSRF on admin POSTs | Tampering | `[ValidateAntiForgeryToken]` |
| Stored XSS via board / quest / event titles | Tampering | Razor default encoding; attributes (`title="..."`) via Razor |
| Link rendered but 404 / board enumeration via links | Information disclosure | Link only when viewer is a member (fresh `UserGroups`); plain text otherwise (accepted exposure per D-06) |
| Feed revision movement / write path creep | Tampering (integrity of calendar clients) | Pure read; `FeedRevisionWriteSeamTests` + untouched feed files |
| DM-only quest title shown to non-members | Information disclosure | Open question A5 / Open Question 2 |

## Sources

### Primary (HIGH confidence - opened this session)
- `.planning/phases/91-.../91-CONTEXT.md`, `91-DISCUSSION-LOG.md`, `.claude/ui-guidelines.md`, `CLAUDE.md` + imported `.claude/*.md`, `.planning/config.json`, `.planning/STATE.md` (phase 91/92 lines)
- `QuestBoard.UnitTests/Architecture/{CrossBoardIgnoreQueryFiltersSeamTests,AmbientClockSeamTests,FeedRevisionWriteSeamTests}.cs`; `QuestBoard.UnitTests/Migrator/MigrationPreflightTests.cs`; `QuestBoard.UnitTests/Helpers/CrossBoardLinkRegistryTests.cs`
- `QuestBoard.Repository/{QuestRepository,EventRepository,GroupRepository,CrossBoardLinkRepository,BaseRepository}.cs`, `Entities/{GroupEntity,QuestEntity,EventEntity,ProposedDateEntity,UserGroupEntity,CalendarSubscriptionEntity,QuestBoardContext}.cs`, `Automapper/EntityProfile.cs`, `Migrations/{20260703113120,20260918080027,20260930161610}*.cs`, `QuestBoardContextModelSnapshot.cs`
- `QuestBoard.Domain/{Models/Group.cs,Enums/BoardType.cs,Interfaces/*,Services/{EventService,GroupService,CrossBoardLinkResolverService,BoardClock}.cs,Extensions/ActiveGroupContextExtensions.cs}`
- `QuestBoard.Service/Controllers/{QuestBoard/CalendarController,QuestBoard/QuestController,AgendaController}.cs`, `Middleware/{CrossBoardDeepLinkMiddleware,GroupSessionMiddleware}.cs`, `Helpers/CrossBoardLinkRegistry.cs`, `ViewExpanders/MobileViewLocationExpander.cs`, `Program.cs` (policy, route, middleware order), `Areas/Platform/**` (controller, views, layouts, ViewImports), `ViewModels/{CalendarViewModels,PlatformViewModels}/*`, `Views/{Calendar,Shared,Quest}/*` calendar markup, `wwwroot/css/{calendar,calendar.mobile,platform-group.mobile}.css`, `Extensions/HtmlHelperExtensions.cs`
- `QuestBoard.IntegrationTests/**` listed above (factory, helpers, closest tests)
- `QuestBoard.Migrator/MigrationRunner.cs`; `.config/dotnet-tools.json`; sandbox probes (`dotnet --version`, `localhost:1433`)

### Secondary (MEDIUM confidence)
- `.planning/phases/82-personal-cross-board-event-agenda/82-CONTEXT.md` (D-14..D-17), `.planning/phases/88-.../88-05-PLAN.md`, `88-VALIDATION.md` (migration + manual SQL check precedent) - read via grep excerpts

### Tertiary (LOW confidence)
- EF Core behaviours stated as `[ASSUMED]` in the Assumptions Log (InMemory SetNull on tracked entities, AutoMapper unmatched members, SQL translation of the window predicate)

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH - no new packages; versions read from csproj files
- Architecture: HIGH - every integration point opened and line-cited; design choices (column on `Groups`, reads in existing repositories) follow existing precedent
- Pitfalls: HIGH for guard-test interactions (read the test source); MEDIUM for SQL translation (cannot run SQL Server here)

**Research date:** 2026-10-15
**Valid until:** 2026-11-15 (stable codebase; re-check allowlist/guard tests if another phase lands first - Phases 90 and 92 are adjacent)
