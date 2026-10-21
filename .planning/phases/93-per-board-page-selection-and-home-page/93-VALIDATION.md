---
phase: "93"
slug: "per-board-page-selection-and-home-page"
# status lifecycle: draft (seeded by plan-phase) → validated (set by validate-phase §6)
# audit-milestone §5.5 distinguishes NOT-VALIDATED (draft) from PARTIAL (validated + nyquist_compliant: false) (#2117)
status: draft
nyquist_compliant: false
wave_0_complete: false
created: "2026-10-21"
---

# Phase 93 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xunit.v3 3.2.2 + FluentAssertions 8.10.0 + NSubstitute 5.3.0, net10.0 |
| **Config file** | `QuestBoard.IntegrationTests/xunit.runner.json` (serial); unit tests: none |
| **Quick run command** | `dotnet test QuestBoard.UnitTests --filter "FullyQualifiedName~BoardPage|FullyQualifiedName~Architecture"` |
| **Full suite command** | `dotnet test` (repo root, both test projects) |
| **Estimated runtime** | ~25 seconds quick slice; full suite several minutes |

---

## Sampling Rate

- **After every task commit:** Run the quick command plus the task's own `--filter` slice
- **After every plan wave:** Run `dotnet test` (both projects)
- **Before `/gsd-verify-work`:** Full suite must be green, plus the static checks below
- **Max feedback latency:** ~60 seconds per task slice

---

## Per-Task Verification Map

Filled in by the planner from the test map in `93-RESEARCH.md` § Validation Architecture. Every
decision D-01…D-21 maps to at least one automated command there.

| Task ID | Plan | Wave | Requirement | Threat Ref | Secure Behavior | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|------------|-----------------|-----------|-------------------|-------------|--------|
| (planner) | | | D-xx | | | | | | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

- [ ] `QuestBoard.UnitTests/Services/BoardPagePolicyTests.cs` — type matrix, Quest Log dependency, effective home chain
- [ ] `QuestBoard.UnitTests/Services/BoardPageSettingsServiceTests.cs` — refusal counts on a fake `IBoardClock`, home-page refusal, sanitising, absent rows
- [ ] `QuestBoard.UnitTests/Services/EntityProfileEnumCastTests.cs` — add the page enum
- [ ] `QuestBoard.IntegrationTests/Helpers/BoardPageSeed.cs` — set page settings on a group row
- [ ] `QuestBoard.IntegrationTests/Controllers/BoardPageGateTests.cs`, `BoardPageCoverageTests.cs` (reflection), `BoardPageNavigationTests.cs` (both user agents), `BoardPageHomeTests.cs`, `BoardPagePlatformTests.cs`, `BoardPageQuestsOffTests.cs`
- [ ] `QuestBoard.IntegrationTests/Tests/BoardPageStaticGuardTests.cs` — no planning references in touched source
- [ ] DI-graph factory + test proving the real resolver resolves with the real `IBoardTypeResolver`
- [ ] Framework install: none

---

## Static Checks (phase gate)

- `git diff --name-only main...HEAD -- QuestBoard.Domain/Services/CalendarFeedWriter.cs QuestBoard.Domain/Services/CalendarSubscriptionService.cs QuestBoard.Repository/FeedRevisionStamper.cs` prints nothing
- `AmbientClockSeamTests` green with the new settings service on the board-clock consumer list
- `IgnoreQueryFilters_IsConfinedToTheAllowlist` green with the allowlist unchanged
- `AntiForgeryTokenCoverageTests` green

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| Migration applies on a real SQL Server and existing boards look unchanged | D-18 | InMemory provider does not run migrations or enforce column defaults | Start the app against `localhost:1433`, confirm the migration applied, open an unconfigured OneShot and Campaign board and compare menus to before |
| Mobile layouts hide switched-off pages and show the Quests item | D-07, D-21 | Mobile views are chosen by a real mobile user agent; devtools emulation does not exercise them | Open a configured board on a phone (or a real mobile UA) and check the menu and Quest Details picker |
| A board set to open on Calendar lands there after login and via the brand link | D-10, D-11 | End-to-end login and board-picker flow on the real host | Set a board's home to Calendar in Platform, log out, log in, pick the board |

---

## Validation Sign-Off

- [ ] All tasks have `<automated>` verify or Wave 0 dependencies
- [ ] Sampling continuity: no 3 consecutive tasks without automated verify
- [ ] Wave 0 covers all MISSING references
- [ ] No watch-mode flags
- [ ] Feedback latency < 60s
- [ ] `nyquist_compliant: true` set in frontmatter

**Approval:** pending
