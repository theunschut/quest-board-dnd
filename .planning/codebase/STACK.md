# Technology Stack

**Analysis Date:** 2026-07-01

## Languages

**Primary:**
- C# - Used across all three layers (Service, Domain, Repository)

## Runtime

**Environment:**
- .NET 10.0 (ASP.NET Core 10 MVC)
- Kestrel HTTP server
- Windows host for development; Docker container for production

**Package Manager:**
- NuGet (.csproj package management)
- Lockfile: Not applicable (auto-generated; use `packages.lock.json` when needed)

## Frameworks

**Core:**
- ASP.NET Core 10 (MVC) - Web application framework
- Entity Framework Core 10.0.9 - ORM for database access

**Identity & Authentication:**
- ASP.NET Core Identity - User management, roles, password reset, email confirmation
- Uses `IdentityDbContext<UserEntity, IdentityRole<int>, int>` in `QuestBoard.Repository/Entities/QuestBoardContext.cs`

**Background Jobs:**
- Hangfire 1.8.23 (AspNetCore + SqlServer) - Scheduled and enqueued email jobs
- Stored in SQL Server; runs on 2 worker threads
- Configured in `QuestBoard.Service/Program.cs` lines 186-204
- Dashboard at `/hangfire` (Admin/SuperAdmin only)

**View Templating:**
- Razor Views - Server-side HTML templates
- Mobile view location expander enabled in `QuestBoard.Service/Program.cs` line 38

**Testing:**
- xUnit v3 (test framework for both unit and integration tests)
- xUnit.Runner v3.1.5 - Test runner
- Microsoft.NET.Test.Sdk 18.7.0

**Mapping:**
- AutoMapper 16.1.1 - Object-to-object mapping
- Registered in two layers: Entity↔DomainModel and DomainModel↔ViewModel
- License key: configurable via environment variable `AutoMapper__LicenseKey`

**Build/Dev:**
- Entity Framework Core Tools 10.0.9 - CLI tools for migrations (design-time only)
- Entity Framework Core Design 10.0.9 - Design-time services for migrations

## Key Dependencies

**Critical:**
- Microsoft.EntityFrameworkCore 10.0.9 - Core EF infrastructure
- Microsoft.EntityFrameworkCore.SqlServer 10.0.9 - SQL Server provider
- Microsoft.AspNetCore.Identity.EntityFrameworkCore 10.0.9 - Identity with EF Core
- Hangfire.AspNetCore 1.8.23 - Hangfire integration with ASP.NET Core
- Hangfire.SqlServer 1.8.23 - Hangfire background job storage in SQL Server

**Infrastructure:**
- Microsoft.AspNetCore.Identity.UI 10.0.9 - Identity UI components (scaffolding support)
- Microsoft.Extensions.Configuration.Binder 10.0.9 - Configuration binding
- Microsoft.Extensions.Options.ConfigurationExtensions 10.0.9 - Options pattern for settings
- System.Security.Cryptography.Xml 10.0.9 - XML cryptography support (legacy identity features)
- Microsoft.AspNetCore.HttpOverrides - X-Forwarded-For header handling for reverse proxies

**Testing Libraries:**
- FluentAssertions 8.10.0 - Readable assertions in tests
- NSubstitute 5.3.0 - Mocking framework for unit tests
- Microsoft.AspNetCore.Mvc.Testing 10.0.9 - WebApplicationFactory for integration tests
- Microsoft.EntityFrameworkCore.InMemory 10.0.9 - In-memory database for integration tests

## Configuration

**Environment:**
- Settings sourced from `appsettings.json` and environment variables
- Active configuration bound via `IConfiguration` in dependency injection
- Main entry point: `QuestBoard.Service/Program.cs`

**Key Configurations Required:**
- `ConnectionStrings__DefaultConnection` - SQL Server connection string (required)
- `EmailSettings__SmtpServer` - SMTP server for email (default: 192.168.6.13)
- `EmailSettings__SmtpPort` - SMTP port (default: 25)
- `EmailSettings__SmtpUsername` - SMTP username (optional for relay servers)
- `EmailSettings__SmtpPassword` - SMTP password (optional for relay servers)
- `EmailSettings__FromEmail` - Sender email address (required if sending)
- `EmailSettings__FromName` - Sender display name (default: "D&D Quest Board")
- `EmailSettings__AppUrl` - Public application URL for links in emails (required)
- `EmailSettings__ResendApiKey` - Resend API key for analytics (optional)
- `AutoMapper__LicenseKey` - AutoMapper enterprise license (optional, for license compliance)
- `ReverseProxy__KnownProxies__0=<ip>` - IPs of trusted reverse proxies for X-Forwarded-For trust (production only)

**Build:**
- Project files use `net10.0` target framework across all layers
- Implicit usings enabled; nullable reference types enabled
- Web project: `Microsoft.NET.Sdk.Web`
- Class library projects: `Microsoft.NET.Sdk`

## Platform Requirements

**Development:**
- Visual Studio 2024 or VS Code with C# extension
- .NET 10.0 SDK
- SQL Server 2022+ (localhost or networked)
  - Dev connection string: `Server=localhost;Database=QuestBoard;User Id=QuestBoardUser;Password=QuestBoardUser!;Trusted_Connection=true;TrustServerCertificate=true;`
- Docker (optional, for containerized SQL Server: see docker-compose.yml)

**Production:**
- .NET 10.0 runtime
- SQL Server 2022 (or SQL Server 2019 compatible)
  - Production connection string uses service name: `Server=sqlserver;Database=QuestBoard;User Id=sa;Password=${MSSQL_SA_PASSWORD};TrustServerCertificate=true;`
- SMTP server reachable (for email delivery) or Resend API key
- Docker & Docker Compose (single container deployment)
- Reverse proxy support: Traefik or similar (optional; configure `ReverseProxy__KnownProxies` if used)

## Architecture Highlights

**Three-Layer Clean Architecture:**
1. `QuestBoard.Service` - MVC controllers, views, authorization handlers, background job definitions
2. `QuestBoard.Domain` - Business logic, service interfaces, models, AutoMapper profiles
3. `QuestBoard.Repository` - EF Core entities, repositories, DbContext, migrations

**Strict Dependency Direction:**
- Service → Domain → Repository
- No circular dependencies
- Entity Framework packages confined to Repository layer only

**Dependency Injection:**
- Service registration in extension methods: `AddRepositoryServices()`, `AddDomainServices()`, `AddControllersWithViews()`
- Scoped lifetime for DbContext, repositories, and most services
- Session-scoped active group context for multi-tenancy

---

*Stack analysis: 2026-07-01*
