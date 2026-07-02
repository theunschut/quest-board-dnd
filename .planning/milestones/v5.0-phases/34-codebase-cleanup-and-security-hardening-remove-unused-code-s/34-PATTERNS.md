# Phase 34: Codebase Cleanup & Security Hardening - Pattern Map

**Mapped:** 2026-07-01
**Files analyzed:** 35 interfaces (XML doc backfill; 26 Domain + 9 Repository per final plan split) + 30 comment-cleanup files (9 non-test + 21 test files per final plan split) + ~6 net-new test files (Wave 0 gaps, deferred to Phase 34.1/34.2) + ~10 CONCERNS.md code-fix files (deferred to Phase 34.1/34.2)
**Analogs found:** All categories have strong in-repo analogs — this phase modifies/extends existing patterns rather than introducing new ones.

**Special note:** Unlike a typical feature phase, most "files to modify" ARE the analogs (e.g. `IQuestRepository` is both the exemplar for D-07 and a file that itself needs its embedded `(D-08)` tag stripped). This document is organized by **workstream** rather than a flat file list, since CONTEXT.md/RESEARCH.md enumerate ~26 distinct fix items rather than a fixed file manifest.

## File Classification

| New/Modified File | Role | Data Flow | Closest Analog | Match Quality |
|---|---|---|---|---|
| 29 zero-coverage interfaces (`IUserService`, `IEmailService`, `IShopService`, etc.) — plus 6 partial-coverage interfaces below, 35 total, split 26 Domain (34-04) / 9 Repository (34-05) | interface (Domain/Repository) | N/A (doc-only) | `IQuestRepository.GetQuestsForTomorrowAllGroupsAsync` (has doc) | exact — explicit exemplar named in CONTEXT.md D-07 |
| `IQuestRepository.cs`, `IQuestService.cs`, `IActiveGroupContext.cs` (existing partial docs) | interface | N/A (doc-only) | themselves (strip embedded `(D-08)`/`(D-01, D-02)` tags, keep prose) | exact — self-referential cleanup |
| 30 comment-cleanup files (`QuestService.cs`, `Program.cs`, `QuestController.cs`, `AdminController.cs`, `GroupController.cs`, `SessionReminderJob.cs`, `QuestFinalizedEmailJob.cs`, `GroupSessionMiddleware.cs`, `QuestBoardContext.cs`, + 21 test files), split 9 non-test (34-02) / 21 test (34-03) per final plan | mixed (controller/service/middleware/test) | N/A (comment-only) | `TenantIsolationTests.cs` (has both good example of ID-tag comment AND useful XML summary to preserve) | exact — shows both "strip" and "keep" cases side by side |
| `RegisterViewModel.cs` deletion | component (ViewModel) | N/A (delete) | none needed — zero-reference deletion | n/a |
| New test: Hangfire retry behavior | test | event-driven | `QuestBoard.UnitTests/Services/DailyReminderJobTests.cs` | exact — same Hangfire job mocking conventions (NSubstitute + `IServiceScopeFactory`/`IBackgroundJobClient`) |
| New test: Group query filter enforcement (`ActiveGroupId` null/nonexistent) | test | CRUD (query-filter) | `QuestBoard.IntegrationTests/Tests/TenantIsolationTests.cs` | exact — tests the exact same `HasQueryFilter`/`TestGroupContext` mechanism CONTEXT.md's Wave 0 gap asks for |
| New test: Follow-up quest cleanup rollback on update failure | test | request-response (controller) | `QuestBoard.UnitTests/Services/QuestServiceTests.cs` (service-level) or existing `QuestControllerIntegrationTests_Comprehensive.cs` (controller-level) | role-match — mirrors service/controller test structure already in repo |
| New test: Resend 429 retry-backoff | test | request-response (HTTP client) | `QuestBoard.UnitTests/Services/ResendStatsAggregatorTests.cs` (same feature area, pure-function style) + `AdminController.GetResendStatsAsync` (production code being tested) | role-match — same subsystem, no existing HttpClient-mock test to copy 1:1 (see No Analog Found) |
| New test: `ActiveGroupId` null-guard (`InvalidOperationException`) | test | CRUD | `TenantIsolationTests.cs` `GroupFilter_NullGroupIdShowsAllGroups` (shows current null=see-all behavior that must NOT break) | exact — this is the regression-guard test for behavior already covered here |
| `Program.cs` Hangfire retry config (`GlobalJobFilters.Filters.Add(new AutomaticRetryAttribute...)`) | config | event-driven | `Program.cs` lines 216-248 (existing `AddHangfire`/`AddHangfireServer` block) | exact — same file, same `if (!IsEnvironment("Testing"))` guard block |
| `QuestBoardContext.cs` composite index addition | model/migration | CRUD | `QuestBoardContext.cs` existing `.HasIndex(x => new {...})` calls (4 existing, e.g. line ~202) | exact |
| `ShopSeedService.cs` `DateTime.Now` → `UtcNow` fix | service | CRUD (seed) | itself, lines 223-224 | exact — trivial in-place fix |

## Pattern Assignments

### XML Doc Comment Backfill (D-07) — 35 interfaces (29 zero-coverage + 6 partial-coverage; final split: 26 Domain / 9 Repository)

**Analog:** `QuestBoard.Domain/Interfaces/IQuestRepository.cs` lines 38-42 (existing exemplar)

**Current pattern (has embedded ID tag to strip per D-06):**
```csharp
/// <summary>
/// Returns all finalized quests for the given date across ALL groups.
/// Bypasses the group query filter — use only for system-wide sweep operations (DailyReminderJob). (D-08)
/// </summary>
Task<IList<Quest>> GetQuestsForTomorrowAllGroupsAsync(DateTime date, CancellationToken token = default);
```

**Target pattern (D-06 + D-07 applied together):**
```csharp
/// <summary>
/// Returns all finalized quests for the given date across ALL groups.
/// Bypasses the group query filter — use only for system-wide sweep operations (DailyReminderJob).
/// </summary>
Task<IList<Quest>> GetQuestsForTomorrowAllGroupsAsync(DateTime date, CancellationToken token = default);
```

**Second existing exemplar with multi-tag embedded prose** — `IQuestService.cs` lines 32-39 (`CreateFollowUpQuestAsync`), demonstrates stripping MULTIPLE inline `(D-xx)` references from a single multi-line `<summary>` while keeping every substantive sentence:
```csharp
// BEFORE (lines 32-39)
/// <summary>
/// Creates a follow-up quest from a finalized original quest.
/// Copies Title+" - Part 2", Description, ChallengeRating, TotalPlayerCount, DungeonMasterId (D-01, D-02).
/// Clears ProposedDates (D-03). Resets DungeonMasterSession to false (D-04).
/// Bulk-imports IsSelected=true signups from original as SignupRole.Player (D-05, D-06, D-07).
/// Returns the Id of the newly created follow-up quest.
/// </summary>
Task<int> CreateFollowUpQuestAsync(int originalQuestId, CancellationToken token = default);

// AFTER
/// <summary>
/// Creates a follow-up quest from a finalized original quest.
/// Copies Title+" - Part 2", Description, ChallengeRating, TotalPlayerCount, DungeonMasterId.
/// Clears ProposedDates. Resets DungeonMasterSession to false.
/// Bulk-imports IsSelected=true signups from original as SignupRole.Player.
/// Returns the Id of the newly created follow-up quest.
/// </summary>
Task<int> CreateFollowUpQuestAsync(int originalQuestId, CancellationToken token = default);
```

**`IActiveGroupContext.cs` (full file, 11 lines) — has a phase-number reference to strip inside otherwise-good prose:**
```csharp
// BEFORE
/// <summary>
/// Provides the active group ID for the current request or execution context.
/// Null means "see all records" (Phase 28 temporary state; Phase 29 adds IsSuperAdmin).
/// </summary>
public interface IActiveGroupContext
{
    int? ActiveGroupId { get; }
}

// AFTER — phase reference stripped, substantive meaning ("null = see all") kept
/// <summary>
/// Provides the active group ID for the current request or execution context.
/// Null means "see all records".
/// </summary>
public interface IActiveGroupContext
{
    int? ActiveGroupId { get; }
}
```

**Net-new doc example (for one of the 29 zero-coverage interfaces)** — write a `<summary>` per method by reading the method's implementation first (do not guess). Structure to follow (from the exemplar): one-line purpose statement, second line only if there's a non-obvious side effect, gotcha, or scope note (e.g. "bypasses group filter", "requires X precondition"). Do not add `<param>`/`<returns>` tags — the existing codebase convention uses `<summary>`-only XML docs (verified: neither `IQuestRepository` nor `IQuestService` use `<param>`/`<returns>`).

**Full undocumented-interface list (Domain, 20):** `IBaseRepository`, `IBaseService`, `ICharacterRepository`, `ICharacterService`, `IDungeonMasterProfileRepository`, `IDungeonMasterProfileService`, `IEmailRenderService`, `IEmailService`, `IGroupRepository`, `IGroupService`, `IPlayerSignupRepository`, `IPlayerSignupService`, `IReminderLogRepository`, `IShopRepository`, `IShopSeedService`, `IShopService`, `ITradeItemRepository`, `IUserRepository`, `IUserService`, `IUserTransactionRepository` — all at `QuestBoard.Domain/Interfaces/*.cs`.

**Full undocumented-interface list (Repository, 9 — separate namespace from same-named Domain interfaces):** `IBaseRepository`, `ICharacterRepository`, `IPlayerSignupRepository`, `IQuestRepository`, `IReminderLogRepository`, `IShopRepository`, `ITradeItemRepository`, `IUserRepository`, `IUserTransactionRepository` — all at `QuestBoard.Repository/Interfaces/*.cs`. **Caution:** these share short names with Domain interfaces but are distinct types in `QuestBoard.Repository.Interfaces` namespace operating on EF entities, not domain models — verify the correct file/namespace when assigning doc tasks.

---

### Comment Cleanup — ID/Phase-Reference Stripping (D-06/D-08)

**Analog:** `QuestBoard.IntegrationTests/Tests/TenantIsolationTests.cs` — demonstrates the full range of cases in one file (read already, full text above).

**Case 1 — strip inline `//` comment referencing a requirement ID, no substantive content worth keeping:**
```csharp
// BEFORE (line 8, class-level XML doc)
/// <summary>
/// Cross-group tenant isolation tests.
/// Proves that the EF Core HasQueryFilter correctly scopes quests to the active group.
/// References: TENANT-03, D-03, D-05, D-10, D-11.
/// </summary>

// AFTER — trailing "References:" line dropped entirely (no standalone value once IDs removed)
/// <summary>
/// Cross-group tenant isolation tests.
/// Proves that the EF Core HasQueryFilter correctly scopes quests to the active group.
/// </summary>
```

**Case 2 — strip ID tag from inline comment, KEEP the substantive description that follows the colon (D-08):**
```csharp
// BEFORE (line 13-14)
// IAsyncLifetime — reset singleton group context after each test class run so that
// test state does not bleed into subsequently-executed test classes (WR-01 / TENANT-03).

// AFTER — parenthetical ID reference removed, rest of sentence intact
// IAsyncLifetime — reset singleton group context after each test class run so that
// test state does not bleed into subsequently-executed test classes.
```

**Case 3 — multi-line comment block with embedded "D-04/D-05" migration-history explanation — decide per D-08 whether the WHY has standalone value:**
```csharp
// BEFORE (lines 23-29)
/// <summary>
/// A quest seeded with GroupId=2 must NOT appear in the response when the active group is 1.
/// D-04/D-05: the quest board moved from / (now the public landing page, no auth) to
/// /quests (authenticated) — use an authenticated client against /quests so this test
/// still exercises the query-filter behavior rather than trivially passing against a
/// landing page that never shows quest content for any group.
/// </summary>

// AFTER — ID prefix stripped, the WHY (routing history / rationale for using /quests) is
// genuinely useful context per D-08 ("why" context should be preserved) — keep the sentence,
// drop only the "D-04/D-05:" tag prefix
/// <summary>
/// A quest seeded with GroupId=2 must NOT appear in the response when the active group is 1.
/// The quest board moved from / (now the public landing page, no auth) to
/// /quests (authenticated) — use an authenticated client against /quests so this test
/// still exercises the query-filter behavior rather than trivially passing against a
/// landing page that never shows quest content for any group.
/// </summary>
```

**Case 4 — the D-08-preserve example named explicitly in CONTEXT.md** — `QuestBoard.Domain/Services/QuestService.cs` `RemoveAsync()` manual-cleanup-order comment (business-logic explanation, NOT an ID-tag comment) must be left completely untouched; only the file's OTHER ~8 ID-tagged comments (`EMAIL-04`, `D-06` x2, `D-11`, `D-01..D-04`, `D-04`, `D-03`, `D-05..D-07`, `D-06`) are cleanup targets.

**Migration-file exclusion (Pitfall 2 in RESEARCH.md):** `QuestBoard.Repository/Migrations/20260420142117_EnableLockoutForExistingUsers.cs` line 13 (`// SEC-02: ...`) — treat as historical/immutable, exclude from the sweep unless user explicitly confirms otherwise.

---

### Dead Code Removal — `RegisterViewModel`

**Analog:** none needed — zero-reference deletion (`grep -rn "RegisterViewModel"` across all `.cs`/`.cshtml` in all 5 projects returns only the class's own declaration at `QuestBoard.Service/ViewModels/AccountViewModels/RegisterViewModel.cs`).

**Verification command to re-run before deleting (D-05 compliance):**
```bash
grep -rn "RegisterViewModel" --include="*.cs" --include="*.cshtml"
```

---

### Net-New Test: Hangfire Retry Behavior

**Analog:** `QuestBoard.UnitTests/Services/DailyReminderJobTests.cs` (full file, 85 lines, read above)

**Imports/setup pattern to copy:**
```csharp
using QuestBoard.Domain.Interfaces;
using QuestBoard.Domain.Models;
using QuestBoard.Domain.Models.QuestBoard;
using QuestBoard.Service.Jobs;
using Hangfire;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace QuestBoard.UnitTests.Services;

public class <JobName>Tests
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly I<Repository> _repository;
    private readonly IBackgroundJobClient _backgroundJobClient;
    private readonly <JobName> _sut;

    public <JobName>Tests()
    {
        _repository = Substitute.For<I<Repository>>();
        _backgroundJobClient = Substitute.For<IBackgroundJobClient>();

        var serviceProvider = Substitute.For<IServiceProvider>();
        serviceProvider.GetService(typeof(I<Repository>)).Returns(_repository);

        var scope = Substitute.For<IServiceScope>();
        scope.ServiceProvider.Returns(serviceProvider);

        _scopeFactory = Substitute.For<IServiceScopeFactory>();
        _scopeFactory.CreateAsyncScope().Returns(new AsyncServiceScope(scope));

        var logger = Substitute.For<ILogger<<JobName>>>();
        _sut = new <JobName>(_scopeFactory, _backgroundJobClient, logger);
    }
}
```

**Assertion style for retry-count verification (adapt from `Received(2).Create(...)` pattern):**
```csharp
_backgroundJobClient.Received(2).Create(Arg.Any<Hangfire.Common.Job>(), Arg.Any<Hangfire.States.IState>());
// or, for retry-specific tests once GlobalJobFilters.Filters.Add(new AutomaticRetryAttribute{...})
// is added to Program.cs, assert against the attribute's configured Attempts/DelaysInSeconds directly
// (unit-testable without Hangfire storage — reflect on the registered GlobalJobFilters collection,
// or test AutomaticRetryAttribute's OnStateElection behavior in isolation per Hangfire's own test patterns)
```

**Config target for the retry fix itself** — `QuestBoard.Service/Program.cs` lines 216-248 (existing `if (!builder.Environment.IsEnvironment("Testing"))` Hangfire block):
```csharp
// existing block to extend, add once after AddHangfireServer(...) registration:
GlobalJobFilters.Filters.Add(new AutomaticRetryAttribute
{
    Attempts = 5,
    DelaysInSeconds = new[] { 1, 2, 4, 8, 16 }
});
```

---

### Net-New Test: Group Query Filter Enforcement

**Analog:** `QuestBoard.IntegrationTests/Tests/TenantIsolationTests.cs` (full file, read above — this IS the existing group-filter test class; extend it rather than creating a new file unless the planner prefers a separate `GroupQueryFilterEnforcementTests.cs`)

**Key reusable pieces:**
- `factory.TestGroupContext.ActiveGroupId = <value>` — mutate the singleton stub before each Act
- `factory.Database.CreateContext()` — get a raw `QuestBoardContext` bypassing the HTTP pipeline, for direct query-filter assertions
- `IClassFixture<WebApplicationFactoryBase>, IAsyncLifetime` — class shape; `DisposeAsync()` resets `ActiveGroupId = 1` to prevent test-bleed into other classes

**Null/nonexistent-group test cases to add (per Wave 0 gap):**
```csharp
[Fact]
public async Task GroupFilter_NullActiveGroupId_ReturnsAllGroups() { /* mirrors GroupFilter_NullGroupIdShowsAllGroups, lines 109-154 */ }

[Fact]
public async Task GroupFilter_NonexistentGroupId_ReturnsEmptyResult()
{
    // Arrange: seed a quest under GroupId=1
    // Act: factory.TestGroupContext.ActiveGroupId = 999; query via factory.Database.CreateContext()
    // Assert: readCtx.Quests.ToList() is empty — no quest for group 999 exists
}
```

---

### Net-New Test: `ActiveGroupId` Null-Guard (`InvalidOperationException`)

**Analog:** `TenantIsolationTests.cs` `GroupFilter_NullGroupIdShowsAllGroups` (lines 109-154) — this is the existing test proving null-is-intentional-and-valid for SuperAdmin/seeding contexts. The new guard test must be written to NOT contradict this test.

**Critical constraint from RESEARCH.md:** the `InvalidOperationException` guard must fire only for *unexpected* null (a regular authenticated request with no active group selected), not for the SuperAdmin/seeding "null = see all" path this existing test exercises. Structure the new test alongside this one so the distinction is visually obvious to future readers:
```csharp
[Fact]
public async Task ActiveGroupContext_UnexpectedNull_ThrowsInvalidOperationException()
{
    // Arrange: simulate the "regular request, no group selected" path (NOT the SuperAdmin/seeding path)
    // Act + Assert: Assert.Throws<InvalidOperationException>(...) / FluentAssertions .Should().Throw<InvalidOperationException>()
}
```

---

### Net-New Test: Follow-Up Quest Cleanup Rollback

**Analog:** `QuestBoard.UnitTests/Services/QuestServiceTests.cs` (service-level unit test conventions — same project as `DailyReminderJobTests.cs`, same NSubstitute mocking style) — read its header/setup pattern before writing if the planner moves the two-phase logic into `QuestService.CreateFollowUpQuestWithDetailsAsync(...)` as RESEARCH.md recommends. If kept at controller level, use `QuestControllerIntegrationTests_Comprehensive.cs` conventions instead (full `WebApplicationFactoryBase` integration style, matching `TenantIsolationTests.cs`'s `IClassFixture<WebApplicationFactoryBase>` shape).

**Pattern:** mock `UpdateQuestPropertiesWithNotificationsAsync` (or its new service-method equivalent) to throw; assert the orphan follow-up quest created in phase 1 is rolled back/deleted — use NSubstitute's `.Returns(x => throw new Exception())` idiom, consistent with how `DailyReminderJobTests` configures repository substitutes.

---

### Net-New Test: Resend 429 Retry-Backoff

**Analog:** `QuestBoard.UnitTests/Services/ResendStatsAggregatorTests.cs` (same feature area — Resend email stats — full file read above) for xUnit `[Fact]`/FluentAssertions conventions; production code under test is `AdminController.GetResendStatsAsync` (lines 383-440+, `QuestBoard.Service/Controllers/Admin/AdminController.cs`).

**No exact HttpClient-mock analog exists in the repo** (see No Analog Found below) — this is the one Wave 0 gap requiring a net-new mocking pattern: mock `IHttpClientFactory`/`HttpMessageHandler` to return HTTP 429 then 200, assert the retry loop backs off (1s/2s/4s per RESEARCH.md's suggested scheme) and eventually succeeds. Follow `ResendStatsAggregatorTests.cs`'s `[Trait("Category", "EmailStats")]` attribute convention for categorization consistency.

---

### CONCERNS.md Fixes — Trivial In-Place Fixes

**`ShopSeedService.cs` DateTime.Now → UtcNow (lines 223-224):** direct find-replace, no analog needed — `DateTime.Now.AddDays(7)` → `DateTime.UtcNow.AddDays(7)`, `DateTime.Now.AddDays(30)` → `DateTime.UtcNow.AddDays(30)`.

**`QuestBoardContext.cs` composite index addition — analog is the file's own existing pattern (4 existing composite indexes):**
```csharp
// Add inside OnModelCreating, alongside existing modelBuilder.Entity<...>().HasIndex(...) calls:
modelBuilder.Entity<QuestEntity>()
    .HasIndex(q => new { q.IsFinalized, q.FinalizedDate });
```
Then: `dotnet ef migrations add AddQuestFinalizedDateIndex --project ../QuestBoard.Repository` (run from `QuestBoard.Service/`, per CLAUDE.md workflow).

## Shared Patterns

### XML Doc Comment Convention
**Source:** `QuestBoard.Domain/Interfaces/IQuestRepository.cs` lines 38-42, `IQuestService.cs` lines 32-45, `IActiveGroupContext.cs` lines 3-6
**Apply to:** All 35 interfaces — 29 zero-coverage + 6 partial-coverage (D-07); final plan split: 26 Domain (34-04) / 9 Repository (34-05)
**Rule:** `<summary>`-only (no `<param>`/`<returns>` tags observed anywhere in the codebase) — one-line purpose + optional second line for gotchas/side-effects/scope. Strip any `(D-xx)`, `(Phase NN...)`, or `TENANT-xx`-style parenthetical/inline ID references while preserving all substantive prose (D-06 + D-08 applied together).

### Hangfire Job Test Mocking
**Source:** `QuestBoard.UnitTests/Services/DailyReminderJobTests.cs` (full file)
**Apply to:** Any new/modified Hangfire job test (retry behavior test, follow-up cleanup test if job-based)
**Pattern:** `Substitute.For<IServiceScopeFactory>()` wired through a fake `IServiceScope`/`IServiceProvider` chain returning `Substitute.For<IBackgroundJobClient>()`; assert via `.Received(N).Create(Arg.Any<Hangfire.Common.Job>(), Arg.Any<Hangfire.States.IState>())`.

### Tenant-Isolation / Group-Filter Test Fixture
**Source:** `QuestBoard.IntegrationTests/Tests/TenantIsolationTests.cs`
**Apply to:** Group query filter enforcement tests, `ActiveGroupId` null-guard test
**Pattern:** `IClassFixture<WebApplicationFactoryBase>, IAsyncLifetime`; mutate `factory.TestGroupContext.ActiveGroupId` before Act; use `factory.Database.CreateContext()` for direct-context assertions bypassing HTTP; reset `ActiveGroupId = 1` in `DisposeAsync()` to prevent test-order bleed.

### Comment-Cleanup Decision Rule (D-06/D-08)
**Source:** `TenantIsolationTests.cs` (demonstrates all 4 cases above), `QuestService.cs RemoveAsync()` (the explicit D-08 preserve-example named in CONTEXT.md)
**Apply to:** All 30 comment-cleanup files; final plan split: 9 non-test source files (34-02) / 21 test files (34-03)
**Rule:** Strip only the `PREFIX-NN`/`(D-xx)`/`Phase NN` ID-shaped substring; if a colon-delimited description follows the ID on the same line, keep the description verbatim; if the entire comment's only content IS the ID reference (e.g., a bare `References: TENANT-03, D-03...` line), delete the whole line; genuinely explanatory "why"/landmine comments (no ID prefix) are never touched.

## No Analog Found

| File | Role | Data Flow | Reason |
|---|---|---|---|
| Resend 429 retry-backoff test (`HttpMessageHandler`/`IHttpClientFactory` mock) | test | request-response (HTTP retry) | No existing test in the repo mocks `HttpClient`/`IHttpClientFactory` directly for retry-backoff behavior — `ResendStatsAggregatorTests.cs` tests a pure aggregation function, not HTTP call behavior. Planner/executor should reach for a standard `HttpMessageHandler`-substitute pattern (e.g. a test double implementing `SendAsync` returning queued responses) — no in-repo precedent to copy verbatim, this is genuinely net-new test infrastructure. |
| `InvalidOperationException` null-ActiveGroupId guard (production code, not just the test) | service/repository guard clause | CRUD | No existing "throw on unexpected null configuration value" guard pattern was found elsewhere in `QuestBoardContext.cs` or `ActiveGroupContextService` to copy — this is a new defensive-code shape for this codebase. Keep the guard minimal and place it where `ActiveGroupId` is consumed by the query-filter predicate, careful not to fire for the intentional SuperAdmin/seeding null case (see `TenantIsolationTests.cs`). |
| Cross-controller `Forbid()` defense-in-depth checks (Scaling Limits item) | controller | request-response | Existing controllers rely entirely on the EF Core global query filter for tenant scoping — no controller currently has an explicit `if (entity.GroupId != activeGroupContext.ActiveGroupId) return Forbid();` check to use as a copy-paste template. RESEARCH.md flags this as the largest-blast-radius item in the phase; recommend the planner draft one canonical example (e.g. in `QuestController`) first, then replicate. |

## Metadata

**Analog search scope:** `QuestBoard.Domain/Interfaces/`, `QuestBoard.Repository/Interfaces/`, `QuestBoard.UnitTests/Services/`, `QuestBoard.IntegrationTests/Tests/`, `QuestBoard.Service/Program.cs`, `QuestBoard.Service/Controllers/Admin/AdminController.cs`
**Files scanned:** ~15 read directly (2 interfaces read in full, 3 test files read in full, Program.cs Hangfire block, IUserService/IActiveGroupContext headers) + glob inventories of all interface/test directories
**Pattern extraction date:** 2026-07-01
