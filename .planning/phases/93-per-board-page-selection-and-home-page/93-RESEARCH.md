# Phase 93: Per-Board Page Selection and Home Page - Research

**Researched:** 2026-10-21
**Domain:** ASP.NET Core 10 MVC (Razor views, MVC filters, EF Core 10 migration, AutoMapper), multi-tenant board gating
**Confidence:** HIGH for the in-repo findings (every controller, view, layout, repository and test named below was opened this session); MEDIUM for the framework-mechanism claims (docs fetched, behaviour proven only by the Wave 0 tests named below)

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions

- **D-01: Board type stays a hard limit.** A switch can only remove a page that the board type
  already allows. Shop (with Shop Management), Players and the DM profile can never be on for a
  Campaign board, whatever is stored. A page is available only when the type allows it and the
  board's switch is on. The server enforces this both when it saves the settings and when it decides
  whether to serve a page. Hiding the switch on the form is not enough.

  This also closes today's gap: `ShopController` and `PlayersController` have no board-type check,
  so `/shop` and `/players` currently open on a Campaign board when typed.

  Rejected: board type as only the starting default, which a super admin could then override.
  Campaign boards with Shop, Players or a DM profile were never built or tested together.

- **D-02: Every board page gets its own switch:** Shop, Players, DM profile, Characters, Contacts,
  Quest Log, Events and Calendar. Management pages and sub-pages follow their page:
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

- **D-03: The quest list can be switched off only if research finds no blocking problem; otherwise
  it is always on.** The operator wants the switch, but only on that condition. The researcher must
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

- **D-10: Any switched-on page that every member can open can be the home page:** the quest list
  (the default), Calendar, Events, Quest Log, Characters, Contacts, Players or Shop, each only where
  board type and switch allow it. DM-only pages cannot be chosen; that includes the DM profile edit
  page and Shop Management.

  Rejected: offering only the quest list or Calendar.

- **D-11: The home page applies everywhere the app sends someone "home":**
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

- **D-16: The Platform board list shows a "Home: Calendar"-style badge only on boards whose home
  page is not the quest list.** It sits beside the existing board-type badge, on both layouts.
  Standard boards look exactly as they do now.

  Rejected: also showing a count of switched-off pages; showing nothing.

- **D-17: Creating a board stays as it is.** A new board gets every page its type allows, with the
  quest list as home, and is trimmed later on its Edit page.

  Rejected: putting the switches and home-page dropdown on the Create form too.

- **D-18: Existing boards keep exactly what they show today.** The migration's defaults turn every
  switch on and set the quest list as home. Together with D-01, a Campaign board still hides Shop,
  Players and the DM profile. Nothing a user sees changes until a super admin changes a setting.

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

### Deferred Ideas (OUT OF SCOPE)

None. The discussion stayed within the phase scope.
</user_constraints>

<phase_requirements>
## Phase Requirements

No requirement IDs were assigned (`Requirements: TBD` in ROADMAP.md). Decisions D-01..D-18 are the contract. The Validation Architecture below maps each decision to a test instead.
</phase_requirements>

## Project Constraints (from CLAUDE.md)

- **Branching:** never commit to `main`. Work goes on the current milestone branch (`milestone/v9-rolling-improvements`) or a `feature/<short-description>` branch cut from it. [VERIFIED: CLAUDE.md § Branching]
- **No planning references in source:** no `D-xx`, phase numbers, plan ids, review ids or planning-document names in comments, XML docs or string literals. Existing guards (`BoardMoveStaticGuardTests`, `LinkedBoardStaticGuardTests`, `CalendarSubscriptionStaticGuardTests`) show the exact regex shape; Phase 93 should add its own (see Validation Architecture).
- **Razor views:** read `.claude/ui-guidelines.md` first (read this session): `modern-card` / `modern-card-header` / `modern-card-body`, an `<hr>` before the button row, filled (not outline) coloured buttons with `me-2` icons, `d-flex justify-content-between` with cancel left and submit right, and check the `.Mobile.cshtml` twin for every view you touch (twins are not always content-equivalent).
- **Dates:** never `.ToString(...)` a `DateTime` in a view; use `Html.LocalTime` / `Html.WallClock`. "What day is it" goes through `IBoardClock`, never `DateTime.Now/UtcNow/Today`.
- **EF:** EF packages only in `QuestBoard.Repository`; schema change = EF migration, auto-applied on startup; writes to `Events`/`Quests` must go through the change tracker (this phase writes neither).
- **Guarded files:** `CalendarFeedWriter.cs`, `CalendarSubscriptionService.cs`, `FeedRevisionStamper.cs` are high-risk and stay untouched.
- **Local database:** SQL Server at `localhost:1433` is the user's business; do not install, start or tear it down. Integration tests use EF InMemory.
- **Build locks:** if `dotnet build/test` fails on locked files, ask the user to stop the debugger.
- **RIP MCP:** the project asks for `mcp__rip__*` navigation when available. It was not available in this session, so Grep/Read were used (no gap notice needed for the plan).
- **Memory notes that affect execution:** worktrees must be cut from `HEAD` (a zero-result grep is the tell of a worktree cut from `main`); `.planning` files can carry mixed or CRLF line endings, so never anchor a match on `\n`; never create git tags.

## Summary

The phase is a closed-set feature: nine page keys, two new integer columns on `Groups`, one scoped resolver that is the single source of truth, one global MVC resource filter driven by an attribute on controllers and actions, and a small amount of view work in two layouts and about a dozen views. Everything the CONTEXT asks for can be built on seams that already exist (`IBoardTypeResolver`, `_Toasts.cshtml` + `TempData`, `GroupController.Edit`, `EntityProfile`, the Phase 91/92 inert-link markup, `IBoardClock`). No new package is needed.

**D-03 verdict: YES, the quest list can be switched off.** Nothing in the guarded feed files has to change, and every surface that could point at a switched-off quest page is either (a) covered by the D-08 refusal for anything dated today or later, (b) rendered inert, or (c) lands on the D-05 redirect with a toast. The phone feed carries no URLs at all (its writer's own header says `DESCRIPTION` and `URL` were dropped), My Agenda lists events only, and the reminder/waitlist/finalized/moved emails are only ever triggered by quest actions (refused when off) or by the daily reminder sweep (which only sees quests dated tomorrow, which D-08 forbids on an off board). The price is higher than the other eight switches, and the planner must treat it as such: Quest Log has to be coupled to Quests (a quest-less board cannot offer a list of closed quests), and about fifteen extra touchpoints exist (listed in "Quests off entirely"). The switch is therefore best built last, behind the shared mechanism, in its own plan so the other eight pages can ship if it slips.

Two CONTEXT premises do not match the code and the planner should know before writing tasks: **(1) Phases 78 and 79 are not built.** There is no share-link route, no signing, no link cards, no persisted data-protection key ring in the repository (STATE.md: Phase 78 "9 plans written, none executed", Phase 79 "not yet planned"). The share-link and link-card parts of D-07 and the Claude's-Discretion item have no code to gate today; they become a forward contract for Phases 78/79 (see "Share-link previews"). **(2) The DM picture endpoints have no consumer outside the DM pages themselves.** `GetDMProfilePicture` is referenced only by `DungeonMaster/Profile(.Mobile)` and `EditProfile(.Mobile)`; quest pages and the Players page do not render a DM portrait. D-07's exemption still stands (keep those two endpoints exempt), it is just not load-bearing for quest pages.

**Primary recommendation:** Add a Domain `BoardPage` enum + pure `BoardPagePolicy`, store `DisabledPages` (bitmask, 0 = nothing switched off) and `HomePage` (0 = quest list) as two `int NOT NULL DEFAULT 0` columns on `Groups`, expose them through one scoped `IBoardPageResolver` that composes the existing `IBoardTypeResolver` with a single memoised group read, enforce with a global `IAsyncResourceFilter` that reads a `[BoardPage(...)]` attribute from endpoint metadata, and have the layouts, in-page links, home redirect and Platform form all ask that one resolver.

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|-------------|----------------|-----------|
| Page catalogue, type rules, dependency rule, effective home | Domain (pure static policy) | — | Pure functions of `(BoardType, disabled set, stored home)`; reused by resolver, Platform settings service, linked-calendar service and move services without DI |
| Per-request "what does the active board offer" | Service (`BoardPageResolver`, scoped) | Domain interface | Same placement and same DI-cycle reasoning as `IBoardTypeResolver`; must not live on `IActiveGroupContext` |
| Refusing a switched-off page (all verbs) | API / Backend (global MVC resource filter) | — | Server-side guard behind every hidden element; runs after authorization, before model binding |
| Menu items, in-page links, pickers | Frontend Server (Razor, both layouts + twins) | — | Allowlist rendering from the resolver; null (no board) renders nothing |
| Home page redirect (`/`, brand, picker fallback, refusal target) | API / Backend (controllers) | Frontend Server (brand href) | `HomeController.Index` is the single home computation; layouts compute the same route for the brand link |
| Settings persistence (`DisabledPages`, `HomePage`) | Database / Storage (`Groups` columns) | Repository (mapping) | Per-board, read with the group row the app already reads |
| Settings validation (type rules, D-08 counts, D-13) | Domain service | Repository (count queries) | Needs `IBoardClock` and cross-board counts; keeps the controller thin |
| Upcoming counts for another board | Repository (inside already-allowlisted `EventRepository.cs` / `QuestRepository.cs`) | — | Needs `IgnoreQueryFilters` + explicit group id; the seam test allows exactly these files |
| Platform Edit form and board-list badge | Frontend Server (Platform area views) | API (`GroupController`) | Existing Edit page, existing single Save |
| Move target refusal | Domain (`QuestMoveService` / `EventMoveService`) | Service (`MoveController` mapping) | Rule belongs where the target is already resolved |
| Linked-calendar inert links | Domain (`LinkedCalendarService` flags) | Frontend Server (4 view sites) | Same seam Phase 91 used for non-member entries |

## Standard Stack

No new packages. Everything below is already referenced by the solution.

### Core
| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| .NET SDK / ASP.NET Core | 10.0.400 SDK, `net10.0` | MVC, filters, Razor | [VERIFIED: `dotnet --version` run this session; `QuestBoard.UnitTests.csproj` `TargetFramework net10.0`] |
| Microsoft.EntityFrameworkCore (+SqlServer, Design) | 10.0.9 | Migration for the two new columns | [VERIFIED: `QuestBoard.Repository.csproj` lines 10-12] |
| Microsoft.EntityFrameworkCore.InMemory | 10.0.9 | Integration/unit test store | [VERIFIED: `QuestBoard.UnitTests.csproj`] |
| AutoMapper | existing | Entity <-> Domain mapping in `EntityProfile` | [VERIFIED: `Program.cs` `AddAutoMapper`] |

### Supporting (test stack)
| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| xunit.v3 | 3.2.2 | Test framework | all new tests |
| FluentAssertions | 8.10.0 | Assertions | all new tests |
| NSubstitute | 5.3.0 | Doubles for `IBoardClock`, repositories | unit tests |

### Alternatives Considered
| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| Two int columns on `Groups` | A `BoardPageSettings` table | A table adds a join, a new `DbSet`, a new repository and (if it ever filters by board) a new `IgnoreQueryFilters` site; the settings are two scalars read with a row the app already reads. Columns win. |
| Bitmask of *disabled* pages | One bool column per page | Eight columns plus a migration per future page. A disabled-bit mask means CLR default `0` is "nothing switched off", so test seeds (`new GroupEntity { ... }` with no settings, used in ~40 places) and every pre-existing board behave unchanged with no `HasDefaultValue` trick. |
| Global resource filter + attribute | Middleware keyed on route values | Middleware would duplicate the controller/action -> page table in code and cannot see attribute metadata as naturally; a middleware before `UseAuthorization` also cannot know the user is authorised. Filter keeps the mapping next to the code it protects and is testable by reflection. |
| `IAsyncResourceFilter` | `IAsyncAuthorizationFilter` | Microsoft's filter docs say custom authorization filters "require a custom authorization framework" and recommend policies; resource filters are the documented short-circuit point and still run before model binding and before any action filter. |

**Installation:** none.

**Version verification:** no external package is added; versions above were read from the csproj files this session.

## Package Legitimacy Audit

No external packages are installed by this phase, so the Package Legitimacy Gate does not apply.

| Package | Registry | Age | Downloads | Source Repo | Verdict | Disposition |
|---------|----------|-----|-----------|-------------|---------|-------------|
| (none) | — | — | — | — | — | — |

**Packages removed due to [SLOP] verdict:** none
**Packages flagged as suspicious [SUS]:** none

## Architecture Patterns

### System Architecture Diagram

```
 Browser request (any verb)
   |
   v
 UseForwardedHeaders -> UseStaticFiles -> MobileDetection -> UseRouting -> UseSession
   -> UseAuthentication -> GroupSessionMiddleware      (no active board -> /groups/pick, 409 for non-GET)
   -> CrossBoardDeepLinkMiddleware                     (member opening another board's entity -> session switched)
   -> UseRateLimiter -> UseAuthorization               ([Authorize] policies; anonymous/denied handled here)
   |
   v
 MVC pipeline
   [global resource filter: BoardPageGateFilter]  <-- reads [BoardPage]/[BoardPageExempt] from endpoint metadata
        |  no attribute / exempt / no resolved board  ----------------------------------> action runs
        |  page(s) available on the ACTIVE board ---------------------------------------> action runs
        |  a page is off or type-disallowed
        v
        TempData["Info"] = "<Page> isn't used on this board."  +  redirect to board home (never a refused page)
   |
   v
 Controller action  --(views)-->  _Layout / _Layout.Mobile / page views
                                      |
                                      +--> @inject IBoardPageResolver BoardPages (same scoped instance as the filter)

 IBoardPageResolver (scoped, memoised per request, keyed on active group id)
   |-- IBoardTypeResolver.GetBoardTypeAsync()   (existing seam; test double = MutableGroupContext)
   |-- IGroupService.GetByIdAsync(activeGroupId) -> Group { DisabledPages, HomePage }  (one DB read: EF Find is tracked)
   '-- BoardPagePolicy (Domain, pure):  IsAvailable = typeAllows && !disabled && dependencyMet

 Home computation (one place):  EffectiveHome(boardType, disabled, storedHome)
   '-- used by: HomeController.Index, brand link (both layouts), GroupPicker fallback, filter redirect, Account "back" button

 Platform Edit POST:
   GroupEditViewModel (Name, Pages[], HomePage?) -> IBoardPageSettingsService.EvaluateAsync(group, request)
        |-- sanitise: type-disallowed / dependency-locked rows keep stored value; absent rows keep stored value
        |-- validate: home must be available in the RESULTING state; Events/Quests on->off needs 0 upcoming (+ no active series)
        |-- errors -> ModelState + re-render (BoardType re-populated) , nothing saved
        '-- ok -> group.DisabledPages/HomePage/Name -> groupService.UpdateAsync (one save, link columns untouched)

 Move preview (Phase 92): target resolved -> BoardPagePolicy.IsAvailable(setBoardType, target.DisabledPages, kind) else MoveStatus.TargetPageOff
```

### Recommended Project Structure
```
QuestBoard.Domain/
  Enums/BoardPage.cs                      # stored int values: never reorder
  Models/BoardPageSet.cs                  # readonly struct over the int mask
  Models/BoardPageAvailability.cs         # per-request snapshot: IsAvailable, EffectiveHome, AvailablePages
  Extensions/BoardPagePolicy.cs           # pure static rules (type matrix, dependency, home candidates, labels)
  Interfaces/IBoardPageResolver.cs        # sibling of IBoardTypeResolver
  Interfaces/IBoardPageSettingsService.cs # Platform save-time evaluation
  Services/BoardPageSettingsService.cs    # uses IBoardClock + count queries; add to the ambient-clock consumer list
QuestBoard.Repository/
  Entities/GroupEntity.cs                 # + DisabledPages, HomePage (int)
  EventRepository.cs / QuestRepository.cs # + count methods (inside the two already-allowlisted files)
  Migrations/<ts>_AddBoardPageSettings.*  # generated by dotnet ef
QuestBoard.Service/
  Services/BoardPageResolver.cs           # registered next to IBoardTypeResolver in Program.cs
  Filters/BoardPageAttribute.cs, BoardPageExemptAttribute.cs, BoardPageGateFilter.cs
  Helpers/BoardPageRoutes.cs              # BoardPage -> (controller, action); toast copy
  Extensions/BoardPageAvailabilityExtensions.cs  # null-safe Allows(...) for Razor
```

### Pattern 1: Single source of truth for availability
**What:** `BoardPagePolicy.IsAvailable(boardType, disabled, page)` is `typeAllows(boardType, page) && !disabled.Contains(page) && dependencyMet(page)`. Everything else (menus, filter, home, Platform, moves, linked calendars) calls that or the resolver that wraps it.
**When to use:** every place that asks "may this board show page X".
**Type matrix** (D-01; the current menu code in `_Layout.cshtml` is the evidence for today's behaviour):
| Page | OneShot | Campaign |
|------|---------|----------|
| Quests, Calendar, Events, QuestLog, Characters, Contacts | yes | yes |
| Players, Shop (incl. management), DungeonMasterProfile | yes | no (hard limit) |
**Dependency:** `QuestLog` is available only while `Quests` is available (a board with no quests has no log). The stored `QuestLog` bit is left alone, so switching Quests back on restores the log exactly as it was (D-09).
**Example (design sketch, not yet in repo) [ASSUMED]:**
```csharp
public static class BoardPagePolicy
{
    public static bool AllowedOn(BoardType type, BoardPage page) => page switch
    {
        BoardPage.Players or BoardPage.Shop or BoardPage.DungeonMasterProfile => type == BoardType.OneShot,
        _ => true
    };

    public static bool IsAvailable(BoardType type, BoardPageSet disabled, BoardPage page) =>
        AllowedOn(type, page) && !disabled.Contains(page)
        && (page != BoardPage.QuestLog || IsAvailable(type, disabled, BoardPage.Quests));
}
```

### Pattern 2: Attribute-driven global resource filter
**What:** `[BoardPage(BoardPage.Shop)]` on a controller or action; one global `IAsyncResourceFilter` reads `context.HttpContext.GetEndpoint()?.Metadata.GetOrderedMetadata<BoardPageAttribute>()` (all must be available) and `GetMetadata<BoardPageExemptAttribute>()` (skips). `GetOrderedMetadata<T>` is the public API ("Gets the metadata items of type T in ascending order of precedence"). Avoid `ActionDescriptor.EndpointMetadata`, whose doc says "This API is meant for infrastructure and should not be used by application code" [CITED: learn.microsoft.com/dotnet/api/microsoft.aspnetcore.mvc.abstractions.actiondescriptor.endpointmetadata].
**Registration:** `Program.cs:37` is `builder.Services.AddControllersWithViews();`. Change to `AddControllersWithViews(options => options.Filters.Add<BoardPageGateFilter>())` (type-activated, so the filter gets `IBoardPageResolver` and `ITempDataDictionaryFactory` from DI) [CITED: learn.microsoft.com/aspnet/core/mvc/controllers/filters, "Filter scopes" and "Dependency injection"].
**Ordering facts:** authorization filters run first; "Only Authorization filters run before resource filters"; a resource filter short-circuits by setting `Result`; authorization middleware (`UseAuthorization`) has already run, so an anonymous request or a Player opening a DM-only page on an off board gets the login redirect / AccessDenied exactly as today, not the page-off toast [CITED: same page; VERIFIED against `Program.cs` lines for `UseAuthorization`].
**Behaviour rules (recommend):**
1. No `[BoardPage]` metadata, or `[BoardPageExempt]` present -> pass.
2. Resolver returns null (no active board / board type unresolved) -> pass. `GroupSessionMiddleware` already owns that state and the board-scoped queries are fail-closed, so a redirect to a home that cannot be computed would only add a loop risk. This also keeps every existing test that sets `MutableGroupContext.BoardType = null` unchanged.
3. Any listed page unavailable -> `TempData["Info"] = "<DisplayName> isn't used on this board."` and `RedirectToAction(home.Action, home.Controller)` where `home` is the resolver's `EffectiveHome` route. All verbs, including POST/DELETE. Use the existing `TempData["Info"]` key (rendered auto-hiding by `_Toasts.cshtml`); the filter obtains TempData through `ITempDataDictionaryFactory` the same way `CrossBoardDeepLinkMiddleware` does.
4. `EffectiveHome` is by construction an available page, so the redirect target is never refused (no loop). If no home page can be computed at all (everything off, which D-13 prevents), fall back to `Account/Profile`, which is ungated.

### Pattern 3: Pages that need two switches
`UpdateSignupCharacter` is a Quest action that writes a character: put `[BoardPage(BoardPage.Characters)]` on the action while the controller carries `[BoardPage(BoardPage.Quests)]`; the filter requires every attribute in the metadata list to be available. The other CharacterId writers (`Details` POST, `JoinFinalizedQuest`) already funnel through the private `ResolveCharacterAssignmentAsync` (QuestController.cs:1149-1167): make it return `NotAssignable` when Characters is unavailable, so a crafted post with a character id is refused with the existing "Invalid character selection." path.

### Anti-Patterns to Avoid
- **A fourth board-type lookup.** `QuestController.GetActiveBoardTypeAsync` and `QuestLogController.GetActiveBoardTypeAsync` already exist (the third copy is `BoardTypeResolver`). The new resolver takes the type from `IBoardTypeResolver` and adds only the settings read. [VERIFIED: PROJECT.md:203, QuestController.cs:1125-1133]
- **Putting the lookup on `IActiveGroupContext`/`ActiveGroupContextService`.** Circular DI (`QuestBoardContext -> IActiveGroupContext -> IGroupService -> QuestBoardContext`), documented at `Program.cs:256-258` and `BoardTypeResolver.cs`. The resolver sits beside `IBoardTypeResolver`, registered scoped at `Program.cs:258`.
- **Caching availability in session or claims.** Read fresh per request (scoped memo only), so a Platform change applies on the next request.
- **Treating "unresolved board" as "everything on".** Menus use an allowlist: unresolved renders nothing (PROJECT.md Validated entry: "anonymous/no-group states default to hidden").
- **Deleting or rewriting data when a page goes off** (D-09).
- **Hand-listing controller names in middleware.** The attribute keeps the page map beside the code it protects and is reflection-testable.

## Page Map (D-02): every controller and action

Verified by reading each controller's source this session.

| Page key | Where the attribute goes | Notes |
|----------|--------------------------|-------|
| `Quests` | `QuestController` (class). Class has no `[Authorize]`; `Details` GET is anonymous-reachable today and stays so (unresolved board -> pass-through). | `UpdateSignupCharacter` additionally `[BoardPage(Characters)]`. Also `AdminController.Quests` and `DeleteQuest` (action level) and `MoveController.Quest` / `ConfirmQuest` (action level). |
| `QuestLog` | `QuestLogController` (class) | `Index`, `Details`, `EditRecap` GET/POST. Available only while Quests is. |
| `Calendar` | `CalendarController` (class, one action `Index`) | |
| `Events` | `EventsController` (class), `SeriesController` (class) | Includes `Index` (availability overview), `PreviewSeries`, `CheckOccurrenceCollision`, `SetAvailability`, `Withdraw`. Also `MoveController.Event` / `ConfirmEvent` (action level). |
| `Characters` | `CharactersController` (class) + `[BoardPageExempt]` on `GetProfilePicture` and `GetCroppedPicture` | D-07 picture endpoints stay open. |
| `Contacts` | `ContactsController` and `ContactCategoryManagementController` (classes) | Contact image endpoints (`GetContactImage`, `GetCroppedContactImage`) are used only by Contacts pages, so they stay gated with the page. |
| `Players` | `PlayersController` (class) | Closes the Campaign gap. |
| `Shop` | `ShopController` and `ShopManagementController` (classes) | `Details?isModal=true` AJAX variant is gated with the page (a stale open tab would get the redirect HTML; acceptable). Closes the Campaign gap. |
| `DungeonMasterProfile` | `DungeonMasterController.Profile`, `EditProfile` GET and POST (action level) | `GetDMProfilePicture`, `GetOriginalDMProfilePicture` carry no attribute (D-07). |
| *(none, always on)* | `HomeController`, `AccountController`, `AgendaController`, `GroupPickerController`, `CalendarFeedController`, `MarkdownController`, `EmailPreviewController`, the rest of `AdminController`, `Areas/Platform/*` | `MarkdownController.Preview` must stay open: every Markdown editor (quest description, recap, contacts, characters) posts to it. Agenda is exempt from `GroupSessionMiddleware` and works with no board. |

**Closed-list coverage test (recommended):** a reflection test in the style of `AntiForgeryTokenCoverageTests` (reflection-only, no HTTP) enumerating every controller action in `typeof(Program).Assembly` and requiring each to have effective `[BoardPage]`, `[BoardPageExempt]`, or be named in an explicit "always on" list. A new controller then fails the build until someone decides which page it belongs to. [VERIFIED pattern: `QuestBoard.IntegrationTests/Security/AntiForgeryTokenCoverageTests.cs`]

## Storage, migration and mapping (question 4)

**Columns on `Groups`** (recommended): `DisabledPages int NOT NULL DEFAULT 0` (bit `1 << (int)page`; 0 = nothing switched off) and `HomePage int NOT NULL DEFAULT 0` (`BoardPage.Quests = 0`).
- Current entity, quoted verbatim [VERIFIED: GroupEntity.cs:17-24]: `public int BoardType { get; set; }`, `public int? LinkedBoardSetId { get; set; }`, `[ForeignKey(nameof(LinkedBoardSetId))] public virtual LinkedBoardSetEntity? LinkedBoardSet { get; set; }`. Add two plain `public int` properties with no `HasDefaultValue` (EF would treat the CLR default `0` as "unset" and omit it from inserts, which is harmless here but needless).
- **Migration:** `dotnet ef migrations add AddBoardPageSettings --project ../QuestBoard.Repository` from `QuestBoard.Service/` (CLAUDE.md command). For a non-nullable int `AddColumn`, EF scaffolds `defaultValue: 0`, so every existing row gets "all on, quest list home" (D-18). SQL Server adds a NOT NULL int with a default as a metadata-only change. Prior migrations carry `ProductVersion "10.0.9"` (Designer files), matching the runtime. **Tooling caveat:** the repo's local tool manifest pins `dotnet-ef 9.0.6` (`.config/dotnet-tools.json`) while EF Core is 10.0.9, and `dotnet ef migrations list` ran fine this session; a tools-older-than-runtime warning is expected on `add`. If it errors, use the global `dotnet-ef 10.0.5` or bump the manifest. [VERIFIED: manifest + `dotnet tool list` + `migrations list` run]
- **Guards that will fire:** `MigrationPreflightTests.ShippedModel_HasNoPendingModelChanges` (snapshot must be regenerated) and `ShippedMigrations_NoneNeedToRunOutsideATransaction` (plain AddColumn is fine). [VERIFIED: QuestBoard.UnitTests/Migrator/MigrationPreflightTests.cs]
- **Mapping:** `EntityProfile` already casts enums explicitly [VERIFIED: EntityProfile.cs:210-219]: `CreateMap<GroupEntity, Group>().ForMember(dest => dest.BoardType, opt => opt.MapFrom(src => (BoardType)src.BoardType));` and `CreateMap<Group, GroupEntity>()...ForMember(dest => dest.LinkedBoardSetId, opt => opt.Ignore()).ForMember(dest => dest.LinkedBoardSet, opt => opt.Ignore());`. Add `(BoardPage)src.HomePage` / `(int)src.HomePage` and `new BoardPageSet(src.DisabledPages)` / `.Mask`, keep both link `Ignore()` lines, and add `BoardPage` to `EntityProfileEnumCastTests` (it round-trips every declared value of every cast enum). Add `HomePage` to `GroupWithMemberCount` and to **both** projections in `GroupRepository` (`GetAllWithMemberCountAsync`, `GetGroupsForUserAsync`) so the Platform list badge (D-16) and anything else reading the DTO never silently see the default.
- **Concurrency token interplay** [VERIFIED: QuestBoardContext.cs:321 `modelBuilder.Entity<GroupEntity>().Property(g => g.LinkedBoardSetId).IsConcurrencyToken();`]: the token is on `LinkedBoardSetId` only. The Edit POST goes `groupService.GetByIdAsync` -> `UpdateAsync`, and `BaseRepository.UpdateAsync` does `FindAsync` + `Mapper.Map(model, entity)` + save (BaseRepository.cs), so with the link columns still ignored in the `Group -> GroupEntity` map the save carries the loaded `LinkedBoardSetId` as its predicate and never overwrites the link. New columns are ordinary properties (last write wins between two super admins, acceptable). A `DbUpdateConcurrencyException` (another admin linked or unlinked the board mid-edit) is not caught by the existing `DbUpdateException ... when (unique/duplicate)` filter and today would surface as a 500: catch it in the Edit POST and re-render with a "board changed, reload" error.
- **Test seeds stay valid:** ~40 integration tests create `new GroupEntity { ... }` with no settings (e.g. `TestDataHelper.SeedDefaultGroupAsync`, `BoardMoveSeed`); CLR defaults `0/0` mean "all on, quest list home", so none changes.

## Resolver, DI and tests (question 5)

- **Interface** `IBoardPageResolver` in `QuestBoard.Domain/Interfaces` (mirrors `IBoardTypeResolver`, which lives in Domain because `EventSeriesService` consumes it). Implementation `BoardPageResolver(IActiveGroupContext, IBoardTypeResolver, IGroupService)` in `QuestBoard.Service/Services`, scoped, registered right after `Program.cs:258`.
- **Contract:** `Task<BoardPageAvailability?> GetAsync(CancellationToken)`; null when no active board or board type unresolved. When the type resolves but the group row is missing (only possible in tests that point `MutableGroupContext.ActiveGroupId` at an unseeded id), use "nothing disabled, home = quests" so existing tests keep rendering. Memoise per scope keyed on the active group id (a board switch inside one request, as `GroupPickerController.SelectGroup` does before redirecting, must not return the old board's answer).
- **One DB read per request:** `BoardTypeResolver` and the new resolver both call `IGroupService.GetByIdAsync`, which is `DbSet.FindAsync` (BaseRepository.GetByIdAsync); a second `Find` on an already-tracked entity in the same scoped context does not query again. [VERIFIED: BaseRepository.cs `FindAsync([id], ...)`]
- **Razor access:** add `@inject IBoardPageResolver BoardPages` to `Views/_ViewImports.cshtml` (it already injects `IAuthorizationService` and `IUserService`, so every view and both layouts get it). Do not name it `Pages`: `_ViewImports` declares `@namespace QuestBoard.Service.Pages`. Add a null-safe extension `Allows(this BoardPageAvailability? a, BoardPage p) => a?.IsAvailable(p) == true` so views read `var pages = await BoardPages.GetAsync(); ... pages.Allows(BoardPage.Shop)`.
- **How tests drive it:** `WebApplicationFactoryBase` registers `MutableGroupContext` as singleton `IActiveGroupContext` **and** `IBoardTypeResolver` (WebApplicationFactoryBase.cs lines 76-77: `services.AddSingleton<IActiveGroupContext>(TestGroupContext); services.AddSingleton<IBoardTypeResolver>(TestGroupContext);`). Because the new resolver composes `IBoardTypeResolver`, **no test double is needed**: tests keep setting `TestGroupContext.BoardType` and drive the *settings* by writing `DisabledPages` / `HomePage` onto group 1 in the InMemory database (add a `BoardPageSeed.SetAsync(services, groupId, disabled, home)` helper beside `BoardMoveSeed`), restoring in `DisposeAsync` as `LayoutNavigationTests` does. This exercises the real resolver and filter end to end. `CrossBoardWebApplicationFactory` swaps in the real session-backed `IActiveGroupContext` but leaves `IBoardTypeResolver` mutable; that mismatch is harmless for the resolver (type from the mutable, settings from the session's board row) and needs no change.
- **Known debt this phase should close:** PROJECT.md:205 records that no test exercises the production DI graph (a circular-dependency regression "wouldn't be caught"). Add one small factory subclass that re-registers the real `BoardTypeResolver` (`services.AddScoped<IBoardTypeResolver, BoardTypeResolver>()` in `ConfigureTestServices`) and a test that resolves `IBoardPageResolver` in a scope and calls it. [MEDIUM: recommended, not required by CONTEXT]
- **Anonymous / preview contexts:** the resolver reads only `IActiveGroupContext.ActiveGroupId`, which also honours the `SetGroupId` override jobs use, so a future anonymous preview route that scopes a request with `SetGroupId` can ask the same resolver (see Share-link previews).

## Home page (question 6)

- **Route table** (home candidates, D-10): `Quests -> Quest/Index` (attribute route `[Route("quests")]`, QuestController.cs:29), `Calendar -> Calendar/Index`, `Events -> Events/Index`, `QuestLog -> QuestLog/Index`, `Characters -> Characters/Index`, `Contacts -> Contacts/Index`, `Players -> Players/Index`, `Shop -> Shop/Index`. `DungeonMasterProfile` needs an id and is DM-edit, so it is never a candidate; `ShopManagement` is DM-only, never a candidate. Keep the `BoardPage -> (controller, action)` table in the Service layer (`BoardPageRoutes`), not in Domain, and unit-test that every candidate has a route whose controller carries the matching `[BoardPage]`.
- **`EffectiveHome(boardType, disabled, storedHome)`**: stored home if it is a candidate and available; else `Quests` if available; else the first available candidate in catalogue order (`Quests, Calendar, Events, QuestLog, Characters, Contacts, Players, Shop`); else none. This is the "redirect never sends someone to a switched-off page" guarantee even if stored data is inconsistent.
- **Entry points** (all verified by reading the source):
  | Entry point | Today | Change |
  |---|---|---|
  | `HomeController.Index` | `RedirectToAction("Index", "Quest")` for signed-in (HomeController.cs:8-11) | Inject the resolver; redirect to `EffectiveHome` route; resolver null -> keep `Quest/Index` (a super admin with no active board is bounced to the picker by `GroupSessionMiddleware` before this runs, so behaviour is unchanged). Anonymous branch untouched. |
  | Brand link | `asp-controller="Quest" asp-action="Index"` in both layouts when authenticated (`_Layout.cshtml:41`, `_Layout.Mobile.cshtml` brand) | Render the `EffectiveHome` route directly (no redirect hop). For an unconfigured board this generates the same `/quests` href as today. |
  | `GroupPickerController.RedirectToLocal` | `Url.IsLocalUrl(returnUrl) ? Redirect(returnUrl) : RedirectToAction("Index", "Quest")` (GroupPickerController.cs:94-103) | Replace the fallback with `RedirectToAction("Index", "Home")`; `HomeController` then resolves the *new* board's home next request. Return URL still wins. A return URL of `/` (what `GroupSessionMiddleware` writes when a signed-in user without a board opens `/`) naturally lands on the home. |
  | Login | `AccountController.Login` POST -> `RedirectToAction("Index", "GroupPicker", new { returnUrl })` (AccountController.cs:146) | No change; goes through the picker fallback above. |
  | `MoveController` | No true home fallback exists. `ConfirmQuest`/`ConfirmEvent` success redirect to `Quest/Index` and `Events/Index` (lines 52, 107) and `NoLongerOnThisBoard` redirects to the list of the same kind (line 134). | Recommend leaving these: they return to the list of the *same kind* whose gated Move page the user was just on, so the page is on by construction. If the operator wants literal D-11 coverage, point only `NoLongerOnThisBoard` at Home. See Open Question 3. |
  | Filter refusal | n/a | Redirect straight to `EffectiveHome` (not via `/`) so the toast in TempData is read by the final page. |
  | `Account/Profile(.Mobile)` "Back to Quest Board" | `asp-controller="Quest" asp-action="Index"` (Profile.cshtml:168, Profile.Mobile.cshtml:117) | `asp-controller="Home" asp-action="Index"`; rename the label to "Back to the board". `AccessDenied.cshtml:41` and `_Layout.Platform.cshtml` already use `Home/Index`. |
- **Loop safety:** the redirect target is `EffectiveHome`, available by construction, and no home page is gated by anything but its own availability, so it cannot bounce. The gate also passes through when nothing resolves.
- **Quest list reachability (important, easy to miss):** the layouts have **no "Quests" nav item**: the quest list is reachable only through the brand link (verified by reading both layouts). Once a board's home is not the quest list, the brand link goes elsewhere and the list becomes unreachable from the UI. Add a "Quests" nav item (both layouts) rendered **only when the board's stored home is not the quest list and Quests is available**, so unconfigured boards render byte-identically to today.

## D-03: "Quests off entirely" (the verdict's work list)

**Verdict: YES.** Reasons checked against code:

| Concern from CONTEXT | Finding | Evidence |
|---|---|---|
| Phone feed | Feed entries carry no link. Writer header: "after dropping DESCRIPTION and URL, this feed emits a five-field VEVENT". Past finalized quests inside the window stay as plain calendar rows (no dead end). | CalendarFeedWriter.cs:9-10 |
| Calendar subscription service | Reads finalized quests for OneShot boards in a window `today -MonthsBack .. +MonthsAhead`; untouched | CalendarSubscriptionService.cs |
| My Agenda | Events only; its only quest-adjacent control is the Events button | `AgendaController` -> `GetCrossBoardAgendaAsync`; Agenda views |
| Emails (finalized, date-changed, moved, waitlist, reminder) | Each links `{AppUrl}/Quest/Details/{id}`. All are enqueued by quest actions (refused when off) except the reminder sweep, which selects finalized quests dated *tomorrow* | Jobs/*EmailJob.cs lines; DailyReminderJob.cs |
| Phase 87 deep links | A member opening an old link switches the session to that board, then the gate redirects to that board's home with the toast: explained, not a dead end | CrossBoardDeepLinkMiddleware.cs |
| Phase 91 linked calendars | Past quests of an off board still appear; render them inert (see below) | LinkedCalendarService.cs, 4 view sites |
| Create Quest, Quest Log | Menu items hidden; Quest Log must be coupled (below) | `_Layout*.cshtml` |
| Phase 92 | Move onto an off board refused (below) | QuestMoveService.cs |
| Reminder / Hangfire | `DailyReminderJob` only enqueues for quests dated tomorrow; none can exist on an off board (D-08 + Create refused + move refused) | DailyReminderJob.cs |
| Core value | Default on; only a super admin can switch it off; refused while any quest is live; the board keeps events, calendar and everything else | D-08 |

**Everything "quests off" must touch:**
1. `[BoardPage(Quests)]` on `QuestController` (class), `AdminController.Quests` + `DeleteQuest`, `MoveController.Quest` + `ConfirmQuest`.
2. **Quest Log coupling:** `BoardPagePolicy` makes `QuestLog` unavailable whenever `Quests` is. Platform shows the Quest Log switch greyed with "Needs the quest list" while Quests is off and keeps its stored bit.
3. Layouts: hide Create Quest (DM menu) and Admin -> "Quest Management" (desktop dropdown and mobile flat list); keep brand link logic above; add the conditional "Quests" nav item.
4. `CalendarController.Index`: `includeQuests = activeBoardType != BoardType.Campaign && quests available` (the existing line is `var includeQuests = activeBoardType != BoardType.Campaign;`, CalendarController.cs). Quests are never fetched, same pattern as Campaign.
5. Linked calendar quests from a board with Quests off render inert (flag below).
6. `DungeonMaster/Profile(.Mobile)`: the DM's quest history rows (desktop `Profile.cshtml:86`, mobile `Profile.Mobile.cshtml:68,70`) become plain text when Quests is off.
7. `Account/Profile(.Mobile)` "Back to Quest Board" -> Home (above).
8. `MoveController`/`QuestMoveService`: refuse a target with Quests off (below). `MoveControlService` should only offer the control when at least one linked board can take that kind.
9. `QuestController.Index` stops being a home candidate/fallback when off (policy handles it).
10. Platform: refusal while any live quest exists (definition below).
11. Not touched: `CalendarFeedWriter.cs`, `CalendarSubscriptionService.cs`, `FeedRevisionStamper.cs`; no write to `Events`/`Quests`.

## D-08: what counts as "upcoming" (question 2)

All dates are judged against `IBoardClock.Today` / `.Now`, passed into the repository as a parameter; the count methods must not read the clock themselves (the ambient-clock guard lists `QuestRepository.cs`, `GroupRepository.cs` and the controllers; `DateTime.Now/UtcNow/Today` are banned there except named real-instant exemptions). [VERIFIED: AmbientClockSeamTests.cs `GuardedRelativePaths`, `AmbientReadShapes`]

- **Events off is refused while either holds:**
  - `events with Date >= today`, **cancelled included**. Reason: linked calendars deliberately show cancelled events, flagged, with a link for members (`GetLinkedEventsForCalendarAsync`: "There is deliberately no cancellation predicate: a cancelled event is shown, flagged"), so a cancelled future event is still something a calendar points into the page with. My Agenda and `GetUpcoming*` exclude cancelled ones; the refusal message should give the total and, when non-zero, how many are cancelled.
  - **any active series**: `EventSeries.EndDate == null || EndDate >= today`, even with zero upcoming occurrences. This is the one hole in a plain "count upcoming events" rule: `RecurringOccurrenceTopUpJob` runs nightly for every board and `GetActiveSeriesAsync(today)` is "every series ... whose end date is null or on/after the given date", so an active series with all occurrences in the past would regenerate upcoming events after the switch. A series is "ended" today by `SetEndDateAsync(seriesId, endDate, removeFutureOccurrences)`; the refusal should tell the admin to end the series first. [VERIFIED: IEventSeriesRepository.cs, RecurringOccurrenceTopUpJob.cs, EventSeries.cs `EndDate`]
- **Quests off is refused while any live quest exists** on that board: `!IsClosed && (!IsFinalized || FinalizedDate >= today-midnight)`. This is exactly "what the quest board would still show": `GetQuestsWithSignupsAsync` filters `(!q.IsFinalized || ... q.FinalizedDate > oneDayAgo) && !q.IsClosed`, and `QuestExtensions.HasBeenPlayed` (finalized date a full day or more before board today) is the single shared "done" definition, so `!IsClosed && !HasBeenPlayed` is the same thing expressed on the board clock. It needs no board-type branch: on a Campaign board quests never carry a finalized date, so the rule reduces to "not closed"; on OneShot it counts open quests (still collecting votes) and finalized quests dated today or later. DM-only sessions count. A stale open OneShot quest therefore blocks the switch until a DM deletes it (there is no Close for OneShot; `Delete` exists); this is deliberate: it is still on the board today.
- **Only on the on -> off transition.** Re-saving a board whose Events/Quests are already off must not re-run the count.
- **Where the queries live:** the target board is arbitrary (Platform edits board N while the super admin's session may point elsewhere or nowhere), so the counts need `IgnoreQueryFilters()` plus an explicit `GroupId` predicate. `CrossBoardIgnoreQueryFiltersSeamTests` allows exactly five files (`EventSignupRepository`, `EventRepository`, `GroupRepository`, `QuestRepository`, `CrossBoardLinkRepository`) and fails on a sixth. Put `CountUpcomingForBoardAsync` and `CountActiveSeriesForBoardAsync` in `EventRepository.cs` (it already queries `DbContext.EventSeries`) and `CountLiveForBoardAsync` in `QuestRepository.cs`. `GroupEntity` has no query filter, so reading the group needs none. [VERIFIED: CrossBoardIgnoreQueryFiltersSeamTests.cs `AllowedCallSites`; QuestBoardContext.cs query filters on Quest/Event/series]
- **Known residual race:** a quest or event created between the count and the save. Acceptable (the switch is a super-admin action on a settings page; after the save the create pages are refused). Mention in the plan's threat notes; do not add a transaction.

## Link and picker sweep (question 7): concrete file list

Hide when the target page is unavailable on the **active** board. "twin" = `.Mobile.cshtml` sibling checked this session.

| File (+ twin) | What changes |
|---|---|
| `Views/Shared/_Layout.cshtml` + `_Layout.Mobile.cshtml` | Brand href; Admin "Quest Management"; DM menu: Create Quest, Create Event, Board Availability, Manage Shop, Edit My Profile; main menu: Shop, Quest Log, Characters, Contacts, Players; Calendar item (right/offcanvas); new conditional "Quests" item. The desktop DM dropdown must not render when all its items are hidden (empty dropdown). Existing `activeBoardType` checks are replaced by `pages.Allows(...)`; unresolved renders nothing. |
| `Views/Calendar/Index.cshtml` + `.Mobile.cshtml` | "Board Availability" (Events/Index) button (desktop line 18, mobile line 44); runway-banner series links (desktop 44/52, mobile 61/69) when Events is off; the board's own events are not loaded by the controller when Events is off; linked entries (mobile 131-166) use the inert flag |
| `Views/Shared/_LinkedDayEntries.cshtml`, `_LinkedAlsoThatDay.cshtml` | Link only when `ViewerIsMember && page available on the owning board`; otherwise the existing inert markup |
| `Views/Agenda/Index.cshtml` (line 30) + `.Mobile.cshtml` (line 29) | "Board Availability" button hidden when the active board's Events is off or no board resolves |
| `Views/Events/Create.cshtml` (106) and `Edit.cshtml` (61) | Cancel link targets `Calendar/Index`; when Calendar is off point it at `Events/Index`. (Create/Edit mobile twins contain no Calendar link.) |
| `Controllers/Events/EventsController.cs` `RedirectToCalendarMonth` (~459), `SeriesController.cs` `Delete`/`Detach` (lines 93, 113) | Server redirects to `Calendar/Index` after create/edit/delete: fall back to `Events/Index` when Calendar is off, otherwise a successful save ends in a "Calendar isn't used" toast |
| `Views/Players/Index.cshtml` (32) + `.Mobile.cshtml` (24-25) | DM name as plain text when DM profile is off |
| `Views/Quest/Details.cshtml` + `.Mobile.cshtml` | Character pickers: finalized-join select (`finalizedQuestCharacter`, desktop ~464, mobile ~390), open-signup select `name="CharacterId"` (desktop ~546); change-character buttons (desktop 164/295/396, mobile 244/289/348, all guarded by `isCurrentUser && ownedCharacters.Any()`); `_CharacterSelectModal` render (desktop 961, mobile 498). **Also guard the inline `<script>` that does `document.getElementById('finalizedQuestCharacter').addEventListener(...)` (desktop ~508, mobile ~431): it is unguarded today and throws if the select is not rendered.** Character name + portrait text and `GetCroppedPicture` image stay. Also hide the Move-to-board control only if the existing control logic says so (no change needed). |
| `Views/DungeonMaster/Profile.cshtml` (86) + `.Mobile.cshtml` (68, 70) | Quest-history links -> plain text when Quests is off |
| `Views/Account/Profile.cshtml` (168) + `.Mobile.cshtml` (117) | "Back to Quest Board" -> `Home/Index` |
| `Views/Admin/Quests.cshtml`, `.Mobile.cshtml` | No per-link edit: the whole action is gated and the menu item hidden |
| `Controllers/QuestBoard/QuestController.cs` | `ViewBag.UserCharacters` and the new availability flag for the views; `ResolveCharacterAssignmentAsync` refusal (above) |
| `Areas/Platform/Views/Group/Edit.cshtml`, `Edit.Mobile.cshtml`, `Index.cshtml`, `Index.Mobile.cshtml` | See "Platform" below |

**No change needed, and why:** `ContactCategoryManagement/Manage(.Mobile)` back link and `Contacts/*` "Manage Categories" links (same page group, so unreachable when Contacts is off: D-07's category-management example is vacuous); `Shop/*` cross-links (same group); `Events/Details` series link (same group); `Series/Details` -> `Events/Details` (same group); `_Calendar.cshtml` event/quest links (the controller omits the lists; the partial is also used by Quest Details date voting, which is a quest page); `Move/Index` views (see Moves).

## Platform Edit and Index (question 10)

- **Edit view model:** `GroupEditViewModel` today is `Id`, `Name` (`[Required]`, `[StringLength(100...)]`) and `[BindNever] BoardType` [VERIFIED: GroupEditViewModel.cs]. Add `List<BoardPageRowViewModel> Pages` (`Page`, `Enabled`, plus display-only `Label`, `Locked`, `LockedReason` that are re-computed on the server every render and never bound) and `BoardPage? HomePage`.
- **Absent means unchanged (critical):** existing integration tests POST `/platform/Group/Edit` with only `Id` and `Name` (`GroupManagementIntegrationTests.cs:540`, `LinkedBoardsPlatformIntegrationTests.cs:451`), and a checkbox that is not posted would otherwise read as "switched off". Rule: a page not present in the posted `Pages` list keeps its stored value; `HomePage == null` keeps the stored home. Render rows only for switchable pages; disabled/greyed rows post nothing. Without this rule a stale or tampered form could switch every page off.
- **Server rules** (D-01, D-13, D-15), evaluated against the **resulting** state: type-disallowed pages ignored (stored bit untouched); `QuestLog` row ignored while the post turns Quests off; `HomePage` must be a candidate and available in the result; refusal text for an unchanged home that is being switched off: `"{Page} is this board's home page; pick another home page first."` (D-13 wording); if the same post also moves the home, the final state is consistent and is allowed.
- **Failure re-render:** `Edit POST` currently does `return View(model)` on invalid state, and `BoardType` is `[BindNever]`, so the re-rendered form would show the default `OneShot` for a Campaign board and compute the greyed rows wrongly. Re-populate `BoardType` (and the row metadata) from the loaded group before every `View(model)` return, including the existing duplicate-name branch.
- **UI contract** (ui-guidelines.md + existing patterns): a "Pages" section under the name/board-type fields in the existing `modern-card` (desktop) / `platform-group-card-mobile` (mobile) form, Bootstrap `form-check form-switch` rows, greyed disabled rows with the reason as `small.text-muted` ("Not available on Campaign boards."), a home-page `<select>` limited to candidates, `asp-validation-summary="ModelOnly"` already present for refusals, the existing `<hr>` + `justify-content-between` button row unchanged. Platform `.Mobile.cshtml` views render inside the desktop Platform layout today (PROJECT.md:206), so update both twins but verify on the desktop chrome. Mobile styles live in `platform-group.mobile.css`.
- **Index badge (D-16):** next to the existing board-type badge in `Index.cshtml` and `Index.Mobile.cshtml` render `<span class="badge ...">Home: {label}</span>` only when `item.HomePage != BoardPage.Quests` (stored value, so a board with an inconsistent stored home still shows what is stored).
- **Create (D-17):** `GroupCreateViewModel` and `Create` unchanged; `new Group { Name, BoardType }` leaves the new properties at `0/0`.

## Moves (question 9)

- **Where:** `QuestMoveService.BuildPreviewAsync` and `EventMoveService.BuildPreviewAsync` both resolve the target with `preview.Target = target;` (QuestMoveService, EventMoveService line ~142). Add the refusal immediately after that line, before the DM-eligibility / roster work. [VERIFIED: both files read]
- **Data:** extend `LinkedBoard` (currently `Id`, `Name`, `ViewerRole`, `ViewerIsMember`) with `DisabledPages`, populated in `LinkedBoardSetRepository.GetLinkedBoardsForViewerAsync` (its row projection already selects from `Groups` and the set's board type is `SetBoardType`; linked boards share the set's type). No extra query.
- **Status:** add `MoveStatus.TargetPageOff` to the enum (current values quoted verbatim: `Ready, ChooseTarget, NotFound, Blocked, NoLinkedBoards, TargetNotLinked, DungeonMasterNotEligible, Changed, Moved, Failed`, MoveStatus.cs:8-17). **Pitfall:** `MoveController.Quest`/`Event` GET handle `NotFound`, `Blocked`, `NoLinkedBoards` and send everything else to `View("Index", ...)`; a new status must get its own case (toast + redirect to Details) or the page renders with an unhandled state. The POST `default:` branch maps statuses to messages and needs a `TargetPageOff` arm ("{Quests|Events} aren't used on {target}, so nothing was moved."). The move page's target picker (`Move/Index`) should show off boards disabled with the reason; `MoveControlService.GetForQuestAsync/GetForEventAsync` should return a control only when some linked board can take that kind.
- **Move pages themselves** are gated by kind (`MoveController.Quest/ConfirmQuest` -> Quests, `Event/ConfirmEvent` -> Events), so a quest-less board's source page cannot be used to start a move.

## Share-link previews (question 8)

**Nothing to gate today.** There is no mint control, no preview route, no signing, no persisted key ring, no `/s/...` route in the repository (searched controllers, views, services and `Program.cs`; `AddDataProtection` has no match). STATE.md records Phase 78 "9 plans written, none executed" and Phase 79 "not yet planned"; `QuestController.Details` GET still has no `[Authorize]` (Phase 78's D-09 not applied). Consequences for this phase:
- D-07's "controls for minting share links" and the Phase 79 character/contact link cards in rendered quest text have no code to edit.
- Forward contract to record in Phase 78/79 planning (not built here): the anonymous preview route scopes its read with `SetGroupId(verified groupId)` (Phase 78 D-12); it should ask `IBoardPageResolver` (which reads `IActiveGroupContext.ActiveGroupId`, honouring that override) whether `Characters`/`Contacts` are available on the signed group and fall back to the generic response when not; mint controls get the same availability check as any other link; the preview route must stay **unannotated** (it is anonymous and outside the page filter) and make the check itself. A stale share URL then stops rendering the card while the page is off and works again when it is switched on, matching the CONTEXT default.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Per-endpoint policy metadata | A route-string -> page dictionary in middleware | `[BoardPage]` attribute + `HttpContext.GetEndpoint().Metadata.GetOrderedMetadata<T>()` | Attribute sits on the code it protects; reflection test catches omissions |
| Filter dependency injection | `new`-ing services in an attribute | `options.Filters.Add<BoardPageGateFilter>()` (type-activated, constructor DI) | Documented; attributes cannot take DI |
| Flash message | A new banner partial | `TempData["Info"]` + existing `_Toasts.cshtml` | Already rendered by both layouts |
| Today / "upcoming" | `DateTime.Today` / `UtcNow` | `IBoardClock.Today` / `.Now` passed in | `AmbientClockSeamTests` |
| "Is this quest still live" | A new quest-state definition | `!IsClosed && !HasBeenPlayed(boardToday)` (shared `QuestExtensions.HasBeenPlayed`) | One definition across surfaces |
| Cross-board counts | A new repository class | Methods inside `EventRepository.cs` / `QuestRepository.cs` | Seam test allows exactly five files |
| Inert link markup for off pages | New markup | The Phase 91 `ViewerIsMember` inert branch (extend the condition) | Four view sites already branch on it |
| Menu gating | Per-view board-type `if`s | `pages.Allows(...)` from one resolver | Prevents menu/server disagreement |
| Tamper-proof form rows | Trusting posted labels/lock flags | Recompute lock state server-side every render and every post | D-01/D-15 |

**Key insight:** the failure mode here is not any one component, it is two components disagreeing (a menu that shows a page the server refuses, a home page that points at a refused page). One policy, one resolver, one home function removes the class of bug.

## Common Pitfalls

### Pitfall 1: An unposted checkbox reads as "switched off"
**What goes wrong:** HTML omits unchecked/disabled checkboxes; a form that omits the Pages section (old tests, tampering, a future client) would switch pages off or reset the home.
**How to avoid:** rows-with-explicit-page list; absent row = unchanged; `HomePage` nullable = unchanged. Existing tests posting only `Id`+`Name` keep passing and must be asserted to leave settings untouched.
**Warning signs:** `GroupManagementIntegrationTests` / `LinkedBoardsPlatformIntegrationTests` Edit tests changing behaviour.

### Pitfall 2: The Edit re-render forgets the board type
**What goes wrong:** `BoardType` is `[BindNever]`; any `return View(model)` after a refusal shows OneShot and mis-greys the rows.
**How to avoid:** re-populate from the loaded group on every return path (validation, duplicate name, concurrency).

### Pitfall 3: Quest list unreachable when home is elsewhere
**What goes wrong:** no layout has a Quests nav item; the brand link was the only door.
**How to avoid:** conditional "Quests" item (home != quests), identical markup otherwise.

### Pitfall 4: Series regenerate upcoming events
**What goes wrong:** counting only existing upcoming events lets a board with an active series but no upcoming occurrence switch Events off, and the 03:00 top-up job then creates events on it.
**How to avoid:** also refuse on any active series (`EndDate` null or >= today).

### Pitfall 5: `IgnoreQueryFilters` sixth call site
**What goes wrong:** a new repository (or file) calling it fails `IgnoreQueryFilters_IsConfinedToTheAllowlist`.
**How to avoid:** put the counts in `EventRepository.cs` and `QuestRepository.cs`; do not touch the allowlist.

### Pitfall 6: Unhandled new `MoveStatus`
**What goes wrong:** `MoveController` GET `default:` renders `Move/Index` for any status it does not enumerate.
**How to avoid:** explicit case + message arm + a test per kind.

### Pitfall 7: Unguarded inline script on Quest Details
**What goes wrong:** removing the finalized-join character select throws `TypeError` from the inline script (desktop and mobile).
**How to avoid:** wrap markup and script in the same availability condition.

### Pitfall 8: Redirect-after-save to a page that is off
**What goes wrong:** `RedirectToCalendarMonth` and Series `Delete`/`Detach` end in a refusal toast after a successful save when Calendar is off.
**How to avoid:** fall back to `Events/Index`.

### Pitfall 9: Double toasts / stale TempData
**What goes wrong:** `NoLongerOnThisBoard` sets `TempData["Info"]` then redirects to a list that may itself refuse; the second message overwrites the first. Redirect the gate straight to the final home (not via `/`) so exactly one page reads the toast.

### Pitfall 10: Memo keyed on request, not board
**What goes wrong:** `GroupPickerController.SelectGroup` switches the session board then redirects in the same request; a resolver memoised on first call would answer for the old board.
**How to avoid:** key the memo on the active group id.

### Pitfall 11: Existing tests that read `/shop`, `/players`, `/DungeonMaster/*` on a Campaign board
**Risk:** D-01 closes that gap, so such a test would now be redirected. Searched the Campaign-using test files (Admin, Calendar*, Characters, Contacts*, Event*, Layout, QuestCampaignUiParity, QuestClose, QuestManageDmControls, BoardMove*, ...): none requests Shop, Players or DM profile pages while `TestGroupContext.BoardType` is Campaign; `LayoutNavigationTests` only asserts menu absence. Re-run the full suite to confirm.

### Pitfall 12: Test seeds and `HasDefaultValue`
Do not configure `HasDefaultValue(0)`; leave CLR defaults so the ~40 `new GroupEntity { ... }` seeds mean "all on, quest list home".

## Runtime State Inventory

Not a rename/refactor/migration phase in the rename sense, but a schema column is added, so each category was checked:

| Category | Items Found | Action Required |
|----------|-------------|------------------|
| Stored data | `Groups` rows (all existing boards) gain two columns | Migration with `defaultValue: 0`; no data migration; no code path writes the columns for existing boards until a super admin saves |
| Live service config | None. Hangfire recurring jobs (`daily-session-reminders`, `recurring-occurrence-top-up`, `calendar-subscription-retention`) are registered in `Program.cs` and need no change | None |
| OS-registered state | None | None |
| Secrets/env vars | None | None |
| Build artifacts | `QuestBoardContextModelSnapshot.cs` and a new Designer file must be regenerated by `dotnet ef`; `MigrationPreflightTests` fails otherwise | Generate the migration; commit snapshot + designer |

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| Menu gated by an inline board-type `if` per item, no server check for Shop/Players/DM profile | One policy + resolver feeding menu, filter, home and Platform | This phase | Closes `/shop`, `/players`, `/DungeonMaster/*` opening on Campaign when typed |
| Quest list is the only home | Per-board home, quest list default | This phase | Brand link, `/`, picker fallback follow the board |

**Deprecated/outdated:** none introduced. The three existing board-type lookups stay (not consolidated here).

## Code Examples

Design sketches only; none exists in the repo yet, so every snippet is `[ASSUMED design]` until the executor builds it. In-repo values used are quoted above with their source.

### Filter registration (Program.cs:37 today is `builder.Services.AddControllersWithViews();`)
```csharp
builder.Services.AddControllersWithViews(options => options.Filters.Add<BoardPageGateFilter>());
```

### Filter body
```csharp
public sealed class BoardPageGateFilter(IBoardPageResolver resolver, ITempDataDictionaryFactory tempDataFactory) : IAsyncResourceFilter
{
    public async Task OnResourceExecutionAsync(ResourceExecutingContext context, ResourceExecutionDelegate next)
    {
        var metadata = context.HttpContext.GetEndpoint()?.Metadata;
        var pages = metadata?.GetOrderedMetadata<BoardPageAttribute>();
        if (pages is not { Count: > 0 } || metadata!.GetMetadata<BoardPageExemptAttribute>() != null)
        {
            await next();
            return;
        }

        var availability = await resolver.GetAsync(context.HttpContext.RequestAborted);
        if (availability is null) { await next(); return; }          // no board: the session middleware owns this state

        var refused = pages.Select(p => p.Page).FirstOrDefault(p => !availability.IsAvailable(p));
        if (refused is null) { await next(); return; }

        tempDataFactory.GetTempData(context.HttpContext)["Info"] = BoardPageCopy.NotUsed(refused);
        var home = BoardPageRoutes.HomeFor(availability);              // never a refused page; Account/Profile as last resort
        context.Result = new RedirectToActionResult(home.Action, home.Controller, routeValues: null);
    }
}
```

### Migration (scaffolded)
```csharp
migrationBuilder.AddColumn<int>(name: "DisabledPages", table: "Groups", type: "int", nullable: false, defaultValue: 0);
migrationBuilder.AddColumn<int>(name: "HomePage", table: "Groups", type: "int", nullable: false, defaultValue: 0);
```

### Razor usage
```razor
@{ var pages = await BoardPages.GetAsync(); }
@if (pages.Allows(BoardPage.Shop)) { <li class="nav-item"> ... </li> }
```

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|---------------|
| A1 | `HttpContext.GetEndpoint().Metadata.GetOrderedMetadata<T>()` includes both controller-level and action-level attributes for MVC actions, and `GetMetadata<T>()` returns the most specific | Pattern 2 | Filter would miss class- or action-level attributes. Wave 0 reflection + HTTP tests prove it before anything depends on it; fallback is `context.ActionDescriptor.FilterDescriptors` with an `IFilterMetadata` attribute |
| A2 | Quest Log availability should be coupled to Quests (a quest-less board has nothing to list) | D-03 | Operator may want Quest Log independently switchable; then Quest Log would list closed quests whose pages redirect. Confirm in plan review |
| A3 | Cancelled future events count toward the D-08 refusal | D-08 | Operator may prefer cancelled to be ignored; then cancelled events keep a link on linked calendars that leads to the redirect |
| A4 | "Live quest" = `!IsClosed && !HasBeenPlayed(today)`, and a stale open OneShot quest blocks the switch until deleted | D-08 | Could frustrate an admin with an abandoned open quest; alternative is counting only quests with a future proposed date |
| A5 | An unresolved board renders no board pages in the menus (today Quest Log/Characters/Contacts/Create Quest/Create Event render for any signed-in user even with no resolved type) | Pattern 1 | Visible only for a signed-in user with no active board on a non-exempt layout page (e.g. Account/Profile); matches PROJECT.md's allowlist rule, but is a small delta from today |
| A6 | The gate passes through when no board resolves | Pattern 2 | A request with no board but a resolvable type cannot occur; fail-closed queries still apply |
| A7 | Toast severity `Info` (auto-hiding) rather than `Error` | Pattern 2 | Wording/severity is Claude's discretion per CONTEXT; low |
| A8 | A "Quests" nav item shown only when home is not the quest list | Home | Without it the list is unreachable; with it always-on, unconfigured boards would change |
| A9 | With Calendar on, Events off and a Campaign board, the calendar simply renders empty | D-03/Events | Could add a rule "Calendar needs Events or Quests"; recommended to leave as is |
| A10 | Linked-calendar entries from an off page render inert rather than reaching the redirect | Link sweep | Discretion item; inert is safer and mirrors Phase 91 |
| A11 | The local `dotnet-ef 9.0.6` tool can scaffold against EF Core 10.0.9 (it listed migrations; `add` expected to warn) | Storage | Migration scaffold fails; use the global 10.0.5 tool or bump the manifest |
| A12 | The MoveController has no true home fallback; the CONTEXT reference means the list redirects | Home | Operator may want `NoLongerOnThisBoard` to go Home; one-line change |

## Open Questions

1. **Is the Quest Log coupling acceptable?** What we know: D-04 says "every quest page redirects", the log is a view of closed/played quests. What's unclear: whether the operator wants the Quest Log switch to stay independently settable. Recommendation: couple it (greyed with "Needs the quest list"), keep the stored bit.
2. **Calendar on, but nothing to show.** A Campaign board with Events off has an empty calendar. Recommendation: allow; no extra rule.
3. **MoveController "fallback" (D-11).** See Home table. Recommendation: leave the same-kind list redirects; optionally send `NoLongerOnThisBoard` Home.
4. **Cross-board deep link onto an off page switches the session first.** `CrossBoardDeepLinkMiddleware` repoints the session before the gate refuses, so the user lands on the other board's home with both banners. CONTEXT accepts this ("which the D-05 redirect then handles"). Optional improvement: have the resolver tell the middleware the target page is off and skip the switch. Not recommended in this phase (the middleware is guarded by oracle-parity tests).
5. **Phase 78/79 sequencing.** Those phases (still open) should inherit the forward contract in "Share-link previews". Recommendation: copy that paragraph into the Phase 78 and 79 context files when they are next touched.

## Environment Availability

| Dependency | Required By | Available | Version | Fallback |
|------------|------------|-----------|---------|----------|
| .NET SDK | build, tests | yes | 10.0.400 | none needed |
| `dotnet-ef` (local manifest) | migration scaffold | yes, mismatched | 9.0.6 (EF Core is 10.0.9) | global `dotnet-ef 10.0.5` |
| SQL Server `localhost:1433` | `dotnet run`, manual smoke | port open | not queried | tests use InMemory and need none |
| Git branch | commits | yes | `milestone/v9-rolling-improvements` | — |

**Missing dependencies with no fallback:** none.
**Missing dependencies with fallback:** matched EF tools (see above).

## Validation Architecture

### Test Framework
| Property | Value |
|----------|-------|
| Framework | xunit.v3 3.2.2 + FluentAssertions 8.10.0 + NSubstitute 5.3.0, net10.0 [VERIFIED: QuestBoard.UnitTests.csproj] |
| Config file | `QuestBoard.IntegrationTests/xunit.runner.json` (serial per Phase 92 validation notes); unit tests none |
| Quick run command | `dotnet test QuestBoard.UnitTests --filter "FullyQualifiedName~BoardPage|FullyQualifiedName~Architecture"` (Architecture slice alone ran in 25 s total including build, 93 passed, this session) |
| Full suite command | `dotnet test` (repo root, both test projects) |

### Phase Requirements -> Test Map (decisions are the contract)
| Decision | Behavior | Test Type | Automated Command | File Exists? |
|----------|----------|-----------|-------------------|-------------|
| D-01 | `BoardPagePolicy`: Players/Shop/DM profile never available on Campaign whatever the mask; QuestLog dependency | unit | `dotnet test QuestBoard.UnitTests --filter "FullyQualifiedName~BoardPagePolicy"` | Wave 0 |
| D-01 | `/shop`, `/players`, `/DungeonMaster/Profile/{id}` redirect on a Campaign board, 200 on OneShot | integration | `dotnet test QuestBoard.IntegrationTests --filter "FullyQualifiedName~BoardPageGate"` | Wave 0 |
| D-02 | Every controller action gated, exempt, or on the always-on list | reflection (no HTTP) | `dotnet test QuestBoard.IntegrationTests --filter "FullyQualifiedName~BoardPageCoverage"` | Wave 0 |
| D-03/D-04 | Quests off: list, create, details, manage, Quest Log, Admin quests, Move quest all redirect; menu entries absent; calendar loads no quests | integration | `... --filter "FullyQualifiedName~BoardPageQuestsOff"` | Wave 0 |
| D-05/D-06 | Redirect target = board home, `Info` toast rendered, for Player/DM/Admin/SuperAdmin, GET and POST | integration | `... --filter "FullyQualifiedName~BoardPageGate"` | Wave 0 |
| D-07 | Picture endpoints still 200 with Characters/DM profile off; pickers and change-character control absent (desktop + mobile UA); DM name plain text on Players; no `getElementById('finalizedQuestCharacter')` script when hidden | integration | `... --filter "FullyQualifiedName~BoardPageLinks"` | Wave 0 |
| D-07 | `UpdateSignupCharacter` and crafted `characterId` posts refused when Characters off | integration | `... --filter "FullyQualifiedName~BoardPageCharacters"` | Wave 0 |
| D-08 | Events off refused with count: upcoming incl. cancelled, active series; Quests off refused for live quests; no recheck when already off | unit (service, fake clock) + integration (Platform POST) | `dotnet test QuestBoard.UnitTests --filter "FullyQualifiedName~BoardPageSettings"` | Wave 0 |
| D-08 | Move onto a target with that kind off refused for quest and event (GET + POST), `MoveStatus.TargetPageOff` handled, no write | unit + integration | `... --filter "FullyQualifiedName~BoardMove|FullyQualifiedName~BoardPageMove"` | Wave 0 (extend BoardMove*) |
| D-08 | Feed/quest feed files untouched | git check | `git diff --name-only main...HEAD -- QuestBoard.Domain/Services/CalendarFeedWriter.cs QuestBoard.Domain/Services/CalendarSubscriptionService.cs QuestBoard.Repository/FeedRevisionStamper.cs` prints nothing | n/a |
| D-09 | Switching a page off then on leaves rows untouched (counts equal before/after) | integration | `... --filter "FullyQualifiedName~BoardPageSettings"` | Wave 0 |
| D-10/D-11 | `/` redirects to the stored home; brand href; picker fallback; return URL wins; unconfigured board brand href = `/quests`; super admin without board unchanged | integration (use `CrossBoardWebApplicationFactory` for the picker/session paths) | `... --filter "FullyQualifiedName~BoardPageHome"` | Wave 0 |
| D-12 | Two boards in one linked set keep different homes | integration | same filter | Wave 0 |
| D-13 | Switching off the current home refused with the quoted message; moving the home in the same post allowed | unit + integration | `BoardPageSettings` | Wave 0 |
| D-14/D-15/D-16 | Edit form renders Pages section on both twins; disabled rows with reason; posted values for them ignored; `Id`+`Name`-only post leaves settings alone; failure re-render keeps Campaign type; Index badge only when home != quests | integration | `... --filter "FullyQualifiedName~BoardPagePlatform"` | Wave 0 |
| D-17 | Create leaves `0/0` | integration | `BoardPagePlatform` | Wave 0 |
| D-18 | Migration defaults; `HasPendingModelChanges` false; `EntityProfile` enum cast for `BoardPage`; default group renders today's menu exactly (existing `LayoutNavigationTests` stay green) | unit + integration | `dotnet test QuestBoard.UnitTests --filter "FullyQualifiedName~Migrator|FullyQualifiedName~EntityProfileEnumCast"` and `dotnet test QuestBoard.IntegrationTests --filter "FullyQualifiedName~LayoutNavigation"` | existing + extend |
| Linked calendars | Entry from a board with the page off renders inert; on renders link | unit (`LinkedCalendarService`) + integration | `... --filter "FullyQualifiedName~LinkedBoard"` | extend |
| Concurrency | Edit POST keeps `LinkedBoardSetId`; concurrency exception handled | integration | `... --filter "FullyQualifiedName~LinkedBoardsPlatform"` | extend |
| DI graph | Real `IBoardPageResolver` resolves with the real `IBoardTypeResolver` | integration | `BoardPageDependencyInjection` | Wave 0 |

### Sampling Rate
- **Per task commit:** the quick command above (UnitTests slice) plus the relevant `--filter` for the file touched.
- **Per wave merge:** `dotnet test` (both projects).
- **Phase gate:** full suite green plus the three static checks below, before `/gsd-verify-work`.

### Static guards to add or extend
- **Planning-reference guard** for every file the phase touches, in the exact shape of `BoardMoveStaticGuardTests` (regex data with the forbidden literals built from fragments): decision ids `D-01`..`D-18`, `Phase 93`, plan ids `93-0x`, planning document names. [VERIFIED shape: Tests/BoardMoveStaticGuardTests.cs]
- **Ambient clock:** add `BoardPageSettingsService.cs` to `BoardClockConsumerPaths` in `AmbientClockSeamTests`; confirm no edited guarded file (QuestController, EventsController, CalendarController, QuestLogController, SeriesController, MoveController, AdminController, GroupRepository, QuestRepository, the Quest/Admin views) gains `DateTime.Now/UtcNow/Today`.
- **Seam allowlist:** `IgnoreQueryFilters_IsConfinedToTheAllowlist` must pass with the allowlist unchanged.
- **Anti-forgery:** no new `[HttpPost]`; the Edit POST keeps `[ValidateAntiForgeryToken]` (`AntiForgeryTokenCoverageTests`).

### Wave 0 Gaps
- [ ] `QuestBoard.UnitTests/Services/BoardPagePolicyTests.cs`: type matrix, dependency, effective home chain
- [ ] `QuestBoard.UnitTests/Services/BoardPageSettingsServiceTests.cs`: D-08 counts on a fake `IBoardClock`, D-13, sanitising, absent rows
- [ ] `QuestBoard.UnitTests/Services/EntityProfileEnumCastTests.cs`: add `BoardPage`
- [ ] `QuestBoard.IntegrationTests/Helpers/BoardPageSeed.cs`: set `DisabledPages`/`HomePage` on a group row
- [ ] `QuestBoard.IntegrationTests/Controllers/BoardPageGateTests.cs`, `BoardPageCoverageTests.cs` (reflection), `BoardPageNavigationTests.cs` (both UAs), `BoardPageHomeTests.cs`, `BoardPagePlatformTests.cs`, `BoardPageQuestsOffTests.cs`
- [ ] `QuestBoard.IntegrationTests/Tests/BoardPageStaticGuardTests.cs`: planning references
- [ ] DI-graph factory + test (closes PROJECT.md:205 for this seam)
- [ ] Framework install: none

## Security Domain

Security enforcement is not disabled in `.planning/config.json` (no `security_enforcement` key), so it applies.

### Applicable ASVS Categories

| ASVS Category | Applies | Standard Control |
|---------------|---------|-----------------|
| V2 Authentication | no | unchanged (cookie auth) |
| V3 Session Management | no | resolver reads fresh, never trusts session beyond the existing `ActiveGroupId` |
| V4 Access Control | **yes** | `SuperAdminOnly` policy on Platform; global resource filter enforces page availability on every verb; filter runs after `[Authorize]` policies so it never weakens them; server recomputes type rules on save (D-01) |
| V5 Input Validation | **yes** | Posted page keys validated against the enum (unknown/duplicate ignored); `[BindNever] BoardType`; home must be a candidate; posted values for locked rows ignored |
| V6 Cryptography | no | none |
| V13 / CSRF | yes | Edit POST keeps `[ValidateAntiForgeryToken]` |

### Known Threat Patterns for ASP.NET Core MVC multi-tenant gating

| Pattern | STRIDE | Standard Mitigation |
|---------|--------|---------------------|
| Typed URL / crafted POST to a hidden page | Elevation / Tampering | Filter on all verbs; reflection coverage test; D-01 type check at serve time |
| Mass-assignment of settings (unposted/tampered rows, forged `BoardType`) | Tampering | Absent = unchanged; `BindNever`; server recomputes lock state |
| Open redirect via the refusal | Spoofing | Redirect target is built server-side from route names, never from request data; the existing `Url.IsLocalUrl` return-URL handling is unchanged |
| Cross-tenant information via the refusal | Information disclosure | The decision depends only on the *active* board's settings, not on whether an id exists; membership-pinned `CrossBoardDeepLinkMiddleware` is unchanged (its oracle-parity tests keep covering it) |
| Cross-board count query leaking rows | Information disclosure | Counts return scalars, scoped by explicit `GroupId` inside the two allowlisted repositories; no entities cross the boundary |
| Lost update between two super admins | Tampering | Existing `LinkedBoardSetId` concurrency token preserved; handle `DbUpdateConcurrencyException` |
| Redirect loop DoS | Denial of service | `EffectiveHome` always available; `Account/Profile` last resort |

## Sources

### Primary (HIGH confidence)
- Repository source read this session: `GroupController`, `GroupEditViewModel`, `GroupEntity`, `Group`, `EntityProfile`, `GroupRepository`, `BaseRepository`, `QuestBoardContext` (query filters, concurrency token), `Program.cs`, `GroupSessionMiddleware`, `CrossBoardDeepLinkMiddleware`, `CrossBoardLinkRegistry`, `BoardTypeResolver`, `ActiveGroupContextService`, `HomeController`, `GroupPickerController`, `MoveController`, `QuestMoveService`, `EventMoveService`, `MoveControlService`, `LinkedCalendarService`, `LinkedBoardSetRepository`, `CalendarController`, `EventsController`, `SeriesController`, `QuestController`, `QuestLogController`, `AdminController`, all controllers' attribute/action lists, `_Layout*.cshtml`, Platform `Edit/Index` views and twins, Quest/Calendar/Agenda/Players/DM/Account views, `DailyReminderJob`, `RecurringOccurrenceTopUpJob`, `CalendarFeedWriter` header, `CalendarSubscriptionService`
- Test infrastructure read this session: `WebApplicationFactoryBase`, `CrossBoardWebApplicationFactory`, `MutableGroupContext`, `TestDataHelper`, `AuthenticationHelper`, `LayoutNavigationTests`, `AmbientClockSeamTests`, `CrossBoardIgnoreQueryFiltersSeamTests`, `AntiForgeryTokenCoverageTests`, `BoardMoveStaticGuardTests`, `MigrationPreflightTests`, `EntityProfileEnumCastTests`, `92-VALIDATION.md`
- `.planning/` docs read: `93-CONTEXT.md`, ROADMAP.md Phase 93 section, STATE.md, PROJECT.md (debt lines 198-207, Validated entry, Key Decisions), `78-CONTEXT.md`
- Microsoft Learn, ASP.NET Core filters (filter order, resource-filter short-circuit, `Filters.Add`, DI via `TypeFilter`/`IFilterFactory`): learn.microsoft.com/aspnet/core/mvc/controllers/filters
- Microsoft Learn API refs: `EndpointMetadataCollection.GetOrderedMetadata<T>` ("in ascending order of precedence"); `ActionDescriptor.EndpointMetadata` ("meant for infrastructure and should not be used by application code")
- Commands run this session: `dotnet --version` (10.0.400), `dotnet test QuestBoard.UnitTests --filter "FullyQualifiedName~Architecture"` (93 passed), `dotnet ef migrations list`, `dotnet tool list`, TCP probe of 1433 (open)

### Secondary (MEDIUM confidence)
- None used.

### Tertiary (LOW confidence)
- None used; no web search results were relied on.

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH, no new packages; versions read from csproj files
- Architecture: HIGH for the repo-specific seams (read directly); MEDIUM for filter/endpoint-metadata behaviour pending the Wave 0 proof (A1)
- Pitfalls: HIGH, each traced to a specific file and line this session
- D-03 verdict: HIGH on the code facts; the Quest Log coupling and cancelled-event rules are judgement calls flagged A2/A3

**Research date:** 2026-10-21
**Valid until:** 2026-11-20 (stable code area; re-check if Phases 78/79 land first, since they add the share-link surface this phase could not inspect)
