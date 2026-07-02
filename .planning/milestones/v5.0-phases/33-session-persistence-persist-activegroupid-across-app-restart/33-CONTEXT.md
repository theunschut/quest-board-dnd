# Phase 33: Session Persistence + Admin Email Rate Limiting - Context

**Gathered:** 2026-07-01
**Status:** Ready for planning

<domain>
## Phase Boundary

Two distinct deliverables:

1. **Session persistence** — Back ASP.NET Core Session with `AddDistributedSqlServerCache` so `ActiveGroupId` survives app restarts. Currently, `AddSession` falls back to an in-memory store that is wiped on every deploy. The auth cookie (Identity, Data Protection) already survives restarts; only the Session data (group selection) does not. `Microsoft.Extensions.Caching.SqlServer` uses the existing SQL Server connection — no new infrastructure.

2. **Admin email rate limiting** — Rate-limit repeatable manual email-send buttons (Resend Welcome, EditUser email-change confirmation) to protect the Resend relay's 100/day quota from accidental admin button-mashing. One-shot automated sends (WelcomeEmailJob on CreateUser, ForgotPasswordEmailJob on ForgotPassword POST) are explicitly exempt — they are not repeatable manual actions.

This phase does NOT include: Redis or any other distributed cache infrastructure, changes to session TTL or cookie configuration, rate-limiting any anonymous or self-service flows (those were covered in Phase 32), or adding new email templates.

</domain>

<decisions>
## Implementation Decisions

### Session Cache Provider

- **D-01:** Register `AddDistributedSqlServerCache` (package: `Microsoft.Extensions.Caching.SqlServer`) using the same `DefaultConnection` connection string already in `appsettings.json`. Registration must appear BEFORE `AddSession` in `Program.cs` (distributed cache must be registered before session or session will throw at startup).
- **D-02:** `SqlServerCacheOptions` must set `SchemaName` and `TableName` explicitly (`dbo` / `AspNetSessionState` are the defaults but should be explicit for clarity). `ExpiredItemsDeletionInterval` is set to 30 minutes (framework default — no override needed).

### Cache Table Provisioning

- **D-03:** The `dbo.AspNetSessionState` table is provisioned via an EF Core migration using `migrationBuilder.Sql(...)` with a raw `CREATE TABLE IF NOT EXISTS` guard. This keeps schema changes in the audit trail, is auto-applied on startup via `context.Database.Migrate()`, and requires no manual deploy step. The exact DDL for this table is well-known (fixed schema: `Id NVARCHAR(449) NOT NULL`, `Value VARBINARY(MAX) NOT NULL`, `ExpiresAtTime DATETIMEOFFSET(7) NOT NULL`, `SlidingExpirationInSeconds BIGINT NULL`, `AbsoluteExpiration DATETIMEOFFSET(7) NULL`; primary key on `Id`; index on `ExpiresAtTime`). Migration should be placed in `QuestBoard.Repository` (where all migrations live).

### Expired Entry Cleanup

- **D-04:** Rely on the built-in `ExpiredItemsDeletionInterval` (default: every 30 minutes) from `SqlServerCacheOptions`. No Hangfire CRON needed for session cleanup — the cache provider handles it automatically.

### Admin Email Rate Limiting

- **D-05:** A new `"admin-email-resend"` rate-limit policy is added to `AddRateLimiter` in `Program.cs`, partitioned by **target user ID** (not admin IP). Limit: 3 requests per hour per target user, fixed-window. Partition key pattern: `$"email-resend:{userId}"`. Using target userId protects any one recipient's inbox from repeated sends, regardless of which admin triggers it.
- **D-06:** `AdminController.SendConfirmationEmail` (POST) gets `[EnableRateLimiting("admin-email-resend")]`. The `userId` action parameter is already in the route/form, accessible from `httpContext.GetRouteValue("userId")` in the policy factory.
- **D-07:** The `EditUser POST` email-change path (which enqueues `ChangeEmailConfirmationJob` when the email field changes) is also rate-limited with the same policy. Because the rate limit should apply only to the email-dispatch sub-path (not to all EditUser saves), the planner should evaluate whether to apply it programmatically via `IPartitionedRateLimiter<HttpContext>` injection inside the action body, or accept that `[EnableRateLimiting]` on the full action is an acceptable approximation (an admin POSTing EditUser without changing email is not a meaningful risk even if counted).
- **D-08:** `CreateUser POST` (which enqueues `WelcomeEmailJob`) is explicitly **exempt** from rate-limiting — it is a one-shot automated send, not a repeatable manual button.

### Claude's Discretion

- Whether to use `[EnableRateLimiting("admin-email-resend")]` on `EditUser` as a whole or inject `IPartitionedRateLimiter` for action-body-level control — planner decides based on complexity vs. accuracy trade-off.
- Exact `SqlServerCacheOptions.ExpiredItemsDeletionInterval` value — framework default (30 min) is fine unless planner sees reason to override.
- Migration name and ordering relative to any other pending migrations.

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Phase Scope
- `.planning/ROADMAP.md` §Phase 33 — goal statement and additional scope item (rate-limit email buttons, open questions addressed in this context)
- `.planning/STATE.md` §Roadmap Evolution — the "Phase 33 added" entry explains exactly why session persistence is needed and confirms `AddDistributedSqlServerCache` + cleanup job as the recommended fix

### Requirements & Prior Decisions
- `.planning/REQUIREMENTS.md` — v5.0 requirements (no session-persistence REQ-IDs yet; planner should add them)
- `.planning/phases/32-first-login-password-flow/32-CONTEXT.md` D-12 — rate-limiting pattern with `Microsoft.AspNetCore.RateLimiting`, `AddRateLimiter`, `[EnableRateLimiting]`. Phase 33 extends the same pattern to admin email actions.

### Key Files to Read Before Planning
- `QuestBoard.Service/Program.cs` lines 100–143 — existing `AddRateLimiter` setup (`forgot-password`, `set-password` policies) and `AddSession` config. Phase 33 adds `AddDistributedSqlServerCache` before `AddSession` and a new `"admin-email-resend"` policy inside the same `AddRateLimiter` call.
- `QuestBoard.Service/Services/ActiveGroupContextService.cs` — reads `ActiveGroupId` from `Session.GetInt32(SessionKeys.ActiveGroupId)`. No change needed to this file; once session is backed by SQL Server cache, it persists automatically.
- `QuestBoard.Service/Controllers/Admin/AdminController.cs` lines 270–330 — `SendConfirmationEmail` action (rate-limit target). Also `EditUser POST` (lines ~145–207) for the email-change path.

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `builder.Services.AddRateLimiter(options => { ... })` at `Program.cs:105` — already registered with 2 policies; adding `"admin-email-resend"` is a new `options.AddPolicy(...)` call inside the same block.
- `builder.Services.AddSession(...)` at `Program.cs:138` — no changes to session options needed; switching the backing store is transparent to session consumers.
- `ActiveGroupContextService.ActiveGroupId` reads from `Session.GetInt32(SessionKeys.ActiveGroupId)` — works without modification once the distributed cache is registered.
- `httpContext.Connection.RemoteIpAddress` pattern (existing `forgot-password` policy) — the `"admin-email-resend"` policy uses `httpContext.GetRouteValue("userId")` instead.

### Established Patterns
- Rate-limiting: named policies in `AddRateLimiter`, `[EnableRateLimiting("policy-name")]` on action, `options.OnRejected` returns 429 with plain-text body (Phase 32 pattern — already in place, no change needed to `OnRejected`).
- EF migrations with raw SQL: `migrationBuilder.Sql("IF NOT EXISTS (...) BEGIN ... END")` is the pattern for non-EF-entity schema changes.
- Session keys: `SessionKeys.ActiveGroupId` constant in `QuestBoard.Service/Constants/` — no new constant needed.

### Integration Points
- `Program.cs` — `builder.Services.AddDistributedSqlServerCache(...)` must be added BEFORE the existing `builder.Services.AddSession(...)` block.
- New EF migration in `QuestBoard.Repository/Migrations/` — raw SQL for `AspNetSessionState` table creation.
- `AdminController.SendConfirmationEmail` — add `[EnableRateLimiting("admin-email-resend")]` attribute.
- `AdminController.EditUser POST` — add rate limit to email-change dispatch path (attribute or programmatic).

### Known Landmines
- `AddDistributedSqlServerCache` must be registered before `AddSession`. If registered after, ASP.NET Core's session middleware will still find a registered `IDistributedCache` but the startup order matters for correct initialization.
- The `AspNetSessionState` table schema is fixed and documented in the `Microsoft.Extensions.Caching.SqlServer` source — do not try to create it via EF entity/fluent config (it's not a tracked entity). Use raw DDL in the migration.
- `GetRouteValue("userId")` in the rate-limit policy factory returns a `string?` from route template matching — the value is available during middleware execution before the action runs, making it a valid partition key source.

</code_context>

<specifics>
## Specific Ideas

- `CreateUser POST` (WelcomeEmailJob enqueue) is **explicitly exempt** from rate-limiting — the user was clear that one-shot automated sends do not need limiting.
- Rate-limit limit values: 3 per hour per target user for admin email resend (same count as `forgot-password` / `set-password`, different window: 1 hour vs. 15 minutes).
- If 429 is returned on `SendConfirmationEmail`, the user expects the standard `options.OnRejected` response (already configured as plain-text "Too many requests. Please try again later.") — no special redirect needed for the admin.

</specifics>

<deferred>
## Deferred Ideas

- **"Password changed" notification email** — deferred from Phase 32; still pending.
- **Digest batching for session reminders** (EMAIL-04/REMIND-02) — still deferred; same-day quests have never occurred.
- **Per-group email configuration** — future milestone item.

None of these came up in Phase 33 discussion — carried forward from PROJECT.md deferred items.

</deferred>

---

*Phase: 33-session-persistence-persist-activegroupid-across-app-restart*
*Context gathered: 2026-07-01*
