# Phase 85: One-Shot Quests in the Calendar Feed - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-09-18
**Phase:** 85-one-shot-quests-in-the-calendar-feed
**Areas discussed:** Whose sessions reach it, Duration and busy/free, What the entry says, When a session leaves

---

## Whose sessions reach it

### A DM's own sessions

A DM holds no `PlayerSignup` row on the quest they are running — `QuestEntity.DungeonMasterId` is the only link.

| Option | Description | Selected |
|--------|-------------|----------|
| Yes — second predicate on `DungeonMasterId` | The query unions signup rows with quests the owner is DM of. A DM's own sessions are the nights they are most certainly busy; leaving them off gives the person doing the scheduling the emptiest calendar. Costs a second branch. | ✓ |
| No — signup rows only | Single query shape, exactly mirroring the event feed (84 D-17). One predicate to get wrong instead of two. Cost: a DM who runs most of the board's quests gets a near-empty feed. | |

**User's choice:** Yes — second predicate on `DungeonMasterId`
**Notes:** Surfaced afterwards as a planner consequence rather than a question — the union can match the same quest twice (a DM who also holds a signup row on it), producing two VEVENTs on one `UID`. Needs a distinct-by-quest-id step.

### Waitlisted signups

`IsSelected == false` means waitlisted; waitlisted players are auto-promoted as seats free up, right up to game night.

| Option | Description | Selected |
|--------|-------------|----------|
| Confirmed seats only (`IsSelected == true`) | The calendar only shows nights you are actually playing; a promotion appears at the next poll. Cost: no advance warning of a night you might end up playing. | ✓ |
| Include waitlisted, marked as such | Every row appears, with a suffix the way an event's `(maybe)`/`(declined)` works. Mirrors 84 D-18. Cost: nights you may never play, with the marker as the only thing keeping them apart. | |
| Include waitlisted, unmarked | Simplest query, no branch. Cost: a waitlist entry reads identically to a confirmed seat — the one thing the reader most needs to tell apart. | |

**User's choice:** Confirmed seats only
**Notes:** This closed the roadmap's open question about whether the vote-marker convention carries over — with no waitlisted entries there is nothing left to mark.

### Signup roles

| Option | Description | Selected |
|--------|-------------|----------|
| Keep all three roles | `IsSelected == true` is the whole predicate. Everyone with an approved seat is at the table that night. Falls out of the previous answer at zero cost. | ✓ |
| Narrow to `SignupRole.Player` | Reads "only nights I am playing" literally. Cost: an Assistant DM co-running the session and a Spectator who set aside the evening both get nothing. | |

**User's choice:** Keep all three roles
**Notes:** Checked before asking — `IsSelected` is set `true` unconditionally for Spectator and AssistantDM (`QuestController.cs:482`); only a Player ever waitlists. The previous answer already admitted all three.

### Dungeon-Master-only quests

| Option | Description | Selected |
|--------|-------------|----------|
| Yes — the seat is the authority | No `DungeonMasterSession` branch. Matches what the app already does. Cost: a flag flipped after signup leaves the title on that reader's phone. | ✓ |
| No — drop unless you are its DM | Fail-closed; declines to publish a title the board deliberately hides. Cost: an Admin or Assistant DM holding a real seat on someone else's DM-only session gets nothing. | |

**User's choice:** Yes — the seat is the authority
**Notes:** Verified before asking that the flag hides a quest from the board listing (`QuestRepository.cs:73`) but `Details` applies no gate (`QuestController.cs:307`) — it is a discoverability hide, not an access control. Also confirmed the flag is force-cleared on campaign boards, so it is live precisely on the boards this phase covers.

---

## Duration and busy/free

### How long a session lasts

`FinalizedDate` is a full `DateTime` so a quest always has a real start time, but the schema has no end time anywhere.

| Option | Description | Selected |
|--------|-------------|----------|
| A fixed multi-hour block, configurable | Session-shaped, exposed on `CalendarFeedOptions` like the window bounds. Reads honestly as "the evening is gone". Still invented — no UI may present it as the board's knowledge. | ✓ |
| The same one hour events get | One duration constant for both sources. Cost: a game night renders as a sliver on a phone month view, understating the commitment. | |
| A true all-day entry | Sidesteps inventing a duration. Cost: throws away the start time the board does know — the one fact a player checks before game night. | |

**User's choice:** A fixed multi-hour block, configurable

### The number

| Option | Description | Selected |
|--------|-------------|----------|
| 4 hours | A typical full evening session. Blocks the evening convincingly without swallowing the whole night. | ✓ |
| 3 hours | Safer against overstating. | |
| 5 hours | Generous — the evening is gone either way once you commit to a table. | |

**User's choice:** 4 hours
**Notes:** Asked of the operator directly rather than left to the planner, since the number appears on every row of every subscriber's phone.

### Busy or free

| Option | Description | Selected |
|--------|-------------|----------|
| Busy — `TRANSP:OPAQUE` on quest entries | The two sources diverge: an informational event stays transparent, a committed session blocks the slot. The phase goal stated in ICS. Cost: the four hours is invented, so the busy block overclaims by however much it is wrong. | |
| Transparent — match events | One rule for the whole feed, no per-source branch. Consistent with 84 D-03. Cost: nobody checking the reader's calendar sees game night. | ✓ |

**User's choice:** Transparent — match events
**Notes:** `OPAQUE` was the option put forward as expressing the phase goal; the operator declined it in favour of one rule for the whole feed.

---

## What the entry says

### A quest marker

| Option | Description | Selected |
|--------|-------------|----------|
| Nothing — the title carries it | A session title already reads as a session; a marker adds width to a title a phone truncates anyway. Keeps one `SUMMARY` rule for both sources. | ✓ |
| A suffix, the way event answers are suffixed | Follows 84 D-18's shape. Costs a per-source branch. | |
| A prefix marker before the board name | Most visible, but 84 D-18 explicitly refused a second prefix — it can consume a narrow day view's whole width before the title begins. | |

**User's choice:** Nothing — the title carries it

### A DM marker

| Option | Description | Selected |
|--------|-------------|----------|
| No — identical entry | One `SUMMARY` rule with no branch on why the row is there. Cost: a DM cannot tell a night they run from a night they play. | ✓ |
| Yes — a suffix on sessions they run | The one case where the reader's relationship to the entry is genuinely different, and the only marker a DM cannot infer from the board prefix. | |

**User's choice:** No — identical entry
**Notes:** The waitlist-marker question that the roadmap flagged for this area had already been removed by the confirmed-seats-only decision.

---

## When a session leaves

### Disappearance vs. cancellation

| Option | Description | Selected |
|--------|-------------|----------|
| Same rule — it just disappears | One rule for the whole feed; the predicate does the work. No tombstone state, no `STATUS` branch. Cost, the same one 84 accepted: a called-off game night reaches the reader only as an absence. | ✓ |
| Emit `STATUS:CANCELLED` for quests | The entry stays struck through rather than absent. Breaks with 84 D-12 and needs state nothing records — the feed would have to know a quest *was* finalized and no longer is. | |

**User's choice:** Same rule — it just disappears
**Notes:** Established before asking that `Close` is campaign-only and rejects with `BadRequest` on a one-shot board (`QuestController.cs:760`), so the roadmap's "or a quest is closed" question is unreachable for every quest this phase can emit. Also recorded rather than asked: 84-RESEARCH already settled constant `SEQUENCE:0` for a plain-`PUBLISH` feed (Assumption A5), so a moved finalized date updates in place and nothing is reopened.

### The window

| Option | Description | Selected |
|--------|-------------|----------|
| Same window as events | One window, one pair of knobs. Three months of history already answers "when did we last play?". | ✓ |
| Separate, longer history for quests | Its own `MonthsBack` so past sessions linger. Cost: two windows in one document and a feed that grows with every campaign year. | |

**User's choice:** Same window as events

---

## Claude's Discretion

Recorded in full in CONTEXT.md § Claude's Discretion. In summary: where the one-shot narrowing happens (the membership read already carries `BoardType`); the query's shape and location, subject to mandatory membership scoping plus the second-layer re-check; treating membership and board type as two independent predicates; deduplicating the DM/signup union by quest id; extending `CalendarFeedSource`; `CalendarFeedEntry`'s shape for a source with no vote; the window comparison's time basis given `FinalizedDate` is server-local; validating the new duration option; and minting the requirement family.

## Deferred Ideas

- Waitlisted sessions in the feed, with a marker
- A `(DM)` suffix on sessions the reader runs
- A separate, longer history window for sessions
- Marking quest entries busy (`TRANSP:OPAQUE`) — revisit together with 84 D-03
- A cross-board quest list as a page in the application — its own phase per the roadmap
- Campaign-board quests in the feed — ruled out by the operator's own constraint
- The real-device subscription check — inherited from Phase 84, still open
