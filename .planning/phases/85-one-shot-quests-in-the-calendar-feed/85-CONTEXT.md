# Phase 85: One-Shot Quests in the Calendar Feed - Context

**Gathered:** 2026-09-18
**Status:** Ready for planning

<domain>
## Phase Boundary

A second source — one-shot quest sessions — added to the personal `text/calendar` feed Phase 84 shipped. No new endpoint, no new token, no new page, no new Profile control: the same subscription address starts carrying sessions alongside events.

Quests are narrowed to boards where `BoardType` is `OneShot`. Events keep the reach Phase 84 gave them across every board — this phase narrows quests only and must not retroactively restrict events.

**This phase stores nothing new.** Every decision below is output scoping, a writer literal, or a configuration default. No entity, no migration, no schema change.

</domain>

<decisions>
## Implementation Decisions

### Locked before this discussion — do not relitigate

Settled by the operator during Phase 84's discuss pass and recorded in the Phase 85 roadmap entry. Listed here so downstream agents do not mistake them for open ground:

- **One-shot boards only.** A campaign board's quests never reach the feed.
- **Only quests the reader is signed up for.** Not every quest on their one-shot boards.
- **Only once the quest is finalized** (`FinalizedDate` set). Proposed dates never reach the feed; a date vote in progress is not a commitment.

### Which quests reach the feed

- **D-01: A session the reader is running as DM reaches their feed, via a second predicate on `DungeonMasterId`.** A DM holds no `PlayerSignup` row on their own quest, so the signup-rooted query alone would give the person doing most of the board's scheduling the emptiest calendar. The quest read is therefore a union of two branches — signup rows for the owner, plus quests where `DungeonMasterId` is the owner — not the single shape the event feed uses.

  **Structural consequence, load-bearing:** the two branches can both match the same quest if a DM also holds a signup row on it. The `UID` is derived from the quest id alone (D-07 keeps 84 D-12's scheme), so an un-deduplicated union emits two VEVENTs sharing one `UID` — undefined behaviour on the reader's phone. A distinct-by-quest-id step is mandatory, not an optimisation.

  — **Reversibility:** reversible — output scoping only, no stored data. Removing the branch changes what appears at the next poll, with no migration.

- **D-02: Confirmed seats only — `IsSelected == true`. A waitlisted signup never reaches the feed.** A row is not a seat. Waitlisted players are auto-promoted as seats free up, right up to game night; a promotion reaches the phone at the next poll like any other change.

  **Accepted cost, stated at the time:** a waitlisted player gets no advance warning of a night they may well end up playing.

  **Consequence:** this removes the roadmap's "whether the vote-marker convention carries over" question entirely. There are no waitlisted entries left to mark, so no quest suffix is needed and none is added (see D-07).

  — **Reversibility:** reversible — a predicate.

- **D-03: All three signup roles count — Player, Spectator and AssistantDM.** No `SignupRole` branch in the predicate.

  This falls out of D-02 at zero cost: `IsSelected` is set `true` unconditionally for Spectator and AssistantDM signups, and only a Player ever lands on the waitlist (`QuestController.cs:482`). Everyone with an approved seat is at the table that night, whatever the seat is called, which is exactly what a "when am I busy" surface is for.

  — **Reversibility:** reversible — adding the role predicate is one clause.

- **D-04: A quest flagged `DungeonMasterSession` is not filtered out. The seat is the authority.** No `DungeonMasterSession` clause in the query.

  Checked against the code before the question was put: the flag hides a quest from the **board listing** (`QuestRepository.cs:73`) but `QuestController.Details` applies no gate at all (`QuestController.cs:307`) — anyone holding the URL can open one today. It is a discoverability hide, not an access control, so honouring a granted seat matches what the application already does rather than widening anything.

  **Accepted cost, stated:** a DM who flips the flag *after* players signed up leaves those readers' phones carrying a title the board no longer lists for them.

  **Scope note:** `DungeonMasterSession` is force-cleared on campaign boards (`QuestController.cs:109`, `:256`), so it is meaningful only on the one-shot boards this phase covers. It is live here, not theoretical.

  — **Reversibility:** reversible — adding `!q.DungeonMasterSession || q.DungeonMasterId == ownerId` is one clause.

### How a session occupies the calendar

- **D-05: A quest is a fixed four-hour block starting at `FinalizedDate`, with the duration configurable on `CalendarFeedOptions`.** Four hours is the operator's own number for a full evening session, chosen deliberately rather than left to the planner because it is visible on every row of every subscriber's phone.

  A quest is therefore **always a timed entry and never all-day** — unlike an event, `FinalizedDate` is a full `DateTime` and always carries a real start time, so the writer's null-`StartTime` all-day branch is unreachable for this source.

  **The four hours is invented**, the same way the event hour is (84 D-02). Nothing in the schema records when a session ends. It must not be presented anywhere in the application UI as though the board knows the duration, and the configuration key must be reachable without a code change in the manner of the existing window bounds.

  Rejected: reusing the event hour (renders a game night as a sliver on a phone month view, understating the exact commitment this phase exists to surface); a true all-day entry (sidesteps inventing a duration, but throws away the start time the board genuinely does know — the one fact a player checks before game night).

  — **Reversibility:** reversible — a new property on `CalendarFeedOptions` is additive, and the default is a literal.

- **D-06: Every quest entry sets `TRANSP` to `TRANSPARENT`, matching events.** One transparency rule for the whole feed; no per-source branch.

  The operator was offered `OPAQUE` on the reasoning that a session is a real commitment while an event is informational, and declined it. 84 D-03's reasoning holds for both sources: the duration is invented, so marking the reader busy for four hours publishes a fiction to anyone checking their availability.

  **Accepted cost, stated:** a subscription that never blocks anything is weaker as a scheduling tool — nobody looking at the reader's calendar sees game night as busy.

  — **Reversibility:** reversible — one literal in the writer.

### What each entry says

- **D-07: `[Board] Title`, unchanged from 84 D-09. No marker identifying an entry as a quest.** `[Sunday Group] The Lost Mine of Phandelver` already reads as a session, and a marker adds width to a title that a narrow phone day view truncates anyway. The writer's `SUMMARY` rule stays one rule for both sources.

  Rejected: a suffix following 84 D-18's shape (costs a per-source branch for something the title already conveys); a prefix marker (84 D-18 explicitly refused a second prefix because it can consume a narrow day view's entire visible width before the title begins).

  — **Reversibility:** reversible.

- **D-08: A session the reader is DMing reads identically to one they are playing. No `(DM)` suffix.** One `SUMMARY` rule for every quest row, with no branch on why the row is there. Consistent with D-07: the title carries the meaning.

  **Accepted cost:** a DM reading their own calendar cannot tell a night they run from a night they play, on a board where they do both.

  — **Reversibility:** reversible.

- Inherited unchanged from Phase 84 and **not reopened**: no `DESCRIPTION` (84 D-10), no `URL` or link of any kind (84 D-11), no `VALARM` (84 D-04), floating local time with no `TZID` (84 D-01).

### When a session leaves the feed

- **D-09: A quest that stops qualifying simply disappears. No `STATUS` of `CANCELLED`.** Same rule as 84 D-12 for cancelled events, applied to a different model.

  A one-shot quest's exits are: the DM moves the finalized date, the quest is un-finalized back to voting, the quest is deleted, or the reader's seat goes away. The predicate does all the work in each case — no finalized date, no seat, no row, nothing to emit. No tombstone state to store and no `STATUS` branch in the writer.

  **Accepted cost, the same one 84 accepted:** a cancellation reaches the subscriber only as a silent disappearance, and a reader is unlikely to notice an entry they are no longer looking for — which is most acute for a called-off game night.

  **The roadmap's "or a quest is closed" question is moot.** `Close` is campaign-only and rejects with `BadRequest` on a one-shot board (`QuestController.cs:760`), so `IsClosed` is unreachable for every quest this phase can emit. No `IsClosed` clause is needed and adding one would be dead code.

  — **Reversibility:** reversible — a predicate.

- **D-10: Quests use the same rolling window as events** — the existing `CalendarFeedOptions.MonthsBack` / `MonthsAhead`. Three months of history already answers "when did we last play?", and a second pair of knobs means two windows in one document to reason about.

  Rejected: a separate, longer history for sessions (a feed that grows with every campaign year, and a second knob to get wrong).

  — **Reversibility:** reversible.

### Claude's Discretion

- **Where the one-shot narrowing happens.** `GroupRepository.GetGroupsForUserAsync` already returns `BoardType` per board (`GroupRepository.cs:39`), and `CalendarSubscriptionService.GetFeedAsync` already calls it to build the board-name map. The one-shot group-id set is derivable from data the feed path holds in hand — no extra read, and `IBoardTypeResolver` must not be touched (it resolves the *active* group and returns null here by construction).
- **Query shape and where it lives** — a new method on the quest or player-signup repository, and how the two D-01 branches are expressed (a union, two queries, or one `||`). Mandatory regardless of shape: membership scoping with the pinned set, the `IgnoreQueryFilters()` comment discipline, and the second-layer in-memory re-check with `LogError` that `CalendarSubscriptionService.GetFeedAsync` already performs for events.
- **Membership and board type are two independent predicates** and both must apply. Filtering on board type and trusting the tenant filter for membership — or the reverse — produces either a leak or a silently empty section. The existing re-check covers membership; board type needs its own.
- **Deduplication of the D-01 union** by quest id, before entries reach the writer.
- **Extending `CalendarFeedSource`** with a `Quest` member. The enum's own comment already names this as the reason it exists; `BuildUid` needs no change.
- **`CalendarFeedEntry`'s shape for a source with no vote.** `Availability` is a bare `VoteType` today and `BuildSummary` branches on it unconditionally. Making it nullable, defaulting it, or branching on `Source` are all open — but a quest must never pick up a `(maybe)` or `(declined)` suffix by accident.
- **The window comparison's time basis.** The feed computes `today` from `timeProvider.GetUtcNow().UtcDateTime`, events compare against a naive `DateOnly`, and `FinalizedDate` is a `DateTime` stored in **server local time** (a standing known issue in `PROJECT.md`). Decide the comparison explicitly; do not silently mix the two.
- **The new duration option's validation** in `CalendarFeedOptions.IsValid()`, following the existing refuse-to-start pattern.
- **Minting the requirement family.** Phase 85's `Requirements:` is `TBD`; Phase 84 minted `CALFEED-01`–`17` into `REQUIREMENTS.md` and `ROADMAP.md` in its own first plan and this phase should follow that precedent.

### Inherited assumptions — settled, not reopened

- **`SEQUENCE:0` stays constant.** 84's research established that a plain-`PUBLISH` feed with no `METHOD` is replaced by `UID` on each client refetch rather than gated by iTIP-style sequence comparison, so a moved finalized date updates in place. `QuestEntity` has no modified timestamp any more than `EventEntity` does, so nothing changes here. This is 84-RESEARCH's Assumption A5 and its own least-certain claim — if a stale-entry report ever surfaces, it is the first thing to re-test, for both sources at once.
- **The real-device subscription check is still outstanding.** Phase 84 deferred it: Outlook and Google fetch server-side, so no localhost or LAN address satisfies them. Phase 85 inherits that gap rather than closing it, and must not claim a refresh latency it has not observed.

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### This phase and the one it extends
- `.planning/ROADMAP.md` § Phase 85 — goal, scope notes, the operator-locked constraints, and the three named risks. Note that D-02 removes its vote-marker question and D-09 removes its "quest is closed" question.
- `.planning/ROADMAP.md` § Phase 84 — the foundation this builds on.
- `.planning/phases/84-calendar-feed-foundation-and-event-subscription/84-CONTEXT.md` — **authoritative** for D-01 (floating local time), D-02/D-03 (duration and transparency), D-09 (title prefix), D-10/D-11 (no description, no link), D-12 (silent disappearance and the `UID` rule), D-13 (the window), D-17 (the signup-rooted query shape), D-18 (the suffix convention).
- `.planning/phases/84-calendar-feed-foundation-and-event-subscription/84-RESEARCH.md` — Pitfall 4 and Assumption A5 (`SEQUENCE` on a published feed), RFC 5545 folding and escaping mechanics, client-behaviour confidence levels.
- `.planning/phases/84-calendar-feed-foundation-and-event-subscription/84-VERIFICATION.md` — the deferred real-device check this phase inherits.

### Safety pattern this phase must reproduce
- `.planning/phases/82-personal-cross-board-event-agenda/82-CONTEXT.md` — D-14/D-15/D-16 (the `IgnoreQueryFilters()` + pinned-membership-set query pattern), D-17 (the tenant-isolation test shape a new cross-board read needs).

### Project-level
- `.planning/REQUIREMENTS.md` § CALFEED-01–17 — the shipped family; Phase 85 mints its own.
- `.planning/PROJECT.md` § Constraints, § Context — including the standing `FinalizedDate`-is-server-local-time known issue.
- `.planning/codebase/CONVENTIONS.md` — naming, async, migration conventions.
- `CLAUDE.md` — CRLF for source files, EF packages confined to `QuestBoard.Repository`, no planning IDs in source comments.

### Code this phase builds on
- `QuestBoard.Domain/Services/CalendarSubscriptionService.cs:52` — `GetFeedAsync`, the composition point: membership read, window computation, second-layer re-check, `LogError`, entry projection, ETag. The quest source plugs in here.
- `QuestBoard.Repository/EventSignupRepository.cs:76` — `GetFeedRowsForUserAsync`, the signup-rooted query and its safety comment block. The model for the new quest query's safety, not its shape.
- `QuestBoard.Domain/Services/CalendarFeedWriter.cs` — `BuildSummary`, `BuildUid`, the timed and all-day branches, folding and escaping.
- `QuestBoard.Domain/Enums/CalendarFeedSource.cs` — the single-member enum whose comment names this phase as its reason for existing.
- `QuestBoard.Domain/Models/CalendarFeedEntry.cs` — `Availability` is a bare `VoteType` today.
- `QuestBoard.Domain/Models/CalendarFeedOptions.cs` — window bounds, throttle, retention, and `IsValid()`.
- `QuestBoard.Repository/GroupRepository.cs:29` — the membership read that already carries `BoardType`.
- `QuestBoard.Repository/Entities/QuestEntity.cs` — `FinalizedDate`, `IsFinalized`, `DungeonMasterId`, `DungeonMasterSession`, `CreatedAt` (the stable `DTSTAMP` source).
- `QuestBoard.Repository/Entities/PlayerSignupEntity.cs` — `IsSelected`, `SignupRole`.
- `QuestBoard.Service/Controllers/QuestBoard/QuestController.cs:482` — `IsSelected` set by role; `:760` — `Close` rejected on a one-shot board; `:307` — `Details` applies no `DungeonMasterSession` gate.

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `CalendarSubscriptionService.GetFeedAsync` — already performs the membership read, the window computation, the second-layer re-check with `LogError`, the writer call, the ETag hash and the throttled last-fetched write. The quest source is an addition inside it, not a parallel path.
- `GroupRepository.GetGroupsForUserAsync` — already returns `BoardType` per board and is already called on this path. The one-shot narrowing needs no new read and no `IBoardTypeResolver`.
- `EventSignupRepository.GetFeedRowsForUserAsync` — not reusable as-is (different entity, and D-01 makes the quest read a union rather than a single rooted query), but its `IgnoreQueryFilters()` + pinned-set comment block is the safety pattern to reproduce verbatim in spirit.
- `CalendarFeedWriter` — the timed branch, `BuildUid`, folding and escaping all apply unchanged. Only the duration literal and the `Availability` suffix branch need to become source-aware.
- `CalendarFeedOptions` — the pattern for D-05's configurable duration, including the refuse-to-start `IsValid()` guard.

### Established Patterns
- Three-layer Service → Domain → Repository with AutoMapper at both boundaries; EF packages only in `QuestBoard.Repository`.
- Tenant safety is defence in depth: a scoped query, a pinned predicate, and an independent in-memory re-check that fails closed and logs. Mandatory on a feed, because a machine reader means a leak has nobody to notice it.
- `IActiveGroupContext` and `IBoardTypeResolver` must never be resolved on this path — there is no session and no active board, and every entity filtered through the ambient filter returns zero rows silently rather than throwing.
- A UI flag that hides a row from a listing is not an access control in this codebase (`DungeonMasterSession` is the live example behind D-04).

### Integration Points
- `CalendarSubscriptionService.GetFeedAsync` — filter the already-loaded membership set to `BoardType.OneShot`, run the quest read against that set, project to `CalendarFeedEntry` with `Source = Quest`, and concatenate with the event entries before the writer call.
- `CalendarFeedSource` — add the `Quest` member.
- `CalendarFeedWriter` — source-aware duration and a `SUMMARY` path that never appends a vote suffix to a quest.
- `CalendarFeedOptions` — the new duration property plus its `IsValid()` clause.
- New repository query and its interface method. No entity, no migration, no configuration file change required for the feature to work.
- Ordering: the combined document mixes two sources — the existing event query orders by date then start time then id, and the merged list needs a defined order rather than events-then-quests by accident.

</code_context>

<specifics>
## Specific Ideas

- The operator's four-hour figure is their own, given when asked directly rather than picked from a planner's guess — it is what a full evening session runs in this group.
- The consistent through-line across all ten decisions is minimalism in the entry itself: no quest marker, no DM marker, no busy flag, one window, one title rule. The feed stays what Phase 84 made it — a "when am I busy" surface, not a second view of the board — and quests are added without changing its character.
- Where the operator diverged from the advice offered: `TRANSP:OPAQUE` was put forward as the decision that would express the phase goal in ICS, and was declined in favour of one transparency rule for the whole feed.
- A consequence recorded rather than asked: `BoardType` is editable after board creation (`Areas/Platform/Views/Group/Edit.cshtml`). Switching a board from One-Shot to Campaign silently removes every one of its sessions from every subscriber's phone at the next poll. That falls out of the locked one-shot constraint and needs no separate decision, but it should not surprise anyone later.

</specifics>

<deferred>
## Deferred Ideas

- **Waitlisted sessions in the feed, with a marker.** D-02 excluded them. Revisit if a player promoted off the waitlist says they would have wanted the night blocked out in advance.
- **A `(DM)` suffix on sessions the reader runs.** D-08 dropped it. Revisit if a DM who both runs and plays on one board finds the rows indistinguishable in practice.
- **A separate, longer history window for sessions.** D-10 rejected it in favour of one shared window.
- **Marking quest entries busy (`TRANSP:OPAQUE`).** D-06 declined it. Revisit together with 84 D-03 if the feed ever needs to function as a real availability signal to other people — it is one rule for both sources, not a per-source tweak.
- **A cross-board quest list as a page in the application.** Explicitly its own phase per the roadmap's scope notes; this phase adds quests to a feed, not a screen.
- **Campaign-board quests in the feed.** Ruled out by the operator's own constraint, not by analysis.
- **The real-device subscription check.** Inherited from Phase 84, still open, needs a public tunnel or the deployed application.

</deferred>

---

*Phase: 85-one-shot-quests-in-the-calendar-feed*
*Context gathered: 2026-09-18*
