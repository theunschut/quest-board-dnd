# Phase 83: Availability Surface Naming and Placement - Context

**Gathered:** 2026-08-30
**Status:** Ready for planning

<domain>
## Phase Boundary

Two shipped availability surfaces stop competing for the same reader. The board-scoped grid at `EventsController.Index` (`/Events`, Phase 77) is renamed from **Availability Overview** to **Board Availability**, and its navigation entry moves out of the Calendar dropdown into the **Dungeon Master** menu on both layouts. The cross-board personal list at `/Agenda` (Phase 82) keeps the name **My Agenda** and gains matching subtitle copy so the two read as a deliberate pair.

This is a **naming and discoverability phase, not an authorization phase**. No permission gate is added to either surface. Board Availability keeps rendering normally for every board member who reaches it; only which routes are *discoverable* changes.

Not in this phase: any change to what either page computes, queries, or displays beyond its heading copy; any change to the `IgnoreQueryFilters()` boundary Phase 82 established; any change to Phase 77's grid semantics, paging, or five-state cell vocabulary; any quest-side surface.

</domain>

<decisions>
## Implementation Decisions

### Naming and rename scope

- **D-01: The rename touches user-visible strings only.** Page title, both nav labels, cross-link copy, and the test assertions that carry the old string — 14 sites in total. The route stays `/Events`, the controller stays `EventsController`, the view folder stays `Views/Events/`, and the domain vocabulary is untouched (`EventAvailabilityOverview`, `EventAvailabilityRow`, `EventOverviewViewModel`, `EventOverviewRowViewModel`, `OverviewMemberViewModel`, `GetAvailabilityOverviewAsync`, `EventsOverviewOptions`).

  Three reasons, all load-bearing. The defect the roadmap names is the *user-visible* name, and `/Events` is not that name. `EventsController` legitimately owns `Create`, `Edit`, `Details` and `PreviewSeries`, so its folder and route are not the availability page's to claim. And `EventsOverviewOptions.SectionName` is `"EventsOverview"` with code defaults and **no entry in any `appsettings*.json`** — renaming it would silently drop any override set in the server environment file, which is not mirrored in the repo.

  Rejected: adding a dedicated route (a second way to address one action, and stale bookmarks unless a redirect is kept). Rejected: a full domain-type rename (large mechanical diff, the config hazard above, zero user-visible gain).

- **D-02: The board-scoped page is named exactly "Board Availability"** — one string, used identically for the nav label, the browser tab title (`ViewData["Title"]`), and the card heading.

- **D-03: A muted subtitle under the heading carries both secondary answers at once — which board, and events-not-quests.** Shape: the active board's name plus an events qualifier, e.g. *"Events on {ActiveGroupName}"*. `SessionKeys.ActiveGroupName` is already read in `_Layout.cshtml`, so the value costs nothing, but it **can be empty** — the layout falls back to `"Switch Group"` for the same value, and this subtitle needs its own defined fallback.

  The roadmap called the events-versus-quests ambiguity "secondary" but real; on a One-Shot board quests carry their own date-voting, so unqualified "Availability" genuinely could read as quest voting. The subtitle fixes it for free without spending a word in the name.

  Considered and rejected: **"Event Availability"** — reads well beside "Create Event" but drops the whose-axis, which is the *primary* defect this phase exists to fix. **"Board Event Availability"** — fixes both in the name itself and stands alone correctly when quoted, but is three words, the longest label in the DM dropdown, and breaks the two-word symmetry with "My Agenda". **Interpolating the board name into the heading** — the page would stop having one fixed name, and every string assertion would need to know the seeded board name.

- **D-04: My Agenda keeps its name and gains a matching subtitle** stating its cross-board, events-only scope (e.g. *"Upcoming events across all your boards"*). The pair then reads symmetrically: both say whose, both say events-only, both say the board scope — which is the entire point of naming them against each other. Purely additive copy in `Agenda/Index.cshtml` and `Agenda/Index.Mobile.cshtml`; no logic, no conditional, no query change to a page that shipped on 2026-08-30.

  Rejected: a subtitle shown only to multi-board viewers (a second rendering path to test on both layouts, for one line of copy).

### Navigation placement

- **D-05: The desktop Calendar dropdown collapses back to a plain `nav-item`.** Phase 77 D-19 created that dropdown for the sole purpose of holding Calendar plus the overview; with the overview gone, its stated reason leaves with it, and a dropdown wrapping one link costs every reader a click for nothing.

  Safe by inspection: the four existing `Nav_*_CalendarLinkPresent` cases assert on the string `"Calendar"` via `Contain`, not on markup structure, so they stay green through the collapse. Treat these lines with care regardless — Phase 76 plan `76-14` fought a regression in exactly this block.

- **D-06: The moved entry sits behind BOTH the `DungeonMasterOnly` policy AND the existing `activeBoardType is BoardType.OneShot or BoardType.Campaign` gate.** Strictly narrows, never widens.

  Its new sibling `Create Event` has no board-type gate, so DM-policy-only would be the flatter markup — but it would silently *widen* visibility: a DM whose board type has not resolved would see an entry Phase 77 D-22 deliberately hid ("an unresolved board type is deliberately excluded rather than guessed at"), and following it lands on the upstream redirect to the group picker. Nothing in this phase disturbs that reasoning. Cost accepted: one nested condition inside the DM block on each layout.

- **D-07: Position — directly after `Create Event`**, at the same index in the desktop dropdown and in the mobile flat run. Puts the two event surfaces adjacent, and keeps the desktop and mobile DM blocks readable as mirrors of one another — the property the roadmap's desktop-but-not-mobile regression risk depends on.

  Rejected: last below a divider (the mobile offcanvas has no divider idiom, so the layouts would stop mirroring). Rejected: first above `Create Quest` (pushes the app's most-used entry down a slot for a read-only page).

### Cross-links — CONTAINS A ROADMAP AMENDMENT

- **D-08: The Calendar page's cross-link to Board Availability becomes DM-only**, on both `Calendar/Index.cshtml` and `Calendar/Index.Mobile.cshtml`.

  **This amends `.planning/ROADMAP.md`'s Phase 83 scope note**, which currently reads *"It stays reachable by URL and by the existing calendar cross-link; only its default discoverability changes."* The operator took this decision with the contradiction stated explicitly. **`.planning/ROADMAP.md` must be updated to match before planning**, or the researcher, planner and verifier will each read a conflict between their two inputs.

- **D-09: No player-facing link to Board Availability anywhere.** Every discoverable route is DM-only; a player's remaining routes are a bookmark or a shared URL.

  **The page itself keeps no authorization gate.** A player who reaches `/Events` gets a normal 200 and the full grid — no 403, no redirect. This is a soft hide, not a permission change, and the roadmap's reasoned rejection of a DM-only gate (2026-08-30) still stands. Do not "finish the job" by adding `[Authorize(Policy = "DungeonMasterOnly")]` to `EventsController.Index`; that is a different decision the roadmap already declined, on the grounds that gating the grid hides strictly less than the unrestricted agenda already reveals.

- **D-10: My Agenda gains a DM-only return link to Board Availability** in its card header, mirroring the button that already points the other way. Today the link between the two surfaces is one-directional; with every other route DM-gated, a DM standing on the agenda would otherwise have no way across.

- **D-11: Board Availability's existing "My Agenda" header button becomes DM-only too** (`Events/Index.cshtml:15`, `Events/Index.Mobile.cshtml:16`). One rule in both directions.

  **Nobody is stranded by this.** Phase 82 D-08 put My Agenda in the user dropdown unconditionally for every authenticated user, outside every board-type condition — so a player who lands on Board Availability by bookmark still reaches their own view in one click from their own menu. The header button is redundant for a player, not load-bearing.

### Navigation test set

- **D-12: Role-flip string assertions in the existing `Contain`/`NotContain` idiom, run as `[Theory]` across both user agents.** No HTML parser is introduced into `LayoutNavigationTests`.

  The roadmap warns that string assertions cannot see markup structure, so a move can pass while leaving an entry unreachable. That warning is answered here by the *flip* rather than by parsing: the entry's old site was role-blind, so an entry that is now present for a DM and absent for a player can only be inside the DM block. Presence alone would not prove it; presence-and-absence does.

  Rejected: parsing markup to assert dropdown ancestry (a new dependency and a new idiom in a class the whole nav suite shares). Rejected: substring-window proximity assertions (brittle against unrelated edits in the same block).

- **D-13: Four case groups, all four decided — the operator delegated the selection, and none is redundant.**
  1. **DM sees it — Campaign and OneShot, both user agents.** Replaces `Nav_CampaignDm_AvailabilityOverviewLinkPresent`. Without it, the move is indistinguishable from a deletion.
  2. **Player does not — Campaign and OneShot, both user agents.** Inverts `Nav_CampaignPlayer_AvailabilityOverviewLinkPresent` and `Nav_OneShotPlayer_AvailabilityOverviewLinkPresent`. This is the load-bearing half of D-12's argument.
  3. **DM with unresolved board type does not.** Proves D-06's board-type half survived a move into a menu that has no board-type gate of its own — precisely what a DM-policy-only implementation would silently drop. No existing case covers this combination.
  4. **Player `GET /Events` returns 200.** Proves no authorization gate crept in — D-09's hardest constraint, and the one thing the nav assertions cannot see. Belongs beside `EventsOverviewControllerIntegrationTests`, not in the layout suite.

- **D-14: `CalendarButtonStyleTests` re-seeds both existing cases as a DM and gains a player-absent case in the same file.** Both cases currently authenticate as a Player (`roles: ["Player"]`) and assert the link is present, so **D-08 breaks both** — this is not optional cleanup. They keep their filled-button (`btn btn-secondary`, not `btn-outline-`) assertions; the class doc comment widens from styling to the cross-link generally.

  Rejected: splitting style from visibility across two files, or a third dedicated Calendar class — the suite already has four `Calendar*` test classes for one page.

- **D-15: A new dedicated test class is the structural guard against "two names for one page."** It fetches Board Availability, My Agenda and the Calendar on both user agents as a DM — the one role that sees every affected surface and every cross-link — and asserts the literal string `"Availability Overview"` appears in none of them.

  This stays valid under D-01 because internal C# type names never reach rendered HTML, so keeping the `Overview` domain vocabulary cannot make it fail. Name the class for what it guards, so a future reader understands why it exists rather than deleting it as redundant with the nav suite.

  Rejected: folding the assertion into `EventsOverviewControllerIntegrationTests` (sees one page, so a stale label on the Calendar or the agenda slips through — the exact failure this guards). Rejected: crawling every authenticated route (slow, brittle, and needs maintenance on every new route, for a string that lived in four views).

### Claude's Discretion

Not discussed — planner decides:

- The exact subtitle wording on both pages, and the fallback when `SessionKeys.ActiveGroupName` is empty.
- Icon treatment for the entry inside the DM menu. Its new siblings render plain icons; the page heading uses `fas fa-calendar-check text-purple`; the DM toggle itself is `text-danger`.
- Whether the four DM-only conditions (two calendar views, two page-header buttons) share a helper or are inlined per view.
- Test class and method naming, and where D-15's new class sits in the folder structure.
- Whether the code comments in `Agenda/Index.cshtml` that refer to "the availability overview" are reworded to the new label. They are internal prose, not user-visible, so D-01 does not compel it.
- Whether the two pages' subtitles use a shared partial or are written per view.

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Phase scope and requirements
- `.planning/ROADMAP.md` — the **Phase 83** entry in full: goal, origin, all four scope notes, the rejected DM-only gate with its reasoning, the reopen condition, and both named risks. **Read it knowing D-08 amends it** — the "stays reachable by the existing calendar cross-link" clause no longer holds, and the entry needs updating to match. Also the **Phase 77** and **Phase 82** entries, which define the two surfaces being named against each other.
- `.planning/REQUIREMENTS.md` — **Phase 83 has no requirement IDs yet** (`Requirements: TBD` in the roadmap). The `EVTVIEW-01`…`EVTVIEW-04` block is Phase 77's and the `EVTAGENDA-01`…`EVTAGENDA-10` block is Phase 82's; neither covers this phase. Requirements need minting during planning, and the coverage table plus its 60/60 count need updating.
- `.planning/phases/77-availability-overview-page/77-CONTEXT.md` — **the surface being renamed and moved.** D-19 (the Calendar dropdown D-05 collapses), D-20 (the mobile flat-sibling rule), D-21 (the calendar cross-link D-08 gates), D-22 (the board-type gate D-06 preserves, and its note that `LayoutNavigationTests` asserts on strings), D-16/D-23 (all-members, read-only).
- `.planning/phases/82-personal-cross-board-event-agenda/82-CONTEXT.md` — **the other half of the pair.** D-08 (the unconditional user-dropdown entry — the reason D-11 strands nobody), D-10 (the existing one-directional cross-links D-10/D-11 here re-gate), D-02 (the full roster on every row — the fact that makes a DM-only gate on the grid incoherent, per the roadmap's rejection).

### Project conventions
- `CLAUDE.md` — the `modern-card` / `modern-card-header` / `modern-card-body` view pattern; **no GSD references in source comments** (applies to every comment this phase writes); Windows/CRLF; EF packages only in `QuestBoard.Repository`.
- `.planning/codebase/CONVENTIONS.md` — naming and code style.
- `.planning/codebase/TESTING.md` — integration versus unit test placement.

### Code the phase must read before changing
- `QuestBoard.Service/Views/Shared/_Layout.cshtml:90-127` — the Dungeon Master dropdown, gated on `DungeonMasterOnly` alone, with `Create Event` at `:103`. D-07's insertion point.
- `QuestBoard.Service/Views/Shared/_Layout.cshtml:168-189` — the Calendar dropdown D-05 collapses and the `Availability Overview` item at `:184` that leaves it.
- `QuestBoard.Service/Views/Shared/_Layout.cshtml:204-220` — the Switch Group entry and the unconditional `My Agenda` entry at `:217`. **Do not gate this one** — it is what makes D-11 safe.
- `QuestBoard.Service/Views/Shared/_Layout.Mobile.cshtml:76-105` — the flat DM block, with `Create Event` at `:85`. D-07's mobile insertion point. **This layout contains no dropdown anywhere** (Phase 77 D-20).
- `QuestBoard.Service/Views/Shared/_Layout.Mobile.cshtml:141-158` — the board-type-gated Calendar and `Availability Overview` flat siblings; the latter at `:153` is what moves.
- `QuestBoard.Service/Views/Shared/_Layout.Mobile.cshtml:176-183` — the unconditional flat `My Agenda` entry. Same rule as desktop: leave it alone.
- `QuestBoard.Service/Views/Events/Index.cshtml:1-17` and `Index.Mobile.cshtml:1-17` — `ViewData["Title"]`, the card header, and the `My Agenda` button D-11 gates. D-02/D-03's subtitle lands here.
- `QuestBoard.Service/Views/Agenda/Index.cshtml:1-24` and `Index.Mobile.cshtml:1-24` — the agenda's card header. D-04's subtitle and D-10's DM-only return link land here. **Read the comment block at `:9-15` first** — it explains why this page deliberately diverges from the overview's row-click pattern; do not "restore consistency".
- `QuestBoard.Service/Views/Calendar/Index.cshtml:14-21` and `Index.Mobile.cshtml:32-39` — the two cross-link buttons sitting side by side. D-08 gates the first of each pair; the `My Agenda` button beside it stays as it is.
- `QuestBoard.Service/Controllers/Events/EventsController.cs:25-53` — `Index` and the comment above it stating why the page is unrestricted. **That comment is now partly stale**: the reasoning still holds, but a reader needs to know the nav no longer offers it to players. Update it to say why the page stays open while its links are DM-only, without naming any phase.
- `QuestBoard.Domain/Models/EventsOverviewOptions.cs` — `SectionName = "EventsOverview"`, with a code comment stating no deployment environment file has to change. D-01 leaves this alone precisely because a server override may exist that the repo cannot see.
- `QuestBoard.IntegrationTests/Controllers/LayoutNavigationTests.cs:319-382` — the four existing overview cases D-13 replaces, and `:384-452` — the My Agenda cases, whose shape the new DM cases should follow.
- `QuestBoard.IntegrationTests/Controllers/CalendarButtonStyleTests.cs` — both cases seed a Player and assert presence; **D-08 breaks both**. D-14 rewrites them.
- `QuestBoard.IntegrationTests/Controllers/EventsOverviewControllerIntegrationTests.cs` — where D-13's case 4 (player `GET /Events` → 200) belongs.
- `QuestBoard.IntegrationTests/Controllers/EventsOverviewMobileStyleTests.cs` — the mobile-view sibling; check whether any of its assertions carry the old string.

### Do not touch
- `QuestBoard.Domain/Services/EventService.cs`, `QuestBoard.Repository/EventRepository.cs`, `QuestBoard.Repository/EventSignupRepository.cs` — no query, no computation, and no tenancy behaviour changes in this phase.
- `QuestBoard.Service/Views/Events/_AvailabilityCell.cshtml` and `_AvailabilityCounts.cshtml` — the shared five-state partials both surfaces render through. Unchanged.
- `QuestBoard.Service/Views/Events/Details.cshtml` — Phase 75 D-01's single availability write surface, and Phase 82 D-13's conditional back link. Untouched.
- `EventsController.Index`'s `[Authorize]`-only attribute set — D-09. Adding a policy here is the design the roadmap declined.

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- **The `DungeonMasterOnly` policy check idiom** — `(await AuthorizationService.AuthorizeAsync(User, "DungeonMasterOnly")).Succeeded`, already used in both layouts. D-06's gate and D-08/D-10/D-11's view-level conditions all reuse it; `AuthorizationService` is already injected in both layouts.
- **`SessionKeys.ActiveGroupName`** — already read in both layouts for the Switch Group label, with an empty-string fallback in place. D-03's subtitle reuses the value and needs its own fallback.
- **`LayoutNavigationTests`' `[Theory]` + `GetWithUserAgentAsync` + desktop/mobile `InlineData` shape** — the established idiom for asserting a nav entry on both layouts in one case. D-13's eight cases follow it unchanged.
- **`AuthenticationHelper.CreateAuthenticatedDMClientAsync` / `CreateAuthenticatedClientWithUserAsync`** — the DM-versus-player client pair D-13's role flip depends on; both already used throughout the nav suite.
- **`_factory.TestGroupContext.BoardType` with a `finally` reset** — the existing pattern for board-type-conditional nav cases, and what D-13's case 3 needs to drive an unresolved board type.

### Established Patterns
- **`LayoutNavigationTests` asserts on strings, not markup structure.** Load-bearing in both directions here: it is why D-05's dropdown collapse is safe, and why D-12 leans on the role flip rather than presence alone.
- **Mobile views are selected by user agent, not by breakpoint.** Every mobile assertion must send the mobile UA; devtools emulation will never exercise these views.
- **`_Layout.Mobile.cshtml` contains no dropdown anywhere** — its DM block is a flat run of `nav-item`s. D-07's mobile entry is a flat sibling, not a nested menu.
- **The Calendar nav gate is on a resolved board type, not a role** — which is exactly why D-06 has to state both gates explicitly rather than inheriting one.
- **Navigation regressions have shipped here before.** A prior phase shipped a nav entry on desktop but not mobile, and another gated an entry to the wrong board type and made a surface unreachable; Phase 76 plan `76-14` fought a regression in this same block. Every nav change in this phase needs its mobile twin in the same commit.

### Integration Points
- **`_Layout.cshtml`** — remove the overview item from the Calendar dropdown, collapse that dropdown to a plain link (D-05), add the entry to the DM dropdown after `Create Event` behind both gates (D-06, D-07).
- **`_Layout.Mobile.cshtml`** — the same two edits in the flat offcanvas list.
- **`Calendar/Index.cshtml` + `Index.Mobile.cshtml`** — DM-gate the Board Availability button and relabel it (D-08).
- **`Events/Index.cshtml` + `Index.Mobile.cshtml`** — new title, new subtitle, DM-gate the `My Agenda` button (D-02, D-03, D-11).
- **`Agenda/Index.cshtml` + `Index.Mobile.cshtml`** — new subtitle, new DM-only return link (D-04, D-10).
- **`EventsController.Index`** — comment only; no code change (D-09).
- **`.planning/ROADMAP.md`** — Phase 83 scope note amended for D-08, and `Requirements: TBD` resolved during planning.
- **Tests** — `LayoutNavigationTests` (four cases replaced by eight), `CalendarButtonStyleTests` (two re-seeded, one added), `EventsOverviewControllerIntegrationTests` (one added), and one new guard class.

</code_context>

<specifics>
## Specific Ideas

- **"What about Event Availability? or Board Event Availability? as this page only shows events and not quests?"** — the operator's own framing, and the origin of D-03. The roadmap had already logged the events-versus-quests ambiguity as real but secondary; the subtitle exists specifically so the phase fixes it without spending the name on it. A planner tempted to simplify D-03 away should understand that the subtitle is carrying a defect fix, not decoration.
- **The pair is the deliverable, not the rename.** D-03 and D-04 are one decision seen from two sides: both pages say whose, both say events-only, both say their board scope. Shipping the Board Availability subtitle without the My Agenda one leaves the pair asymmetric and the phase half-done.
- **D-08 is an operator amendment to the roadmap, taken with the conflict stated.** It is recorded that way deliberately. Whoever updates `.planning/ROADMAP.md` should edit the scope note rather than adding a footnote, so the next reader sees one instruction, not two.
- **The line D-09 draws is thin and worth guarding.** Every discoverable link is DM-only, and the page is not. That is a defensible end state, but it is one small edit away from the DM-only gate the roadmap explicitly rejected on 2026-08-30. D-13's case 4 exists to make that edit fail loudly rather than pass quietly.
- **The role flip is the proof, not the presence.** D-12's whole argument is that the old site was role-blind. If a future change makes the *old* location role-aware too, the flip stops proving placement and the suite needs rethinking — worth a sentence in the test class comment.

</specifics>

<deferred>
## Deferred Ideas

None — discussion stayed within the phase boundary. Naming alternatives ("Event Availability", "Board Event Availability") and placement alternatives were evaluated and rejected inside D-03 and D-07 rather than deferred; the reasoning is recorded there so a later phase does not relitigate them from scratch.

Carried forward unchanged from the roadmap: **the reopen condition on the rejected DM-only gate.** If Phase 82's D-02 per-row roster is ever reduced to counts, the two surfaces stop overlapping and an authorization gate on Board Availability becomes coherent. That is a scope change to Phase 82's D-02, not a permission tweak, and needs its own decision. D-09 does not consume it.

</deferred>

---

*Phase: 83-availability-surface-naming-and-placement*
*Context gathered: 2026-08-30*
