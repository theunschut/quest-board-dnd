# Phase 44: Post-Finalization Voting & Waitlist Auto-Promotion - Context

**Gathered:** 2026-07-04
**Status:** Ready for planning

<domain>
## Phase Boundary

Remove the hard capacity wall on finalized **One-Shot** quests, order the resulting waitlist by vote priority (Yes > Maybe > No, then time), automatically promote the top waitlisted candidate when a selected player steps back, and send a narrowly-targeted "you were promoted" email to only that one player. This phase covers requirements VOTE-01 through VOTE-07.

**Not in scope:** Campaign-board quests (no date voting/signup on that board type at all — untouched), the "brand-new player joins a finalized quest" flow (`JoinFinalizedQuest`), which stays an automatic Yes with no change, and anything to do with the image-cropping work (Phases 45/46 — different tables entirely).

</domain>

<decisions>
## Implementation Decisions

### Vote-Change UI (for players who already signed up before finalization)
- **D-01:** A player who already has a signup record on a finalized quest (selected or waitlisted) gets three explicit buttons — **Vote Yes / Vote Maybe / Vote No** — matching the style of the existing pre-finalization vote UI already used elsewhere in this app. This satisfies VOTE-04 (selected player votes No → seat frees + promotion), VOTE-05 (selected player votes Maybe → keeps seat, no promotion), and VOTE-06 (waitlisted player votes No → stays on waitlist, record retained, sorts to bottom).
- **D-02:** These three vote buttons render in the **same row as the existing "Revoke Signup" button** — Revoke stays left-aligned; Vote Yes/Maybe/No are pushed to the right side of that row. Applies to both the selected-participants section and the waitlist section.
- **D-03 (locked, not new scope):** A **brand-new player** joining a finalized quest for the first time (never signed up before) still goes through the existing `JoinFinalizedQuest` action unchanged — always an automatic Yes signup. No Maybe/No option is offered at initial-join time; the three-button vote-change UI only applies to players who already have a `PlayerSignup` row.

### Revoke vs. Vote No
- **D-04:** Keep **both** actions available post-finalization, side by side. "Revoke Signup" remains a hard delete of the `PlayerSignup` row (existing `RevokeSignup`/`RemoveAsync` behavior, unchanged). "Vote No" is a new, softer action that keeps the record (visible on the waitlist per VOTE-06, or moved to the waitlist per VOTE-04) rather than deleting it. **Both** must trigger the seat-freed/auto-promotion check when the voter was previously selected.

### Mobile Parity
- **D-05:** Build full mobile parity in this same phase — port the waitlist table + vote-change buttons + promotion behavior to `Details.Mobile.cshtml`, matching whatever desktop `Details.cshtml` ships. Desktop currently has a waitlist section; mobile currently has none at all. Given this project just spent all of Phase 43 closing mobile-parity gaps (#115/#116), shipping this feature mobile-incomplete would just recreate that same backlog pattern — so mobile ships alongside desktop, not deferred.

### Promotion Email
- **D-06:** Reuse the existing `FinalizedEmail` template's visual styling/branding (same Razor + HtmlRenderer + Hangfire + Resend pipeline already used 6+ times in this codebase) — just new copy explaining the player was promoted off the waitlist into a freed seat. No new template family, no new infrastructure.

### Claude's Discretion
- Exact wording/copy for the promotion email subject and body (within the reused `FinalizedEmail` visual style) is Claude's call during planning/implementation.
- How "the timestamp used for waitlist ordering" is tracked at the data-model level (e.g., reusing `PlayerSignup.SignupTime` by updating it on every vote change, vs. adding a new field) is an implementation detail for research/planning to resolve — not a user-facing behavior choice. Whatever is chosen must satisfy VOTE-03 (any vote change resets the ordering timestamp).
- Fixing the latent `Vote = 0` bug in `PlayerSignupRepository.ChangeVoteToYesAndSelectAsync` (see Code Context below) as part of rewriting this method — not a separate decision, just correct-by-construction since Phase 44 rebuilds this code path anyway.

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Requirements & Roadmap
- `.planning/ROADMAP.md` §"Phase 44: Post-Finalization Voting & Waitlist Auto-Promotion" — phase goal, success criteria, requirements mapping
- `.planning/REQUIREMENTS.md` §"Post-Finalization Voting" — VOTE-01 through VOTE-07 full text, plus v2 deferred VOTE-08 (in-app promotion banner — explicitly out of scope for this phase)

### Project-Level Research (pre-dates roadmap, partially covers this phase)
- `.planning/research/SUMMARY.md` §"Phase 4: Waitlist auto-promotion notification" — locked notification approach (targeted email only, reuse existing infra), confidence assessment
- `.planning/research/FEATURES.md` — full must-have/nice-to-have/anti-pattern breakdown for waitlist UX (targeted notification, no broadcast, no SMS/push); note this research covered the *notification* UX only — it did **not** scout the existing codebase's partial waitlist implementation (see Code Context below), so its "no dependency, standalone feature" framing understates how much existing code this phase touches and rewrites.

</canonical_refs>

<code_context>
## Existing Code Insights

**Important:** This phase is NOT greenfield. A partial, desktop-only implementation of "waitlist" already exists and must be extended/fixed, not built from scratch. Read this section carefully before researching or planning.

### Reusable Assets
- `PlayerSignup.IsSelected` (bool) — already IS the selected-vs-waitlisted distinction. No new field needed for this.
- `PlayerSignup.SignupTime` — existing timestamp currently used for waitlist ordering (ascending). VOTE-03 requires this ordering-timestamp to reset on vote change — decide during planning whether to repurpose this field or add a new one (see Claude's Discretion above).
- The fetch/AJAX pattern used by `revokeSignup()`/`changeVoteToYes()` in `QuestBoard.Service/Views/Quest/Details.cshtml` (POST/DELETE with `__RequestVerificationToken` FormData, `location.reload()` on success, `alert()` on failure) — reuse this exact pattern for the new Vote Yes/Maybe/No actions, both desktop and mobile.
- `IQuestEmailDispatcher` + existing `FinalizedEmail` Razor/HtmlRenderer template + Hangfire job pattern (see `QuestService.FinalizeQuestAsync`) — the promotion email is a new trigger condition + new template following this exact established pattern, not new plumbing.
- `SessionReminderJob` already computes its recipient list dynamically from `PlayerSignups.Where(ps => ps.IsSelected)` at send-time (`QuestBoard.Service/Jobs/SessionReminderJob.cs:79`) — auto-promotion (flipping `IsSelected`) will flow through to reminders automatically with no separate wiring needed.

### Established Patterns (existing, partial implementation to extend)
- **`ChangeVoteToYes` action + `ChangeVoteToYesAndSelectAsync`** (`QuestBoard.Service/Controllers/QuestBoard/QuestController.cs:553`, `QuestBoard.Repository/PlayerSignupRepository.cs:21`) — already lets a waitlisted player self-promote to Yes+Selected. **Currently hard-rejects with `BadRequest` when the quest is full** (`QuestController.cs:582`) — this exact block is what VOTE-01 requires removing (vote Yes should always succeed; land on/stay on waitlist instead of rejection).
- **Known bug to fix while rewriting:** `ChangeVoteToYesAndSelectAsync` sets `entity.DateVotes...Vote = 0` with a comment claiming `// VoteType.Yes = 0` (`PlayerSignupRepository.cs:36,44`). Actual enum is `VoteType { No, Maybe, Yes }` (`QuestBoard.Domain/Enums/VoteType.cs`) — so `Yes = 2`, not `0`. The literal `0` actually stores `VoteType.No`. Confirmed by `VoteType.Yes` usages elsewhere (`Details.cshtml:157,257`). This silently corrupts the stored vote for anyone who has used "Change Vote to Yes" — matters directly for VOTE-02's vote-priority ordering.
- **`Details.cshtml`'s existing waitlist table** (lines 64-67, 189-290) — desktop already renders `waitlistPlayers = PlayerSignups.Where(!IsSelected && Role == Player).OrderBy(SignupTime)`. This ordering must change to vote-priority-then-time per VOTE-02. The view currently computes this list with inline LINQ in Razor — planning should decide whether to keep that pattern (consistent with existing code) or move it to a ViewModel/service method (cleaner, but a bigger diff).
- **`RevokeSignup`** (`QuestController.cs:605`) — hard-deletes the `PlayerSignup` row, available at any time today, does **not** currently trigger any promotion check. Must be wired to check-and-promote when the revoked signup was `IsSelected`.
- **`UpdateSignup`** (the general per-date vote-change endpoint, `QuestController.cs:489`) — explicitly blocked once `quest.IsFinalized` (`return NotFound()` at line 492). This confirms there is currently **no** endpoint at all for changing a vote once finalized except the narrow `ChangeVoteToYes`. The new Vote Yes/Maybe/No actions are net-new controller actions, not a relaxation of this existing guard.
- **Mobile gap:** `Details.Mobile.cshtml` has none of the above — no waitlist section, no vote-change buttons, no `ChangeVoteToYes`/`RevokeSignup` JS at all today for finalized-quest views. Per D-05, this phase builds mobile from scratch to parity with whatever desktop ships.

### Integration Points
- New vote-change logic lives alongside the existing `ChangeVoteToYes` action/repository method in `QuestController.cs` and `PlayerSignupRepository.cs` — likely consolidating into a single "vote + auto-promote" code path shared by Vote Yes / Vote Maybe / Vote No, rather than three near-duplicate methods.
- Promotion trigger must be checked from **two** call sites: the new "Vote No" action (for a previously-selected player) and `RevokeSignup` (for a previously-selected player who fully deletes their signup). Both need to find the top-ordered waitlisted candidate and flip `IsSelected = true` on them, then enqueue the targeted promotion email for exactly that one player.
- `Quest.TotalPlayerCount` is the capacity field already used by all existing capacity checks (`ChangeVoteToYes`, `JoinFinalizedQuest`) — reuse it for "is there a free seat" logic; do not add a new capacity field.

</code_context>

<specifics>
## Specific Ideas

- Vote Yes / Maybe / No buttons sit in the same row as "Revoke Signup", with Revoke on the left and the three vote buttons pushed to the right of that row — an explicit layout instruction from the user, applies to both desktop and mobile.

</specifics>

<deferred>
## Deferred Ideas

None — discussion stayed within phase scope. (VOTE-08, the in-app "you were promoted" banner, is already a documented v2 deferred requirement in REQUIREMENTS.md, not a new idea from this discussion.)

</deferred>

---

*Phase: 44-Post-Finalization Voting & Waitlist Auto-Promotion*
*Context gathered: 2026-07-04*
