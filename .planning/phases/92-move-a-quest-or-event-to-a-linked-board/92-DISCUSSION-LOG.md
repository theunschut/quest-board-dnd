# Phase 92: Move a Quest or Event to a Linked Board - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-10-17
**Phase:** 92-move-a-quest-or-event-to-a-linked-board
**Areas discussed:** Signups & people, Series & follow-ups, Which entries can move, Notify & feed

Findings presented before the questions, from the codebase scout: signups, votes, proposed dates
and reminder logs carry no board column and follow `Quest.GroupId`; characters are board-scoped;
no gold hangs off signups (`Rewards` is free text, `QuestReward` is never written); the nightly
series top-up reads slots through the board filter, so a moved or detached occurrence would be
recreated; Campaign events carry automatic Yes rows per member; follow-up chains would cross
boards; Event Details has no `.Mobile` twin.

---

## Signups & people

### Target-board member's quest signup

| Option | Description | Selected |
|--------|-------------|----------|
| Keep it, clear the character | Signup, role, selection/waitlist and votes come along; character cleared; player re-picks via the existing control | ✓ |
| Keep it, auto-pick their Main | Point the signup at the player's Main on the target board if any | |
| Drop all signups | Quest arrives with no signups or votes | |

### Non-member of the target board signed up

| Option | Description | Selected |
|--------|-------------|----------|
| Drop their signup, named on confirm | Confirm lists who loses their signup and flags selected ones | ✓ |
| Refuse the move | Blocked until every signed-up player is a member | |
| Keep their signup anyway | Roster row on a board they can't open | |

### Quest's DM not a member, or only a Player, on the target

| Option | Description | Selected |
|--------|-------------|----------|
| Refuse unless DM or Admin there | Only offered where the DM is DungeonMaster or Admin, read fresh | ✓ |
| Refuse only if not a member | Any membership is enough | |
| Allow, board admins take over | Original DM loses sight of the quest | |

### Event availability answers

| Option | Description | Selected |
|--------|-------------|----------|
| Like created there | Keep members' answers, drop non-members', add Campaign auto-Yes rows for target members | ✓ |
| Keep members' answers, no fill-in | No automatic Yes rows added | |
| Reset all answers | Drop every answer, re-create Campaign rows | |

### Selected non-member dropped from a finalized quest

| Option | Description | Selected |
|--------|-------------|----------|
| Yes, the normal rule | Promote the top waitlisted kept signup, including the Waitlist Promoted email | ✓ |
| Promote, but no email | Seats fill silently | |
| Leave the seats open | DM fills seats on Manage | |

**Notes:** The user asked for more questions after the first four; the waitlist question was the follow-up.

---

## Series & follow-ups

### Event is an occurrence of a recurring series

| Option | Description | Selected |
|--------|-------------|----------|
| Refuse; whole-series move deferred | Only one-offs (never in a series, or detached) can move | ✓ |
| Move it out, leave a cancelled stub | Placeholder keeps the slot taken on the source board | |
| Move the whole series | Series and all occurrences move together | |

### Quest is in a follow-up chain

| Option | Description | Selected |
|--------|-------------|----------|
| Allow, unlink, named on confirm | Chain cut where it crosses boards; a hidden link would break the one-follow-up constraint | ✓ |
| Refuse while in a chain | Safest for the link, but a wrong-board follow-up can never be fixed | |
| Move the whole chain | Every quest in the chain moves | |

---

## Which entries can move

### Quests

| Option | Description | Selected |
|--------|-------------|----------|
| Not yet played | Open and finalized-upcoming; played or closed cannot move | ✓ |
| Any quest, history included | Played and closed quests move too | |
| Open quests only | Finalized quests cannot move | |

### Events

| Option | Description | Selected |
|--------|-------------|----------|
| Upcoming, cancelled or not | One-offs dated today or later; cancelled stays cancelled | ✓ |
| Upcoming and not cancelled | Live upcoming only | |
| Any event, past included | Past one-offs move too | |

### Move flow

| Option | Description | Selected |
|--------|-------------|----------|
| Dedicated Move page | Target picker, consequence preview, confirm; desktop + `.Mobile` | ✓ |
| Modal on Details | Board dropdown and a fetched consequences panel | |
| Platform page instead | Moves from the Linked boards page | |

### Landing after the move

| Option | Description | Selected |
|--------|-------------|----------|
| Source list + toast | Back to the source board's list; toast links to the entry if a member of the target | ✓ |
| Switch to the target board | Needs a fallback for non-members anyway | |

---

## Notify & feed

### Who is told

| Option | Description | Selected |
|--------|-------------|----------|
| Quest people by email; events silent | DM and kept players get "now on Board B"; dropped players get "signup removed"; events send nothing | ✓ |
| Nobody; the super admin tells people | No new email | |
| Only dropped players and the DM | Kept players not mailed | |

### Calendar feed behaviour

| Option | Description | Selected |
|--------|-------------|----------|
| Accept as is | No change to the feed files; the change tracker raises the revision | ✓ |
| Send a cancellation to those who lose it | Per-subscriber tombstones in high-risk files | |

### Trace of a move

| Option | Description | Selected |
|--------|-------------|----------|
| Server log only | One structured log line; nothing stored on the entry | ✓ |
| Visible "Moved from Board A" note | Stored fields and a migration | |
| No trace | Nothing | |

---

## Claude's Discretion

- Disabled-with-reason vs hidden control for ineligible entries on a linked board (default: disabled with reason)
- Handling a change between preview and confirm (default: re-show the Move page)
- Email template shape and copy; whether the acting super admin is mailed; mails for a promoted player
- Where the move logic and actions live; any allowlist argument if a new `IgnoreQueryFilters()` file is needed
- Move page, toast and reason copy and layout; which events list an event move returns to
- Test structure beyond the mandatory proofs

## Deferred Ideas

- Move a whole event series to a linked board
- `STATUS:CANCELLED` feed entries for subscribers who lose a moved entry
