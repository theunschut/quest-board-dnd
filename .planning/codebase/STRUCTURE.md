# Codebase Structure

**Analysis Date:** 2026-07-01

## Directory Layout

```
quest-board/
├── QuestBoard.Service/              # MVC Service layer — HTTP entry point
│   ├── Areas/Platform/              # Group/tenant management area
│   │   ├── Controllers/             # GroupController
│   │   └── Views/                   # Group views (picker, listing, etc.)
│   ├── Authorization/               # Policy handlers & requirements
│   ├── Automapper/                  # ViewModelProfile (ViewModel ↔ DomainModel)
│   ├── Components/                  # Razor components (email templates)
│   │   └── Emails/                  # Email component files (QuestFinalized, etc.)
│   ├── Constants/                   # SessionKeys enum
│   ├── Controllers/                 # MVC controllers by feature
│   │   ├── Admin/                   # AccountController, AdminController, EmailPreviewController
│   │   ├── Characters/              # GuildMembersController
│   │   ├── DungeonMaster/           # DungeonMasterController
│   │   ├── QuestBoard/              # QuestController, CalendarController, PlayersController, HomeController, QuestLogController
│   │   └── Shop/                    # ShopController, ShopManagementController
│   ├── Extensions/                  # ConfigurationDebugExtensions
│   ├── Jobs/                        # Hangfire background jobs
│   ├── Middleware/                  # GroupSessionMiddleware, MobileDetectionMiddleware
│   ├── Services/                    # Service-layer only (email dispatch, rendering, group context)
│   ├── ViewExpanders/               # MobileViewLocationExpander
│   ├── ViewModels/                  # ViewModel classes by feature
│   │   ├── AccountViewModels/       # Login, Register, ChangePassword, etc.
│   │   ├── AdminViewModels/         # UserManagement, EmailStats, CreateUser, etc.
│   │   ├── CalendarViewModels/      # CalendarViewModel, QuestOnDay, CalendarDay
│   │   ├── CharacterViewModels/     # CharacterViewModel, CharacterIndexViewModel
│   │   ├── DungeonMasterViewModels/ # DMProfileViewModel, EditDMProfileViewModel
│   │   ├── GuildMembersViewModels/  # GuildMembersViewModel
│   │   ├── GroupPickerViewModels/   # GroupPickerViewModel
│   │   ├── PlatformViewModels/      # GroupViewModel, GroupListViewModel
│   │   ├── QuestLogViewModels/      # QuestLogViewModel
│   │   ├── QuestViewModels/         # QuestViewModel
│   │   └── ShopViewModels/          # ShopItemViewModel, ShopViewModel, UserTransactionViewModel
│   ├── Views/                       # Razor view (.cshtml) files by feature
│   │   ├── Account/                 # Login, Register, ForgotPassword, SetPassword, Profile views
│   │   ├── Admin/                   # User management, email preview, stats views
│   │   ├── Calendar/                # Calendar view
│   │   ├── DungeonMaster/           # DM profile views
│   │   ├── GroupPicker/             # Group selection view
│   │   ├── GuildMembers/            # Guild members views
│   │   ├── Home/                    # Home/Index view
│   │   ├── Players/                 # Player views (quest listing, availability)
│   │   ├── Quest/                   # Quest detail, create, edit, finalize views
│   │   ├── QuestLog/                # Quest log view
│   │   ├── Shared/                  # Layout, navigation, error partials
│   │   ├── Shop/                    # Shop browsing, transaction history
│   │   └── ShopManagement/          # Item creation, editing, management
│   ├── wwwroot/                     # Static assets
│   │   ├── css/                     # Stylesheets
│   │   ├── images/                  # D&D theme images, wax seals, character blanks
│   │   └── js/                      # Client-side JavaScript
│   ├── Program.cs                   # ASP.NET Core app startup, service registration, middleware config
│   ├── appsettings.json             # Configuration, logging, database connection
│   └── QuestBoard.Service.csproj    # Project file
│
├── QuestBoard.Domain/               # Business logic layer
│   ├── Enums/                       # Role, DndClass, CharacterStatus, ItemRarity, ItemStatus, ItemType, CharacterRole, GroupRole, SignupRole, TransactionType, VoteType
│   ├── Interfaces/                  # Service & Repository contracts
│   │   ├── IBaseService.cs          # Generic CRUD interface
│   │   ├── IBaseRepository.cs       # Generic data access interface
│   │   ├── IQuestService.cs         # Quest business operations
│   │   ├── IQuestRepository.cs      # Quest data access
│   │   ├── IPlayerSignupService.cs  # Signup business operations
│   │   ├── IQuestEmailDispatcher.cs # Email job dispatch (Domain → Service layer bridge)
│   │   ├── IActiveGroupContext.cs   # Multi-tenancy context (session/job group ID)
│   │   ├── IEmailService.cs         # Email sending contract
│   │   ├── IEmailRenderService.cs   # Email template rendering
│   │   ├── IUserService.cs          # User queries, role checks
│   │   ├── IShopService.cs          # Shop business logic
│   │   ├── ICharacterService.cs     # Character CRUD
│   │   ├── IDungeonMasterProfileService.cs
│   │   ├── IGroupService.cs         # Group/tenant management
│   │   ├── IIdentityService.cs      # Password hashing, token generation
│   │   └── [other repositories...]  # ICharacterRepository, IUserRepository, IShopRepository, ITradeItemRepository, IUserTransactionRepository, etc.
│   ├── Models/                      # Domain entity models (untracked DTOs)
│   │   ├── QuestBoard/              # Quest, PlayerSignup, ProposedDate, PlayerDateVote, ReminderLog
│   │   ├── Shop/                    # ShopItem, UserTransaction, TradeItem
│   │   ├── User.cs                  # User domain model
│   │   ├── Character.cs             # Character domain model
│   │   ├── DungeonMasterProfile.cs  # DM profile
│   │   ├── Group.cs                 # Group (tenant) model
│   │   ├── UserGroup.cs             # User group membership
│   │   ├── IModel.cs                # Marker interface
│   │   └── [other models...]        # EmailSettings, ServiceResult, GroupWithMemberCount
│   ├── Services/                    # Service implementations (business logic)
│   │   ├── BaseService.cs           # Generic CRUD operations base class
│   │   ├── QuestService.cs          # Quest creation, finalization, email dispatch coordination
│   │   ├── PlayerSignupService.cs   # Signup registration, voting, removal
│   │   ├── ShopService.cs           # Shop item listing, purchase logic, gold accounting
│   │   ├── UserService.cs           # User queries, authentication helpers
│   │   ├── CharacterService.cs      # Character CRUD
│   │   ├── GroupService.cs          # Group creation, member management
│   │   └── [other services...]
│   ├── Extensions/                  # ServiceExtensions (DI registration), UserExtensions (IsInRole helpers)
│   └── QuestBoard.Domain.csproj
│
├── QuestBoard.Repository/           # Data access layer (EF Core)
│   ├── Entities/                    # EF Core entity models (tracked by DbContext)
│   │   ├── QuestEntity.cs           # Maps to [Quests] table
│   │   ├── UserEntity.cs            # Maps to [AspNetUsers] table (Identity)
│   │   ├── PlayerSignupEntity.cs    # Maps to [PlayerSignups] table
│   │   ├── ProposedDateEntity.cs    # Maps to [ProposedDates] table
│   │   ├── PlayerDateVoteEntity.cs  # Maps to [PlayerDateVotes] table
│   │   ├── ShopItemEntity.cs        # Maps to [ShopItems] table
│   │   ├── UserTransactionEntity.cs # Maps to [UserTransactions] table
│   │   ├── TradeItemEntity.cs       # Maps to [TradeItems] table
│   │   ├── CharacterEntity.cs       # Maps to [Characters] table
│   │   ├── CharacterImageEntity.cs  # Maps to [CharacterImages] table
│   │   ├── CharacterClassEntity.cs  # Maps to [CharacterClasses] table (many-to-many join)
│   │   ├── DungeonMasterProfileEntity.cs
│   │   ├── DungeonMasterProfileImageEntity.cs
│   │   ├── GroupEntity.cs           # Maps to [Groups] table (tenant)
│   │   ├── UserGroupEntity.cs       # Maps to [UserGroups] table (user group membership)
│   │   ├── ReminderLogEntity.cs     # Maps to [ReminderLogs] table
│   │   ├── IEntity.cs               # Marker interface
│   │   └── QuestBoardContext.cs     # EF DbContext, schema config, group filter integration
│   ├── Automapper/                  # EntityProfile (Entity ↔ DomainModel mapping)
│   ├── BaseRepository.cs            # Generic CRUD base class (Add, Get, Update, Remove, AutoMapper)
│   ├── [Repository Classes]         # Specializations for complex queries
│   │   ├── QuestRepository.cs       # GetQuestsWithDetails, GetQuestsForCalendar, FinalizeQuest
│   │   ├── PlayerSignupRepository.cs
│   │   ├── ShopRepository.cs        # Paginated item listing
│   │   ├── UserRepository.cs
│   │   ├── CharacterRepository.cs
│   │   ├── GroupRepository.cs
│   │   ├── IdentityService.cs       # Password hashing, token generation (IIdentityService impl)
│   │   └── [other repositories...]
│   ├── Migrations/                  # EF Core migration files (auto-applied on startup)
│   │   ├── Migration files          # One .cs per schema change
│   │   └── QuestBoardContextModelSnapshot.cs
│   ├── Extensions/                  # ServiceExtensions (DI registration for Repository services)
│   ├── Interfaces/                  # Repository interface definitions (e.g., IBaseRepository — mirrors Domain/Interfaces)
│   └── QuestBoard.Repository.csproj
│
├── QuestBoard.IntegrationTests/     # Integration test suite
│   ├── Controllers/                 # Controller tests
│   ├── Services/                    # Service tests
│   ├── Repositories/                # Repository tests
│   ├── Fixtures/                    # Test data factories, WebApplicationFactory
│   ├── appsettings.Testing.json     # Test-specific config (in-memory DB or test SQL Server)
│   └── QuestBoard.IntegrationTests.csproj
│
├── QuestBoard.UnitTests/            # Unit test suite (if present — not heavily used; integration tests preferred)
│   └── QuestBoard.UnitTests.csproj
│
├── .planning/                       # GSD planning documents
│   ├── codebase/                    # Codebase analysis documents
│   │   ├── ARCHITECTURE.md          # This file
│   │   ├── STRUCTURE.md             # Directory layout, file locations
│   │   ├── CONVENTIONS.md           # Code style, naming patterns
│   │   ├── STACK.md                 # Technology versions, dependencies
│   │   ├── INTEGRATIONS.md          # External APIs, databases
│   │   ├── TESTING.md               # Testing framework, patterns
│   │   └── CONCERNS.md              # Technical debt, known issues
│   ├── milestones/                  # Historical phase documentation
│   ├── phases/                      # Current phase work
│   ├── quick/                       # Quick fix notes
│   ├── STATE.md                     # Project state: completed phases, current milestone
│   └── ROADMAP.md                   # Future roadmap
│
├── docs/                            # Documentation
│   └── [deployment, setup, architecture guides]
│
├── Dockerfile                       # Single-container deployment
├── docker-compose.yml               # Local dev: Quest Board + SQL Server
├── .dockerignore
├── appsettings.json                 # Production config defaults
├── CLAUDE.md                        # Claude Code guidance (this project)
└── QuestBoard.slnx                  # Solution file

```

## Directory Purposes

**QuestBoard.Service/ (MVC Service Layer):**
- Purpose: HTTP request handling, authorization, view rendering, background job dispatch
- Contains: Controllers, Views, ViewModels, Middleware, Authorization, Jobs
- Key files: `Program.cs` (app startup), `Middleware/GroupSessionMiddleware.cs` (multi-tenancy gate), `Controllers/QuestBoard/QuestController.cs` (primary quest operations), `Jobs/` (Hangfire background jobs)

**QuestBoard.Domain/ (Business Logic Layer):**
- Purpose: Domain models, service contracts, business rule implementation
- Contains: Services (business logic), Models (untracked DTOs), Interfaces (contracts), Enums (shared constants)
- Key files: `Services/QuestService.cs` (quest finalization, email dispatch), `Services/PlayerSignupService.cs` (signup voting), `Interfaces/IQuestService.cs` (domain contract)

**QuestBoard.Repository/ (Data Access Layer):**
- Purpose: EF Core entity definitions, DbContext, repository implementations, schema migrations
- Contains: Entities (EF models), BaseRepository (generic CRUD), Repository specializations, Migrations, AutoMapper EntityProfile
- Key files: `Entities/QuestBoardContext.cs` (DbContext, schema config), `BaseRepository.cs` (generic CRUD template), `QuestRepository.cs` (complex queries), `Migrations/` (auto-applied on startup)

**QuestBoard.IntegrationTests/ & QuestBoard.UnitTests/:**
- Purpose: Automated test coverage
- Contains: Test fixtures (WebApplicationFactory, test data), test suites for controllers/services/repos
- Key files: `Fixtures/` (test setup, mocked dependencies), `appsettings.Testing.json` (test environment config)

**.planning/codebase/ (Analysis Documents):**
- Purpose: Guidance for future phases and feature development
- Contains: ARCHITECTURE.md (layers, data flow), STRUCTURE.md (file locations, where to add code), CONVENTIONS.md (code style), TESTING.md (test patterns), STACK.md (tech versions), INTEGRATIONS.md (external APIs), CONCERNS.md (tech debt)

## Key File Locations

**Entry Points:**
- `QuestBoard.Service/Program.cs` — App startup, service registration, middleware pipeline, database initialization
- `QuestBoard.Service/Controllers/QuestBoard/HomeController.cs` — Default landing page route
- `QuestBoard.Service/Controllers/QuestBoard/QuestController.cs` — Quest management (list, create, edit, finalize)
- `QuestBoard.Service/Controllers/Admin/AccountController.cs` — User login, registration, password reset, email confirmation

**Configuration:**
- `QuestBoard.Service/appsettings.json` — Connection string, logging, email settings defaults
- `QuestBoard.Service/appsettings.Development.json` (optional, `.gitignored`) — Local dev overrides
- `QuestBoard.Domain/Extensions/ServiceExtensions.cs` — Domain-layer DI registration
- `QuestBoard.Repository/Extensions/ServiceExtensions.cs` — Repository-layer DI registration

**Core Business Logic:**
- `QuestBoard.Domain/Services/QuestService.cs` — Quest creation, finalization, email dispatch orchestration
- `QuestBoard.Domain/Services/PlayerSignupService.cs` — Player signup registration, date voting, removal
- `QuestBoard.Domain/Services/ShopService.cs` — Shop item listing, purchase, gold transactions
- `QuestBoard.Domain/Services/UserService.cs` — User queries, authentication helpers, role checks

**Data Access & Models:**
- `QuestBoard.Repository/Entities/QuestBoardContext.cs` — EF DbContext, schema modeling, group context filtering
- `QuestBoard.Repository/Entities/QuestEntity.cs` — EF entity for Quests table
- `QuestBoard.Repository/Entities/UserEntity.cs` — EF entity for AspNetUsers table (Identity)
- `QuestBoard.Repository/QuestRepository.cs` — Complex quest queries (GetQuestsWithDetails, GetQuestsForCalendar, FinalizeQuest)
- `QuestBoard.Repository/Automapper/EntityProfile.cs` — EF Entity ↔ Domain Model mappings
- `QuestBoard.Repository/Migrations/` — Database schema migration files (auto-applied)

**ViewModels & Views:**
- `QuestBoard.Service/ViewModels/QuestViewModels/QuestViewModel.cs` — Web form model for quest creation/editing
- `QuestBoard.Service/Views/Quest/Index.cshtml` — Quest list view
- `QuestBoard.Service/Views/Quest/Create.cshtml` — Quest creation form
- `QuestBoard.Service/Views/Shared/_Layout.cshtml` — Master layout (navbar, footer)

**Background Jobs & Email:**
- `QuestBoard.Service/Jobs/QuestFinalizedEmailJob.cs` — Hangfire job: render finalized email, send via Resend
- `QuestBoard.Service/Jobs/SessionReminderJob.cs` — Hangfire job: nightly sweep for upcoming sessions
- `QuestBoard.Service/Components/Emails/QuestFinalized.cshtml` — Email template (Razor component)
- `QuestBoard.Service/Services/HangfireQuestEmailDispatcher.cs` — Enqueue email jobs to Hangfire

**Middleware & Authorization:**
- `QuestBoard.Service/Middleware/GroupSessionMiddleware.cs` — Multi-tenancy gate; redirects to group picker if session group missing
- `QuestBoard.Service/Authorization/DungeonMasterHandler.cs` — Authorization policy handler for [Authorize(Policy = "DungeonMasterOnly")]

**Testing:**
- `QuestBoard.IntegrationTests/Fixtures/` — Test WebApplicationFactory, test data builders
- `QuestBoard.IntegrationTests/Controllers/` — Integration tests for controller action flows
- `QuestBoard.IntegrationTests/appsettings.Testing.json` — Test DB config

## Naming Conventions

**Files:**
- Controllers: `{Feature}Controller.cs` (e.g., `QuestController.cs`, `AccountController.cs`)
- Services: `{Entity}Service.cs` (e.g., `QuestService.cs`, `PlayerSignupService.cs`)
- Repositories: `{Entity}Repository.cs` or specialized names (e.g., `QuestRepository.cs`, `IdentityService.cs`)
- ViewModels: `{Entity}{Action}ViewModel.cs` or just `{Entity}ViewModel.cs` (e.g., `QuestViewModel.cs`, `CreateUserViewModel.cs`)
- Views: `{Action}.cshtml` (e.g., `Index.cshtml`, `Create.cshtml`, `Details.cshtml`)
- Entities: `{Entity}Entity.cs` (e.g., `QuestEntity.cs`, `UserEntity.cs`)
- Domain Models: `{Entity}.cs` (e.g., `Quest.cs`, `User.cs`)
- Interfaces: `I{Entity}{Contract}.cs` (e.g., `IQuestService.cs`, `IQuestRepository.cs`)
- Enums: `{Type}.cs` (e.g., `Role.cs`, `SignupRole.cs`)
- Jobs: `{TriggerOrAction}Job.cs` (e.g., `QuestFinalizedEmailJob.cs`, `SessionReminderJob.cs`)

**Directories:**
- Features: `{FeatureName}/` (e.g., `Controllers/QuestBoard/`, `ViewModels/QuestViewModels/`, `Views/Quest/`)
- Layers: Project name = layer (QuestBoard.Service = Service layer)
- Domain: `Domain/`, `Interfaces/`, `Models/`, `Services/`, `Enums/`
- Data: `Entities/`, `Repositories/`, `Migrations/`

## Where to Add New Code

**New Feature (e.g., "Bounty System"):**

1. **Database/Model Layer** (QuestBoard.Repository)
   - Create entity: `QuestBoard.Repository/Entities/BountyEntity.cs` (EF model)
   - Create migration: Run `dotnet ef migrations add AddBountySystem --project ../QuestBoard.Repository` from QuestBoard.Service/
   - Register in QuestBoardContext.OnModelCreating()

2. **Domain Layer** (QuestBoard.Domain)
   - Create domain model: `QuestBoard.Domain/Models/Bounty.cs` (untracked DTO)
   - Create interface: `QuestBoard.Domain/Interfaces/IBountyService.cs` (service contract)
   - Create service: `QuestBoard.Domain/Services/BountyService.cs` (business logic)
   - Register in `QuestBoard.Domain/Extensions/ServiceExtensions.cs`

3. **Repository Layer** (QuestBoard.Repository)
   - Create repository interface: `QuestBoard.Repository/Interfaces/IBountyRepository.cs` (if custom queries)
   - Create repository: `QuestBoard.Repository/BountyRepository.cs` (EF queries)
   - Add AutoMapper in `QuestBoard.Repository/Automapper/EntityProfile.cs`
   - Register in `QuestBoard.Repository/Extensions/ServiceExtensions.cs`

4. **Service Layer** (QuestBoard.Service)
   - Create controller: `QuestBoard.Service/Controllers/{Feature}/BountyController.cs` (HTTP endpoints)
   - Create ViewModel: `QuestBoard.Service/ViewModels/BountyViewModels/BountyViewModel.cs`
   - Create Views: `QuestBoard.Service/Views/Bounty/Index.cshtml`, Create.cshtml, Details.cshtml, etc.
   - Add AutoMapper in `QuestBoard.Service/Automapper/ViewModelProfile.cs`

5. **Tests** (QuestBoard.IntegrationTests)
   - Add tests: `QuestBoard.IntegrationTests/Controllers/BountyControllerTests.cs`
   - Add service tests: `QuestBoard.IntegrationTests/Services/BountyServiceTests.cs`

**New Component/Modal (e.g., UI dialog):**
- Implementation: `QuestBoard.Service/Components/{Feature}/{ComponentName}.cshtml`
- Usage: `@await Component.InvokeAsync("ComponentName", new { ... })` in views

**Shared Utilities/Helpers:**
- String/collection helpers: `QuestBoard.Domain/Extensions/` (accessible to both Service and Repository)
- Controller helpers: `QuestBoard.Service/Extensions/`
- Data query helpers: `QuestBoard.Repository/` (custom query methods in repository classes)

**New Background Job:**
- Job class: `QuestBoard.Service/Jobs/{Trigger}Job.cs`
- Dispatcher interface: Define in `QuestBoard.Domain/Interfaces/I{Feature}JobDispatcher.cs` (so Domain can call it)
- Dispatcher implementation: `QuestBoard.Service/Services/Hangfire{Feature}JobDispatcher.cs`
- Registration: In `Program.cs`, add `builder.Services.AddScoped<I{Feature}JobDispatcher, Hangfire{Feature}JobDispatcher>()`
- Scheduling: Use `RecurringJob.AddOrUpdate<{Job}>()` in Program.cs or trigger via domain service call

**Email Template:**
- New email: `QuestBoard.Service/Components/Emails/{Purpose}.cshtml` (Razor component)
- Usage: Render in job via `renderService.RenderAsync<{ComponentName}>(new Dictionary<string, object?> { ... })`
- Styling: Use inline CSS or reference from `wwwroot/css/` (email clients don't support external stylesheets)

## Special Directories

**QuestBoard.Service/wwwroot/:**
- Purpose: Static assets (CSS, JS, images)
- Generated: No (committed to git)
- Committed: Yes
- Structure: `css/` (stylesheets), `images/` (D&D theme art, character blanks, wax seals), `js/` (client-side scripts)

**QuestBoard.Repository/Migrations/:**
- Purpose: EF Core migration files (database schema versioning)
- Generated: Yes (via `dotnet ef migrations add`)
- Committed: Yes (required for reproducible schema)
- Usage: Auto-applied on app startup via `context.Database.Migrate()` in Program.cs

**QuestBoard.Service/bin/ & obj/:**
- Purpose: Build output (compiled DLLs, intermediate objects)
- Generated: Yes (during build)
- Committed: No (`.gitignore`)

**.planning/codebase/ (Analysis Docs):**
- Purpose: Reference for implementation planning and phase execution
- Generated: Yes (via GSD commands)
- Committed: Yes
- Used by: `/gsd:plan-phase` and `/gsd:execute-phase` to follow conventions and architecture patterns

---

*Structure analysis: 2026-07-01*
