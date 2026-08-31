# Requirements: D&D Quest Board — v9.0 Rolling Improvements

**Defined:** 2026-08-25
**Core Value:** The quest board must reliably let DMs post quests and players sign up — everything else enhances that loop.

> **Rolling milestone.** Unlike v1.0–v8.0, v9.0 has no fixed end-state. The requirements below are the *opening* scope. Additional requirements will be appended as ad-hoc work arrives, and the milestone closes when the operator decides to cut it. New categories get new REQ-ID prefixes; existing ones continue their numbering.

## v1 Requirements

Requirements for the v9.0 milestone. Each maps to a roadmap phase.

### Signup Character Selection

- [x] **SIGNCHAR-01**: A player viewing a quest they are signed up for can change the character on their signup, even when a character is already selected — on the desktop quest Details page, in both the finalized-participants table and the waitlist table
- [x] **SIGNCHAR-02**: A player can change the character on their signup from the mobile quest Details page, which today offers no way to set or change a character at all
- [x] **SIGNCHAR-03**: A player can clear their signup back to "no character" from the change UI, on both desktop and mobile
- [x] **SIGNCHAR-04**: When a player's signup holds a character that is no longer Active (Retired or Dead), the change UI shows that character as the current selection, clearly labelled with its status, so opening and saving the form cannot silently wipe the selection
- [x] **SIGNCHAR-05**: Changing the character remains possible after a quest is finalized, with no time cutoff — a player can still swap right up to and during game night
- [x] **SIGNCHAR-06**: Changing the character remains possible for waitlisted signups and for all three signup roles (Player, Spectator, AssistantDM), matching what signup-time character selection already allows
- [x] **SIGNCHAR-07**: A player cannot set their signup to a character owned by another user or belonging to another group, and this is proven by an automated cross-group regression test rather than assumed from the query filters

### Security Alert Resolution

- [x] **SECALERT-01**: The five open HIGH GitHub security alerts (#17–#21, `System.Security.Cryptography.Xml`) are investigated individually — branch scope, manifest attribution, and dependency-graph freshness confirmed per alert — before any of them is closed
- [x] **SECALERT-02**: GitHub's dependency graph is force-refreshed and the alerts re-checked, so the staleness conclusion rests on GitHub's own re-scan rather than on local `dotnet list package` output alone
- [x] **SECALERT-03**: Each of the five alerts is closed individually with a dismissal reason that cites the actual evidence gathered — never a bulk action with a generic reason
- [x] **SECALERT-04**: The investigation and its outcome are recorded in `.planning/PROJECT.md`, so a future reviewer can distinguish a genuine triage from a rubber stamp without relying on GitHub's UI history
- [x] **SECALERT-05**: The GitHub Security tab shows zero open HIGH alerts for this repository once the phase closes

### Calendar Events — Foundation

- [x] **EVENT-01**: A DM can create an event on their board with a title, an optional description, a date, and an optional start time
- [x] **EVENT-02**: A DM can edit and delete events on their own board; events are scoped to that board and never visible to another
- [x] **EVENT-03**: Events appear on the desktop calendar page, visually distinguishable from quests at a glance
- [x] **EVENT-04**: Events appear on the mobile calendar page, which today lists only days that have quests
- [x] **EVENT-05**: Events never appear on the quest board main page and never block or constrain quest creation — they are informational only
- [x] **EVENT-06**: "Create Event" sits in the same navbar category as "Create Quest" and is available to all DM roles

### Calendar Events — Availability

- [x] **EVTAVAIL-01**: On a One-Shot board, a player can optionally sign up to an event and record their availability as Yes, Maybe, or No — with no signup created unless they choose to
- [x] **EVTAVAIL-02**: On a Campaign board, every board member is automatically signed up to each event with availability Yes, and opts out by changing their own answer to No rather than by removing the signup
- [x] **EVTAVAIL-03**: A player can change their availability on an event at any time
- [x] **EVTAVAIL-04**: A member who joins a Campaign board after events already exist is auto-signed-up to every event dated today or later, and a member who leaves has all of their event signups on that board removed — past and future, automatic and deliberate
- [x] **EVTAVAIL-05**: A player cannot see or change availability for an event on a board they are not a member of, proven by an automated test using two distinct groups

### Calendar Events — Recurrence

- [x] **EVTRECUR-01**: A DM can make an event recur by setting a base cadence (every N weeks on a given weekday), an anchor date, and a repeating on/off cycle mask — so "two sessions on, two off" is expressible directly
- [x] **EVTRECUR-02**: While configuring a series, the DM sees a live preview of the next ~10 dates it will generate, before saving
- [x] **EVTRECUR-03**: Occurrences are generated ahead of time on a rolling window and topped up automatically, so an open-ended campaign never needs manual re-extension
- [x] **EVTRECUR-04**: A DM can cancel a single occurrence without affecting the rest of the series
- [x] **EVTRECUR-05**: A DM can move a single occurrence to a different date without affecting the rest of the series
- [x] **EVTRECUR-06**: A DM can edit a single occurrence's details without affecting the rest of the series
- [x] **EVTRECUR-07**: Re-running the occurrence generator never duplicates an existing occurrence, resurrects a cancelled one, or overwrites one that was moved or edited
- [x] **EVTRECUR-08**: Two boards can be configured with mirrored cycle masks on the same cadence and anchor so their sessions interleave rather than collide

> **Supersession note (recorded by plan 76-15).** Closing EVTRECUR-03 gave the campaign calendar
> two campaign-relevant read surfaces — the DM horizon banner and the cancelled-occurrence chip —
> that did not exist when Phase 37 decided to hide the Calendar nav entry on campaign boards. Plan
> `76-14` therefore supersedes **only the calendar clause** of NAV-01, the Phase 37 decision
> (shipped in commit `f7a31fa9`), archived under
> `.planning/milestones/v6.0-phases/37-navigation-access-control/`: campaign boards now show the
> Calendar nav entry on both layouts, and the campaign calendar itself is an events-only surface
> (quests are excluded in `CalendarController`, never hidden in a view). NAV-02 (shop), NAV-04
> (manage shop), NAV-05 (edit my profile), NAV-06 (players), and the logged-out-visitor rule are all
> untouched and remain in force. `LayoutNavigationTests.Nav_CampaignDm_CalendarLinkAbsent` was
> replaced by `LayoutNavigationTests.Nav_CampaignDm_CalendarLinkPresent` rather than deleted.

### Calendar Events — Availability Overview

- [x] **EVTVIEW-01**: A new page shows upcoming events for the current board as a grid of events against players, with each player's availability
- [x] **EVTVIEW-02**: The overview visually distinguishes an untouched default from an answer the player actually gave, so "available" is not confused with "never looked"
- [x] **EVTVIEW-03**: The overview shows a per-event availability count, so a poorly-attended date is obvious at a glance
- [x] **EVTVIEW-04**: The overview never displays events or members from another board

### Personal Cross-Board Event Agenda

- [x] **EVTAGENDA-01**: The agenda lists every upcoming, non-cancelled event across every board the viewer is a member of, one row per event, ordered chronologically and interleaved across boards, whether or not a signup row already exists for the viewer
- [x] **EVTAGENDA-02**: Every agenda row names the board its event belongs to
- [x] **EVTAGENDA-03**: Every agenda row carries the viewer's own availability and the event's complete roster, using the same five-state cell vocabulary the board-scoped surfaces already use
- [x] **EVTAGENDA-04**: A board-selection filter narrows the agenda, defaults to all of the viewer's boards, is remembered for the session, and is applied before the next-N window is taken
- [x] **EVTAGENDA-05**: The agenda is reachable from the user menu beside Switch Group on both the desktop and the mobile layout, for every authenticated user, with no board-type gate and no board-count gate
- [x] **EVTAGENDA-06**: Acting on a row whose event is on a board other than the viewer's active board prompts to switch before continuing to that event's details; a row already on the active board goes straight through
- [x] **EVTAGENDA-07**: A board the viewer has left never appears on their agenda on the very next request, because membership is read fresh on every request rather than from session or claims
- [x] **EVTAGENDA-08**: A SuperAdmin's agenda is scoped by their own board memberships exactly like any other user's, with no wider cross-board read
- [x] **EVTAGENDA-09**: The agenda never shows another board's events or members, and the board filter can only narrow the viewer's own memberships, never widen them
- [x] **EVTAGENDA-10**: The agenda page loads for an authenticated user who has no active board selected, rather than diverting them to the board picker

### Availability Surface Naming and Placement

- [x] **EVTNAME-01**: The board-scoped availability page is called "Board Availability" everywhere a reader meets it — browser tab title, card heading, navigation entry, and both cross-link buttons — and the retired label survives in no rendered page on either layout
- [x] **EVTNAME-02**: Board Availability carries a subtitle under its heading naming the active board and stating that the page covers events rather than quests, with a defined fallback when no board name is in session
- [x] **EVTNAME-03**: My Agenda keeps its name and carries a matching subtitle stating its cross-board, events-only scope, so the two surfaces read as a deliberate pair rather than as two unrelated pages
- [x] **EVTNAME-04**: Board Availability's navigation entry sits inside the Dungeon Master menu directly after Create Event on both the desktop and the mobile layout, behind both the Dungeon Master policy and the existing resolved-board-type condition
- [x] **EVTNAME-05**: Every discoverable route to Board Availability — the navigation entry and the Calendar page's cross-link — renders only for a Dungeon Master on both layouts; a player's remaining route is a bookmark or a shared URL
- [x] **EVTNAME-06**: The two header cross-buttons between Board Availability and My Agenda render only for a Dungeon Master, in both directions and on both layouts, while the unconditional My Agenda entry in each layout's user menu stays visible to every authenticated user
- [x] **EVTNAME-07**: Board Availability itself keeps no authorization gate — a player who reaches it still receives a normal 200 and the full grid, proven by an automated case rather than assumed from the absence of an attribute

### Link Previews — Foundation and Quests

- [ ] **LINKPREV-01**: The app generates correct absolute URLs behind the reverse proxy, honouring forwarded scheme and host
- [ ] **LINKPREV-02**: A "Copy shareable link" control on the quest Details page, desktop and mobile, mints a signed link and copies it to the clipboard
- [ ] **LINKPREV-03**: A quest URL carrying a valid signature serves Open Graph and Twitter Card meta tags so Discord, Slack, and iMessage render a rich card
- [ ] **LINKPREV-04**: A quest URL with no signature behaves exactly as it does today — no card, no quest data served to an unauthenticated caller
- [ ] **LINKPREV-05**: A tampered, malformed, or otherwise invalid signature is rejected and renders no card
- [ ] **LINKPREV-06**: The card description is plain text derived from the quest's Markdown — syntax stripped, whitespace collapsed, truncated, HTML-escaped
- [ ] **LINKPREV-07**: The signed preview read path is scoped to the signature's own verified group and cannot serve data from any other board
- [ ] **LINKPREV-08**: A branded fallback card image is served unauthenticated at an absolute URL, with no redirect
- [ ] **LINKPREV-09**: A valid signature grants card metadata only — never page access, never the ability to sign up or post

### Link Previews — Characters and Contacts

- [ ] **LINKCARD-01**: A "Copy shareable link" control on the character Details page, desktop and mobile
- [ ] **LINKCARD-02**: A "Copy shareable link" control on the contact Details page, unavailable while the contact is unrevealed
- [ ] **LINKCARD-03**: A signed contact link renders no card while the contact is unrevealed, evaluated when the card is served rather than when the link was minted
- [ ] **LINKCARD-04**: Character and contact portraits are served at a signed, unauthenticated image endpoint with an explicit content type and `X-Content-Type-Options: nosniff`
- [ ] **LINKCARD-05**: The signed image endpoint enforces a size cap and cache headers, and falls back to the branded image when the entity has no portrait
- [ ] **LINKCARD-06**: The character card shows name, level, and class; the contact card shows name and location — and neither exposes backstory, description, or notes

### Contact Categories

- [x] **CONTACTCAT-01**: A DungeonMaster-tier user can create a named category from a dedicated Manage Categories page reached from a button on the Contacts index, and the category belongs to the board it was created on
- [x] **CONTACTCAT-02**: A contact belongs to exactly one category or to none, assigned from a single dropdown with a blank "— None —" option on the contact Create and Edit forms on both desktop and mobile
- [x] **CONTACTCAT-03**: A DungeonMaster-tier user can rename and delete a category, and deleting a category that still holds contacts moves those contacts to Ungrouped rather than deleting them or blocking the delete
- [x] **CONTACTCAT-04**: Category names are unique per board, compared case-insensitively, and a duplicate submission returns a validation message on the form rather than an unhandled server error
- [x] **CONTACTCAT-05**: Every category read and write is scoped to the active board by the global query filter, a request with no active board resolves zero categories, and no application code path bypasses the filter
- [x] **CONTACTCAT-06**: Only DungeonMaster-tier users can create, rename, delete, or reorder categories, enforced server-side so a player who guesses the URL is refused
- [x] **CONTACTCAT-07**: A DungeonMaster-tier user can reorder categories with up and down controls on the Manage Categories page, and the Contacts index renders headings in that order rather than alphabetically
- [x] **CONTACTCAT-08**: The Manage Categories page ships as both a desktop and a mobile view, and the mobile view is proven to be the one actually selected under a real mobile User-Agent
- [x] **CONTACTCAT-09**: The Contacts index renders contacts under their category headings on both desktop and mobile, with contacts sorted alphabetically by name within each heading
- [x] **CONTACTCAT-10**: Contacts with no category render under a synthetic "Ungrouped" heading that is pinned after every real category and is neither renameable nor orderable
- [x] **CONTACTCAT-11**: A board with no categories renders the flat contact list exactly as it renders today, with no category headings at all including no Ungrouped heading
- [x] **CONTACTCAT-12**: A category heading renders only when at least one contact beneath it is visible to the viewer, and the heading carries the category name alone with no contact count
- [x] **CONTACTCAT-13**: A category name is stored with a 60-character cap and rendered as plain escaped text, never routed through the Markdown pipeline
- [x] **CONTACTCAT-14**: A contact's category is shown on the contact Details view on both desktop and mobile, and a contact with no category shows no category line at all
- [x] **CONTACTCAT-15**: On a board with no categories, the contact Create and Edit forms render the category select disabled with helper text linking to the Manage Categories page

### Contact Tags and Filtering

- [ ] **CONTACTTAG-01**: Every tag surface — chips, the filter control, the details tag line, and the tag-entry field — renders only for a DM-tier viewer, and a player-tier response contains no tag markup at all on either the desktop or the mobile layout
- [ ] **CONTACTTAG-02**: Contacts carry free-form tags through a board-scoped ContactTag entity joined many-to-many to contacts, not a second category column and not a per-contact name string
- [ ] **CONTACTTAG-03**: Tag names are unique per board and compared case-insensitively, so typing a case variant of an existing tag reuses that row instead of minting a twin
- [ ] **CONTACTTAG-04**: Tag reads and writes are scoped to the viewer's active board by a fail-closed query filter that returns zero tags when no board is active, and no code path in this feature bypasses that filter
- [ ] **CONTACTTAG-05**: Tag rows are created by free-typing on the contact form and pruned automatically when the last contact drops them, on both contact save and contact delete; there is no management page and no rename path
- [ ] **CONTACTTAG-06**: An unknown, deleted, or other-board tag id supplied in the filter query string silently matches nothing rather than producing an error or a not-found response
- [ ] **CONTACTTAG-07**: Selecting several tags returns the union of their contacts, not the intersection
- [ ] **CONTACTTAG-08**: Filter selection lives in the URL query string as repeated tag ids rather than in session state
- [ ] **CONTACTTAG-09**: The tag filter is applied in memory after the existing contact visibility gate, so it can only narrow what the viewer could already see and can never surface a contact that gate excluded
- [ ] **CONTACTTAG-10**: The filter's tag list is derived from the viewer's visible-but-unfiltered contact set, so a tag borne only by contacts the viewer cannot see never appears, and selecting one tag does not remove the rest from the list
- [ ] **CONTACTTAG-11**: Toggling Show Hidden preserves the active tag selection across the post-redirect round trip
- [ ] **CONTACTTAG-12**: All four contact create and edit views offer a chips-and-typeahead tag field backed by a version-pinned, integrity-checked CDN library and a thin local init module
- [ ] **CONTACTTAG-13**: The tag field is a real text input holding a comma-separated list, and the server parses that one value shape whether or not the client script ran — trimming, dropping empties, and de-duplicating case-insensitively
- [ ] **CONTACTTAG-14**: Tags render as chips on both contact index layouts and as a muted line on both contact details layouts, always as plain escaped text and never through the Markdown renderer
- [ ] **CONTACTTAG-15**: Before a board has any tags the filter control still renders, disabled, with helper text pointing at the contact form; once tags exist, a filter matching nothing shows a distinct no-match message with a clear-filters action, leaving the genuinely-empty-list message unchanged
- [ ] **CONTACTTAG-16**: The desktop filter is an inline get-form with checkboxes and apply and clear actions, the mobile filter is a bottom drawer behind a filter button, both ship in the same phase, and the mobile markup is proven under a real mobile user agent rather than viewport emulation
- [ ] **CONTACTTAG-17**: The tag filter is applied before any contact grouping step, so a later category-heading pass groups the already-filtered set and empty headings drop out without a second rendering mode

## Future Requirements

Deferred — revisit if the need becomes real.

- [ ] **EVTNOTIFY-01**: Notify board members by email when an event occurrence is cancelled or moved — deliberately deferred out of the opening scope. This is a genuine two-sided trade-off, not an obvious no: `QuestDateChangedEmailJob` is direct precedent *for* notifying on a moved date, while Phase 36 deliberately engineered Campaign boards to fire no scheduling email at all. Decide once real Campaign usage exists rather than guessing at rate-limit impact up front.
- [ ] **EVTRECUR-09**: Regenerate untouched future occurrences when a series rule is edited. Initial behaviour is additive-only — an edited rule never retroactively deletes or rewrites occurrences that already exist, especially ones people have voted on.
- [ ] **EVENT-07**: A Campaign-board event that skips auto-signup (e.g. "holiday, no session") — a mixed-purpose board case outside the current scope
- [ ] **SIGNCHAR-08**: A "recently changed" indicator on the DM's Manage page, showing that a player swapped character since finalization — surfaced during research as the cheap alternative to an email notification; the operator chose no notification for v9.0, and this edges toward the audit-trail exclusion below
- [ ] Digest batching for session reminders — single combined email when a player has multiple same-day quests (EMAIL-04 / REMIND-02, deferred since v4.0; same-day quests have never occurred in over a year)
- [ ] Markdown toolbar extras — strikethrough, horizontal rule, cheatsheet link (EDITOR-07/08/09, deferred at v8.0 close; add only if users ask)

## Out of Scope

Explicit exclusions for v9.0, with reasoning.

- **DM-side character editing on the Quest Manage page** — the operator scoped this out. `UpdateSignupCharacter` only ever edits the caller's own signup, so this would need a new authorized action plus its own cross-tenant test surface. A separate feature, not a corner of this one.
- **Email notification to the DM on character swap** — operator decision, backed by research. The relay budget is 100/day and no existing edit feature (Phase 61 finalized-quest edit, Phase 63 recap edit) fires email. A character swap does not change attendance, and the DM's read surface on Details/Manage is already live and authoritative.
- **Audit trail / change history on signup fields** — no field-level audit logging exists anywhere in this app. Adding one bespoke audited field would be inconsistent with every other mutable field, and disproportionate for a 17-person trusted group. If this ever becomes a real ask it should be scoped as its own cross-cutting concern, applied uniformly.
- **A confirmation dialog before applying a swap** — the change is trivially reversible, affects no other player, and fires no email. The modal-open-then-Save flow is already the same friction as the existing add-character flow, which has never had a confirm step.
- **Restricting the change to Players only** — would be a net-new inconsistency. `JoinFinalizedQuest` already lets Spectators and AssistantDMs pick a character at signup, and the existing action has no role check.
- **Auto-clearing an inactive character server-side** — surprising and undiscoverable. SIGNCHAR-04 keeps the decision with the player instead.
- **Adding a `.github/dependabot.yml`** — none exists today. It governs Dependabot *version-update PRs*, not *alerts*, so it would have no effect on alert staleness.
- **Bumping or adding any NuGet package for the security alerts** — `System.Security.Cryptography.Xml` is absent from every tracked `.csproj` and from the transitive graph. There is nothing to bump.
- **An `EventType` field on events** — the distinction between "a day is blocked" and "this is a play session" derives entirely from the board's already-immutable `BoardType`. A second discriminator would duplicate it and let the two disagree. Matches the existing `CloseQuestAsync`/`ReopenQuestAsync` vs `FinalizeQuestAsync`/`OpenQuestAsync` split.
- **Any relation between an Event and a Quest entity** — events are informational by definition. A foreign key would invite exactly the blocking semantics that were explicitly ruled out.
- **An RRULE / iCalendar library for recurrence** — RFC 5545's `BYSETPOS` selects only within a single interval, not across periods, so a repeating on/off mask riding a base cadence is not expressible. Any library would need post-filtering anyway, adding a dependency for negative benefit.
- **`DateTimeOffset` or UTC storage for occurrence dates** — an event date is a calendar date, not an instant. `DateOnly` makes the DST bug class structurally impossible and avoids introducing a second timezone strategy alongside the existing `FinalizedDate` convention.
- **A client-side calendar component or JS calendar library** — the existing hand-rolled Razor month grid is fused with quest vote-button and signup rendering, not a generic widget. Events extend it; they do not justify replacing it.
- **Cross-board collision warnings when configuring a recurring series** — flagging that a generated date collides with an event on another board would be noise for boards that have nothing to do with each other, and would train people to ignore the warning. The date preview covers the real need.
- **A shared cadence entity spanning two boards** — this would make interleaving structurally correct rather than configuration-dependent, but a board *is* a group, so it would cut through the tenant isolation model that has already leaked twice (v7.0, Phases 49/55). Not worth the trade.
- **A Spotify-style interactive iframe embed** — Discord and Slack render interactive players only for providers on their own hardcoded allowlists. A self-hosted app cannot join one at any effort level. The achievable target is the standard rich card, and scoping toward an embed would be chasing something that does not exist for us.
- **Public, unsigned link previews** — a card that renders for any URL would make every quest title and description snippet readable by anyone holding a link, and Discord and Slack cache the result server-side. Considered and rejected in favour of signed share links, which make sharing a deliberate act.
- **A board-level "allow link previews" toggle** — one switch governing every quest on a board is coarser than the risk warrants, and the link itself would carry no evidence that anyone meant to share it.
- **Server-generated per-quest card images** — rendering title, challenge rating, and date onto a parchment background needs a new imaging dependency for pure polish. A branded static fallback carries the feature; revisit only if the cards prove useful.
- **Accepting a preview signature as authentication** — the token unlocks card metadata and nothing else. Honouring it for page access or any POST would turn a shared link into a permanent unauthenticated door into a private board.

## Traceability

| Requirement | Phase | Status |
|-------------|-------|--------|
| SIGNCHAR-01 | Phase 72 | Complete |
| SIGNCHAR-02 | Phase 72 | Complete |
| SIGNCHAR-03 | Phase 72 | Complete |
| SIGNCHAR-04 | Phase 72 | Complete |
| SIGNCHAR-05 | Phase 72 | Complete |
| SIGNCHAR-06 | Phase 72 | Complete |
| SIGNCHAR-07 | Phase 72 | Complete |
| SECALERT-01 | Phase 73 | Complete |
| SECALERT-02 | Phase 73 | Complete |
| SECALERT-03 | Phase 73 | Complete |
| SECALERT-04 | Phase 73 | Complete |
| SECALERT-05 | Phase 73 | Complete |
| EVENT-01 | Phase 74 | Complete |
| EVENT-02 | Phase 74 | Complete |
| EVENT-03 | Phase 74 | Complete |
| EVENT-04 | Phase 74 | Complete |
| EVENT-05 | Phase 74 | Complete |
| EVENT-06 | Phase 74 | Complete |
| EVTAVAIL-01 | Phase 75 | Complete |
| EVTAVAIL-02 | Phase 75 | Complete |
| EVTAVAIL-03 | Phase 75 | Complete |
| EVTAVAIL-04 | Phase 75 | Complete |
| EVTAVAIL-05 | Phase 75 | Complete |
| EVTRECUR-01 | Phase 76 | Complete |
| EVTRECUR-02 | Phase 76 | Complete |
| EVTRECUR-03 | Phase 76 | Complete |
| EVTRECUR-04 | Phase 76 | Complete |
| EVTRECUR-05 | Phase 76 | Complete |
| EVTRECUR-06 | Phase 76 | Complete |
| EVTRECUR-07 | Phase 76 | Complete |
| EVTRECUR-08 | Phase 76 | Complete |
| EVTVIEW-01 | Phase 77 | Complete |
| EVTVIEW-02 | Phase 77 | Complete |
| EVTVIEW-03 | Phase 77 | Complete |
| EVTVIEW-04 | Phase 77 | Complete |
| LINKPREV-01 | Phase 78 | Not started |
| LINKPREV-02 | Phase 78 | Not started |
| LINKPREV-03 | Phase 78 | Not started |
| LINKPREV-04 | Phase 78 | Not started |
| LINKPREV-05 | Phase 78 | Not started |
| LINKPREV-06 | Phase 78 | Not started |
| LINKPREV-07 | Phase 78 | Not started |
| LINKPREV-08 | Phase 78 | Not started |
| LINKPREV-09 | Phase 78 | Not started |
| LINKCARD-01 | Phase 79 | Not started |
| LINKCARD-02 | Phase 79 | Not started |
| LINKCARD-03 | Phase 79 | Not started |
| LINKCARD-04 | Phase 79 | Not started |
| LINKCARD-05 | Phase 79 | Not started |
| LINKCARD-06 | Phase 79 | Not started |
| EVTAGENDA-01 | Phase 82 | Not started |
| EVTAGENDA-02 | Phase 82 | Not started |
| EVTAGENDA-03 | Phase 82 | Not started |
| EVTAGENDA-04 | Phase 82 | Not started |
| EVTAGENDA-05 | Phase 82 | Not started |
| EVTAGENDA-06 | Phase 82 | Not started |
| EVTAGENDA-07 | Phase 82 | Not started |
| EVTAGENDA-08 | Phase 82 | Not started |
| EVTAGENDA-09 | Phase 82 | Not started |
| EVTAGENDA-10 | Phase 82 | Not started |
| CONTACTCAT-01 | Phase 80 | Not started |
| CONTACTCAT-02 | Phase 80 | Not started |
| CONTACTCAT-03 | Phase 80 | Not started |
| CONTACTCAT-04 | Phase 80 | Not started |
| CONTACTCAT-05 | Phase 80 | Not started |
| CONTACTCAT-06 | Phase 80 | Not started |
| CONTACTCAT-07 | Phase 80 | Not started |
| CONTACTCAT-08 | Phase 80 | Not started |
| CONTACTCAT-09 | Phase 80 | Not started |
| CONTACTCAT-10 | Phase 80 | Not started |
| CONTACTCAT-11 | Phase 80 | Not started |
| CONTACTCAT-12 | Phase 80 | Not started |
| CONTACTCAT-13 | Phase 80 | Not started |
| CONTACTCAT-14 | Phase 80 | Not started |
| CONTACTCAT-15 | Phase 80 | Not started |
| EVTNAME-01 | Phase 83 | Not started |
| EVTNAME-02 | Phase 83 | Not started |
| EVTNAME-03 | Phase 83 | Not started |
| EVTNAME-04 | Phase 83 | Not started |
| EVTNAME-05 | Phase 83 | Not started |
| EVTNAME-06 | Phase 83 | Not started |
| EVTNAME-07 | Phase 83 | Not started |
| CONTACTTAG-01 | Phase 81 | Not started |
| CONTACTTAG-02 | Phase 81 | Not started |
| CONTACTTAG-03 | Phase 81 | Not started |
| CONTACTTAG-04 | Phase 81 | Not started |
| CONTACTTAG-05 | Phase 81 | Not started |
| CONTACTTAG-06 | Phase 81 | Not started |
| CONTACTTAG-07 | Phase 81 | Not started |
| CONTACTTAG-08 | Phase 81 | Not started |
| CONTACTTAG-09 | Phase 81 | Not started |
| CONTACTTAG-10 | Phase 81 | Not started |
| CONTACTTAG-11 | Phase 81 | Not started |
| CONTACTTAG-12 | Phase 81 | Not started |
| CONTACTTAG-13 | Phase 81 | Not started |
| CONTACTTAG-14 | Phase 81 | Not started |
| CONTACTTAG-15 | Phase 81 | Not started |
| CONTACTTAG-16 | Phase 81 | Not started |
| CONTACTTAG-17 | Phase 81 | Not started |

**Coverage:**

- v1 requirements: 99 total
- Mapped to phases: 99/99 ✓
- Unmapped: 0

---
*Requirements defined: 2026-08-25*
