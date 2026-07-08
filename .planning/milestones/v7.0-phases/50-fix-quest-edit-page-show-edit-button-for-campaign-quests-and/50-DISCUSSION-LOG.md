# Phase 50: Fix quest edit page: show edit button for campaign quests and align field visibility with create page - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-07-05
**Phase:** 50-fix-quest-edit-page-show-edit-button-for-campaign-quests-and
**Areas discussed:** Edit button style & placement, Delete Quest parity on Manage, Edit page field-hiding scope, Edit page sidebar tips

---

## Edit button style & placement

| Option | Description | Selected |
|--------|-------------|----------|
| Reuse existing pattern | btn-primary + fa-edit icon + "Edit Quest" text — identical to the two existing "Edit Quest" buttons already in this same file for OneShot quests | ✓ |
| De-emphasized (outline/secondary) | A lighter-weight style so Edit doesn't visually compete with Close/Reopen | |

**User's choice:** Reuse existing pattern (btn-primary + fa-edit + "Edit Quest")
**Notes:** Recommended option chosen for consistency with the codebase's existing OneShot Manage button styling.

| Option | Description | Selected |
|--------|-------------|----------|
| Before Close/Reopen | Edit Quest, then Close/Reopen — mirrors the OneShot unfinalized row's order (Finalize, Edit, Delete) | ✓ |
| After Close/Reopen | Close/Reopen first, then Edit Quest | |

**User's choice:** Before Close/Reopen
**Notes:** Content-editing actions precede state-transition actions, matching the existing OneShot row convention.

---

## Delete Quest parity on Manage

| Option | Description | Selected |
|--------|-------------|----------|
| Yes, add it | Same root cause as the Edit button gap — the Campaign branch was built with a minimal Close/Reopen-only action set | ✓ |
| No, leave Index-only | Delete already works via the Quest Index card for Campaign quests — don't expand scope beyond the reported bug | |

**User's choice:** Yes, add it
**Notes:** Discovered mid-discussion (not in the original bug report) while investigating the Manage page's Campaign action row. Placement/style resolved by Claude's discretion afterward (not re-asked): reuse the existing `btn-danger` + `fa-trash` + "Delete" pattern and the already-defined `deleteQuest(id)` JS function, placed last in the action row after Edit and Close/Reopen.

---

## Edit page field-hiding scope

| Option | Description | Selected |
|--------|-------------|----------|
| Yes, mirror Create exactly | Hide all 4 fields (Challenge Rating, Total Player Count, DM-Session-Only, Proposed Dates) identically to Create — the POST action already silently discards them for Campaign server-side | ✓ |
| Keep some visible as read-only | Show one or more fields in a disabled/read-only state for context | |

**User's choice:** Yes, mirror Create exactly
**Notes:** Full parity with Create's existing `@if (boardType != BoardType.Campaign)` conditional — no read-only variant.

---

## Edit page sidebar tips

| Option | Description | Selected |
|--------|-------------|----------|
| Leave as-is | Matches Create.cshtml's existing sidebar, which also isn't Campaign-aware | ✓ |
| Hide/adjust for Campaign | Go further than Create's current behavior and make Edit's sidebar Campaign-aware | |

**User's choice:** Leave as-is
**Notes:** User confirmed the intent is to mirror what Create does today, not fix a pre-existing gap Create itself still has.

---

## Claude's Discretion

- Delete Quest button's exact placement/style (D-03) — resolved without a follow-up question: reuse the existing OneShot `btn-danger`/`fa-trash`/"Delete" pattern and the already-defined `deleteQuest(id)` JS function, ordered last in the action row.

## Deferred Ideas

None — discussion stayed within phase scope (including the Delete Quest addition, which shares the same root cause as the original report and was explicitly pulled in, not deferred).
