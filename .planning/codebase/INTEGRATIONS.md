# External Integrations

**Analysis Date:** 2026-07-02

## APIs & External Services

**Email Delivery:**
- SMTP (Primary) - Configurable SMTP server for transactional emails (quests, password resets, email confirmations)
  - Configuration via `EmailSettings` in appsettings.json
  - Client: `System.Net.Mail.SmtpClient` (built-in .NET)
  - Credentials: `SmtpUsername` / `SmtpPassword` (env vars: `EmailSettings__SmtpUsername`, `EmailSettings__SmtpPassword`)
  - Implementation: `QuestBoard.Domain/Services/EmailService.cs`

- Resend (Optional/Analytics) - Email delivery analytics and webhook support
  - SDK/Client: Custom HTTP client via `IHttpClientFactory` named "Resend"
  - API Base: `https://api.resend.com/`
  - Auth: Bearer token in Authorization header per-request (never stored in client config)
  - Configuration: `ResendApiKey` env var (`EmailSettings__ResendApiKey`)
  - Implementation: `QuestBoard.Service/Services/ResendStatsClient.cs`, `ResendStatsAggregator.cs`
  - Purpose: Fetch email delivery statistics (sent, delivered, bounced, failed) via `ResendStatsClient.FetchAllRecordsAsync()`
  - Retry Policy: Exponential backoff with 3 max retries on HTTP 429 (rate limit)

## Data Storage

**Databases:**

- SQL Server 2022
  - Connection: `DefaultConnection` env var or appsettings.json
  - Client: Entity Framework Core 10.0.9
  - Schema: Multiple tables auto-created by migrations in `QuestBoard.Repository/Migrations/`
  - Migrations: 61 migration files (latest: `20260702081517_AddQuestFinalizedDateIndex.cs`)
  - Auto-apply: Enabled via `context.Database.Migrate()` on startup (non-Testing environments)
  - Session State Table: `AspNetSessionState` (distributed cache backing)
  - Hangfire Job Queue: Auto-created tables in default schema

**File Storage:**
- Local filesystem only - No cloud blob storage configured
- Images stored in database as binary via `CharacterImageEntity` and `DungeonMasterProfileImageEntity`

**Caching:**
- Distributed SQL Server Cache - Session state persists across app restarts
  - Implementation: `Microsoft.Extensions.Caching.SqlServer`
  - Configuration: `AspNetSessionState` table in SQL Server
  - Fallback: In-memory cache in Testing environment (no persistence)
  - Session timeout: 24 hours idle
  - Purpose: Store `ActiveGroupId` and other session data
  - Related: `QuestBoard.Domain/Services/ActiveGroupContextService.cs`

**In-Process Cache:**
- None configured (no distributed cache for transient data; session cache is the primary state mechanism)

## Authentication & Identity

**Auth Provider:**
- ASP.NET Core Identity (custom implementation)
  - Implementation: `Microsoft.AspNetCore.Identity.EntityFrameworkCore`
  - Store: SQL Server via `QuestBoardContext` (inherits `IdentityDbContext<UserEntity, IdentityRole<int>, int>`)
  - Token Provider: `DataProtectionTokenProvider` with 7-day lifespan for password-reset, email-confirmation, change-email tokens
  - Password Policy:
    - Minimum 8 characters
    - Require uppercase, lowercase, digit
    - No special characters required
  - Lockout: 5 failed attempts → 15-minute lockout (enabled for new users)
  - Email: Must be unique per user

**Authorization Policies:**
- `"DungeonMasterOnly"` - Requires DungeonMaster or Admin role (handlers in `QuestBoard.Service/Authorization/`)
- `"AdminOnly"` - Requires Admin role only
- `"SuperAdminOnly"` - Requires SuperAdmin role only (highest privilege, gates Hangfire dashboard)
- Implementation: Custom `IAuthorizationHandler` implementations: `DungeonMasterHandler`, `AdminHandler`

**Roles:**
- SuperAdmin - System administrator (Hangfire dashboard access at `/hangfire`)
- Admin - Dungeon master admin operations
- DungeonMaster - Quest creation and management
- User - Default player role

## Monitoring & Observability

**Error Tracking:**
- None configured - No external error tracking service (e.g., Sentry, Application Insights)
- Logging: Built-in `ILogger<T>` via dependency injection
- Log levels: Information (default), Warning (ASP.NET Core, EF Core)

**Logs:**
- Console output (default for Docker/container deployments)
- Structured logging via `ILogger<T>` throughout application
- Log sinks: Depends on deployment (Docker logs, stdout, or optional syslog/Splunk via appsettings override)
- Important loggers:
  - `QuestBoard.Domain/Services/EmailService.cs` - Email send failures
  - `QuestBoard.Service/Services/ResendStatsClient.cs` - Resend API errors and retry attempts

**Health Checks:**
- Endpoint: `/health` (HTTP GET)
- Interval: 30 seconds
- Docker healthcheck: included in `docker-compose.yml`

## CI/CD & Deployment

**Hosting:**
- Docker container via `docker-compose.yml`
- Container registry: GitHub Container Registry (`ghcr.io/theunschut/dnd-quest-board:latest`)
- Self-hosted environment (not cloud-platform-specific)

**CI Pipeline:**
- GitHub Actions (inferred from `.git` and GHCR registry; not analyzed in detail)

**Deployment Process:**
- `docker-compose up -d` starts app and SQL Server
- Environment variables passed via docker-compose.yml or `.env` file (secrets)
- Database migrations auto-apply on container startup
- Kestrel server listens on port 8080 (mapped to host port 7080 in compose)

## Environment Configuration

**Required Environment Variables (Production):**
- `ConnectionStrings__DefaultConnection` - SQL Server connection string (required)
- `EmailSettings__FromEmail` - Sender email address (required in Production, validated on startup)
- `EmailSettings__SmtpServer` - SMTP server hostname (required in Production, validated on startup)
- `EmailSettings__SmtpPort` - SMTP port (default: 25)
- `EmailSettings__EnableSsl` - Enable TLS for SMTP (default: false)
- `EmailSettings__SmtpUsername` - SMTP credentials (optional)
- `EmailSettings__SmtpPassword` - SMTP credentials (optional)
- `EmailSettings__FromName` - Sender display name (default: "D&D Quest Board")
- `EmailSettings__AppUrl` - Application root URL (used in email links)
- `EmailSettings__ResendApiKey` - Resend API key (optional, only if using Resend analytics)
- `ReverseProxy__KnownProxies__0` - Trusted reverse proxy IP (for X-Forwarded-For header trust)
- `ASPNETCORE_ENVIRONMENT` - Environment name (Development, Production, Testing)
- `AutoMapper__LicenseKey` - AutoMapper license (optional, empty for free tier)

**Optional Environment Variables:**
- `MSSQL_SA_PASSWORD` - SQL Server SA password (docker-compose only)

**Secrets Location:**
- `.env` file (docker-compose) - Contains `MSSQL_SA_PASSWORD` and email credentials
- Environment variables (production deployment) - Set by container orchestration
- User Secrets (development only) - Via `dotnet user-secrets` for `AutoMapper__LicenseKey`
- **IMPORTANT:** Never commit `.env` or credential files to git; they are `.gitignored`

## Webhooks & Callbacks

**Incoming:**
- None configured - No webhook endpoints for external services

**Outgoing:**
- Email webhooks (optional via Resend) - Can be configured but not actively processed in current codebase
  - Would require additional endpoint implementation if enabled

**Background Jobs (via Hangfire):**
- Job Queue: SQL Server (Hangfire.SqlServer storage)
- Worker Count: 2 (configured in `Program.cs`)
- Polling: Zero-delay polling (QueuePollInterval = TimeSpan.Zero)
- Retry Policy: Exponential backoff — 5 attempts over 1/2/4/8/16 seconds for transient failures
- Recurring Jobs:
  - `daily-session-reminders` - Runs daily at 09:00 server local time (CET/CEST)
    - Implementation: `QuestBoard.Service/Jobs/DailyReminderJob.cs`
- One-Off Email Jobs (enqueued by controllers/services):
  - `QuestFinalizedEmailJob` - Sends email when quest date is finalized
  - `SessionReminderJob` - Sends reminder emails before session starts
  - `WelcomeEmailJob` - Welcome email for new users
  - `ForgotPasswordEmailJob` - Password reset email
  - `ChangeEmailConfirmationJob` - Email address change confirmation
  - `QuestDateChangedEmailJob` - Notification when quest date changes
- Dashboard: SuperAdmin-only at `/hangfire` (authenticated via custom `AdminDashboardAuthFilter`)

## Rate Limiting

**Authentication Endpoints:**
- `/Account/ForgotPassword` POST - 3 requests per 15 minutes per client IP
- `/Account/SetPassword` POST - 3 requests per 15 minutes per client IP (independent limit)

**Admin Email Resend:**
- Manual email resend (admin actions) - 3 requests per hour per target user ID
- Partitioned by userId to prevent recipient inbox spam
- Configured as `PartitionedRateLimiter<int>` in `Program.cs` (not AddRateLimiter policy)
- Note: Automated welcome emails (CreateUser) exempt from rate limiting

**Implementation:** Built-in ASP.NET Core rate limiting middleware (no external service required)

---

*Integration audit: 2026-07-02*
