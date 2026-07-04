---
phase: 44-post-finalization-voting-waitlist-auto-promotion
plan: 01
subsystem: database
tags: [efcore, automapper, xunit, waitlist, voting]

# Dependency graph
requires: []
provides:
  - "LastVoteChangeTime nullable column on PlayerSignups (entity + domain model + migration)"
  - "PlayerSignupRepository.ChangeVoteAsync(vote) — generalized, capacity-safe, fixes the Vote=0 enum-cast bug"
  - "PlayerSignupRepository.GetTopWaitlistedCandidateAsync — vote-priority + timestamp ordered top waitlisted player query"
  - "WaitlistOrdering.OrderWaitlist extension — centralized Yes>Maybe>No + timestamp sort for desktop/mobile parity"
affects: [44-02, 44-03]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "InternalsVisibleTo(QuestBoard.UnitTests) on QuestBoard.Repository, mirroring the existing QuestBoard.Domain pattern, to allow direct repository-level testing"
    - "EF Core InMemory provider in QuestBoard.UnitTests (previously only in QuestBoard.IntegrationTests) for repository unit tests"

key-files:
  created:
    - QuestBoard.Repository/Migrations/20260704220948_AddLastVoteChangeTimeToPlayerSignup.cs
    - QuestBoard.Domain/Extensions/WaitlistOrdering.cs
    - QuestBoard.Repository/Properties/AssemblyInfo.cs
    - QuestBoard.UnitTests/Repository/PlayerSignupRepositoryTests.cs
    - QuestBoard.UnitTests/Extensions/WaitlistOrderingTests.cs
  modified:
    - QuestBoard.Repository/Entities/PlayerSignupEntity.cs
    - QuestBoard.Domain/Models/QuestBoard/PlayerSignup.cs
    - QuestBoard.Repository/PlayerSignupRepository.cs
    - QuestBoard.Domain/Interfaces/IPlayerSignupRepository.cs
    - QuestBoard.Domain/Services/PlayerSignupService.cs
    - QuestBoard.UnitTests/QuestBoard.UnitTests.csproj

key-decisions:
  - "Added a new nullable LastVoteChangeTime column rather than repurposing SignupTime, per RESEARCH.md — preserves 9+ existing 'Signed up: X' display sites"
  - "Added InternalsVisibleTo + EF Core InMemory to QuestBoard.UnitTests so the internal PlayerSignupRepository class could be tested directly at the repository layer rather than only through a public service seam"
  - "Kept PlayerSignupService.ChangeVoteToYesAndSelectAsync as a temporary pass-through shim delegating to the new ChangeVoteAsync(..., VoteType.Yes) — Plan 02 removes this shim entirely"

patterns-established:
  - "Repository queries that need in-memory priority+timestamp sorting materialize with ToListAsync() first, then sort with LINQ-to-Objects — avoids EF provider translation issues for the null-coalescing ThenBy"

requirements-completed: [VOTE-01, VOTE-02, VOTE-03]

# Metrics
duration: 45min
completed: 2026-07-04
status: complete
---

# Phase 44 Plan 01: Data-Model Foundation & Repository/Ordering Primitives Summary

**Generalized ChangeVoteAsync (fixing the pre-existing Vote=0 enum-cast bug), a new GetTopWaitlistedCandidateAsync query, and a centralized WaitlistOrdering extension, backed by a new LastVoteChangeTime column and 12 new unit tests.**

## Performance

- **Duration:** ~45 min
- **Tasks:** 3
- **Files modified:** 11 (6 modified, 5 created)

## Accomplishments
- Added `LastVoteChangeTime` (nullable `DateTime?`) to `PlayerSignupEntity`, `PlayerSignup`, and a new EF Core migration — no backfill, `SignupTime` semantics untouched
- Replaced `ChangeVoteToYesAndSelectAsync` with a generalized `ChangeVoteAsync(playerSignupId, proposedDateId, vote)` that fixes the pre-existing bug where a Yes vote persisted as `0` instead of `2`, stamps the ordering timestamp on every call, and never rejects on capacity
- Added `GetTopWaitlistedCandidateAsync`, returning the highest-priority waitlisted player ordered by vote (Yes>Maybe>No) then `LastVoteChangeTime ?? SignupTime`
- Centralized waitlist sorting into a single `WaitlistOrdering.OrderWaitlist` extension method that both desktop and mobile views will consume in Plan 03, preventing the kind of mobile/desktop drift fixed in Phase 43
- Added 12 new unit tests (8 repository, 4 ordering) covering VOTE-01/02/03/04/05/06 and the enum-cast regression; full suite (150 tests) green

## Task Commits

Each task was committed atomically:

1. **Task 1: Add LastVoteChangeTime column via migration to entity + domain model** - `6f8ee9d` (feat)
2. **Task 2: Generalize ChangeVoteAsync + add GetTopWaitlistedCandidateAsync (fixes VoteType bug)** - `b2f7a09` (feat)
3. **Task 3: Centralize waitlist ordering into a shared WaitlistOrdering extension (VOTE-02)** - `edc01e7` (feat)

_Note: docs metadata commit is applied by the orchestrator after all wave agents complete, per worktree execution mode._

## Files Created/Modified
- `QuestBoard.Repository/Entities/PlayerSignupEntity.cs` - added nullable `LastVoteChangeTime` column property
- `QuestBoard.Domain/Models/QuestBoard/PlayerSignup.cs` - added matching nullable domain property
- `QuestBoard.Repository/Migrations/20260704220948_AddLastVoteChangeTimeToPlayerSignup.cs` - new nullable-column migration (Up/Down), no backfill
- `QuestBoard.Repository/PlayerSignupRepository.cs` - `ChangeVoteToYesAndSelectAsync` replaced by `ChangeVoteAsync`; added `GetTopWaitlistedCandidateAsync`
- `QuestBoard.Domain/Interfaces/IPlayerSignupRepository.cs` - new method signatures, old signature removed
- `QuestBoard.Domain/Services/PlayerSignupService.cs` - `ChangeVoteToYesAndSelectAsync` now delegates to `ChangeVoteAsync(..., VoteType.Yes)` as a temporary shim (removed in Plan 02)
- `QuestBoard.Domain/Extensions/WaitlistOrdering.cs` - new `OrderWaitlist` extension method (pure function)
- `QuestBoard.Repository/Properties/AssemblyInfo.cs` - new `InternalsVisibleTo("QuestBoard.UnitTests")` attribute
- `QuestBoard.UnitTests/QuestBoard.UnitTests.csproj` - added `Microsoft.EntityFrameworkCore.InMemory` package reference
- `QuestBoard.UnitTests/Repository/PlayerSignupRepositoryTests.cs` - 8 new tests
- `QuestBoard.UnitTests/Extensions/WaitlistOrderingTests.cs` - 4 new tests

## Decisions Made
- New nullable `LastVoteChangeTime` column instead of repurposing `SignupTime` (per RESEARCH.md A1 — avoids corrupting 9+ existing "Signed up: X" display sites)
- `GetTopWaitlistedCandidateAsync` materializes candidates via `ToListAsync()` before sorting in-memory with LINQ-to-Objects, avoiding EF provider translation limitations on the `?? ` null-coalescing `ThenBy`
- `PlayerSignupService.ChangeVoteToYesAndSelectAsync` kept as a thin pass-through shim to `ChangeVoteAsync(..., VoteType.Yes)` so the solution keeps compiling until Plan 02 removes the old service method and its `QuestController` caller entirely

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] Added InternalsVisibleTo and EF Core InMemory package to enable repository-level testing**
- **Found during:** Task 2 (writing `PlayerSignupRepositoryTests.cs`)
- **Issue:** `PlayerSignupRepository` is an `internal` class in `QuestBoard.Repository`. The plan's `read_first` list referenced `QuestServiceTests.cs`'s NSubstitute style, but that style substitutes interfaces — it cannot exercise the actual EF Core query/ordering logic inside `ChangeVoteAsync`/`GetTopWaitlistedCandidateAsync`, which is exactly what the acceptance criteria require (e.g., "a test asserts the persisted Vote int equals 2"). Without `InternalsVisibleTo`, `QuestBoard.UnitTests` cannot even reference the internal class, and without an EF Core in-memory provider referenced in that test project, there was no way to construct a real `QuestBoardContext` to exercise the query.
- **Fix:** Added `QuestBoard.Repository/Properties/AssemblyInfo.cs` with `[assembly: InternalsVisibleTo("QuestBoard.UnitTests")]` (exact mirror of the existing `QuestBoard.Domain/Properties/AssemblyInfo.cs` pattern), and added `Microsoft.EntityFrameworkCore.InMemory` (version 10.0.9 — already pinned and in active use in `QuestBoard.IntegrationTests`, not a new/unverified package) to `QuestBoard.UnitTests.csproj`.
- **Files modified:** `QuestBoard.Repository/Properties/AssemblyInfo.cs` (new), `QuestBoard.UnitTests/QuestBoard.UnitTests.csproj`
- **Verification:** `dotnet test ../QuestBoard.UnitTests --filter "FullyQualifiedName~PlayerSignupRepositoryTests"` — 8/8 passed; full suite 150/150 passed
- **Committed in:** `b2f7a09` (part of Task 2 commit)

---

**Total deviations:** 1 auto-fixed (1 blocking)
**Impact on plan:** Necessary to satisfy the plan's own acceptance criteria (repository-level test asserting the actual persisted `Vote` int). No scope creep — both additions mirror existing, already-verified patterns in this codebase (`QuestBoard.Domain`'s `InternalsVisibleTo`, `QuestBoard.IntegrationTests`' EF Core InMemory reference).

## Issues Encountered
- AutoMapper 16.2.0's `MapperConfiguration` constructor requires an `ILoggerFactory` argument (not present in the RESEARCH.md-era API shape) — resolved by passing `NullLoggerFactory.Instance` in the test helper. Minor test-infrastructure detail, not a deviation from plan intent.
- `PlayerSignupEntity → PlayerSignup` AutoMapper profile does not `Ignore()` the `Player`/`Quest` navigation properties (unlike the reverse direction), so `GetTopWaitlistedCandidateAsync` tests needed a minimal seeded `GroupEntity`/`UserEntity`/`QuestEntity` graph to satisfy the domain model's `required Player`/`required Quest` properties during mapping. Handled with a small `SeedQuestAndUserAsync` test helper — no production code impact.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness
- Plan 02 (orchestration) can now call `PlayerSignupRepository.ChangeVoteAsync`/`GetTopWaitlistedCandidateAsync` directly and build `QuestService`-level promotion/email orchestration on top of these tested primitives
- Plan 03 (UI) can call `WaitlistOrdering.OrderWaitlist` from both `Details.cshtml` and `Details.Mobile.cshtml` for guaranteed parity
- `PlayerSignupService.ChangeVoteToYesAndSelectAsync` and its `QuestController.ChangeVoteToYes` caller still exist as a compiling shim — Plan 02/03 must remove them per the plan's `<artifacts_produced>` note
- No blockers

---
*Phase: 44-post-finalization-voting-waitlist-auto-promotion*
*Completed: 2026-07-04*

## Self-Check: PASSED

All 8 created/modified files verified present on disk; all 4 task/summary commit hashes (`6f8ee9d`, `b2f7a09`, `edc01e7`, `2ea4348`) verified present in git log.
