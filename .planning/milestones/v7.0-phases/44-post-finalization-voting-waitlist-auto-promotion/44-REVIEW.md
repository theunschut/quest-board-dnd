---
phase: 44-post-finalization-voting-waitlist-auto-promotion
reviewed: 2026-07-05T00:00:00Z
depth: standard
files_reviewed: 24
files_reviewed_list:
  - QuestBoard.Domain/Extensions/WaitlistOrdering.cs
  - QuestBoard.Domain/Interfaces/IPlayerSignupRepository.cs
  - QuestBoard.Domain/Interfaces/IPlayerSignupService.cs
  - QuestBoard.Domain/Interfaces/IQuestEmailDispatcher.cs
  - QuestBoard.Domain/Interfaces/IQuestService.cs
  - QuestBoard.Domain/Models/QuestBoard/PlayerSignup.cs
  - QuestBoard.Domain/Services/PlayerSignupService.cs
  - QuestBoard.Domain/Services/QuestService.cs
  - QuestBoard.Repository/Entities/PlayerSignupEntity.cs
  - QuestBoard.Repository/Migrations/20260704220948_AddLastVoteChangeTimeToPlayerSignup.Designer.cs
  - QuestBoard.Repository/Migrations/20260704220948_AddLastVoteChangeTimeToPlayerSignup.cs
  - QuestBoard.Repository/Migrations/QuestBoardContextModelSnapshot.cs
  - QuestBoard.Repository/PlayerSignupRepository.cs
  - QuestBoard.Repository/Properties/AssemblyInfo.cs
  - QuestBoard.Service/Components/Emails/WaitlistPromoted.razor
  - QuestBoard.Service/Controllers/QuestBoard/QuestController.cs
  - QuestBoard.Service/Jobs/QuestWaitlistPromotedEmailJob.cs
  - QuestBoard.Service/Services/HangfireQuestEmailDispatcher.cs
  - QuestBoard.Service/Services/NullQuestEmailDispatcher.cs
  - QuestBoard.Service/Views/Quest/Details.Mobile.cshtml
  - QuestBoard.Service/Views/Quest/Details.cshtml
  - QuestBoard.UnitTests/Extensions/WaitlistOrderingTests.cs
  - QuestBoard.UnitTests/QuestBoard.UnitTests.csproj
  - QuestBoard.UnitTests/Repository/PlayerSignupRepositoryTests.cs
  - QuestBoard.UnitTests/Services/QuestServiceTests.cs
findings:
  critical: 2
  warning: 3
  info: 2
  total: 7
status: issues_found
---

# Phase 44: Code Review Report

**Reviewed:** 2026-07-05T00:00:00Z
**Depth:** standard
**Files Reviewed:** 24
**Status:** issues_found

## Summary

Phase 44 adds post-finalization vote changes, a `LastVoteChangeTime` ordering column, waitlist
ordering/promotion, and a new "waitlist promoted" email. The core plumbing (migration, mapper,
dispatcher, email job, unit tests for the happy paths) is solid and well tested for the
`SignupRole.Player` case. However, the promotion trigger in both `ChangeVoteAsync` and
`RevokeSignupAsync` is **not scoped to `SignupRole.Player`**, while the seat-freed signal it
reacts to (`PlayerSignupRepository.ChangeVoteAsync`'s `wasSelected && vote == No` return, and
`RevokeSignupAsync`'s raw `signup.IsSelected` check) fires for *any* role, including
`AssistantDM` and `Spectator`. Since `TotalPlayerCount`/seat capacity and the waitlist candidate
pool are both `SignupRole.Player`-only, this produces a real cross-role promotion bug: revoking
or voting No on a selected Assistant DM or Spectator incorrectly promotes an unrelated waitlisted
Player. No test in the reviewed suite exercises a non-Player role through these two paths, which
is consistent with the bug going unnoticed.

Additionally, the `IQuestService.ChangeVoteAsync` XML doc contract ("A Maybe vote never changes
selection state and never triggers promotion") is stale — a later commit in this same phase
(`d26cf73`) deliberately changed the implementation so Maybe *can* fill an open seat, but the
interface doc comment was never updated to match, leaving a misleading contract for future
readers/callers.

## Critical Issues

### CR-01: Revoking a selected non-Player signup incorrectly promotes a waitlisted Player

**File:** `QuestBoard.Domain/Services/QuestService.cs:290-309`
**Issue:** `RevokeSignupAsync` triggers promotion based solely on `signup.IsSelected` (`wasSelected`),
with no check on `signup.Role`. `TotalPlayerCount` capacity and `GetTopWaitlistedCandidateAsync`
are both scoped to `SignupRole.Player` only (see `PlayerSignupRepository.cs:70`,
`QuestService.cs:273`). If a selected `AssistantDM` or `Spectator` revokes their signup, no
Player seat was actually freed, yet `PromoteNextWaitlistedPlayerIfSeatFreedAsync` still runs and
promotes (and emails) an unrelated waitlisted Player into a seat that was never vacated. This can
inflate the number of selected Players beyond `TotalPlayerCount`, since the "freed" seat never
existed for that role.
**Fix:**
```csharp
public async Task RevokeSignupAsync(int questId, int playerSignupId, CancellationToken token = default)
{
    var quest = await repository.GetQuestWithDetailsAsync(questId, token);
    if (quest == null) return;

    var signup = quest.PlayerSignups.FirstOrDefault(ps => ps.Id == playerSignupId);
    if (signup == null) return;

    // Only a selected Player-role signup frees a seat that counts against TotalPlayerCount.
    var wasSelectedPlayer = signup.IsSelected && signup.Role == SignupRole.Player;

    await playerSignupRepository.RemoveAsync(signup, token);

    if (!wasSelectedPlayer) return;

    var finalizedProposedDate = quest.ProposedDates
        .FirstOrDefault(pd => quest.FinalizedDate.HasValue && pd.Date.Date == quest.FinalizedDate.Value.Date);
    if (finalizedProposedDate == null) return;

    await PromoteNextWaitlistedPlayerIfSeatFreedAsync(questId, finalizedProposedDate.Id, freeingPlayerSignupId: playerSignupId, token);
}
```

### CR-02: A No vote from a selected non-Player signup incorrectly promotes a waitlisted Player

**File:** `QuestBoard.Repository/PlayerSignupRepository.cs:22-63` and `QuestBoard.Domain/Services/QuestService.cs:257-287`
**Issue:** `PlayerSignupRepository.ChangeVoteAsync` computes `wasSelected` and returns
`wasSelected && vote == VoteType.No` (the "seat freed" signal) without ever inspecting
`entity.SignupRole`. `QuestService.ChangeVoteAsync` acts on that boolean unconditionally and
calls `PromoteNextWaitlistedPlayerIfSeatFreedAsync`. Because `ChangeVote` in `QuestController` has
no role restriction (any authenticated signed-up user, regardless of role, can vote on the
finalized date), a selected `AssistantDM` or `Spectator` voting No will be reported as "seat
freed" and will promote a waitlisted Player — even though Assistant DM/Spectator seats are not
part of `TotalPlayerCount` capacity and no Player seat was actually vacated. This mirrors CR-01
and is unguarded in the same way.
**Fix:** Scope the seat-freed signal to Player-role signups only, e.g. in the repository:
```csharp
// PlayerSignupRepository.ChangeVoteAsync
var wasSelectedPlayer = entity.IsSelected && entity.SignupRole == (int)SignupRole.Player;

if (vote == VoteType.No && wasSelectedPlayer)
{
    entity.IsSelected = false;
}

await DbContext.SaveChangesAsync(cancellationToken);

return wasSelectedPlayer && vote == VoteType.No;
```
(Keep `IsSelected = false` unconditional on any No vote from a previously-selected signup if that
is intended for non-Player roles too — but the *promotion signal* returned to the caller must not
fire for non-Player roles, since the waitlist pool and capacity check are Player-only.)

## Warnings

### WR-01: Stale XML doc contract on `IQuestService.ChangeVoteAsync` contradicts implementation

**File:** `QuestBoard.Domain/Interfaces/IQuestService.cs:105-110`
**Issue:** The doc comment states "A Maybe vote never changes selection state and never triggers
promotion." Commit `d26cf73` ("fix(44-03): let a Maybe vote also fill an open seat, not just
Yes") deliberately changed `QuestService.ChangeVoteAsync` so a Maybe vote *can* select a
waitlisted signup into an open seat (`if (vote == VoteType.Yes || vote == VoteType.Maybe)`), and
this is explicitly covered by
`QuestServiceTests.ChangeVoteAsync_WaitlistedPlayerVotesMaybe_SelectsWhenSeatAvailable`. The
interface doc was never updated to match, so it now actively misleads future readers/callers
about the contract.
**Fix:**
```csharp
/// <summary>
/// Records a player's vote change on a finalized quest. A Yes or Maybe vote can select a
/// waitlisted player into an open seat when a fresh server-side seat count shows room (never
/// rejects on capacity). A selected player voting No frees their seat and triggers promotion
/// of the top waitlisted candidate. A No vote never grants or changes selection.
/// </summary>
```

### WR-02: `GetTopWaitlistedCandidateAsync` vote-priority sort duplicated verbatim in Domain and Repository

**File:** `QuestBoard.Repository/PlayerSignupRepository.cs:66-79,81-85` vs `QuestBoard.Domain/Extensions/WaitlistOrdering.cs:15-26`
**Issue:** The repository re-implements the exact same "order by vote priority, then by
`LastVoteChangeTime ?? SignupTime`" logic as `WaitlistOrdering.OrderWaitlist`/`VotePriority`,
instead of reusing the extension (e.g. by projecting entities to domain models first, or by
extracting the priority function into a shared helper). Two independent implementations of the
same business rule increase the chance they silently drift apart (e.g. if the vote-priority
mapping or tiebreak rule changes in one place and not the other).
**Fix:** Either fetch candidates as domain models and call `.OrderWaitlist(...)` directly, or
extract a single `VotePriority`/ordering helper shared by both layers (e.g. in
`WaitlistOrdering`, operating on `(int? vote, DateTime signupTime, DateTime? lastVoteChangeTime)`
tuples so it can be reused without a Domain→Repository dependency).

### WR-03: No test coverage for non-Player roles through the promotion-trigger paths

**File:** `QuestBoard.UnitTests/Services/QuestServiceTests.cs`, `QuestBoard.UnitTests/Repository/PlayerSignupRepositoryTests.cs`
**Issue:** Every `ChangeVoteAsync`/`RevokeSignupAsync` promotion test uses `MakeSignup(..., role: SignupRole.Player` (default)) or the repository test helper's default `SignupRole.Player`. No
test exercises a selected `AssistantDM` or `Spectator` revoking/voting No, which is exactly the
gap that let CR-01/CR-02 ship unnoticed.
**Fix:** Add cases such as
`RevokeSignupAsync_WhenRevokedSignupWasSelectedAssistantDM_DoesNotPromote` and
`ChangeVoteAsync_SelectedSpectatorVotesNo_DoesNotPromote` asserting
`_playerSignupRepository.DidNotReceive().UpdateAsync(...)` and
`_dispatcher.DidNotReceive().EnqueueWaitlistPromotedEmail(...)`.

## Info

### IN-01: `waitlistFinalizedProposedDate` uses `.Date.Date` while sibling lookups in the same view use exact `DateTime` equality

**File:** `QuestBoard.Service/Views/Quest/Details.cshtml:69-70,84-85,215-216`
**Issue:** The new waitlist-ordering lookup at line 70 normalizes with `pd.Date.Date ==
Model.Quest.FinalizedDate.Value.Date`, but the pre-existing `finalizedProposedDateForSelected`
(line 85) and the waitlist vote-badge lookup (line 216) both use exact `pd.Date ==
Model.Quest.FinalizedDate` in the same code block. This works today only because
`FinalizeQuestAsync` happens to persist `FinalizedDate` as exactly the selected `ProposedDate.Date`
value; if that invariant is ever relaxed (e.g. time-of-day added to `FinalizedDate` independently
of the proposed date), the exact-equality lookups will silently stop matching and votes will
render as "No Vote" instead of failing loudly. Since this phase already introduced the safer
`.Date.Date` pattern once in this exact function, consider using it consistently for all three
lookups.
**Fix:** Reuse the already-computed `waitlistFinalizedProposedDate` (or a shared `.Date.Date`
lookup) for the participant and waitlist vote-badge lookups instead of duplicating three
non-consistent match expressions.

### IN-02: `IPlayerSignupRepository.ChangeVoteAsync` doc doesn't mention role scoping either

**File:** `QuestBoard.Domain/Interfaces/IPlayerSignupRepository.cs:13-20`
**Issue:** The doc comment describes the "seat freed" return contract but, like the `IQuestService`
counterpart (WR-01), doesn't mention that the underlying implementation currently applies to any
`SignupRole`, not just `Player`. Once CR-02 is fixed, update this doc alongside it so the contract
stays accurate for the actual (fixed) scoping behavior.
**Fix:** After fixing CR-02, add a line such as: "The seat-freed signal is scoped to
`SignupRole.Player` signups; non-Player signups' `IsSelected` is still updated but never reports
a seat as freed."

---

_Reviewed: 2026-07-05T00:00:00Z_
_Reviewer: Claude (gsd-code-reviewer)_
_Depth: standard_
