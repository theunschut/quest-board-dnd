# Phase 92: Move a Quest or Event to a Linked Board - Context

**Gathered:** 2026-10-17
**Status:** Ready for planning

<domain>
## Phase Boundary

A super admin moves one existing quest or one-off event from its board to another board in the
same linked set, from that entry's Details page, through a dedicated Move page that shows what the
move will do on the chosen board before it is confirmed. After the move the entry lives on the
target board as if it had been created there: its people are reconciled against the target board's
membership, nothing on it points back at the source board, and the source board no longer shows it.
Entries on a board that is in no set, or in a set of one, offer no move.

Out of this phase: moving a whole event series, moving a follow-up chain as a unit, moving quests
that have already been played or closed, any change to the calendar feed's code, and any stored
"moved from" history on the entry.

</domain>

<decisions>
## Implementation Decisions

### Who may move, and to where

- **D-01: Super admin only, enforced on the server on both GET and POST.** The `SuperAdminOnly`
  policy guards the Move page and its POST; the button on Details renders only for a SuperAdmin.
  A board Admin or DM of both boards is refused like anyone else. The super admin does not need to
  be a member of the target board.

- **D-02: The target is another board in the source board's linked set, read fresh per request.**
  The set is looked up on every GET and again on the POST, never from session or claims (Phase 82
  D-15). A posted target that is not in the source board's set, or the source board itself, is
  refused even when posted directly. Same board type follows from Phase 91 D-01 and needs no
  separate rule, but the server must not rely on it being true.

- **D-03: The quest's DM must be a DungeonMaster or Admin on the target board.** Read fresh from
  `UserGroups`, using the same effective-role rule the board already applies. Otherwise the Move page
  shows the target as refused with the reason, and the POST refuses. A quest never arrives on a
  board whose members cannot see its DM as a DM, and the DM never loses sight of their own quest.

  A quest whose DM is a SuperAdmin passes this rule on any target board, member or not, because the
  effective-role rule counts a SuperAdmin as Admin everywhere (Phase 91 D-20). Confirmed by the
  operator on 2026-10-18 when research raised the case. The researcher had suggested refusing unless
  a real `UserGroups` row exists, and that was rejected as inconsistent with the rest of the board.

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

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Phase scope
- `.planning/ROADMAP.md` §"Phase 92: Move a Quest or Event to a Linked Board": goal, the open
  questions this discussion answered, constraints (super-admin only, same set, change tracker,
  mobile twins)

### Decisions this phase builds on
- `.planning/phases/91-linked-boards-with-a-shared-calendar/91-CONTEXT.md`: D-01 (same-type
  sets), D-02 (named set, one per board), D-04 (inactive set of one), D-06 (linked board names
  visible across the set, which D-17 relies on), D-08 (member links through the Phase 87 switch),
  D-20 (a SuperAdmin counts as Admin for role rules), and the deferred idea this phase came from
- `.planning/phases/87-cross-board-deep-link-recovery/87-CONTEXT.md`: the board switch and
  banner D-16's toast link uses; D-12 (no SuperAdmin branch in board experience)
- `.planning/phases/82-personal-cross-board-event-agenda/82-CONTEXT.md`: D-15 (membership read
  fresh per request), D-17 (test shape, `DisposeAsync` reset)

### Guard tests and high-risk files
- `QuestBoard.UnitTests/Architecture/FeedRevisionWriteSeamTests.cs`: bans `ExecuteUpdate` in
  production source; writes to `Events`/`Quests` go through the change tracker
- `QuestBoard.UnitTests/Architecture/CrossBoardIgnoreQueryFiltersSeamTests.cs`: closed allowlist
  of files that may call `IgnoreQueryFilters()`
- `QuestBoard.UnitTests/Architecture/AmbientClockSeamTests.cs`: "today" reads go through
  `IBoardClock`
- `QuestBoard.Domain/Services/CalendarFeedWriter.cs`, `QuestBoard.Domain/Services/CalendarSubscriptionService.cs`,
  `QuestBoard.Repository/FeedRevisionStamper.cs`: must not change (D-18)

### Project rules
- `.claude/architecture.md`: layer rules, the feed-revision rule for `Events`/`Quests` writes,
  the board clock
- `.claude/ui-guidelines.md`: card pattern, buttons, `.Mobile.cshtml` twins (read before any view)
- `CLAUDE.md`: no planning IDs in source comments

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `ILinkedBoardSetRepository.GetLinkedBoardsForViewerAsync(activeGroupId, viewerUserId)`: the
  fresh linked-set read Phase 91 uses; returns the other boards in the active board's set.
- `QuestService.PromoteNextWaitlistedPlayerIfSeatFreedAsync` and
  `PlayerSignupRepository.GetTopWaitlistedCandidateAsync`: the existing promotion rule D-06 reuses,
  including the "freeing player is never promoted" guard and the email-eligibility gate.
- `IQuestEmailDispatcher.EnqueueWaitlistPromotedEmail(…, groupId, …)` and the
  `Quest*EmailJob` / `HangfireJobHelper.RunInScopeAsync(scopeFactory, groupId, …)` pattern: jobs
  run under the board id they are given, which is why D-06's email must carry the target board.
- `EventRepository.AddWithCampaignFanOutAsync`: how a new Campaign event gets its automatic Yes
  rows; D-08 must produce the same rows.
- `QuestController.ResolveCharacterAssignmentAsync` and the `UpdateSignup` action: the existing
  path a player uses to pick a character after D-04 clears it.
- `QuestExtensions.HasBeenPlayed(boardToday)`: the "already played" test D-11 uses.
- `Areas/Platform/Controllers/GroupController.cs` and the Linked boards controller from Phase 91:
  the `SuperAdminOnly` + TempData toast + `.Mobile` view pattern.

### Established Patterns
- `PlayerSignups`, `PlayerDateVotes`, `ProposedDates` and `ReminderLogs` have no `GroupId`. Their
  query filters reach through `Quest.GroupId`, so changing the quest's board carries all of them
  along unless they are removed on purpose. `EventSignups` follow `Event.GroupId` the same way.
- `Characters` carry their own `GroupId` and a fail-closed filter. A signup pointing at a
  source-board character would read as having no character on the target, hence D-04.
- `EventSeriesService.TopUpAsync` reads "every slot the series has ever produced" through the
  board-filtered event repository, so an event that leaves the board frees its slot, hence D-09.
- `QuestBoardContext` configures `OriginalQuest`/`FollowUpQuest` as one-to-one on
  `OriginalQuestId`, and `QuestController` refuses a second follow-up when `FollowUpQuest != null`.
  A hidden cross-board link would get past that check, hence D-10.
- `FeedRevisionStamper` treats quest and event `GroupId` as feed-visible, so the move raises the
  revision with no extra code, provided it goes through the change tracker.
- Quest Details computes `CanManage` as quest DM or Admin; a SuperAdmin is checked with
  `User.IsInRole("SuperAdmin")`.

### Integration Points
- `Quest/Details.cshtml` (the right-hand column that holds "DM Controls"), `Quest/Details.Mobile.cshtml`
  and `Events/Details.cshtml` (its admin button stack): the move control.
- New Move page plus its `.Mobile.cshtml` twin, and its GET/POST actions.
- A new Domain move service, plus repository writes in `QuestRepository` / `EventRepository` /
  `PlayerSignupRepository` / `EventSignupRepository` that go through the change tracker.
- New email template(s) under `QuestBoard.Service/Components/Emails/`, a dispatcher method and a
  job, with entries on `EmailPreviewController`.

</code_context>

<specifics>
## Specific Ideas

- The case behind this phase: the same players run several campaigns as separate boards in one
  linked set, and a quest or event gets posted on the wrong one. The move fixes that without
  re-posting and without losing the votes of the people who are on both boards.
- The Move page is a preview first and an action second. Everything the move will destroy (dropped
  signups by name, chain links by quest title) is visible before the confirm button.

</specifics>

<deferred>
## Deferred Ideas

- **Move a whole event series to a linked board.** The series row, its template and every
  occurrence move together, so the top-up keeps working on the target board. Needs its own answers
  for past occurrences, answers and Campaign rows. Not roadmapped.
- **Cancellation entries for subscribers who lose a moved entry.** A `STATUS:CANCELLED` copy kept
  for a while in the feed of anyone who can no longer see the entry, so phones remove it explicitly
  rather than on a silent next poll. Touches the high-risk feed files; only if wanted.

</deferred>

---

*Phase: 92-move-a-quest-or-event-to-a-linked-board*
*Context gathered: 2026-10-17*
