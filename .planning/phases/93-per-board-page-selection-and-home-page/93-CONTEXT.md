# Phase 93: Per-Board Page Selection and Home Page - Context

**Gathered:** 2026-10-20
**Status:** Ready for planning

<domain>
## Phase Boundary

In the Platform area, a super admin chooses which pages each board offers and which page the board
opens on. A switched-off page disappears from both layouts' menus, and links into it disappear. The
server refuses it for everyone on that board: anyone who opens it is sent to the board's home page
with a message. The home page applies wherever the app sends someone "home".

Board type stays a hard limit: a switch only removes pages the type already allows. Boards nobody
configures keep exactly the pages they show today, and they keep the quest list as home.

Not in this phase: changing what a page does when it is on, showing or hiding pages per user or per
role, and a Campaign board with Shop, Players or a DM profile.

</domain>

<decisions>
## Implementation Decisions

### Page list and board type

- **D-01: Board type stays a hard limit.** A switch can only remove a page that the board type
  already allows. Shop (with Shop Management), Players and the DM profile can never be on for a
  Campaign board, whatever is stored. A page is available only when the type allows it and the
  board's switch is on. The server enforces this both when it saves the settings and when it decides
  whether to serve a page. Hiding the switch on the form is not enough.

  This also closes today's gap: `ShopController` and `PlayersController` have no board-type check,
  so `/shop` and `/players` currently open on a Campaign board when typed.

  Rejected: board type as only the starting default, which a super admin could then override.
  Campaign boards with Shop, Players or a DM profile were never built or tested together.

- **D-02: Every board page gets its own switch.** The switchable pages are Shop, Players, DM profile,
  Characters, Contacts, Quest Log, Events and Calendar. Management pages and sub-pages follow their
  page:
  - Shop covers `ShopController` and `ShopManagementController`.
  - Contacts covers `ContactsController` and `ContactCategoryManagementController`.
  - Events covers `EventsController`, including the availability overview on Events Index, and
    `SeriesController`.
  - DM profile covers `DungeonMasterController.Profile` and `EditProfile`.
  - Characters covers the Characters pages (list, details, create, edit and their posts), but not
    its picture endpoints. See D-07.

  Personal and cross-board pages get no switch: My Agenda, Account Profile, the board picker,
  Platform and Admin.

  Rejected: switches only for the four pages first named (Shop, Contacts, Players, Characters).

- **D-03: The quest list is switchable only if research finds no blocking problem.** Otherwise it
  is always on. The operator wants the switch, but only on that condition. The researcher must
  give a clear verdict in RESEARCH.md. If the verdict is "always on", the Quests switch does not
  exist and D-04 does not apply.

  What the researcher must weigh:
  - existing open, finalized and closed quests on the board
  - quest pages reached from the calendar feed, emails (reminders, waitlist, finalized, moved),
    Phase 87 cross-board deep links, and Phase 91 linked boards' calendars
  - the Create Quest menu item and the Quest Log
  - Phase 92 moves onto a board with quests off
  - the reminder jobs
  - the quest board being the app's core value

  "No problem" means that, with D-08's refusal rule in place, nothing user-facing breaks or leads
  into a dead end, and the guarded calendar feed files need no change.

- **D-04: If quests can be switched off, "off" means quests are gone from the board entirely.**
  There is no quest list, no Create Quest, no quest entries on that board's own calendar, and every
  quest page redirects per D-05.

  Rejected: hiding only the list page while DMs can still create quests, and existing quests still
  show on the calendar.

### Switched-off behaviour

- **D-05: Opening a switched-off page redirects to the board's home page with a message.** The
  message is a toast through the existing `_Toasts.cshtml`, for example "Shop isn't used on this
  board." This covers bookmarks, links in emails, and landing on a board after a board switch or a
  Phase 87 deep-link switch.

  Rejected: a plain 404.

- **D-06: Off means off for everyone on that board.** Players, DMs, board Admins and super admins
  all get the redirect. A super admin who needs the page switches it back on in Platform.

  Rejected: letting board Admins and super admins still open a switched-off page (for example, to
  stock a shop before launch).

- **D-07: Links into a switched-off page disappear, but data already shown elsewhere stays.**
  - Buttons, menu items and links that lead into the page are not rendered, on both layouts.
    Examples: the "Events" buttons on Calendar and My Agenda, Create Event, Manage Shop, Edit My
    Profile, DM profile links on the Players page, the back link on contact category management,
    and the controls for minting share links on a switched-off page.
  - Pickers for a switched-off feature are hidden. With Characters off, signing up for a quest
    offers no character choice, and the Phase 72 change-character control is hidden. A sign-up
    without a character is already valid, since `CharacterId` is nullable.
  - Data that is already on the page stays, as plain non-clickable text: a sign-up's character name
    and portrait, and a DM's name. The picture endpoints those portraits load from keep working, so
    the quest board, Quest Details, Manage and the Quest Log look the same apart from the links.
    These are `CharactersController.GetCroppedPicture` and `GetProfilePicture`, and
    `DungeonMasterController.GetDMProfilePicture` and `GetOriginalDMProfilePicture`.

  Rejected: removing everything about the feature, including names and portraits on existing
  sign-ups; leaving the links in place so they lead to the redirect.

- **D-08: Platform refuses to switch Events off while the board has upcoming events.** The same
  applies to Quests, if D-03 allows the switch, while the board has upcoming quests. The refusal
  says how many there are. A recurring series has to be ended first, because it keeps producing
  upcoming events.

  The goal: nothing that the phone feed, My Agenda or linked boards' calendars show ahead of today
  points into a switched-off page. `CalendarFeedWriter.cs`, `CalendarSubscriptionService.cs` and
  `FeedRevisionStamper.cs` stay untouched.

  "Upcoming" is judged by `IBoardClock`, never by the ambient clock. The researcher defines exactly
  what counts: whether cancelled events count, and which quest states count on OneShot and on
  Campaign boards.

  The same reasoning covers anything that would put a new entry on such a board. A Phase 92 move
  onto a board where that kind of entry (quest or event) is switched off is refused. This follows
  from D-04 and D-08; it was not asked separately.

  Rejected: switching only the board's own pages, which would leave existing entries in the feed,
  My Agenda and linked calendars with links leading to the redirect; removing the entries
  everywhere, which would change the guarded feed code.

- **D-09: Switching a page off hides it; it never deletes anything.** Shop items, characters,
  contacts, events and quests stay in the database untouched. Switching the page back on brings
  everything back as it was. This follows from D-07 and from boards having to keep working as they
  do today; it was not asked separately.

### Home page

- **D-10: Any switched-on page that every member can open can be the home page.** That covers the
  quest list (the default), Calendar, Events, Quest Log, Characters, Contacts, Players or Shop, each only where
  board type and switch allow it. DM-only pages cannot be chosen; that includes the DM profile edit
  page and Shop Management.

  Rejected: offering only the quest list or Calendar.

- **D-11: The home page applies everywhere the app sends someone "home".** That means:
  - the `/` address (`HomeController.Index`, for a signed-in user)
  - the "D&D Quest Board" brand link in `_Layout.cshtml` and `_Layout.Mobile.cshtml`, which today
    links straight to `Quest/Index`
  - landing after login or after picking a board with no return URL (the
    `GroupPickerController.RedirectToLocal` fallback), and the `MoveController` fallback
  - the redirect target of D-05

  A return URL, such as a deep link someone followed, still wins over the home page.

  Rejected: applying it only to `/` and the brand link.

- **D-12: The home page is set per board.** Boards in the same linked set can have different home
  pages. The operator's wording "for this board group" turned out to mean "for this board".

  Rejected: one home page shared by every board in a linked set.

- **D-13: Platform refuses to switch off the page that is the board's current home page.** It says
  so, for example "Calendar is this board's home page; pick another home page first." The home page
  can therefore never point at a switched-off page.

  Rejected: quietly moving the home page back to the quest list or the first switched-on page.

### Platform settings screen

- **D-14: The switches and the home page live on the board's existing Edit page.** They sit in a
  "Pages" section under the name and board type: one switch per page and a home-page dropdown,
  saved with the existing single Save. A refusal (D-08 or D-13) shows as a validation error on that
  form, and nothing is saved. Both `Areas/Platform/Views/Group/Edit.cshtml` and
  `Edit.Mobile.cshtml` change.

  Rejected: a separate Pages screen with its own button on the board list; one grid of every board
  against every page.

- **D-15: Pages the board type does not allow show as greyed-out, off switches with the reason,**
  for example "Not available on Campaign boards." The server ignores any posted value for them, in
  line with D-01.

  Rejected: leaving them out of the list.

- **D-16: The Platform board list shows a home-page badge only on non-default boards.** The badge
  reads like "Home: Calendar" and appears only on boards whose home page is not the quest list. It
  sits beside the existing board-type badge, on both layouts.
  Standard boards look exactly as they do now.

  Rejected: also showing a count of switched-off pages; showing nothing.

- **D-17: Creating a board stays as it is.** A new board gets every page its type allows, with the
  quest list as home, and is trimmed later on its Edit page.

  Rejected: putting the switches and home-page dropdown on the Create form too.

- **D-18: Existing boards keep exactly what they show today.** The migration's defaults turn every
  switch on and set the quest list as home. Together with D-01, a Campaign board still hides Shop,
  Players and the DM profile. Nothing a user sees changes until a super admin changes a setting.

### Confirmed after research (2026-10-21)

Research concluded that the quest list can be switched off (the D-03 verdict). The operator then
settled three of its assumptions.

- **D-19: The Quest Log goes off with Quests.** When Quests is off, the Quest Log is unavailable
  whatever its own switch says. Its switch shows greyed out with "Needs the quest list", and its
  stored setting is kept for when Quests comes back on.

  Rejected: keeping the Quest Log independently switchable, which would list quests whose pages all
  redirect.

- **D-20: A quest that is not closed and has not been played counts as live.** A live quest blocks
  switching Quests off (D-08). This is the same rule the quest board uses to list a quest, so an abandoned
  open quest with only past dates also blocks. The admin closes or deletes it first.

  Rejected: counting only quests with a proposed or finalized date from today onward.

- **D-21: A "Quests" menu item appears only on boards whose home page is not the quest list.** It
  shows in both layouts, and only while Quests is on. Neither menu has a Quests item today, because
  the brand link leads to the quest list, so unconfigured boards' menus stay exactly as they are.

  Rejected: always showing a Quests item, which would change the menu on every board.

### Claude's Discretion

- How the settings are stored (columns on `Groups` or a per-board settings table), how pages are
  identified (for example an enum of page keys), and how the server check is wired (action filter,
  attribute or middleware keyed on controller and action). One source of truth must decide page
  availability for the menus, the in-page links, the redirect, the home-page choice and the Platform
  form, so they can never disagree.
- Where the lookup of available pages lives: a sibling of `IBoardTypeResolver`, or an extension of
  it. The settings are read fresh for each request rather than cached in the session or in claims,
  so a Platform change takes effect on the next request (the same reasoning as Phase 82 D-15). It
  must not add a fourth copy of the board-type lookup.
- Toast and refusal wording, the order of the switches, and the dropdown labels.
- Share-link preview cards (Phases 78 and 79) for characters and contacts on a board where that page
  is off. The default is that existing share URLs stop rendering the character or contact card while
  the page is off, falling back to the generic response, and work again once it is switched back
  on. The researcher confirms where the preview endpoint lives.
- Redirect safety. D-13 guarantees the home page is switched on, but the redirect code must still
  never send someone to a switched-off page. It falls back to the quest list, or to the first
  switched-on page if quests are off.
- Past entries from a board with Events or Quests off can still appear on linked boards' calendars
  for past months. Render their links as inert text, the same way Phase 91 renders non-member
  entries, or let them reach the redirect, whichever is simpler and safe.
- The anonymous landing page (the `Home/Index` view for signed-out visitors) stays unchanged, and
  so does a super admin with no active board.

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Phase scope

- `.planning/ROADMAP.md` § Phase 93: goal, origin, starting point (code locations) and constraints.

### Project rules

- `.claude/architecture.md`: layer rules, EF Core only in `QuestBoard.Repository`, migrations
  applied on startup, the change-tracker rule for `Events` and `Quests` writes, `IBoardClock` for
  "what day is it", and the guarded calendar feed files.
- `.claude/ui-guidelines.md`: read before creating or editing any Razor view (card pattern, button
  layout, `.Mobile.cshtml` twins).
- `.planning/PROJECT.md` § Key Decisions: a view-only guard must be backed by the same guard on the
  server, `BoardType` cannot change, and the Platform area sits at `/platform`. See also the
  Validated entry for Navigation & access control (menus show a page only when it is confirmed
  allowed; anonymous and no-board states default to hidden).
- `.planning/codebase/ARCHITECTURE.md` and `.planning/codebase/CONVENTIONS.md`: layer structure,
  naming and AutoMapper patterns.

### Prior phases this builds on

- `.planning/milestones/v6.0-phases/37-navigation-access-control/37-RESEARCH.md`: how board-type
  menu gating and `IBoardTypeResolver` came to be, including the circular-dependency fix that
  forced the resolver split.
- `.planning/phases/91-linked-boards-with-a-shared-calendar/91-CONTEXT.md`: linked sets, the read
  path for linked boards' calendars, inert markup for non-members, and D-20 (a super admin counts as
  Admin on every board).
- `.planning/phases/92-move-a-quest-or-event-to-a-linked-board/92-CONTEXT.md`: the move flow that
  D-08 refuses onto a board with quests or events off.

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets

- `IBoardTypeResolver` (`QuestBoard.Service/Services/BoardTypeResolver.cs`): already injected
  into both layouts for menu gating. It is the natural home, or the model for a sibling service,
  that answers "which pages does the active board offer, and what is its home page".
- `_Toasts.cshtml` with `TempData["Error"]` / `TempData["Success"]` and
  `QuestBoard.Service/Constants/TempDataKeys.cs`: carry the D-05 redirect message.
- The Platform `GroupController.Edit` GET and POST, `GroupEditViewModel` (whose `BoardType` is
  `[BindNever]`, display only) and `Areas/Platform/Views/Group/Edit.cshtml` / `.Mobile.cshtml`:
  the place for the Pages section (D-14).
- `Areas/Platform/Views/Group/Index.cshtml` / `.Mobile.cshtml`: already show a board-type badge;
  the "Home: X" badge goes beside it (D-16).
- `GroupEntity` (`QuestBoard.Repository/Entities/GroupEntity.cs`: `Name`, `CreatedAt`,
  `BoardType`, `LinkedBoardSetId`), the `Group` domain model, `EntityProfile` and
  `ViewModelProfile`: where new settings travel.
- `IBoardClock` (`QuestBoard.Domain`): decides "upcoming" for D-08.

### Established Patterns

- **Menu allowlist gating:** `_Layout.cshtml` and `_Layout.Mobile.cshtml` show a page only when it
  is confirmed allowed. New gating extends this pattern rather than adding a blocklist.
- **Server-side guard behind every hidden UI element:** a project-wide rule, reinforced after
  Phase 52.
- **Board-type lookup debt:** the lookup exists three times (`QuestController`,
  `QuestLogController`, `BoardTypeResolver`). Do not add a fourth.
- **Test doubles:** integration tests replace `IActiveGroupContext` and `IBoardTypeResolver` with
  `MutableGroupContext`, so a new lookup service needs a test-double path too.
- **Concurrency token:** Phase 91 put a concurrency token on `Groups.LinkedBoardSetId`. Saving the
  Edit form must not overwrite the board's link.
- **Platform mobile views:** they render inside the desktop Platform layout (known debt). Update
  both twins anyway.
- **Guarded feed files:** `CalendarFeedWriter.cs`, `CalendarSubscriptionService.cs` and
  `FeedRevisionStamper.cs` stay untouched (D-08).

### Integration Points

- **Home:** `HomeController.Index`, the brand links in both layouts, the
  `GroupPickerController.RedirectToLocal` fallback, and the `MoveController` fallback (D-11). Login
  routes through `AccountController.Login` and the board picker.
- **Middleware:** `GroupSessionMiddleware` (sends users with no active board to the picker with a
  return URL) and `CrossBoardDeepLinkMiddleware` (switches the session board). Either can land
  someone on a switched-off page, which the D-05 redirect then handles.
- **Controllers to gate:** listed in D-02. `CalendarController`, `EventsController` and
  `QuestLogController` already branch on board type inside their actions.
- **Links and pickers to hide (D-07):**
  - the Events buttons in `Calendar/Index(.Mobile)` and `Agenda/Index(.Mobile)`
  - the menu items in both layouts (Create Quest, Create Event, Manage Shop, Edit My Profile, and
    the page links)
  - DM profile links in `Players/Index(.Mobile)`
  - the back link in `ContactCategoryManagement/Manage(.Mobile)`
  - the sign-up character picker and change-character control on Quest Details, both layouts
  - character and contact link cards in rendered quest text
- **Picture endpoints that stay open (D-07):** used from `Quest/Details`, `Quest/Manage`,
  `Quest/_QuestCard`, `QuestLog/Details(.Mobile)` and the Players page.

</code_context>

<specifics>
## Specific Ideas

- The operator's concrete target: at least one board opens on `/calendar` instead of the quest list.
- Example wording given during discussion: the toast "Shop isn't used on this board.", the refusal
  "Calendar is this board's home page; pick another home page first.", the greyed-out reason "Not
  available on Campaign boards.", and the board-list badge "Home: Calendar".

</specifics>

<deferred>
## Deferred Ideas

None. The discussion stayed within the phase scope.

</deferred>

---

*Phase: 93-per-board-page-selection-and-home-page*
*Context gathered: 2026-10-20*
