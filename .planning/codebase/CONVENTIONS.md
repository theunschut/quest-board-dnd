# Coding Conventions

**Analysis Date:** 2026-07-01

## Naming Patterns

**Files:**
- Class files use PascalCase matching their class name: `UserService.cs`, `EmailSettings.cs`
- Test files follow the pattern `[TargetClass]Tests.cs`: `EmailServiceTests.cs`, `QuestServiceTests.cs`
- Integration test files use `[Controller]IntegrationTests.cs`: `QuestControllerIntegrationTests_Comprehensive.cs`
- Partial classes append descriptor: `QuestControllerIntegrationTests_Comprehensive.cs` for large test suites
- View files use PascalCase: `Create.cshtml`, `Details.cshtml` with mobile variants as `Create.Mobile.cshtml`
- Partial views prefixed with underscore: `_QuestCard.cshtml`, `_ShopItemDetailsContent.cshtml`

**Classes and Interfaces:**
- Public classes and interfaces: PascalCase
- Services use pattern `[Entity]Service`: `QuestService`, `UserService`, `ShopService`
- Service implementations marked as `internal` when they implement an interface
- Repository classes use pattern `[Entity]Repository` in Repository layer (not seen directly in Domain)
- Interfaces prefixed with `I`: `IQuestService`, `IBaseRepository<T>`, `IEmailService`
- View Models suffixed with `ViewModel`: `QuestViewModel`, `CreateShopItemViewModel`, `EditDMProfileViewModel`
- Enum classes suffixed with type: `CharacterRole`, `ItemRarity`, `VoteType`, `TransactionType`
- Requirements tracking constants use pattern `[PREFIX]-[NUMBER]`: `D-06`, `EMAIL-04`, `CTRL-01` — embedded in comments as requirement references

**Functions/Methods:**
- PascalCase: `GetQuestWithDetailsAsync()`, `FinalizeQuestAsync()`, `CreateSmtpClient()`
- Async methods suffixed with `Async`: `GetByIdAsync()`, `AddAsync()`, `SaveChangesAsync()`
- Private helper methods PascalCase: `MakeQuest()`, `MakeSignup()`
- Setters follow pattern `SetAsMainCharacterAsync()`, `SetPasswordAsync()`
- Removal methods: `RemoveAsync()`
- Query/fetch methods: `GetXxxAsync()`, `GetXxxWithDetailsAsync()` for loaded navigation properties

**Variables:**
- Local variables and parameters: camelCase: `viewModel`, `selectedPlayers`, `currentUser`, `token`
- Private fields prefixed with underscore: `_repository`, `_mapper`, `_dispatcher`, `_settings`
- Test setup variables (test context): `_sut` for "system under test" (the class being tested)
- Test fixture/helper variables: `_repository`, `_factory`

**Enums and Constants:**
- Enum values: PascalCase: `SignupRole.Player`, `ItemRarity.Rare`, `Role.Admin`
- Static helper classes: `TestDataHelper`, `AuthenticationHelper`
- Namespace constants classes: `SessionKeys`

## Code Style

**Formatting:**
- No explicit `.editorconfig` or `.ruleset` file detected — uses C# 10+ implicit usings and nullable reference types
- `ImplicitUsings` enabled: `global using FluentAssertions;` in test projects
- `Nullable` enabled: `#nullable enable` in csproj targets
- Target framework: .NET 10.0

**Linting:**
- No `.eslintrc*` or Roslyn analyzer configuration detected
- Assumes Visual Studio/Rider defaults for C#

## Import Organization

**Order (from observed codebase):**
1. System namespaces: `using System;`, `using System.Collections.Generic;`, `using System.Linq;`, `using System.Net;`, `using System.Net.Mail;`
2. Microsoft namespaces: `using Microsoft.AspNetCore.*;`, `using Microsoft.Extensions.*;`, `using Microsoft.EntityFrameworkCore;`
3. Third-party packages: `using AutoMapper;`, `using NSubstitute;`, `using Hangfire;`
4. Domain/Project namespaces: `using QuestBoard.Domain.*;`, `using QuestBoard.Repository.*;`, `using QuestBoard.Service.*;`

**Path Aliases:**
- Not detected in configuration — full namespaces used throughout

**File-level namespace declaration:**
- Consistently uses file-scoped namespaces: `namespace QuestBoard.Domain.Services;` (no braces)

## Error Handling

**Patterns:**
- **Exceptions for validation failures**: `InvalidOperationException`, `ArgumentException` thrown directly when invariants are violated
  - Example: `throw new InvalidOperationException("Item is not available for purchase.");` in `ShopService.cs:72`
  - Example: `throw new ArgumentException("Player signup not found", nameof(playerSignupId));` in `PlayerSignupService.cs:15`
- **ServiceResult<T> for operations with recoverable failures**: Used when operation success must be checked without exception throwing
  - `ServiceResult<T>.Ok(data)` returns success with data
  - `ServiceResult<T>.Fail(error)` returns failure with error message
  - Used in `QuestService.UpdateQuestPropertiesWithNotificationsAsync()` which returns `Task<ServiceResult<int>>`
- **Silent failures in optional operations**: Email sending catches exceptions and logs — see `EmailService.SendAsync()` at line 50-54
- **Null-coalescing and guards**: Extensive use of `?` and `??` for null handling
  - Example: `quest?.Id ?? 0`, `src.CreatedByDm?.Name ?? "Unknown"`

## Logging

**Framework:** `Microsoft.Extensions.Logging.ILogger<T>` injected via constructor

**Patterns:**
- Only `ILogger<EmailService>` observed in Domain layer
- Log levels: `LogWarning()` for configuration issues, `LogError()` for exceptions
- Message format: `"Email settings not configured. Skipping email notification."`
- Exception logging includes template parameters: `logger.LogError(ex, "Failed to send email with subject {Subject}", subject);`

## Comments

**When to Comment:**
- Requirement references embedded inline: `// EMAIL-04: re-fetch post-save to avoid stale IsSelected state`
- Complex business logic explanations: Visible in `QuestService.FinalizeQuestAsync()` explaining email inclusion logic
- Manual cleanup instructions: "Manual cleanup required since Quest->PlayerSignup is NoAction" in `QuestService.RemoveAsync()`
- Test setup descriptions: "Arrange — even with quests seeded, the public landing page must not surface them"

**JSDoc/TSDoc:**
- Not used in C# codebase — uses XML doc comments `/// <summary>` instead (observed in interface definitions)
- Example from `IQuestRepository`: `/// Bypasses the group query filter — use only for system-wide sweep operations (DailyReminderJob). (D-08)`

## Function Design

**Size:** Functions are focused and delegate to helpers
- `FinalizeQuestAsync()` delegates to repository then dispatcher
- Helper methods used for test data creation: `MakeQuest()`, `MakeSignup()`

**Parameters:** 
- Injection via constructor preferred (primary-constructor pattern used throughout)
- Async operations always include `CancellationToken token = default` as final parameter
- ViewModels passed to actions for complex input binding

**Return Values:** 
- Async methods return `Task<T>` or `Task`
- Nullable return types common: `Task<Quest?>`, `Task<User?>`
- Collections returned as `IList<T>` interface type (not concrete List)

## Module Design

**Exports:**
- Service classes are `internal` in Domain layer — only their `IServiceInterface` is public
- Controllers are `public` in Service layer
- Repositories `internal` in Repository layer with public repository interface in Domain
- ViewModels and Entities are public

**Barrel Files:**
- Not used — direct namespace imports from specific files

**Primary Constructor Pattern:**
- Extensively used: `public class QuestService(IQuestRepository repository, IMapper mapper) : BaseService<Quest>(...)`
- Fields initialized inline: `private readonly EmailSettings _settings = options.Value;`

**AutoMapper Profile Pattern:**
- `ViewModelProfile` in `QuestBoard.Service/Automapper/` handles ViewModel ↔ DomainModel mappings
- `EntityProfile` in `QuestBoard.Repository/Automapper/` handles Entity ↔ DomainModel mappings
- Uses fluent API: `CreateMap<Quest, QuestViewModel>()` with `.ForMember()` overrides
- Ignores or maps to calculated values for non-stored fields

## Enum Handling

Enums that are stored as `int` in the database and cast at the AutoMapper `EntityProfile` boundary (e.g. `(int)src.Type`, `(SignupRole)src.SignupRole`) must be **append-only and never reordered**. New values must always get the next unused int; reordering existing values corrupts data already persisted with the old numeric mapping, since the database only stores the int and has no knowledge of the enum's symbolic names.

A round-trip validation test guards this convention going forward — it verifies that every enum value present in the database deserializes back to the same symbolic name it was written with.

## Three-Layer Dependency Direction

**Architecture constraint (CLAUDE.md):**
- Service → Domain → Repository (one-way strict dependency)
- Service layer contains controllers, ViewModels, job handlers, middleware
- Domain layer contains business logic, domain models, service interfaces
- Repository layer contains EF Core entities, repositories, database context

**No cross-layer violations detected:**
- Service never directly uses Repository
- Domain never references Service or Repository (except for abstract interfaces)
- Repository never references Domain or Service business logic

---

*Convention analysis: 2026-07-01*
