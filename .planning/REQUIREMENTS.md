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

- [ ] **EVENT-01**: A DM can create an event on their board with a title, an optional description, a date, and an optional start time
- [ ] **EVENT-02**: A DM can edit and delete events on their own board; events are scoped to that board and never visible to another
- [ ] **EVENT-03**: Events appear on the desktop calendar page, visually distinguishable from quests at a glance
- [ ] **EVENT-04**: Events appear on the mobile calendar page, which today lists only days that have quests
- [ ] **EVENT-05**: Events never appear on the quest board main page and never block or constrain quest creation — they are informational only
- [ ] **EVENT-06**: "Create Event" sits in the same navbar category as "Create Quest" and is available to all DM roles

### Calendar Events — Availability

- [ ] **EVTAVAIL-01**: On a One-Shot board, a player can optionally sign up to an event and record their availability as Yes, Maybe, or No — with no signup created unless they choose to
- [ ] **EVTAVAIL-02**: On a Campaign board, every board member is automatically signed up to each event with availability Yes, and opts out by changing their own answer to No rather than by removing the signup
- [ ] **EVTAVAIL-03**: A player can change their availability on an event at any time
- [ ] **EVTAVAIL-04**: A member who joins a Campaign board after events already exist is auto-signed-up to the future events, and a member who leaves keeps their past answers while their future auto-signups are removed
- [ ] **EVTAVAIL-05**: A player cannot see or change availability for an event on a board they are not a member of, proven by an automated test using two distinct groups

### Calendar Events — Recurrence

- [ ] **EVTRECUR-01**: A DM can make an event recur by setting a base cadence (every N weeks on a given weekday), an anchor date, and a repeating on/off cycle mask — so "two sessions on, two off" is expressible directly
- [ ] **EVTRECUR-02**: While configuring a series, the DM sees a live preview of the next ~10 dates it will generate, before saving
- [ ] **EVTRECUR-03**: Occurrences are generated ahead of time on a rolling window and topped up automatically, so an open-ended campaign never needs manual re-extension
- [ ] **EVTRECUR-04**: A DM can cancel a single occurrence without affecting the rest of the series
- [ ] **EVTRECUR-05**: A DM can move a single occurrence to a different date without affecting the rest of the series
- [ ] **EVTRECUR-06**: A DM can edit a single occurrence's details without affecting the rest of the series
- [ ] **EVTRECUR-07**: Re-running the occurrence generator never duplicates an existing occurrence, resurrects a cancelled one, or overwrites one that was moved or edited
- [ ] **EVTRECUR-08**: Two boards can be configured with mirrored cycle masks on the same cadence and anchor so their sessions interleave rather than collide

### Calendar Events — Availability Overview

- [ ] **EVTVIEW-01**: A new page shows upcoming events for the current board as a grid of events against players, with each player's availability
- [ ] **EVTVIEW-02**: The overview visually distinguishes an untouched default from an answer the player actually gave, so "available" is not confused with "never looked"
- [ ] **EVTVIEW-03**: The overview shows a per-event availability count, so a poorly-attended date is obvious at a glance
- [ ] **EVTVIEW-04**: The overview never displays events or members from another board

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
| EVENT-01 | Phase 74 | Not started |
| EVENT-02 | Phase 74 | Not started |
| EVENT-03 | Phase 74 | Not started |
| EVENT-04 | Phase 74 | Not started |
| EVENT-05 | Phase 74 | Not started |
| EVENT-06 | Phase 74 | Not started |
| EVTAVAIL-01 | Phase 75 | Not started |
| EVTAVAIL-02 | Phase 75 | Not started |
| EVTAVAIL-03 | Phase 75 | Not started |
| EVTAVAIL-04 | Phase 75 | Not started |
| EVTAVAIL-05 | Phase 75 | Not started |
| EVTRECUR-01 | Phase 76 | Not started |
| EVTRECUR-02 | Phase 76 | Not started |
| EVTRECUR-03 | Phase 76 | Not started |
| EVTRECUR-04 | Phase 76 | Not started |
| EVTRECUR-05 | Phase 76 | Not started |
| EVTRECUR-06 | Phase 76 | Not started |
| EVTRECUR-07 | Phase 76 | Not started |
| EVTRECUR-08 | Phase 76 | Not started |
| EVTVIEW-01 | Phase 77 | Not started |
| EVTVIEW-02 | Phase 77 | Not started |
| EVTVIEW-03 | Phase 77 | Not started |
| EVTVIEW-04 | Phase 77 | Not started |
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

**Coverage:**

- v1 requirements: 50 total
- Mapped to phases: 50/50 ✓
- Unmapped: 0

---
*Requirements defined: 2026-08-25*
