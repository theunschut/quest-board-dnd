# Phase 92: Move a Quest or Event to a Linked Board - Research

**Researched:** 2026-10-18
**Domain:** Cross-board write on an EF Core 10 / ASP.NET Core 10 MVC multi-tenant app (global query filters, store-owned feed revision, Hangfire email jobs, Razor desktop + `.Mobile` twins)
**Confidence:** HIGH (every structural claim below was read in source this session; one throwaway InMemory spike confirmed the write order, then was deleted)

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions

### Who may move, and to where

- **D-01: Super admin only, enforced on the server on both GET and POST.** The `SuperAdminOnly`
  policy guards the Move page and its POST; the button on Details renders only for a SuperAdmin.
  A board Admin or DM of both boards is refused like anyone else. The super admin does not need
  to be a member of the target board.

- **D-02: The target is another board in the source board's linked set, read fresh per request.**
  The set is looked up on every GET and again on the POST, never from session or claims (Phase 82
  D-15). A posted target that is not in the source board's set, or the source board itself, is
  refused even when posted directly. Same board type follows from Phase 91 D-01 and needs no
  separate rule, but the server must not rely on it being true.

- **D-03: The quest's DM must be a DungeonMaster or Admin on the target board.** Read fresh from
  `UserGroups`, using the same effective-role rule the board already applies. Otherwise the Move page
  shows the target as refused with the reason, and the POST refuses. A quest never arrives on a
  board whose members cannot see its DM as a DM, and the DM never loses sight of their own quest.

  Rejected: any membership being enough (a DM would sit on the board as a Player); letting the move
  go ahead and having the target board's Admins take the quest over.

### People on a moved quest

- **D-04: A signup from a target-board member is kept, with its character cleared.** Role,
  `IsSelected`, `SignupTime`, `LastVoteChangeTime` (so the waitlist order holds) and every date vote
  come along unchanged. `CharacterId` is set to null, because characters belong to one board and the
  source board's character is invisible on the target. The player re-picks a target-board character
  with the existing change-character control on Details, which already runs
  `ResolveCharacterAssignmentAsync`.

  Rejected: auto-pointing the signup at the player's Main on the target board (could pick a character
  the player did not intend); dropping every signup (loses votes and the selected party).

- **D-05: A signup from a non-member of the target board is dropped, and the Move page names it.**
  The signup and its votes are removed. The Move page lists those players by name and marks the ones
  who were selected. This applies to every signup role (Player, Assistant DM, Spectator). The super
  admin can cancel, add the players to the target board, and come back.

  Rejected: refusing the move until everyone is a member (a player who deliberately is not in the
  other campaign would block it for good); keeping their signup (a roster row on a board they cannot
  open).

- **D-06: A seat freed on a finalized quest moves the waitlist up by the normal rule.** For each
  selected Player-role signup dropped under D-05, the existing promotion rule runs: the top waitlisted
  candidate among the kept signups is selected, and the existing Waitlist Promoted email goes to that
  player. The Move page says how many seats will open and that the waitlist moves up.

  The promotion and its email belong to the target board. The email job takes a `groupId` for its
  query filter, so it must be enqueued with the target board's id. Running the promotion through
  repository reads that are scoped to the source board after the quest has left it would find
  nothing. The order of operations is for the planner.

- **D-07: Everything else that hangs off the quest comes along unchanged.** Proposed dates, the
  kept signups' votes and the reminder logs have no board column of their own and follow
  `Quest.GroupId`, so a reminder already sent is not sent again. No gold is involved: `Rewards` is
  free text and `TransactionType.QuestReward` is never written anywhere.

### People on a moved event

- **D-08: Event availability answers are reconciled as if the event had been created on the target
  board.** Answers from target-board members are kept exactly as they are, including whether they
  were real answers or automatic passes. Answers from non-members are dropped and named on the Move
  page, the same rule as D-05. On a Campaign board, every target-board member who has no row gets the
  automatic Yes row exactly as `AddWithCampaignFanOutAsync` creates it for a new event.

  Rejected: keeping members' answers with no fill-in (target-only members would look unanswered,
  unlike any other Campaign event); resetting every answer (loses real answers).

### Series and follow-up chains

- **D-09: An occurrence of a recurring series cannot move.** Only one-off events can: events that
  were never in a series, or whose series was detached with the existing "Detach sessions" action on the series page. The
  reason this is a hard rule: the nightly top-up decides which slots already exist by reading the
  source board's events, so an occurrence that left the board, or was detached on its own, would
  read as a free slot and be recreated on its original date. Moving a whole series is deferred.

  Rejected: moving the occurrence out and leaving a cancelled placeholder in its slot (a
  struck-through entry on the source calendar for no visible reason); moving the whole series now.

- **D-10: A quest in a follow-up chain can move, and the chain is cut where it would cross
  boards.** If the moved quest is a follow-up of a source-board quest, its own `OriginalQuestId` is
  cleared. If it has a follow-up on the source board, that follow-up's `OriginalQuestId` is cleared.
  A quest in the middle of a chain loses both links. The Move page names each quest it will no
  longer be linked to. The cut is permanent: moving the quest back does not restore it.

  The link has to be cut, not left in the database but hidden by the board filter. The relationship
  is one-to-one on `OriginalQuestId`, so a hidden link would let a DM on the source board try to
  create a second follow-up to the original, which hits the uniqueness constraint and errors.

  Rejected: refusing quests in a chain (a follow-up created on the wrong board could then never be
  fixed); moving the whole chain.

### Which entries can move

- **D-11: Quests that have not been played yet.** Open quests (still voting) and finalized quests
  whose date has not yet passed on the board clock. A quest that `HasBeenPlayed(boardToday)`, or is
  closed into the Quest Log, is board history and cannot move.

- **D-12: One-off events dated today or later on the board clock, cancelled or not.** A cancelled
  event moves and stays cancelled, so the target calendar shows it struck through. Past events
  cannot move. "Today" comes from `IBoardClock`, never an ambient clock read.

- **D-13: Boards outside a set offer nothing.** On a board in no set, or in a set of one, Details
  shows no move control at all, to anyone.

### Flow

- **D-14: A dedicated Move page, desktop and `.Mobile.cshtml`.** A "Move to linked board" control
  on Details opens it. On the page the super admin picks the target board (preselected when the set
  has only one other board) and sees what the move will do on that board before confirming:
  - how many signups or answers are kept, and that characters will be cleared;
  - who is dropped, by name, with selected players marked;
  - how many seats open and that the waitlist moves up;
  - which quests it will no longer be linked to;
  - on a Campaign board, how many automatic Yes rows are added;
  - or, if the move is refused for that board, why.

  The consequences depend on the target, because membership differs per board, so they are computed
  for the chosen board, not for the set.

  Entry points: Quest Details on both layouts (`Quest/Details.cshtml` and `Quest/Details.Mobile.cshtml`)
  and Event Details (`Events/Details.cshtml`, which has no `.Mobile` twin today and serves both).

  Rejected: a modal on Details (needs a fetch for the consequence list, and a second modal on
  mobile); starting moves from the Platform Linked boards page (the roadmap places the move on the
  entry's Details page).

- **D-15: The POST recomputes everything on the server.** Membership, roles, eligibility and the
  set are read again at confirm time and nothing from the preview is trusted.

- **D-16: Afterwards the super admin lands back on the source board.** A quest move returns to the
  source board's Quest Board, and an event move to the source board's events list, with a toast
  such as "Moved 'Goblin Market' to Board B". If the super admin is a member of the target board, the
  toast links to the entry. The link is the ordinary Details URL and goes through the Phase 87 board
  switch.

  Rejected: switching the session to the target board (a super admin who is not a member would hit
  the Phase 87 404, so a fallback would be needed anyway).

### Notifications, feed and trace

- **D-17: A quest move emails the people on the quest. An event move emails nobody.**
  - The DM and every kept signup get a "this quest is now on Board B" email linking to the quest's
    Details.
  - Every dropped player gets a "your signup was removed because you are not a member of Board B"
    email.
  - Only confirmed addresses are mailed, as with every existing quest email.
  - Naming Board B to a dropped player is acceptable: they are a member of a board in the same set,
    so Phase 91 D-06 already shows them Board B's name on their calendar.
  - Event moves send nothing. On a Campaign board that would mail every member, and calendars update
    anyway.
  - The cost is roughly 5–10 mails per quest move, well within the 100/day relay limit, and moves
    are rare.

  Rejected: no email at all (a dropped player just finds their signup gone); mailing only dropped
  players and the DM.

- **D-18: The calendar feed is accepted as it is. No change to `CalendarFeedWriter.cs`,
  `CalendarSubscriptionService.cs` or `FeedRevisionStamper.cs`.** The move changes `GroupId`
  through the change tracker, so `FeedRevisionStamper` raises the entry's revision. The UID stays
  the same, `SEQUENCE` goes up and the `SUMMARY` prefix changes from `[Board A]` to `[Board B]`.
  Subscribers who cannot see the entry on the target board lose it on their next poll, with no
  cancellation, and target-board subscribers gain it.

  Rejected: keeping a `STATUS:CANCELLED` copy in the feeds of subscribers who lose the entry (needs
  per-subscriber tombstones in high-risk files).

- **D-19: A move leaves one structured server log line and nothing on the entry.** The line
  carries the actor, the entry type and id, the source and target board ids, and the counts of kept,
  dropped and promoted signups or answers, plus any chain links cut. Nothing is stored on the entry
  and there is no migration, so it really does look as if it had been created on the target board.

  Rejected: a visible "Moved from Board A" note (needs stored fields and contradicts the goal).

### The write and its proof

- **D-20: One move is one unit of work.** Changing the board, clearing characters, dropping
  signups and answers, cutting chain links, adding Campaign rows and promoting from the waitlist
  either all happen or none do. A failure leaves the entry on the source board untouched. Every write
  to `Quests` and `Events` goes through the change tracker: no `ExecuteUpdate`, raw SQL, attached
  detached entity or hand-set entry state (`FeedRevisionWriteSeamTests` bans `ExecuteUpdate` in all
  production source). No schema change is expected. If planning finds one is needed, it ships as an
  EF Core migration.

- **D-21: Required integration-test proofs.** The tests must prove at least:
  1. **Authorization.** A non-SuperAdmin, including an Admin or DM of both boards, is refused on the
     Move page GET and POST.
  2. **Target rules.** A target outside the source board's set, or the source board itself, is
     refused even when posted directly; a board in no set or a set of one shows no move control.
  3. **The move happens.** The quest or event appears on the target board and is gone from the
     source board's pages, and its `FeedRevision` has risen.
  4. **Kept signups.** A target member's signup keeps its role, selection and votes, and its
     character is null.
  5. **Dropped signups.** A non-member's signup and votes are gone.
  6. **DM rule.** A quest whose DM is not a DungeonMaster or Admin on the target board is refused.
  7. **Waitlist.** On a finalized quest, dropping a selected non-member promotes the top waitlisted
     kept signup, and the promotion email is enqueued with the target board's id.
  8. **Series.** A series occurrence is refused; a one-off event, including a detached one, moves.
  9. **Chains.** Both chain links are cleared. The InMemory provider enforces no unique index, so
     assert the stored links are null rather than relying on a duplicate-follow-up error to appear.
  10. **Eligibility.** A played or closed quest and a past event are refused; an upcoming cancelled
      event moves and stays cancelled.
  11. **Campaign events.** Target members without a row get the automatic Yes row, and non-members'
      rows are dropped.
  12. **Mail.** A quest move enqueues the kept and dropped emails; an event move enqueues none.

  Reset `ActiveGroupId` to `1` in `DisposeAsync`, as Phase 82 D-17 requires, because
  `WebApplicationFactoryBase.TestGroupContext` is a shared singleton.

### Claude's Discretion

- **An entry that cannot move, on a linked board.** Default: the super admin sees the control
  disabled with a one-line reason (series occurrence, already played, closed, past) rather than
  hidden, so a missing button never looks like a bug. A refusal that depends on the target (D-03)
  shows on the Move page, per board.
- **A change between preview and confirm** (someone signs up, withdraws or changes membership in
  between). Default: when the recomputed consequences differ from what the page showed, re-show the
  Move page with the new consequences instead of moving.
- **The email templates.** One new template with a kept and a dropped variant, or two templates,
  rendered through the existing Razor email pipeline and dispatcher and listed on
  `EmailPreviewController` like the others. Exact copy is open.
- **Whether the acting super admin is mailed** when they are also the quest's DM or a signup.
  Default: not about their own move.
- **A player promoted by D-06** may get the Waitlist Promoted mail only or that plus the move mail.
  Keep it to the fewest mails that still tell them both facts.
- **Where the move logic lives.** A Domain service behind an interface, called from new actions on
  `QuestController` / `EventsController` or from a small dedicated controller. It must not reuse the
  `CrossBoardLink*` prefix, which belongs to Phase 87's deep-link resolver. Any new file that needs
  `IgnoreQueryFilters()` must be argued onto `CrossBoardIgnoreQueryFiltersSeamTests`' allowlist in
  that test's comment. Prefer reading membership through the existing `UserGroups` and linked-set
  reads, which need no bypass.
- **Copy and layout** of the Move page, the toast and the disabled-control reasons, within
  `.claude/ui-guidelines.md`.
- **Which events list** an event move returns to, if the board has more than one.
- Test structure beyond the mandatory cases in D-21.

### Deferred Ideas (OUT OF SCOPE)

- **Move a whole event series to a linked board.** The series row, its template and every
  occurrence move together, so the top-up keeps working on the target board. Needs its own answers
  for past occurrences, answers and Campaign rows. Not roadmapped.
- **Cancellation entries for subscribers who lose a moved entry.** A `STATUS:CANCELLED` copy kept
  for a while in the feed of anyone who can no longer see the entry, so phones remove it explicitly
  rather than on a silent next poll. Touches the high-risk feed files; only if wanted.
</user_constraints>

<phase_requirements>
## Phase Requirements

No requirement IDs are assigned (ROADMAP: "Requirements: TBD"). The locked decisions D-01 to D-21 are the requirement set. UI copy, tokens and the email template contract are fixed by `92-UI-SPEC.md` (approved).

| Decision group | Research support (section) |
|----|----|
| D-01, D-02, D-13 (who, where to, nothing outside a set) | Pattern 1 (authorization + fresh set read), Architecture, Pitfall 7 |
| D-03 (DM rule) | Pattern 3 (membership reads), Open Question 1 |
| D-04, D-05, D-06, D-07, D-10 (quest people, waitlist, chain) | Pattern 2 (write order and the one SaveChanges), Pattern 4 (in-memory promotion), Pitfalls 1-4 |
| D-08 (event answers, Campaign fan-out) | Pattern 5, Code Examples |
| D-09, D-11, D-12 (eligibility) | Pattern 6, Pitfall 9 |
| D-14, D-15, D-16 (page, recompute, toast) | Pattern 1, Pattern 7 (preview stamp), Code Examples, Pitfalls 8, 10 |
| D-17 (mail) | Pattern 8, Pitfalls 5, 6 |
| D-18, D-19, D-20 (feed, log, unit of work) | Pattern 2, "FeedRevisionStamper findings", Validation Architecture |
| D-21 (proofs) | Validation Architecture (test map, harness, InMemory limits) |
</phase_requirements>

## Summary

The whole move can be done as **one `SaveChangesAsync` on tracked entities that were all loaded while the source board is still active**. Every read that the query filters would hide after the board changes (signups, votes, proposed dates, follow-ups, event answers) happens first; every mutation happens next; the quest or event `GroupId` is changed last; one save commits everything. Nothing in the write needs `IgnoreQueryFilters()`: `UserGroups` and `Groups` carry no query filter, so target membership and roles are read with the existing `IUserRepository` and `ILinkedBoardSetRepository` reads. A throwaway InMemory spike (deleted afterwards) confirmed this order: one `SavedChanges` event, the quest `FeedRevision` rose 1 to 2 exactly once, a follow-up whose only change was `OriginalQuestId` kept revision 1, deleted signups took their loaded votes with them, and the quest was invisible on board 1 and visible on board 2 afterwards.

`FeedRevisionStamper` already treats `GroupId` as a feed field for both quests and events and is already unit-tested for it, so the revision rises with no code change, provided the write stays on the change tracker. The waitlist rule cannot reuse `QuestService.PromoteNextWaitlistedPlayerIfSeatFreedAsync` (it is private, writes per candidate and reads through the filtered repository), so its selection rule is applied in memory with `WaitlistOrdering.OrderWaitlist` on the kept signups before the save, and pinned to `GetTopWaitlistedCandidateAsync` by a parity test. Emails are enqueued only after the save succeeds. Two existing guard tests will break if the UI-SPEC is followed literally (`EmailPreviewController` must keep exactly five `DateTime.Today`; `CrossBoardLinkRegistry` must keep exactly 18 routes), and the integration factory registers a no-op email dispatcher, so D-21 #7 and #12 need a dispatcher override in a new factory subclass.

**Primary recommendation:** Add a Domain `IBoardMoveService` that computes a plan from AsNoTracking reads (kept, dropped, promoted, links to cut, Campaign rows, a stamp), plus a new internal `BoardMoveRepository` whose `ApplyQuestMoveAsync` / `ApplyEventMoveAsync` re-load tracked entities under the source filter, validate the plan against what is loaded, mutate, change `GroupId` last and call `SaveChangesAsync` exactly once; enqueue mail after it returns; serve the pages from a new `MoveController` (`SuperAdminOnly`) with one `Views/Move/Index(.Mobile).cshtml` pair.

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|-------------|----------------|-----------|
| Super-admin gate on GET and POST | API / Backend (MVC controller policy) | Browser (button only rendered for SuperAdmin) | The policy is the boundary; hidden markup is not enforcement (existing comment in `EventViewModel`) |
| Linked-set membership of the target, fresh per request | API / Backend (Domain service over `ILinkedBoardSetRepository`) | — | D-02: never from session or claims |
| Eligibility (closed, played, series, past) | Domain (pure rule using `IBoardClock`) | Razor (disabled control uses the same rule via the controller) | One definition shared by control and server, like `HasBeenPlayed` |
| Plan computation (kept, dropped, promoted, cut links, Campaign rows) | Domain service on domain models | — | Pure logic, unit-testable with NSubstitute and `FakeBoardClock` |
| The atomic write | Repository (`QuestBoardContext`, one `SaveChangesAsync`) | — | EF only in `QuestBoard.Repository`; tracked writes so the stamper sees `GroupId` |
| Feed revision bump | Repository (existing `FeedRevisionStamper`, no change) | — | Store-owned, D-18 |
| Email enqueue | Domain service via `IQuestEmailDispatcher` (after save) | Service layer jobs render and send | Dispatcher is the existing Domain-to-Hangfire seam |
| Move page, toast, Details control | Frontend Server (Razor, desktop + `.Mobile`) | — | Server-rendered, no new JS (UI-SPEC) |
| Audit trail | API / Backend (structured log line) | — | D-19, nothing stored |

## Standard Stack

No new packages. Everything below is already referenced in the solution.

### Core
| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| Microsoft.EntityFrameworkCore (+ SqlServer, InMemory for tests) | 10.0.9 | Tracked unit of work, global query filters | Already the persistence layer `[VERIFIED: QuestBoard.Repository.csproj, QuestBoard.UnitTests.csproj]` |
| AutoMapper | 16.2.0 | Entity to domain model for the read side | Already used at both boundaries `[VERIFIED: QuestBoard.Domain.csproj]` |
| Hangfire.AspNetCore | 1.8.23 | Fire-and-forget email jobs via `IBackgroundJobClient` | Existing dispatcher pattern `[VERIFIED: QuestBoard.Service.csproj]` |
| System.Security.Cryptography.SHA256 (BCL) | in-box | Preview stamp (hash of the consequence set) | No package needed; the stamp is a change detector, not a secret, because the server recomputes everything |

### Supporting (test stack, already present)
| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| xunit.v3 | 3.2.2 | Test framework (both test projects) | All new tests `[VERIFIED: QuestBoard.UnitTests.csproj]` |
| FluentAssertions | 8.10.0 | Assertions | All new tests |
| NSubstitute | 5.3.0 | Repository/dispatcher substitutes in Domain unit tests | Service-level plan and mail tests |
| Microsoft.AspNetCore.Mvc.Testing | 10.0.9 | `WebApplicationFactory<Program>` | Integration tests |

### Alternatives Considered
| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| One tracked `SaveChangesAsync` | Explicit `BeginTransactionAsync` around several saves | Needs an `IsRelational()` guard (the InMemory provider rejects transactions; `EventSeriesRepository.CreateWithOccurrencesAsync` already guards line 195) and gives the stamper several save boundaries. One save is atomic by default `[CITED: learn.microsoft.com/ef/core/saving/transactions]`; no reason to add a transaction |
| New `BoardMoveRepository` | Add methods to `QuestRepository` and `EventRepository` | Those two files are on `AmbientClockSeamTests` and `CrossBoardIgnoreQueryFiltersSeamTests` lists and are already large; a new file needs neither argument |
| New `MoveController` | Add actions to `QuestController` / `EventsController` | `QuestController` is 1,100+ lines and its file is ambient-clock guarded; a small dedicated controller keeps `SuperAdminOnly` at class level |

**Installation:** none.

**Version verification:** versions above were read from the `.csproj` files this session; no registry lookups were needed because no package is added.

## Package Legitimacy Audit

No external packages are installed by this phase. SHA-256 comes from the BCL. Audit not applicable.

**Packages removed due to [SLOP] verdict:** none
**Packages flagged as suspicious [SUS]:** none

## Architecture Patterns

### System Architecture Diagram

```
Super admin browser
   |  GET /Move/Quest/{id}?targetBoardId=B           POST /Move/Quest (id, targetBoardId, stamp)
   v                                                      v
[GroupSessionMiddleware] -> active board A required (not an exempt prefix)
   v
[MoveController  SuperAdminOnly]  --policy fail--> /Account/AccessDenied
   |
   |-- load entry under A's filter (IQuestRepository.GetQuestWithDetailsAsync / IEventRepository.GetEventWithDetailsAsync)
   |        null -> 404 (entry is on another board or absent)
   |-- eligibility rule (IBoardClock.Today) -- refuse -> TempData Error + redirect to Details
   |-- linked set read fresh: ILinkedBoardSetRepository.GetLinkedBoardsForViewerAsync(A, actorId)
   |        target not in .Boards (or == A) -> refuse
   |-- target membership + DM role: IUserRepository.GetAllGroupMembers(B), GetGroupRoleAsync(dmId, B)   (UserGroups: no filter)
   v
[IBoardMoveService]  compute plan (kept / dropped / promoted / cut links / campaign adds / stamp)   <- pure, domain models
   |
   |-- GET: render preview (or refusal)                         
   |-- POST: stamp differs -> re-render page 200 with notice, nothing written
   |         stamp equal   -> IBoardMoveRepository.Apply*Async(plan)
   v
[BoardMoveRepository]  ONE unit of work, source board still active
   1. load tracked entry + children (Include signups, DateVotes / event Signups)  + follow-ups by OriginalQuestId
   2. verify loaded rows == plan (else Conflict, nothing mutated)
   3. mutate: Remove dropped (+their votes), CharacterId=null, IsSelected=true (promoted), cut links, add Campaign rows
   4. entry.GroupId = target   (last)
   5. SaveChangesAsync  -> FeedRevisionStamper bumps entry revision once; FK-safe, atomic on SQL Server
   v
[IBoardMoveService] after success: enqueue mail (target group id) -> structured log line
   v
302 -> source board Quest Board / Events list, TempData toast (+ "Open it on B" link if actor is a member of B)
```

### Recommended Project Structure
```
QuestBoard.Domain/
  Interfaces/IBoardMoveService.cs, IBoardMoveRepository.cs
  Services/BoardMoveService.cs                  # plan + stamp + mail + log
  Extensions/MoveEligibility.cs                 # pure: reason for a quest / event, takes boardToday
  Models/QuestMovePlan.cs, EventMovePlan.cs, QuestMovePreview.cs, EventMovePreview.cs, MoveWriteResult (enum)
  Enums/QuestMovedAudience.cs                   # Player, DungeonMaster, Removed
QuestBoard.Repository/
  BoardMoveRepository.cs                        # new file, internal sealed
QuestBoard.Service/
  Controllers/MoveController.cs                 # [Authorize(Policy="SuperAdminOnly")]
  ViewModels/MoveViewModels/*.cs
  Views/Move/Index.cshtml + Index.Mobile.cshtml
  Components/Emails/QuestMoved.razor ; WaitlistPromoted.razor (+ MovedToBoardName)
  Jobs/QuestMovedEmailJob.cs
  Constants/TempDataKeys.cs (3 new keys) ; Views/Shared/_Toasts.cshtml (1 block)
  wwwroot/css/move-entry.css, move-entry.mobile.css
```
Register `IBoardMoveService` in `QuestBoard.Domain/Extensions/ServiceExtensions.cs` and `IBoardMoveRepository` in `QuestBoard.Repository/Extensions/ServiceExtensions.cs` (both are `AddScoped` lists, read this session).

### Pattern 1: Controller placement, authorization and the fresh set read
**What:** A dedicated `MoveController` with class-level `[Authorize(Policy = "SuperAdminOnly")]`; GET `Quest(int id, int? targetBoardId)` and `Event(...)`, POST `Quest(...)` and `Event(...)` with `[ValidateAntiForgeryToken]`. The `SuperAdminOnly` policy is `policy.RequireRole("SuperAdmin")` `[VERIFIED: QuestBoard.Service/Program.cs:90-91]` and failures route to `/Account/AccessDenied` (`ConfigureApplicationCookie`, Program.cs ~line 97).
**When to use:** Every Move action. Never key the server check on `CanManage` (a board Admin or DM must be refused, D-01).
**Rules found in the code:**
- The target set is `ILinkedBoardSetRepository.GetLinkedBoardsForViewerAsync(activeGroupId, actorUserId)`. It reads the groups fresh, excludes the active board (`g.Id != activeGroupId`), excludes boards of a different type, and returns `Empty` when the active board is in no set, alone, or differs from the set's type `[VERIFIED: LinkedBoardSetRepository.cs:244-292]`. So "target is in `.Boards`" is the complete D-02 check, and `ViewerIsMember` on the same rows answers the toast-link question for free (`LinkedBoard.ViewerIsMember => ViewerRole != null`).
- `SetBoardType` on `ViewerLinkedBoards` is the target's board type (all boards returned share it) - use it for the Campaign fan-out decision and the "calendar apps" row (quests only exist on One-Shot sets; `LinkedCalendarService` never fetches quests for a Campaign set).
- The new route needs **no** entry in `CrossBoardLinkRegistry`. `CrossBoardLinkRegistryTests.RegisteredRoutes_HasExactly18Entries` pins the count at 18 `[VERIFIED: QuestBoard.UnitTests/Helpers/CrossBoardLinkRegistryTests.cs:50]`, and the middleware only resolves GET routes that are in the closed table. A Move URL followed from another board simply 404s, which is the intended oracle-safe behaviour. The toast link and email links target `Quest/Details` and `Events/Details`, which are already registered.
- `GroupSessionMiddleware` exempt prefixes are `/groups/pick`, GroupPicker, Account, `/platform`, `/Error`, Agenda. `/Move` is not exempt, so a request without an active board is redirected (GET) or answered 409 (POST) before the controller runs.
- Do not declare any type starting with `CrossBoardLink`. `LinkedBoardStaticGuardTests.CrossBoardLinkPrefix_BelongsOnlyToTheDeepLinkResolver` fails on a fourth declaration `[VERIFIED: LinkedBoardStaticGuardTests.cs:251-253]`.

### Pattern 2: The write order (answers research question 1)
**What:** Reads first, mutations second, `GroupId` last, one save. All of it inside `BoardMoveRepository`, with the source board still the active board.
**Why this works:** Query filters apply only when a query is composed, never to entities already tracked. The filters that hide data after a move are `Quest.GroupId == ActiveGroupId` and the filters that reach through `Quest`/`ProposedDate`/`Event` (`PlayerSignups`, `PlayerDateVotes`, `ProposedDates`, `ReminderLogs`, `EventSignups`) `[VERIFIED: QuestBoardContext.cs:480-522, 619-636]`. Once the graph is tracked, changing `GroupId` has no effect on what the save does. `FeedRevisionStamper.Apply` reads only the change tracker and issues no query `[VERIFIED: FeedRevisionStamper.cs remarks]`.

**Quest steps (concrete):**
1. `quest = Quests.Include(PlayerSignups).ThenInclude(DateVotes).FirstOrDefault(id)` tracked, under the source filter. `null` returns `NotFound`.
2. `followUps = Quests.Where(q => q.OriginalQuestId == id).ToList()` tracked, still under the source filter (do this **before** step 6; after the board changes this query returns nothing). Do not `Include(OriginalQuest)`: a link that points across boards would be filtered to null, but the scalar `OriginalQuestId` is what must be cleared.
3. Validate: the loaded signup ids must equal `plan.KeptSignupIds` union `plan.DroppedSignupIds`; the loaded follow-up ids must equal `plan.FollowUpQuestIdsToUnlink`. Any difference returns `Conflict` before anything is mutated (the controller re-shows the page with the "things changed" notice).
4. Dropped signups: `DbContext.PlayerSignups.Remove(signup)` **and** `RemoveRange(signup.DateVotes)`. The spike showed that removing a signup whose votes are tracked cascades in EF, while a second spike showed that removing a signup **without** loading its votes leaves the votes behind under InMemory (SQL Server would cascade in the database). Always load the votes.
5. Kept signups: `CharacterId = null`; promoted signups: `IsSelected = true` (nothing else changes; `LastVoteChangeTime` and `SignupTime` stay).
6. Chain: `quest.OriginalQuestId = null` when the plan says so; `followUp.OriginalQuestId = null` for each follow-up.
7. `quest.GroupId = plan.TargetGroupId` (last).
8. `await DbContext.SaveChangesAsync(token)` exactly once.

**Event steps:** `Events.Include(Signups)` tracked; validate the loaded `UserId` set equals kept union dropped; `Remove` dropped rows; `Signups.Add(new EventSignupEntity { UserId = id, Availability = (int)VoteType.Yes })` for each Campaign add (see Pattern 5); `GroupId = target` last; one save. Cancellation (`CancelledAt`) and `SeriesId`/`SeriesSlotIndex` are untouched (eligibility already required `SeriesId == null`).

**Anti-pattern to avoid:** calling `IPlayerSignupRepository.UpdateAsync`, `RemoveAsync`, `IQuestRepository` helpers or `GetTopWaitlistedCandidateAsync` between "change GroupId" and "save": each issues filtered queries (and several call `SaveChangesAsync` themselves, which splits the unit of work). All of them hide the quest once `GroupId` has changed in the database, and a half-applied move is exactly what D-20 forbids.

### Pattern 3: Membership and role reads for the target (answers question 3)
- `UserGroups` has **no** query filter (the only filtered entities are Quest, ShopItem, ProposedDate, PlayerDateVote, PlayerSignup, ReminderLog, Character*, Contact*, Event, EventSeries, EventSignup, UserTransaction `[VERIFIED: QuestBoardContext.cs:480-657]`). `Groups` has none either. Therefore no new `IgnoreQueryFilters()` file is needed anywhere in this phase and the `CrossBoardIgnoreQueryFiltersSeamTests` allowlist (`EventSignupRepository`, `EventRepository`, `GroupRepository`, `QuestRepository`, `CrossBoardLinkRepository`) stays untouched.
- Members of board X: `IUserRepository.GetAllGroupMembers(groupId)` returns every user with a `UserGroups` row for that group regardless of role `[VERIFIED: UserRepository.cs:57-65]` (this is also what the Campaign create path uses).
- The DM rule (D-03): `IUserRepository.GetGroupRoleAsync(dmUserId, targetGroupId)` returns the raw `UserGroups` role or null `[VERIFIED: UserRepository.cs:79-86]`. Accept `GroupRole.DungeonMaster` or `GroupRole.Admin`; null or `Player` refuses. Quoted values: `Player = 0, DungeonMaster = 1, Admin = 2` `[VERIFIED: QuestBoard.Domain/Enums/GroupRole.cs:5-7]`.
- `IUserService.GetEffectiveGroupRoleAsync(ClaimsPrincipal, groupId)` takes a principal and short-circuits SuperAdmin to Admin, so it cannot be used for the DM (a user id, not a principal). Use the raw read above (see Open Question 1 for a SuperAdmin who is the DM).
- No role map for the whole board is needed for D-04/D-05: kept versus dropped depends only on membership.

### Pattern 4: Waitlist promotion applied in memory (answers question 1b)
`QuestService.PromoteNextWaitlistedPlayerIfSeatFreedAsync` is `private`, promotes one candidate through `GetTopWaitlistedCandidateAsync` (a filtered read), saves per candidate (`playerSignupRepository.UpdateAsync`), then re-reads the quest to enqueue mail - none of that is usable after a board change `[VERIFIED: QuestService.cs:317-345]`. Its rule is:
- finalized proposed date = the quest's proposed date whose `.Date.Date == FinalizedDate.Value.Date` (null means no promotion) `[VERIFIED: QuestService.cs:306-310, Details.cshtml:85-87]`;
- candidates = same quest, `!IsSelected`, `SignupRole == Player`, vote for that date is Yes or Maybe `[VERIFIED: PlayerSignupRepository.cs:91-110]`;
- order = vote priority descending (`WaitlistOrdering.VotePriority`, Yes above Maybe above No/none) then `LastVoteChangeTime ?? SignupTime` ascending;
- the freeing signup is never promoted (moot here: a dropped signup is not among the kept).

Apply it on the **kept** domain signups in the Domain service: `kept.Where(!IsSelected && Role == Player && vote is Yes/Maybe on finalized date).OrderWaitlist(finalizedPd.Id).Take(seatsFreed)`. `WaitlistOrdering.OrderWaitlist` is the existing public sort used by Details and Manage `[VERIFIED: WaitlistOrdering.cs:15, Details.cshtml:89]`; it does not filter, so the Yes/Maybe filter is the three lines to keep in sync. `seatsFreed` = number of dropped signups with `IsSelected && Role == Player`. The existing rule ignores `TotalPlayerCount` and so does this one. Pin the in-memory selection to the repository rule with a **parity test** (seed one dataset, run `GetTopWaitlistedCandidateAsync` repeatedly versus the in-memory selection, assert the same order). Open quests (not finalized) get no promotion; the preview row "Seats" renders only for finalized quests.

Only promoted signups with an email and `EmailConfirmed` get mail (selection is never gated on email, only the send) `[VERIFIED: QuestService.cs:327-329]`.

### Pattern 5: Campaign fan-out parity (answers question 4)
`AddWithCampaignFanOutAsync` writes, per distinct member id, `new EventSignupEntity { UserId = memberId, Availability = (int)VoteType.Yes }` and nothing else: `UpdatedAt` stays null (the "automatic row" marker, `HasAnswered => UpdatedAt != null`), `CreatedAt` takes the entity initializer, `EventId` comes from the relationship `[VERIFIED: EventRepository.cs:50-63, EventSignup.cs HasAnswered]`. Reproduce exactly that: add to `eventEntity.Signups` the same object shape for each target member (any role) whose `UserId` is not in the loaded rows, `Distinct()`ed because the (EventId, UserId) unique index is not enforced by InMemory. Kept rows are left byte-identical, so their `UpdatedAt`/`CreatedAt` (the per-reader feed stamp `AnswerWrittenAt = UpdatedAt ?? CreatedAt`) do not move. Only fan out when the target board type is Campaign; a One-Shot target gets no fill-in.

### Pattern 6: Eligibility (answers question 8)
- Quest: refuse when `IsClosed` (check first), then when `quest.HasBeenPlayed(boardClock.Today)`. `HasBeenPlayed` is `quest is { IsFinalized: true, FinalizedDate: { } finalizedDate } && DateOnly.FromDateTime(finalizedDate) <= boardToday.AddDays(-1)` `[VERIFIED: QuestExtensions.cs]`. Open quests are always eligible; a finalized quest dated today or later is eligible.
- Event: refuse when `SeriesId != null` (checked first, matching the UI-SPEC reason order), then when `Date < boardClock.Today` (`DateOnly`). Cancelled events move. A detached occurrence has both `SeriesId` and `SeriesSlotIndex` nulled `[VERIFIED: EventSeriesRepository.cs:178-182]`.
- Put the rule in one pure `MoveEligibility` class taking `boardToday`, used by the Details control (disabled state and reason) and by the Move service (GET redirect, POST refusal). New code must read "today" only from `IBoardClock`/a parameter. `AmbientClockSeamTests` bans `DateTime.Today/Now/UtcNow` in 24 guarded paths including `QuestController.cs`, `EventsController.cs`, `QuestService.cs`, `QuestRepository.cs`, `Quest/Details.cshtml`, `Quest/Details.Mobile.cshtml`, and requires the two quest views to keep the token `ViewBag.BoardToday` `[VERIFIED: AmbientClockSeamTests.cs:19-95]`. Do not add ambient reads to those files while adding the control.

### Pattern 7: Preview stamp and the re-show rule
Stamp = hex SHA-256 of a canonical string: kind, entry id, target id, sorted kept ids, sorted dropped ids, sorted promoted ids, sorted cut-link quest ids, sorted Campaign-add user ids, and the refusal reason if any. GET renders it in a hidden field; POST recomputes the plan, and if the stamp differs (or the repository returns `Conflict`) it re-renders the Move page with HTTP 200 and the UI-SPEC notice, never moving. The stamp is a change detector only; security rests on the server recompute (D-15), so no HMAC key is needed.

### Pattern 8: Mail (answers question 6)
- Dispatcher seam: `IQuestEmailDispatcher` (Domain) with `HangfireQuestEmailDispatcher` (Service) enqueueing `jobClient.Enqueue<TJob>(j => j.ExecuteAsync(...))`, and `NullQuestEmailDispatcher` registered when the environment is `Testing` `[VERIFIED: Program.cs:260-295]`. Jobs run through `HangfireJobHelper.RunInScopeAsync(scopeFactory, groupId, ...)`, which sets the concrete `ActiveGroupContextService.SetGroupId` before any repository call `[VERIFIED: HangfireJobHelper.cs]`. `QuestWaitlistPromotedEmailJob` renders from its arguments and reads no repository; `groupId` is purely the scope, so passing the **target** id satisfies D-06.
- Add `EnqueueQuestMovedEmail(int questId, int groupId, QuestMovedAudience audience, string recipientEmail, string recipientName, string questTitle, string dmName, string boardName, DateTime? sessionDate)` plus a `QuestMovedEmailJob` and `QuestMoved.razor` with `Audience`. Recommended: **one job per recipient** (singular email), like the waitlist job's "can never broadcast" design. The array style of `QuestDateChangedEmailJob` re-sends to earlier recipients on a Hangfire retry (5 attempts) `[VERIFIED: QuestDateChangedEmailJob.cs, Program.cs:284]`; at 5-10 recipients per move per-recipient jobs cost nothing and make retries idempotent.
- Waitlist after a move: add `EnqueueWaitlistPromotedAfterMoveEmail(..., string movedToBoardName)` (or an overload) and a job method that passes `MovedToBoardName` to the template. Do **not** add an optional parameter to the existing `ExecuteAsync` or interface method. C# forbids optional arguments in expression trees (CS0854: "An expression tree may not contain a call or invocation that uses optional arguments") `[CITED: learn.microsoft.com/dotnet/csharp/language-reference/compiler-messages/expression-tree-restrictions]`, and the dispatcher calls are Hangfire expression trees. Changing the existing job signature also risks jobs already queued in production storage failing to bind `[ASSUMED]` (Hangfire stores type, method and parameter types), so add a new method and leave the old one alone. The promoted player gets this mail only (operator decision in the UI-SPEC), so exclude promoted signup ids from the Quest Moved recipients.
- Recipient rules: only `!string.IsNullOrEmpty(Player.Email) && Player.EmailConfirmed`; skip the acting super admin; dedupe by user id (the DM and a signup could coincide). Kept = every kept signup that was not promoted plus the DM. Removed = every dropped signup, any role.
- Enqueue **after** `SaveChangesAsync` returns, each enqueue in a try/catch that logs: the move is committed and cannot be un-done, so an enqueue failure must not turn into a "move failed" toast.
- Mail data is read from the AsNoTracking snapshot taken before the move (Player Name/Email/EmailConfirmed, DM name, quest title, `FinalizedDate`, target board name), so no post-move read is needed.
- `SessionDate` is a wall-clock `DateTime?` passed straight through; the template formats it as `ToString("dddd, MMMM d")` and never converts it (UI-SPEC; project rule in `.claude/architecture.md`).
- The existing in-flight `QuestFinalizedEmailJob` reads the quest under the group it was enqueued with and writes `FinalizedEmailSentForDate` through `FindAsync`; a job that was queued for the source board and runs after a move finds nothing and no-ops its dedupe write. Low risk, mention only.

### Anti-Patterns to Avoid
- **Splitting the move across several saves or repository helper calls.** Each helper saves on its own; a failure between saves leaves a half-moved quest (D-20).
- **`ExecuteUpdate`, `.Update(`, `.UpdateRange(`, `.Attach(`, `.AttachRange(`, `.Entry(`, `.TrackGraph(`, `ExecuteSql`, `.State =`** anywhere in `QuestBoard.Repository`, `QuestBoard.Domain` or `QuestBoard.Service` `.cs` files: `FeedRevisionWriteSeamTests` fails the build on any of these shapes, with an empty allowlist `[VERIFIED: FeedRevisionWriteSeamTests.cs:12, 46-59]`. Write `.UpdateAsync(` freely (does not match `.Update(`), but use `Remove`, `Add` and property assignment.
- **Setting `FeedRevision`/`FeedRevisedAt`.** The stamper puts any caller-written value back before deciding anything.
- **Reading membership through a new `IgnoreQueryFilters()` call.** Not needed; would require arguing a new file onto the allowlist.
- **Trusting the posted target or stamp.** Re-read the set, the target members and eligibility on POST.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Raising the calendar revision on a board change | A manual `FeedRevision++` or a feed bump call | The tracked `GroupId` assignment; `FeedRevisionStamper` does it | `GroupId` is in both `EventFeedFields` and `QuestFeedFields`; the stamper resets any hand-written revision |
| Retry on a revision race | A custom concurrency loop | `QuestBoardContext.SaveChangesAsync` retry (adopt stored revision, up to 5 attempts) | Already wraps every save |
| Linked-set membership | A new query over `Groups`/`LinkedBoardSets` | `ILinkedBoardSetRepository.GetLinkedBoardsForViewerAsync` | Already fresh, type-checked, excludes the active board, returns `ViewerRole` |
| Waitlist order | A second sort implementation | `WaitlistOrdering.OrderWaitlist` | Same ordering used on Details and Manage |
| Automatic Campaign rows | A different row shape | The exact `EventSignupEntity { UserId, Availability = Yes }` object | Row shape distinguishes automatic from real answers (`UpdatedAt == null`) |
| Date rendering in the Move page and emails | `.ToString()` on a `DateTime` in a view | `Html.WallClock(value, style)` in views; the in-template `ToString("dddd, MMMM d")` pattern in emails | Project rule (`.claude/ui-guidelines.md`); a game night must never be zone-converted |
| Email rendering | Hand-built HTML | `IEmailRenderService.RenderAsync<T>` + a `.razor` template under `Components/Emails` | Existing pipeline; `EmailPreviewController` lists templates |
| Anti-forgery | Manual token handling | `[ValidateAntiForgeryToken]` on every POST | `AntiForgeryTokenCoverageTests` reflects over all controllers and fails on a POST without it |

**Key insight:** every hard part of this phase already has a seam; the risk is not missing machinery but stepping around a seam (a second save, a bypassed filter, an ambient clock read, a new route in the closed registry).

## Runtime State Inventory

Not a rename or migration phase, but the move re-homes live rows, so the five categories were answered explicitly.

| Category | Items Found | Action Required |
|----------|-------------|------------------|
| Stored data | `Quests.GroupId` / `Events.GroupId` change; `PlayerSignups`, `PlayerDateVotes`, `ProposedDates`, `ReminderLogs`, `EventSignups` follow it through filters (no column). `PlayerSignups.CharacterId` set null on kept rows. `Quests.OriginalQuestId` nulled on the moved quest and on a source-board follow-up. No stored "moved from" field (D-19) | Code edit only; no migration. Data changes are performed by the move itself |
| Live service config | Hangfire storage may hold jobs queued for the source board id (finalized email, session reminders). Calendar subscribers' phones hold the old `SUMMARY` and `SEQUENCE` | None for code. Accept per D-18; in-flight finalized-email job no-ops its dedupe write (Pattern 8) |
| OS-registered state | None. Recurring Hangfire jobs (`DailyReminderJob`, `RecurringOccurrenceTopUpJob`) read board data at run time and carry no board id for a quest; verified by listing `QuestBoard.Service/Jobs` | None |
| Secrets and env vars | None. The Hangfire dashboard and relay limit are unchanged | None |
| Build artifacts / installed packages | None. No schema change, so no migration and no stale model snapshot | None |

## Common Pitfalls

### Pitfall 1: Reading after the board changed
**What goes wrong:** A filtered read after `GroupId` changes in the database returns nothing (signups, votes, follow-ups, `GetQuestWithDetailsAsync` all null), so the promotion or chain cut silently does nothing.
**Why it happens:** Filters are `Quest.GroupId == ActiveGroupId` and the session still holds the source board.
**How to avoid:** All reads before any mutation; `GroupId` assigned last; no repository helper between assignment and save (Pattern 2).
**Warning signs:** A test where promotion or a cut link "works" only when the dropped signup was selected after the save.

### Pitfall 2: Orphan votes and cascade differences between InMemory and SQL Server
**What goes wrong:** Removing a `PlayerSignupEntity` without loading its `DateVotes` leaves the votes in InMemory (confirmed by a spike: count stayed 1) while SQL Server would cascade, so D-21 #5 passes in production and fails in tests, or the reverse for a hand-written delete.
**How to avoid:** `Include(DateVotes)` and `RemoveRange(signup.DateVotes)` explicitly, then assert the vote count in a repository unit test.

### Pitfall 3: The retry loop is only for revision races
**What goes wrong:** `QuestBoardContext.SaveChangesAsync` adopts the stored revision and retries only when every conflicting entry is a modified or deleted `EventEntity`/`QuestEntity` `[VERIFIED: FeedRevisionStamper.IsRecoverable]`. A conflict on a deleted `PlayerSignup` (a player withdrew between read and save) is rethrown as `DbUpdateConcurrencyException`.
**How to avoid:** The repository catches `DbUpdateConcurrencyException`/`DbUpdateException` and returns `MoveWriteResult.Conflict` (controller re-shows the page); any other exception is logged and shown as the "move did not complete" toast. Do not reuse the context after a failed save inside the request; redirect. A revision-only race on the quest itself retries transparently and re-applies the whole graph, which is safe because a failed `SaveChanges` rolls back as a unit `[CITED: learn.microsoft.com/ef/core/saving/transactions]`. Whether `GetDatabaseValues` in the adopt step ignores the source filter after a concurrent board change is not verified `[ASSUMED]`; the only case is two super admins moving the same quest at the same instant, which is outside D-21.

### Pitfall 4: Chain links and the unique index
**What goes wrong:** Only clearing the moved quest's `OriginalQuestId` leaves a source-board follow-up pointing at it. The follow-up is hidden by the board filter on the quest, but the one-to-one FK (`HasOne(OriginalQuest).WithOne(FollowUpQuest).HasForeignKey<QuestEntity>(q => q.OriginalQuestId)` `[VERIFIED: QuestBoardContext.cs:267-272]`) means a DM on the source board could later create a second follow-up for the original and hit `IX_Quests_OriginalQuestId`.
**How to avoid:** Clear both ends in the same save. Both ends are loadable under the source filter because follow-ups are created on the original's board (`GroupId = original.GroupId`, QuestService follow-up creation); a link that already crosses boards cannot be produced by current code, but clear the scalar regardless of whether the navigation loads. InMemory does not enforce the unique index, so assert the stored `OriginalQuestId` is null (D-21 #9). A follow-up whose only change is `OriginalQuestId` is not feed-visible, so its revision stays 1 (spike-confirmed).

### Pitfall 5: The integration factory discards dispatcher calls
**What goes wrong:** In the `Testing` environment `IQuestEmailDispatcher` is `NullQuestEmailDispatcher`, so `factory.JobClient.EnqueuedJobs` (a `CapturingBackgroundJobClient`) never sees quest mail. D-21 #7 and #12 would assert on an empty bag.
**How to avoid:** A new factory subclass (as `CrossBoardWebApplicationFactory` does) that re-registers `services.AddScoped<IQuestEmailDispatcher, HangfireQuestEmailDispatcher>()` in `ConfigureTestServices`; `HangfireQuestEmailDispatcher` only needs `IBackgroundJobClient`, which the base already registers as the capturing client. Then assert `EnqueuedJobs` by `Job.Type`, `Job.Method.Name` and `Job.Args` (the `groupId` argument equals the target board). Do not change `WebApplicationFactoryBase` itself (other suites count jobs).

### Pitfall 6: Optional parameters and expression trees
**What goes wrong:** Adding `string? movedToBoardName = null` to `EnqueueWaitlistPromotedEmail` or the job's `ExecuteAsync` does not compile at the Hangfire call site (CS0854). `?.`, collection expressions and named arguments are also illegal inside those lambdas.
**How to avoid:** New explicit method and job entry point (Pattern 8); pass every argument explicitly.

### Pitfall 7: Tampered or stale target
**What goes wrong:** Trusting `targetBoardId` from the query string or form, or a cached set.
**How to avoid:** Re-run `GetLinkedBoardsForViewerAsync` on every GET and POST; unknown or self target renders the UI-SPEC "That board is not part of this linked set" (GET) or the "not linked" toast (POST) and writes nothing.

### Pitfall 8: Guard tests that the UI-SPEC wording would break
**What goes wrong (a):** `EmailPreviewController_StillContainsFiveCosmeticDateTimeTodayUses` pins exactly five `DateTime.Today` occurrences in `EmailPreviewController.cs` `[VERIFIED: AmbientClockSeamTests.cs:379-388, quoted "Should().Be(5"]`. The UI-SPEC says the four new preview actions use `DateTime.Today.AddDays(7)`; that makes nine and fails the test.
**How to avoid:** Use a fixed sample constant in the new preview actions (for example a `private static readonly DateTime SampleSessionDate = new(2031, 6, 14, 19, 0, 0)`), leaving the five existing uses untouched. This deviates from the UI-SPEC sample-date note only cosmetically.
**What goes wrong (b):** `CrossBoardLinkRegistryTests` pins 18 routes; adding Move routes to the registry fails it (and is wrong anyway, Pattern 1).
**What goes wrong (c):** `LinkedBoardStaticGuardTests.FeatureSourceFiles_CarryNoPlanningOrTrackingReference` scans a closed file list (including `QuestService.cs`, `QuestController.cs`, `IQuestRepository.cs`, `QuestRepository.cs`, `EventRepository.cs`, `ServiceExtensions.cs`) for `D-01`..`D-20`, `Phase 91/92`, `CONTEXT.md`, `RESEARCH.md`, `UI-SPEC` `[VERIFIED: LinkedBoardStaticGuardTests.cs:272-310]`. CLAUDE.md forbids those references in all source anyway; write plain-language comments.

### Pitfall 9: Eligibility edge cases
A finalized quest whose `FinalizedDate` matches no proposed date has no waitlist date, so no promotion (seats stay open); the "Seats" row copy for "nobody is waiting" covers it. An event whose `SeriesId` points at a series that was deleted independently is still refused (conservative). Closed is checked before played so a closed-and-played quest shows the Quest Log reason.

### Pitfall 10: Event roster read is tracked
`IEventSignupRepository.GetRosterForEventAsync` does `Include(es => es.User)` with no `AsNoTracking`, so calling it first leaves tracked signup entities in the request's context `[VERIFIED: EventSignupRepository.cs:81-93]`. That is harmless because the move repository's tracked load returns the same instances in the same request, but do not mutate them outside the move repository, and do not rely on it being untracked. `GetQuestWithManageDetailsAsync` is likewise tracked; use `GetQuestWithDetailsAsync` (AsNoTracking) for the snapshot.

### Pitfall 11: Toast data in TempData
Three new keys go in `TempDataKeys.cs` (message, entry URL, target board name), a new block in `_Toasts.cshtml` after the board-switch block, rendered only when the message key is set. TempData is cookie-backed by default, which is fine for a sentence and a relative URL. The URL is `Url.Action("Details", "Quest"/"Events", new { id })`, set only when `LinkedBoard.ViewerIsMember`; it passes through the Phase 87 switch because both routes are in the registry.

## Code Examples

Patterns drawn from the verified sources above; adapt names to the plan.

### Repository unit of work (shape, not final code)
```csharp
// Source: pattern verified by an InMemory spike this session (1 save, revision 1 -> 2,
// follow-up untouched at revision 1, votes removed when loaded, quest hidden on the source board afterwards).
public async Task<MoveWriteResult> ApplyQuestMoveAsync(QuestMovePlan plan, CancellationToken token = default)
{
    var quest = await dbContext.Quests
        .Include(q => q.PlayerSignups).ThenInclude(ps => ps.DateVotes)
        .FirstOrDefaultAsync(q => q.Id == plan.QuestId, token);          // source filter still active
    if (quest == null) return MoveWriteResult.NotFound;

    var followUps = await dbContext.Quests
        .Where(q => q.OriginalQuestId == plan.QuestId)
        .ToListAsync(token);                                             // before GroupId changes

    var loadedSignups = quest.PlayerSignups.Select(s => s.Id).OrderBy(i => i);
    var expectedSignups = plan.KeptSignupIds.Concat(plan.DroppedSignupIds).OrderBy(i => i);
    if (!loadedSignups.SequenceEqual(expectedSignups)
        || !followUps.Select(f => f.Id).OrderBy(i => i).SequenceEqual(plan.FollowUpQuestIdsToUnlink.OrderBy(i => i)))
        return MoveWriteResult.Conflict;                                 // nothing mutated yet

    foreach (var signup in quest.PlayerSignups.Where(s => plan.DroppedSignupIds.Contains(s.Id)).ToList())
    {
        dbContext.Set<PlayerDateVoteEntity>().RemoveRange(signup.DateVotes); // InMemory does not cascade untracked votes
        dbContext.PlayerSignups.Remove(signup);
    }
    foreach (var signup in quest.PlayerSignups.Where(s => plan.KeptSignupIds.Contains(s.Id)))
    {
        signup.CharacterId = null;
        if (plan.PromotedSignupIds.Contains(signup.Id)) signup.IsSelected = true;
    }
    if (plan.ClearOriginalLink) quest.OriginalQuestId = null;
    foreach (var followUp in followUps) followUp.OriginalQuestId = null;

    quest.GroupId = plan.TargetGroupId;                                  // last

    try { await dbContext.SaveChangesAsync(token); }                     // the only save
    catch (DbUpdateConcurrencyException) { return MoveWriteResult.Conflict; }
    return MoveWriteResult.Success;
}
```
Note: `QuestBoardContext` declares no `DbSet` for `PlayerDateVoteEntity` or `ProposedDateEntity` `[VERIFIED: QuestBoardContext.cs:16-62]`; reach them with `dbContext.Set<PlayerDateVoteEntity>()`, as `TestDataHelper` does for proposed dates.

### Domain plan for a quest (shape)
```csharp
// kept = signups whose player is a member of the target; dropped = the rest
var memberIds = (await userRepository.GetAllGroupMembers(targetId, token)).Select(u => u.Id).ToHashSet();
var kept = quest.PlayerSignups.Where(s => memberIds.Contains(s.Player.Id)).ToList();
var dropped = quest.PlayerSignups.Except(kept).ToList();
var seatsFreed = dropped.Count(s => s.IsSelected && s.Role == SignupRole.Player);

var finalizedPd = quest is { IsFinalized: true, FinalizedDate: { } fd }
    ? quest.ProposedDates.FirstOrDefault(pd => pd.Date.Date == fd.Date) : null;
var promoted = finalizedPd == null ? [] : kept
    .Where(s => !s.IsSelected && s.Role == SignupRole.Player
        && s.DateVotes.FirstOrDefault(v => v.ProposedDateId == finalizedPd.Id)?.Vote is VoteType.Yes or VoteType.Maybe)
    .OrderWaitlist(finalizedPd.Id).Take(seatsFreed).ToList();
```

### Event Campaign rows (parity with fan-out)
```csharp
// Source: EventRepository.AddWithCampaignFanOutAsync writes exactly this shape per member.
foreach (var userId in campaignAddUserIds.Distinct())
{
    eventEntity.Signups.Add(new EventSignupEntity { UserId = userId, Availability = (int)VoteType.Yes });
}
```

### Test dispatcher for D-21 #7 and #12
```csharp
public class MoveWebApplicationFactory : WebApplicationFactoryBase
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureTestServices(services =>
            services.AddScoped<IQuestEmailDispatcher, HangfireQuestEmailDispatcher>()); // feeds the capturing client
    }
}
// assert: factory.JobClient.EnqueuedJobs.Where(j => j.Type == typeof(QuestWaitlistPromotedEmailJob))
//         ... j.Args[1] is the groupId argument (the job's second parameter) == target board id
```

## FeedRevisionStamper findings (answers question 2)

- `GroupId` is in both field lists: `EventFeedFields` = `Title, Date, StartTime, CancelledAt, GroupId`; `QuestFeedFields` = `Title, FinalizedDate, IsFinalized, GroupId` `[VERIFIED: FeedRevisionStamper.cs:23-39, quoted]`.
- `StampChangedRow` compares original and current values per feed field; any difference sets `FeedRevision = original + 1` and `FeedRevisedAt = max(now, original)` **once per row per save**. Changing several feed fields (for example `GroupId` and `FinalizedDate`) is still +1 (`Event_ChangingTwoFeedFieldsInOneSave_RaisesTheRevisionByExactlyOne`). A row with only non-feed changes (follow-up `OriginalQuestId`, promoted signups) keeps its revision and stamp. Already pinned for `GroupId`: `Event_ChangingAFeedVisibleField_...` and `Quest_ChangingAFeedVisibleField_...` have `InlineData` for `GroupId` `[VERIFIED: FeedRevisionStamperTests.cs:179, 220]`.
- Dropping `EventSignup` rows or adding Campaign rows does not touch the event revision (the stamper looks only at `EventEntity`/`QuestEntity`). Per-reader stamps are `AnswerWrittenAt = UpdatedAt ?? CreatedAt` on each signup row (`GetFeedRowsForUserAsync`): kept rows are unchanged so their stamps do not move; added Campaign rows are stamped by their own `CreatedAt`; dropped rows disappear from non-members' feeds.
- The concurrency token is `FeedRevision` on both entities; every UPDATE or DELETE of a quest/event row (including the follow-up whose only change is its link) carries `WHERE FeedRevision = original`. Retry covers a pure revision race (up to 5 attempts, whole graph re-applied) and is rethrown for anything else (Pitfall 3).
- No stamper change is needed or allowed (D-18).

**Guard tests the plan must keep green (names verified by reading):**
- `FeedRevisionWriteSeamTests` (`NoProductionSource_WritesRowsPastTheChangeTracker`, `TheContext_OverridesBothSaveMethods_AndRunsTheStamperInEach`, the detector self-checks)
- `CrossBoardIgnoreQueryFiltersSeamTests` (closed allowlist of five files; this phase adds none)
- `AmbientClockSeamTests` (24 guarded paths, `BoardClockConsumerPaths`, the `ViewBag.BoardToday` view tokens, and `EmailPreviewController_StillContainsFiveCosmeticDateTimeTodayUses`)
- `CrossBoardLinkRegistryTests` (18 routes) and `LinkedBoardStaticGuardTests` (`CrossBoardLink` prefix set, planning references in the closed file list)
- `FeedRevisionStamperTests`, `CalendarFeedEventRevisionTests`, `CalendarFeedQuestRevisionTests`, `CalendarFeedRevisionInputTests`, `CalendarFeedWriterTests`, `CalendarSubscriptionQuestRecheckTests` (feed behaviour must not change)
- `AntiForgeryTokenCoverageTests` (every `[HttpPost]` needs `[ValidateAntiForgeryToken]`)
- `MobileViewsTests`/`MobileCssTests`/`MobileViewLocationExpanderTests` and `QuestDetailsMobileCharacterControlTests` (Details twins keep rendering)
- `QuestServiceTests`, `EmailConfirmationJobGuardTests`, `QuestCompletionBoardClockTests` (substitute `IQuestEmailDispatcher`; adding interface methods does not break NSubstitute fakes, but any hand-written implementer must be updated: only `HangfireQuestEmailDispatcher` and `NullQuestEmailDispatcher` implement it today)

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| Per-service multi-save workflows | One tracked graph and one `SaveChangesAsync` (atomic by default) | EF Core standing behaviour | No explicit transaction; no `IsRelational()` guard needed |
| Array-recipient email jobs | One job per recipient (singular email) | Waitlist job precedent in this repo | Retries cannot duplicate earlier recipients |

**Deprecated/outdated:** none relevant.

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|---------------|
| A1 | Changing an existing Hangfire job method's signature can make jobs already queued in production storage fail to bind (Hangfire stores type, method, parameter types) | Pattern 8 | Low: the plan adds a new method instead, so nothing in storage changes either way |
| A2 | `DbUpdateConcurrencyException.Entries[i].GetDatabaseValues()` in the stamper's adopt step ignores the source query filter after a concurrent board change | Pitfall 3 | Low: only two super admins moving the same entry at the same instant |
| A3 | Hangfire's Newtonsoft JSON round-trips a nullable `DateTime` (Kind Unspecified) and an enum argument unchanged | Pattern 8 (Quest Moved args) | Medium: if the audience enum or the wall-clock date shifts, emails show the wrong text. Mitigate with a render test, or pass the audience as a string and the date as `DateTime?`; the existing jobs already pass `DateTime` finalized dates this way |
| A4 | Campaign-only fan-out is the right reading of "On a Campaign board" for a One-Shot target (no fill-in rows) | Pattern 5 | Low: matches the UI-SPEC row, which shows only for Campaign targets |
| A5 | A SuperAdmin who is the quest's DM with no `UserGroups` row on the target should be refused by D-03 (raw `UserGroups` read) | Open Question 1 | Low-medium: operator may prefer treating a SuperAdmin DM as an Admin everywhere |

## Open Questions

1. **A quest whose DM is a SuperAdmin with no membership row on the target board.**
   - What we know: D-03 says "read fresh from `UserGroups`"; the DM/Admin authorization handlers short-circuit SuperAdmin without a row, but D-03 names `UserGroups` explicitly and there is no principal for the DM at move time.
   - What's unclear: whether such a quest should be refused (raw rule) or accepted (SuperAdmin counts as Admin, Phase 91 D-20).
   - Recommendation: implement the raw `UserGroups` rule (refuse), because a DM with no membership row cannot open the target board (Phase 87 resolver has no SuperAdmin branch), which is the exact outcome D-03 wants to prevent. Mention it in the plan so the operator can overrule.
   - **RESOLVED by the operator, 2026-10-18: allow.** A SuperAdmin DM counts as Admin on any target board, per the effective-role rule (see the CONTEXT.md D-03 addendum). There is no principal for the DM at move time, so the check has to read the DM user's Identity `SuperAdmin` role (for example through `UserManager.IsInRoleAsync` on the DM's user, or an existing user-role query). If the user is a SuperAdmin, the rule passes; otherwise the raw `UserGroups` role on the target must be DungeonMaster or Admin. This recommendation is superseded.

2. **Which board the toast "Open it on {Board}" link needs when the actor is a member.**
   - What we know: `LinkedBoard.ViewerIsMember` is already on the set read; the Details routes are in the registry, so the Phase 87 switch handles the click.
   - Nothing unclear; recorded so the planner does not add a second membership query.

3. **Return target for an event move when the board has several lists.** The UI-SPEC pins `Events/Index`; no further research needed.

## Environment Availability

| Dependency | Required By | Available | Version | Fallback |
|------------|------------|-----------|---------|----------|
| .NET SDK | build and test | yes | 10.0.112 | — |
| SQL Server `localhost:1433` | running the app by hand only | not probed (project rule: never provision or start it) | — | Unit and integration tests use EF Core InMemory and need no database `[CITED: CLAUDE.md]` |
| EF Core InMemory 10.0.9 | all new automated tests | yes (package referenced) | 10.0.9 | — |

**Missing dependencies with no fallback:** none for automated verification.
**Missing dependencies with fallback:** a real SQL Server is needed only for the manual 390px/phone and cascade confirmation the UI-SPEC backstops mention; if unreachable, stop and tell the user (CLAUDE.md), do not provision it.

## Validation Architecture

### Test Framework
| Property | Value |
|----------|-------|
| Framework | xunit.v3 3.2.2 + FluentAssertions 8.10.0 + NSubstitute 5.3.0, .NET 10 `[VERIFIED: QuestBoard.UnitTests.csproj]` |
| Config file | `QuestBoard.IntegrationTests/xunit.runner.json` (`parallelizeAssembly: false`, `parallelizeTestCollections: false`); unit tests have none |
| Quick run command | `dotnet test QuestBoard.UnitTests --filter "FullyQualifiedName~BoardMove"` |
| Full suite command | `dotnet test` (from the repo root, runs both test projects) |

### Phase Requirements to Test Map
Locations: **U** = `QuestBoard.UnitTests` (Domain service with NSubstitute and `FakeBoardClock`; Repository with InMemory and a `Store`/`MutableGroupContext` helper as in `FeedRevisionStamperTests`), **I** = `QuestBoard.IntegrationTests` (HTTP through a `MoveWebApplicationFactory`).

| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| D-21.1 | Admin/DM/Player (incl. Admin of both boards) refused on GET and POST; data unchanged. Policy refusal is a 302 to AccessDenied or 403, so assert `BeOneOf(...)` like `LinkedBoardsPlatformIntegrationTests` | I | `dotnet test QuestBoard.IntegrationTests --filter "FullyQualifiedName~BoardMoveAuthorization"` | Wave 0 |
| D-21.2 | Target outside set / self refused on direct POST; board in no set or set of one: Details has no control (assert absence of the control markup, byte-for-byte on a non-linked board) | U (service) + I | `--filter "FullyQualifiedName~BoardMoveTarget"` | Wave 0 |
| D-21.3 | Quest/event visible on B, 404 on A (flip `TestGroupContext.ActiveGroupId`), `FeedRevision` rose (read with `IgnoreQueryFilters()`, which test code may use) | I + U (repository) | `--filter "FullyQualifiedName~BoardMoveApply"` | Wave 0 |
| D-21.4 | Kept signup keeps role, `IsSelected`, `SignupTime`, `LastVoteChangeTime`, votes; `CharacterId == null` | U (repository) + I | same | Wave 0 |
| D-21.5 | Non-member signup (every role) and its votes gone | U (repository) + I | same | Wave 0 |
| D-21.6 | DM with no row or `Player` role on target refused (GET shows refusal, POST toast, nothing written) | U (service) + I | `--filter "FullyQualifiedName~BoardMoveDmRule"` | Wave 0 |
| D-21.7 | Selected non-member dropped on a finalized quest promotes top kept waitlisted; enqueue carries target board id; parity with `GetTopWaitlistedCandidateAsync` | U (service plan + `Received()` on dispatcher with target id; parity test) + I (capturing client `Job.Args`) | `--filter "FullyQualifiedName~BoardMoveWaitlist"` | Wave 0 |
| D-21.8 | Series occurrence refused; one-off and detached event move | U (eligibility) + I | `--filter "FullyQualifiedName~BoardMoveEligibility"` | Wave 0 |
| D-21.9 | Both chain links stored as null; middle-of-chain loses both | U (repository) + I | `--filter "FullyQualifiedName~BoardMoveChain"` | Wave 0 |
| D-21.10 | Played/closed quest and past event refused; upcoming cancelled event moves and `CancelledAt` is still set; `FakeBoardClock` for the date edge (finalized yesterday vs today) | U + I | `--filter "FullyQualifiedName~BoardMoveEligibility"` | Wave 0 |
| D-21.11 | Campaign target: members without a row get `Availability == Yes`, `UpdatedAt == null`; non-member rows dropped; a One-Shot target adds none. Seed sets with `BoardType.Campaign` and set `TestGroupContext.BoardType` | U (repository) + I | `--filter "FullyQualifiedName~BoardMoveCampaign"` | Wave 0 |
| D-21.12 | Quest move enqueues `QuestMovedEmailJob` per kept recipient (+ DM) and per dropped player; none for unconfirmed email or the acting super admin; event move enqueues none | U (service) + I | `--filter "FullyQualifiedName~BoardMoveMail"` | Wave 0 |
| D-20 | One `SaveChangesAsync`, atomic: repository test counts `SavedChanges` events == 1 on the context; a `Conflict` outcome leaves every row untouched; static guard that `BoardMoveRepository.cs` contains exactly one `SaveChangesAsync(` call (same style as `FeedRevisionWriteSeamTests`) | U | `--filter "FullyQualifiedName~BoardMoveRepository"` | Wave 0 |
| D-18/19 | Feed/stamper files unchanged (existing tests stay green); one structured log line with ids and counts and no names or emails (assert via `CapturingLoggerProvider`, available as `factory.LogCapture`) | existing + I | `dotnet test` | existing / Wave 0 |
| UI | Move view pair renders for desktop and mobile UA (`Index.Mobile.cshtml` chosen), Details controls on `Quest/Details`, `Details.Mobile`, `Events/Details`; email templates render (`QuestMoved` three audiences, `WaitlistPromoted` byte-identical with no `MovedToBoardName`) | I (`IEmailRenderService` via `factory.Services`, as `WaitlistPromotedMarkdownRenderTests`) | `--filter "FullyQualifiedName~Emails"` | partial / Wave 0 |

**Integration harness facts (answers question 9):**
- `WebApplicationFactoryBase`: EF InMemory, `TestGroupContext` (a singleton `MutableGroupContext`, default `ActiveGroupId = 1`, `BoardType = OneShot`) registered as both `IActiveGroupContext` and `IBoardTypeResolver`; anti-forgery validation is a no-op decorator; `CapturingBackgroundJobClient JobClient` captures `Job` objects; `LogCapture` captures logs `[VERIFIED: WebApplicationFactoryBase.cs]`. `IQuestEmailDispatcher` is the no-op `NullQuestEmailDispatcher` unless a factory subclass overrides it (Pitfall 5).
- SuperAdmin client: `AuthenticationHelper.CreateAuthenticatedSuperAdminClientAsync(factory)` (Identity role `SuperAdmin`; it also seeds a `Player` membership row on board 1 whenever roles is null or non-empty, so seed or remove memberships deliberately to test "super admin not a member of target").
- Board and set seeding: copy `LinkedBoardQuestDetailsTests` (`SeedBoardAsync`, `LinkAsync` building a `LinkedBoardSetEntity` and setting `LinkedBoardSetId` and `BoardType` on the groups) and `TestDataHelper.SeedCampaignGroupAsync`. `TestDatabase.CreateContext()` uses `ActiveGroupId = null`, so reading rows back through it requires `IgnoreQueryFilters()` (allowed in test projects).
- `DisposeAsync` must reset `factory.TestGroupContext.ActiveGroupId = 1` and `BoardType = BoardType.OneShot` (D-21 note; Phase 91 tests do the same).
- Tests run serially (`parallelizeTestCollections: false`) with one factory per class (`IClassFixture`).
- **InMemory limits:** no unique-index enforcement (assert stored `OriginalQuestId`, `(EventId,UserId)` uniqueness by construction, not by an exception), no transactions (`Database.BeginTransaction` throws or is ignored; the single-`SaveChanges` design avoids needing one; atomicity on SQL Server rests on EF's default per-`SaveChanges` transaction `[CITED: learn.microsoft.com/ef/core/saving/transactions]`), no database cascade (Pitfall 2), but concurrency tokens are honoured. Failure-injection for "all or none" is therefore replaced by: single-save proof, validate-before-mutate proof (`Conflict` leaves data untouched), and a manual SQL Server spot check if the operator wants one.

### Sampling Rate
- **Per task commit:** `dotnet test QuestBoard.UnitTests --filter "FullyQualifiedName~BoardMove"` (plus the architecture guards: `--filter "FullyQualifiedName~Architecture"`)
- **Per wave merge:** `dotnet test` (full solution; integration suite included)
- **Phase gate:** full suite green before `/gsd-verify-work`; the UI-SPEC backstops (390px phone width, email render at long names) are manual checks listed in `92-UI-SPEC.md`.

### Wave 0 Gaps
- [ ] `QuestBoard.UnitTests/Repository/BoardMoveRepositoryTests.cs` — covers D-21.3/4/5/9/11 and the single-save and `Conflict` proofs
- [ ] `QuestBoard.UnitTests/Services/BoardMoveServiceTests.cs` — plan, DM rule, promotion parity, mail recipients, target rules (NSubstitute repos, `FakeBoardClock`)
- [ ] `QuestBoard.UnitTests/Services/MoveEligibilityTests.cs` — D-21.8/10 incl. board-clock edges
- [ ] `QuestBoard.UnitTests/Architecture/BoardMoveRepositorySingleSaveTests.cs` — source scan: exactly one `SaveChangesAsync(` in the move repository, no banned write shapes
- [ ] `QuestBoard.IntegrationTests/Helpers/MoveWebApplicationFactory.cs` — dispatcher override (Pitfall 5)
- [ ] `QuestBoard.IntegrationTests/Tests/BoardMove*IntegrationTests.cs` — D-21.1/2/3/6/7/8/10/11/12 over HTTP, with `DisposeAsync` reset
- [ ] `QuestBoard.IntegrationTests/Emails/QuestMovedRenderTests.cs` — three audiences, wall-clock date text, `WaitlistPromoted` byte-identical when `MovedToBoardName` is null
- [ ] No framework install needed.

## Security Domain

`security_enforcement` is not set to false in `.planning/config.json` (key absent), so this section applies.

### Applicable ASVS Categories

| ASVS Category | Applies | Standard Control |
|---------------|---------|-----------------|
| V2 Authentication | no (unchanged) | Existing Identity cookie / test scheme |
| V3 Session Management | yes (active board from session) | Active board only identifies the source; target is never taken from session; `GroupSessionMiddleware` gating unchanged |
| V4 Access Control | yes | `[Authorize(Policy = "SuperAdminOnly")]` on the controller, repeated server-side on POST; fresh set membership per request; server-side eligibility; IDOR closed because the entry is loaded through the source board's query filter (other board's id returns 404) |
| V5 Input Validation | yes | Model binding to typed ints, explicit allow-list check of `targetBoardId` against the fresh set, hidden stamp compared by recompute (never trusted), Razor encoding for every user text (titles, names) |
| V6 Cryptography | no secrets | SHA-256 stamp is a change detector only; never use it for authorization |
| V13/V14 (CSRF, config) | yes | `[ValidateAntiForgeryToken]` on POSTs (enforced by `AntiForgeryTokenCoverageTests`) |
| Logging (V7) | yes | One structured line with ids and counts; no names, emails or descriptions |

### Known Threat Patterns for ASP.NET Core MVC multi-tenant write

| Pattern | STRIDE | Standard Mitigation |
|---------|--------|---------------------|
| Board Admin/DM of both boards moves an entry | Elevation of privilege | `SuperAdminOnly` policy, not `CanManage`; D-21.1 tests Admin and DM clients |
| Posted `targetBoardId` outside the set or equal to the source | Tampering | Re-read `GetLinkedBoardsForViewerAsync` on POST; refuse unknown ids with the generic "not linked" copy |
| Posted entry id of another board's quest/event | Information disclosure / tampering | Load through the source filter; absent = 404 (same oracle-safe path as a missing id) |
| Cross-site POST | Tampering | Anti-forgery token on the POST |
| Oracle on board names to a non-member | Information disclosure | Only a SuperAdmin reaches the page; the removed-player email names the target board, which Phase 91 already shows them (accepted in D-17) |
| Over-sharing in logs or toasts | Information disclosure | Log ids and counts only; toast shows the entry title and board name the SuperAdmin already sees |
| Partially applied move after a fault | Tampering (integrity) | One `SaveChanges`, validate before mutate, `Conflict` result |
| Email to unverified addresses | Spoofing / abuse | `EmailConfirmed` gate on every recipient |

## Project Constraints (from CLAUDE.md)

- Never commit to `main`; work stays on the milestone branch `milestone/v9-rolling-improvements`. The orchestrator commits this research (executor pushes after each green wave per memory).
- Three layers Service -> Domain -> Repository; EF packages only in `QuestBoard.Repository`; AutoMapper at the two boundaries (new read models can be plain classes).
- Writes to `Events` and `Quests` must go through the change tracker (no bulk update, raw SQL, attach, hand-set state), or the feed revision does not rise and a guard test fails.
- Time: wall-clock values are never timezone-converted (`Html.WallClock`); "today" comes from `IBoardClock`, never `DateTime.Now/UtcNow/Today`; `AmbientClockSeamTests` holds a closed list; do not weaken it.
- `CalendarFeedWriter.cs`, `CalendarSubscriptionService.cs`, `FeedRevisionStamper.cs` are high-risk: no changes.
- Migrations: any schema change needs an EF migration (none expected).
- No planning references (`D-06`, `Phase 92`, `92-CONTEXT`, requirement ids, review ids) in comments, XML docs or string literals in source; commit messages may carry them.
- Before any Razor view: read `.claude/ui-guidelines.md` (card pattern, `<hr>` before buttons, filled buttons, `.Mobile.cshtml` twins, `Html.LocalTime`/`Html.WallClock`); both `.Mobile.cshtml` twins updated; no `@media`, no inline `style=""`, no new JS (UI-SPEC).
- Local DB: do not install, start or provision SQL Server; if unreachable, stop and tell the user. Tests use InMemory and need no DB.
- Linux: never set `DOTNET_GCHeapHardLimit`; `Fatal error. Internal CLR error. (0x80131506)` is a flake, re-run once.
- RIP MCP navigation protocol applies when the `rip` server is available; it was not available in this session, so navigation used Grep/Read.
- Do not cite a fixed member count for the quest board (memory note).

## Sources

### Primary (HIGH confidence)
- Source files read this session (paths in the claims above): `QuestBoardContext.cs` (filters, concurrency token, save overrides, relationship config), `FeedRevisionStamper.cs`, `FeedRevisionStamperTests.cs`, `FeedRevisionWriteSeamTests.cs`, `CrossBoardIgnoreQueryFiltersSeamTests.cs`, `AmbientClockSeamTests.cs`, `LinkedBoardStaticGuardTests.cs`, `CrossBoardLinkRegistry.cs`, `CrossBoardLinkRegistryTests.cs`, `CrossBoardDeepLinkMiddleware.cs`, `GroupSessionMiddleware.cs`, `QuestRepository.cs`, `PlayerSignupRepository.cs`, `EventRepository.cs`, `EventSignupRepository.cs`, `EventSeriesRepository.cs`, `UserRepository.cs`, `LinkedBoardSetRepository.cs`, `QuestService.cs`, `WaitlistOrdering.cs`, `QuestExtensions.cs`, `UserService.cs`, `IQuestEmailDispatcher.cs`, `HangfireQuestEmailDispatcher.cs`, `NullQuestEmailDispatcher.cs`, `QuestWaitlistPromotedEmailJob.cs`, `QuestDateChangedEmailJob.cs`, `QuestFinalizedEmailJob.cs`, `HangfireJobHelper.cs`, `WaitlistPromoted.razor`, `EmailPreviewController.cs`, `QuestController.cs`, `EventsController.cs`, `SeriesController.cs`, views `Quest/Details(.Mobile).cshtml`, `Events/Details.cshtml`, `_Toasts.cshtml`, `TempDataKeys.cs`, `Program.cs`, `WebApplicationFactoryBase.cs`, `CrossBoardWebApplicationFactory.cs`, `MutableGroupContext.cs`, `AuthenticationHelper.cs`, `TestDataHelper.cs`, `TestDatabase.cs`, `LinkedBoardQuestDetailsTests.cs`, `LinkedBoardsPlatformIntegrationTests.cs`, `AntiForgeryTokenCoverageTests.cs`, enums `GroupRole`, `SignupRole`, `VoteType`, `BoardType`.
- Throwaway InMemory spike (written, run, deleted; working tree clean): one-save quest move, revision, follow-up revision, cascade with loaded votes, orphan votes without Include, filter visibility before and after.
- `learn.microsoft.com/en-us/ef/core/saving/transactions` — `SaveChanges` is atomic by default.
- `learn.microsoft.com/en-us/dotnet/csharp/language-reference/compiler-messages/expression-tree-restrictions` — CS0854 optional arguments in expression trees.

### Secondary (MEDIUM confidence)
- none

### Tertiary (LOW confidence)
- Items tagged `[ASSUMED]` in the Assumptions Log (Hangfire serialization of the new job arguments, adopt-step filter behaviour, in-flight job binding).

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH - no new packages; versions read from csproj.
- Architecture (write order, stamper, filters, membership reads): HIGH - read in source and confirmed by a spike.
- Mail pipeline: MEDIUM-HIGH - structure verified; Hangfire argument serialization of the new job is assumed (A3), mitigated by a render/enqueue test.
- Pitfalls: HIGH - each is backed by a read file or a failing-then-explained spike result.
- Validation architecture: HIGH - harness, factories and limits read directly.

**Research date:** 2026-10-18
**Valid until:** 2026-11-17 (stable internals; re-check if `QuestBoardContext`, `FeedRevisionStamper` or the email dispatcher change before planning)
