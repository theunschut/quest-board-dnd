# Phase 54: Fix mobile signup for finalized quests (inconsistent with desktop) - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-07-06
**Phase:** 54-fix-mobile-signup-for-finalized-quests-inconsistent-with-des
**Areas discussed:** Bug confirmation, Join card layout/content, Placement, Full-quest/waitlist scope, Character selection UX, Role availability messaging

---

## Bug confirmation

No SPEC.md or original user report existed for this phase — the ROADMAP title was the only input. Claude traced the actual gap via code inspection before asking anything: `Details.Mobile.cshtml`'s "not yet signed up" section is hard-gated on `IsFinalized == false`, so mobile has no way for a new player to join a finalized quest at all, unlike desktop's `JoinFinalizedQuest`-backed "Join This Quest" card.

| Option | Description | Selected |
|--------|-------------|----------|
| Yes, that's it | New (not-yet-signed-up) players can't join a finalized quest on mobile at all — that's the gap to fix. | ✓ |
| Something else | I hit a different inconsistency — let me describe what I actually saw. | |

**User's choice:** Yes, that's it.
**Notes:** Confirmed the root cause before any implementation decisions were discussed.

---

## Join card layout/content

| Option | Description | Selected |
|--------|-------------|----------|
| Mirror desktop exactly | 3 stacked buttons (Player/Assistant DM/Spectator), one shared character select above them, synced via the same JS pattern desktop uses. | ✓ |
| Simplify to role dropdown + one button | A single role select + one "Join" button, matching the pre-finalization signup form's existing dropdown pattern. | |

**User's choice:** Mirror desktop exactly.
**Notes:** None.

---

## Placement

| Option | Description | Selected |
|--------|-------------|----------|
| Right after header, before participant list | Matches Claude's initial (incorrect) read of desktop's order. | |
| After participant/waitlist lists | Different order than Claude's initial claim. | |

**User's choice:** Other (free text) — "are you sure this is correct? When i open the page, i see the buttons below the participant/waitlist lists?"
**Notes:** Claude's initial claim about desktop's visual order was wrong. Re-reading `Details.cshtml`'s brace structure confirmed the participant/waitlist tables render inside the shared "Quest Finalized!" alert (visible to everyone), with the `userCanJoin` "Join This Quest" card following afterward — i.e., participant list → waitlist list → join card. Corrected and locked in against the live page + source, matching mobile's existing list order (waitlist list, then new card, then DM/revoke section).

---

## Full-quest / waitlist scope

**Round 1 — should this phase touch it at all:**

| Option | Description | Selected |
|--------|-------------|----------|
| Mirror desktop bug-for-bug | Mobile gets the same hard "quest is full" rejection for new Player joins. | |
| Also waitlist new joiners | Expand `JoinFinalizedQuest` (both platforms) so a full quest waitlists a new Player joiner instead of rejecting. | |

**User's choice:** Other (free text) — "I thought this was fixed in a previous phase? I believe 44?"
**Notes:** Claude checked `.planning/phases/44-.../44-CONTEXT.md` and found this was in fact explicitly decided the opposite way: Phase 44's D-03 (locked) deliberately kept `JoinFinalizedQuest` unchanged — brand-new players always get an automatic Yes or a hard reject, no waitlist. Reported this back to the user with the citation.

**Round 2 — given D-03 was a deliberate prior boundary, how to treat it now:**

| Option | Description | Selected |
|--------|-------------|----------|
| Leave it alone, mirror on mobile | Stay scoped to the reported bug; mobile inherits the same hard-reject-when-full behavior. | |
| Reopen D-03 now, add waitlist for new joiners | Expand this phase to waitlist new Player joins on a full quest, on both platforms. | |

**User's choice:** Other (free text) — "I want any player to be able to join a finalized quest, even when it's already 'full'. But when this happens, the player should be added to the waitlist. That's what i had in mind"
**Notes:** Explicit scope expansion, confirmed by the user (not Claude-initiated). Became D-03 through D-06 in CONTEXT.md.

**Round 3 — mechanism/scope confirmation:**

| Option | Description | Selected |
|--------|-------------|----------|
| Yes, fix both platforms | Since it's one shared controller action, fix it once — both desktop and mobile benefit. | ✓ |
| Mobile only somehow | Only mobile's new joiners get waitlisted; desktop keeps hard-rejecting (not really feasible without duplicating logic). | |

**User's choice:** Yes, fix both platforms.
**Notes:** Confirmed the fix is to `IsSelected = false` on the created signup instead of the current `ModelState` reject, reusing Phase 44's existing `OrderWaitlist`/`PromoteNextWaitlistedPlayerIfSeatFreedAsync` machinery unchanged.

**Round 4 — messaging copy:**

| Option | Description | Selected |
|--------|-------------|----------|
| "Player slots full — you'll join the waitlist" | Explicit up-front copy so there's no surprise after submit. | |
| Leave exact wording to Claude | Trust the implementer to write clear, consistent copy. | ✓ |

**User's choice:** Leave exact wording to Claude.
**Notes:** None.

---

## Character selection UX

Already resolved by the "Join card layout/content" decision (shared character `<select>` synced via desktop's existing JS pattern) — no separate question needed. Confirmed with the user this required no further discussion before moving to the next area.

---

## Role availability messaging

| Option | Description | Selected |
|--------|-------------|----------|
| Only Player needs waitlist messaging | Assistant DM/Spectator are always immediate, uncapped joins — no new copy needed for them. | ✓ |
| Leave all copy details to Claude | Trust the implementer across all three roles without a per-role decision. | |

**User's choice:** Only Player needs waitlist messaging.
**Notes:** None.

---

## Claude's Discretion

- Exact copy/wording for the updated "quest full → you'll join the waitlist" messaging.
- Minor markup/CSS choices for the new mobile card (follow existing `quest-section-card-mobile` conventions).

## Deferred Ideas

None — all discussed areas resolved into in-scope decisions (D-01 through D-06).
