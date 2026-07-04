---
phase: 44
slug: post-finalization-voting-waitlist-auto-promotion
status: draft
nyquist_compliant: false
wave_0_complete: false
created: 2026-07-04
---

# Phase 44 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit v3 3.2.2 + NSubstitute 5.3.0 + FluentAssertions 8.10.0 (verified via `QuestBoard.UnitTests.csproj`) |
| **Config file** | `QuestBoard.UnitTests/QuestBoard.UnitTests.csproj` (no separate test-runner config file) |
| **Quick run command** | `dotnet test QuestBoard.UnitTests --filter "FullyQualifiedName~PlayerSignup\|FullyQualifiedName~QuestService"` |
| **Full suite command** | `dotnet test` (run from repo root; builds all projects then runs `QuestBoard.UnitTests`) |
| **Estimated runtime** | ~30 seconds |

---

## Sampling Rate

- **After every task commit:** Run `dotnet test QuestBoard.UnitTests --filter "FullyQualifiedName~PlayerSignup|FullyQualifiedName~QuestService"`
- **After every plan wave:** Run `dotnet test` (full suite)
- **Before `/gsd-verify-work`:** Full suite must be green
- **Max feedback latency:** 30 seconds

---

## Per-Task Verification Map

*Task IDs are assigned by the planner (step 8) — this table maps each requirement to its test approach; the planner should align actual task IDs to these rows.*

| Task ID | Plan | Wave | Requirement | Threat Ref | Secure Behavior | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|------------|-----------------|-----------|-------------------|-------------|--------|
| TBD | TBD | TBD | VOTE-01 | T-44-IDOR | Voting Yes on a full quest sets `IsSelected=false` (waitlisted), never throws/rejects | unit | `dotnet test --filter FullyQualifiedName~PlayerSignupRepositoryTests` | ❌ W0 | ⬜ pending |
| TBD | TBD | TBD | VOTE-02 | — | Waitlist ordering method sorts Yes > Maybe > No, then by timestamp | unit | `dotnet test --filter FullyQualifiedName~WaitlistOrdering` | ❌ W0 | ⬜ pending |
| TBD | TBD | TBD | VOTE-03 | — | `LastVoteChangeTime` updates on every vote mutation | unit | `dotnet test --filter FullyQualifiedName~ChangeVoteAsync` | ❌ W0 | ⬜ pending |
| TBD | TBD | TBD | VOTE-04 | T-44-IDOR | Selected player votes No or is revoked → next waitlisted candidate promoted | unit | `dotnet test --filter FullyQualifiedName~QuestServiceTests.PromoteNextWaitlisted` | ❌ W0 | ⬜ pending |
| TBD | TBD | TBD | VOTE-05 | T-44-IDOR | Selected player votes Maybe → `IsSelected` stays true, no promotion call | unit | `dotnet test --filter FullyQualifiedName~ChangeVoteAsync_Maybe` | ❌ W0 | ⬜ pending |
| TBD | TBD | TBD | VOTE-06 | T-44-IDOR | Waitlisted player votes No → record retained, `IsSelected` stays false | unit | `dotnet test --filter FullyQualifiedName~ChangeVoteAsync_WaitlistedVotesNo` | ❌ W0 | ⬜ pending |
| TBD | TBD | TBD | VOTE-07 | T-44-EMAIL-LEAK | Promotion email dispatched to exactly one recipient (the promoted player), never the freeing player | unit | `dotnet test --filter FullyQualifiedName~EnqueueWaitlistPromotedEmail` | ❌ W0 | ⬜ pending |
| TBD | TBD | TBD | Regression | — | `(int)VoteType.Yes` cast persists correctly (bug-fix regression guard for the pre-existing `Vote = 0` bug) | unit | `dotnet test --filter FullyQualifiedName~EntityProfileEnumCastTests` | ✅ exists — extend with a repository-level persisted-value assertion | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

- [ ] `QuestBoard.UnitTests/Repository/PlayerSignupRepositoryTests.cs` (or extend `QuestServiceTests.cs`) — stubs for VOTE-01, VOTE-03, VOTE-05, VOTE-06, and the enum-cast bug-fix regression
- [ ] Extend `QuestBoard.UnitTests/Services/QuestServiceTests.cs` — stubs for VOTE-04, VOTE-07 (promotion orchestration + single-recipient email dispatch assertion)
- [ ] New `WaitlistOrderingTests.cs` (or wherever the centralized ordering logic lands) — stubs for VOTE-02, no existing precedent
- [ ] No framework install needed — `xunit.v3`/`NSubstitute`/`FluentAssertions` already installed

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|--------------------|
| Mobile waitlist + vote-change UI renders and functions correctly | VOTE-01 through VOTE-06 (mobile parity, per CONTEXT.md D-05) | Visual/interaction correctness on `Details.Mobile.cshtml` isn't unit-testable | Load a finalized, full One-Shot quest on a mobile viewport (or real device), confirm waitlist table + Vote Yes/Maybe/No buttons render and behave identically to desktop |
| Promotion email content/rendering | VOTE-07 | Email HTML rendering via Razor/HtmlRenderer is not covered by the unit suite | Trigger a promotion in a dev environment, inspect the rendered email (or check Hangfire dashboard job output) for correct recipient and copy |

---

## Validation Sign-Off

- [ ] All tasks have `<automated>` verify or Wave 0 dependencies
- [ ] Sampling continuity: no 3 consecutive tasks without automated verify
- [ ] Wave 0 covers all MISSING references
- [ ] No watch-mode flags
- [ ] Feedback latency < 30s
- [ ] `nyquist_compliant: true` set in frontmatter

**Approval:** pending
