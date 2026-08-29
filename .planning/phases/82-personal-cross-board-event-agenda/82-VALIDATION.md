---
phase: 82
slug: personal-cross-board-event-agenda
status: draft
nyquist_compliant: true
wave_0_complete: false
created: 2026-08-29
---

# Phase 82 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.
> Source: `82-RESEARCH.md` § Validation Architecture.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit v3.2.2 + FluentAssertions 8.10.0 (already configured — no install needed) |
| **Config file** | `QuestBoard.IntegrationTests/xunit.runner.json` (serial execution: `parallelizeAssembly: false`, `parallelizeTestCollections: false` — tests share one in-memory database per factory) |
| **Quick run command** | `dotnet test --filter "FullyQualifiedName~Agenda" --no-build` |
| **Full suite command** | `dotnet test` |
| **Estimated runtime** | ~15s quick / full suite per existing project baseline |

---

## Sampling Rate

- **After every task commit:** Run `dotnet test --filter "FullyQualifiedName~Agenda" --no-build`
- **After every plan wave:** Run `dotnet test QuestBoard.IntegrationTests` plus `dotnet test QuestBoard.UnitTests`
- **Before `/gsd-verify-work`:** Full suite must be green
- **Max feedback latency:** 60 seconds

### Phase-gate static audit (mandatory, in addition to the suite)

This phase ships the application's **first user-facing read that deliberately bypasses the ambient tenant
filter**. Phase 77's verification used a `grep -c 'IgnoreQueryFilters'` audit to prove the *absence* of a
bypass; this phase inverts it and must prove the bypass is **bounded to exactly one call site**:

- `grep -c 'IgnoreQueryFilters' <new repository file>` **== 1** — the single deliberate D-14 call.
- `grep -c 'IgnoreQueryFilters' QuestBoard.Domain/` **== 0** and same for `QuestBoard.Service/` — the bypass
  never leaks past the repository boundary.

---

## Per-Task Verification Map

> Filled by the planner once PLAN.md task IDs exist. The requirement → test-type
> mapping below is fixed by `82-RESEARCH.md` § Validation Architecture and must be preserved.

| Requirement | Behavior | Task(s) | Test Type | Automated Command | Status |
|-------------|----------|---------|-----------|-------------------|--------|
| EVTAGENDA-01 | Cross-board rows start from events, not signups; an event with no signup row for the viewer still appears | 82-02 T2, 82-02 T3, 82-03 T3 | integration | `dotnet test QuestBoard.IntegrationTests/QuestBoard.IntegrationTests.csproj --filter "FullyQualifiedName~AgendaControllerIntegrationTests"` | ⬜ |
| EVTAGENDA-02 | Every row names its board, resolved from the membership read rather than an event-to-group include | 82-03 T2, 82-03 T3, 82-04 T1 | integration | `dotnet test QuestBoard.IntegrationTests/QuestBoard.IntegrationTests.csproj --filter "FullyQualifiedName~AgendaControllerIntegrationTests"` | ⬜ |
| EVTAGENDA-03 | Own availability plus the full roster on every row, rendered through the shared cell partial | 82-02 T3, 82-03 T3, 82-04 T1 | integration | `dotnet test QuestBoard.IntegrationTests/QuestBoard.IntegrationTests.csproj --filter "FullyQualifiedName~AgendaControllerIntegrationTests"` and `dotnet test QuestBoard.IntegrationTests/QuestBoard.IntegrationTests.csproj --filter "FullyQualifiedName~AgendaMobileRenderTests"` | ⬜ |
| EVTAGENDA-04 | Filter applied before the take, defaults to all, remembered for the session | 82-03 T2, 82-06 T3 | integration | `dotnet test QuestBoard.IntegrationTests/QuestBoard.IntegrationTests.csproj --filter "FullyQualifiedName~Agenda"` | ⬜ |
| EVTAGENDA-05 | Nav entry present for every authenticated user including when the board type is unresolved | 82-05 T3 | integration | `dotnet test QuestBoard.IntegrationTests/QuestBoard.IntegrationTests.csproj --filter "FullyQualifiedName~LayoutNavigationTests"` | ⬜ |
| EVTAGENDA-06 | Switch prompt posts to the existing board-selection action with a local return url; an active-board row skips the prompt | 82-03 T3, 82-04 T1, 82-05 T2 | integration | `dotnet test QuestBoard.IntegrationTests/QuestBoard.IntegrationTests.csproj --filter "FullyQualifiedName~Agenda"` | ⬜ |
| EVTAGENDA-07 | A left board disappears on the next request | 82-06 T2 | integration | `dotnet test QuestBoard.IntegrationTests/QuestBoard.IntegrationTests.csproj --filter "FullyQualifiedName~AgendaTenantIsolationTests"` | ⬜ |
| EVTAGENDA-08 | SuperAdmin scoped by their own memberships, empty state with none | 82-02 T3, 82-03 T3, 82-06 T2 | integration + unit | `dotnet test QuestBoard.IntegrationTests/QuestBoard.IntegrationTests.csproj --filter "FullyQualifiedName~Agenda"` and `dotnet test QuestBoard.UnitTests/QuestBoard.UnitTests.csproj --filter "FullyQualifiedName~CrossBoardAgenda"` | ⬜ |
| EVTAGENDA-09 | The four-case isolation suite: non-member board absent; two joined boards both present and a third absent; left board gone; filter cannot widen | 82-02 T2, 82-02 T3, 82-06 T1, 82-06 T2 | integration | `dotnet test QuestBoard.IntegrationTests/QuestBoard.IntegrationTests.csproj --filter "FullyQualifiedName~AgendaTenantIsolationTests"` | ⬜ |
| EVTAGENDA-10 | The page loads with no active board instead of redirecting to the picker | 82-03 T2, 82-03 T3 | integration | `dotnet test QuestBoard.IntegrationTests/QuestBoard.IntegrationTests.csproj --filter "FullyQualifiedName~AgendaControllerIntegrationTests"` | ⬜ |

*Status column added by the planner alongside task IDs: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

> **Compiled-language note (planner):** this solution will not build if a test references a method that
> does not exist yet, so a literal wave-0-only test pass would red the whole repository and block every
> other plan. Follow Phase 77's resolution: create each test file inside the plan that ships the seam it
> exercises, written before the implementation within that task. Every task still carries a real
> `<automated>` command and no three consecutive tasks lack one.

- [x] A new agenda controller/integration test class — covers EVTAGENDA-01/-02/-03/-04/-06/-08, happy path plus empty states (no boards, no upcoming events, everything filtered out) [82-03 T3 (extended by 82-06 T3)]
- [x] A new cross-board tenant isolation test class — covers EVTAGENDA-07/-09, generalising `EventAvailabilityTenantIsolationTests.cs`'s `SeedOtherBoardEventAsync` / `SeedSignupAsync` helpers to a **third** board and a viewer holding memberships in two of three. The harness supports this today via direct `factory.Database.CreateContext()` writes — **no harness change needed**. Must reset `ActiveGroupId` to `1` in `DisposeAsync`; `WebApplicationFactoryBase.TestGroupContext` is a shared singleton `MutableGroupContext` defaulting to 1, so the standard harness is structurally blind to this bug class without it. [82-06 T1/T2]
- [x] A "leave a board" test helper — no existing test calls `IGroupService.RemoveMemberAsync` / `GroupRepository.RemoveMemberAsync`. It is reachable via `scope.ServiceProvider.GetRequiredService<IGroupService>()` inside a test, following the DI-scope pattern `EventAvailabilityTenantIsolationTests.Roster_ForGroupOneEvent_ContainsOnlyGroupOneMembers` already uses for `IEventSignupService`. [82-06 T2]
- [x] `QuestBoard.IntegrationTests/Controllers/LayoutNavigationTests.cs` (extend existing file) — a case with `MutableGroupContext.BoardType` left `null`/unresolved asserting the agenda entry is still present. The field is nullable (`BoardType?`) so this is directly settable; no current case exercises it. Existing Calendar/Availability-Overview cases must stay green. [82-05 T3]
- [x] A unit test for the membership-set intersection (EVTAGENDA-04/-09) — the stored filter selection intersected against the freshly-read membership set, including the empty-membership case. **Do not assume** EF Core's InMemory provider handles `Contains` over an empty `List<int>` identically to SQL Server (open question 2 in `82-RESEARCH.md`); test it explicitly rather than relying on provider behaviour. [82-02 T3]
- [x] No framework install needed — xUnit v3 + FluentAssertions already referenced by both test projects.

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| The agenda's mobile layout, including D-06's tap-to-reveal roster | EVTAGENDA-01/-03 | Mobile views are **user-agent selected**, not breakpoint-driven. Devtools viewport emulation never exercises them. | Load the agenda with a real mobile user agent (or a real device) and confirm the card layout renders, the roster expands on tap, and a tap inside the expanded roster does **not** navigate away — Phase 77 shipped that exact bug and fixed it by putting `stopPropagation()` on the collapse container, not only on the toggle. |
| The switch-board prompt reads as a deliberate context change | EVTAGENDA-06 | Whether the prompt makes the consequence legible is a human judgement; tests can only assert the POST and the redirect. | From a row on a non-active board, trigger the control and confirm the prompt names the target board before switching, and that the way back to the agenda is visible after landing on Details. |
| Board identity is distinguishable at a glance across a mixed list | EVTAGENDA-02 | Automated tests assert the board name is present; perceived distinctness of two boards' rows interleaved is visual. | Load the agenda as a member of two boards with interleaved dates and confirm which board a row belongs to is readable without hunting. |

---

## Validation Sign-Off

- [x] All tasks have `<automated>` verify or Wave 0 dependencies
- [x] Sampling continuity: no 3 consecutive tasks without automated verify
- [x] Wave 0 covers all MISSING references
- [x] No watch-mode flags
- [x] Feedback latency < 60s (filtered runs; full suite reserved for wave merges)
- [x] Phase-gate static audit recorded: exactly one `IgnoreQueryFilters` call site, repository-layer only
- [x] `nyquist_compliant: true` set in frontmatter

**Approval:** signed by planner — mapped to 82-01 … 82-06 task ids
