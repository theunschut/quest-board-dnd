# Phase 91: Linked Boards with a Shared Calendar - Context

**Gathered:** 2026-10-15
**Status:** Ready for planning

<domain>
## Phase Boundary

A super admin groups two or more boards of the same board type into a named **link group** on a
new Platform page. On every board in a group, `/calendar` (desktop and mobile) and the Quest
Details date-vote calendar (desktop and mobile) then also show the quests and events of the other
boards in the group, read-only and marked with the board they belong to, so the same players never
get a date booked twice. Removing a board from its group restores its own calendar. Boards in no
group, or in a group of one, behave exactly as they do today.

Out of this phase: any write that crosses boards (no editing, signing up, voting or moving from
another board's calendar), the phone calendar feed, the DM series-runway warning, My Agenda and the
Events availability overview.

</domain>

<decisions>
## Implementation Decisions

### Link model

- **D-01: Same-type links only, enforced server-side.** Confirmed against the real case that
  started this phase: the operator's existing board and the new one-shot board are both One-Shot.
  `BoardType` cannot change after creation (Edit shows it disabled), so a same-type group cannot
  later become mixed. Enforcement lives on the server path that adds a board to a group, not only in
  the form's option list.

  On a Campaign-type group the existing events-only rule still holds: a Campaign board's calendar
  never loads quests, its own or a linked board's.

- **D-02: A named link group; a board belongs to at most one.** The super admin creates a group
  with a name and adds boards to it. Every board in a group sees every other board in it, so links
  are transitive by construction and there is no "A–B and B–C, does A see C" case.

  The operator first answered "you decide" by an early click and then chose the named group
  explicitly on re-ask; record it as the operator's choice, not Claude's.

  Rejected: pairwise links (needs a transitivity rule and makes a "who sees what" screen hard to
  read).
  — **Reversibility:** costly — the schema ships as an EF Core migration (a group table plus a
  board-to-group reference); moving to pairwise later means a second migration, though a group
  converts losslessly into all of its pairs.

- **D-03: Deleting a board removes it from its group automatically.** Delete stays one confirm,
  exactly as today (Platform Delete is already only allowed for boards with no members). No "unlink
  first" step.

- **D-04: A group left with one board (or none) is kept and shown as inactive.** It stays on the
  Linked boards page with its name so the super admin can add a board back without recreating it.
  A group of one adds nothing to any calendar. Removing a group is an explicit action on the page.

### Platform admin

- **D-05: A dedicated "Linked boards" page in the Platform area, under `SuperAdminOnly`.** It lists
  every group with its boards, its state (active / inactive) and its board type, and supports
  create, rename, add a board, remove a board and delete the group. It is a new page with its
  `.Mobile.cshtml` twin, using the Platform layouts.

  The operator chose this over a picker on the board's Edit page (confirmed on re-ask).

  The board picker when adding a board offers only boards of the group's type that are not already
  in another group; the server re-checks both rules.

### What a viewer sees of a linked board

- **D-06: A non-member of linked board B sees B's entries in full, as plain text, not clickable.**
  Title, time, B's board name and (for quests) the DM's name. No link, because a link would 404 for
  them under Phase 87's rules.

  This is a deliberate divergence from Phase 82 D-15 / Phase 87 D-10, which treat board names and
  titles as sensitive to non-members. Here the super admin's act of linking the boards *is* the
  authorization to show those dates to every member of every board in the group. The reason it was
  chosen: the person who picks a quest's dates is that board's DM. If a DM who is not on B could not
  see B's entries, A's calendar would look exactly as it does unlinked, and the clash this phase
  exists to prevent could still be booked.

  Rejected: an anonymous "busy" block (hides what is booked from the DM deciding dates); members-only
  visibility (works against the phase goal).

- **D-07: A linked quest carries title, time, board name and DM name, and nothing else.** No
  building-key warning, no finalized tick derived from signups, no votes, no roster, no signup
  state. The read is a **narrow projection** that never loads signups, votes or players. The
  existing `ProjectForCalendar` (which eagerly loads all of them) must not be reused for linked
  boards. A linked event carries title, date, start time label and board name.

- **D-08: A member of board B gets a clickable entry through the Phase 87 board switch.** For a
  member of board B, B's entry is clickable and goes through the Phase 87 board switch. The link is the ordinary `/Quest/Details/{id}` or `/Events/Details/{id}` URL;
  `CrossBoardDeepLinkMiddleware` switches the session to B and shows the "switched to B, switch
  back" banner. Nothing new is built for this.

  Whether the viewer is a member of each linked board is read fresh from `UserGroups` on every
  request (Phase 82 D-15), never from session or claims. No SuperAdmin branch (Phase 55 override,
  Phase 87 D-12): a SuperAdmin who is not a member of B gets the plain-text entry like anyone else.

### Which entries, and where

- **D-09: Linked quests follow the board's own quest rules.** A finalized quest on its chosen date;
  an open quest on every proposed date, with the same proposed / finalized distinction. Clashes
  mostly start while dates are still being voted on, so proposed dates count.

- **D-10: Cancelled linked events show struck through.** Cancelled events from a linked board show
  struck through, exactly like the board's own cancelled events. *(Revised 2026-10-15 — originally "hidden"; superseded by D-19's one-board
  rule.)* The strike-through already says the date is free, so showing it costs nothing and keeps
  linked and own entries under one rule.

- **D-11: `/calendar` shows linked entries on both layouts.** Desktop renders through the shared
  `_Calendar` partial; `Calendar/Index.Mobile.cshtml` builds its own agenda list from
  `GetCalendarDays()` and must be changed separately. A mobile day with only linked entries still
  gets a day section.

- **D-12: The Quest Details date-vote calendar also shows linked quests and events, read-only.**
  - Desktop (`Quest/Details.cshtml`, three `_Calendar` call sites): linked entries appear on their
    days in the grid, marked as in D-14.
  - Mobile (`Quest/Details.Mobile.cshtml` via `_Calendar.Mobile.cshtml`, which is a list of this
    quest's own proposed dates, not a grid): each proposed date that coincides with a linked entry
    gets an "Also that day: *Board B* — *Title*, *time*" line under its date label.
  - A linked entry never renders a vote control, a vote indicator or any form input. Today vote
    markup is gated on `Quest.Id == CurrentQuestId`; linked entries must render through a path that
    cannot reach that markup at all, not one that relies on ids never colliding.
  - The board's **own** events stay off the Details page as today. Adding them would change unlinked
    boards, which the phase goal forbids.

- **D-13: Unchanged by linking.** The DM series-runway warning stays per-board; the phone calendar
  feed stays membership-based and `CalendarFeedWriter.cs`, `CalendarSubscriptionService.cs` and
  `FeedRevisionStamper.cs` are not touched; My Agenda and the Events availability overview are
  untouched.

### Presentation

- **D-14: On the desktop month grid, linked entries are muted and dashed, with no board-name line.**
  *(Revised 2026-10-15 — the board-name line was dropped because the title already has too little
  room in a day cell.)* Lighter background and a dashed left border that keeps the proposed (yellow)
  / finalized (green) border colour for quests and the event colour for events, so the edge still
  says what the entry is and the dash says it is from a linked board. No extra text line and no
  title prefix or icon, so a linked chip costs no more room than an own chip. Hovering a linked chip
  reveals board name, DM name (quests) and time, the same way an own chip reveals DM and time on
  hover; the board name is also in the chip's tooltip. The board's own entries are unchanged.

  Where there is room, the board name stays visible as text: the mobile `/calendar` agenda rows and
  the mobile Quest Details "Also that day" line.

  Rejected: an always-visible board-name line (eats the title's room in a narrow cell); a "[Board]"
  title prefix (same problem, does not stand out); a colour per board on the left edge (collides with
  the proposed/finalized/event colours already on that edge); a per-board colour stripe on the right
  edge (only pays off with three or more linked boards, which is not the real case).

- **D-15: The page states which boards it is showing.** Desktop: the existing Legend card on
  `Calendar/Index.cshtml` gains a "Linked boards" section with a dashed swatch explaining that dashed
  entries come from a linked board, and listing each linked board by name. Mobile: an "Also showing:
  Board B, Board C" line under the month navigation bar. Desktop Quest Details, which has no legend,
  gets an "Also showing: Board B (read-only)" note above its calendar. Nothing renders on an unlinked
  board or a group of one.

- **D-16: Within a day, own entries come first, then linked.** Own entries keep today's order
  (events, then quests); linked entries follow. A linked board's sessions can never push the board's
  own entries out of view in a cramped cell.

### One board, by role (added 2026-10-15)

- **D-19: Linked boards fill the calendar as if they were one board.** A linked entry is included
  under exactly the same rules as an own entry (D-09, D-10, D-20); what differs is only how it is
  displayed (D-14 to D-16) and that it is read-only (D-07, D-12). Any future inclusion rule applies
  to own and linked entries alike.

- **D-20: DM-only sessions follow the viewer's role on the owning board.** A DM-only session
  (`DungeonMasterSession`) is shown only to a viewer who is DungeonMaster or Admin on the board that
  owns it; a SuperAdmin counts as Admin, as on the board today. The role
  is the viewer's role on the *owning* board, read fresh, not their role on the board they are
  viewing from. On A's calendar a DM of B sees B's DM-only sessions even if they are only a player
  on A; a player of B, or a non-member of B, does not. This mirrors the quest board's existing rule
  (`QuestRepository.GetQuestsWithSignupsForRoleAsync`) and costs nothing for clash prevention: a
  DM-only session does not use the players' evenings, and the DMs it does involve still see it.

  The same rule now applies to the board's **own** calendar too. Today `/calendar` (via
  `GetQuestsForCalendarAsync`) shows every DM-only session to every player, unlike the quest board;
  this phase closes that on `/calendar` (both layouts) and on the Quest Details calendar, because one
  board has one rule. Unlinked boards change only in that players stop seeing DM-only sessions on
  the calendar — a deliberate fix, not a regression. Opening a DM-only quest's Details page by URL
  is out of scope (see Deferred). The calendar feed is unaffected: it already includes only quests
  the subscriber is selected for or DMs.

  This is an exception to D-06 (non-members see B's entries in full): a DM-only session is not an
  entry a non-member of B would ever see.

### The cross-board read and its proof

- **D-17: One narrowed, filter-bypassing read per entity, pinned to the linked board ids.** The
  linked set is the other boards in the active board's group, read fresh per request (never cached
  in session). The query uses `IgnoreQueryFilters()` with `linkedGroupIds.Contains(x.GroupId)` in
  the predicate, following Phase 82 D-14's shape, and lives in a file on the
  `CrossBoardIgnoreQueryFiltersSeamTests` allowlist. After materialization every row's `GroupId` is
  re-checked against the linked set and the read fails closed (Phase 82 D-16), with a comment that
  says plainly both layers read the same set.

  The read is pure: no write to `Events` or `Quests`, so no feed revision moves.

- **D-18: Required integration-test proofs.** The integration tests must prove at least:
  1. **An unlinked board is unchanged.** No entry, title or board name from any other board reaches
     its `/calendar` or Quest Details page (tenant isolation still holds).
  2. **Linked boards see each other, and an outside board does not.** A and B in one group, C not:
     A's calendar shows B's entries and not C's, and B's shows A's. Without this, a suite that only
     proves absence stays green if the linked read silently returns nothing.
  3. **Removing a board from the group takes effect on the next request.**
  4. **Member vs non-member rendering.** A member of B gets a link to B's Details; a non-member gets
     the same entry with no link.
  5. **No roster or vote data leaks.** Another board's player names, votes and signup state are
     absent from the page.
  6. **Cancelled linked events show struck through; open linked quests appear on their proposed
     dates.**
  7. **Linked entries on Quest Details carry no vote input.**
  8. **Server-side enforcement on the Platform page.** A non-SuperAdmin is refused; adding a board of
     the other type, or a board already in another group, is refused even when posted directly.
  9. **DM-only sessions follow the owning board's role (D-20).** A player of A sees neither A's nor
     B's DM-only sessions; a DM of B who is a player on A sees B's but not A's; a non-member of B
     never sees B's.

  Reset `ActiveGroupId` to `1` in `DisposeAsync`, as Phase 82 D-17 requires, because
  `WebApplicationFactoryBase.TestGroupContext` is a shared singleton.

### Claude's Discretion

- **Entity, table, service and repository names**, as long as they do not reuse the
  `CrossBoardLink*` prefix: that name already belongs to Phase 87's deep-link resolver
  (`CrossBoardLinkRepository`, `CrossBoardLinkRegistry`, `CrossBoardLinkResolverService`), and a
  second "link" concept under the same prefix would be misread.
- **How the group's board type is held**: stored on the group at creation, or derived from its
  first board. Either is fine as long as the server rejects a mismatched join and an empty group
  cannot accept a board of the wrong type.
- **Whether the linked read goes into existing allowlisted repositories** (`QuestRepository.cs`,
  `EventRepository.cs`) **or a new dedicated class** added to the allowlist. Phase 87 D-11's
  preference for one auditable class is a fair default, but adding a file to the allowlist has to
  be argued for in the test's comment.
- **How linked entries reach the view model**: separate collections on `CalendarViewModel` /
  `CalendarDay`, or a flag on a shared entry type. The view model's "defaults to empty on purpose"
  convention must carry over, so callers that do not set linked data render nothing.
- **Where the Linked boards page is reached from.** A header button beside "Create" and "Users" on
  the Platform board index matches how the Users page is reached; a board-index marker showing
  which boards are linked is optional.
- **Whether the group name is ever shown to players.** It is admin-facing by default; players see
  board names only.
- **Exact copy** for the Linked boards page, the legend section, the mobile "Also showing" line and
  the Details "Also that day" line, and empty / inactive states.
- **The Linked boards page layout and form flow** (inline add/remove vs separate edit page), within
  `.claude/ui-guidelines.md`.
- Test structure beyond the mandatory cases in D-18.

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Phase scope
- `.planning/ROADMAP.md` §"Phase 91: Linked Boards with a Shared Calendar" — goal, starting point,
  constraints (super-admin only, same type, read-only, pure read, migration, mobile twins)

### Cross-board reads and tenancy (decisions this phase builds on)
- `.planning/phases/82-personal-cross-board-event-agenda/82-CONTEXT.md` — D-14 (one query,
  `IgnoreQueryFilters` with the set pinned), D-15 (membership read fresh per request), D-16
  (in-memory re-check and its stated limits), D-17 (mandatory test shape, `DisposeAsync` reset)
- `.planning/phases/87-cross-board-deep-link-recovery/87-CONTEXT.md` — D-01…D-08 (auto-switch on a
  member's deep link, banner), D-10/D-11 (leak-nothing lookup, `IgnoreQueryFilters` allowlist),
  D-12 (no SuperAdmin branch)
- `QuestBoard.UnitTests/Architecture/CrossBoardIgnoreQueryFiltersSeamTests.cs` — closed allowlist of
  files that may call `IgnoreQueryFilters()`

### Project rules
- `.claude/ui-guidelines.md` — card pattern, buttons, `.Mobile.cshtml` twins (read before any view)
- `.claude/architecture.md` — layer rules, migrations, the feed-revision rule for `Events`/`Quests`
  writes, high-risk feed files
- `CLAUDE.md` — no planning IDs in source comments

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `QuestBoard.Service/ViewModels/CalendarViewModels/CalendarViewModel.cs` — month model; `GetQuestsForDate`
  already applies the finalized / proposed rule D-09 reuses; collections default to empty on purpose.
- `QuestBoard.Service/Views/Shared/_Calendar.cshtml` — desktop grid used by `/calendar` and Quest
  Details; events render first, then quests; vote markup is inside the quest loop.
- `QuestBoard.Service/Views/Calendar/Index.cshtml` — has the Legend card D-15 extends.
- `QuestBoard.Repository/EventRepository.cs` `GetUpcomingAcrossGroupsWithSignupsAsync` — the pinned
  `IgnoreQueryFilters` shape to follow.
- `QuestBoard.Service/Middleware/CrossBoardDeepLinkMiddleware.cs` + `Helpers/CrossBoardLinkRegistry.cs`
  — already resolve `(Quest, Details)` and `(Events, Details)` for members; D-08 needs nothing new.
- `QuestBoard.Service/Areas/Platform/Controllers/GroupController.cs` — pattern for a `SuperAdminOnly`
  Platform controller with TempData toasts and `.Mobile` views.

### Established Patterns
- Every tenant entity has a fail-closed query filter on `GroupId == ActiveGroupId`
  (`QuestBoardContext.cs`); crossing it needs the allowlist.
- `CalendarController.Index` skips quests on Campaign boards; the same check must apply to linked
  quests.
- `ProjectForCalendar` (`QuestRepository.cs`) eagerly loads signups, votes and players — fine for
  own quests, wrong for linked ones (D-07).
- `QuestController.Details` builds one `CalendarViewModel` per month that has a proposed date for
  this quest, with `Quests = allQuests` and no `Events`.
- `_Calendar.Mobile.cshtml` renders only the current quest's proposed dates as a vote list.

### Integration Points
- `CalendarController.Index` and `QuestController.Details` — load the linked set and linked entries.
- `Calendar/Index.Mobile.cshtml`, `Quest/Details.cshtml`, `Quest/Details.Mobile.cshtml`,
  `_Calendar.cshtml`, `_Calendar.Mobile.cshtml`, `wwwroot/css/calendar.css` and
  `calendar.mobile.css`.
- Platform: the new Linked boards controller and views; `Group/Index` header for the entry point;
  `GroupController.DeleteConfirmed` / `GroupService.RemoveAsync` for D-03.
- `QuestBoard.Repository` — new entity, `QuestBoardContext` configuration, EF Core migration.

</code_context>

<specifics>
## Specific Ideas

- The real use case: the same group of players runs several campaigns as separate One-Shot boards;
  the operator is starting a new one-shot board for another campaign and wants its dates on the
  existing board's calendar and vice versa.
- Approved desktop day-cell sketch for D-14 (revised 2026-10-15: own entries solid, linked entry
  muted and dashed with no extra line; board, DM and time appear on hover):

  ```
  ┌─ 14 ───────────────┐
  │┃ The Sunken Vault  │  ← own (solid)
  │┃ Session 12        │  ← own (solid)
  │┆ Goblin Market     │  ← linked (muted, dashed)
  └────────────────────┘

  hover on the linked chip:
  │┆ Goblin Market          │
  │┆ Board B · Sara · 19:30 │

  Legend:  ┃ Proposed  ┃ Finalized  ┃ Event
           ┆ dashed = Linked boards: Board B
  ```

- A desktop day cell with linked entries also carries a small link icon with a count in its corner,
  so linked entries pushed below the fold (Quest Details especially) are still noticed.
- In the Platform area a link group is called a **"Set"** ("Create Set", "Delete Set"), because
  Platform already calls a board a "Group". Players never see the set's name.

- Mobile Quest Details: an "Also that day: Board B — Title, 19:30" line under each clashing proposed
  date, never a vote control.

</specifics>

<deferred>
## Deferred Ideas

- **Move a quest or event to a linked board (super admin, from its Details page).** Raised by the
  operator at the end of this discussion. It is a cross-board *write*, the opposite of this phase's
  read-only constraint, and it opens questions this phase does not answer: signups reference
  board-scoped characters and players who may not be members of the target board (votes, waitlist
  and rewards hang off those signups); an event in a recurring series must leave it or take it
  along; a board change is a feed-visible field, so it bumps `FeedRevision` and the entry leaves the
  feed of subscribers who are only on the source board; and affected members probably need to be
  told. **Added to the roadmap as its own phase after 91, depending on it.**
- **Own-board events on the Quest Details date-vote calendar.** Today Details shows none of the
  board's own events. Adding them would change unlinked boards, which this phase's goal forbids; a
  natural small follow-up if wanted.

- **DM-only quest Details by URL.** A player who has the id of a DM-only quest can still open its
  Details page directly. D-20 hides DM-only sessions on every calendar; closing the Details route is
  a separate fix.

</deferred>

---

*Phase: 91-linked-boards-with-a-shared-calendar*
*Context gathered: 2026-10-15*
