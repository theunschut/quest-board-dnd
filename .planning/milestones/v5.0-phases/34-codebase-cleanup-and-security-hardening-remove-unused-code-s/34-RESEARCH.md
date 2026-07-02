# Phase 34: Codebase Cleanup & Security Hardening - Research

**Researched:** 2026-07-01
**Domain:** .NET codebase hygiene (dead code removal, comment cleanup, dependency/security audit) across an ASP.NET Core 10 MVC + EF Core 10 + Hangfire 1.8 solution
**Confidence:** HIGH

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions

- **D-01:** Fix scope = everything in `CONCERNS.md`'s **Tech Debt**, **Known Bugs**, **Security Considerations**, **Performance Bottlenecks**, **Fragile Areas**, **Scaling Limits**, **Dependencies at Risk**, and **Test Coverage Gaps** sections.
- **D-02:** EXCEPT `CONCERNS.md`'s **Missing Critical Features** section (digest batching for session reminders, email unsubscribe/preferences) — these overlap with EMAIL-04/REMIND-02, already explicitly deferred in `PROJECT.md`/`STATE.md` at the v4.0 milestone close. Phase 34 does NOT re-open that decision.
- **D-03:** The planner may split Phase 34 into sub-phases (e.g., `34a`, `34b`) grouped by concern area (e.g., security/bugs first, then performance/architecture) if the full `CONCERNS.md` scope exceeds one phase's context budget for full-fidelity planning. Use `/gsd-phase --insert` if a split is recommended — user has pre-approved this outcome.
- **D-04:** Remove ALL confirmed-unused code, including previously-deferred items explicitly called out in `STATE.md` (e.g., `RegisterViewModel` — noted "unused but harmless" and kept out-of-scope in Phase 30-02). Nothing is "left for later" — this is the dedicated cleanup phase closing v5.0.
- **D-05:** "Confirmed unused" means verified via reference search (no callers found) — not just suspected. Verify via RIP/grep before removing.
- **D-06:** Strip low-value inline comments referencing GSD requirement IDs or phase numbers (e.g. `// EMAIL-04:`, `// Phase 30-04:`, `// D-06`) across the ENTIRE codebase, including code that predates GSD tracking — not just v1.x–v5.0 GSD-era comments.
- **D-07:** Replace with XML doc comments (`/// <summary>`) on **interfaces** (e.g. `IQuestService`, `IUserService`, `IActiveGroupContext`, `IQuestRepository`, etc.). `CONVENTIONS.md` already shows one interface following this pattern (`IQuestRepository.GetQuestsForTomorrowAllGroupsAsync`) — extend the pattern to interfaces that currently lack XML docs.
- **D-08:** Preserve genuinely useful inline comments (business-logic explanations, landmine/gotcha warnings, "why" context) — e.g. the manual-cleanup-order comment in `QuestService.RemoveAsync()`. Only ID/phase-number-referencing comments are targeted for removal.
- **D-09:** Security audit = manual code review against `CONCERNS.md`'s "Security Considerations" section PLUS an automated dependency/vulnerability scan (`dotnet list package --vulnerable`) across all three projects (Service, Domain, Repository).
- **D-10:** Any vulnerable package found by the scan should be upgraded if a non-breaking fix version exists; if a breaking upgrade is required, document it rather than forcing a breaking change mid-cleanup-phase.

### Claude's Discretion

- Exact grouping/ordering of `CONCERNS.md` items into plans/waves (e.g., security-first vs. dependency order).
- Whether XML doc comments are added to ALL public interfaces or only those touched during this phase's other work — planner should aim for full coverage per D-07 but may scope-limit if it would balloon the phase.
- Naming/grouping of any sub-phases if a split is recommended (e.g., "34a: Security & Bugs", "34b: Performance & Architecture").

### Deferred Ideas (OUT OF SCOPE)

- **Digest batching for session reminders** (EMAIL-04/REMIND-02) — stays deferred per D-02; do not implement in Phase 34.
- **Email unsubscribe/preference management** — stays deferred per D-02; do not implement in Phase 34.
- **"Password changed" notification email** — already deferred from Phase 32; not resurfaced in this discussion.

None else — discussion stayed within phase scope.
</user_constraints>

## Summary

This phase has no new technology to learn — it is a full-codebase audit-and-fix pass against an already-mapped set of concerns (`CONCERNS.md`), plus two independent cleanup sweeps (dead code, ID-referencing comments). All findings below were verified directly against the live repository (not inferred), so confidence is HIGH throughout except where explicitly flagged.

**Key verified facts that change the plan's shape:**

1. **Dependency scan is clean.** `dotnet list package --vulnerable --include-transitive` across all 5 projects returns zero vulnerable packages. `dotnet list package --outdated` and `--deprecated` are also clean — every package is already at its latest version. **D-09/D-10 require no remediation action**, only the scan itself needs to be run and its clean result documented as evidence.
2. **`RegisterViewModel` is confirmed dead** — its only match in source code is its own class declaration (`QuestBoard.Service/ViewModels/AccountViewModels/RegisterViewModel.cs`). Zero controller or view references. Safe to delete per D-04/D-05.
3. **One CONCERNS.md item is factually stale and should NOT be "fixed" as written.** The "Nullable Navigation Property in PlayerSignup Causes Null Dereference Risk" bug (Known Bugs section) claims `SessionReminderJob.cs` doesn't guard `quest.DungeonMaster` — but the code already uses `quest.DungeonMaster?.Name ?? string.Empty` (line 110). This is a **null-safe read**, not a dereference risk. The planner should verify-and-close this item rather than write a fix for it (see Common Pitfalls).
4. **120 ID-referencing comment occurrences across 24 files** need cleanup (D-06). 108 of those occurrences (90%) are in test files (`QuestBoard.UnitTests`/`QuestBoard.IntegrationTests`), heavily concentrated in three mobile UI test files. Only 12 occurrences are in actual source code (Domain/Repository/Service, non-test).
5. **35 public interfaces exist across Domain + Repository layers; 29 have zero XML doc coverage, 6 have partial coverage.** This is a much larger surface than the CONTEXT.md's example list (`IQuestService`, `IUserService`, `IActiveGroupContext`, `IQuestRepository`) suggests — full D-07 coverage is a non-trivial, separately-sizeable task.
6. **`CONCERNS.md`'s specific technical suggestion for Hangfire retry (`UseAutoRetry`) is not a real Hangfire API.** The correct mechanism for Hangfire 1.8.23 (confirmed installed version) is `AutomaticRetryAttribute`, either per-job (`[AutomaticRetry(Attempts = 5, DelaysInSeconds = new[] {1,2,4,8,16})]`) or globally via `GlobalJobFilters.Filters.Add(...)` in `Program.cs`. No retry filter currently exists in the codebase at all (default Hangfire behavior — 10 immediate retries — is what CONCERNS.md is describing as a problem).

**Primary recommendation:** Treat this phase as three largely independent workstreams (dead code, comment cleanup, CONCERNS.md fixes) that can be planned/executed as separate waves or sub-phases. Given the total scope (~20 CONCERNS.md items + 120 comment occurrences + XML doc backfill across 29 interfaces + dead code sweep), a D-03 split into at least two sub-phases is recommended — see Scope Sizing below.

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|-------------|----------------|-----------|
| Dead code removal | All tiers (Service/Domain/Repository) | — | `RegisterViewModel` is Service-tier; other candidates may span any layer |
| Comment cleanup (ID-tag removal) | All tiers + Test projects | — | Comments are scattered across Domain, Repository, Service, and both test projects |
| XML doc comment backfill | Domain (interfaces), Repository (interfaces) | — | Public interface surface lives in these two layers per `ARCHITECTURE.md`; Service layer has few public interfaces to document |
| Dependency vulnerability scan | Build/tooling (cross-cutting) | — | `dotnet list package` operates at the solution level, not a specific architectural tier |
| Tech debt fixes (controller size, DateTime.Now, cascade cleanup) | Service (controllers) + Domain (services) | Repository (FK/context config) | Matches where each CONCERNS.md item's files live |
| Known bug fixes (email validation, resend pagination) | Service (Program.cs startup, AdminController) | Domain (EmailService) | Startup validation belongs in `Program.cs`; pagination already exists in `AdminController.GetResendStatsAsync` |
| Security fixes (CSRF, secret logging) | Service (controllers, Program.cs) | Domain (EmailService logging) | Both concern areas' files are Service/Domain |
| Performance fixes (DB index, shop query projection) | Repository (EF queries, migrations) | — | Index creation and query projection are Repository-tier concerns |
| Fragile-area fixes (Hangfire scope helper, query filter docs, enum cast safety) | Service (Jobs/) + Repository (QuestBoardContext, EntityProfile) | Domain | Split across all three tiers depending on item |
| Scaling-limit fixes (assertion on null ActiveGroupId, Forbid() checks) | Repository (QuestBoardContext assertion) | Service (controller-level Forbid() checks) | Matches CONCERNS.md file references |
| Dependencies-at-risk fixes (SMTP fallback docs) | Domain (EmailService) | Service (Program.cs Identity config) | Documentation-only fix per CONCERNS.md's own "Migration plan" |
| Test coverage gap fixes | Test projects (UnitTests, IntegrationTests) | — | New tests only; no production code changes |

## Package Legitimacy Audit

No new external packages are being introduced by this phase — Phase 34 is a cleanup/hardening pass over the existing dependency set, not a feature phase that adds dependencies. The Package Legitimacy Gate does not apply here; instead, the existing dependency set was scanned for vulnerabilities (see below), which is the correct audit mechanism for D-09/D-10.

**Live scan results (2026-07-01, `dotnet list package --vulnerable --include-transitive`):**

| Project | Vulnerable packages found |
|---------|---------------------------|
| QuestBoard.Domain | None |
| QuestBoard.Repository | None |
| QuestBoard.Service | None |
| QuestBoard.UnitTests | None |
| QuestBoard.IntegrationTests | None |

`dotnet list package --outdated` and `dotnet list package --deprecated` were also run across all 5 projects — both returned "no updates"/"no deprecated packages" for every project. **All installed package versions are already current; D-10's upgrade-if-non-breaking clause has no packages to act on.**

**Installed package inventory (verified via .csproj, current as of 2026-07-01):**

| Package | Version | Project |
|---------|---------|---------|
| AutoMapper | 16.1.1 | Domain |
| Microsoft.EntityFrameworkCore(.SqlServer/.Design) | 10.0.9 | Repository |
| Microsoft.AspNetCore.Identity.EntityFrameworkCore | 10.0.9 | Repository |
| Hangfire.AspNetCore / Hangfire.SqlServer | 1.8.23 | Service |
| Microsoft.AspNetCore.Identity.UI | 10.0.9 | Service |
| Microsoft.EntityFrameworkCore.Tools | 10.0.9 | Service |
| Microsoft.Extensions.Caching.SqlServer | 10.0.9 | Service |
| FluentAssertions | 8.10.0 | UnitTests, IntegrationTests |
| Microsoft.NET.Test.Sdk | 18.7.0 | UnitTests, IntegrationTests |
| NSubstitute | 5.3.0 | UnitTests |
| xunit.v3 | 3.2.2 | UnitTests, IntegrationTests |
| xunit.runner.visualstudio | 3.1.5 | UnitTests, IntegrationTests |
| Microsoft.AspNetCore.Mvc.Testing | 10.0.9 | IntegrationTests |
| Microsoft.EntityFrameworkCore.InMemory | 10.0.9 | IntegrationTests |

**Verdict:** All `[VERIFIED: dotnet list package]` OK — clean, no removals, no flags, no upgrades needed. The planner's D-09 task should be: run the scan, capture the clean output as evidence in the plan's summary/verification (a "this vulnerability class is closed" artifact), not a code-change task.

## Dead Code Removal Worklist

### Confirmed dead (verified via grep/reference search, D-05 compliant)

| Candidate | Verification method | Result | Disposition |
|-----------|---------------------|--------|-------------|
| `RegisterViewModel` (`QuestBoard.Service/ViewModels/AccountViewModels/RegisterViewModel.cs`) | `grep -rn "RegisterViewModel" --include="*.cs" --include="*.cshtml"` across all 5 source projects | Only match is the class's own `public class RegisterViewModel` declaration | **REMOVE** — confirmed zero callers/references (D-04 explicitly names this) |

### Not exhaustively swept — planner should budget a broader pass

The additional_context requested a "broader sweep... if feasible within scope." A full unused-private-method / unreferenced-class sweep across ~35 interfaces and their implementations was not performed exhaustively in this research pass (would require per-symbol RIP/grep verification for every public and internal type — out of budget for research, appropriate for an execution-phase task with RIP tooling). Recommend the planner allocate a dedicated task using RIP's `FindReferences`/`FindCallers` (per CLAUDE.md's RIP Lookup Protocol) sweeping:

- All `internal` service implementations in Domain (candidates for unused public methods not on their interface)
- ViewModels in `QuestBoard.Service/ViewModels/` not referenced by any `.cshtml` or controller action
- Any `Migrations/*.Designer.cs`-adjacent helper classes no longer referenced

**Known safe (do NOT remove):** `IQuestRepository.GetQuestsForTomorrowAllGroupsAsync` — has exactly one call site (`DailyReminderJob`) but is intentionally kept as the documented cross-group sweep method (STATE.md, ARCHITECTURE.md anti-patterns section). Single-caller is not evidence of dead code here.

## CONCERNS.md Fix-Approach Validation

Per D-01/D-02, every item below is IN scope except "Missing Critical Features" (digest batching, email unsubscribe — explicitly excluded). File/line references were spot-checked against the live repository; drift and technical corrections are called out per item.

### Tech Debt (4 items — all IN scope)

| Item | File/line accuracy | Technical notes for planner |
|------|---------------------|------------------------------|
| Large Controller Files (`QuestController.cs`, 896 lines) | `[CITED: file read]` Not independently re-measured line-by-line but file exists at stated path; scope described (create/edit/finalize/follow-up) matches actual controller actions found during research (`Create`, `CreateFollowUp` at lines ~849-894, etc.) | Extraction into sub-controllers is a structural refactor — largest single item in this phase. Consider isolating to its own plan/wave given blast radius (routes, tests, views all reference `QuestController`). |
| AdminController Size (424 lines, multi-concern) | `[VERIFIED]` `AdminController.cs` confirmed to contain user mgmt (`Users`, `CreateUser`, `EditUser`, `PromoteToAdmin`, etc.), Resend stats (`EmailStats`, `GetResendStatsAsync` at line 383), and quest ops (`Quests`, `DeleteQuest`) all in one file | Resend stats extraction is the most self-contained sub-task — `GetResendStatsAsync` (lines 383-~440) already has clean pagination logic (see Known Bugs below — it's NOT missing pagination, contra CONCERNS.md's Known Bugs claim) that could move wholesale into a new service/controller. |
| `DateTime.Now` in `ShopSeedService.cs` | `[VERIFIED]` Confirmed exactly 2 occurrences at lines 223-224: `AvailableFrom = DateTime.Now.AddDays(7)` and `AvailableUntil = DateTime.Now.AddDays(30)` | Trivial fix: `DateTime.Now` → `DateTime.UtcNow` at both lines. Zero risk, no test-breaking expected since these are seed-only values. |
| Manual Cleanup on Quest Deletion (NoAction Cascade) | `[VERIFIED]` `QuestService.RemoveAsync()` at lines 87-102 matches description exactly — manually removes `PlayerSignups` before `Quest` due to `NoAction` FK. Comment at line 92 ("Manual cleanup required since Quest->PlayerSignup is NoAction...") is a **D-08 preserve-this-comment example** — it's business-logic explanation, not an ID-reference. | Fix approach (add comment to `QuestBoardContext.cs` FK config, add integration test) is straightforward. The existing comment in `QuestService.cs` should stay; only add the *new* comment to the FK configuration site. |
| Two-Phase Follow-Up Quest Update with Rollback | `[VERIFIED, minor line drift]` `QuestController.CreateFollowUp` rollback logic actually at lines ~849-894 (CONCERNS.md says 854-891 — off by ~5 lines, same logic, negligible drift). Comment `// D-07: player import happens...` (line 852) and `// WR-03: if the update fails...` (line 870) are both D-06 cleanup targets. | Recommend moving the two-phase logic into `QuestService.CreateFollowUpQuestWithDetailsAsync(...)` as CONCERNS.md suggests — this also naturally removes the `WR-03`/`D-07` comments since the logic moves to a new method that can get a fresh XML doc comment instead. |

### Known Bugs (3 items — 2 valid, 1 STALE)

| Item | File/line accuracy | Technical notes for planner |
|------|---------------------|------------------------------|
| Email Settings Not Validated on Startup | `[VERIFIED]` `EmailService.cs` lines 16-20 confirmed: `CreateSmtpClient()` logs a warning and returns `null` if `FromEmail` is empty; caller (`SendAsync`, line 33-38) checks `if (client == null) return;` — confirmed silent no-op, no startup validation exists anywhere in `Program.cs`. | Fix: add a startup check in `Program.cs` (after `ConfigureDatabase()`, before `app.Run()`) — `if (builder.Environment.IsProduction() && string.IsNullOrEmpty(config["Email:FromEmail"])) throw new InvalidOperationException(...)`. Must NOT throw in `Testing` environment (would break the test factory, which does not configure email settings). |
| **Nullable Navigation Property Null Dereference Risk — STALE, DO NOT "FIX" AS DESCRIBED** | `[VERIFIED — CONTRADICTS CONCERNS.md]` `SessionReminderJob.cs` line 110 already reads `quest.DungeonMaster?.Name ?? string.Empty` — this is a **null-conditional operator**, already null-safe. Full file review (all 131 lines) found zero unguarded `quest.DungeonMaster.` (non-conditional) dereferences. | **Planner action: verify-and-close, not fix.** This CONCERNS.md item appears to have been written against an earlier version of the file, or is simply inaccurate. Recommend the plan include a verification task ("confirm `SessionReminderJob.cs` already guards `DungeonMaster` access; close item as already-resolved, no code change") rather than writing new null-check code — a redundant `if (quest?.DungeonMaster == null)` guard duplicated with the existing `?.` would be dead code the moment it's written. |
| Resend API Rate Limiting and Pagination Not Handled | `[VERIFIED — PARTIALLY STALE]` `AdminController.GetResendStatsAsync` (lines 383-440+) **already implements a pagination loop** (`while (hasMore)`, `afterId` cursor, lines 394-424) contrary to CONCERNS.md's claim of "no pagination loop." However, **retry-with-backoff for 429 responses is genuinely missing** — the `if (!response.IsSuccessStatusCode)` branch (line 401) just logs and returns an error, no retry. 5-minute caching is confirmed present (`AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5)`, line 378). | Fix approach should be scoped down to: add exponential-backoff retry (e.g., 3 attempts, 1s/2s/4s) specifically for HTTP 429 responses inside `GetResendStatsAsync`'s request loop. Do NOT implement a pagination loop — it already exists. Planner should correct CONCERNS.md's framing when writing the plan's problem statement. |

### Security Considerations (3 items — all IN scope, mitigations already partially in place)

| Item | File/line accuracy | Technical notes for planner |
|------|---------------------|------------------------------|
| No CSRF Token Validation on Some State-Changing Actions | `[VERIFIED]` Every `[HttpPost]` action in `AdminController.cs` (`PromoteToAdmin`, `DemoteFromAdmin`, `PromoteToDM`, `DemoteToPlayer`, `CreateUser`, `EditUser`, `ResetPassword`, `DeleteUser`, `DeleteQuest`, `SendConfirmationEmail`) carries `[ValidateAntiForgeryToken]`. Confirmed via direct attribute grep — no gaps found in this controller. | This item's "Recommendations" are process/test additions, not code fixes: (1) add a reflection-based test asserting all `[HttpPost]` actions across all controllers carry `[ValidateAntiForgeryToken]`, (2) no current violation to fix. Should be filed under Test Coverage Gaps work, not a security code change. |
| Email Configuration Secrets Potentially Logged | `[VERIFIED]` `EmailService.cs` line 52: `logger.LogError(ex, "Failed to send email with subject {Subject}", subject);` — does not log settings/exception message content beyond the subject; `ex` is passed as the structured exception parameter (framework handles this safely, does not string-interpolate secrets into the message template). Current risk is low but the recommendation to double check is still valid — the exception itself could contain SMTP connection details in `ex.Message` depending on the underlying `SmtpException`. | Low-risk item. If action is taken, ensure `ex` remains the structured logging parameter (not interpolated into the message string) — this is already the case. Mostly a documentation/awareness item; likely no code change required beyond confirming the pattern in a code comment (which should NOT carry a D-xx tag per D-06). |
| Resend API Token in HttpClient Default Headers | `[VERIFIED]` `Program.cs` lines 150-157 create a named `"Resend"` HttpClient with no default Authorization header; `AdminController.cs` line 151 area has the referenced pattern — token is added per-request via `request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey)` (confirmed at `GetResendStatsAsync` line 398). | This is a documentation-only fix per CONCERNS.md's own recommendation — add an explanatory code comment (non-ID-tagged, D-08 compliant) near the `HttpClient` registration in `Program.cs` and/or the per-request header line. |

### Performance Bottlenecks (3 items — all IN scope)

| Item | Technical mechanism (verified) | Notes |
|------|-------------------------------|-------|
| No DB Index on Session Reminder Queries | `[VERIFIED]` EF Core 10.0.9 confirmed installed. Existing `OnModelCreating` pattern in `QuestBoardContext.cs` uses fluent `.HasIndex(x => new { x.PropA, x.PropB })` (verified — 4 existing composite indexes at lines 86, 190, 197, 202). Add: `modelBuilder.Entity<QuestEntity>().HasIndex(q => new { q.IsFinalized, q.FinalizedDate });` inside `OnModelCreating`, then `dotnet ef migrations add AddQuestFinalizedDateIndex --project QuestBoard.Repository` (run from `QuestBoard.Service/` per CLAUDE.md). Migrations auto-apply on startup (`context.Database.Migrate()`) — no manual `database update` step needed in dev per CLAUDE.md. | `dotnet ef` CLI confirmed installed at version 9.0.6 (works cross-version with EF Core 10 packages — no action needed). |
| Shop Item Queries Not Optimized (BLOB images) | `[CITED: file paths from CONCERNS.md]` Not independently re-verified in this pass (out of budget) — file `QuestBoard.Repository/ShopRepository.cs` exists per earlier project structure confirmation. | Planner should verify current `.Include()` usage in `GetPublishedItemsAsync` before deciding fix scope — CONCERNS.md's own suggestion is "if <100ms, no action needed," meaning this may resolve to a no-op/documentation item after benchmarking, similar to the stale null-dereference bug above. Recommend a quick benchmark task before committing to a projection refactor. |
| Hangfire Job Queue Not Filtered by Group | `[CITED]` Architectural/scaling concern, not an immediate bug — CONCERNS.md's own language ("if deployed to multi-tenant customers with many groups") indicates this is a future-scaling note, not a current defect. Current single/few-group deployment (per STATE.md: 17 members, one group historically) means this has zero current impact. | Recommend documenting as a known scaling limit (comment + note in ARCHITECTURE.md or CONCERNS.md itself) rather than implementing batching now — implementing batching without a concrete need risks over-engineering for a phase explicitly scoped as "cleanup," not "new feature work." Flag for planner discretion. |

### Fragile Areas (4 items — all IN scope)

| Item | Technical mechanism (verified) | Notes |
|------|-------------------------------|-------|
| Hangfire Job Execution Context (manual scope mgmt) | `[VERIFIED]` Confirmed pattern: every job (`SessionReminderJob.cs` line 23, and others) uses `await using var scope = scopeFactory.CreateAsyncScope();`. CONCERNS.md's suggested `HangfireJobHelper.RunInScopeAsync(...)` static wrapper is a reasonable DRY refactor. | Since ALL current jobs already correctly use the pattern (verified in `SessionReminderJob.cs`), this is a defensive/DRY refactor for future jobs, not a bug fix. Low risk, moderate value. |
| Query Filter Application Inconsistent Across Entity Types | `[VERIFIED]` Confirmed: `QuestBoardContext.cs` line 233 area has `// TENANT-03: Global query filters for group isolation` (itself a D-06 cleanup target) applying filters to `QuestEntity`/`ShopItemEntity` only. `UserEntity` intentionally excluded (matches `ARCHITECTURE.md`'s documented "Known Landmine" — do NOT add a filter to `UserEntity`, would break Identity). | Fix approach = comments only (explain why filtered/unfiltered per entity), NOT a filter-behavior change. Must NOT reintroduce a `UserEntity` filter — `ARCHITECTURE.md` and CONTEXT.md both explicitly flag this as intentional, not a bug. |
| Manual Enum Casting at AutoMapper Boundaries | `[VERIFIED]` `EntityProfile.cs` confirmed — casts at lines 47, 50, 61, 64, 70-77, 82-85, 89-90, 99-100, 107, 125, 129 (line numbers match CONCERNS.md closely, ~2-3 line drift due to file growth). Pattern: `(int)src.Type` / `(ItemType)src.Type` etc. across `SignupRole`, `VoteType`, `ItemType`, `ItemRarity`, `TransactionType`, `CharacterStatus`, `CharacterRole`, `GroupRole`. | Fix approach (CONVENTIONS.md note + validation test) is low-risk documentation/test-only. Do NOT switch to `Enum.Parse` — CONCERNS.md itself flags this as "may not be necessary," and changing 8 cast sites mid-cleanup risks introducing behavior change against the "no user-facing functionality may be removed or broken" constraint in CLAUDE.md. |
| Missing GroupId on Historical Hangfire Job Data | `[VERIFIED — MOSTLY ALREADY DONE]` CONCERNS.md's own text admits: "this is already done (line 36 shows `quest.GroupId` is passed)." | This item is nearly self-resolving per CONCERNS.md's own note. Remaining action is documentation only (note that Hangfire dashboard shows GroupId in job args) — no code change expected. |

### Scaling Limits (3 items — all IN scope, mostly documentation/assertion additions)

| Item | Technical mechanism | Notes |
|------|---------------------|-------|
| EF Core Global Query Filter Scoping at Runtime | Add `InvalidOperationException` guard suggested by CONCERNS.md itself — converts silent null-scope queries into explicit errors. | Straightforward defensive-code addition in `QuestBoardContext` or `ActiveGroupContextService`. Verify this doesn't break the SuperAdmin `null = see all` intentional behavior (`ARCHITECTURE.md`) — the guard must NOT fire when `ActiveGroupId` is null-by-design for SuperAdmin, only when it's null-and-unexpected for a regular request. Needs careful scoping to avoid breaking TENANT-01/TENANT-02 requirements from v5.0. |
| Hangfire Job Retry Limit on Transient Failures | `[VERIFIED]` No `AutomaticRetryAttribute` or `GlobalJobFilters.Filters.Add(...)` currently configured anywhere in `Program.cs` — confirmed via grep. Hangfire 1.8.23 default behavior applies (10 immediate retries via the built-in default global filter, no explicit backoff). `[CITED: api.hangfire.io/html/T_Hangfire_AutomaticRetryAttribute.htm]` The correct fix mechanism is `GlobalJobFilters.Filters.Add(new AutomaticRetryAttribute { Attempts = 5, DelaysInSeconds = new[] { 1, 2, 4, 8, 16 } });` added once near the `AddHangfire(...)` config block in `Program.cs` (around line 223). **Note: CONCERNS.md's suggested syntax `UseAutoRetry(5)` is NOT a real Hangfire API — do not implement as literally written.** | Add the `GlobalJobFilters.Filters.Add(...)` call once, non-breaking, in `Program.cs` inside the `if (!builder.Environment.IsEnvironment("Testing"))` block alongside the existing `AddHangfire` registration. Monitoring/alerting suggestion (Failed Jobs queue >10 items) is out of scope for a code-only cleanup phase — recommend documenting as a manual admin process instead (matches CONCERNS.md's own fallback suggestion). |
| No Tenant Isolation Enforcement at API Boundary | Architectural note — Global Query Filter is correctly described as "a safety net, not a primary security boundary." | CONCERNS.md's own suggested fix (`if (quest.GroupId != activeGroupContext.ActiveGroupId) return Forbid();` per controller action) is a genuinely valuable defense-in-depth addition, but touches EVERY controller action that loads a GroupId-scoped entity — this is one of the larger-footprint items in the whole phase. Recommend isolating to its own task/plan given the number of touch points (QuestController, ShopController, ShopManagementController, and any others). |

### Dependencies at Risk (2 items — documentation-only fixes)

| Item | Technical mechanism | Notes |
|------|---------------------|-------|
| SMTP/Identity Coupling | `[CITED]` CONCERNS.md's own fix approach is "document current flow" — no code change implied beyond verifying the claim. | Verify (already partially confirmed): Identity's built-in email sender is NOT used; all identity emails route through Hangfire jobs calling `IEmailService`. This matches the `EmailService.cs`/`SendAsync` pattern found during this research. Fix = a code comment + confirm via test that no `IEmailSender` (ASP.NET Identity's interface) is registered as Identity's default. |
| Resend SMTP Relay Single Point of Failure | `[CITED]` CONCERNS.md's own fix approaches are optional ("Add secondary SMTP relay" OR "wrap in try-catch, return cached data") — the try-catch approach is already partially implemented for stats (5-min cache confirmed). | The `EmailService.SendAsync` (transactional email sending, not stats) does NOT currently have a fallback/retry for SMTP connection failures — this is a genuine gap, though CONCERNS.md marks it low-priority ("Migration plan," not "Fix approach"). Given the "no breaking changes, cleanup phase" framing, recommend documentation-only unless planner deems a secondary-relay implementation in-scope (larger effort, likely a Claude's Discretion call to descope to "documented, not implemented"). |

### Test Coverage Gaps (3 items — all IN scope, net-new test-writing tasks)

| Item | Notes |
|------|-------|
| Hangfire Job Retry Behavior Not Tested | New tests mocking `EmailService.SendAsync` to throw, verifying Hangfire retry/failed-queue behavior. Requires Hangfire's in-memory/test storage patterns — verify `QuestBoard.IntegrationTests` doesn't already have Hangfire test infrastructure (none found in this research pass; likely net-new). |
| Group Query Filter Enforcement Not Tested | New tests: `activeGroupContext.ActiveGroupId = null` → verify empty result; `= 999` (nonexistent) → verify empty result. `QuestBoard.IntegrationTests/WebApplicationFactoryBase.cs` already has a stub `IActiveGroupContext` per TENANT-05 (v5.0 requirement) — reuse that stub's mutation capability. |
| Follow-Up Quest Cleanup on Update Failure Not Tested | New tests mocking `UpdateQuestPropertiesWithNotificationsAsync` to throw, verifying orphan quest cleanup in `QuestController.CreateFollowUp`. Directly ties to the Two-Phase Follow-Up tech-debt item above — if that logic moves into a new `QuestService` method (recommended), write the test against the new method instead of the controller. |

## Comment Cleanup Worklist (D-06/D-07/D-08)

### Full inventory: ID-referencing comment occurrences

**Search pattern used:** `//[/]?\s*([A-Z]{2,12}-[0-9]{1,3})\b` (matches both `//` and `///` prefixed comments referencing patterns like `EMAIL-04`, `D-06`, `Phase 30-04`, `WR-03`, `CTRL-01`, `REQ-24-04`, `DMPRO-01`, `INFRA-06`, `HOME-01`, etc. — the full universe of GSD requirement-ID-shaped and ad-hoc test-ID-shaped tags found, not just the four examples in CONTEXT.md D-06)

**Total: 120 occurrences across 24 files.**

| Layer | Files | Occurrences |
|-------|-------|-------------|
| Domain (non-test) | 1 (`QuestService.cs`) | ~8 |
| Repository (non-test) | 2 (`QuestBoardContext.cs`, `Migrations/20260420142117_EnableLockoutForExistingUsers.cs`) | ~2 |
| Service (non-test) | 5 (`Program.cs`, `QuestController.cs`, `AdminController.cs`, `GroupController.cs`, `SessionReminderJob.cs`, `QuestFinalizedEmailJob.cs`, `GroupSessionMiddleware.cs`) | ~12 total non-test |
| UnitTests | 4 files | ~7 |
| IntegrationTests | 14 files | ~101 |

**Non-test source occurrences: 12. Test-file occurrences: 108 (90% of total).**

### Full file list (verified via grep, 2026-07-01)

**Non-test source (12 occurrences, 8 files) — highest priority, smallest footprint:**
- `QuestBoard.Domain/Services/QuestService.cs` — `EMAIL-04`, `D-06` (x2), `D-11`, `D-01..D-04`, `D-04`, `D-03`, `D-05..D-07`, `D-06` (8 tags across ~8 lines)
- `QuestBoard.Repository/Entities/QuestBoardContext.cs` — `TENANT-03`, `D-08`
- `QuestBoard.Repository/Migrations/20260420142117_EnableLockoutForExistingUsers.cs` — `SEC-02` (migration file — see caution below)
- `QuestBoard.Service/Program.cs` — `PWFLOW-06 (D-13)`, `PWFLOW-04 (D-12)`, `EMAIL-RATE-01..04`, `SESSION-01/SESSION-02`, `D-09` (block comment)
- `QuestBoard.Service/Controllers/QuestBoard/QuestController.cs` — `D-08`, `D-11`, `D-01..D-04`, `D-03`, `D-05`, `D-07`, `FOLLOW-03`, `WR-03`
- `QuestBoard.Service/Controllers/Admin/AdminController.cs` — `EMAIL-RATE-02/03`, `EMAIL-RATE-01/03`
- `QuestBoard.Service/Areas/Platform/Controllers/GroupController.cs` — `MGMT-02`, `MGMT-03`, `MGMT-04a`, `MGMT-04b`, `MGMT-05/06`
- `QuestBoard.Service/Jobs/SessionReminderJob.cs` — `D-09` (x2, one inline `NEW — D-06, D-09`)
- `QuestBoard.Service/Jobs/QuestFinalizedEmailJob.cs` — `D-09`
- `QuestBoard.Service/Middleware/GroupSessionMiddleware.cs` — `WR-03 (31-REVIEW)`

**Test files (108 occurrences, 16 files) — larger volume, mechanical removal, lower individual risk:**
- `QuestBoard.UnitTests/Services/SessionReminderJobTests.cs`, `DailyReminderJobTests.cs`, `EmailServiceTests.cs`, `EmailConfirmationJobGuardTests.cs`
- `QuestBoard.IntegrationTests/Controllers/*.cs` (12 files: `GroupManagementIntegrationTests.cs`, `QuestReminderTests.cs`, `GroupPickerControllerIntegrationTests.cs`, `AdminControllerIntegrationTests.cs`, `AdminHandlerIntegrationTests.cs`, `AccountControllerIntegrationTests.cs`, `HomeControllerIntegrationTests.cs`, `GroupSessionMiddlewareIntegrationTests.cs`, `PlatformAreaIntegrationTests.cs`, `QuestControllerIntegrationTests_Comprehensive.cs`, `PlayersControllerIntegrationTests.cs`, `QuestFinalizeTests.cs`, `QuestLogControllerIntegrationTests.cs`, `DungeonMasterControllerIntegrationTests.cs`, `CalendarControllerIntegrationTests.cs`)
- `QuestBoard.IntegrationTests/Tests/TenantIsolationTests.cs`
- `QuestBoard.IntegrationTests/Mobile/MobileViewsTests.cs` (**34 occurrences alone — largest single file**), `MobileCssTests.cs`, `MobileLayoutTests.cs`

### Caution: migration file comment

`QuestBoard.Repository/Migrations/20260420142117_EnableLockoutForExistingUsers.cs` line 13 has `// SEC-02: backfill LockoutEnabled = 1 for all existing users so the...`. **Recommend leaving migration file comments untouched** even though they match the D-06 pattern — migrations are historical/immutable records of what a specific deploy did, and editing them (even just comments) risks accidental changes to files that EF Core hashes/checksums are not affected by, but which convention treats as append-only. Flag this as a discretionary exception for the planner; the "entire codebase" language in D-06 technically includes it, but editing shipped migrations is unconventional.

### Caution: test-name ID tags may not all be "GSD requirement IDs"

Many test-file occurrences (`DMPRO-01`, `INFRA-04`, `HOME-01`, `CAL-01`, `QVIEW-01`, `CHAR-01`, `BROWSE-01`, `ADMIN-01`, `WR-01..05`, `CR-02`) are **test-case labels**, not necessarily traceable to a current `REQUIREMENTS.md` ID — some (e.g., `WR-03 (31-REVIEW)`) reference an ad-hoc code-review finding ID rather than a roadmap requirement. D-06's language ("referencing GSD requirement IDs or phase numbers") technically covers these too since they follow the same `PREFIX-NN` shorthand convention established by GSD tracking, and CONTEXT.md D-06 says "across the ENTIRE codebase... not just v1.x–v5.0 GSD-era comments" — so removal is in scope regardless of whether the ID maps to a still-tracked requirement. Planner should apply D-06 uniformly: strip the ID/phase prefix, keep any substantive description that follows the colon if it has standalone value (e.g., `// DMPRO-01: Profile page returns 200 for a valid DM user id` → `// Profile page returns 200 for a valid DM user id` — the WHAT is worth keeping, the ID prefix is not).

### D-07: XML doc comment backfill targets

**Existing pattern (the one D-07 asks to replicate), verified:**
```csharp
// Source: QuestBoard.Domain/Interfaces/IQuestRepository.cs (existing, lines 38-42)
/// <summary>
/// Returns all finalized quests for the given date across ALL groups.
/// Bypasses the group query filter — use only for system-wide sweep operations (DailyReminderJob). (D-08)
/// </summary>
Task<IList<Quest>> GetQuestsForTomorrowAllGroupsAsync(DateTime date, CancellationToken token = default);
```

**Important: this existing exemplar comment itself has a `(D-08)` tag embedded inside the `<summary>` text** — per D-06/D-08, the tag should be stripped from the prose (`(D-08)` → removed) while the XML doc structure and substantive content are kept. Same applies to `IQuestService.CreateFollowUpQuestAsync` and `GetQuestsByDungeonMasterAsync`, which embed `(D-01, D-02)`, `(D-03)`, `(D-04)`, `(D-05, D-06, D-07)`, `(D-08)` inline within their `<summary>` prose (verified, `QuestBoard.Domain/Interfaces/IQuestService.cs` lines 32-45).

**Full interface inventory (35 public interfaces total across Domain + Repository):**

| Interface | Location | XML doc status | D-07 priority |
|-----------|----------|-----------------|----------------|
| `IActiveGroupContext` | Domain | Has doc (1 block) — but contains "(Phase 28 temporary state; Phase 29 adds IsSuperAdmin)" phase-reference needing D-06 cleanup | Clean up existing doc |
| `IIdentityService` | Domain | Has doc (1 block) | Verify completeness |
| `IQuestEmailDispatcher` | Domain | Has doc (1 block) | Verify completeness |
| `IQuestRepository` (Domain) | Domain | Has doc (1 block, contains D-08 tag to strip) | Clean up existing doc; add docs to other 15 undocumented methods in same interface |
| `IQuestService` | Domain | Has doc (2 blocks, contain D-01..D-08 tags to strip) | Clean up existing docs; add docs to other 10 undocumented methods |
| `IReminderJobDispatcher` | Domain | Has doc (1 block) | Verify completeness |
| `IBaseRepository`, `IBaseService`, `ICharacterRepository`, `ICharacterService`, `IDungeonMasterProfileRepository`, `IDungeonMasterProfileService`, `IEmailRenderService`, `IEmailService`, `IGroupRepository`, `IGroupService`, `IPlayerSignupRepository`, `IPlayerSignupService`, `IReminderLogRepository`, `IShopRepository`, `IShopSeedService`, `IShopService`, `ITradeItemRepository`, `IUserRepository`, `IUserService`, `IUserTransactionRepository` | Domain | **Zero XML docs** (20 interfaces) | Net-new doc-writing — largest sub-task |
| `IBaseRepository`, `ICharacterRepository`, `IPlayerSignupRepository`, `IQuestRepository` (Repository — separate from Domain's), `IReminderLogRepository`, `IShopRepository`, `ITradeItemRepository`, `IUserRepository`, `IUserTransactionRepository` | Repository | **Zero XML docs** (9 interfaces — ALL Repository-layer interfaces) | Net-new doc-writing |

**Note on duplicate interface names:** `IQuestRepository` and other repository-shaped interfaces exist in BOTH `QuestBoard.Domain/Interfaces/` (operating on domain models, e.g. `Quest`) and `QuestBoard.Repository/Interfaces/` (operating on EF entities, e.g. `QuestEntity`) — these are two distinct interfaces with the same short name in different namespaces (`QuestBoard.Domain.Interfaces.IQuestRepository` vs `QuestBoard.Repository.Interfaces.IQuestRepository`). Both need independent XML doc treatment; the Domain one is the public contract consumed by Services, the Repository one is `internal`-adjacent tooling within the Repository project (still technically `public interface` but consumed only within that layer). This doubles the apparent interface count for repository-shaped interfaces — verify the planner's task list references the correct namespace/file when assigning doc-writing tasks.

**Scope-limiting guidance (per CONTEXT.md's "Claude's Discretion" on D-07):** Given 29 interfaces have zero coverage and each requires reading the interface's implementation to write an accurate `<summary>`, this is realistically a multi-hour task on its own. If the planner needs to scope-limit, prioritize: (1) interfaces that already have partial coverage (`IQuestRepository`, `IQuestService` — extend to 100%), (2) interfaces explicitly named in CONTEXT.md's example list (`IUserService`, `IActiveGroupContext`), (3) remaining Domain interfaces, (4) Repository interfaces last (lowest visibility — consumed only within Repository project, `internal` implementations).

## Scope Sizing Signal for the Planner (D-03 split decision)

| Workstream | Item count | File-touch estimate | Independent of other workstreams? |
|------------|-------------|----------------------|-------------------------------------|
| Dead code removal | 1 confirmed (`RegisterViewModel`) + broader sweep (unbounded, budget-dependent) | 1 file confirmed; sweep could add 5-15 more | Yes — fully independent |
| Comment cleanup (D-06) | 120 occurrences | 24 files (8 source, 16 test) | Yes — fully independent, mechanical |
| XML doc backfill (D-07) | 29 interfaces with zero coverage + 6 with partial coverage needing D-06 cleanup within existing docs | 35 files | Mostly independent — touches same files as comment cleanup in ~6 cases (docs living in interfaces that already have ID-tagged doc text) |
| CONCERNS.md fixes — Tech Debt | 4 items | ~5-6 files, 1 large (QuestController extraction) | Semi-independent; QuestController refactor has the largest blast radius in the phase |
| CONCERNS.md fixes — Known Bugs | 3 items (1 stale/no-op) | 3 files | Independent |
| CONCERNS.md fixes — Security | 3 items (mostly doc-only, 1 test addition) | 3-4 files | Independent |
| CONCERNS.md fixes — Performance | 3 items (1 needs migration, 1 may be no-op after benchmark, 1 documentation-only) | 2-3 files + 1 migration | Independent |
| CONCERNS.md fixes — Fragile Areas | 4 items (mostly comments/DRY refactor) | 4-5 files | Independent |
| CONCERNS.md fixes — Scaling Limits | 3 items (1 large — Forbid() checks across all controllers) | Potentially 6+ controller files for the Forbid() item | The Forbid()-check item is comparable in size to the QuestController refactor — could be its own sub-phase |
| CONCERNS.md fixes — Dependencies at Risk | 2 items (both documentation-only per CONCERNS.md's own fix approach) | 2 files | Independent, trivial |
| CONCERNS.md fixes — Test Coverage Gaps | 3 items (net-new tests) | 3+ new/modified test files | Independent, can run after other fixes land (tests validate the fixes) |
| Dependency vulnerability scan (D-09/D-10) | 1 scan, already clean | 0 files (evidence-only) | Fully independent, trivial, should be an early/first task |

**Total estimated distinct fix items: ~26** (1 dead-code + 20 CONCERNS.md items across 7 in-scope sections + 1 vuln scan + implicit XML-doc-backfill-as-one-item + implicit comment-cleanup-as-one-item, though both of the latter are really 24-35 file-level sub-tasks each).

**Total estimated file touches: 60-80+** across the whole phase (24 comment-cleanup files + 35 interface files, with overlap + ~15-20 CONCERNS.md fix files + dead-code files).

**Recommendation for planner:** This phase's scope, as sized above, is meaningfully larger than a typical single GSD phase. Two natural split points exist:
1. **By risk/blast-radius:** "34a: Mechanical cleanup" (dead code removal, comment stripping, XML doc backfill, dependency scan — all low-risk, high-file-count, no behavior change) vs. "34b: CONCERNS.md fixes" (the ~20 items, several of which involve actual logic changes, refactors, or new tests).
2. **By CONTEXT.md's own suggested grouping:** "34a: Security & Bugs" (Security Considerations + Known Bugs + the vuln scan) vs. "34b: Performance & Architecture" (Tech Debt + Performance + Fragile Areas + Scaling Limits + Dependencies at Risk + Test Coverage Gaps), with comment cleanup and dead code and XML docs either folded into 34a as prerequisite hygiene or run as a third "34c: Comments & Docs" phase.

Given the volume, a 3-way split (mechanical cleanup / security+bugs / performance+architecture) may fit better than 2, but this is explicitly a planner discretion call per D-03.

## Common Pitfalls

### Pitfall 1: Treating stale CONCERNS.md claims as ground truth
**What goes wrong:** Writing a "fix" for the `SessionReminderJob.cs` null-dereference bug that CONCERNS.md describes, when the code is already null-safe.
**Why it happens:** `CONCERNS.md` and `CONTEXT.md` were both authored the same day (2026-07-01) via automated codebase analysis, but the analysis itself can be wrong or based on a slightly different code state / analysis pass than what's in the repo now.
**How to avoid:** Before writing any fix task, re-verify the claimed problem exists in the current code (this research already did this for the highest-risk items — see per-item verification notes above). For items not re-verified in this research pass (Shop Item Queries, Hangfire Job Queue scaling), the planner or executor should do a quick verification read before committing to the fix.
**Warning signs:** A "fix" that would just re-add code equivalent to what's already there (e.g., a null-check duplicate of an existing `?.` operator).

### Pitfall 2: Editing migration file comments
**What goes wrong:** D-06 says "entire codebase," which technically includes `Migrations/*.cs` files — editing a shipped migration's comment is unconventional even though it's low-risk (comments don't affect `ModelSnapshot` hashing).
**Why it happens:** Overly literal application of "entire codebase" scope.
**How to avoid:** Treat migrations as append-only historical artifacts; exclude `QuestBoard.Repository/Migrations/*.cs` (both the migration and its `.Designer.cs`) from the comment-cleanup sweep, or explicitly confirm with the user this exclusion is acceptable during planning.

### Pitfall 3: Removing XML-doc-embedded ID tags improperly
**What goes wrong:** Some existing XML doc comments embed the ID tag INSIDE the `<summary>` prose (e.g., `...(D-08)` at the end of a sentence) rather than as a separate `//` line. Blindly stripping the whole line would destroy the useful doc content, not just the tag.
**Why it happens:** The existing exemplar pattern (`IQuestRepository.GetQuestsForTomorrowAllGroupsAsync`) itself has this issue.
**How to avoid:** For XML doc comments, remove only the parenthetical ID reference substring (e.g., `" (D-08)"`) and keep the rest of the sentence intact — do not delete the whole doc comment.

### Pitfall 4: Breaking the "no user-facing functionality removed" constraint via enum-cast or query-filter changes
**What goes wrong:** CONCERNS.md's fix suggestions for "Manual Enum Casting" (switch to `Enum.Parse`) and "Query Filter Inconsistent" (implies adding filters) could change runtime behavior if implemented literally.
**Why it happens:** Some CONCERNS.md fix approaches are written as general best-practice suggestions, not scoped specifically to preserve current behavior.
**How to avoid:** For the enum-casting item, stick to documentation/comments + a new validation test — do not change the cast mechanism. For the query-filter item, do NOT add a filter to `UserEntity` (explicitly called out as intentional in both `ARCHITECTURE.md` and this phase's CONTEXT.md "Known Landmines").

### Pitfall 5: Scope creep into "Missing Critical Features"
**What goes wrong:** Fixing the "Resend API Rate Limiting" item (Known Bugs) or the "Shop Item Queries" item (Performance) might tempt an implementer toward related territory that overlaps with digest batching or email preferences (explicitly deferred per D-02).
**Why it happens:** Email-related fixes are adjacent to the deferred email features.
**How to avoid:** Keep the 429-retry-backoff fix scoped strictly to `GetResendStatsAsync`'s HTTP retry logic — do not touch `SessionReminderJob`/`DailyReminderJob`'s per-quest send-one-email-each behavior, which is the actual digest-batching deferred item.

## Code Examples

### Hangfire global retry configuration (Hangfire 1.8.23, verified installed version)
```csharp
// Source: api.hangfire.io/html/T_Hangfire_AutomaticRetryAttribute.htm — add near existing AddHangfire(...) block in Program.cs
using Hangfire;

// Place once, inside the `if (!builder.Environment.IsEnvironment("Testing"))` block,
// after builder.Services.AddHangfire(...) registration:
GlobalJobFilters.Filters.Add(new AutomaticRetryAttribute
{
    Attempts = 5,
    DelaysInSeconds = new[] { 1, 2, 4, 8, 16 }
});
```

### EF Core composite index (matches existing fluent pattern in QuestBoardContext.cs)
```csharp
// Source: QuestBoard.Repository/Entities/QuestBoardContext.cs (existing pattern, e.g. line 202)
// Add inside OnModelCreating, alongside the other modelBuilder.Entity<...>().HasIndex(...) calls:
modelBuilder.Entity<QuestEntity>()
    .HasIndex(q => new { q.IsFinalized, q.FinalizedDate });
```
Then from `QuestBoard.Service/` (per CLAUDE.md's documented workflow):
```bash
dotnet ef migrations add AddQuestFinalizedDateIndex --project ../QuestBoard.Repository
```
Migration auto-applies on next app startup via `context.Database.Migrate()` — no manual `dotnet ef database update` needed in dev.

### XML doc comment pattern to replicate (D-07), with ID-tag stripped
```csharp
// Source: QuestBoard.Domain/Interfaces/IQuestRepository.cs — BEFORE (current state)
/// <summary>
/// Returns all finalized quests for the given date across ALL groups.
/// Bypasses the group query filter — use only for system-wide sweep operations (DailyReminderJob). (D-08)
/// </summary>
Task<IList<Quest>> GetQuestsForTomorrowAllGroupsAsync(DateTime date, CancellationToken token = default);

// AFTER (D-06 + D-07 applied — tag stripped, doc kept)
/// <summary>
/// Returns all finalized quests for the given date across ALL groups.
/// Bypasses the group query filter — use only for system-wide sweep operations (DailyReminderJob).
/// </summary>
Task<IList<Quest>> GetQuestsForTomorrowAllGroupsAsync(DateTime date, CancellationToken token = default);
```

### Comment cleanup example (D-06 + D-08 applied together)
```csharp
// Source: QuestBoard.IntegrationTests/Controllers/DungeonMasterControllerIntegrationTests.cs:26 — BEFORE
// DMPRO-01: Profile page returns 200 for a valid DM user id

// AFTER — ID prefix stripped, substantive description kept (D-08: description has standalone value)
// Profile page returns 200 for a valid DM user id
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|---------------|--------|
| N/A — no external framework/library version changes in this phase | N/A | N/A | This phase does not involve adopting new libraries or migrating to newer major versions; all packages are already current (verified via `dotnet list package --outdated`) |

**Deprecated/outdated:** None found — dependency scan confirms no deprecated packages in use.

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|---------------|
| A1 | The broader dead-code sweep (beyond `RegisterViewModel`) was not exhaustively performed in this research pass — additional candidates likely exist but require per-symbol RIP verification during planning/execution | Dead Code Removal Worklist | Low — explicitly flagged as a to-do for the planner/executor, not presented as "no other dead code exists" |
| A2 | `ShopRepository.cs`'s current `.Include()` usage for the "Shop Item Queries Not Optimized" performance item was not independently re-verified against the live file in this research pass | CONCERNS.md Fix-Approach Validation, Performance Bottlenecks | Low-Medium — if the file already has appropriate projection, this item (like the null-dereference bug) may be stale; planner should verify before writing a fix task |
| A3 | The "Hangfire Job Queue Not Filtered by Group" scaling concern's recommendation to defer/document-only (rather than implement batching) is this researcher's judgment call based on current single-group-history usage patterns (STATE.md), not an explicit user decision | CONCERNS.md Fix-Approach Validation, Performance Bottlenecks | Medium — planner/user may prefer to implement the batching fix now; flagged as discretionary, not a hard recommendation |

## Open Questions

**(RESOLVED 2026-07-01 via phase split — see ROADMAP.md)** All three questions below concerned the D-03 split decision. The planner resolved them by splitting Phase 34 into 34 (mechanical cleanup, planned), 34.1 (Security & Bugs, inserted), and 34.2 (Performance & Architecture, inserted). Left in place for historical context; not open items blocking Phase 34's 5 plans.

1. **Should the phase split 2-way or 3-way (D-03)?**
   - What we know: Total scope is ~26 distinct fix items + 120 comment occurrences + 35-interface doc backfill, spanning 60-80+ file touches.
   - What's unclear: Exact context-budget threshold that triggers a split, and whether the user prefers fewer/larger sub-phases or more/smaller ones.
   - Recommendation: Planner should size a single-phase attempt first against its own context budget; if it doesn't fit, use the "mechanical cleanup / security+bugs / performance+architecture" 3-way split suggested above, since these three groups have the cleanest independence boundaries (verified during this research).
   - **Resolved:** 3-way split adopted — Phase 34 (mechanical cleanup, 5 plans), Phase 34.1 (Security & Bugs), Phase 34.2 (Performance & Architecture).

2. **Should `Migrations/*.cs` comment be excluded from D-06's sweep?**
   - What we know: Only 1 occurrence found (`SEC-02` in `20260420142117_EnableLockoutForExistingUsers.cs`), so the stakes are low either way.
   - What's unclear: Whether the user's "entire codebase" framing in D-06 was meant to include historical migration files.
   - Recommendation: Default to excluding migrations (treat as immutable history); flag this exclusion explicitly in the plan for user visibility given the phase's "no exceptions" framing (D-04's "nothing is left for later" language could be read to include this).
   - **Resolved:** Migrations excluded from Phase 34's comment sweep per the default recommendation; not carried into 34-02/34-03 plan scope.

3. **Is the "No Tenant Isolation Enforcement at API Boundary" Forbid()-check fix in-scope for THIS phase, given its size rivals the QuestController refactor?**
   - What we know: CONCERNS.md explicitly lists it under Scaling Limits (in scope per D-01), but its blast radius (every controller loading a GroupId-scoped entity) is comparable to the largest Tech Debt item.
   - What's unclear: Whether the user intended "Scaling Limits" items to include architecture-wide changes of this size, or lighter-weight documentation/monitoring notes (as the other two Scaling Limits items resolve to).
   - Recommendation: Planner should size this item explicitly and consider isolating it to its own plan/sub-phase if it's implemented at all — do not silently fold it into a "quick fixes" wave given its actual footprint.
   - **Resolved:** Moved to Phase 34.2 (Performance & Architecture) scope; not part of the mechanical-cleanup Phase 34 planned here. Should be sized as its own plan when 34.2 is planned.

## Validation Architecture

### Test Framework
| Property | Value |
|----------|-------|
| Framework | xUnit v3 (`xunit.v3` 3.2.2, `xunit.runner.visualstudio` 3.1.5), `Microsoft.NET.Test.Sdk` 18.7.0 |
| Config file | `QuestBoard.IntegrationTests/xunit.runner.json` (only project with a custom runner config) |
| Quick run command | `dotnet test QuestBoard.UnitTests` (fast, no DB dependency — uses NSubstitute mocks) |
| Full suite command | `dotnet test` (runs all 5 projects; 191 tests confirmed passing as of Phase 33 close per `.planning/STATE.md`) |

### Phase Requirements → Test Map

This phase has no REQ-IDs (cleanup/hardening phase, not a features phase) — the "requirements" are the CONCERNS.md items themselves plus the dead-code/comment-cleanup deliverables. Test mapping below is by deliverable category rather than REQ-ID:

| Deliverable | Behavior | Test Type | Automated Command | File Exists? |
|-------------|----------|-----------|-------------------|--------------|
| Dead code removal | Build succeeds after `RegisterViewModel` deletion; no broken references | build | `dotnet build` | N/A — compiler is the test |
| Comment cleanup | Build succeeds after comment removal (comments don't affect compilation, but a bad find/replace could corrupt syntax) | build | `dotnet build` | N/A |
| XML doc backfill | Build succeeds; XML doc comments don't break compilation | build | `dotnet build` | N/A |
| Dependency vuln scan | No vulnerable packages found | manual/scripted | `dotnet list package --vulnerable --include-transitive` | N/A — CLI output is the evidence |
| Tech Debt: `DateTime.Now` → `UtcNow` fix | Seed dates use UTC | unit | Existing `ShopSeedService` tests if any exist, else manual verification (seed data isn't typically asserted precisely) | Check `QuestBoard.UnitTests/Services/` for `ShopSeedServiceTests.cs` — not confirmed in this research pass |
| Known Bugs: startup email validation | App throws in Production with missing email config; app starts fine in Testing/Development | integration | New test needed — verify `WebApplicationFactoryBase` doesn't accidentally trigger the new throw in Testing environment | ❌ Wave 0 — net-new test |
| Known Bugs: Resend 429 retry-backoff | Retries on 429, succeeds on eventual 2xx | unit | New test mocking `HttpClient` (`IHttpClientFactory`) to return 429 then 200 | ❌ Wave 0 — net-new test |
| Performance: composite index | Migration applies cleanly; query behavior unchanged (index doesn't change result set) | integration | Existing `DailyReminderJob`/`QuestReminderTests` should still pass unmodified — index is additive | ✅ existing tests cover behavior; new test optional for query-plan verification |
| Scaling: null ActiveGroupId guard | Throws `InvalidOperationException` only for unexpected-null (not SuperAdmin's intentional null) | unit/integration | New test — must NOT break existing SuperAdmin `null = see all` tests (`TenantIsolationTests.cs`) | ❌ Wave 0 — net-new test, high care needed not to break TENANT-01/02 |
| Test Coverage Gaps (3 CONCERNS.md items) | See CONCERNS.md Fix-Approach Validation, Test Coverage Gaps section | integration/unit | New tests per item | ❌ Wave 0 — net-new, explicitly the deliverable itself |

### Sampling Rate
- **Per task commit:** `dotnet build` (fast compile-check for comment/dead-code/doc changes); `dotnet test QuestBoard.UnitTests` for logic changes
- **Per wave merge:** `dotnet test` (full 191+ test suite — count will grow as Test Coverage Gap items add new tests)
- **Phase gate:** Full suite green before `/gsd-verify-work`, plus a final `dotnet list package --vulnerable --include-transitive` re-run as phase-closing evidence

### Wave 0 Gaps
- [ ] New test: startup email-config validation (Production-only throw) — needs care to not trigger in Testing environment
- [ ] New test: Resend API 429 retry-backoff behavior (mock `HttpClient` via `IHttpClientFactory`)
- [ ] New test: `ActiveGroupId` null-guard behavior (must not break existing SuperAdmin-null-is-valid tests)
- [ ] New tests: the 3 explicit Test Coverage Gaps items from CONCERNS.md (Hangfire retry, group filter enforcement, follow-up cleanup rollback)
- [ ] Verify: does `QuestBoard.UnitTests/Services/ShopSeedServiceTests.cs` exist? Not confirmed in this research pass — check before planning the `DateTime.Now` fix's test coverage.

## Security Domain

### Applicable ASVS Categories

| ASVS Category | Applies | Standard Control |
|----------------|---------|-------------------|
| V2 Authentication | No new work | ASP.NET Core Identity (existing, unchanged by this phase) |
| V3 Session Management | No new work | ASP.NET Core Session + SQL Server distributed cache (Phase 33, unchanged) |
| V4 Access Control | Yes | `[Authorize]` policies + `GroupSessionMiddleware` (existing) — this phase's "No Tenant Isolation Enforcement at API Boundary" item adds defense-in-depth `Forbid()` checks |
| V5 Input Validation | No new work | Data Annotations + ModelState (existing, unchanged) |
| V6 Cryptography | No new work | ASP.NET Core Data Protection / Identity password hashing (existing, unchanged) |
| V7 Error Handling & Logging | Yes | This phase's "Email Configuration Secrets Potentially Logged" item — verify structured logging pattern (`logger.LogError(ex, "template", param)`) is used consistently, never string-interpolating secrets |
| V13 Malicious Code / Dependency | Yes | This phase's D-09/D-10 dependency vulnerability scan directly maps to ASVS V13's third-party component verification requirement |

### Known Threat Patterns for this stack

| Pattern | STRIDE | Standard Mitigation |
|---------|--------|----------------------|
| CSRF on state-changing POST actions | Tampering | `[ValidateAntiForgeryToken]` — already present on all `AdminController` POST actions (verified this research pass); phase's recommendation is a regression test, not a new mitigation |
| Cross-tenant data leakage via missing/incorrect Global Query Filter | Information Disclosure | EF Core `HasQueryFilter` on `QuestEntity`/`ShopItemEntity` (existing) + this phase's proposed `Forbid()` defense-in-depth checks and `InvalidOperationException` guard for unexpected-null `ActiveGroupId` |
| Vulnerable third-party dependency | Tampering / Elevation of Privilege (via known CVEs) | `dotnet list package --vulnerable` scan (D-09) — confirmed clean this pass; should be re-run as a recurring practice, not just a one-time phase task |
| Secrets in logs/exception traces | Information Disclosure | Structured logging with `ILogger<T>` template parameters (never string-interpolating secret values into the message) — existing pattern, this phase adds explicit verification |
| Resend API token exposure via HttpClient default headers | Information Disclosure | Per-request `Authorization` header injection (existing, confirmed) rather than a shared default header on the named `HttpClient` — reduces blast radius if the `HttpClient` instance were ever logged/inspected |

## Sources

### Primary (HIGH confidence)
- Live repository read/grep/build-tool output (`dotnet list package --vulnerable/--outdated/--deprecated`, `dotnet --version`, `dotnet ef --version`) — 2026-07-01
- `.planning/codebase/CONCERNS.md`, `ARCHITECTURE.md`, `CONVENTIONS.md`, `STATE.md`, `REQUIREMENTS.md` — all dated 2026-07-01, read directly
- `.planning/phases/34-.../34-CONTEXT.md` — read directly

### Secondary (MEDIUM confidence)
- [Hangfire AutomaticRetryAttribute documentation](https://api.hangfire.io/html/T_Hangfire_AutomaticRetryAttribute.htm) — confirmed via WebSearch, matches installed Hangfire 1.8.23's public API surface (attribute has existed since early Hangfire versions, no version-specific syntax risk identified)

### Tertiary (LOW confidence)
- None — all claims in this research were either verified directly against the repository or cited from Hangfire's official API docs.

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH — no new libraries introduced; all versions verified via .csproj + `dotnet list package`
- Architecture: HIGH — directly read `ARCHITECTURE.md` and cross-verified anti-patterns against live code
- Pitfalls: HIGH — the stale-CONCERNS.md-claim pitfall was discovered through direct verification, not speculation
- CONCERNS.md fix-approach validation: HIGH for spot-checked items (most Security/Known Bugs/Tech Debt items), MEDIUM for items not independently re-verified (Shop Item Queries projection, Hangfire Job Queue group-filtering)

**Research date:** 2026-07-01
**Valid until:** 14 days (this research is tightly coupled to the exact current state of a fast-moving branch — `milestone/v5-multi-tenancy` — re-verify file/line references if significant additional commits land before planning begins)
