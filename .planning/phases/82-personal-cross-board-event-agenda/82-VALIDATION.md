---
phase: 82
slug: personal-cross-board-event-agenda
status: draft
nyquist_compliant: false
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
> Requirement IDs are the `EVTAGENDA-*` family proposed in `82-RESEARCH.md` § Phase Requirements —
> the planner mints the final IDs into `.planning/REQUIREMENTS.md` (Phase 82 is `Requirements: TBD`
> in ROADMAP.md today) and updates this table to match.

| Requirement | Behavior | Test Type | Automated Command | File Exists |
|-------------|----------|-----------|-------------------|-------------|
| EVTAGENDA-01 / -03 | Cross-board rows carry the full roster; events with no signup row for the viewer still appear (D-01 starts the query from `Events`, not `EventSignups`) | integration | `dotnet test QuestBoard.IntegrationTests/QuestBoard.IntegrationTests.csproj --filter "FullyQualifiedName~AgendaControllerIntegrationTests"` | ❌ Wave 0 — new file |
| EVTAGENDA-02 | Every row names its board; the name resolves from the membership read, not from an `Event.Group` include (no such navigation exists on the domain model) | integration | same class as above | ❌ Wave 0 |
| EVTAGENDA-04 | Board filter applied **before** the take, defaults to all, remembered for the session | integration | same class as above, additional `[Fact]`s | ❌ Wave 0 |
| EVTAGENDA-05 | Nav entry present for every authenticated user, **including when the board type is unresolved** | integration | `dotnet test QuestBoard.IntegrationTests/QuestBoard.IntegrationTests.csproj --filter "FullyQualifiedName~LayoutNavigationTests"` | ✅ extends existing file — but the unresolved-board-type case is new (every current case sets `OneShot` or `Campaign` explicitly) |
| EVTAGENDA-06 | Switch-prompt posts to `GroupPickerController.SelectGroup` with a local `returnUrl`; a row already on the active board skips the prompt | integration | new test class | ❌ Wave 0 |
| EVTAGENDA-07 / -09 | A left board disappears on the next request; the filter cannot widen the set beyond the viewer's memberships | integration | new class generalising `EventAvailabilityTenantIsolationTests.cs`'s seeding helpers to **three** boards | ❌ Wave 0 — D-17 mandates this |
| EVTAGENDA-08 | SuperAdmin is scoped by their own `UserGroups` rows, not `GetAllWithMemberCountAsync` | integration | `[Fact]` using `CreateAuthenticatedSuperAdminClientAsync` with zero seeded memberships | ❌ Wave 0 |
| EVTAGENDA-09 | The four-case tenant isolation test (D-17): non-member board absent; **two joined boards both present and a third absent**; left board gone; filter cannot widen | integration | dedicated isolation test class | ❌ Wave 0 |

*Status column added by the planner alongside task IDs: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

> **Compiled-language note (planner):** this solution will not build if a test references a method that
> does not exist yet, so a literal wave-0-only test pass would red the whole repository and block every
> other plan. Follow Phase 77's resolution: create each test file inside the plan that ships the seam it
> exercises, written before the implementation within that task. Every task still carries a real
> `<automated>` command and no three consecutive tasks lack one.

- [ ] A new agenda controller/integration test class — covers EVTAGENDA-01/-02/-03/-04/-06/-08, happy path plus empty states (no boards, no upcoming events, everything filtered out)
- [ ] A new cross-board tenant isolation test class — covers EVTAGENDA-07/-09, generalising `EventAvailabilityTenantIsolationTests.cs`'s `SeedOtherBoardEventAsync` / `SeedSignupAsync` helpers to a **third** board and a viewer holding memberships in two of three. The harness supports this today via direct `factory.Database.CreateContext()` writes — **no harness change needed**. Must reset `ActiveGroupId` to `1` in `DisposeAsync`; `WebApplicationFactoryBase.TestGroupContext` is a shared singleton `MutableGroupContext` defaulting to 1, so the standard harness is structurally blind to this bug class without it.
- [ ] A "leave a board" test helper — no existing test calls `IGroupService.RemoveMemberAsync` / `GroupRepository.RemoveMemberAsync`. It is reachable via `scope.ServiceProvider.GetRequiredService<IGroupService>()` inside a test, following the DI-scope pattern `EventAvailabilityTenantIsolationTests.Roster_ForGroupOneEvent_ContainsOnlyGroupOneMembers` already uses for `IEventSignupService`.
- [ ] `QuestBoard.IntegrationTests/Controllers/LayoutNavigationTests.cs` (extend existing file) — a case with `MutableGroupContext.BoardType` left `null`/unresolved asserting the agenda entry is still present. The field is nullable (`BoardType?`) so this is directly settable; no current case exercises it. Existing Calendar/Availability-Overview cases must stay green.
- [ ] A unit test for the membership-set intersection (EVTAGENDA-04/-09) — the stored filter selection intersected against the freshly-read membership set, including the empty-membership case. **Do not assume** EF Core's InMemory provider handles `Contains` over an empty `List<int>` identically to SQL Server (open question 2 in `82-RESEARCH.md`); test it explicitly rather than relying on provider behaviour.
- [ ] No framework install needed — xUnit v3 + FluentAssertions already referenced by both test projects.

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| The agenda's mobile layout, including D-06's tap-to-reveal roster | EVTAGENDA-01/-03 | Mobile views are **user-agent selected**, not breakpoint-driven. Devtools viewport emulation never exercises them. | Load the agenda with a real mobile user agent (or a real device) and confirm the card layout renders, the roster expands on tap, and a tap inside the expanded roster does **not** navigate away — Phase 77 shipped that exact bug and fixed it by putting `stopPropagation()` on the collapse container, not only on the toggle. |
| The switch-board prompt reads as a deliberate context change | EVTAGENDA-06 | Whether the prompt makes the consequence legible is a human judgement; tests can only assert the POST and the redirect. | From a row on a non-active board, trigger the control and confirm the prompt names the target board before switching, and that the way back to the agenda is visible after landing on Details. |
| Board identity is distinguishable at a glance across a mixed list | EVTAGENDA-02 | Automated tests assert the board name is present; perceived distinctness of two boards' rows interleaved is visual. | Load the agenda as a member of two boards with interleaved dates and confirm which board a row belongs to is readable without hunting. |

---

## Validation Sign-Off

- [ ] All tasks have `<automated>` verify or Wave 0 dependencies
- [ ] Sampling continuity: no 3 consecutive tasks without automated verify
- [ ] Wave 0 covers all MISSING references
- [ ] No watch-mode flags
- [ ] Feedback latency < 60s (filtered runs; full suite reserved for wave merges)
- [ ] Phase-gate static audit recorded: exactly one `IgnoreQueryFilters` call site, repository-layer only
- [ ] `nyquist_compliant: true` set in frontmatter

**Approval:** pending — planner signs after mapping to 82-NN task ids
