---
phase: 100-admin-and-platform-ui-conversion
plan: 07
subsystem: email-limits-and-stats-seam
tags: [rate-limiting, security, email-stats, integration-tests, test-seam]
requires: ["100-06", "100-05"]
provides:
  - "SendConfirmationEmail takes the per-target permit only after the membership, reach and already-confirmed checks"
  - "EditUser consults the limiter before any save; a refused email change writes nothing"
  - "Both refusals are a redirect to Users with the approved Error toast (AdminActionMessages.EmailRateLimited)"
  - "A per-administrator CreateUser limit of 20 per 60 minutes on its own keyed limiter (AdminRateLimitKeys.CreateUser), with the approved toast (AdminActionMessages.CreateLimited)"
  - "EmailSettings.ResendBaseUrl honoured only on a Development host, through ResendBaseAddress.Resolve"
  - "CannedResendHandler, EmailStatsWebApplicationFactory and EmailStatsRenderTests: the Email Stats statistics, API-error, not-configured and forced-refresh states rendered through the real client"
affects: [100-20]
tech-stack:
  added: []
  patterns: ["permit taken after every cheap refusal", "limiter before any write", "keyed singleton limiter next to the per-target limiter", "Development-gated address resolved in a small testable helper called from the named-client registration"]
key-files:
  created:
    - QuestBoard.Service/Services/ResendBaseAddress.cs
    - QuestBoard.Service/Services/AdminRateLimitKeys.cs
    - QuestBoard.IntegrationTests/Helpers/CannedResendHandler.cs
    - QuestBoard.IntegrationTests/Helpers/EmailStatsWebApplicationFactory.cs
    - QuestBoard.IntegrationTests/Tests/EmailStatsRenderTests.cs
  modified:
    - QuestBoard.Service/Controllers/Admin/AdminController.cs
    - QuestBoard.Service/Program.cs
    - QuestBoard.Domain/Models/EmailSettings.cs
    - QuestBoard.Service/ViewModels/AdminViewModels/AdminActionMessages.cs
    - QuestBoard.IntegrationTests/Controllers/AdminControllerRateLimitIntegrationTests.cs
    - QuestBoard.IntegrationTests/Tests/AdminPlatformConversionGuardTests.cs
decisions:
  - "O-4 toast: both refusals redirect to Users with an Error toast (toastRateLimit wording); the three 429 facts were rewritten to the same intent"
  - "O-5 limit: 20 per 60 minutes per administrator, a second keyed PartitionedRateLimiter<int> singleton; the per-target limiter registration is untouched"
  - "O-13 dev-setting: EmailSettings:ResendBaseUrl is read when the named client is configured and honoured only when the host environment is Development"
  - "The create permit is taken after model validation and the board check, before CreateOrAddToGroupAsync, so an invalid form never spends it"
status: complete
actuals:
  tokens: 21000
  tasks: 3
  commits: 3
---

# Phase 100 Plan 07: Email limits and the Email Stats seam Summary

The resend and email-change limiters now run in the right order and refuse with the approved Error toast instead of a bare 429, Create User has a per-administrator hourly limit, and the Email Stats statistics branch is rendered from canned data through the real client without any real key.

## Order of checks as built

**SendConfirmationEmail** (`AdminController.cs`): active board -> membership (`GetGroupRoleByIdAsync`) -> reach check -> load account -> already-confirmed check -> `emailResendLimiter.AttemptAcquire` (line 410) -> token, link, enqueue. The acceptance check: the `AttemptAcquire` call sits after the `GetGroupRoleByIdAsync` call of the same action (membership at line 390, lease at line 410). An out-of-board, refused or already-confirmed request no longer spends the target's budget.

**EditUser POST**: validation -> membership -> reach -> load account -> work out whether the email changes -> limiter (only when it does, and only for a non-empty new address) -> save name, key flag and email. A refused change writes nothing.

**CreateUser POST**: model validation -> active board -> per-actor limiter -> account work. The permit is spent only by a request that passed validation.

## Refusal contract (O-4 toast)

A limited resend and a limited email change answer `RedirectWithError(Users, AdminActionMessages.EmailRateLimited)`: "Too many emails were sent to this account recently. Try again in an hour." The sentence is the one approved at the audit checkpoint and names no role. A limited create answers "You have created many accounts in a short time. Try again later." Both sentences now live in `AdminActionMessages`.

## Create limit (O-5)

A second `PartitionedRateLimiter<int>` is registered as a keyed singleton (`AdminRateLimitKeys.CreateUser`), fixed window, 20 permits per 60 minutes, partitioned by the signed-in administrator's id (`create-user:{id}`), injected into `AdminController` with `[FromKeyedServices]`. The per-target limiter registration (key, count, window) is unchanged. Facts: the twenty-first create by one administrator answers the toast and leaves no account behind, the twentieth account exists, a different administrator is unaffected, and twenty invalid posts do not spend the budget.

## Capture seam (O-13 dev-setting)

`EmailSettings.ResendBaseUrl` (optional) is passed with the fixed address to `ResendBaseAddress.Resolve` when the named "Resend" client is configured (the `(sp, client)` overload, so the setting is read at resolution time, never at an earlier point where a host's settings may not be loaded). The helper returns the configured address only when `IHostEnvironment.IsDevelopment()` is true and the value is an absolute http or https address; everything else returns the fixed address. `https://api.resend.com/` is written once, in `Program.cs`. No settings file was edited.

Facts:
- A real Testing host built with the setting present resolves the named client to the fixed address, and the same host proves the setting reached it (`IOptions<EmailSettings>.ResendBaseUrl` is the configured value), so the fact cannot pass for the wrong reason.
- The default Testing host uses the fixed address.
- A theory over the helper pins Production, Testing and Staging with the setting present, Development with the setting missing, blank, not an address or not http(s) (all the fixed address), and Development with a valid address (the configured one).
- A source guard pins that `Program.cs` holds the fixed address once, reads the setting once, and only as an argument to the helper.

**Deviation recorded openly (the plan's own allowance): the Production host could not be started by the harness.** A probe host with `UseEnvironment("Production")` fails at startup with "Relational-specific methods can only be used when the context is using a relational database provider" (the Production path migrates a relational database). So the Production rule is pinned against the registration's decision (`ResendBaseAddress.Resolve` with a Production `IHostEnvironment`), which the registration calls, plus the real Testing-host fact. The probe file was deleted.

## Email Stats facts (`EmailStatsRenderTests`)

`EmailStatsWebApplicationFactory` sets `EmailSettings:ResendApiKey` to a stub through `UseSetting` and replaces the named client's primary handler through `ConfigurePrimaryHttpMessageHandler` with `CannedResendHandler` (which survives the client factory's handler disposal). Through the real `ResendStatsClient`, aggregator and view, as a SuperAdmin:
- canned records (1 sent, 3 delivered/opened/clicked, 2 bounced, 4 failed) render the four figures with their labels, the "Stats as of" line with a `local-time` element, and neither alert;
- the only request seen carries the stub key and goes to `https://api.resend.com/emails?limit=100` inside the handler (no network);
- a canned 500 renders the API-error alert and no figures;
- the default factory renders the not-configured alert;
- after the canned data changes, a plain read is served from the cache and `?force=true` shows the new figures.
Assertions read labels and numbers from the markup, never CSS.

## Renamed tests (also in the scratch `tests-renamed.md`)

- `SendConfirmationEmail_ExceedingRateLimit_ShouldReturn429` -> `SendConfirmationEmail_ExceedingRateLimit_IsRefusedWithAToast_AndSendsNothingMore` (fourth resend is a redirect with the Error toast, three mails queued)
- `SendConfirmationEmail_DifferentTargetUsers_ShouldHaveIndependentBudgets` -> `SendConfirmationEmail_DifferentTargetUsers_HaveIndependentBudgets` (three mails each; the fourth for A is the toast refusal)
- `EditUser_EmailChange_ExceedingRateLimit_ShouldReturn429` -> `EditUser_EmailChange_ExceedingRateLimit_IsRefusedWithAToast`

The two reproduction facts (out-of-board resends not spending the budget; a refused email change leaving the name unchanged) are enabled: `grep -c 'Skip = '` on the rate-limit test file prints 0. The refused-change fact now also asserts a redirect.

## Verification

- Task 1 verify (rate-limit, admin controller and reach classes): 55 passed, 0 failed.
- Task 2 verify: `ResendStats` unit facts 11 passed; integration filter (EmailStats, rate-limit, admin controller, guard) 49 passed, 0 failed.
- Full suites: unit 1613 passed, 0 failed, 0 skipped; integration 2824 passed, 0 failed, 28 skipped (2852 total; was 2804 passed and 30 skipped before this plan).
- `appsettings.json` and `appsettings.Development.json` unchanged since the plan start; `QuestBoard.Service/Views`, the calendar feed writer, the subscription service and the feed revision stamper unchanged since the plan start.
- `grep -c "https://api.resend.com/" QuestBoard.Service/Program.cs` prints 1; `ConfigurePrimaryHttpMessageHandler` is in the new factory.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] Production host cannot start in the harness**
- **Found during:** Task 2
- **Issue:** The plan's first choice (a `UseEnvironment("Production")` factory resolving the client) fails at host startup because Production runs relational migrations.
- **Fix:** The plan's stated fallback: the registration's decision is a small public helper tested with a Production environment, plus a real Testing-host fact with the setting present.
- **Files modified:** `ResendBaseAddress.cs`, `EmailStatsRenderTests.cs`
- **Commit:** 1fa815fc

**2. [Rule 2 - Missing critical] Source guard for the address resolution**
- **Found during:** Task 2
- **Issue:** Nothing would stop a later edit from reading the setting directly in `Program.cs`, bypassing the Development gate.
- **Fix:** `ResendClient_BaseAddressIsDecidedOnlyByTheDevelopmentGatedHelper` in the conversion guard.
- **Commit:** 1fa815fc

**3. A new helper file and key class were added beyond the plan's file list** (`ResendBaseAddress.cs`, `AdminRateLimitKeys.cs`), both appended to `PhaseSourceFiles` together with `Program.cs` and `EmailSettings.cs`.

No other deviations.

## Notes for the next plans

- `AdminControllerIntegrationTests.EmailStats_WhenSuperAdmin_ShouldSucceed` depends on an earlier test in its class having seeded the default board: run alone it answers 302. It passes in the class and in the full suite; it was not changed (out of scope, pre-existing order dependence).
- The capture in the later screenshot plan can point a Development run at a canned local server with `EmailSettings:ResendBaseUrl` (environment variable form `EmailSettings__ResendBaseUrl`); no settings file needs editing. The run script's email suppression is unaffected.
- The hourly budgets (per target and per administrator) live for the life of the app process; tests that spend them need their own account ids or their own fixture.
- Out-of-scope observation: the calendar-feed limiter's `OnRejected` still answers 429 text; it is an `AddRateLimiter` policy and unrelated to this plan.

## Known Stubs

None.

## Threat Flags

None. The threat register items for this plan (resend budget burn, half-saved edit, unlimited creates, key in tests or config, hostile base address) are mitigated as described above.

## Self-Check: PASSED

- Created files exist on disk: `ResendBaseAddress.cs`, `AdminRateLimitKeys.cs`, `CannedResendHandler.cs`, `EmailStatsWebApplicationFactory.cs`, `EmailStatsRenderTests.cs`.
- Commits found: 2407aae6, 1fa815fc.
