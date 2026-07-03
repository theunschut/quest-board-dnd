# Technology Stack

**Analysis Date:** 2026-07-03
**Last Mapped Commit:** e5b37a73cda29bf355c4de6ebf4663b1625c3cf6

## Languages

**Primary:**
- C# 13 (ASP.NET Core 10) - All application code (Service, Domain, Repository layers)

## Runtime

**Environment:**
- .NET 10 (net10.0)

**Package Manager:**
- NuGet
- Lockfile: `packages.lock.json` (implicit via .csproj declarations)

## Frameworks

**Core:**
- ASP.NET Core 10.0.9 - Web application framework, MVC, routing, middleware
- Entity Framework Core 10.0.9 - ORM for SQL Server database access
- Microsoft.AspNetCore.Identity 10.0.9 - User authentication and authorization with role-based policies

**Testing:**
- xUnit 3.2.2 - Unit and integration test framework
- FluentAssertions 8.10.0 - Assertion helpers and matchers
- Microsoft.AspNetCore.Mvc.Testing 10.0.9 - WebApplicationFactory for integration testing
- Microsoft.EntityFrameworkCore.InMemory 10.0.9 - In-memory database for test isolation

**Build/Dev:**
- Microsoft.EntityFrameworkCore.Tools 10.0.9 - EF migrations CLI (dotnet ef)
- Microsoft.EntityFrameworkCore.Design 10.0.9 - Design-time EF services

**Background Jobs:**
- Hangfire.AspNetCore 1.8.23 - Background job scheduling and retry logic
- Hangfire.SqlServer 1.8.23 - SQL Server persistence for Hangfire jobs

## Key Dependencies

**Critical:**
- AutoMapper 16.1.1 - Object-to-object mapping between domain models, entities, and view models
- Microsoft.Extensions.Caching.SqlServer 10.0.9 - Distributed session state via SQL Server (persists across app restarts)
- NSubstitute 5.3.0 - Mocking framework for unit tests
- Microsoft.NET.Test.Sdk 18.7.0 - Test SDK runtime

**Infrastructure:**
- Microsoft.AspNetCore.Identity.EntityFrameworkCore 10.0.9 - Identity entity configurations for EF Core
- xunit.runner.visualstudio 3.1.5 - Visual Studio test runner integration

## Configuration

**Environment:**
- Appsettings-based configuration (`appsettings.json`)
- Environment variables override appsettings via `builder.Configuration.GetConnectionString()` and `builder.Configuration["Section:Key"]`
- Three environments: Development, Production, Testing
  - Testing environment: Hangfire disabled, in-memory cache used instead of SQL Server cache, database isolation via `WebApplicationFactoryBase`
- `ASPNETCORE_ENVIRONMENT` env var controls environment selection

**Build:**
- SDK: `.NET 10 SDK`
- Multi-stage Docker build with BuildKit cache for faster rebuilds
- Target framework: `net10.0` (all projects)
- Nullable reference types enabled (`<Nullable>enable</Nullable>`)
- Implicit usings enabled (`<ImplicitUsings>enable</ImplicitUsings>`)

**Key Configuration Files:**
- `QuestBoard.Service/appsettings.json` - Database connection, email settings, Resend API key, reverse proxy trust config
- `docker-compose.yml` - Local dev containerization with SQL Server 2022 and ASP.NET app
- `Dockerfile` - Multi-stage production build image (base: .NET 10 runtime)
- `.config/dotnet-tools.json` - Local tool manifest specifying `dotnet-ef` v9.0.6 for migration management

## Local Tools

**dotnet-tools.json** (`.config/dotnet-tools.json`):
- `dotnet-ef` v9.0.6 - Entity Framework Core migration tooling for local development
- Invoked via `dotnet ef` commands after restore

## Platform Requirements

**Development:**
- Windows or Linux with .NET 10 SDK
- SQL Server 2022 (local via `docker-compose up -d` or external `localhost`)
- Visual Studio or VS Code with C# extensions recommended
- Bash/PowerShell for command-line tooling (CLAUDE.md specifies Windows development with CRLF line endings)
- `create-migration.sh` script available for migration generation from Windows WSL

**Production:**
- Container runtime (Docker) with `docker-compose.yml`
- SQL Server 2022 database (externally managed, mounted from host)
- Environment variables for:
  - Database credentials and connection string
  - Email configuration (SmtpServer, FromEmail, optional ResendApiKey)
  - Session secret (implicit in distributed cache configuration)
  - Reverse proxy known IP addresses (ReverseProxy:KnownProxies)
- Kestrel HTTP server (configured on port 8080 in container)
- Rate limiting enforced at application level (no external rate limiter required)

## Deployment Architecture

**Container Image:**
- Base: `mcr.microsoft.com/dotnet/aspnet:10.0`
- Build base: `mcr.microsoft.com/dotnet/sdk:10.0`
- Publishes as: `ghcr.io/theunschut/dnd-quest-board:latest` (referenced in docker-compose.yml)
- Healthcheck: HTTP GET `/health` every 30s (10s timeout, 3 retries, 40s start grace period)
- Multi-stage build (`.Dockerfile`):
  - Stage 1 (base): ASP.NET 10.0 runtime image
  - Stage 2 (build): .NET 10 SDK for compilation and publishing
  - Stage 3 (publish): Outputs to `/app/publish`
  - Stage 4 (final): Copies artifacts and runs service
- BuildKit cache mount enabled for NuGet packages (`RUN --mount=type=cache,target=/root/.nuget/packages`)
- Environment variables set in final image: `ASPNETCORE_URLS=http://+:8080`, `ASPNETCORE_ENVIRONMENT=Production`
- Entry point: `dotnet QuestBoard.Service.dll`

**Docker Compose** (`docker-compose.yml`):
- App service: `ghcr.io/theunschut/dnd-quest-board:latest`
  - Port mapping: 7080 (host) → 8080 (container)
  - Network: `net-dnd` (external, must be pre-created)
  - Depends on: `sqlserver` service
  - Restart policy: `unless-stopped`
  - Health check: HTTP GET `/health` every 30s
- SQL Server 2022 service: `mcr.microsoft.com/mssql/server:2022-latest`
  - Port mapping: 1433 (host) → 1433 (container)
  - Volume mount: `sqlserver_data:/var/opt/mssql` (persisted across restarts)
  - Environment: `ACCEPT_EULA=Y`, `MSSQL_SA_PASSWORD` from `.env`
  - Restart policy: `unless-stopped`

**Database:**
- SQL Server 2022 (`mcr.microsoft.com/mssql/server:2022-latest`)
- Auto-migrations via `context.Database.Migrate()` in `Program.cs` (non-Testing environments only)
- Session state table: `AspNetSessionState` (schema: `dbo`)
- Hangfire job tables: auto-created in default schema
- Connection string in docker-compose: `Server=sqlserver;Database=QuestBoard;User Id=sa;Password=${MSSQL_SA_PASSWORD};TrustServerCertificate=true;`

## CI/CD & GitHub Actions

**Workflows Location:** `.github/workflows/`

**dotnet.yml** — .NET build and test CI:
- Triggers: Push to `main` branch, pull requests to `main`
- Runs on: `ubuntu-latest`
- Steps:
  1. Checkout repository
  2. Setup .NET (version 8.0.x — **note: older than runtime; CI uses .NET 8 while app runs .NET 10**)
  3. Restore dependencies
  4. Build (Release configuration)
  5. Run all tests via `dotnet test`
- Permissions: Read-only (`contents: read`)

**docker-publish.yml** — Container image build and publish:
- Triggers: Push with tags matching `v*.*.*` (semver releases only)
- Runs on: `ubuntu-latest`
- Registry: GitHub Container Registry (`ghcr.io`)
- Image name: Derived from `github.repository` (resolves to `theunschut/dnd-quest-board`)
- Build pipeline:
  1. Checkout
  2. Install cosign v2.4.1 (container signing tool, skip on PRs)
  3. Setup Docker Buildx for multi-platform builds
  4. Login to GHCR (skip on PRs)
  5. Extract Docker metadata and apply semver tags
  6. Build and push image
     - Platform: `linux/amd64` only
     - Cache: GitHub Actions cache (GHA) with max mode
     - BuildKit inline cache enabled
  7. Sign published image with cosign (skip on PRs)
- Permissions: `contents: read`, `packages: write`, `id-token: write` (for cosign)

**binary-release.yml** — Binary release and self-hosted deployment:
- Triggers: 
  - Push with semver tags (`v*.*.*`)
  - Manual workflow dispatch with tag input
- Jobs:
  - `release` (only on push events):
    - Runs on: `ubuntu-latest`
    - Steps:
      1. Checkout
      2. Setup .NET (10.0.x — **correct version for app runtime**)
      3. Publish Service project to Release configuration (`./publish`)
      4. Create zip archive: `questboard-${tag}.zip`
      5. Create GitHub release with zip attached
    - Permissions: `contents: write` (create releases)
  - `deploy` (conditional: runs after `release` succeeds OR on manual dispatch):
    - Runs on: `self-hosted` runner (must be configured in repo settings)
    - Steps:
      1. Execute `/home/questboard/deploy.sh` with tag (from git ref or manual input)
    - Permissions: Empty (no GitHub permissions needed)
- **Note:** Self-hosted runner must have deployment script at `/home/questboard/deploy.sh`

## Docker Build Optimization

**BuildKit Features Used:**
- Cache mount for NuGet packages (`.Dockerfile` lines 16, 25)
- Inline cache enabled for GitHub Actions integration
- Excludes test projects and development files via `.dockerignore`

**.dockerignore** (`/.dockerignore`):
- Excludes: `.dockerignore`, `.env`, `.git`, `.gitignore`, `.vs`, `.vscode`, `bin`, `obj`, `node_modules`, `docker-compose.yml`, `Dockerfile*`, `LICENSE`, `README.md`, test/dev artifacts
- Purpose: Minimize final image size by excluding unnecessary build context

## Local Development Tooling

**create-migration.sh** (`/create-migration.sh`):
- Purpose: Convenience script for Windows WSL migration generation
- Steps:
  1. Changes to `QuestBoard.Service/` directory
  2. Adds `Microsoft.EntityFrameworkCore.Tools` package
  3. Invokes `dotnet ef migrations add` with QuestBoard.Repository project
- Usage: `bash create-migration.sh` (from Windows WSL environment)

---

*Stack analysis: 2026-07-03*
*Last mapped commit: e5b37a73cda29bf355c4de6ebf4663b1625c3cf6*
