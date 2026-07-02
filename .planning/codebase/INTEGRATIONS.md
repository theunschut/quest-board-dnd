# External Integrations

**Analysis Date:** 2026-07-01

## APIs & External Services

**Email Analytics:**
- Resend API - Optional email statistics and delivery monitoring
  - SDK/Client: Named HttpClient registered in `QuestBoard.Service/Program.cs` line 152-157
  - Base URL: `https://api.resend.com/`
  - Auth: Bearer token in `Authorization` header per-request
  - API Key env var: `EmailSettings__ResendApiKey`
  - Usage: `QuestBoard.Service/Controllers/Admin/AdminController.cs` `GetResendStatsAsync()` method
  - Endpoint: `GET /emails?limit=100` (paginated email records)
  - Cache: 5-minute TTL on stats views

## Data Storage

**Database:**
- SQL Server 2022
  - Connection: `ConnectionStrings__DefaultConnection` (env var override)
  - Dev: `localhost` with Windows authentication
  - Production: Service name `sqlserver` in docker-compose
  - ORM: Entity Framework Core 10.0.9 with SQL Server provider
  - Context: `QuestBoard.Repository/Entities/QuestBoardContext.cs`
  - Auto-migrations: Applied on app startup via `context.Database.Migrate()`

**Background Job Storage:**
- SQL Server (same database as application)
  - Hangfire uses SQL Server storage for job queue, state, and recurring job history
  - Hangfire configuration: `QuestBoard.Service/Program.cs` lines 186-204
  - Tables: Auto-created in `Hangfire` schema on first run
  - Polling interval: 0 (push-based job dispatch)

**In-Memory Storage (Testing Only):**
- Microsoft.EntityFrameworkCore.InMemory - Used in integration tests
  - Configuration: `QuestBoard.IntegrationTests/` test setup
  - Not used in production

**File Storage:**
- Local filesystem only - No external blob storage configured
- Character and DM profile images stored locally (relative paths in database)

**Caching:**
- In-memory cache (IMemoryCache)
  - Used for Resend API stats (5-minute TTL)
  - Configured in DI container via `builder.Services.AddMemoryCache()` (implicit in ASP.NET Core)

## Authentication & Identity

**Auth Provider:**
- ASP.NET Core Identity (built-in)
  - Implementation: Forms-based authentication with session cookies
  - User store: SQL Server via `IdentityDbContext<UserEntity, IdentityRole<int>, int>`
  - Password requirements: Uppercase + lowercase + digit + 8+ chars, no special chars required
  - Lockout: 5 failed attempts → 15-minute lockout
  - Email confirmation: Optional (configurable)
  - Role-based access control: `DungeonMasterOnly`, `AdminOnly`, `SuperAdminOnly` policies

**Token Providers:**
- Data Protection Token Provider (default)
  - Lifespan: 7 days (configured in `QuestBoard.Service/Program.cs` line 68-70)
  - Used for: Password reset, email confirmation, change-email tokens

**Authorization Policies:**
- `DungeonMasterOnly` - DungeonMaster or Admin role required (handler: `DungeonMasterHandler`)
- `AdminOnly` - Admin role only (handler: `AdminHandler`)
- `SuperAdminOnly` - SuperAdmin role only

**Hangfire Dashboard:**
- Auth: Custom filter (`AdminDashboardAuthFilter`)
- Access: Admin or SuperAdmin role only
- Path: `/hangfire`
- Enforced in middleware: `QuestBoard.Service/Program.cs` lines 250-274

## Email Service

**Primary Provider:**
- SMTP (configurable)
  - Client: System.Net.Mail.SmtpClient
  - Configuration: `EmailSettings` model in `QuestBoard.Domain/Models/EmailSettings.cs`
  - Server: Env var `EmailSettings__SmtpServer` (default: 192.168.6.13)
  - Port: Env var `EmailSettings__SmtpPort` (default: 25)
  - Username: Env var `EmailSettings__SmtpUsername` (optional)
  - Password: Env var `EmailSettings__SmtpPassword` (optional)
  - SSL/TLS: Env var `EmailSettings__EnableSsl` (default: false)
  - From address: Env var `EmailSettings__FromEmail`
  - From name: Env var `EmailSettings__FromName` (default: "D&D Quest Board")
  - Implementation: `QuestBoard.Domain/Services/EmailService.cs`

**Email Rendering:**
- Razor View Engine - Server-side template rendering
  - Service: `IEmailRenderService` (registered as `RazorEmailRenderService`)
  - Email templates: `QuestBoard.Service/Components/Emails/*.cshtml`
  - Template models: Passed as `Dictionary<string, object?>` to renderer
  - Examples: `QuestFinalized.cshtml`, `WelcomeEmail.cshtml`, `SessionReminder.cshtml`

**Background Job Dispatch:**
- Hangfire job queue
  - Dispatcher: `IQuestEmailDispatcher` (implementation: `HangfireQuestEmailDispatcher` or `NullQuestEmailDispatcher` in testing)
  - Reminder dispatcher: `IReminderJobDispatcher` (implementation: `HangfireReminderJobDispatcher` or `NullReminderJobDispatcher`)
  - Jobs registered: `QuestBoard.Service/Jobs/`
    - `QuestFinalizedEmailJob` - Quest finalized notification to players
    - `SessionReminderJob` - Individual reminder for a session
    - `WelcomeEmailJob` - New user welcome
    - `ForgotPasswordEmailJob` - Password reset link
    - `ChangeEmailConfirmationJob` - Email change confirmation
    - `QuestDateChangedEmailJob` - Quest date modification notification
    - `DailyReminderJob` - Recurring job: daily session reminder sweep at 09:00 CET/CEST
  - Configuration: `QuestBoard.Service/Program.cs` lines 186-204
  - Worker threads: 2 (configured in `AddHangfireServer`)
  - Polling interval: 0 (immediate dispatch when available)

**Email Addresses:**
- From: `EmailSettings__FromEmail` (env var, required if sending)
- To: User email addresses from identity system
- No email list/distribution group integrations

## Monitoring & Observability

**Error Tracking:**
- None detected - No external error tracking service (Sentry, Application Insights, etc.)
- Logging: Built-in ASP.NET Core structured logging with serilog-like patterns

**Logs:**
- Console output (Kestrel default)
- Log levels configured: `appsettings.json` lines 2-7
  - Default: Information
  - Microsoft.AspNetCore: Warning
  - Microsoft.EntityFrameworkCore: Warning
- Job execution logged via `ILogger<T>` in Hangfire jobs

**Health Checks:**
- Endpoint: `GET /health`
- Service: `builder.Services.AddHealthChecks()`
- Docker healthcheck: curl to `http://localhost:8080/health` every 30s (5 retries)

## CI/CD & Deployment

**Hosting:**
- Docker container (`ghcr.io/theunschut/dnd-quest-board:latest`)
- Docker Compose orchestration (`docker-compose.yml`)
- Self-hosted environment (not cloud-managed)

**Container Networking:**
- Network: `net-dnd` (user-defined bridge)
- Service name: `questboard` (internal DNS for docker-compose)
- Port mapping: `7080:8080` (host:container)

**CI Pipeline:**
- None detected - Repository does not include CI config files (GitHub Actions, GitLab CI, etc.)

**Database Migrations:**
- Auto-applied on startup: `context.Database.Migrate()` in `QuestBoard.Service/Program.cs` line 290
- EF Core migrations stored in `QuestBoard.Repository/Migrations/`
- Migration commands (for development): 
  ```bash
  dotnet ef migrations add <name> --project ../QuestBoard.Repository
  dotnet ef migrations remove --project ../QuestBoard.Repository
  ```

## Environment Configuration

**Required Env Vars:**
- `ConnectionStrings__DefaultConnection` - SQL Server connection (required; if not set, app will not start)
- `ASPNETCORE_ENVIRONMENT` - Environment name (Production, Development, Testing)
- `MSSQL_SA_PASSWORD` - SQL Server admin password (docker-compose only)

**Optional Env Vars (Email):**
- `EmailSettings__SmtpServer` - SMTP relay server
- `EmailSettings__SmtpPort` - SMTP relay port
- `EmailSettings__SmtpUsername` - SMTP auth username
- `EmailSettings__SmtpPassword` - SMTP auth password
- `EmailSettings__FromEmail` - Sender email address
- `EmailSettings__FromName` - Sender display name
- `EmailSettings__AppUrl` - Application base URL (used in email links)
- `EmailSettings__ResendApiKey` - Resend API key (for stats dashboard)

**Optional Env Vars (Other):**
- `AutoMapper__LicenseKey` - AutoMapper license (enterprise only)
- `ReverseProxy__KnownProxies__0` - Trusted proxy IP addresses (production with reverse proxy only)

**Secrets Location:**
- `.env` file (local development) - Contains database password and SMTP credentials
- `.env` is Git-ignored; see `.env.example` for template
- Production: Injected via environment variables in docker-compose or orchestration platform

## Session Management

**Session Storage:**
- In-memory (default ASP.NET Core session middleware)
- Cookie-based session ID
- Config: `QuestBoard.Service/Program.cs` lines 137-143
  - Idle timeout: 24 hours
  - HttpOnly: true
  - Essential: true (always set, no consent needed)

**Active Group Context (Multi-Tenancy):**
- Scoped session context: `ActiveGroupContextService` and `IActiveGroupContext`
- Set per-request: `GroupSessionMiddleware`
- Stores current `groupId` for tenant isolation
- Dual registration pattern allows both interface and concrete type resolution (for Hangfire jobs)
- Testing override: `MutableGroupContext` singleton

## Rate Limiting

**Password Reset & Account Actions:**
- Policy: `forgot-password` - 3 requests per 15 minutes per client IP
- Policy: `set-password` - 3 requests per 15 minutes per client IP (independent budget)
- Partition key: Client IP address via `RemoteIpAddress`
- X-Forwarded-For support: Trusted proxies configured in `ReverseProxy__KnownProxies`
- Response on limit exceeded: HTTP 429 ("Too many requests")

## Webhooks & Callbacks

**Incoming:**
- None detected - No external webhook endpoints

**Outgoing:**
- Internal callbacks only (e.g., password reset token callbacks, email change confirmation)
- Generated via `Url.Action()` in controllers and included in Hangfire job payloads
- Example: `QuestBoard.Service/Controllers/Admin/AccountController.cs` line 72
  ```csharp
  var callbackUrl = Url.Action(nameof(SetPassword), "Account", ...);
  // Passed to ForgotPasswordEmailJob for inclusion in email link
  ```

---

*Integration audit: 2026-07-01*
