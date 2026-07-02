# Codebase Concerns

**Analysis Date:** 2026-07-01

## Tech Debt

### Large Controller Files — Monolithic Quest Management

**Issue:** `QuestController.cs` is 896 lines, containing quest creation, management, finalization, follow-up creation, and complex error handling in a single class.

**Files:** `QuestBoard.Service/Controllers/QuestBoard/QuestController.cs`

**Impact:** 
- Difficult to test individual features (mixed concerns: quest CRUD, signup management, email dispatch)
- Hard to navigate; Create/Edit/Finalize/FollowUp all share similar patterns but duplicated
- Follow-up quest logic is fragile (two-phase update with manual rollback on error — see lines 854–891)

**Fix approach:**
- Extract follow-up quest creation into a dedicated controller action or service wrapper
- Consider breaking into focused sub-controllers: `QuestManagementController` (create/edit), `QuestFinalizationController` (finalize/reminder), `QuestFollowUpController` (follow-up creation)
- Reduce quest controller to <500 lines by delegating business logic entirely to services

### AdminController Size and Multi-Concern Design

**Issue:** `AdminController.cs` is 424 lines, mixing user management, email stats, Resend API integration, email previews, and quest operations.

**Files:** `QuestBoard.Service/Controllers/Admin/AdminController.cs`

**Impact:**
- Hard to test; email stats fetching with external HTTP client is embedded in user management logic
- Resend API key handling requires manual per-request Bearer token injection (line 151 comment "Authorization header is NOT set here")
- Email preview testing shares controller space with production admin operations

**Fix approach:**
- Extract Resend stats into a dedicated `ResendStatsController` or service wrapper
- Move email preview into `EmailPreviewController` (already separated but not fully isolated)
- Reduce AdminController to user/role management only

### DateTime.Now Usage in ShopSeedService

**Issue:** `ShopSeedService.cs` uses `DateTime.Now` (local time, not UTC) for seeding shop item availability dates.

**Files:** `QuestBoard.Domain/Services/ShopSeedService.cs` lines 223–224

**Impact:**
- Inconsistent with rest of codebase (all other timestamps use `DateTime.UtcNow`)
- Seed dates will be wrong if deployment host timezone differs from dev machine
- Makes it ambiguous whether seeded availability windows are server-local or UTC

**Fix approach:**
- Replace both `DateTime.Now` calls with `DateTime.UtcNow`
- Document timezone handling: production server is CET/CEST; quest finalized dates and reminder job use server-local time intentionally, but all transactional timestamps should be UTC

### Manual Cleanup on Quest Deletion (NoAction Cascade)

**Issue:** `RemoveAsync` in `QuestService.cs` (lines 87–102) manually deletes PlayerSignups before deleting the Quest because Quest→PlayerSignup FK uses `NoAction` to avoid cascade cycles.

**Files:** `QuestBoard.Domain/Services/QuestService.cs`, `QuestBoard.Repository/Entities/QuestBoardContext.cs` line 64

**Impact:**
- Fragile: if someone forgets to call `RemoveAsync` and uses repository directly, orphaned PlayerSignups will block quest deletion
- DateVotes still cascade-delete from PlayerSignups (correct), but the service layer has to remember the order
- No database-level protection if the pattern is bypassed

**Fix approach:**
- Add a comment above the FK configuration in `QuestBoardContext.cs` explaining why NoAction is used (SQL Server prevents cascade delete cycles)
- Add integration test specifically for quest deletion with active player signups to prevent regression
- Consider adding a repository-level guard: `DeleteQuestAsync(id)` that enforces cleanup order internally

### Two-Phase Follow-Up Quest Update with Rollback

**Issue:** `QuestController.CreateFollowUp` (lines 854–891) creates a quest shell, then updates it with proposed dates. If the update fails, it attempts to delete the orphaned shell.

**Files:** `QuestBoard.Service/Controllers/QuestBoard/QuestController.cs` lines 854–891

**Impact:**
- Rollback may fail silently if the delete itself errors (exception not re-raised, only logged implicitly)
- Between shell creation and update, a race condition is theoretically possible if another action reads the incomplete quest
- The orphan cleanup happens in the controller, not the service layer, so it can be easily forgotten in future refactors

**Fix approach:**
- Move entire two-phase operation into a service method `CreateFollowUpQuestWithDetailsAsync(originalQuestId, title, description, proposedDates, ...)` that handles both creation and update in a single transaction
- Add explicit logging on rollback failure so errors don't silently disappear
- Add integration test that covers update failure scenario to ensure cleanup is tested

---

## Known Bugs

### Email Settings Not Validated on Startup

**Issue:** `EmailService.cs` (line 16–20) logs a warning and returns null if `FromEmail` is not configured, but the application continues. Hangfire jobs will then fail silently.

**Files:** `QuestBoard.Domain/Services/EmailService.cs` lines 16–20

**Symptoms:** 
- Quest finalization, password reset, welcome emails all fail if SMTP config is missing
- No error visible in Hangfire dashboard (job completes but email was never sent)
- Admins only discover the issue when users report not receiving emails

**Workaround:** Check `appsettings.json` Email section manually during deployment setup

**Fix approach:**
- Add startup validation in `Program.cs`: throw if `Email:FromEmail` or `Email:SmtpServer` are empty in production environment
- Document required email config in deployment guide

### Nullable Navigation Property in PlayerSignup Causes Null Dereference Risk

**Issue:** `SessionReminderJob.cs` line checks `if (quest == null)` but does not guard against null `quest.DungeonMaster` in subsequent usage.

**Files:** `QuestBoard.Service/Jobs/SessionReminderJob.cs`

**Symptoms:**
- If a quest's DungeonMaster is deleted before the session reminder fires, the job will crash with NullReferenceException
- Hangfire will retry the job (default: 10 times), consuming retries without alerting admin

**Workaround:** None; depends on data integrity constraint (DM row must not be deleted if quest exists)

**Fix approach:**
- Add explicit null check: `if (quest?.DungeonMaster == null) { logger.LogWarning(...); return; }`
- Add database-level FK constraint ON DELETE behavior review (currently `NoAction` for DungeonMaster FK)

### Resend API Rate Limiting and Pagination Not Handled

**Issue:** `AdminController.GetResendStatsAsync` (referenced in STATE.md, line 96) retrieves email stats from Resend API with no pagination loop and no rate-limit retry logic.

**Files:** Resend HTTP client usage in `AdminController.cs` (implementation details in STATE.md concern note)

**Impact:**
- Stats dashboard shows incomplete data if Resend API returns multiple pages
- Network errors or rate limits (429 Too Many Requests) will cause dashboard to error instead of retrying

**Fix approach:**
- Implement pagination loop: `while (pageCount < totalPages)` to fetch all pages
- Add retry-with-backoff for 429 responses (e.g., exponential backoff: 1s → 2s → 4s)
- Cache results for 5 minutes (already done per STATE.md, but document TTL)

---

## Security Considerations

### No CSRF Token Validation on Some State-Changing Actions

**Issue:** Most POST actions carry `[ValidateAntiForgeryToken]`, but some administrative actions (e.g., role changes in `AdminController`) may skip validation if not carefully reviewed.

**Files:** `QuestBoard.Service/Controllers/Admin/AdminController.cs` (lines 55–93 carry `[ValidateAntiForgeryToken]` — currently safe)

**Current mitigation:** All role-change and user-edit actions in `AdminController` have `[ValidateAntiForgeryToken]`. Policy-based authorization (`[Authorize(Policy = "AdminOnly")]`) provides defense-in-depth.

**Recommendations:**
- Code review before adding new POST actions to ensure anti-forgery token is always present
- Add a test that verifies all state-changing actions have the attribute

### Email Configuration Secrets Potentially Logged

**Issue:** `EmailService.cs` logs warnings if email is not configured, but does not guard against sensitive values (SMTP password) being logged in exception traces.

**Files:** `QuestBoard.Domain/Services/EmailService.cs`, `QuestBoard.Service/Controllers/Admin/AdminController.cs` (email preview feature)

**Current mitigation:** Email settings read from `appsettings.json` (safe in deployed environment via env var override), not hardcoded. Exception logging does not explicitly log settings.

**Recommendations:**
- Ensure email config never leaks into structured logging; use dedicated `_logger.LogError(ex, "Email send failed")` without including settings in the message
- Avoid logging the full exception message if it contains connection strings

### Resend API Token in HttpClient Default Headers Not Recommended

**Issue:** `Program.cs` (lines 150–157) creates a named HttpClient for Resend API but does NOT set the Authorization header (per comment). Instead, it's added per-request in `AdminController`.

**Files:** `QuestBoard.Service/Program.cs` lines 150–157, `QuestBoard.Service/Controllers/Admin/AdminController.cs` line 151 (comment)

**Current mitigation:** Token is correctly stored in `appsettings.json` and retrieved per-request; no hardcoded or default header leaks.

**Recommendations:**
- Document this pattern in code comment: "Bearer token must be added per-request in AdminController, not set globally in Program.cs, because each request may need different scoping or the service may change."
- Consider centralizing token retrieval in a dedicated `ResendApiClient` class to avoid duplication if stats retrieval is called from multiple places in the future

---

## Performance Bottlenecks

### No Database Indexing on Session Reminder Queries

**Issue:** `GetQuestsForTomorrowAllGroupsAsync` (called by `DailyReminderJob.cs`) does a full table scan of Quests to find quests finalized for tomorrow.

**Files:** `QuestBoard.Repository/QuestRepository.cs`, `QuestBoard.Service/Jobs/DailyReminderJob.cs` line 23

**Cause:** 
- Query filters on `IsFinalized = true` and `FinalizedDate = tomorrow` (a computed range)
- No composite index on `(IsFinalized, FinalizedDate)`
- EF Core Global Query Filter for group scoping may prevent index optimization

**Improvement path:**
- Add composite index: `CREATE INDEX IX_Quests_Finalized_Date ON Quests(IsFinalized, FinalizedDate)` 
- Benchmark: compare query time on 10K+ quests with and without index
- Monitor slow log if job execution time increases as quest count grows

### Shop Item Queries Not Optimized for Large Inventory

**Issue:** `GetPublishedItemsAsync` and other shop queries load full ShopItem entities without projection. Images are stored as BLOB.

**Files:** `QuestBoard.Repository/ShopRepository.cs`

**Cause:** 
- Character images and DM profile images stored as `byte[]` in database
- No lazy-loading or explicit .Include() control — risk of N+1 query
- Large image BLOBs loaded unnecessarily for list views that only need Name/Price/Type

**Improvement path:**
- Add optional `.ThenInclude(si => si.Images)` only for detail views; list views exclude images
- Or: Extract images into separate `ShopItemImage` table with explicit loading
- Benchmark shop page load time on production data; if <100ms, no action needed

### Hangfire Job Queue Not Filtered by Group

**Issue:** `DailyReminderJob` fetches quests from ALL groups, then enqueues a `SessionReminderJob` per quest (including GroupId). If there are 500 groups with 10 quests each, the job enqueues 5000 items every morning.

**Files:** `QuestBoard.Service/Jobs/DailyReminderJob.cs` line 23, `QuestBoard.Repository/QuestRepository.cs`

**Impact:** 
- Hangfire job queue grows linearly with group count
- No sharding or batching; single daily job does all work
- Risk of slowdown when deployed to multi-tenant customers with many groups

**Improvement path:**
- Consider breaking DailyReminderJob into a recurring job per group (if number of groups is manageable, <100)
- Or: Implement batching — enqueue a single `SessionReminderBatchJob` that processes all quests in one transaction instead of N separate jobs
- Monitor Hangfire dashboard queue length as group count increases; if queue >1000 items, implement batching

**Status (Phase 34.2):** documented as deferred, not implemented — see ARCHITECTURE.md.

---

## Fragile Areas

### Hangfire Job Execution Context Requires Manual Service Scope Management

**Issue:** Every Hangfire job (e.g., `DailyReminderJob`, `SessionReminderJob`) manually creates an `IServiceScopeFactory.CreateAsyncScope()` because scoped services cannot be constructor-injected into background jobs.

**Files:** `QuestBoard.Service/Jobs/DailyReminderJob.cs` line 20, and similar in all other job files

**Why fragile:** 
- If a new service is added and injected into a job without using `IServiceScopeFactory`, the job will fail at runtime
- No compiler check; pattern is easy to forget
- Scopes are created but not always explicitly disposed (relying on `await using`)

**Safe modification:**
- Always use `await using var scope = scopeFactory.CreateAsyncScope()` pattern; never constructor-inject scoped services
- Document this in a job base class or README, not just comments scattered in each file
- Add a static job helper class: `public static class HangfireJobHelper { public static async Task RunInScopeAsync(IServiceScopeFactory factory, Func<IServiceProvider, Task> action) }` to centralize the pattern

**Test coverage:** 
- Integration tests verify jobs execute without error, but don't test scope lifecycle
- No test breaks if scope management is accidentally broken in a new job

### Query Filter Application Inconsistent Across Entity Types

**Issue:** Global Query Filters are applied to `QuestEntity` and `ShopItemEntity` only, but NOT to `UserEntity`, `CharacterEntity`, or `PlayerSignupEntity`.

**Files:** `QuestBoard.Repository/Entities/QuestBoardContext.cs`, `QuestBoard.Domain/Interfaces/IActiveGroupContext.cs`

**Why fragile:**
- New code that assumes all entities are filtered by group will silently leak data if the filter is not applied
- Example: if someone adds a query `Characters.Where(c => ...)` without checking the model configuration, they will get all characters across all groups
- Different entities have different query requirements (UserEntity must NOT be filtered because Identity queries rows globally)

**Safe modification:**
- Add a comment in `OnModelCreating` before each HasQueryFilter call explaining why it applies to that entity
- Add a comment above entities that are NOT filtered explaining why (e.g., "UserEntity excludes filter because Identity framework queries all users globally for login")
- Document the rule in CONVENTIONS.md: "Global Query Filters must be explicitly enabled per entity type; check `QuestBoardContext.OnModelCreating` before querying a new entity type."

**Test coverage:**
- Integration tests verify quest and shop item queries are group-scoped
- No test verifies that characters are NOT group-scoped (if a filter is accidentally added, test won't catch it)

### Manual Enum Casting at AutoMapper Boundaries

**Issue:** `EntityProfile.cs` casts enums between `int` storage and domain enum types at the automapper boundary (e.g., `(int)src.Type`, `(SignupRole)src.SignupRole`).

**Files:** `QuestBoard.Repository/Automapper/EntityProfile.cs` lines 47, 50, 61, 64, 70–77, 82–85, 89–90, 107–110, 125, 129

**Why fragile:**
- If an enum value is added (e.g., `ItemType.Artifact = 10`) but the database still has old rows with `Type = 5`, the cast will silently truncate or wrap to an undefined enum value
- No validation that the int value is a valid enum member
- If an enum is reordered (e.g., `Common = 0` becomes `Common = 1`), all existing data becomes corrupted on read

**Safe modification:**
- Add a note in CONVENTIONS.md: "Enums must not be reordered and new values must be appended. Document any enum change in migration notes."
- Consider adding explicit validation in the mapping: `(ItemType)Enum.Parse(typeof(ItemType), src.Type.ToString())` (slower but safer; may not be necessary if data integrity is maintained)
- Add a data validation test: fetch a sample record and verify enums deserialize correctly

**Test coverage:**
- Unit tests mock enum conversions; they don't test against real database data with edge-case enum values
- No test covers "what happens if an old enum value is in the database?"

### Missing GroupId on Historical Hangfire Job Data

**Issue:** Hangfire job queue, job history, and recurring job configuration do not currently track GroupId explicitly. Jobs resolve it from the `activeGroupContext.ActiveGroupId` at runtime (e.g., line 36 in `DailyReminderJob`).

**Files:** `QuestBoard.Service/Jobs/DailyReminderJob.cs` line 35–36, `QuestBoard.Service/Program.cs` lines 297–300

**Why fragile:**
- If an app restart happens during job execution, the active group context will be reset to null or default
- Historical job data in Hangfire SQL Server table will not show which group was processed (debugging is harder)
- No audit trail if a job fails and you need to replay it for a specific group

**Safe modification:**
- Add GroupId as an explicit enqueued job parameter: `backgroundJobClient.Enqueue<SessionReminderJob>(job => job.ExecuteAsync(quest.Id, quest.GroupId, false, false, CancellationToken.None))`  — this is already done (line 36 shows `quest.GroupId` is passed)
- Ensure all job enqueue calls include GroupId explicitly, not relying on context
- Document that Hangfire dashboard shows GroupId in each job's arguments for audit purposes

---

## Scaling Limits

### EF Core Global Query Filter Scoping at Runtime

**Issue:** Global Query Filters on Quests and ShopItems are applied based on `activeGroupContext.ActiveGroupId` at query time. If this value is null or incorrect, the filter either includes all data or excludes everything unexpectedly.

**Files:** `QuestBoard.Repository/Entities/QuestBoardContext.cs`, `QuestBoard.Domain/Interfaces/IActiveGroupContext.cs`, `QuestBoard.Service/Program.cs` lines 175–177

**Current capacity:** Works for single-tenant and small multi-tenant deployments (<100 groups) because GroupId is simple int lookup

**Limit:** 
- If ActiveGroupId resolution becomes async or non-deterministic (e.g., user holds roles in multiple groups and the picker is not enforced), queries will silently scope wrong data
- No compile-time safety; wrong group scope is a runtime data-integrity bug

**Scaling path:**
- Phase 30 (Group Picker Enforcement) must ensure SessionKeys.ActiveGroupId is always set before any data query
- Add assertion in Repository: `if (activeGroupContext.ActiveGroupId == null) throw InvalidOperationException("Group context not initialized")` — converts silent data leaks to explicit errors
- Monitor logs for "Group context not initialized" errors in production; if any appear, Phase 30 enforcement was not sufficient

### Hangfire Job Retry Limit on Transient Failures

**Issue:** Hangfire jobs (e.g., email send failures) retry up to 10 times by default. If a job consistently fails (e.g., SMTP down for 1 hour), it will consume 10 retries and then be moved to a Failed Jobs queue where it sits without further retry.

**Files:** All job files in `QuestBoard.Service/Jobs/`, `QuestBoard.Service/Program.cs` lines 186–205 (Hangfire config)

**Current capacity:** Works if SMTP/Resend outages are <10 minutes. For longer outages, admin must manually retry jobs from dashboard.

**Limit:**
- No exponential backoff configured (default immediate retry for each attempt)
- No alert if multiple jobs land in Failed Jobs queue (symptoms go unnoticed)

**Scaling path:**
- Add retry policy in Hangfire config: `UseAutoRetry(5)` with exponential backoff (1s, 2s, 4s, 8s, 16s)
- Set up monitoring/alerting in production: if Failed Jobs queue has >10 items, send alert to admin
- Document admin manual recovery process: Hangfire dashboard → Failed Jobs → Requeue

### No Tenant Isolation Enforcement at API Boundary

**Issue:** Controllers accept user input (e.g., `QuestController.Details(questId)`) and load data filtered by activeGroupId. If activeGroupId is null or a user manually manipulates the URL to access `questId = 999` from a different group, the filter may not protect.

**Files:** All controllers in `QuestBoard.Service/Controllers/`, Query filter in `QuestBoardContext.cs`

**Current capacity:** Works because activeGroupId is set per request via session and group picker enforces selection

**Limit:**
- If Phase 30 Group Picker Enforcement misses a code path, a user can access data from unassigned group
- Global Query Filter is a safety net, not a primary security boundary

**Scaling path:**
- Add explicit authorization check in each controller: `if (quest.GroupId != activeGroupContext.ActiveGroupId) return Forbid()`
- Or: Create a service wrapper: `IAuthorizedQuestRepository` that enforces GroupId match in every Get method
- Add comprehensive test: for each controller action that loads an entity, verify it returns Forbid() if the entity's GroupId differs from activeGroupId

**Status (Phase 34.2):** documented as deferred, not implemented — see ARCHITECTURE.md.

---

## Dependencies at Risk

### SMTP/Email Configuration Tightly Coupled to ASP.NET Core Identity Email Sender

**Issue:** Email sending is implemented via `EmailService` which uses `SmtpClient`. ASP.NET Core Identity (password reset, email confirmation) may attempt to use a default email sender if not explicitly wired.

**Files:** `QuestBoard.Domain/Services/EmailService.cs`, `QuestBoard.Service/Program.cs` lines 44–63 (Identity config)

**Risk:** 
- Identity might attempt to send emails through a different path if email sender is not registered correctly
- Hangfire jobs send emails directly via EmailService, but controllers may use Identity's built-in email sender for password reset

**Migration plan:**
- Ensure all identity email operations (password reset, email confirmation) are routed through `IEmailService` via a custom `UserManager` override or email sender middleware
- Document current flow: Password reset email → handled by `ForgotPasswordEmailJob` via Hangfire (not Identity's built-in sender)
- Test: verify that a password reset request enqueues a job, not tries to send via unconfigured Identity sender

### Resend SMTP Relay as Single Point of Failure

**Issue:** Email delivery depends on Resend SMTP relay. If Resend is down, all email notifications fail.

**Files:** `QuestBoard.Domain/Services/EmailService.cs` line 22 (SmtpClient connection), `QuestBoard.Service/Program.cs` line 153 (HttpClient for Resend stats)

**Risk:** 
- No fallback SMTP server configured
- No retries with exponential backoff in `SendAsync` (Hangfire provides job retries, but SmtpClient throws immediately on connection failure)
- Resend API for stats is HTTP-only; if Resend API is down, the stats dashboard will fail

**Migration plan:**
- Add secondary SMTP relay config (fallback): if primary fails, retry with secondary
- Or: Configure multiple SmtpClient instances in a round-robin: try server1, then server2, then fail
- For stats: wrap Resend API calls in try-catch; return cached data if API is unavailable (currently cached for 5 min anyway)
- Monitor: if SessionReminder emails start failing, check Resend status page and SMTP logs on host

---

## Missing Critical Features

### No Digest Batching for Same-Day Quests

**Issue:** Session reminders and finalized quest emails are sent individually per quest. If a player signed up for 3 quests on the same day, they receive 3 separate emails.

**Files:** `QuestBoard.Domain/Services/QuestService.cs` line 35 (EnqueueFinalizedEmail per quest), `QuestBoard.Service/Jobs/SessionReminderJob.cs` (sends per quest)

**Blocking:** 
- Email spam for active players with multiple same-day quests
- No known production impact yet (same-day quests have never occurred in one year of operation — per STATE.md line 48)

**Fix approach:**
- Implement `BatchSessionReminders(playerId, reminders[])` that groups reminders by player
- Change `DailyReminderJob` to collect all reminder emails per player, then send one combined email
- Requires email template redesign to show multiple quests in one email
- **Deferred until multi-quest same-day sessions occur in practice**

### No Email Unsubscribe / Preference Management

**Issue:** All confirmed users receive all emails (session reminders, finalized quests, password resets). No opt-out mechanism.

**Files:** All job files that check `EmailConfirmed` before sending

**Blocking:**
- Not an issue for trusted small group (17 members); low risk of spam complaints
- Would become critical if the platform is deployed to larger external communities

**Fix approach:**
- Add `User.EmailPreferences` or `UserEmailSettings` entity (one row per user)
- For each email category, store bool flag: `OptOutSessionReminders`, `OptOutFinalizedQuests`, etc.
- Check flag before enqueuing job: `if (user.EmailPreferences?.OptOutSessionReminders == true) return;`
- Add settings UI in account page

---

## Test Coverage Gaps

### Hangfire Job Retry Behavior Not Tested

**Issue:** Hangfire jobs are tested to ensure they execute without error, but not tested for retry behavior when an upstream dependency (SMTP, database) fails.

**Files:** `QuestBoard.UnitTests/Services/SessionReminderJobTests.cs`, `QuestBoard.IntegrationTests/Controllers/QuestFinalizeTests.cs`

**What's not tested:**
- Job executes, catches exception, re-queues successfully
- Retry count increments correctly
- Failed jobs are moved to Failed Jobs queue after 10 retries

**Risk:** 
- A job could be retrying but never succeeding; issue only noticed if admin checks Hangfire dashboard regularly
- Code change could break retry logic without tests catching it

**Fix approach:**
- Add test: `SessionReminderJob_EmailSendFailure_RetriesCorrectly` that mocks `EmailService.SendAsync` to throw, verifies Hangfire records retry
- Add test: `SessionReminderJob_PersistentFailure_MovesToFailedQueue` that verifies job lands in Failed after 10 retries
- Requires test setup that fully simulates Hangfire (not just unit test of the job method)

### Group Query Filter Enforcement Not Tested

**Issue:** Global Query Filter on Quests and ShopItems is tested implicitly (integration tests pass), but no explicit test verifies that querying without setting activeGroupId returns no results.

**Files:** `QuestBoard.Repository/Entities/QuestBoardContext.cs`, test factory in `QuestBoard.IntegrationTests/WebApplicationFactoryBase.cs`

**What's not tested:**
- If a query is run with `activeGroupContext.ActiveGroupId = null`, does the filter exclude all rows?
- If a query is run with `activeGroupContext.ActiveGroupId = 999` (non-existent group), are results correctly empty?
- If a new entity type is added to the domain, does the developer remember to add a query filter?

**Risk:** 
- Data could leak across groups if someone forgets to apply a filter
- Silent data-integrity bug; tests pass, but multi-tenant deployments expose data

**Fix approach:**
- Add explicit test: `QueryFilterTests.GetQuests_NoActiveGroup_ReturnsEmpty` that sets activeGroupContext.ActiveGroupId to null, verifies no quests returned
- Add test: `QueryFilterTests.GetQuests_UnassignedGroup_ReturnsEmpty` that sets activeGroupContext.ActiveGroupId to 999, verifies no quests returned
- Add test for each filtered entity type (Quests, ShopItems)

### Follow-Up Quest Cleanup on Update Failure Not Tested

**Issue:** `QuestController.CreateFollowUp` (lines 854–891) attempts to rollback (delete) an orphaned quest if the follow-up update fails. This rollback code is not tested.

**Files:** `QuestBoard.Service/Controllers/QuestBoard/QuestController.cs` lines 854–891

**What's not tested:**
- If `UpdateQuestPropertiesWithNotificationsAsync` throws, does the orphaned quest get deleted?
- If the delete itself fails, does the exception propagate correctly?

**Risk:** 
- Orphaned incomplete quests could accumulate in the database if the rollback fails
- Users see an error but a partial quest was still created, causing confusion

**Fix approach:**
- Add integration test: `QuestController_CreateFollowUp_UpdateFailure_CleansUpOrphan` that mocks `UpdateQuestPropertiesWithNotificationsAsync` to throw, verifies the orphaned quest is deleted
- Add test: `QuestController_CreateFollowUp_DeleteFailure_PropagatesException` that mocks the delete to also fail, verifies exception is not swallowed

---

*Concerns audit: 2026-07-01*
