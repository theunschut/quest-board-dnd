# Phase 54: Fix mobile signup for finalized quests (inconsistent with desktop) - Context

**Gathered:** 2026-07-06
**Status:** Ready for planning

<domain>
## Phase Boundary

The ROADMAP title was vague ("inconsistent with desktop") with no prior user report captured — the actual bug was traced from code, then confirmed with the user before any implementation decisions were made.

**Confirmed root cause:** On a finalized One-Shot quest, a player who has **not** signed up yet can join on desktop via a "Join This Quest" card (`QuestController.JoinFinalizedQuest` — pick Player/Assistant DM/Spectator, optional character, auto-approved). `Details.Mobile.cshtml` has no equivalent section at all — mobile's "not yet signed up" block is hard-gated on `IsFinalized == false`, so on mobile there is currently no way to join a quest once it's finalized.

Vote-changing for players who signed up **before** finalization (Phase 44's waitlist/vote-change UI) already has full mobile/desktop parity — that part is not part of this bug.

**Scope grew during discussion:** while confirming the "full quest" behavior the new mobile card would inherit, the user chose to reopen Phase 44's locked D-03 decision (`JoinFinalizedQuest` always hard-rejects a brand-new Player join once the quest is full) and fix it for both platforms — see D-03 through D-06 below. This is a deliberate, user-directed scope expansion, not scope creep: same shared controller action, same underlying "can a new player join a finalized quest" problem the phase title is about.

**Not in scope:** Assistant DM / Spectator joins are unaffected — no capacity check exists for those roles today (always immediate/auto-approved), and that stays true. Campaign-board quests are unaffected (no date voting/signup on that board type at all).

</domain>

<decisions>
## Implementation Decisions

### Mobile "Join This Quest" card
- **D-01:** Add a "Join This Quest" card to `Details.Mobile.cshtml` mirroring desktop's structure exactly: 3 stacked full-width buttons (Join as Player / Join as Assistant DM / Join as Spectator), each its own form posting to `JoinFinalizedQuest`, all reading from one shared character `<select>` synced across the 3 hidden `characterId` inputs via the same inline JS pattern `Details.cshtml` already uses (lines 370-378). No mobile-specific simplification (e.g. single role-dropdown) — full parity with desktop's interaction model.
- **D-02:** Placement: insert the new card immediately after the waitlist list section (`Details.Mobile.cshtml` lines 214-254) and before the "DM controls link" section (line 257) — this matches desktop's actual visual order once traced correctly (Quest Finalized alert → Participants table → Waitlist table → Join This Quest card at `Details.cshtml` line 293). Initial assumption during discussion (join card before the lists) was wrong and corrected against the live page/source before locking this in.

### Full-quest / waitlist scope (reopens Phase 44 D-03)
- **D-03:** `JoinFinalizedQuest` must stop hard-rejecting a new Player join when the quest is at capacity. Instead of the current `ModelState.AddModelError("", "This quest is full...")` + redirect (the block inside the `if (role == SignupRole.Player)` capacity check, `QuestController.cs` lines 438-448), create the `PlayerSignup` with `IsSelected = false` (waitlisted) rather than `true`. This reuses the exact same waitlist table/ordering (`WaitlistOrdering.OrderWaitlist`) and auto-promotion (`QuestService.PromoteNextWaitlistedPlayerIfSeatFreedAsync`) Phase 44 already built for existing signups — no new waitlist mechanism, just routing a new joiner into the existing one instead of rejecting them.
- **D-04:** Since `JoinFinalizedQuest` is one shared controller action, this fix applies to **both** desktop and mobile identically. User confirmed explicitly — this is not a mobile-only behavior change, it corrects the action for every caller.
- **D-05:** Assistant DM / Spectator role joins are unaffected by D-03 — no capacity check exists for those roles today (the capacity branch in `JoinFinalizedQuest` only runs `if (role == SignupRole.Player)`), and that stays true. Only the Player-role branch changes.
- **D-06:** The join card's "quest full" messaging (`Details.cshtml` line 314: "Player slots full, but you can join as Assistant DM or Spectator!") is now inaccurate once a full-quest Player join succeeds via waitlist instead of being blocked — it must be rewritten on both platforms to reflect that a Player join still succeeds but lands on the waitlist. Only the Player-role copy needs updating (per D-05, Assistant DM/Spectator are always-immediate and need no waitlist wording). Exact wording is Claude's discretion (see below) — should read consistently in tone with the existing "You are on the waitlist for this quest" message (`Details.cshtml` line 56 / mobile's waitlist section).

### Claude's Discretion
- **Exact copy/wording** for the updated "quest full → you'll join the waitlist" messaging (D-06). Should be clear that clicking "Join as Player" still succeeds but results in a waitlist position, not immediate seating.
- **Minor markup/CSS choices** for the new mobile card — follow the existing mobile card conventions already used elsewhere in `Details.Mobile.cshtml` (`quest-section-card-mobile` class, icon + heading pattern) rather than inventing a new visual style.

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### The bug (mobile missing the Join card)
- `QuestBoard.Service/Views/Quest/Details.cshtml` (lines 33-382) — full desktop finalized-quest block: "Quest Finalized!" alert (61-291, includes participants table 83-195 and waitlist table 197-290) + `userCanJoin` "Join This Quest" card (293-382, 3-button + character-select structure to port to mobile per D-01).
- `QuestBoard.Service/Views/Quest/Details.Mobile.cshtml` (lines 181-296) — current mobile structure: participant list (182-211), waitlist list (214-254), DM manage link (257-263), revoke/vote section (266-294). New Join card goes between waitlist list and DM manage link per D-02. Note mobile's "not yet signed up" section (line 144) is gated on `Model.Quest?.IsFinalized == false` — that gate is correct and stays; the new card is a separate, additional section for the finalized case.

### The bug (JoinFinalizedQuest hard-rejects when full)
- `QuestBoard.Service/Controllers/QuestBoard/QuestController.cs` — `JoinFinalizedQuest` action (lines 414-491). The `if (role == SignupRole.Player)` capacity-check block (lines 438-448) currently returns a `ModelState` error + redirect when `selectedPlayersCount >= quest.TotalPlayerCount`; per D-03 this must instead create the signup with `IsSelected = false`.
- `QuestBoard.Domain/Extensions/WaitlistOrdering.cs` (`WaitlistOrdering.OrderWaitlist`, line 15) — the existing waitlist ordering extension (Yes > Maybe > No, then `LastVoteChangeTime`-or-`SignupTime` tiebreak) that a waitlisted new joiner automatically participates in once `IsSelected = false` — no changes needed to this extension itself.
- `QuestBoard.Domain/Services/QuestService.cs` — `PromoteNextWaitlistedPlayerIfSeatFreedAsync` (line 317) — the existing auto-promotion logic (runs on vote-No/revoke of a selected player) that will pick up a waitlisted new joiner exactly like any other waitlisted signup — no changes needed here either.

### Phase 44 decision being reopened
- `.planning/phases/44-post-finalization-voting-waitlist-auto-promotion/44-CONTEXT.md` — D-03 (locked): "A brand-new player joining a finalized quest for the first time... still goes through the existing `JoinFinalizedQuest` action unchanged — always an automatic Yes signup... Not in scope." This phase explicitly overrides that boundary per the user's direction above.
- `.planning/phases/44-post-finalization-voting-waitlist-auto-promotion/44-03-PLAN.md` — documents the three-button vote-change UI (Yes/Maybe/No) that D-01's new mobile card must NOT be confused with — the new Join card is for users with no `PlayerSignup` row at all (initial join), distinct from the existing vote-change buttons for already-signed-up users (which mobile already has, unchanged).

No external ADRs/specs govern this beyond the prior-phase context above — requirements are fully captured in the decisions section.

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- Desktop's `JoinFinalizedQuest` 3-button + shared-character-select markup (`Details.cshtml` lines 293-380) — the exact structure D-01 ports to mobile, including the inline JS character-sync script (lines 370-378).
- `WaitlistOrdering.OrderWaitlist` and `QuestService.PromoteNextWaitlistedPlayerIfSeatFreedAsync` — both already handle arbitrary waitlisted `PlayerSignup` rows; D-03's fix only needs to change how a new joiner's `IsSelected` flag is set, not touch either of these.
- Mobile's `quest-section-card-mobile` CSS class and icon+heading header pattern — already used for the "Update Your Vote" / "Choose a Date" sections in `Details.Mobile.cshtml`; the new Join card should follow this same visual convention.

### Established Patterns
- `QuestController.JoinFinalizedQuest` already recomputes `selectedPlayersCount` server-side immediately before deciding `IsSelected` (never trusts client state) — D-03's fix keeps this same recompute-then-decide shape, just changes the decision's outcome from reject to waitlist.
- Both `.cshtml`/`.Mobile.cshtml` view pairs in this codebase share the same controller action and ViewBag data (`UserCharacters`, `CalendarMonths`, `IsPlayerSignedUp`, `BoardType`) — the mobile view is missing this section purely because a Razor block wasn't written, not because backend data is unavailable. No controller/ViewBag changes are needed for D-01/D-02.

### Integration Points
- `QuestController.JoinFinalizedQuest` (shared by both views) is the only controller code touched by D-03. D-01/D-02 are pure Razor view additions (no new controller action, service, or ViewModel).

</code_context>

<specifics>
## Specific Ideas

No original user bug report existed for this phase — the ROADMAP title was the only input. The actual gap was located via code inspection (comparing `Details.cshtml` vs `Details.Mobile.cshtml`'s `IsFinalized` branches) and confirmed with the user before any decisions were made.

During discussion, the user corrected an incorrect assumption about desktop's visual order (join card was initially assumed to render *before* the participant/waitlist lists; live-page observation showed it renders *after* them) — resolved by re-reading `Details.cshtml`'s actual brace structure, which confirmed the participant/waitlist tables render inside the shared "Quest Finalized!" alert (visible to everyone), with the `userCanJoin` card following afterward, outside the `currentUserSignedUp` branch.

The user then explicitly expanded scope mid-discussion: "I want any player to be able to join a finalized quest, even when it's already 'full'. But when this happens, the player should be added to the waitlist." This is what became D-03 through D-06, reopening Phase 44's locked D-03 boundary by deliberate user direction.

</specifics>

<deferred>
## Deferred Ideas

None — all four discussed areas (join card layout, placement, full-quest/waitlist scope, role messaging) resolved into in-scope decisions (D-01 through D-06). No ideas were pushed out to a future phase.

</deferred>

---

*Phase: 54-fix-mobile-signup-for-finalized-quests-inconsistent-with-des*
*Context gathered: 2026-07-06*
