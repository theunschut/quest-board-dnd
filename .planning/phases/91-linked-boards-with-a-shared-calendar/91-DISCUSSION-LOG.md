# Phase 91: Linked Boards with a Shared Calendar - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-10-15
**Phase:** 91-linked-boards-with-a-shared-calendar
**Areas discussed:** Link model & admin UI, What non-members see, Surfaces & which entries, How linked entries look

---

## Link model & admin UI

### Is the existing board also One-Shot?

| Option | Description | Selected |
|--------|-------------|----------|
| Yes, both One-Shot | Same-type rule fits the real case; keep it as the roadmap says, enforced server-side | ✓ |
| No, one's a Campaign | A same-type rule would block the origin case; scope change | |
| Not sure | Record the rule as stated plus a note to check production board types | |

**User's choice:** Yes, both One-Shot

### How should the link work?

| Option | Description | Selected |
|--------|-------------|----------|
| Named group | Named group, a board in at most one, transitive by construction | ✓ |
| Pairwise links | Each link between two boards; needs a transitivity rule | |
| You decide | Planner decides, every board in a link sees all others | |

**User's choice:** Named group
**Notes:** First answered "You decide" with an early click; the operator asked to go back and chose
Named group on re-ask.

### Where does a super admin manage links?

| Option | Description | Selected |
|--------|-------------|----------|
| On the board's Edit page | "Shared calendar with" picker on Platform › Edit; no new page | |
| A dedicated 'Linked boards' page | Own Platform page listing every link; new page with a .Mobile twin | ✓ |
| You decide | Planner decides placement | |

**User's choice:** A dedicated 'Linked boards' page
**Notes:** Re-asked together with the link model after the early click; the same answer was confirmed.

### A linked board is deleted — what happens?

(First asked before the re-ask; the operator declined that prompt and it was asked again.)

| Option | Description | Selected |
|--------|-------------|----------|
| Leaves the group automatically | One confirm, as Delete works today | ✓ |
| Block delete while linked | Refuse with "remove from its group first" | |
| You decide | Planner decides | |

**User's choice:** Leaves the group automatically

### A group left with one board

| Option | Description | Selected |
|--------|-------------|----------|
| Keep it, shown as inactive | Stays on the page with its name; shows nothing on calendars | ✓ |
| Remove it automatically | Deleted on the spot; re-linking means recreating it | |
| You decide | Planner decides | |

**User's choice:** Keep it, shown as inactive

---

## What non-members see

### Non-member of linked board B — what do they see?

| Option | Description | Selected |
|--------|-------------|----------|
| Full entry, not clickable | Title, time, board name as plain text; linking is the authorization | ✓ |
| Anonymous 'busy' block | "Busy — Board B", no title | |
| Nothing, members only | Safest, but a DM not on B gets no warning | |

**User's choice:** Full entry, not clickable

### What does a linked quest carry?

| Option | Description | Selected |
|--------|-------------|----------|
| Title, time, board, DM name | No key warning, votes or roster; narrow query | ✓ |
| Title, time, board only | Smallest; drops the DM name | |
| Same as own entries for members | Two shapes; brings back the heavy query | |

**User's choice:** Title, time, board, DM name

### Is B's entry clickable for a member of B?

| Option | Description | Selected |
|--------|-------------|----------|
| Clickable, uses the board switch | Phase 87 middleware switches board and shows the banner | ✓ |
| Never clickable | Plain text for everyone | |
| You decide | Planner decides | |

**User's choice:** Clickable, uses the board switch

---

## Surfaces & which entries

### Which linked quests appear?

| Option | Description | Selected |
|--------|-------------|----------|
| Same rules as own quests | Finalized on chosen date, open on each proposed date | ✓ |
| Finalized only | Only booked dates | |
| You decide | Planner decides | |

**User's choice:** Same rules as own quests

### Quest Details date-vote calendar

| Option | Description | Selected |
|--------|-------------|----------|
| Yes: linked quests and events | Desktop grid read-only; mobile "Also that day" line; own events stay off | ✓ |
| Yes: linked quests only | Matches Details being quests-only | |
| No, /calendar only | Vote form untouched | |

**User's choice:** Yes: linked quests and events

### Cancelled linked events

| Option | Description | Selected |
|--------|-------------|----------|
| Hide it | Doesn't occupy the date | ✓ |
| Show it struck-through | Same as own events | |

**User's choice:** Hide it

### DM series-runway warning

| Option | Description | Selected |
|--------|-------------|----------|
| Stays per-board | The DM can only act on their own board's series | ✓ |
| Include linked boards' series | Not actionable from A | |

**User's choice:** Stays per-board

### Calendar feed

| Option | Description | Selected |
|--------|-------------|----------|
| No change | Feed stays membership-based; high-risk files untouched | ✓ |
| Add linked boards' entries | Touches CalendarFeedWriter / CalendarSubscriptionService | |

**User's choice:** No change

---

## How linked entries look

### Marking

| Option | Description | Selected |
|--------|-------------|----------|
| Muted + board-name line | Muted, dashed border, board name under the title; keeps proposed/finalized colour | ✓ |
| Board-name prefix, not muted | "[Crew] Title"; compact but doesn't stand out | |
| A colour per linked board | Per-board accent in the legend; crowds and clashes | |

**User's choice:** Muted + board-name line (with the day-cell preview sketch)

### Does the page say which boards it shows?

| Option | Description | Selected |
|--------|-------------|----------|
| Yes, in the legend + a mobile line | Legend "Linked boards" section; mobile "Also showing" line | ✓ |
| No, the entries say enough | One less element | |

**User's choice:** Yes, in the legend + a mobile line

### Order within a day

| Option | Description | Selected |
|--------|-------------|----------|
| Own first, then linked | Own entries never pushed out of view | ✓ |
| Interleaved by time | Strict time order across boards | |

**User's choice:** Own first, then linked

---

## Scope addition raised at the close

The operator asked for a super admin to be able to move an existing quest or event to a linked board
from its Details page.

| Option | Description | Selected |
|--------|-------------|----------|
| Its own phase after 91 | Deferred idea + a new roadmap phase depending on 91 | ✓ |
| Backlog note only | Deferred idea, no roadmap entry yet | |
| Fold it into Phase 91 | Drop the read-only constraint and discuss it now | |

**User's choice:** Its own phase after 91

## Claude's Discretion

- Entity / table / service / repository naming (avoiding the `CrossBoardLink*` prefix)
- How a group holds its board type
- Which allowlisted file hosts the linked read
- How linked entries reach the view model
- Entry point to the Linked boards page, group-name visibility to players, copy, page layout
- Test structure beyond the mandatory cases

## Deferred Ideas

- Move a quest or event to a linked board (super admin, from Details) — its own phase after 91
- Own-board events on the Quest Details date-vote calendar
