---
phase: 77
slug: availability-overview-page
status: draft
nyquist_compliant: false
wave_0_complete: false
created: 2026-08-29
---

# Phase 77 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.
> Source: `77-RESEARCH.md` § Validation Architecture.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit v3.2.2 + FluentAssertions 8.10.0 (already configured) |
| **Config file** | `QuestBoard.IntegrationTests/xunit.runner.json` (serial execution: `parallelizeAssembly: false`) |
| **Quick run command** | `dotnet test --filter "FullyQualifiedName~EventsOverview" --no-build` |
| **Full suite command** | `dotnet test` |
| **Estimated runtime** | ~15s quick / full suite per existing project baseline |

---

## Sampling Rate

- **After every task commit:** Run `dotnet test --filter "FullyQualifiedName~EventsOverview" --no-build`
- **After every plan wave:** Run `dotnet test`
- **Before `/gsd-verify-work`:** Full suite must be green
- **Max feedback latency:** 60 seconds

---

## Per-Task Verification Map

> Filled by the planner once PLAN.md task IDs exist. Requirement → test-type
> mapping below is fixed by RESEARCH.md and must be preserved.

| Task ID | Plan | Wave | Requirement | Threat Ref | Secure Behavior | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|------------|-----------------|-----------|-------------------|-------------|--------|
| TBD | TBD | TBD | EVTVIEW-01 | — | Grid/card page shows upcoming events × members with availability | integration | `dotnet test --filter "FullyQualifiedName~EventsOverviewControllerIntegrationTests"` | ❌ W0 | ⬜ pending |
| TBD | TBD | TBD | EVTVIEW-02 | — | Untouched-default cell renders distinctly from a confirmed answer | unit + integration | `dotnet test --filter "FullyQualifiedName~EventOverviewMapping"` | ❌ W0 | ⬜ pending |
| TBD | TBD | TBD | EVTVIEW-03 | — | Per-event count shown (headline + confirmed + maybe) | unit | `dotnet test --filter "FullyQualifiedName~EventOverviewCounts"` | ❌ W0 | ⬜ pending |
| TBD | TBD | TBD | EVTVIEW-04 | T-77-01 (cross-tenant disclosure via aggregating query) | Never shows another board's events/members; no `IgnoreQueryFilters()` | integration | `dotnet test --filter "FullyQualifiedName~EventsOverviewTenantIsolationTests"` | ❌ W0 | ⬜ pending |
| TBD | TBD | TBD | EVTVIEW-01 | T-77-02 (unbounded `take` → resource exhaustion) | `take` query parameter clamped server-side before `.Take()` | unit or integration | `dotnet test --filter "FullyQualifiedName~EventsOverview"` | ❌ W0 | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

- [ ] `QuestBoard.IntegrationTests/Tests/EventsOverviewTenantIsolationTests.cs` — covers EVTVIEW-04, modeled on `EventAvailabilityTenantIsolationTests.cs` (two-group isolation, D-27 mandatory)
- [ ] `QuestBoard.IntegrationTests/Controllers/EventsControllerIntegrationTests.cs` (extend existing file) — covers EVTVIEW-01/02/03 happy-path + SuperAdmin-no-active-group + empty-state
- [ ] `QuestBoard.UnitTests/...` (new file for the aggregation/mapping logic) — covers EVTVIEW-02/03 in isolation from HTTP, using constructed `EventSignup` lists across all 5 cell states and the Yes/confirmed/maybe count math
- [ ] `QuestBoard.IntegrationTests/Controllers/LayoutNavigationTests.cs` (extend existing file) — new cases for the "Availability Overview" nav entry on both layouts; existing 4 Calendar-string cases must stay green (D-22)
- [ ] No framework install needed — xUnit v3 + FluentAssertions already configured project-wide

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| Mobile layout of the overview under a real mobile user agent | EVTVIEW-01 | Mobile views are UA-selected; devtools emulation does not exercise them | Load the overview page with a real mobile UA (or a real device) and confirm the mobile layout locked in `77-UI-SPEC.md` renders |
| Visual distinctness of muted-default vs. confirmed cell to the eye | EVTVIEW-02 | Automated tests assert the CSS class; perceived contrast is a human judgement | Open the page with a mix of answered and untouched signups; confirm the two states read as different at a glance |

---

## Validation Sign-Off

- [ ] All tasks have `<automated>` verify or Wave 0 dependencies
- [ ] Sampling continuity: no 3 consecutive tasks without automated verify
- [ ] Wave 0 covers all MISSING references
- [ ] No watch-mode flags
- [ ] Feedback latency < 60s
- [ ] `nyquist_compliant: true` set in frontmatter

**Approval:** pending
