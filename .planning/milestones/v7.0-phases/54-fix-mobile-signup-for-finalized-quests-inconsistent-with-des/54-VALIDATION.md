---
phase: 54
slug: fix-mobile-signup-for-finalized-quests-inconsistent-with-des
status: draft
nyquist_compliant: false
wave_0_complete: false
created: 2026-07-06
---

# Phase 54 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xunit.v3 (xunit.runner.visualstudio 3.1.5) — confirmed via `QuestBoard.UnitTests.csproj` / `QuestBoard.IntegrationTests.csproj` |
| **Config file** | none — standard SDK-style `.csproj` test projects, no separate xunit config file |
| **Quick run command** | `dotnet test --filter "FullyQualifiedName~QuestController|FullyQualifiedName~PlayerSignupRepository|FullyQualifiedName~MobileViewsTests"` |
| **Full suite command** | `dotnet test` |
| **Estimated runtime** | Quick filter well under 30s given existing suite sizes; full suite per project baseline |

---

## Sampling Rate

- **After every task commit:** Run `dotnet test --filter "FullyQualifiedName~QuestController|FullyQualifiedName~PlayerSignupRepository|FullyQualifiedName~MobileViewsTests"`
- **After every plan wave:** Run `dotnet test` (full suite)
- **Before `/gsd:verify-work`:** Full suite must be green, plus the real-device manual verification checkpoint (see Manual-Only Verifications)
- **Max feedback latency:** 30 seconds (targeted filter)

---

## Per-Task Verification Map

| Decision | Behavior | Test Type | Automated Command | File Exists | Status |
|----------|----------|-----------|-------------------|-------------|--------|
| D-01/D-02 | Mobile renders "Join This Quest" card (3 buttons + character select) for an authenticated, not-yet-signed-up user on a finalized One-Shot quest, positioned after the waitlist section | integration | `dotnet test --filter FullyQualifiedName~MobileViewsTests` | ❌ W0 — extend `QuestBoard.IntegrationTests/Mobile/MobileViewsTests.cs` | ⬜ pending |
| D-01/D-02 (negative) | Mobile does NOT render the card for a user already signed up, or for an unauthenticated user | integration | `dotnet test --filter FullyQualifiedName~MobileViewsTests` | ❌ W0 — same file | ⬜ pending |
| D-03 | `JoinFinalizedQuest` creates a `PlayerSignup` with `IsSelected = false` when `selectedPlayersCount >= TotalPlayerCount` for a Player-role join, instead of a `ModelState` error | integration | new test class, e.g. `dotnet test --filter FullyQualifiedName~QuestJoinFinalizedQuestTests` | ❌ W0 — no existing coverage of `JoinFinalizedQuest` at all | ⬜ pending |
| D-03 (regression guard) | A Player join with available space still sets `IsSelected = true` (unchanged behavior) | integration | same new test file | ❌ W0 | ⬜ pending |
| D-05 | Assistant DM / Spectator joins remain always-`IsSelected = true` regardless of Player-slot fullness | integration | same new test file | ❌ W0 | ⬜ pending |
| D-03 + waitlist integration | A `JoinFinalizedQuest`-created waitlisted signup is correctly included and ordered by `WaitlistOrdering.OrderWaitlist` / `GetTopWaitlistedCandidateAsync` alongside pre-existing waitlisted signups | unit | `dotnet test --filter FullyQualifiedName~PlayerSignupRepositoryTests` | ✅ exists — extend following `GetTopWaitlistedCandidateAsync_SameVote_OrdersByLastVoteChangeTimeFallingBackToSignupTime` (lines 271-298) | ⬜ pending |
| D-06 | Updated "quest full" copy appears consistently on both `Details.cshtml` and `Details.Mobile.cshtml`, no longer implying a hard block for Player role | integration | `dotnet test --filter FullyQualifiedName~MobileViewsTests` (mobile) + manual/visual check (desktop) | ❌ W0 | ⬜ pending |
| Antiforgery coverage (regression guard) | `JoinFinalizedQuest` still carries `[ValidateAntiForgeryToken]` after the edit (no new action introduced) | unit | `dotnet test --filter FullyQualifiedName~AntiForgeryTokenCoverageTests` | ✅ exists — no changes needed, existing reflection test auto-covers this | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

- [ ] New integration test file/class for `JoinFinalizedQuest` capacity-branch behavior (D-03/D-05) — likely `QuestBoard.IntegrationTests/Controllers/QuestJoinFinalizedQuestTests.cs` or an extension of `QuestControllerIntegrationTests_Comprehensive.cs`
- [ ] New `[Fact]`(s) in `QuestBoard.IntegrationTests/Mobile/MobileViewsTests.cs` for the mobile join card's presence/absence/content (D-01/D-02/D-06)
- [ ] Extend `QuestBoard.UnitTests/Repository/PlayerSignupRepositoryTests.cs` with a test seeding a `JoinFinalizedQuest`-shaped signup (`LastVoteChangeTime = null`, fresh `SignupTime`, `IsSelected = false`) into an existing waitlist and asserting correct ordering

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| Mobile "Join This Quest" card renders correctly, is tappable, and the join flow completes end-to-end | D-01/D-02 | This project's standing precedent (Phase 43 `VERIFICATION.md`, `PITFALLS.md`) requires real-device verification for mobile UI/JS changes — devtools emulation has previously missed real bugs on this codebase | Verify on a real device or real-device cloud (not devtools emulation): open a finalized One-Shot quest as a not-yet-signed-up user, confirm card position (after waitlist, before DM/revoke section), tap through all 3 role buttons + character select, confirm resulting signup state. Record device model + OS version + "not devtools emulation" in the resulting SUMMARY.md, per Phase 43 precedent. |

---

## Validation Sign-Off

- [ ] All tasks have `<automated>` verify or Wave 0 dependencies
- [ ] Sampling continuity: no 3 consecutive tasks without automated verify
- [ ] Wave 0 covers all MISSING references
- [ ] No watch-mode flags
- [ ] Feedback latency < 30s
- [ ] `nyquist_compliant: true` set in frontmatter

**Approval:** pending
