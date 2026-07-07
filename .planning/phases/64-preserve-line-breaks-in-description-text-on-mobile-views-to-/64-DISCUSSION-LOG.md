# Phase 64: Preserve line breaks in description text on mobile views to match desktop rendering - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-07-07
**Phase:** 64-Preserve line breaks in description text on mobile views to match desktop rendering
**Areas discussed:** Fix scope

---

## Fix scope

Before asking, Claude cross-referenced every `Description`/`Bio`/`Backstory`/`Notes` free-text field in the codebase against its rendering view(s) on both desktop and mobile, and found:
- Characters mobile Description/Backstory — confirmed mobile-only bug (matches literal phase title)
- QuestLog desktop "Original Quest Description" box — confirmed **desktop**-only bug (mobile already correct), previously documented and deferred as finding IN-02 in Phase 59's review
- Shop item Description — broken equally on both platforms (no divergence)
- Quest board list-card preview (`_QuestCard.cshtml`) — broken equally on both platforms (shared partial), surfaced as an additional finding partway through discussion

| Option | Description | Selected |
|--------|-------------|----------|
| Just Characters (mobile-only bug) | Strictly matches the roadmap title — fix only the one genuine mobile-vs-desktop divergence | |
| Characters + QuestLog desktop fix | Also close the known, already-flagged IN-02 finding from Phase 59's review | |
| Characters + QuestLog + Shop | Full sweep of every free-text field with the bug, regardless of which platform is at fault | ✓ |

**User's choice:** "everything!" — Characters + QuestLog + Shop (full sweep).

**Follow-up:** Claude surfaced one more instance found after the initial question — the quest list card preview (`Quest/_QuestCard.cshtml`), a shared partial rendering Description inside a scrollable box with no `pre-wrap`. Asked separately whether to include it.

| Option | Description | Selected |
|--------|-------------|----------|
| Yes, include it | Same bug category; shared partial means one fix covers both platforms | ✓ |
| No, leave it out | Treat as a separate, lower-priority backlog item | |

**User's choice:** Yes, include it.

**Notes:** Final locked scope is four fixes: Characters mobile (D-01), QuestLog desktop (D-02), Shop item Description both platforms (D-03), quest list card preview both platforms (D-04). All four are pure CSS/inline-style fixes — no controller, service, ViewModel, or migration changes.

---

## Claude's Discretion

- Whether to fix each instance via inline `style="white-space: pre-wrap;"` or by extending the relevant CSS class — follow whatever convention the specific file already uses for its sibling elements (both patterns coexist in the codebase; see CONTEXT.md `<decisions>` for the full list of established examples).
- Whether to clean up the now-redundant inline `pre-wrap` override on QuestLog's Rewards box if `pre-wrap` is added to `.quest-description-box` directly (optional, not required).

## Deferred Ideas

None — discussion stayed within phase scope. Every additional item folded in (QuestLog desktop, Shop, card preview) is the same underlying bug pattern the phase title already targets, not a new capability.
