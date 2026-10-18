---
phase: 92
slug: move-a-quest-or-event-to-a-linked-board
# status lifecycle: draft (seeded by plan-phase) → validated (set by validate-phase §6)
# audit-milestone §5.5 distinguishes NOT-VALIDATED (draft) from PARTIAL (validated + nyquist_compliant: false) (#2117)
status: draft
nyquist_compliant: false
wave_0_complete: false
created: 2026-10-18
---

# Phase 92 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution. Derived from
> `92-RESEARCH.md` § Validation Architecture.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xunit.v3 3.2.2 + FluentAssertions 8.10.0 + NSubstitute 5.3.0, .NET 10 |
| **Config file** | `QuestBoard.IntegrationTests/xunit.runner.json` (serial: `parallelizeAssembly: false`, `parallelizeTestCollections: false`); unit tests have none |
| **Quick run command** | `dotnet test QuestBoard.UnitTests --filter "FullyQualifiedName~BoardMove|FullyQualifiedName~Architecture"` |
| **Full suite command** | `dotnet test` (repo root; both test projects) |
| **Estimated runtime** | ~60 seconds quick, several minutes full |

---

## Sampling Rate

- **After every task commit:** Run the quick run command
- **After every plan wave:** Run `dotnet test`
- **Before `/gsd-verify-work`:** Full suite must be green
- **Max feedback latency:** 120 seconds

---

## Per-Task Verification Map

Filled in by the planner and kept current by the executor. Each D-21 proof and the D-20 single-save
proof must map to at least one task.

| Task ID | Plan | Wave | Requirement | Threat Ref | Secure Behavior | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|------------|-----------------|-----------|-------------------|-------------|--------|
| 92-xx-xx | xx | x | D-21.1 | Elevation of privilege | Admin/DM/Player refused on Move GET and POST, nothing written | integration | `dotnet test QuestBoard.IntegrationTests --filter "FullyQualifiedName~BoardMoveAuthorization"` | ❌ W0 | ⬜ pending |
| 92-xx-xx | xx | x | D-21.2 | Tampering | Target outside the set or the source board refused on direct POST; unlinked board shows no control | unit + integration | `--filter "FullyQualifiedName~BoardMoveTarget"` | ❌ W0 | ⬜ pending |
| 92-xx-xx | xx | x | D-21.3 / 4 / 5 | — | Entry on target, gone from source, FeedRevision rose; kept signup intact with CharacterId null; non-member signups and votes gone | unit + integration | `--filter "FullyQualifiedName~BoardMoveApply"` | ❌ W0 | ⬜ pending |
| 92-xx-xx | xx | x | D-21.6 | — | DM not DungeonMaster/Admin on target refused (SuperAdmin DM passes) | unit + integration | `--filter "FullyQualifiedName~BoardMoveDmRule"` | ❌ W0 | ⬜ pending |
| 92-xx-xx | xx | x | D-21.7 | — | Waitlist promotion in memory, parity with `GetTopWaitlistedCandidateAsync`, email enqueued with target board id | unit + integration | `--filter "FullyQualifiedName~BoardMoveWaitlist"` | ❌ W0 | ⬜ pending |
| 92-xx-xx | xx | x | D-21.8 / 10 | — | Series occurrence, played/closed quest, past event refused; one-off/detached and upcoming cancelled events move | unit + integration | `--filter "FullyQualifiedName~BoardMoveEligibility"` | ❌ W0 | ⬜ pending |
| 92-xx-xx | xx | x | D-21.9 | — | Both follow-up links stored as null | unit + integration | `--filter "FullyQualifiedName~BoardMoveChain"` | ❌ W0 | ⬜ pending |
| 92-xx-xx | xx | x | D-21.11 | — | Campaign target adds Yes rows with UpdatedAt null; non-member rows dropped | unit + integration | `--filter "FullyQualifiedName~BoardMoveCampaign"` | ❌ W0 | ⬜ pending |
| 92-xx-xx | xx | x | D-21.12 | Spoofing | Quest move enqueues kept/dropped mails to confirmed addresses only, not the actor; event move enqueues none | unit + integration | `--filter "FullyQualifiedName~BoardMoveMail"` | ❌ W0 | ⬜ pending |
| 92-xx-xx | xx | x | D-20 | Tampering (integrity) | Exactly one SaveChanges; Conflict leaves every row untouched; source guard on one `SaveChangesAsync(` | unit + architecture | `--filter "FullyQualifiedName~BoardMoveRepository"` | ❌ W0 | ⬜ pending |
| 92-xx-xx | xx | x | D-18 / D-19 | Information disclosure | Feed files unchanged; one structured log line with ids and counts, no names or emails | existing + integration | `dotnet test` | partial | ⬜ pending |
| 92-xx-xx | xx | x | UI-SPEC | — | Move view pair renders for desktop and mobile UA; Details controls; QuestMoved renders three audiences; WaitlistPromoted byte-identical without MovedToBoardName | integration | `--filter "FullyQualifiedName~Emails|FullyQualifiedName~BoardMoveViews"` | ❌ W0 | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

- [ ] `QuestBoard.UnitTests/Repository/BoardMoveRepositoryTests.cs` — D-21.3/4/5/9/11, single-save and Conflict proofs
- [ ] `QuestBoard.UnitTests/Services/BoardMoveServiceTests.cs` — plan, DM rule, promotion parity, mail recipients, target rules
- [ ] `QuestBoard.UnitTests/Services/MoveEligibilityTests.cs` — D-21.8/10 incl. board-clock edges
- [ ] `QuestBoard.UnitTests/Architecture/BoardMoveRepositorySingleSaveTests.cs` — exactly one `SaveChangesAsync(`, no banned write shapes
- [ ] `QuestBoard.IntegrationTests/Helpers/MoveWebApplicationFactory.cs` — re-registers the Hangfire quest email dispatcher so enqueues are captured
- [ ] `QuestBoard.IntegrationTests/Tests/BoardMove*IntegrationTests.cs` — HTTP proofs with `DisposeAsync` reset of `ActiveGroupId = 1` and `BoardType = OneShot`
- [ ] `QuestBoard.IntegrationTests/Emails/QuestMovedRenderTests.cs` — three audiences, wall-clock date text, WaitlistPromoted unchanged when the new parameter is null

No framework install needed.

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| Move page at phone width with long board names | UI-SPEC backstop (E2, E5) | Visual layout | At 390px: select and preview heading wrap, confirm button wraps and stays ≥44px, no horizontal scroll |
| Quest Moved email with long title and board name | UI-SPEC backstop (E8) | Visual email render | Open the three Email Preview entries with long names; nothing overlaps the poster's bottom edge |
| Atomicity on real SQL Server | D-20 | InMemory has no transactions or unique indexes | Optional operator spot check against the local SQL Server at localhost:1433 (do not provision it) |

---

## Validation Sign-Off

- [ ] All tasks have `<automated>` verify or Wave 0 dependencies
- [ ] Sampling continuity: no 3 consecutive tasks without automated verify
- [ ] Wave 0 covers all MISSING references
- [ ] No watch-mode flags
- [ ] Feedback latency < 120s
- [ ] `nyquist_compliant: true` set in frontmatter

**Approval:** pending
