# Phase 63: Allow any player to edit quest recaps, not just the assigned DM or admin - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-07-07
**Phase:** 63-allow-any-player-to-edit-quest-recaps-not-just-the-assigned-
**Areas discussed:** Editor scope, Manage Quest link split, Attribution, DM notification

---

## Editor scope

| Option | Description | Selected |
|--------|-------------|----------|
| Any group member | Any authenticated user in the quest's group/tenant can edit — including players who never signed up for that specific quest. Simplest change: remove the DM/Admin gate on the existing EditRecap action and CanEditRecap check. | ✓ |
| Only quest participants | Only players who were actually selected (IsSelected) for that specific quest, plus the assigned DM/Admin, can edit. Requires checking quest.PlayerSignups for the current user. | |

**User's choice:** Any group member
**Notes:** Matches the user's own framing — "not all DMs want to do this, some players do, so why not leave it open for all" — the intent is removing the restriction, not replacing it with a narrower one.

---

## Manage Quest link split

| Option | Description | Selected |
|--------|-------------|----------|
| Yes, keep Manage Quest DM/Admin-only (recommended) | Split into two flags: CanEditRecap (broadened) and a separate CanManageQuest (unchanged: assigned DM or Admin only). | ✓ |
| Open Manage Quest to everyone too | Not recommended — Manage Quest exposes destructive/administrative actions (Open, remove player signups, edit quest details). | |

**User's choice:** Yes, keep Manage Quest DM/Admin-only (recommended)
**Notes:** Flagged as a discovered risk — `Details.cshtml`/`Details.Mobile.cshtml` currently reuse the same `ViewBag.CanEditRecap` flag to gate both the recap edit button and the Manage Quest sidebar link. Broadening it naively would have accidentally exposed Manage Quest (Open/Close, remove signups, edit quest) to every player.

---

## Attribution

| Option | Description | Selected |
|--------|-------------|----------|
| No attribution (recommended) | Keep Recap as a plain text field, no schema changes. | ✓ |
| Show last editor | Add a new column (e.g. RecapLastEditedByUserId) and display "Last edited by X". Requires a migration. | |

**User's choice:** No attribution (recommended)
**Notes:** None.

---

## DM notification

| Option | Description | Selected |
|--------|-------------|----------|
| No notification (recommended) | Matches Phase 61's existing precedent of not adding new emails for quest-detail edits; constrained email budget. | ✓ |
| Email the DM | Send the quest's DM a targeted email when a non-DM player saves recap changes. New email job/template needed. | |

**User's choice:** No notification (recommended)
**Notes:** None.

---

## Claude's Discretion

- Exact name of the new "Manage Quest" gating flag (`CanManageQuest` suggested).
- Whether the old in-action DM/Admin check code is fully deleted vs. left as unreachable — expectation is full removal.
- Whether `[Authorize(Policy = "DungeonMasterOnly")]` is removed entirely from `EditRecap` GET/POST or left implicit via the class-level `[Authorize]` — no behavior difference.

## Deferred Ideas

None — the "quest participants only" and "last edited by" alternatives were both considered and explicitly declined, not deferred as future work.
