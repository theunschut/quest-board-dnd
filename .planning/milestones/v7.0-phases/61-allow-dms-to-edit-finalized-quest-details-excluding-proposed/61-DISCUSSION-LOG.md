# Phase 61: Allow DMs to edit finalized quest details (excluding proposed and selected dates) - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-07-07
**Phase:** 61-allow-dms-to-edit-finalized-quest-details-excluding-proposed
**Areas discussed:** Total Player Count edge case, Player notification on edits, Edit window (upcoming vs. past quests), Entry point & form reuse

---

## Total Player Count edge case

| Option | Description | Selected |
|--------|-------------|----------|
| Block with validation error | "Total Player Count cannot be less than the N players already selected." DM must remove a player first before lowering the count. | ✓ |
| Allow it silently | Quest ends up over-capacity; existing selected players keep seats, no new promotions until count rises or a seat frees up. | |
| You decide | Claude picks the safest default during planning. | |

**User's choice:** Block with validation error
**Notes:** None beyond the selection.

---

## Player notification on edits

| Option | Description | Selected |
|--------|-------------|----------|
| No notification | Stay silent — matches how Rewards/Description edits already work on non-finalized quests today. | ✓ |
| Reuse date-changed email, reworded | Extend the existing quest-updated notification job to also fire on detail changes. | |
| You decide | Claude picks the safest default during planning. | |

**User's choice:** No notification
**Notes:** None beyond the selection.

---

## Edit window (upcoming vs. past quests)

| Option | Description | Selected |
|--------|-------------|----------|
| Any finalized quest, any time | IsFinalized is the only gate, no time cutoff — matches Phase 53's Recap editing. | ✓ |
| Only while upcoming (not yet "Done") | Edit blocked once FinalizedDate has passed and the "Done" badge shows. | |
| You decide | Claude picks the safest default during planning. | |

**User's choice:** Any finalized quest, any time
**Notes:** None beyond the selection.

---

## Entry point & form reuse

| Option | Description | Selected |
|--------|-------------|----------|
| Yes — reuse existing Edit view | Same EditQuestViewModel/Edit.cshtml/controller action; hide Proposed Dates block when finalized. | ✓ |
| Separate dedicated view for finalized edits | Mirrors the Phase 53 Recap pattern with a distinct EditFinalized action/view. | |
| You decide | Claude picks during planning based on how much the two forms would diverge. | |

**User's choice:** Yes — reuse existing Edit view
**Notes:** None beyond the selection.

---

## Claude's Discretion

- Exact wording of the Total Player Count validation error message.
- Whether the "Quest Editing Tips" sidebar needs a finalized-quest variant vs. naturally not rendering dates-specific tips.
- Whether `IsFinalized` reaches the view via a new `EditQuestViewModel.IsFinalized` property or via `ViewBag`.
- Where the Total Player Count validation lives — controller `ModelState.AddModelError` vs. a service/repository guard.

## Deferred Ideas

None — discussion stayed within phase scope.
