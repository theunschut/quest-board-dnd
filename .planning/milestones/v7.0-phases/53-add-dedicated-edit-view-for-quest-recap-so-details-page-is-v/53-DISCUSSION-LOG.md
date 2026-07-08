# Phase 53: Add dedicated Edit view for Quest recap so Details page is view-only - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-07-06
**Phase:** 53-add-dedicated-edit-view-for-quest-recap-so-details-page-is-v
**Areas discussed:** Edit entry point, Save & Cancel navigation, Direct-URL access

---

## Edit entry point

| Option | Description | Selected |
|--------|-------------|----------|
| Inline in Recap section | A small button directly under the read-only recap text (or the "No recap yet" message) — keeps the edit affordance right where the content it edits lives, closest to today's in-place feel. | ✓ |
| Quick Actions sidebar | Add it next to the existing "Manage Quest" link in the Quick Actions card — consistent with where other DM-only actions already live on this page. | |
| Both places | Small edit icon/link next to the "Session Recap" header, plus also listed in Quick Actions — more discoverable but adds a second entry point to keep in sync. | |

**User's choice:** Inline in Recap section.

Follow-up question — button label:

| Option | Description | Selected |
|--------|-------------|----------|
| Dynamic label (Recommended) | "Add Recap" when the recap is empty, "Edit Recap" when it already has content — matches the empty-state message pattern already used elsewhere in this view. | ✓ |
| Always "Edit Recap" | Same label regardless of whether a recap exists yet — simpler, one string to maintain. | |

**User's choice:** Dynamic label (Add Recap / Edit Recap).

---

## Save & Cancel navigation

| Option | Description | Selected |
|--------|-------------|----------|
| Yes, add Cancel (Recommended) | Cancel (secondary, left) + Save Recap (primary, right) in a d-flex justify-content-between row — matches CLAUDE.md's locked button-layout convention, same as Quest/Edit.cshtml. Both land back on Details. | ✓ |
| No Cancel button | Only a Save button, like today's inline form — abandoning an edit means navigating away manually. | |

**User's choice:** Yes, add Cancel.
**Notes:** Save's redirect-to-Details behavior already exists on `UpdateRecap` and needs no change; the new page just adds the explicit Cancel action alongside it.

---

## Direct-URL access

| Option | Description | Selected |
|--------|-------------|----------|
| 403 Forbidden (Recommended) | Matches the existing, already-tested behavior of UpdateRecap's POST action (Forbid() when the caller isn't the quest's DM or an Admin) — the GET action mirrors the same authorization outcome for consistency. | ✓ |
| 404 Not Found | This project's usual cross-tenant/unauthorized-access convention elsewhere (Phase 49/55) — noted as diverging from UpdateRecap's existing tested 403 behavior on the same feature. | |
| Silent redirect to Details | Bounce them back to the read-only page with no error shown — friendliest UX, but hides that anything was blocked. | |

**User's choice:** 403 Forbidden.
**Notes:** Deliberately breaks from the project's general cross-tenant 404 convention (Phase 49/55) — this is judged a same-group role/ownership check with an existing tested precedent on the sibling POST action, not a cross-tenant existence-hiding case.

---

## Claude's Discretion

- Exact new action names/routes on `QuestLogController` for the edit GET/POST pair.
- Whether the GET action reuses the same "is this quest completed" guard `UpdateRecap` already has (recommended: yes).
- Exact Razor structure/wording of the new Edit Recap view — mirror `Quest/Edit.cshtml`'s modern-card pattern; carry over existing textarea styling and helper text verbatim unless it looks wrong once built.

## Deferred Ideas

None — discussion stayed within phase scope.
