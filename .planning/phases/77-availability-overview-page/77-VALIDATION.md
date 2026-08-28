---
phase: 77
slug: availability-overview-page
status: planned
nyquist_compliant: true
wave_0_complete: true
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
| 01-T1 | 77-01 | 1 | EVTVIEW-01/02/03 | T-77-02 | Domain vocabulary + `MaxTake` ceiling exist | build | `dotnet build QuestBoard.Domain/QuestBoard.Domain.csproj` | n/a | ⬜ pending |
| 01-T2 | 77-01 | 1 | EVTVIEW-01 | T-77-01 | Bounded aggregate read rides the ambient query filters; no bypass, no manual group predicate | build + static gate | `dotnet build QuestBoard.Repository/QuestBoard.Repository.csproj` + `grep -c 'IgnoreQueryFilters' QuestBoard.Repository/EventRepository.cs` == 0 | n/a | ⬜ pending |
| 01-T3 | 77-01 | 1 | EVTVIEW-02, EVTVIEW-03 | — | Five cell states + three counts derived in memory from the answered marker alone | unit | `dotnet test QuestBoard.UnitTests/QuestBoard.UnitTests.csproj --filter "FullyQualifiedName~EventsOverviewAggregationTests"` | ❌ created by 01-T3 | ⬜ pending |
| 02-T1 | 77-02 | 1 | EVTVIEW-02 | — | Unconfirmed-default chip carries a non-colour signal (dashed border) | static gate | `grep -q 'dashed' QuestBoard.Service/wwwroot/css/events-overview.css` | ❌ created by 02-T1 | ⬜ pending |
| 02-T2 | 77-02 | 1 | EVTVIEW-01 | T-77-05 | Nav entries sit under the unchanged board-type gate with no role condition | build | `dotnet build QuestBoard.Service/QuestBoard.Service.csproj` | n/a | ⬜ pending |
| 02-T3 | 77-02 | 1 | EVTVIEW-01 | T-77-05 | Overview nav entry present for DM and player on both board types and both user agents, absent for anonymous | integration | `dotnet test QuestBoard.IntegrationTests/QuestBoard.IntegrationTests.csproj --filter "FullyQualifiedName~LayoutNavigationTests"` | ✅ extends existing file | ⬜ pending |
| 03-T1 | 77-03 | 2 | EVTVIEW-02, EVTVIEW-03 | — | Counts and cell order survive the domain-to-view-model mapping | unit | `dotnet test QuestBoard.UnitTests/QuestBoard.UnitTests.csproj --filter "FullyQualifiedName~EventsOverviewViewModelMappingTests"` | ❌ created by 03-T1 | ⬜ pending |
| 03-T2 | 77-03 | 2 | EVTVIEW-01, EVTVIEW-02, EVTVIEW-03 | T-77-09 | Five distinct chips, three-figure count block, no write path, Razor-encoded output | build | `dotnet build QuestBoard.Service/QuestBoard.Service.csproj` | n/a | ⬜ pending |
| 03-T3 | 77-03 | 2 | EVTVIEW-01, EVTVIEW-02, EVTVIEW-03 | T-77-02, T-77-03, T-77-08 | `take` clamped server-side; all-members access; no 500 for SuperAdmin with no active group | integration | `dotnet test QuestBoard.IntegrationTests/QuestBoard.IntegrationTests.csproj --filter "FullyQualifiedName~EventsOverviewControllerIntegrationTests"` | ❌ created by 03-T3 | ⬜ pending |
| 04-T1 | 77-04 | 3 | EVTVIEW-04 | T-77-01, T-77-10, T-77-11 | Never shows another board's events, members or counts, including under a widened `take` and with no active board | integration | `dotnet test QuestBoard.IntegrationTests/QuestBoard.IntegrationTests.csproj --filter "FullyQualifiedName~EventsOverviewTenantIsolationTests"` | ❌ created by 04-T1 | ⬜ pending |
| 04-T2 | 77-04 | 3 | EVTVIEW-04 | T-77-12 | No phase-77 production file bypasses the query filters; whole suite green | full suite + static gate | `dotnet test` | n/a | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

> **Compiled-language note (planner):** this solution will not build if a test references a
> method that does not exist yet, so a literal wave-0-only test pass would red the whole
> repository and block every other plan. Each test file below is therefore created inside the
> plan that ships the seam it exercises, written before the implementation within that task
> (see the `<behavior>` blocks in 77-01 T3 and 77-03 T1). Every task still carries a real
> `<automated>` command and no three consecutive tasks lack one.

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

- [x] All tasks have `<automated>` verify or Wave 0 dependencies
- [x] Sampling continuity: no 3 consecutive tasks without automated verify
- [x] Wave 0 covers all MISSING references (each test file is created in the plan that ships its seam — see note above)
- [x] No watch-mode flags
- [x] Feedback latency < 60s (filtered runs; full suite reserved for wave merges)
- [x] `nyquist_compliant: true` set in frontmatter

**Approval:** planner-signed 2026-08-29, mapped to 77-01..77-04 task ids
