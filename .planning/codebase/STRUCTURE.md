# Codebase Structure

**Analysis Date:** 2026-07-03
**Last Mapped Commit:** e5b37a73cda29bf355c4de6ebf4663b1625c3cf6

## Directory Layout

```
quest-board/ (project root)
├── QuestBoard.Service/                    # ASP.NET Core 10 MVC service layer
│   ├── Controllers/
│   │   ├── QuestBoard/                    # Feature: quest management
│   │   │   ├── HomeController.cs
│   │   │   ├── QuestController.cs
│   │   │   ├── QuestLogController.cs
│   │   │   ├── CalendarController.cs
│   │   │   └── PlayersController.cs
│   │   ├── Admin/                         # Feature: admin panel (user/account management)
│   │   │   ├── AccountController.cs       # Login, password reset, email confirmation
│   │   │   ├── AdminController.cs         # User CRUD, email preview
│   │   │   └── EmailPreviewController.cs
│   │   ├── Characters/                    # Feature: character/guild member management
│   │   │   └── GuildMembersController.cs
│   │   ├── DungeonMaster/                 # Feature: DM profile management
│   │   │   └── DungeonMasterController.cs
│   │   ├── Shop/                          # Feature: shop/trade items
│   │   │   ├── ShopController.cs
│   │   │   └── ShopManagementController.cs
│   │   └── GroupPickerController.cs       # Group selection UI
│   ├── Areas/
│   │   └── Platform/                      # SuperAdmin area: /platform/*
│   │       ├── Controllers/
│   │       │   └── GroupController.cs     # Group CRUD + membership management
│   │       └── Views/
│   │           ├── Group/
│   │           │   ├── Index.cshtml
│   │           │   ├── Create.cshtml
│   │           │   ├── Edit.cshtml
│   │           │   ├── Delete.cshtml
│   │           │   └── Members.cshtml
│   │           └── Shared/
│   ├── Views/                             # Razor views (organized by controller)
│   │   ├── Shared/                        # Layout, partial views, error pages
│   │   ├── Account/                       # Login, password reset, profile
│   │   ├── Admin/                         # Admin dashboard, user list/edit
│   │   ├── Quest/                         # Quest index, create, edit, details
│   │   ├── QuestLog/                      # Quest history/recap
│   │   ├── Calendar/                      # Quest calendar view
│   │   ├── Shop/                          # Shop browsing, item details
│   │   ├── ShopManagement/                # Shop admin (create/edit items)
│   │   ├── Characters/                    # Character listing
│   │   ├── GuildMembers/                  # Guild member management
│   │   ├── DungeonMaster/                 # DM profile view/edit
│   │   ├── GroupPicker/                   # Group selection UI
│   │   ├── Home/                          # Landing page
│   │   ├── Players/                       # Player directory
│   │   ├── _ViewStart.cshtml              # Layout initialization
│   │   └── _ViewImports.cshtml            # Global using statements
│   ├── ViewModels/                        # ViewModel (input/output, controller ↔ view)
│   │   ├── AccountViewModels/
│   │   ├── AdminViewModels/
│   │   ├── CalendarViewModels/
│   │   ├── CharacterViewModels/
│   │   ├── DungeonMasterViewModels/
│   │   ├── GroupPickerViewModels/
│   │   ├── GuildMembersViewModels/
│   │   ├── PlatformViewModels/
│   │   ├── QuestLogViewModels/
│   │   ├── QuestViewModels/
│   │   └── ShopViewModels/
│   ├── Authorization/                     # Policy handlers, requirements
│   │   ├── DungeonMasterHandler.cs        # Checks if user is DM in active group
│   │   ├── DungeonMasterRequirement.cs
│   │   ├── AdminHandler.cs                # Checks if user is admin in active group
│   │   ├── AdminRequirement.cs
│   │   └── AdminDashboardAuthFilter.cs    # Hangfire dashboard auth
│   ├── Middleware/                        # ASP.NET Core middleware
│   │   ├── GroupSessionMiddleware.cs      # Enforces group session; redirects to picker
│   │   └── MobileDetectionMiddleware.cs   # Mobile/desktop view selection
│   ├── Services/                          # Service-layer services (Service project only)
│   │   ├── ActiveGroupContextService.cs   # Reads/writes active group from session
│   │   ├── HangfireQuestEmailDispatcher.cs
│   │   ├── HangfireReminderJobDispatcher.cs
│   │   ├── NullQuestEmailDispatcher.cs    # No-op for testing
│   │   ├── NullReminderJobDispatcher.cs
│   │   ├── RazorEmailRenderService.cs
│   │   ├── ResendStatsClient.cs           # Resend API stats client
│   │   └── ResendStatsAggregator.cs
│   ├── Jobs/                              # Hangfire background job implementations
│   │   ├── QuestFinalizedEmailJob.cs      # Quest finalization emails
│   │   ├── DailyReminderJob.cs            # Daily session reminders (09:00 CET)
│   │   ├── SessionReminderJob.cs          # Session reminder logic
│   │   ├── ForgotPasswordEmailJob.cs      # Password reset emails
│   │   ├── QuestDateChangedEmailJob.cs
│   │   ├── ChangeEmailConfirmationJob.cs
│   │   ├── WelcomeEmailJob.cs
│   │   └── HangfireJobHelper.cs           # DI scope + group context setup
│   ├── Components/
│   │   └── Emails/                        # Email template components (Razor)
│   │       ├── QuestFinalized.razor
│   │       ├── SessionReminder.razor
│   │       └── [others]
│   ├── Automapper/
│   │   └── ViewModelProfile.cs            # DomainModel ↔ ViewModel mappings
│   ├── Constants/
│   │   └── SessionKeys.cs                 # Session key string constants
│   ├── Extensions/
│   │   ├── ControllerExtensions.cs
│   │   └── ConfigurationDebugExtensions.cs
│   ├── ViewExpanders/
│   │   └── MobileViewLocationExpander.cs  # View location for mobile/desktop
│   ├── Program.cs                         # Startup; DI, middleware, routing config
│   ├── appsettings.json                   # Default configuration
│   ├── appsettings.Development.json
│   └── appsettings.Production.json
│
├── QuestBoard.Domain/                     # Business logic layer
│   ├── Services/                          # Business logic implementations
│   │   ├── QuestService.cs                # Quest creation, finalization, email dispatch
│   │   ├── PlayerSignupService.cs
│   │   ├── GroupService.cs                # Group CRUD, membership queries
│   │   ├── UserService.cs                 # User queries, role resolution
│   │   ├── CharacterService.cs
│   │   ├── DungeonMasterProfileService.cs
│   │   ├── ShopService.cs
│   │   ├── ShopSeedService.cs             # Seeds basic equipment for groups
│   │   ├── EmailService.cs                # Email sending (SMTP/Resend)
│   │   └── BaseService.cs                 # Base class for CRUD services
│   ├── Interfaces/                        # Service & repository contracts
│   │   ├── IQuestService.cs
│   │   ├── IPlayerSignupService.cs
│   │   ├── IGroupService.cs
│   │   ├── IUserService.cs
│   │   ├── ICharacterService.cs
│   │   ├── IDungeonMasterProfileService.cs
│   │   ├── IShopService.cs
│   │   ├── IShopSeedService.cs
│   │   ├── IEmailService.cs
│   │   ├── IEmailRenderService.cs
│   │   ├── IQuestRepository.cs
│   │   ├── IPlayerSignupRepository.cs
│   │   ├── IGroupRepository.cs
│   │   ├── IUserRepository.cs
│   │   ├── ICharacterRepository.cs
│   │   ├── IDungeonMasterProfileRepository.cs
│   │   ├── IShopRepository.cs
│   │   ├── IUserTransactionRepository.cs
│   │   ├── ITradeItemRepository.cs
│   │   ├── IReminderLogRepository.cs
│   │   ├── IBaseRepository.cs
│   │   ├── IBaseService.cs
│   │   ├── IActiveGroupContext.cs         # Group scoping interface
│   │   ├── IIdentityService.cs            # Wraps UserManager/SignInManager
│   │   ├── IQuestEmailDispatcher.cs       # Enqueue finalized emails
│   │   └── IReminderJobDispatcher.cs      # Enqueue reminder emails
│   ├── Models/                            # Domain models (POCO, no EF annotations)
│   │   ├── IModel.cs                      # Base interface (Id property)
│   │   ├── User.cs
│   │   ├── Group.cs
│   │   ├── UserGroup.cs                   # User membership in group
│   │   ├── Character.cs
│   │   ├── DungeonMasterProfile.cs
│   │   ├── IModel.cs
│   │   ├── EmailSettings.cs               # Email configuration POCO
│   │   ├── ServiceResult.cs
│   │   ├── GroupWithMemberCount.cs        # DTO for group listing
│   │   ├── QuestBoard/                    # Quest-related models
│   │   │   ├── Quest.cs
│   │   │   ├── PlayerSignup.cs
│   │   │   ├── ProposedDate.cs
│   │   │   ├── PlayerDateVote.cs
│   │   │   └── ReminderLog.cs
│   │   └── Shop/                          # Shop-related models
│   │       ├── ShopItem.cs
│   │       ├── UserTransaction.cs
│   │       ├── TradeItem.cs
│   │       └── TransactionWithRemaining.cs
│   ├── Enums/                             # Enumeration types
│   │   ├── GroupRole.cs                   # Player=0, DungeonMaster=1, Admin=2
│   │   ├── Role.cs                        # Player, DungeonMaster, Admin (mirrors GroupRole)
│   │   ├── CharacterRole.cs
│   │   ├── CharacterStatus.cs
│   │   ├── DndClass.cs
│   │   ├── ItemRarity.cs
│   │   ├── ItemStatus.cs
│   │   ├── ItemType.cs
│   │   ├── SignupRole.cs
│   │   ├── TransactionType.cs
│   │   └── VoteType.cs
│   ├── Extensions/                        # Extension methods
│   │   ├── ServiceExtensions.cs           # AddDomainServices() DI method
│   │   ├── ActiveGroupContextExtensions.cs # RequireActiveGroupId() guard
│   │   └── UserExtensions.cs
│
├── QuestBoard.Repository/                 # Data access layer
│   ├── Entities/                          # EF Core entity classes (DB-mapped)
│   │   ├── IEntity.cs                     # Base interface (Id property)
│   │   ├── QuestBoardContext.cs           # DbContext; global query filters defined here
│   │   ├── UserEntity.cs                  # ASP.NET Identity user (unfiltered)
│   │   ├── GroupEntity.cs                 # Group tenant; no query filter
│   │   ├── UserGroupEntity.cs             # User-to-group membership (multi-tenancy)
│   │   ├── QuestEntity.cs                 # Quest (HAS query filter)
│   │   ├── PlayerSignupEntity.cs
│   │   ├── ProposedDateEntity.cs
│   │   ├── PlayerDateVoteEntity.cs
│   │   ├── ShopItemEntity.cs              # Shop item (HAS query filter)
│   │   ├── UserTransactionEntity.cs
│   │   ├── TradeItemEntity.cs
│   │   ├── CharacterEntity.cs
│   │   ├── CharacterImageEntity.cs
│   │   ├── CharacterClassEntity.cs
│   │   ├── DungeonMasterProfileEntity.cs
│   │   ├── DungeonMasterProfileImageEntity.cs
│   │   └── ReminderLogEntity.cs
│   ├── [Repositories]/                    # Concrete repository implementations
│   │   ├── BaseRepository.cs              # Base CRUD (AddAsync, GetByIdAsync, etc.)
│   │   ├── QuestRepository.cs             # Quest queries + finalization logic
│   │   ├── PlayerSignupRepository.cs
│   │   ├── GroupRepository.cs             # Group queries + membership
│   │   ├── UserRepository.cs
│   │   ├── CharacterRepository.cs
│   │   ├── DungeonMasterProfileRepository.cs
│   │   ├── ShopRepository.cs
│   │   ├── UserTransactionRepository.cs
│   │   ├── TradeItemRepository.cs
│   │   ├── ReminderLogRepository.cs
│   │   └── IdentityService.cs             # Wraps UserManager/SignInManager
│   ├── Automapper/
│   │   └── EntityProfile.cs               # Entity ↔ DomainModel mappings
│   ├── Migrations/                        # EF Core migration files (auto-generated)
│   │   ├── [timestamp]_InitialSqlServerNoAction.cs
│   │   ├── [timestamp]_InitialSqlServerNoAction.Designer.cs
│   │   ├── [timestamp]_EnableCascadeDeleteForPlayerDateVotes.cs
│   │   └── [...]
│   ├── Extensions/
│   │   └── ServiceExtensions.cs           # AddRepositoryServices() DI method
│
├── QuestBoard.UnitTests/                  # Unit test project
│   ├── Authorization/                     # Authorization handler tests
│   └── [feature]Tests.cs
│
├── QuestBoard.IntegrationTests/           # Integration test project
│   ├── WebApplicationFactory subclass
│   ├── [feature]IntegrationTests.cs
│   └── Fixtures/
│
├── .planning/
│   ├── codebase/                          # Architecture reference docs (this directory)
│   │   ├── ARCHITECTURE.md
│   │   ├── STRUCTURE.md
│   │   ├── CONVENTIONS.md
│   │   ├── TESTING.md
│   │   ├── STACK.md
│   │   ├── INTEGRATIONS.md
│   │   └── CONCERNS.md
│   └── ROADMAP.md                         # Milestone and phase planning
│
├── docs/                                  # Operational documentation
│   └── server-setup.md
│
├── .github/                               # GitHub configuration
│   ├── workflows/
│   │   ├── dotnet.yml                     # .NET CI pipeline (build, test)
│   │   ├── docker-publish.yml             # Docker image build & publish to ghcr.io
│   │   └── binary-release.yml             # Binary release workflow (if used)
│   └── ISSUE_TEMPLATE/
│       ├── bug_report.md                  # Bug report template
│       └── feature_request.md             # Feature request template
│
├── .config/
│   └── dotnet-tools.json                  # Local tool manifest (dotnet-ef v9.0.6)
│
├── .dockerignore                          # Docker build context exclusions
├── Dockerfile                             # Multi-stage container build
├── docker-compose.yml                     # Local dev & production deployment
├── create-migration.sh                    # Migration helper script (WSL/Git Bash)
│
├── QuestBoard.slnx                        # Modern solution file
├── .env                                   # Environment variables (secrets; NOT committed)
├── .env.example                           # Template for .env (committed)
├── CLAUDE.md                              # Project-specific Claude instructions
├── README.md                              # Project documentation
├── LICENSE                                # Project license
└── .gitignore                             # Git exclusions
```

## Directory Purposes

**QuestBoard.Service/**
- Purpose: ASP.NET Core 10 MVC web application; HTTP request handlers, views, DI orchestration.
- Contains: Controllers (request handlers), Views (Razor templates), ViewModels (presentation DTOs), Middleware, Authorization handlers, Hangfire job classes.
- Key files: `Program.cs` (startup config), `appsettings.json` (configuration).

**QuestBoard.Domain/**
- Purpose: Business logic and domain models; shared by Service and Repository layers.
- Contains: Service implementations, domain models (User, Quest, Group, etc.), service/repository interfaces, enums, validation rules.
- Key files: `Services/` (business logic), `Models/` (domain entities), `Interfaces/` (contracts).

**QuestBoard.Repository/**
- Purpose: Data access via EF Core; entity mappings, DbContext, migrations.
- Contains: EF Core entity classes, repositories (IRepository implementations), QuestBoardContext (DbContext with global query filters), AutoMapper Entity profile.
- Key files: `Entities/QuestBoardContext.cs` (query filters defined here), `Migrations/` (schema versions).

**Views/ (Service project)**
- Purpose: Razor templates rendered by controllers.
- Organization: Subdirectories mirror controller names (e.g., `Views/Quest/Index.cshtml` served by `QuestController.Index`).
- Layout: `Shared/_Layout.cshtml` wraps all views (defined in `_ViewStart.cshtml`).

**Areas/Platform/ (Service project)**
- Purpose: SuperAdmin-only area for group management and system administration.
- Route prefix: `/platform/` (mapped in `Program.cs` line 318–321).
- Contains: `GroupController` (group CRUD), views for group index/create/edit/delete/members.

**Authorization/ (Service project)**
- Purpose: Policy handlers and requirements for role-based access control.
- Patterns: `DungeonMasterRequirement` + `DungeonMasterHandler` (checks if user is DM in active group), `AdminRequirement` + `AdminHandler` (checks if user is admin in active group).
- Usage: Controllers decorated with `[Authorize(Policy = "DungeonMasterOnly")]` etc.

**Middleware/ (Service project)**
- Purpose: ASP.NET Core middleware for cross-cutting concerns.
- Key: `GroupSessionMiddleware` enforces that authenticated, non-SuperAdmin users have an active group; redirects to group picker if missing.

**Jobs/ (Service project)**
- Purpose: Hangfire background job implementations; executed by the Hangfire scheduler.
- Pattern: Each job class has an `ExecuteAsync()` method; uses `HangfireJobHelper.RunInScopeAsync()` to set group context.

**Entities/ (Repository project)**
- Purpose: EF Core entity classes; mapped directly to SQL Server tables.
- Global query filters: `QuestEntity` and `ShopItemEntity` have `HasQueryFilter()` applied in `QuestBoardContext.OnModelCreating()`.
- Special: `UserEntity` is intentionally unfiltered (ASP.NET Identity requires unrestricted user queries).

**Migrations/ (Repository project)**
- Purpose: EF Core migration files (one per schema change).
- Auto-applied: `context.Database.Migrate()` runs on startup (Program.cs line 332).
- Do NOT edit by hand; use `dotnet ef migrations add MigrationName`.

**.github/workflows/ (Deployment)**
- Purpose: GitHub Actions CI/CD automation.
- Key workflows:
  - `dotnet.yml`: Restores packages, builds Service project, runs unit + integration tests on push to main and PRs.
  - `docker-publish.yml`: Builds multi-stage Dockerfile, publishes `ghcr.io/theunschut/dnd-quest-board:{tag}` on semver tag push, signs image with Cosign.
  - `binary-release.yml`: (Optional) Distributes standalone binaries.

**.config/dotnet-tools.json (Tool Manifest)**
- Purpose: Declares `dotnet-ef` v9.0.6 as a local tool for the project.
- Usage: Allows `dotnet ef` commands without global installation.
- Managed by: `dotnet tool install` / `dotnet tool update` commands.

**.dockerignore**
- Purpose: Specifies files/directories excluded from Docker build context.
- Excludes: node_modules, compiled output (bin/obj), IDE settings, test projects, Docker files themselves, git history.
- Benefit: Reduces context size sent to Docker daemon; faster builds.

**Dockerfile**
- Purpose: Multi-stage container image definition.
- Stages:
  1. `base`: ASP.NET Core 10 runtime (final runtime environment)
  2. `build`: SDK image; restores packages, builds Release configuration
  3. `publish`: Publishes Release build to `/app/publish` (stripped of debug symbols)
  4. `final`: Copies artifacts to runtime image; sets environment + entrypoint
- Entrypoint: `dotnet QuestBoard.Service.dll`
- Environment: `ASPNETCORE_ENVIRONMENT=Production`, `ASPNETCORE_URLS=http://+:8080`

**docker-compose.yml**
- Purpose: Defines development and production deployment stack.
- Services:
  - `questboard`: Application container (port 7080 external → 8080 internal), depends on SQL Server.
  - `sqlserver`: SQL Server 2022 container (port 1433, persistent volume `sqlserver_data`).
- Network: External `net-dnd` (must exist or be auto-created).
- Configuration: Environment variables for DB connection string, email settings (commented out template).
- Health check: HTTP GET `/health` with retries and 40s start grace period.
- Restart: Unless-stopped (survives host reboot).

**create-migration.sh (Migration Helper)**
- Purpose: Documents the EF Core migration workflow for Git Bash / WSL.
- Steps: Navigates to Service project, ensures Microsoft.EntityFrameworkCore.Tools is installed, runs `dotnet ef migrations add`.
- Usage: Executed manually when schema changes are needed (not part of CI/CD).

**QuestBoard.slnx (Solution File)**
- Purpose: Modern solution format for IDE navigation and batch builds.
- Structure:
  - Folder `/Tests/`: Unit + Integration test projects
  - Production projects: Domain → Repository → Service (dependency order)
- Platforms: Any CPU, x64, x86 (for multi-platform builds).

**CLAUDE.md (Project Instructions)**
- Purpose: Guides Claude Code when working in this repository.
- Contains: Platform guidance (Windows paths, CRLF), branching rules, development commands, architecture overview, EF/AutoMapper patterns, UI guidelines, reference docs.

**README.md**
- Purpose: Entry point for developers; quick-start guide, tech stack, local dev setup.
- Key: Explains Docker Compose usage, .NET 10 SDK requirement, GitHub Actions badge links.

**LICENSE**
- Purpose: Legal license for the project (e.g., MIT, GPL).

## Key File Locations

**Entry Points:**
- `QuestBoard.Service/Program.cs` — Startup; DI container, middleware pipeline, route configuration.
- `QuestBoard.Service/Controllers/QuestBoard/HomeController.cs` — First controller hit by browser; redirects to quest board or login.
- `QuestBoard.Service/Controllers/GroupPickerController.cs` — Group selection UI; stores `ActiveGroupId` in session.

**Deployment & Configuration:**
- `Dockerfile` — Multi-stage build definition (base → build → publish → final).
- `docker-compose.yml` — Stack definition for production deployment (QuestBoard + SQL Server).
- `.dockerignore` — Build context optimization.
- `.config/dotnet-tools.json` — Declares `dotnet-ef` tool.
- `create-migration.sh` — Helper script for creating EF migrations.

**CI/CD Pipelines:**
- `.github/workflows/dotnet.yml` — Build, restore, test pipeline (on main push / PRs).
- `.github/workflows/docker-publish.yml` — Docker image build & publish (on semver tag).
- `.github/workflows/binary-release.yml` — Binary release (optional).

**Configuration:**
- `QuestBoard.Service/appsettings.json` — Default settings (connection string, email, Hangfire, logging).
- `QuestBoard.Service/appsettings.Development.json` — Local development overrides.
- `QuestBoard.Service/appsettings.Production.json` — Production overrides.
- `.env` — Environment variables (secrets; NOT committed).
- `.env.example` — Template showing required keys; committed (no values).
- `CLAUDE.md` — Project-specific instructions for Claude.

**Core Logic:**
- `QuestBoard.Domain/Services/` — Business logic (QuestService, GroupService, UserService, etc.).
- `QuestBoard.Repository/Entities/QuestBoardContext.cs` — Global query filters for group isolation.
- `QuestBoard.Service/Services/ActiveGroupContextService.cs` — Group context management (session + job override).
- `QuestBoard.Service/Middleware/GroupSessionMiddleware.cs` — Session validation and group picker redirect.

**Testing:**
- `QuestBoard.UnitTests/` — Unit tests (services, handlers, business logic).
- `QuestBoard.IntegrationTests/` — Integration tests (full HTTP stack, database).
- Test fixtures and factories in `IntegrationTests/Fixtures/`.

**Automapper:**
- `QuestBoard.Repository/Automapper/EntityProfile.cs` — Entity ↔ DomainModel mappings.
- `QuestBoard.Service/Automapper/ViewModelProfile.cs` — DomainModel ↔ ViewModel mappings.

**Solution & Project Files:**
- `QuestBoard.slnx` — Modern solution file (groups tests, orders projects by dependency).

## Naming Conventions

**Files:**
- **Controllers:** `[Feature]Controller.cs` — e.g., `QuestController.cs`, `GroupPickerController.cs`.
- **Services (Domain):** `[Feature]Service.cs` — e.g., `QuestService.cs`, `GroupService.cs`.
- **Repositories:** `[Feature]Repository.cs` — e.g., `QuestRepository.cs`, `GroupRepository.cs`.
- **ViewModels:** `[Purpose]ViewModel.cs` — e.g., `QuestViewModel.cs`, `EditUserViewModel.cs`.
- **Entities:** `[Feature]Entity.cs` — e.g., `QuestEntity.cs`, `UserGroupEntity.cs`.
- **Migrations:** `[Timestamp]_[Description].cs` — auto-generated by EF tooling.
- **Razor templates:** `[Action].cshtml` — e.g., `Index.cshtml`, `Create.cshtml`, `Edit.cshtml`.
- **Jobs:** `[Purpose]EmailJob.cs` or `[Purpose]Job.cs` — e.g., `QuestFinalizedEmailJob.cs`, `DailyReminderJob.cs`.

**Directories:**
- **Controllers:** `Controllers/[Feature]/` — e.g., `Controllers/QuestBoard/`, `Controllers/Admin/`.
- **Views:** `Views/[Feature]/` — e.g., `Views/Quest/`, `Views/Admin/`.
- **ViewModels:** `ViewModels/[Feature]ViewModels/` — e.g., `ViewModels/QuestViewModels/`, `ViewModels/AdminViewModels/`.
- **Services (Domain):** `Services/` (no subdirs; prefix class names with feature).
- **Repositories:** Root of `Repository` project (no subdirs; prefix class names with feature).
- **Entities:** `Entities/` (all in one directory; prefix class names with feature).

**Namespaces:**
- Service layer: `QuestBoard.Service`, `QuestBoard.Service.Controllers`, `QuestBoard.Service.ViewModels.QuestViewModels`, etc.
- Domain layer: `QuestBoard.Domain`, `QuestBoard.Domain.Services`, `QuestBoard.Domain.Interfaces`, etc.
- Repository layer: `QuestBoard.Repository`, `QuestBoard.Repository.Entities`, etc.

## Where to Add New Code

**New Feature (e.g., Quest Scheduling):**
1. **Controller:** `QuestBoard.Service/Controllers/QuestBoard/[Feature]Controller.cs`
2. **Views:** `QuestBoard.Service/Views/[Feature]/Index.cshtml`, `Create.cshtml`, etc.
3. **ViewModels:** `QuestBoard.Service/ViewModels/[Feature]ViewModels/[Purpose]ViewModel.cs`
4. **Domain Model:** `QuestBoard.Domain/Models/QuestBoard/[Feature].cs` (or `Models/[Feature].cs` if not quest-related)
5. **Domain Service:** `QuestBoard.Domain/Services/[Feature]Service.cs` implementing `I[Feature]Service` interface in `QuestBoard.Domain/Interfaces/I[Feature]Service.cs`
6. **Repository:** `QuestBoard.Repository/[Feature]Repository.cs` implementing `I[Feature]Repository` interface in `QuestBoard.Domain/Interfaces/I[Feature]Repository.cs`
7. **Entity:** `QuestBoard.Repository/Entities/[Feature]Entity.cs`
8. **Automapper profiles:** Add mappings to `EntityProfile.cs` (Entity ↔ Model) and `ViewModelProfile.cs` (Model ↔ ViewModel)
9. **DI registration:** If service/repo, add to `QuestBoard.Domain/Extensions/ServiceExtensions.cs` and `QuestBoard.Repository/Extensions/ServiceExtensions.cs` respectively
10. **Tests:** `QuestBoard.UnitTests/[Feature]Tests.cs` and/or `QuestBoard.IntegrationTests/[Feature]IntegrationTests.cs`

**New Component (e.g., Modal, Card, Partial):**
1. Create `QuestBoard.Service/Views/Shared/_[ComponentName].cshtml`
2. Reference in parent view with `@Html.PartialAsync("_[ComponentName]", model)`

**Utility/Extension:**
1. If domain-level: `QuestBoard.Domain/Extensions/[Purpose]Extensions.cs`
2. If controller-level: `QuestBoard.Service/Extensions/ControllerExtensions.cs`
3. If repository-level: Add to existing repository or create domain-level interface + Repository extension

**Authorization Policy:**
1. Create `QuestBoard.Service/Authorization/[Policy]Requirement.cs` (the requirement class)
2. Create `QuestBoard.Service/Authorization/[Policy]Handler.cs` (the handler that checks the requirement)
3. Register in `Program.cs` under `AddAuthorizationBuilder().AddPolicy(...)`

**Background Job:**
1. Create `QuestBoard.Service/Jobs/[Purpose]Job.cs` with `ExecuteAsync()` method
2. Use `HangfireJobHelper.RunInScopeAsync(scopeFactory, groupId, async sp => {...})` to set group context
3. Register in `Program.cs` or trigger via `IBackgroundJobClient.Enqueue<[Job]>(j => j.ExecuteAsync(...))`

**Email Template:**
1. Create `QuestBoard.Service/Components/Emails/[Purpose].razor` (Razor component)
2. Reference in job via `RenderAsync<[Purpose]>(Dictionary<string, object?> data)`

**Database Schema Change:**
1. Add/modify entity class in `QuestBoard.Repository/Entities/[Feature]Entity.cs`
2. Update entity relationships in `QuestBoardContext.OnModelCreating()` if needed
3. Run `dotnet ef migrations add [Description]` from `QuestBoard.Service/` directory (generates migration file)
4. Migration auto-applies on app startup

**Middleware:**
1. Create `QuestBoard.Service/Middleware/[Purpose]Middleware.cs`
2. Register in `Program.cs` with `app.UseMiddleware<[Purpose]Middleware>()`
3. Middleware runs in registration order; place before dependent middleware

**Session Data:**
1. Add key constant to `QuestBoard.Service/Constants/SessionKeys.cs`
2. Use `HttpContext.Session.SetInt32(SessionKeys.[Key], value)` in controllers
3. Read via `HttpContext.Session.GetInt32(SessionKeys.[Key])`

**Docker Build Optimization:**
1. Add exclusions to `.dockerignore` (node_modules, bin/obj, git files, test projects, etc.)
2. Use `--mount=type=cache` in Dockerfile RUN commands for NuGet package caching
3. Multi-stage builds: runtime image in `base` stage is smaller; `build` stage discarded after publish

**GitHub Actions Workflow:**
1. Create `.github/workflows/[purpose].yml` with YAML syntax
2. Define triggers (`on: push`, `on: pull_request`, `on: schedule`, etc.)
3. Jobs run on ubuntu-latest (or other runner); steps execute sequentially
4. Use `actions/checkout@v4`, `actions/setup-dotnet@v4`, and standard CLI commands
5. Secrets referenced as `${{ secrets.SECRET_NAME }}`; must be registered in GitHub project settings

## Special Directories

**Migrations/:**
- Purpose: EF Core schema version history (auto-generated, one per `dotnet ef migrations add`).
- Generated: Yes (by `dotnet ef migrations add` CLI command).
- Committed: Yes (part of schema version control).
- **Do NOT edit by hand** — regenerate via CLI if a mistake is made.

**bin/ and obj/:**
- Purpose: Build artifacts and compiled output.
- Generated: Yes (by `dotnet build`).
- Committed: No (in `.gitignore`).

**Properties/:**
- Purpose: Project metadata and build settings.
- Generated: Partly (project GUID auto-generated on project creation).
- Committed: Yes (contains .csproj metadata).

**.env and appsettings files:**
- `.env` — Secrets (connection string, API keys); NOT committed.
- `.env.example` — Template showing required keys; committed (no values).
- `appsettings.json` — Default settings; committed.
- `appsettings.Development.json` — Dev overrides (can be NOT committed if it contains secrets).
- `appsettings.Production.json` — Production overrides; NOT committed (secrets).

**.github/workflows/:**
- Purpose: GitHub Actions CI/CD automation files.
- Committed: Yes (part of repo).
- Auto-triggered: On push to main, pull requests, scheduled crons, or tag pushes (per workflow).

---

*Structure analysis: 2026-07-03*
