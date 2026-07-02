# Technology Stack

**Analysis Date:** 2026-07-02

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

## Platform Requirements

**Development:**
- Windows or Linux with .NET 10 SDK
- SQL Server 2022 (local via `docker-compose up -d` or external `localhost`)
- Visual Studio or VS Code with C# extensions recommended
- Bash/PowerShell for command-line tooling (CLAUDE.md specifies Windows development with CRLF line endings)

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
- Built from: `mcr.microsoft.com/dotnet/sdk:10.0`
- Publishes as: `ghcr.io/theunschut/dnd-quest-board:latest` (referenced in docker-compose.yml)
- Healthcheck: HTTP GET `/health` every 30s (10s timeout, 3 retries, 40s start grace period)

**Database:**
- SQL Server 2022 (`mcr.microsoft.com/mssql/server:2022-latest`)
- Auto-migrations via `context.Database.Migrate()` in `Program.cs` (non-Testing environments only)
- Session state table: `AspNetSessionState` (schema: `dbo`)
- Hangfire job tables: auto-created in default schema

---

*Stack analysis: 2026-07-02*
