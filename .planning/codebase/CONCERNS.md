# Codebase Concerns

**Analysis Date:** 2026-07-03
**last_mapped_commit:** e5b37a73cda29bf355c4de6ebf4663b1625c3cf6

## Tech Debt

**GroupSessionMiddleware POST-body data loss on session expiry:**
- Issue: `GroupSessionMiddleware.cs` (lines 80–92) redirects on *all* HTTP verbs including POST. When an authenticated user's session expires mid-form-submission, a 302 redirect causes browsers to re-issue the request as GET, silently dropping the submitted body with no error signal.
- Files: `QuestBoard.Service/Middleware/GroupSessionMiddleware.cs`
- Impact: Users submitting forms (Create Quest, Create Character, etc.) experience silent data loss if their `ActiveGroupId` session expires between page load and submission. The middleware returns HTTP 409 Conflict for non-idempotent requests (documented in comments, line 26), but this is not yet implemented.
- Fix approach: Change line 80's condition to check if method is NOT idempotent; for POST/PUT/PATCH/DELETE, return 409 Conflict *before* attempting any redirect. Requires coordination with client-side error handling (currently expects 302 redirect, must handle 409).
- Flagged: Phase 31 code review (PROJECT.md line 152)
- Status: Not fixed — deferred post-v5.0 as a lower-priority user-experience edge case (session expiry mid-submission is rare with 7-day token lifespan)

**Digest batching for session reminders (EMAIL-04/REMIND-02):**
- Issue: Session reminder emails are sent one-per-quest. If a player has multiple quests scheduled for the same day, they receive separate emails for each.
- Files: `QuestBoard.Service/Jobs/SessionReminderJob.cs`, `QuestBoard.Service/Services/HangfireReminderJobDispatcher.cs`
- Impact: Minor user experience (inbox clutter on busy scheduling days) and marginal Resend API quota usage multiplier.
- Fix approach: Implement a batching layer in `IReminderJobDispatcher` that deduplicates player+date combinations across all pending reminder jobs, emitting a single combined email per player per day. Requires grouping `SessionReminderJob` by (PlayerId, FinalizedDate).
- Status: Deferred — same-day quests have never occurred in one year of operation (17 members, low scheduling density). Complexity not justified without real demand.

**Profile picture crop/avatar selection (issue #78):**
- Issue: Character profile picture upload exists, but no client-side crop/selection feature. Implementation requires `SkiaSharp` for image manipulation.
- Files: View model in `QuestBoard.Service/ViewModels/CharacterViewModels/CharacterViewModel.cs`, no current image-processing logic
- Impact: Users cannot resize/crop uploaded images; profile page displays full-resolution uploads which may degrade performance or page layout on large images.
- Fix approach: Add SkiaSharp NuGet package, implement image crop/resize service in Domain layer (no direct image I/O), wire into character creation/edit controllers.
- Blocker: SkiaSharp native library (`libSkiaSharp.so`) availability on deployment host (Debian Bookworm, aspnet:10 base image) must be verified via build testing on production environment before implementation.
- Status: Paused — native-lib verification deferred

**CI/CD .NET version mismatch (dotnet.yml vs runtime):**
- Issue: `.github/workflows/dotnet.yml` line 25 uses `dotnet-version: 8.0.x` for CI builds, but project runs on .NET 10 (verified in Phase 37 context). Binary release workflow (line 26 of `binary-release.yml`) correctly uses `10.0.x`, but main CI pipeline builds against .NET 8.
- Files: `.github/workflows/dotnet.yml`, `.github/workflows/binary-release.yml`
- Impact: CI tests may pass against wrong runtime version; incompatibilities (new language features, breaking APIs) between .NET 8 and 10 could be masked until release. Build artifacts from dotnet.yml cannot run on .NET 10 host.
- Fix approach: Update `.github/workflows/dotnet.yml` line 25 to `dotnet-version: 10.0.x` to match `binary-release.yml` and runtime version. Verify all tests pass with .NET 10 SDK.
- Priority: High — unify CI/release build configuration to prevent version skew

---

## Known Bugs

**Stale SessionReminderJob null-dereference claim (VERIFIED CLOSED):**
- Symptoms: Historical Phase 34.1 CONCERNS.md claimed `SessionReminderJob.cs` could null-dereference on `quest.DungeonMaster.Name` (line 105).
- Files: `QuestBoard.Service/Jobs/SessionReminderJob.cs`
- Root cause analysis: The null-dereference was never real — line 105 already has null-coalescing (`quest.DungeonMaster?.Name ?? string.Empty`), and `signup.Player` is checked at line 86 before use, and `signup.Player.Email` is checked again at line 86 before sending. All access paths are null-safe.
- Verification: Phase 34.1 regression test added (lines 94–100 of `SessionReminderJob`) confirming the pattern holds. No fix needed.
- Status: Closed — documented and verified; never a true defect

**Resend API 429 rate-limit retry-with-backoff (FIXED in Phase 34.1):**
- Symptoms: `ResendStatsClient.cs` FetchAllRecordsAsync had no retry logic for Resend API rate-limit responses (HTTP 429 Too Many Requests).
- Files: `QuestBoard.Service/Services/ResendStatsClient.cs`
- Fix: Added exponential backoff retry loop (lines 30–48) with up to 3 retries, base delay 1s, 2x multiplier per attempt (1s/2s/4s), respecting Retry-After header if present. Bounded by MaxRetries constant.
- Status: Fixed — Phase 34.1

**Email configuration secrets appearing in logs (MITIGATED):**
- Symptoms: Early implementations logged Resend API tokens or SMTP passwords in exception traces or debug output.
- Files: `QuestBoard.Service/Services/ResendStatsClient.cs`, `QuestBoard.Service/Program.cs`
- Mitigation: Bearer token injected per-request (line 35 `ResendStatsClient.cs`), never held in shared HttpClient state. Email configuration values never logged directly; only HTTP status codes and error messages are written to stderr (lines 46, 59). Production-only validation guard checks for empty config at startup (`Program.cs` email validation).
- Status: Mitigated — documented in CLAUDE.md "Code Navigation" section and resend client comment (line 33–34)

**Production email configuration startup guard typo (FIXED in Phase 34.1):**
- Symptoms: Startup validation checked `Email:FromEmail`/`Email:SmtpServer` keys that don't exist in config (actual keys are `EmailSettings:FromEmail`/`EmailSettings:SmtpServer`).
- Files: `QuestBoard.Service/Program.cs` (email validation block, roughly lines 160–170 before fix)
- Impact: The guard would fail on every Production boot regardless of real config, preventing startup.
- Fix: Corrected the config key names to match `appsettings.json` and `EmailSettings` class bindings.
- Status: Fixed — Phase 34.1 (CR-01)

**DM ownership checks using `User.Name` instead of `User.Id` (FIXED in Phase 34.3):**
- Symptoms: Early implementations checked DM ownership via `User.Name` comparison (e.g., `quest.DungeonMaster.Name == User.FindFirst(ClaimTypes.Name)?.Value`).
- Files: Affected `QuestController.cs` ownership checks (corrected in Phase 34.3-02 plan)
- Issue: `User.Name` has no uniqueness constraint. `AccountController.Edit` allows any user to rename freely, permitting display-name collision attacks — one user could impersonate another's ownership by adopting their display name.
- Fix: Switched all ownership checks to `User.Id` comparison, which *is* unique across the system.
- Status: Fixed — Phase 34.3 (WR-03); user-initiated rename flow tested to ensure no collision regression

**Group role authorization regression — ~20 inline `IsInRole` orphaned after Phase 27 (FIXED in Phase 34.3):**
- Symptoms: Phase 27 moved per-group roles from `AspNetUserRoles` table to `UserGroups.GroupRole` enum, but only policy-based authorization handlers (`AdminHandler`, `DungeonMasterHandler`) were updated in Phase 29. Inline `User.IsInRole("Admin")` calls in controllers silently always evaluated false for real Admins/DMs.
- Files: `QuestController.cs` (~12 sites), `QuestLogController.cs` (2 sites), `DungeonMasterController.cs` (3 sites), `Admin/AccountController.cs` (3 sites)
- Impact: Admins/DMs could not manage quests, edit quest logs, or access admin-only actions; feature regression vs. v4.x behavior.
- Fix: Created shared `IUserService.GetEffectiveGroupRoleAsync(User, groupId)` helper with SuperAdmin bypass; migrated all 20 call sites to use it across four controllers.
- Status: Fixed — Phase 34.3; discovered during manual pre-ship testing, not in production deployment

**SuperAdmin null-dereference on `RequireActiveGroupId()` (FIXED in Phase 34.3):**
- Symptoms: Phase 34.2 introduced `RequireActiveGroupId()` guard to enforce ASVS V4 null checks, but wired it into places SuperAdmin could reach (SuperAdmin has no active group by design).
- Files: Call sites in `QuestController.cs`, line 47 after 34.2 fix
- Impact: SuperAdmin accessing `/quests` would crash with NullReferenceException because `RequireActiveGroupId()` throws on null.
- Fix: Added SuperAdmin short-circuit in all call sites: check `User.IsInRole("SuperAdmin")` before calling `RequireActiveGroupId()`. Alternately, `GetEffectiveGroupRoleAsync` already short-circuits SuperAdmin internally (Phase 34.3).
- Status: Fixed — Phase 34.3 (multiple code-review rounds CR-02, WR-01); comprehensive integration tests added

---

## Security Considerations

**Resend API token in httpOnly session/cookies:**
- Risk: Resend authentication is managed via environment variables, not session. If environment variable is ever logged or printed, the token could be exposed.
- Files: `QuestBoard.Service/Services/ResendStatsClient.cs`, Program configuration
- Current mitigation: Token never held in HttpClient default headers (injected per-request at line 35). No logging of token value anywhere. Configuration secrets never logged.
- Recommendations: Audit all logging and exception handlers to ensure `IOptions<EmailSettings>` values are never stringified. Use `[Sensitive]` attribute on `EmailSettings` properties if available in logging framework.

**CSRF protection on state-changing actions:**
- Risk: State-changing HTTP verbs (POST/PUT/DELETE) vulnerable to cross-site form submission if `[ValidateAntiForgeryToken]` is missing.
- Files: `QuestController.cs`, `AdminController.cs`, `ShopManagementController.cs`, etc.
- Current coverage: Phase 34.1 added a reflection-based scan during tests to verify all POST/PUT/PATCH/DELETE actions have the attribute. Coverage verified at test time.
- Recommendations: Maintain the test coverage scan; consider a pre-commit hook or CI check to prevent future additions of unprotected state-changing actions.

**Email enumeration in password-reset flow:**
- Risk: ForgotPassword endpoint could leak whether an email exists in the system.
- Files: `AccountController.ForgotPassword` POST action
- Current mitigation: Generic "If an account with that email exists..." message regardless of lookup result. Both success and non-found cases return the same view.
- Status: Adequate — enumeration safety verified during Phase 32 integration tests

**SQL injection via EF Core query filters:**
- Risk: Global Query Filter on `QuestEntity` filters by `groupId` — if `ActiveGroupId` is sourced from user input without validation, group boundaries could be bypassed.
- Files: `QuestBoard.Repository/QuestBoardContext.cs` (HasQueryFilter configuration), `IActiveGroupContext` resolution
- Current mitigation: `ActiveGroupId` is read from ASP.NET Core Session, which is server-side managed and tamper-proof (stored in SQL Server cache). Never sourced from HTTP headers or query strings.
- Status: Secure by design

**SuperAdmin bypass logic in authorization:**
- Risk: SuperAdmin short-circuit in `GetEffectiveGroupRoleAsync` and `RequireActiveGroupId()` could bypass intended access controls if condition is applied incorrectly.
- Files: `QuestController.cs` (lines 45–47), `UserService.GetEffectiveGroupRoleAsync`, all Phase 34.3 migration sites
- Current mitigation: SuperAdmin check is explicit (`User.IsInRole("SuperAdmin")`) at each call site. Hangfire jobs explicitly reject SuperAdmin (no group context). Platform area routes block non-SuperAdmins via `[Authorize(Policy = "SuperAdminOnly")]`.
- Status: Adequate — Phase 34.3 verified all sites and added comprehensive integration tests

**Docker build stage secrets exposure:**
- Risk: Multi-stage Dockerfile copies all source code and dependencies into build stage, which can be inspected if intermediate image layers are accessible or if build cache is exposed. NuGet cache mount (line 16, `.github/workflows/docker-publish.yml`) caches `/root/.nuget/packages` — if build secrets or nuget.config auth are stored there, they could leak.
- Files: `Dockerfile` (lines 1–41), `.dockerignore`
- Current mitigation: `.dockerignore` (line 2) excludes `.env` files from Docker build context. Build runs in GitHub Actions, not exposing intermediate layers publicly. NuGet auth via GitHub Packages uses `GITHUB_TOKEN` secret (docker-publish.yml line 59), injected per-request, never stored in build cache.
- Recommendations: Verify `.dockerignore` includes all sensitive files (`.env*`, `secrets/`, `*.key`, `*.pem`). Consider using `docker/build-push-action` BuildKit secrets mount (`--secret=id=...`) for any future auth needs instead of build args.
- Status: Adequate — current mitigation sufficient; document for future builds

---

## Performance Bottlenecks

**Full table loads via `ToListAsync()` in repository:**
- Problem: Several repository methods call `ToListAsync()` on large result sets without pagination or projection, materializing all rows into memory.
- Files: `QuestRepository.cs` (`GetQuestsWithSignupsForRoleAsync`), `CharacterRepository.cs` (guild member loads), `GroupRepository.cs` (member count queries)
- Current scale: 17 users, ~100 characters, ~50 quests total — queries return dozens of rows, acceptable performance.
- Improvement path: Implement `IAsyncPageable<T>` pattern or cursor-based pagination for views that render unbounded lists. Add `.Select(e => new { Id, Title })` projections for read-only views that don't need full entity graph. Composite index on `Quests(IsFinalized, FinalizedDate)` added in Phase 34.2 helps finalized-quest queries.
- Priority: Low — no user complaints, but flag for revisit if user base grows beyond ~50 members or quest volume increases

**N+1 query risk in character + image loading:**
- Problem: `CharacterViewModel` lazy-loads character images via separate HTTP request (Url.Action helper), not eager-loaded EF Include.
- Files: `QuestBoard.Service/ViewModels/CharacterViewModels/CharacterViewModel.cs`, Razor views
- Current scale: Guild member page renders ~17 characters max, each triggering one image-fetch request.
- Improvement path: Pre-load `CharacterImage` navigation property via `.Include(c => c.CharacterImage)` in character repository method. Render images inline using `Base64` encoding or blob URLs instead of separate HTTP requests.
- Priority: Medium — visible on guild members page if user base grows; easy win once achieved

**Session reminder job query scope:**
- Problem: `SessionReminderJob` loads full quest + all signups + all votes + all date proposals to filter Yes/Maybe voters. No projection.
- Files: `QuestBoard.Service/Jobs/SessionReminderJob.cs` (lines 30 and 61–75)
- Current scale: Quests have ~5–10 signups on average; full loads acceptable.
- Improvement path: Add a lean repository method `GetQuestReminderProjectionAsync(questId)` returning only (Title, DM, FinalizedDate, Signups with votes on finalized date, confirmed player list). Project in SQL, not in-memory LINQ.
- Priority: Low — Hangfire is not timing-critical (24h cadence for daily reminders, manual DM triggers); current implementation is defensive and correct over fast

**Hangfire dashboard on high job volume:**
- Problem: Hangfire SQL Server storage grows unbounded with job history. Dashboard queries all-jobs UI on the `/hangfire` admin page without pagination.
- Files: Hangfire configuration in `Program.cs` (lines 170–190 approx.), no custom cleanup job
- Current scale: ~50 jobs/month (quests x 3 email jobs), ~600/year. Negligible.
- Improvement path: Add a cleanup job or configure Hangfire's `Dashboard` options to limit history display window. Monitor `HangfireJob` table size post-deployment.
- Priority: Low — current scale poses no storage concern; revisit at >1000 jobs/month

---

## Fragile Areas

**GroupSessionMiddleware edge cases:**
- Files: `QuestBoard.Service/Middleware/GroupSessionMiddleware.cs`
- Why fragile: Line 71 checks `StartsWithSegments` on exempt paths. If a controller name changes or a new route is added, the exempt path list must be manually updated. No compile-time check prevents drift.
- Safe modification: When renaming controllers or adding new routes that should skip session recovery:
  1. Update `ExemptPathPrefixes` array (lines 41–48) with new path
  2. Use `nameof(...)` for controller-derived paths when possible to catch renames
  3. Add integration test case for the new path to `GroupSessionMiddlewareIntegrationTests`
- Test coverage: `GroupSessionMiddlewareIntegrationTests.cs` covers all current paths; extend when adding exempt paths

**AutoMapper navigation property circular recursion:**
- Files: `QuestBoard.Repository/Automapper/EntityProfile.cs` (FollowUpQuest shallow mapping), `QuestBoard.Service/Automapper/ViewModelProfile.cs` (enum conversions)
- Why fragile: Mapping navigation properties to lazy-loaded related entities can trigger infinite recursion or serialization failures. `OriginalQuest` and `FollowUpQuest` use shallow mapping (new Quest { Id, Title }) to avoid the issue.
- Safe modification: When adding new navigation properties to domain models:
  1. Do NOT use `.ForMember(m => m.NavProperty, cfg => cfg.MapFrom(...) with recursive entity load)` — this will cause circular mapping
  2. Use shallow mapping: `cfg.MapFrom(src => src.NavProperty == null ? null : new Quest { Id = src.NavProperty.Id, Title = src.NavProperty.Title })`
  3. Add a unit test `AutoMapperCircularReferenceTests` to verify the pattern works without stackoverflow
- Test coverage: Phase 34.2 added `EntityProfileEnumCastTests` to catch enum-conversion bugs; extend for navigation patterns

**IActiveGroupContext null handling in SuperAdmin context:**
- Files: `QuestBoard.Domain/Extensions/ActiveGroupContextExtensions.cs` (RequireActiveGroupId method), all Phase 34.3 migration sites
- Why fragile: SuperAdmin has `ActiveGroupId == null` by design, but many code paths assume a non-null group context. The `RequireActiveGroupId()` guard throws on null, intentionally — but if called in a SuperAdmin-accessible code path without checking `User.IsInRole("SuperAdmin")` first, it crashes at runtime.
- Safe modification: When adding a new controller action or domain service call:
  1. If the action is Admin-only (policy-protected), check `User.IsInRole("SuperAdmin")` before calling `RequireActiveGroupId()`
  2. Alternately, use `GetEffectiveGroupRoleAsync(User, groupId)` which internally short-circuits SuperAdmin and never accesses groupId
  3. Add an integration test that invokes the action as SuperAdmin and verifies no null-dereference occurs
- Test coverage: Phase 34.3 added comprehensive SuperAdmin integration tests; extend when adding new SuperAdmin-accessible actions

**Hangfire job group-context setup:**
- Files: `QuestBoard.Service/Services/HangfireJobHelper.cs` (scope/group-context helper), all 3 job classes
- Why fragile: Each Hangfire job must call `HangfireJobHelper.RunInScopeAsync(scopeFactory, groupId, ...)` to set up `IActiveGroupContext` with the correct group before querying. If a new job is added without this setup, queries will silently return wrong-group results or empty sets due to Global Query Filters.
- Safe modification: When adding a new Hangfire job:
  1. Inject `IServiceScopeFactory` in constructor
  2. Wrap all repository/service calls in `HangfireJobHelper.RunInScopeAsync(scopeFactory, groupId, sp => { ... })`
  3. Add a unit test `[Fact] public async Task ExecuteAsync_RespectsTenantIsolation()` — verify the job respects global query filters and only processes the specified groupId's data
- Test coverage: `DailyReminderJobTests`, `SessionReminderJobTests`, etc. all test tenant isolation; extend pattern to new jobs

**Dockerfile multi-stage build cache invalidation:**
- Files: `Dockerfile` (lines 1–41), `.github/workflows/docker-publish.yml`
- Why fragile: BuildKit cache mount at line 16 persists NuGet package cache across builds. If a `csproj` file adds a new dependency or updates an existing one, the cache should be invalidated to fetch the new version. BuildKit cache-bust keys are automatic (based on `dotnet restore` command hash), but if dependency manifests are not hashed correctly, stale packages could be used.
- Safe modification: When updating NuGet dependencies:
  1. Run `dotnet restore` locally to verify new versions are fetched
  2. Run Docker build with `--no-cache` flag once to force cache invalidation: `docker build --no-cache -t questboard .`
  3. Verify the build output shows "Downloading [package]..." for new/updated packages, not "Restoring from cache..."
- Test coverage: CI pipeline (docker-publish.yml) runs every release tag; local testing can trigger cache-bust as needed

---

## Scaling Limits

**Email relay quota (Resend 3000/month):**
- Current capacity: 3000 emails/month Resend relay limit, 17 members
- Usage: ~50 emails/month (5 quests * 1 finalized-email + 1 reminder-email per quest, manually triggered + automated). Headroom: 2950/month unused.
- Limit: 3000 emails/month is a hard Resend API rate limit. Hitting it suspends all email delivery until quota resets.
- Scaling path: Monitor actual email volume (`ResendStatsAggregator` dashboard at `/admin/email-stats`). If volume exceeds 2500/month:
  1. Implement digest batching (EMAIL-04/REMIND-02) — combine same-day-quest reminders into single emails
  2. Consider tiered Resend plan upgrade from $20/month (3000 limit) to higher tiers
- Trigger for action: Implement automated alert when monthly volume exceeds 2000; escalate at >2500

**Quest/signup table growth:**
- Current state: ~50 quests, ~17 characters, 1 group (post-v5.0)
- Scaling limit: No hard row-count limit in current schema, but composite index `Quests(IsFinalized, FinalizedDate)` added in Phase 34.2 becomes more critical above ~10K quests.
- Improvement path at 10K+ quests: Archive completed quests to a separate `ArchivedQuests` table; implement archival job (monthly, runs after month-end).
- Priority: None — 50 quests represents ~1 year of operation; at current cadence (< 1 quest/week), 10K mark is 200 years away

**Session state table growth:**
- Current state: `AspNetSessionState` table grows by one row per session (distributed cache backing). Default expiry: 20 minutes (ASP.NET Core session default).
- Scaling limit: `AddDistributedSqlServerCache` auto-purges expired rows every 30 minutes (SqlServerCache internals). No manual cleanup needed.
- Capacity: No performance impact observed; schema validated in Phase 33 integration tests.
- Monitor: If session volume exceeds 100K concurrent sessions (never at 17 members), review cache eviction policy and consider external Redis

**Hangfire job queue:**
- Current state: 1 background worker (Hangfire configuration in Program.cs)
- Scaling limit: Single worker processes jobs sequentially. At current volume (50 jobs/month), latency is zero. If volume hits 100+ jobs/day, job processing could lag.
- Improvement path: Increase worker count via `UseHangfireServer(options => options.WorkerCount = N)` in Program.cs. Monitor queue depth via Hangfire dashboard.
- Trigger for action: If Hangfire dashboard shows >10 jobs queued at any time, increase worker count or implement job priority tiers

---

## Dependencies at Risk

**Resend SMTP relay dependency:**
- Package: Postfix MTA → Resend SMTP relay (no NuGet package, just email transport)
- Risk: Resend changes SMTP credentials, changes IP whitelisting, or deprecates the relay endpoint.
- Impact: All email delivery breaks (session reminders, password resets, confirmations).
- Current mitigation: Credentials are environment-configured; swapping Resend for another relay requires only `.env` update. SMTP client is framework-only (`System.Net.Mail.SmtpClient`), no vendor lock-in.
- Migration plan: If Resend becomes unavailable, implement `IEmailService` adapter for SendGrid, Mailgun, or other SMTP relay. No code changes needed beyond `EmailService.cs` implementation.
- Status: Low risk — widely-used relay service with good track record; no immediate action needed

**Hangfire SQL Server dependency:**
- Package: Hangfire.SqlServer 1.8.23
- Risk: Hangfire 1.8.x is no longer receiving updates; version 2.0 is in development. SQL Server storage format may drift.
- Impact: Jobs stored in SQL Server become unserializable on Hangfire version upgrade; queued jobs are lost.
- Current mitigation: Job state is serialized as JSON; no complex .NET type serialization used. Migration from 1.8 → 2.0 should be straightforward.
- Migration plan: When Hangfire 2.0 is released: 1) test upgrade on staging with >0 queued jobs, 2) run job re-serialization sweep, 3) deploy. Alternatively, stick with 1.8.x long-term (no functional gaps at current usage).
- Status: Moderate risk — plan upgrade when Hangfire 2.0 stabilizes, not urgent

**Microsoft.AspNetCore.Identity UI 10.0.9:**
- Package: Microsoft.AspNetCore.Identity.UI (5-year LTS support, .NET 10 LTS)
- Risk: ASP.NET Core 10 goes out of support Nov 2026 (18 months from now). Staying on an out-of-support version exposes to unpatched CVEs.
- Impact: Security vulnerabilities in ASP.NET Core runtime are not fixed; identity provider is vulnerable.
- Current mitigation: .NET 10 is still in LTS support window (through Nov 2026). Plan a migration to .NET 12 LTS (next LTS release, expected ~Nov 2024, likely pushed to 2025).
- Migration plan: When .NET 12 LTS stabilizes, test upgrade on staging (.NET 12 has strong backwards compatibility with .NET 10). No code changes expected; mostly NuGet package version bumps.
- Status: Low urgency — 18 months until .NET 10 goes EOL; revisit in 2025-H2

**EF Core Global Query Filters stability:**
- Package: Microsoft.EntityFrameworkCore 10.0.9
- Risk: Global Query Filters are a stable EF Core feature (since v2.0), but complex filter logic can regress on EF Core major version upgrades.
- Impact: Tenant isolation filter on Quests/ShopItems could behave unexpectedly on upgrade, risking data leakage across groups.
- Current mitigation: Phase 34.2 added "Documentation-only notes for query filter enforcement" and Phase 34.2-05 added integration tests `GroupQueryFilterEnforcementTests` to verify filter correctness.
- Test coverage: Regression tests must always accompany EF Core major version upgrades.
- Status: Low risk — filter logic is simple (equality on GroupId); no complex branching or subqueries

---

## Test Coverage Gaps

**GroupSessionMiddleware POST-redirect behavior:**
- What's not tested: The POST redirect-body-loss scenario (request expires mid-form-submission) is not testable in unit tests (requires timing of session expiry). Integration tests cover happy-path redirect but not the 409 Conflict response that should replace the redirect.
- Files: `QuestBoard.IntegrationTests/Controllers/GroupSessionMiddlewareIntegrationTests.cs`
- Risk: Without a fix test, the POST-body-loss bug could regress if the middleware is refactored. Integration test should verify: 1) GET request → 302 redirect to picker, 2) POST request with expired session → 409 Conflict (once implemented).
- Priority: Medium — add test *before* implementing the fix to prevent regression

**SuperAdmin-specific authorization bypass scenarios (corrected — Phase 55):**
- Superseded: This entry previously asserted that a SuperAdmin querying `/quests` with no active group should list all quests across all groups (via `IgnoreQueryFilters`), treating a null-group "show everything" merge as intended behavior. That expectation was the confirmed root cause of a cross-tenant quest leak — a SuperAdmin with a null active group reached `/quests` and saw every group's quests merged together, because `GroupSessionMiddleware` blanket-bypassed the "must have an active group" gate for the SuperAdmin role on every route.
- Corrected behavior: SuperAdmin is now gated on group-scoped routes exactly like every other role — a null active group redirects to `/groups/pick` (GET/HEAD) or returns 409 Conflict (non-idempotent verbs), the same as any other authenticated user. SuperAdmin's genuine group-agnostic workflows (the group picker, account management, platform-wide administration) remain exempt via the middleware's exempt-path list, which now runs before any role check.
- Files: `QuestBoard.Service/Middleware/GroupSessionMiddleware.cs`, `QuestBoard.IntegrationTests/Controllers/GroupSessionMiddlewareIntegrationTests.cs`
- Status: Closed — the fail-open bypass is removed and regression-tested (SuperAdmin redirect on `/quests`, `/Calendar`, `/DungeonMaster/EditProfile`, `/QuestLog`).

**Email configuration secret logging:**
- What's not tested: Email settings (SMTP password, Resend API token) are never logged in exception traces or debug output. Current tests mock ILogger but don't verify log content.
- Files: `QuestBoard.UnitTests/Services/EmailServiceTests.cs`, `QuestBoard.IntegrationTests/Services/ResendStatsClientTests.cs`
- Risk: A future developer refactoring email service could accidentally stringify EmailSettings or add debug logging, leaking secrets.
- Priority: Medium — add a test that verifies `logger.LogError()` calls never receive EmailSettings objects; use a custom LogCapture spy to inspect log messages

**AutoMapper enum-cast round-trip integrity:**
- What's not tested: AutoMapper enum conversions (e.g., `DndClass.Wizard` → 3 → `DndClass.Wizard`) are tested for successful mapping but not for exhaustiveness. An enum value added to source but not to target could silently cast to wrong type.
- Files: `QuestBoard.UnitTests/Services/EntityProfileEnumCastTests.cs` (added Phase 34.2)
- Risk: Low — only 8 enums in codebase, all small (<20 values). But if new large enums are added, silent cast bugs could occur.
- Priority: Low — current test coverage adequate; extend when new enums are introduced

**Tenant isolation under edge cases:**
- What's not tested: Queries with `.IgnoreQueryFilters()` (cross-group queries in `QuestRepository.GetAllQuestsAsync()`) are tested for correctness, but no test verifies that a normal (non-SuperAdmin) user cannot somehow bypass the filter via request manipulation.
- Files: `QuestBoard.IntegrationTests/` (integration tests suite)
- Risk: If a Hangfire job or admin action accidentally uses `.IgnoreQueryFilters()` without validating SuperAdmin role, a normal Admin/Player could see other groups' data.
- Priority: Medium — add a negative test: `[Fact] public async Task NormalAdmin_CannotBypassGroupFilter_ViaIgnoreQueryFilters()` which verifies QueryFilter is still active for non-SuperAdmin queries (via controller action that attempts to list all groups' quests).

**Docker healthcheck verification:**
- What's not tested: `docker-compose.yml` defines a healthcheck for the questboard service (lines 21–26) with `/health` endpoint. No test verifies the endpoint responds correctly or that the container is marked healthy after startup.
- Files: `docker-compose.yml`, `QuestBoard.Service/Program.cs` (health endpoint configuration)
- Risk: A future refactor of health-check endpoint logic could break the endpoint without detection. Container orchestrators rely on this endpoint to know when service is ready.
- Priority: Medium — add a Docker integration test: `[Fact] public async Task DockerHealthcheck_RespondsWithOk()` that verifies HTTP `GET /health` returns 200 OK. Consider adding a startup smoke test to CI (docker-publish.yml) that runs container and hits the endpoint before declaring success.

