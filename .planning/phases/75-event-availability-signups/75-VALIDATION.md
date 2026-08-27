---
phase: 75
slug: event-availability-signups
status: draft
nyquist_compliant: false
wave_0_complete: false
created: 2026-08-27
---

# Phase 75 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit v3.2.2 + FluentAssertions v8.10.0 |
| **Config file** | `QuestBoard.IntegrationTests/xunit.runner.json` (serial execution) |
| **Quick run command** | `dotnet test --filter "FullyQualifiedName~Event"` |
| **Full suite command** | `dotnet test` |
| **Estimated runtime** | ~90 seconds (full suite) |

**Provider note:** every test project uses `UseInMemoryDatabase`. `BeginTransactionAsync` throws on that provider, so atomicity is achieved by staging all mutations on one `DbContext` and calling `SaveChangesAsync()` once — never by an explicit transaction.

---

## Sampling Rate

- **After every task commit:** Run `dotnet test --filter "FullyQualifiedName~Event"`
- **After every plan wave:** Run `dotnet test`
- **Before `/gsd-verify-work`:** Full suite must be green
- **Max feedback latency:** 90 seconds

---

## Per-Task Verification Map

> Task IDs are assigned by the planner. This table records the requirement→test mapping the plans must satisfy; the planner fills in Task ID / Plan / Wave when PLAN.md files are written.

| Task ID | Plan | Wave | Requirement | Threat Ref | Secure Behavior | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|------------|-----------------|-----------|-------------------|-------------|--------|
| TBD | TBD | 0 | EVTAVAIL-01 | — | One-Shot: no signup row exists until the player creates one; Yes/Maybe/No all recordable | integration | `dotnet test --filter "FullyQualifiedName~EventsControllerIntegrationTests"` | ❌ W0 | ⬜ pending |
| TBD | TBD | 0 | EVTAVAIL-02 | — | Campaign: every member holds an auto-Yes row from event creation; opt-out flips to No and never deletes | integration | `dotnet test --filter "FullyQualifiedName~EventsControllerIntegrationTests"` | ❌ W0 | ⬜ pending |
| TBD | TBD | 0 | EVTAVAIL-03 | T-75-01 | A player can change only their own answer; a write targeting another user's signup is refused | integration | `dotnet test --filter "FullyQualifiedName~EventsControllerIntegrationTests"` | ❌ W0 | ⬜ pending |
| TBD | TBD | 0 | EVTAVAIL-04 | — | Join backfills every event dated today or later; leave deletes all rows on that board, past and future | integration + unit | `dotnet test --filter "FullyQualifiedName~GroupServiceTests"` | ❌ W0 | ⬜ pending |
| TBD | TBD | 0 | EVTAVAIL-05 | T-75-02 | Cross-board isolation: a player can neither read nor write availability on another board's event | integration | `dotnet test --filter "FullyQualifiedName~EventAvailabilityTenantIsolationTests"` | ❌ W0 | ⬜ pending |
| TBD | TBD | 0 | D-08 (CONTEXT) | T-75-03 | Withdraw on a Campaign board is refused server-side, not merely hidden in markup | integration | `dotnet test --filter "FullyQualifiedName~EventsControllerIntegrationTests"` | ❌ W0 | ⬜ pending |
| TBD | TBD | 0 | D-10 (CONTEXT) | — | Auto-signup passes leave `UpdatedAt` null; every player-initiated write stamps it, including the creating write | unit | `dotnet test --filter "FullyQualifiedName~EventSignupRepositoryTests"` | ❌ W0 | ⬜ pending |
| TBD | TBD | 0 | D-19 (CONTEXT) | — | A failed backfill rolls back the membership add — no half-synced state | unit | `dotnet test --filter "FullyQualifiedName~GroupServiceTests"` | ❌ W0 | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

- [ ] `QuestBoard.IntegrationTests/Tests/EventAvailabilityTenantIsolationTests.cs` — new class covering EVTAVAIL-05, following the `TenantIsolationTests.cs` structural recipe: seed Group 2 via `factory.Database.CreateContext()`, flip `factory.TestGroupContext.ActiveGroupId = 1`, assert **both** read and write are refused, reset to `1` in `DisposeAsync`
- [ ] `QuestBoard.IntegrationTests/Controllers/EventsControllerIntegrationTests.cs` — new class (or extension of whatever Phase 74 added) covering EVTAVAIL-01/02/03 and the D-08 Campaign-withdraw refusal
- [ ] Extend `QuestBoard.UnitTests/Services/GroupServiceTests.cs` — the atomicity (D-19) and board-type-scoping halves of EVTAVAIL-04 with a mocked `IGroupRepository`
- [ ] `QuestBoard.UnitTests/Repository/EventSignupRepositoryTests.cs` — narrow scalar-update behaviour and the D-10 `UpdatedAt` stamping rule

**Harness gotcha:** `MutableGroupContext.BoardType` is a hardcoded settable flag decoupled from any DB row. Setting it does **not** exercise backfill logic that resolves board type from `GroupEntity`. Tests covering Campaign-vs-One-Shot behaviour must seed a real `GroupEntity` with the intended `BoardType`.

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| Roster and three availability buttons render correctly on a real mobile device | EVTAVAIL-01/03 | `Events/Details.cshtml` has no `.Mobile` variant, so one view serves both platforms — devtools emulation has previously masked a live case of mobile markup never being selected | Open an event's details page with a real mobile User-Agent; confirm the buttons and roster are usable and the layout does not break |
| Both confirmation dialogs read correctly | D-24, D-25 (CONTEXT) | Native `confirm()` text is not assertable through the integration harness | Delete an event with signups; remove a member from the Platform group page. Confirm each dialog names what will be lost |

---

## Validation Sign-Off

- [ ] All tasks have `<automated>` verify or Wave 0 dependencies
- [ ] Sampling continuity: no 3 consecutive tasks without automated verify
- [ ] Wave 0 covers all MISSING references
- [ ] No watch-mode flags
- [ ] Feedback latency < 90s
- [ ] `nyquist_compliant: true` set in frontmatter

**Approval:** pending
