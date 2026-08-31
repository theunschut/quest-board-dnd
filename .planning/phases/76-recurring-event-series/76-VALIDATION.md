---
phase: 76
slug: recurring-event-series
status: draft
nyquist_compliant: false
wave_0_complete: false
created: 2026-08-28
---

# Phase 76 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.
> Derived from `76-RESEARCH.md` § Validation Architecture.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit v3 3.2.2 + FluentAssertions 8.10.0 + NSubstitute 5.3.0 + EF Core InMemory 10.0.9 |
| **Config file** | none — convention-based project structure (`Repository/`, `Services/` folders) |
| **Quick run command** | `dotnet test QuestBoard.UnitTests --filter "FullyQualifiedName~EventSeries"` |
| **Full suite command** | `dotnet test` (runs `QuestBoard.UnitTests` and `QuestBoard.IntegrationTests`, per `QuestBoard.slnx`) |
| **Estimated runtime** | quick ~10s · full suite ~60-90s |

---

## Sampling Rate

- **After every task commit:** Run the targeted `dotnet test --filter <area>` for the area just touched
- **After every plan wave:** Run `dotnet test` (full suite — both test projects)
- **Before `/gsd-verify-work`:** Full suite must be green, plus a manual UAT pass covering all three cancelled-state read surfaces and a real mobile User-Agent check on the agenda view (per `76-UI-SPEC.md`)
- **Max feedback latency:** 90 seconds

---

## Per-Task Verification Map

> Task IDs are assigned by the planner. Each row below is a phase requirement that MUST map to at least one task's `<verify><automated>` block.

| Task ID | Plan | Wave | Requirement | Threat Ref | Secure Behavior | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|------------|-----------------|-----------|-------------------|-------------|--------|
| TBD | TBD | TBD | EVTRECUR-01 | — | N/A | unit | `dotnet test --filter EventSeriesDateGeneratorTests` | ❌ W0 | ⬜ pending |
| TBD | TBD | TBD | EVTRECUR-02 | — | N/A | unit + integration | `dotnet test --filter PreviewSeries` | ❌ W0 | ⬜ pending |
| TBD | TBD | TBD | EVTRECUR-03 | — | Job scopes to one group at a time | integration | `dotnet test --filter TopUpAsync` | ❌ W0 | ⬜ pending |
| TBD | TBD | TBD | EVTRECUR-04 | — | Delete rejected on a series occurrence | integration | `dotnet test --filter Cancel` | ❌ W0 | ⬜ pending |
| TBD | TBD | TBD | EVTRECUR-05 | — | N/A | integration | `dotnet test --filter MoveThenRun` | ❌ W0 | ⬜ pending |
| TBD | TBD | TBD | EVTRECUR-06 | — | Edit scope never crosses series boundary | integration | `dotnet test --filter EditScope` | ❌ W0 | ⬜ pending |
| TBD | TBD | TBD | EVTRECUR-07 | — | Retry-safe under global `AutomaticRetryAttribute` | integration | `dotnet test --filter Idempotency` | ❌ W0 | ⬜ pending |
| TBD | TBD | TBD | EVTRECUR-08 | — | N/A | unit | `dotnet test --filter MirroredMask` | ❌ W0 | ⬜ pending |
| TBD | TBD | TBD | cross-cutting | — | Series/occurrences never visible or writable across boards | integration | `dotnet test --filter EventSeriesTenantIsolationTests` | ❌ W0 | ⬜ pending |
| TBD | TBD | TBD | cross-cutting | — | Job iterates real groups; never `IgnoreQueryFilters()` | unit | `dotnet test --filter RecurringOccurrenceTopUpJobTests` | ❌ W0 | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

- [ ] `QuestBoard.UnitTests/Services/EventSeriesDateGeneratorTests.cs` — pure mask/cadence math, mirrored-mask non-collision (EVTRECUR-01, EVTRECUR-08)
- [ ] `QuestBoard.UnitTests/Repository/EventSeriesMaterializationTests.cs` — idempotency: double-run, cancel-then-run, move-then-run, move-outside-runway-then-run (EVTRECUR-05, EVTRECUR-07)
- [ ] `QuestBoard.UnitTests/Services/RecurringOccurrenceTopUpJobTests.cs` — mirrors `DailyReminderJobTests.cs` mocked-scope-factory pattern; asserts per-group `SetGroupId` calls, not a single cross-group call (EVTRECUR-03)
- [ ] `QuestBoard.IntegrationTests/Tests/EventSeriesTenantIsolationTests.cs` — mirrors `EventTenantIsolationTests.cs`: series/occurrence visibility, create/edit/cancel rejection across boards
- [ ] No new test framework install needed — everything builds on the already-configured xUnit v3 + EF Core InMemory stack

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| Live date preview updates as the DM edits cadence/anchor/mask | EVTRECUR-02 | Client-side interaction timing and rendering are not covered by the server-side test stack | Open the series setup screen, change cadence, anchor date, and mask in turn; confirm the next ~10 dates re-render each time and match the saved result after submit |
| Cancelled-state rendering on all three read surfaces | EVTRECUR-04 | Visual/tombstone presentation across calendar, agenda, and detail views | Cancel one occurrence; confirm it renders as cancelled (not absent, not active) on each of the three surfaces |
| Horizon/runway staleness banner is visible to a human | EVTRECUR-03 | The failure mode is silence — needs a human to confirm the signal actually surfaces | Let runway fall below threshold (or simulate); confirm the banner appears where a DM actually looks |
| Agenda view on a real mobile User-Agent | EVTRECUR-06 | Device-gated layout per `76-UI-SPEC.md` (restated Phase 74 D-16) | Load the agenda view with a real mobile UA; confirm the mobile layout gate fires |

---

## Validation Sign-Off

- [ ] All tasks have `<automated>` verify or Wave 0 dependencies
- [ ] Sampling continuity: no 3 consecutive tasks without automated verify
- [ ] Wave 0 covers all MISSING references
- [ ] No watch-mode flags
- [ ] Feedback latency < 90s
- [ ] `nyquist_compliant: true` set in frontmatter

**Approval:** pending
