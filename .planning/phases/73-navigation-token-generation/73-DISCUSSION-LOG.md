# Phase 73: Navigation + Token Generation - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-07-13
**Phase:** 73-navigation-token-generation
**Areas discussed:** Quest-page entry points, DM navbar link behavior, Disabled/misconfigured UX, Redirect failure handling

---

## Quest-page entry points

| Option | Description | Selected |
|--------|-------------|----------|
| Details page only | Matches the scouted DM Controls card pattern | ✓ |
| Details + quest list | Button in both places | |
| You decide | Claude picks | |

**User's choice:** Details page only

| Option | Description | Selected |
|--------|-------------|----------|
| Finalized quests only | Button only appears once quest has a confirmed date | |
| Any quest, any state | Button always visible for DMs | ✓ |

**User's choice:** Any quest, any state
**Notes:** `questDate` may be null/unset for unscheduled quests.

| Option | Description | Selected |
|--------|-------------|----------|
| Launch immediately | Plain link, no confirmation, matches "Manage Quest" pattern | ✓ |
| Confirm before leaving | Modal/dialog before external redirect | |

**User's choice:** Launch immediately

---

## DM navbar link behavior

| Option | Description | Selected |
|--------|-------------|----------|
| Quest picker in Quest Board first | Navbar → pick a quest → quest-specific launch | |
| Generic landing in Omphalos | Navbar → signed quest-less token → Omphalos index | ✓ |

**User's choice:** Generic landing in Omphalos

| Option | Description | Selected |
|--------|-------------|----------|
| Null/empty quest fields | Token with nullable quest fields, IIntegrationTokenService supports it | |
| You decide | Claude figures out shape during planning | |

**User's choice (free text):** "Maybe just redirect the navbar link to the index page of omphalos. No need for any payload. Only when clicking the button in a quest details page?"

**Notes:** This initial answer proposed an unsigned raw redirect for the navbar link, which conflicts with the phase goal ("no surface ever linking to Omphalos's raw base URL unsigned") and TOKEN-04 (identity claim must always be `UserEntity.Id`). Claude flagged this conflict and asked a follow-up.

| Option | Description | Selected |
|--------|-------------|----------|
| Yes — signed, user-only token | Signed token carrying only UserEntity.Id, questId/questTitle/questDate null, lands on Omphalos index | ✓ |
| Something else | User describes something different | |

**User's choice:** Yes — signed, user-only token

---

## Disabled/misconfigured UX

| Option | Description | Selected |
|--------|-------------|----------|
| Hide entirely | Button/link doesn't render when disabled | ✓ |
| Show disabled with tooltip | Grayed-out with explanation | |

**User's choice:** Hide entirely

| Option | Description | Selected |
|--------|-------------|----------|
| Redirect back to quest with a flash message | Reuses existing flash-message pattern | ✓ |
| Dedicated "not available" page | New standalone view | |

**User's choice:** Redirect back to quest with a flash message

| Option | Description | Selected |
|--------|-------------|----------|
| Redirect to Quest Board home/dashboard | Natural fallback for quest-less navbar case | ✓ |
| You decide | Claude picks fallback page | |

**User's choice:** Redirect to Quest Board home/dashboard

---

## Redirect failure handling

| Option | Description | Selected |
|--------|-------------|----------|
| Same graceful fallback as disabled | One consistent failure path | ✓ |
| Generic error page | Distinct "something went wrong" error | |

**User's choice:** Same graceful fallback as disabled

---

## Claude's Discretion

- Exact `IIntegrationTokenService` method signature(s) for the quest-less variant (nullable-params overload vs. separate method).
- Exact nonce generation mechanism and low-level serialization details not already fixed by Phase 72.
- Exact wording of flash/TempData messages for disabled/failure cases.
- Icon and button copy/placement details within the DM Controls card and navbar dropdown.
- Where the navbar (quest-less) action lives — existing `QuestController` vs. a new controller.

## Deferred Ideas

None — discussion stayed within phase scope.
