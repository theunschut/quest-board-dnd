# Coding Conventions

**Analysis Date:** 2026-07-02
**last_mapped_commit:** e5b37a73cda29bf355c4de6ebf4663b1625c3cf6

## Naming Patterns

**Files:**
- PascalCase for all classes: `HomeController.cs`, `QuestService.cs`, `EmailSettings.cs`
- Namespace hierarchy aligns with file structure: `QuestBoard.Service.Controllers.QuestBoard`, `QuestBoard.Domain.Models`
- Enums in dedicated `Enums/` directory: `GroupRole.cs`, `ItemType.cs`
- ViewModels in `ViewModels/` with category subdirectories: `ViewModels/QuestViewModels/`, `ViewModels/CharacterViewModels/`
- Tests use suffix matching the class tested: `QuestServiceTests.cs`, `EmailServiceTests.cs`, `AdminDashboardAuthFilterTests.cs`

**Functions and Methods:**
- PascalCase for public methods: `GetByIdAsync()`, `SendAsync()`, `CreateAsync()`
- Async methods suffixed with `Async`: `GetQuestsByDmNameAsync()`, `FinalizeQuestAsync()`
- Abbreviations avoided except for well-understood acronyms (DM, GM, etc.)
- Constructor-injected dependencies use parameter list names (lowercase): `IQuestService questService`

**Variables:**
- camelCase for local variables and parameters: `currentUserId`, `questViewModel`, `groupId`
- Private fields use `_camelCase`: `_settings`, `_dbContext`
- Collection types are plural: `quests`, `proposedDates`, `playerSignups`

**Types:**
- PascalCase for classes, interfaces, enums, records: `Quest`, `IQuestService`, `EmailSettings`, `GroupRole`
- Interfaces prefixed with `I`: `IModel`, `IBaseService<T>`, `IQuestRepository`
- Enum values are PascalCase: `Player`, `DungeonMaster`, `Admin` in `GroupRole` enum
- Database records use `Entity` suffix: `QuestEntity`, `UserEntity`, `PlayerSignupEntity`
- View models use `ViewModel` suffix: `QuestViewModel`, `CharacterViewModel`, `LoginViewModel`

## Code Style

**Formatting:**
- Target framework: .NET 10 (net10.0) — development target for production deployment
- CI pipeline uses .NET 8.0.x (ubuntu-latest) for build validation (`.github/workflows/dotnet.yml`)
- Implicit usings enabled in all `.csproj` files (`<ImplicitUsings>enable</ImplicitUsings>`)
- Nullable reference types enabled (`<Nullable>enable</Nullable>`)
- Line endings: CRLF (Windows) — per `CLAUDE.md` development guidelines
- No line length limit enforced (varies 60-120 characters observed)

**Linting:**
- No .editorconfig or centralized linting configuration detected
- StyleCop, Roslyn analyzers not explicitly configured
- Code follows .NET style guide conventions implicitly

**Access Modifiers:**
- Public methods explicitly declared: `public async Task<IActionResult> Index()`
- Private fields not explicitly declared (C# defaults to private)
- Internal repositories/services use `internal` keyword to hide from external consumers: `internal abstract class BaseRepository<TModel, TEntity>`
- Protected members used in base classes for inheritance: `protected IMapper Mapper => mapper`

## Import Organization

**Order:**
1. Using statements for System and Microsoft namespaces first
2. Using statements for project namespaces (QuestBoard.*)
3. Namespace declaration
4. Class definition

Example from `QuestController.cs`:
```csharp
using AutoMapper;
using QuestBoard.Domain.Enums;
using QuestBoard.Domain.Extensions;
using QuestBoard.Domain.Interfaces;
using QuestBoard.Domain.Models;
using QuestBoard.Domain.Models.QuestBoard;
using QuestBoard.Service.ViewModels.CalendarViewModels;
using QuestBoard.Service.ViewModels.QuestViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace QuestBoard.Service.Controllers.QuestBoard;
```

**Path Aliases:**
- No custom path aliases configured (`.csproj` or configuration files)
- Explicit long-form namespaces used throughout

## Error Handling

**Patterns:**
- Exceptions logged with structured logging before rethrowing: `logger.LogError(ex, "Failed to send email with subject {Subject}", subject); throw;`
- Never log credentials or sensitive connection details inline — use structured log parameters: `logger.LogWarning("Email settings not configured. Skipping email notification.");`
- Try-catch blocks used to wrap SMTP operations (`EmailService.SendAsync`)
- Authorization failures return `Challenge()` or redirect implicitly via ASP.NET Core
- Guard clauses prevent null reference errors: `if (entity == null) return;` in repository methods
- Model validation errors handled by ASP.NET Core model binding: `if (!ModelState.IsValid) return View(viewModel);`

**Specific patterns:**
- Controllers check user authentication: `User.Identity?.IsAuthenticated == true`
- Service dependencies are never null-coalesced in constructors (dependency injection guarantees non-null)
- Optional navigation properties use `!` null-forgiving operator when certain: `src.Owner != null ? src.Owner.Name : "Unknown"`

## Logging

**Framework:** `Microsoft.Extensions.Logging.ILogger<T>`

**Patterns:**
- Injected via constructor: `ILogger<EmailService> logger`
- Structured logging used for all messages: `logger.LogWarning("Email settings not configured. Skipping email notification.");`
- Log levels used appropriately:
  - `LogWarning()` for configuration gaps or skipped operations
  - `LogError(Exception, string)` for actual failures with exception context
- No console.WriteLine or Debug.WriteLine used (all go through ILogger)

## Comments

**When to Comment:**
- Block comments explain non-obvious business logic: `// SuperAdmin bypass — reads claims directly, no DB call`
- Single-line comments for clarification at code sections, not on individual statements
- High-level intent documented in XML doc comments for public methods and types
- **Never embed GSD planning references** (phase/plan IDs, requirement IDs, review IDs) in source code comments — these belong in `.planning/`, not in code that stays deployed (per `CLAUDE.md`)

**JSDoc/TSDoc:**
- Not used (C# codebase uses XML documentation instead)
- XML doc comments (`/// <summary>`) used on public interfaces and abstract methods
- Example from `BaseService<TModel>`:
  ```csharp
  /// <inheritdoc/>
  public virtual async Task AddAsync(TModel model, CancellationToken token = default)
  {
      await repository.AddAsync(model, token);
  }
  ```

## Function Design

**Size:**
- Controllers are slim (delegate to services)
- Service methods are focused and single-purpose
- Repository methods handle data access only
- Complex business logic encapsulated in service methods

**Parameters:**
- Dependency injection preferred over parameter passing
- CancellationToken as optional final parameter: `async Task SomeMethodAsync(CancellationToken token = default)`
- ViewModel parameters used for POST actions to batch form data

**Return Values:**
- Async methods return `Task`, `Task<T>`, `ValueTask`, or `ValueTask<T>`
- Service methods return domain models (`Quest`, `User`, `Character`)
- Repository methods return domain models (mapped from entities via AutoMapper)
- Controllers return `IActionResult` (including `View()`, `RedirectToAction()`, `Ok()`, `NotFound()`)
- Nullable return types explicit: `async Task<User?>` or `async Task<IList<TModel>>`

## Module Design

**Exports:**
- Services exported via interfaces (`IQuestService`, `IUserService`)
- Repository pattern: interface in Domain, implementation in Repository
- Base classes marked `internal abstract` to prevent external subclassing
- No public static fields or constants in shared modules (enums preferred for fixed sets)

**Barrel Files:**
- Not used (no `index.ts`-style re-exports)
- Namespace organization provides logical grouping
- Full namespace paths required in imports

## Immutability

**Record Types:**
- `EmailSettings` is a record type for configuration: `public record EmailSettings { ... }`
- Record properties use `init` accessor: `public string SmtpServer { get; init; } = "localhost";`
- Domain models (classes) are mutable: `Quest`, `User`, `Character` all use `{ get; set; }`

**Collections:**
- Initialize empty collections as empty list literals: `public IList<Quest> Quests { get; set; } = [];`
- No null collection properties — default to empty list/array
- Collection properties are IList<T> (allows both interface consumers and concrete mutations)

## Authorization Patterns

**Policy-Based:**
- Authorization policies defined in `Program.cs`: `"DungeonMasterOnly"`, `"AdminOnly"`, `"SuperAdminOnly"`
- Policies enforced via `[Authorize(Policy = "...")]` attributes on action methods
- Authorization handlers (`AdminHandler`, `DungeonMasterHandler`) implement custom role logic
- Role claims checked via `User.IsInRole("SuperAdmin")` for global roles
- Group roles checked via `IActiveGroupContext` for tenant-scoped authorization

**Implementation Files:**
- Authorization handler logic: `QuestBoard.Service/Authorization/AdminHandler.cs`
- Authorization requirement classes: `QuestBoard.Service/Authorization/AdminRequirement.cs`
- Dashboard auth filter: `QuestBoard.Service/Authorization/AdminDashboardAuthFilter.cs`

## Data Validation

**Approach:**
- Data annotations on model properties: `[Required]`, `[StringLength(100)]`, `[EmailAddress]`, `[Range(1, 20)]`
- ASP.NET Core model binding validates automatically before controller action executes
- Service methods receive pre-validated domain models (validation happens at boundary)
- Database constraints enforced at Entity Framework level via entity mappings

**Example from `Character.cs`:**
```csharp
[Required]
[StringLength(100)]
public string Name { get; set; } = string.Empty;

[Range(1, 20)]
public int Level { get; set; } = 1;
```

## Async/Await

**Conventions:**
- All I/O operations (database, HTTP, email) are async: `async Task`, `await`
- Database operations use `DbSet.ToListAsync()`, `DbSet.FindAsync()`, `DbContext.SaveChangesAsync()`
- HTTP requests in integration tests use `await client.GetAsync()` / `await client.PostAsync()`
- Async methods always suffix with `Async`: never `GetQuests()`, always `GetQuestsAsync()`
- CancellationToken piped through: `await repository.GetAllAsync(token)`

## Migration Management

**Tool:** `dotnet-ef` v9.0.6 (`.config/dotnet-tools.json`)

**Patterns:**
- Migrations created via: `dotnet ef migrations add MigrationName --project ../QuestBoard.Repository`
- Removed via: `dotnet ef migrations remove --project ../QuestBoard.Repository`
- Auto-applied on startup via `context.Database.Migrate()` in `Program.cs` — no manual `database update` step needed in dev
- Shell script helper available: `create-migration.sh` (adds EF tools, creates initial migration)

## UI/UX Design

**Modern Card Pattern (from `CLAUDE.md`):**
- All new views use CSS classes: `modern-card`, `modern-card-header`, `modern-card-body`
- Header structure:
  ```html
  <div class="card-header modern-card-header">
      <h2 class="mb-0">
          <i class="fas fa-icon-name text-color me-2"></i>
          Page Title
      </h2>
  </div>
  ```
- Always include `<hr>` before the button section
- Use filled colored buttons (not outline), FontAwesome icons with `me-2` spacing
- Button layout: `d-flex justify-content-between` — secondary (cancel) left, primary (submit) right

---

*Convention analysis: 2026-07-02 (updated 2026-07-03)*
