# Phase 44: Post-Finalization Voting & Waitlist Auto-Promotion - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-07-04
**Phase:** 44-Post-Finalization Voting & Waitlist Auto-Promotion
**Areas discussed:** Vote-change UI, Mobile parity, Revoke vs Vote No, Promotion email

---

## Vote-Change UI

**First pass (clarification round):**

| Option | Description | Selected |
|--------|-------------|----------|
| Three explicit buttons | Vote Yes / Vote Maybe / Vote No buttons, matching existing pre-finalization vote UI | |
| Single vote toggle/dropdown | One compact control showing current vote, changeable in place | |
| Keep it minimal | Keep existing Join/Vote Yes action; add only one new "Vote No / Give up seat" button for selected players | |

**User's response:** Interpreted the question as being about a brand-new player joining a finalized quest, and noted that should stay an automatic Yes with no Maybe/No option — correct existing behavior, no change needed there. This surfaced a need to re-ask with clearer framing distinguishing "new player joining" from "existing signed-up player changing their vote after the fact."

**Second pass (clarified):**

| Option | Description | Selected |
|--------|-------------|----------|
| Three explicit buttons | Vote Yes/Maybe/No buttons on their own row/participant entry, matching pre-finalization vote UI style | ✓ |
| Single vote toggle/dropdown | One compact control, less clutter but a new UI pattern | |
| Just a "can't make it" action | Single "Can't make it" button (=vote No) for selected players; no Maybe UI post-finalization | |

**User's choice:** Three explicit buttons (Vote Yes / Vote Maybe / Vote No).
**Notes:** Buttons go in the same row as the existing "Revoke Signup" button — Revoke stays left-aligned, the three vote buttons pushed to the right of that row. Applies to both selected participants and waitlisted players.

---

## Mobile Parity

| Option | Description | Selected |
|--------|-------------|----------|
| Yes, full parity this phase | Build waitlist section + vote controls for mobile in Phase 44 itself | ✓ |
| Desktop only, defer mobile | Ship desktop-only, log a follow-up backlog item for mobile | |

**User's choice:** Yes, full parity this phase.
**Notes:** Consistent with this project's recent precedent (Phase 43 was a dedicated mobile-parity phase); avoids recreating a #115/#116-style backlog gap immediately.

---

## Revoke vs Vote No

| Option | Description | Selected |
|--------|-------------|----------|
| Keep both, both trigger promotion | "Vote No" keeps the record (waitlist-visible); "Revoke" still fully deletes. Both free a seat and trigger promotion. | ✓ |
| Retire Revoke post-finalization | Remove standalone Revoke once finalized; only offer Vote No | |

**User's choice:** Keep both, both trigger promotion.

---

## Promotion Email

| Option | Description | Selected |
|--------|-------------|----------|
| Reuse FinalizedEmail styling | Same HTML template family/branding as the existing quest-finalized email, new copy only | ✓ |
| I want to specify the content/tone | User describes specific content/emphasis | |

**User's choice:** Reuse FinalizedEmail styling.

---

## Claude's Discretion

- Exact wording/copy for the promotion email subject and body (within the reused `FinalizedEmail` visual style).
- Data-model mechanism for tracking the "waitlist ordering timestamp" that VOTE-03 requires to reset on vote change (reuse `PlayerSignup.SignupTime` vs. a new field) — implementation detail for research/planning, not a user-facing behavior choice.
- Fixing the latent `Vote = 0`/`VoteType.Yes` mismatch bug in `PlayerSignupRepository.ChangeVoteToYesAndSelectAsync` while rewriting this method for the new vote-change flow.

## Deferred Ideas

None — discussion stayed within phase scope. VOTE-08 (in-app "you were promoted" banner) is an existing documented v2-deferred requirement, not a new idea raised in this discussion.
