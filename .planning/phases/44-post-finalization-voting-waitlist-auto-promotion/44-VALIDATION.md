---
phase: 44
slug: post-finalization-voting-waitlist-auto-promotion
status: planned
nyquist_compliant: true
wave_0_complete: true
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

*Task IDs reflect the actual task order within each PLAN.md as written (task 1/2/3 per plan; Plan 03's task 4 is the human-verify checkpoint).*

| Task ID | Plan | Wave | Requirement | Threat Ref | Secure Behavior | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|------------|-----------------|-----------|-------------------|-------------|--------|
| 44-01-01/02 | 01 | 1 | VOTE-01, VOTE-03 | T-44-VOTE-SMUGGLE | `ChangeVoteAsync` repository primitive persists vote + `LastVoteChangeTime`, never rejects on capacity | unit | `dotnet test --filter FullyQualifiedName~PlayerSignupRepositoryTests` | ✅ created by 44-01 | ⬜ pending |
| 44-01-03 | 01 | 1 | VOTE-02 | — | `WaitlistOrdering.OrderWaitlist` sorts Yes > Maybe > No, then by `LastVoteChangeTime` | unit | `dotnet test --filter FullyQualifiedName~WaitlistOrdering` | ✅ created by 44-01 | ⬜ pending |
| 44-01-01 | 01 | 1 | Regression | — | `(int)VoteType.Yes` cast persists correctly (bug-fix regression guard for the pre-existing `Vote = 0` bug) | unit | `dotnet test --filter FullyQualifiedName~EntityProfileEnumCastTests` | ✅ exists — extended by 44-01 with a repository-level persisted-value assertion | ⬜ pending |
| 44-02-01/02 | 02 | 2 | VOTE-04, VOTE-05 | T-44-IDOR | Selected player votes No/revokes → next waitlisted candidate promoted via shared `PromoteNextWaitlistedPlayerIfSeatFreedAsync`; Maybe keeps seat, no promotion | unit | `dotnet test --filter FullyQualifiedName~QuestServiceTests` | ✅ created by 44-02 | ⬜ pending |
| 44-02-02/03 | 02 | 2 | VOTE-07 | T-44-EMAIL-LEAK | `EnqueueWaitlistPromotedEmail` dispatched to exactly one recipient (the promoted player), never the freeing player | unit | `dotnet test --filter FullyQualifiedName~EnqueueWaitlistPromotedEmail` | ✅ created by 44-02 | ⬜ pending |
| 44-03-01 | 03 | 3 | VOTE-01, VOTE-02, VOTE-04, VOTE-05, VOTE-06, VOTE-07 | T-44-IDOR, T-44-CSRF, T-44-VOTE-SMUGGLE | `ChangeVote(id, vote)` controller action: `Enum.IsDefined` guard, ownership resolution via authenticated user (not client-supplied signup id), `[ValidateAntiForgeryToken]`, no capacity reject | unit/integration | `dotnet test --filter FullyQualifiedName~QuestController` | ✅ created by 44-03 | ⬜ pending |
| 44-03-04 | 03 | 3 | VOTE-01 through VOTE-06 (mobile parity, per CONTEXT.md D-05) | — | Desktop + mobile UI render/behave identically | manual | n/a — human-verify checkpoint | ❌ manual | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

No separate Wave 0 — each plan creates its own tests alongside the code under test (Plan 01 creates `PlayerSignupRepositoryTests`/`WaitlistOrderingTests` and extends `EntityProfileEnumCastTests`; Plan 02 extends `QuestServiceTests`), so Waves 1/2/3 are self-contained rather than depending on a pre-existing scaffolding wave. No framework install needed — `xunit.v3`/`NSubstitute`/`FluentAssertions` already installed.

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|--------------------|
| Mobile waitlist + vote-change UI renders and functions correctly | VOTE-01 through VOTE-06 (mobile parity, per CONTEXT.md D-05) | Visual/interaction correctness on `Details.Mobile.cshtml` isn't unit-testable | Load a finalized, full One-Shot quest on a mobile viewport (or real device), confirm waitlist table + Vote Yes/Maybe/No buttons render and behave identically to desktop |
| Promotion email content/rendering | VOTE-07 | Email HTML rendering via Razor/HtmlRenderer is not covered by the unit suite | Trigger a promotion in a dev environment, inspect the rendered email (or check Hangfire dashboard job output) for correct recipient and copy |

---

## Validation Sign-Off

- [x] All tasks have `<automated>` verify or Wave 0 dependencies
- [x] Sampling continuity: no 3 consecutive tasks without automated verify
- [x] Wave 0 covers all MISSING references (no separate Wave 0 needed — see above)
- [x] No watch-mode flags
- [x] Feedback latency < 30s
- [x] `nyquist_compliant: true` set in frontmatter

**Approval:** approved 2026-07-04 (via gsd-plan-checker VERIFICATION PASSED)
