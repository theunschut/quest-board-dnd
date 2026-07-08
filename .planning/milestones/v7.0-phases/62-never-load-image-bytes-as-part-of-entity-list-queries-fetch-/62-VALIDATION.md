---
phase: 62
slug: never-load-image-bytes-as-part-of-entity-list-queries-fetch
status: draft
nyquist_compliant: false
wave_0_complete: false
created: 2026-07-07
---

# Phase 62 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit v3 (3.2.2) — `QuestBoard.UnitTests` (unit, EF Core InMemory + NSubstitute + FluentAssertions) and `QuestBoard.IntegrationTests` (integration, `Microsoft.AspNetCore.Mvc.Testing` + EF Core InMemory) |
| **Config file** | `QuestBoard.UnitTests/QuestBoard.UnitTests.csproj`, `QuestBoard.IntegrationTests/QuestBoard.IntegrationTests.csproj` (no `.sln` at repo root — invoke `dotnet test` against each `.csproj` directly) |
| **Quick run command** | `dotnet test QuestBoard.UnitTests/QuestBoard.UnitTests.csproj --filter "FullyQualifiedName~CharacterRepositoryTests\|FullyQualifiedName~ContactRepositoryTests\|FullyQualifiedName~DungeonMasterProfileRepositoryTests\|FullyQualifiedName~CharacterServiceTests\|FullyQualifiedName~ContactServiceTests"` |
| **Full suite command** | `dotnet test QuestBoard.UnitTests/QuestBoard.UnitTests.csproj && dotnet test QuestBoard.IntegrationTests/QuestBoard.IntegrationTests.csproj` |
| **Estimated runtime** | ~30-60 seconds (InMemory provider, no real SQL Server dependency for unit tests) |

**Correction to 62-RESEARCH.md's "Wave 0 Gaps":** the research session's own confidence assessment flagged "no automated test infrastructure was found/verified this session" as an open item. That assumption is wrong — this repo has full xUnit v3 unit + integration test projects, and **existing test files already cover exactly the repositories/services this phase touches**: `QuestBoard.UnitTests/Repository/CharacterRepositoryTests.cs`, `ContactRepositoryTests.cs`, `DungeonMasterProfileRepositoryTests.cs`, `QuestBoard.UnitTests/Services/CharacterServiceTests.cs`, `ContactServiceTests.cs`, `DungeonMasterProfileServiceTests.cs`, plus `QuestBoard.IntegrationTests/Controllers/CharactersControllerIntegrationTests.cs`, `ContactsControllerIntegrationTests.cs`, `DungeonMasterControllerIntegrationTests.cs`. The planner should extend these existing test files with new cases rather than relying on manual UAT alone — this phase does NOT need a Wave 0 test-infrastructure-bootstrap task.

---

## Sampling Rate

- **After every task commit:** Run the quick filtered command above for whichever repository/service was just touched.
- **After every plan wave:** Run the full suite command (both unit and integration test projects).
- **Before `/gsd-verify-work`:** Full suite must be green.
- **Max feedback latency:** ~60 seconds.

---

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Threat Ref | Secure Behavior | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|------------|-----------------|-----------|-------------------|-------------|--------|
| 62-01-* | 01 | 1 | N/A (ad-hoc backlog phase) | — | Six read methods no longer select image byte columns; `HasProfilePicture`/`HasContactImage` correctly reflects image presence | unit | `dotnet test QuestBoard.UnitTests/QuestBoard.UnitTests.csproj --filter "FullyQualifiedName~CharacterRepositoryTests\|FullyQualifiedName~ContactRepositoryTests\|FullyQualifiedName~DungeonMasterProfileRepositoryTests"` | ✅ (existing files, extend with new cases) | ⬜ pending |
| 62-02-* | 02 | 1-2 | N/A | — | `CharacterService.UpdateAsync`/`ContactService.UpdateAsync` no longer wipe the stored original image on a "no new upload" edit (Pitfall 1 fix) | unit | `dotnet test QuestBoard.UnitTests/QuestBoard.UnitTests.csproj --filter "FullyQualifiedName~CharacterServiceTests\|FullyQualifiedName~ContactServiceTests"` | ✅ (existing files, extend) | ⬜ pending |
| 62-03-* | 03 | 2-3 | N/A | — | ViewModels/views/controllers use `HasProfilePicture`/`HasContactImage` bool consistently end-to-end | integration | `dotnet test QuestBoard.IntegrationTests/QuestBoard.IntegrationTests.csproj --filter "FullyQualifiedName~CharactersControllerIntegrationTests\|FullyQualifiedName~ContactsControllerIntegrationTests\|FullyQualifiedName~DungeonMasterControllerIntegrationTests"` | ✅ (existing files, extend) | ⬜ pending |

*Exact Task IDs are assigned by the planner during `/gsd-plan-phase 62` — this table will be reconciled against actual plan/task IDs at that time.*

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

None — existing test infrastructure (xUnit v3, EF Core InMemory, NSubstitute, FluentAssertions) and existing test files for all three repositories/services already cover the surface this phase modifies. The planner extends existing test files with new cases; no new test project or framework install is needed.

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| Editing a Character/Contact without touching the photo preserves the existing photo end-to-end (Pitfall 1, data-loss risk) | N/A (ad-hoc) | Best confirmed against the real running app/DB even though a unit test should also cover the service-layer logic — this is the single highest-risk regression in the phase and warrants a belt-and-suspenders manual check | Edit a character's Description only (no new photo); reload Details/Index; confirm `GetProfilePicture`/`GetCroppedPicture` endpoint still returns the original bytes, not 404/empty |
| DM Edit Profile page still shows "current image" thumbnail for a DM with an existing photo (Pitfall 2) | N/A (ad-hoc) | Cosmetic UI check, cheapest verified visually | Open Edit Profile for a DM with an uploaded photo; confirm the current-image thumbnail still renders |
| List/detail queries no longer select image byte columns at the SQL level | N/A (ad-hoc) | Confirms the actual DB-level goal of the phase, not just C#-level behavior | Use `ToQueryString()` in a scratch test or SQL Profiler/EF logging to confirm no `OriginalImageData`/`CroppedImageData` column appears in the generated SQL for the six modified methods |

---

## Validation Sign-Off

- [ ] All tasks have `<automated>` verify or Wave 0 dependencies
- [ ] Sampling continuity: no 3 consecutive tasks without automated verify
- [ ] Wave 0 covers all MISSING references (N/A — no Wave 0 needed)
- [ ] No watch-mode flags
- [ ] Feedback latency < 60s
- [ ] `nyquist_compliant: true` set in frontmatter

**Approval:** pending
