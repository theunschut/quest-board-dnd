---
phase: 75
slug: event-availability-signups
status: complete
nyquist_compliant: true
wave_0_complete: true
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

| Task ID | Plan | Wave | Requirement | Threat Ref | Secure Behavior | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|------------|-----------------|-----------|-------------------|-------------|--------|
| 75-05-T1 | 75-05 | 3 | EVTAVAIL-01 | — | One-Shot: no signup row exists until the player creates one; Yes/Maybe/No all recordable | integration | `dotnet test --filter "FullyQualifiedName~EventsControllerIntegrationTests"` | ✅ EventsControllerIntegrationTests.cs | ✅ green |
| 75-05-T1 | 75-05 | 3 | EVTAVAIL-02 | — | Campaign: every member holds an auto-Yes row from event creation; opt-out flips to No and never deletes | integration | `dotnet test --filter "FullyQualifiedName~EventsControllerIntegrationTests"` | ✅ EventsControllerIntegrationTests.cs | ✅ green |
| 75-05-T1 | 75-05 | 3 | EVTAVAIL-03 | T-75-01 | A player can change only their own answer; a write targeting another user's signup is refused | integration | `dotnet test --filter "FullyQualifiedName~EventsControllerIntegrationTests"` | ✅ EventsControllerIntegrationTests.cs | ✅ green |
| 75-02-T1, T2 | 75-02 | 1 | EVTAVAIL-04 | — | Join backfills every event dated today or later; leave deletes all rows on that board, past and future | unit | `dotnet test --filter "FullyQualifiedName~GroupRepositoryTests"` | ✅ GroupRepositoryTests.cs | ✅ green |
| 75-05-T2 | 75-05 | 3 | EVTAVAIL-05 | T-75-02 | Cross-board isolation: a player can neither read nor write availability on another board's event | integration | `dotnet test --filter "FullyQualifiedName~EventAvailabilityTenantIsolationTests"` | ✅ EventAvailabilityTenantIsolationTests.cs | ✅ green |
| 75-05-T1 | 75-05 | 3 | D-08 (CONTEXT) | T-75-03 | Withdraw on a Campaign board is refused server-side, not merely hidden in markup | integration | `dotnet test --filter "FullyQualifiedName~EventsControllerIntegrationTests"` | ✅ EventsControllerIntegrationTests.cs | ✅ green |
| 75-01-T3 | 75-01 | 1 | D-10 (CONTEXT) | — | Auto-signup passes leave `UpdatedAt` null; every player-initiated write stamps it, including the creating write | unit | `dotnet test --filter "FullyQualifiedName~EventSignupRepositoryTests"` | ✅ EventSignupRepositoryTests.cs | ✅ green |
| 75-02-T1 | 75-02 | 1 | D-19 (CONTEXT) | — | A failed backfill rolls back the membership add — no half-synced state | unit | `dotnet test --filter "FullyQualifiedName~GroupRepositoryTests"` | ✅ GroupRepositoryTests.cs | ✅ green |

*Status legend: ✅ green · ❌ red · ⚠️ flaky (every row below is resolved; no row is left unmarked)*

**Why the EVTAVAIL-04 and D-19 commands changed from the original `GroupServiceTests` reference:** the atomic backfill-on-join and cleanup-on-leave logic lives entirely inside `GroupRepository.AddMemberAsync`/`RemoveMemberAsync` — staging the membership row and the signup rows on one `DbContext` and committing them in the one pre-existing `SaveChangesAsync`. `GroupServiceTests` only proves the domain service is a pure pass-through to the repository (2 facts, no board or event seeding involved) and cannot exercise the backfill/cleanup behavior at all, because its repository dependency is a substitute. `GroupRepositoryTests` is the class that seeds a real `GroupEntity.BoardType`, real events, and real membership rows, and is the one that actually proves the atomicity and scoping guarantees these two rows describe. This is a correction to the verification map, not evidence of drift: no plan ever intended `GroupServiceTests` to carry this proof, the map simply named the wrong class before the plans that would produce the real one existed.

---

## Wave 0 Requirements

- [x] `QuestBoard.IntegrationTests/Tests/EventAvailabilityTenantIsolationTests.cs` — new class covering EVTAVAIL-05, following the `TenantIsolationTests.cs` structural recipe: seed Group 2 via `factory.Database.CreateContext()`, flip `factory.TestGroupContext.ActiveGroupId = 1`, assert **both** read and write are refused, reset to `1` in `DisposeAsync`
- [x] `QuestBoard.IntegrationTests/Controllers/EventsControllerIntegrationTests.cs` — extended with the availability facts covering EVTAVAIL-01/02/03 and the D-08 Campaign-withdraw refusal
- [x] `QuestBoard.UnitTests/Repository/GroupRepositoryTests.cs` — the atomicity (D-19) and board-type-scoping halves of EVTAVAIL-04, seeded against a real `GroupRepository` and a real `GroupEntity.BoardType` row rather than a mocked `IGroupRepository`
- [x] `QuestBoard.UnitTests/Repository/EventSignupRepositoryTests.cs` — narrow scalar-update behaviour and the D-10 `UpdatedAt` stamping rule
- [x] `QuestBoard.IntegrationTests/Controllers/EventDetailsAvailabilityRenderTests.cs` — conditional-rendering facts for the three answer buttons, the withdraw control's board-type/ownership gating, the roster badges, and the signup-aware delete confirmation

**Harness gotcha:** `MutableGroupContext.BoardType` is a hardcoded settable flag decoupled from any DB row. Setting it does **not** exercise backfill logic that resolves board type from `GroupEntity`. Tests covering Campaign-vs-One-Shot backfill/cleanup behaviour seed a real `GroupEntity` with the intended `BoardType`; tests covering a normal request's own board-type resolution (create-time fan-out, the withdraw guard, the withdraw control's visibility) correctly use the `MutableGroupContext.BoardType` stub instead, because that is exactly what a real request resolves through.

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| Roster and three availability buttons render correctly on a real mobile device | EVTAVAIL-01/03 | `Events/Details.cshtml` has no `.Mobile` variant, so one view serves both platforms — devtools emulation has previously masked a live case of mobile markup never being selected | Open an event's details page with a real mobile User-Agent; confirm the buttons and roster are usable and the layout does not break |
| Both confirmation dialogs read correctly | D-24, D-25 (CONTEXT) | Native `confirm()` text is not assertable through the integration harness | Delete an event with signups; remove a member from the Platform group page. Confirm each dialog names what will be lost |
| Answering a past-dated event is acceptable in practice | PD-01 (CONTEXT) | The automated facts prove the code permits a changed or withdrawn answer on a past-dated event; whether that is the *right* product behavior — letting someone correct the record of who actually showed up, long after the session happened — is a judgment call about what the feature is for, not something a passing test can settle | As a player, open an event dated well in the past and change your answer; as a Dungeon Master, confirm this reads as "correcting the record of a session that happened" rather than as a bug that should have been blocked |

---

## Validation Sign-Off

- [x] All tasks have `<automated>` verify or Wave 0 dependencies
- [x] Sampling continuity: no 3 consecutive tasks without automated verify
- [x] Wave 0 covers all MISSING references
- [x] No watch-mode flags
- [x] Feedback latency < 90s
- [x] `nyquist_compliant: true` set in frontmatter

**Approval:** approved — every command named in the Per-Task Verification Map passes (`dotnet test` exits 0 with 0 failed across the whole solution as of plan 75-05).
