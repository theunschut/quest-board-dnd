# Phase 67: Remaining Quest Fields & Email Templates - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-07-10
**Phase:** 67-remaining-quest-fields-email-templates
**Areas discussed:** Manage-page Rewards section, Follow-Up mobile Rewards gap

---

## Manage-page Rewards section

| Option | Description | Selected |
|--------|-------------|----------|
| Add to Manage too | Mirrors Phase 66's Description precedent exactly — same collapsible-section pattern, collapsed by default. Keeps Rewards and Description visually consistent on the Manage page. | ✓ |
| Stay within literal ROADMAP scope | Only wire the editor on write forms and render on Details/QuestLog, exactly as REQUIREMENTS.md's QUESTMD-02 states. Leaves Manage's Rewards gap as-is. | |

**User's choice:** Add to Manage too (recommended option)
**Notes:** Locked as D-01 in CONTEXT.md. Recap does not get an equivalent Manage-page section — Manage only covers active/pending quests, Recap only exists for completed ones (reached via QuestLog, not Quest/Manage), so there's no analogous gap there.

---

## Follow-Up mobile Rewards gap

| Option | Description | Selected |
|--------|-------------|----------|
| Fix now, same task | Add the missing Rewards textarea to CreateFollowUp.Mobile.cshtml (blank-start, matching desktop's Phase 59 behavior) and wire the Markdown editor onto it at the same time. | ✓ |
| Leave as separate follow-up | Wire the editor only onto Rewards fields that already exist today; log the mobile gap as a deferred idea. | |

**User's choice:** Fix now, same task (recommended option)
**Notes:** Locked as D-02 in CONTEXT.md. Justified by this project's standing mobile-parity lesson (Phase 43/54/61): backfilling a desktop-only fix onto mobile later has twice required its own dedicated phase, and this phase already touches CreateFollowUp.cshtml to wire in the editor.

---

## Claude's Discretion

- Exact placement/markup of the new Manage-page Rewards collapsible section (D-01) — follow `_QuestSection.cshtml`'s and Phase 66's existing Description-section structure.
- Exact markup for the new `CreateFollowUp.Mobile.cshtml` Rewards field (D-02) — mirror desktop's Rewards block, adapted to the mobile form's layout.
- Order of implementation (fields first vs. emails first) — no user preference expressed.

## Deferred Ideas

None — both gray areas raised were resolved as in-scope extensions (D-01, D-02) rather than deferred to a future phase.
