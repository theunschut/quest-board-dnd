---
phase: 87
slug: cross-board-deep-link-recovery
# status lifecycle: draft (seeded by plan-phase) → validated (set by validate-phase §6)
# audit-milestone §5.5 distinguishes NOT-VALIDATED (draft) from PARTIAL (validated + nyquist_compliant: false) (#2117)
status: draft
nyquist_compliant: false
wave_0_complete: false
created: 2026-09-21
---

# Phase 87 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.
> Seeded by `/gsd-plan-phase` from `87-RESEARCH.md` § Validation Architecture, then filled in
> against the four plans as written.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit v3.2.2, FluentAssertions v8.10.0, NSubstitute v5.3.0 |
| **Config file** | `QuestBoard.IntegrationTests/xunit.runner.json` (serial execution — the integration suite shares one in-memory database per fixture) |
| **Quick run command** | `dotnet test --filter "FullyQualifiedName~CrossBoard"` |
| **Full suite command** | `dotnet test` |
| **Working directory** | `C:\Repos\quest-board` (repo root). Earlier phases recorded a `cd /mnt/Data/repos/quest-board-dnd &&` prefix; that path does not exist on this machine and must not be copied. |
| **Estimated runtime** | quick ~30-60s; full suite several minutes |
| **Database** | none required — the integration suite uses EF Core InMemory. Do not start, install or tear down SQL Server for these runs. |

### Harness caveat this phase had to solve first

`WebApplicationFactoryBase` replaces `IActiveGroupContext` with a **singleton** `MutableGroupContext`
whose `ActiveGroupId` is a plain settable property. Under that fixture a board switch written into
session is invisible to the 18 query filters, so every end-to-end assertion in this phase would pass
vacuously. `CrossBoardWebApplicationFactory` (87-01 Task 1) re-registers the real session-backed
`ActiveGroupContextService` and is the fixture every behavioural test in this phase uses. It is a
subclass; the shared base is not edited, so no existing test changes behaviour.

---

## Sampling Rate

- **After every task commit:** `dotnet test --filter "FullyQualifiedName~CrossBoard"`
- **After every plan wave:** `dotnet test` (the shared resolver and switcher touch `GroupPickerController`
  and sit beside `GroupSessionMiddleware`, both of which have existing regression coverage that must stay green)
- **Before `/gsd-verify-work`:** full suite green, plus the blocking human checkpoint in 87-04
- **Max feedback latency:** ~60 seconds for the quick command

---

## Per-Task Verification Map

| Task ID | Plan | Wave | Decision | Threat Ref | Secure Behavior | Test Type | Automated Command | File Exists | Status |
|---------|------|------|----------|------------|-----------------|-----------|-------------------|-------------|--------|
| 87-01-01 | 01 | 1 | (harness) | — | N/A — makes every later security assertion non-vacuous | integration | `dotnet build && dotnet test QuestBoard.IntegrationTests --filter FullyQualifiedName~CrossBoardTestHarness` | ❌ W0 (this task creates it) | ⬜ pending |
| 87-01-02 | 01 | 1 | D-01, D-03, D-04, D-06, D-08, D-09, D-10, D-12, D-18, D-19 | T-87-03 / T-87-04 | Membership pinned in the query; switch before authorization; one session writer | unit + integration | `dotnet build && dotnet test QuestBoard.UnitTests --filter FullyQualifiedName~CrossBoardLinkRegistry` then `dotnet test QuestBoard.IntegrationTests --filter FullyQualifiedName~CrossBoardDeepLink` | ❌ W0 (this task creates it) | ⬜ pending |
| 87-01-03 | 01 | 1 | D-02, D-13, D-15, D-19 | T-87-01 / T-87-02 / T-87-07 | Non-navigation and non-idempotent requests leave the board untouched; non-member and nonexistent are one response | integration | `dotnet test QuestBoard.IntegrationTests --filter FullyQualifiedName~CrossBoardDeepLink` | ✅ (87-01-02) | ⬜ pending |
| 87-02-01 | 02 | 2 | D-11 | T-87-03 | The filter bypass is confined to a five-file allowlist in production source | unit (architecture) | `dotnet test QuestBoard.UnitTests --filter FullyQualifiedName~CrossBoardIgnoreQueryFilters` | ❌ W0 (this task creates it) | ⬜ pending |
| 87-02-02 | 02 | 2 | D-10, D-12, D-17 | T-87-08 / T-87-10 | Every projection returns a board id only, membership pinned in the predicate | unit (architecture, as regression) | `dotnet build && dotnet test QuestBoard.UnitTests --filter FullyQualifiedName~CrossBoardIgnoreQueryFilters` | ✅ (87-02-01) | ⬜ pending |
| 87-02-03 | 02 | 2 | D-09, D-16, D-17, D-18 | T-87-09 / T-87-11 | Closed registry at exactly 18 entries; image subresources never move the board | unit + integration | `dotnet build && dotnet test QuestBoard.UnitTests --filter FullyQualifiedName~CrossBoardLinkRegistry` then `dotnet test QuestBoard.IntegrationTests --filter FullyQualifiedName~CrossBoardRouteCoverage` | ❌ W0 (this task creates the coverage file) | ⬜ pending |
| 87-03-01 | 03 | 2 | D-05 | T-87-12 / T-87-14 | Only a plain three-segment site-relative path parses; no second decode | unit | `dotnet build && dotnet test QuestBoard.UnitTests --filter FullyQualifiedName~CrossBoardRouteTarget` | ❌ W0 (this task creates it) | ⬜ pending |
| 87-03-02 | 03 | 2 | D-03, D-05, D-07, D-08, D-12 | T-87-01 / T-87-12 / T-87-13 | Local-URL guard before parsing and before redirecting; SuperAdmin not skipped; picker identical when nothing resolves | integration | `dotnet build && dotnet test QuestBoard.IntegrationTests --filter FullyQualifiedName~CrossBoardPickerSkip` plus `dotnet test QuestBoard.IntegrationTests --filter FullyQualifiedName~GroupPicker` | ❌ W0 (this task creates it) | ⬜ pending |
| 87-03-03 | 03 | 2 | D-20, D-21 | — | Emitted email links and the calendar feed are demonstrably unchanged | integration | `dotnet test QuestBoard.IntegrationTests --filter FullyQualifiedName~CrossBoardPickerSkip` | ✅ (87-03-02) | ⬜ pending |
| 87-04-01 | 04 | 3 | D-12, D-13, D-14, D-15 | T-87-01 | Non-member and nonexistent responses identical per entity family and for a SuperAdmin | integration | `dotnet build && dotnet test QuestBoard.IntegrationTests --filter FullyQualifiedName~CrossBoardOracleParity` | ❌ W0 (this task creates it) | ⬜ pending |
| 87-04-02 | 04 | 3 | D-18 | T-87-04 / T-87-16 | Policy judged against the switched board, in both directions | integration | `dotnet test QuestBoard.IntegrationTests --filter FullyQualifiedName~CrossBoardAuthorizationBoundary` | ❌ W0 (this task creates it) | ⬜ pending |
| 87-04-03 | 04 | 3 | D-01, D-03, D-04, D-20 | T-87-17 | A switch is never silent to the viewer; verified on a real mobile user agent | manual (blocking checkpoint) | — | n/a | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

**Stated failing direction.** Every runnable command above fails on a non-zero exit **or** on a run
summary reporting `Passed: 0` / `total: 0`, which is what a `--filter` that matched no tests looks
like — a silent pass otherwise. Each task's `<verify>` block carries the per-command `<fails_when>`
with the behaviour-specific signal as well.

---

## Wave 0 Requirements

All Wave 0 gaps are created inside the plans that need them; no separate Wave 0 plan exists.

- [ ] `QuestBoard.IntegrationTests/Helpers/CrossBoardWebApplicationFactory.cs` — **the load-bearing one.**
  Without it the session-backed board context is not in play and every behavioural assertion in this
  phase is vacuous. Created first, by 87-01 Task 1, and pinned by its own three facts.
- [ ] `QuestBoard.IntegrationTests/Middleware/CrossBoardTestHarnessTests.cs` — 87-01 Task 1
- [ ] `QuestBoard.IntegrationTests/Middleware/CrossBoardDeepLinkMiddlewareTests.cs` — 87-01 Task 2
- [ ] `QuestBoard.UnitTests/Helpers/CrossBoardLinkRegistryTests.cs` — 87-01 Task 2
- [ ] `QuestBoard.UnitTests/Architecture/CrossBoardIgnoreQueryFiltersSeamTests.cs` — 87-02 Task 1
- [ ] `QuestBoard.IntegrationTests/Middleware/CrossBoardRouteCoverageTests.cs` — 87-02 Task 3
- [ ] `QuestBoard.UnitTests/Helpers/CrossBoardRouteTargetTests.cs` — 87-03 Task 1
- [ ] `QuestBoard.IntegrationTests/Controllers/CrossBoardPickerSkipTests.cs` — 87-03 Task 2
- [ ] `QuestBoard.IntegrationTests/Security/CrossBoardOracleParityTests.cs` — 87-04 Task 1
- [ ] `QuestBoard.IntegrationTests/Security/CrossBoardAuthorizationBoundaryTests.cs` — 87-04 Task 2
- [ ] Framework install: **none.** `dotnet test` already runs the whole stack this phase needs, and
  the phase installs zero packages.

---

## Manual-Only Verifications

| Behavior | Decision | Why Manual | Test Instructions |
|----------|----------|------------|-------------------|
| A signed-out viewer follows an emailed link, logs in, and lands on the page rather than the picker | D-20 | The shared harness makes a test authentication scheme the default authenticate scheme, so a request carrying a real Identity login cookie is not something the suite can follow. The hop before it (the login redirect naming the picker and preserving the return URL) *is* asserted automatically in 87-03 Task 3; the hop after it is not. | 87-04 Task 3, check 2 |
| The banner renders readably on a real mobile layout | D-03 | The mobile views are selected by user-agent string, not by viewport, so a resized desktop window never exercises them. Shipping one layout and not the other is a recorded failure mode in this codebase. | 87-04 Task 3, check 3 — a real phone, or a desktop browser with its user-agent string overridden to a real mobile one |
| The automatic switch actually removes the friction rather than replacing it with surprise | D-01 | Subjective; no test can answer it, and it is the reason the phase exists. | 87-04 Task 3, check 4 |
| Timing parity between the non-member response and the nonexistent-id response | D-13 | A paired HTTP test proves status, body and header parity but cannot speak to timing. The timing argument is structural — one code path, one query shape, no branch — and is recorded as an argument, not measured. | Not scheduled. Recorded in 87-04 `must_haves.prohibitions` as flagged-unverified rather than dismissed. |

---

## Validation Sign-Off

- [ ] All tasks have `<automated>` verify or are the one blocking manual checkpoint
- [ ] Sampling continuity: no 3 consecutive tasks without automated verify (the only manual task is the last)
- [ ] Wave 0 covers all MISSING references
- [ ] No watch-mode flags
- [ ] Feedback latency < 60s for the quick command
- [ ] `nyquist_compliant: true` set in frontmatter

**Approval:** pending
