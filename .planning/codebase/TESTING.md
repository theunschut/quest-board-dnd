# Testing Patterns

**Analysis Date:** 2026-07-02

## Test Framework

**Runner:**
- xUnit v3.2.2
- Config: `QuestBoard.IntegrationTests/xunit.runner.json` (serial execution: `parallelizeAssembly: false`, `parallelizeTestCollections: false`)

**Assertion Library:**
- FluentAssertions v8.10.0 — all assertions use `.Should()` syntax

**Mocking Framework:**
- NSubstitute v5.3.0 — used for unit test mocks

**Test Infrastructure:**
- Microsoft.AspNetCore.Mvc.Testing v10.0.9 — WebApplicationFactory for integration tests
- Microsoft.EntityFrameworkCore.InMemory v10.0.9 — in-memory database for integration tests
- Microsoft.NET.Test.Sdk v18.7.0

**Run Commands:**
```bash
# Run all tests
dotnet test

# Run all unit tests only
dotnet test QuestBoard.UnitTests

# Run all integration tests only
dotnet test QuestBoard.IntegrationTests

# Run specific test class
dotnet test --filter "FullyQualifiedName~QuestServiceTests"

# Run with detailed output
dotnet test --verbosity detailed

# Run tests from solution root
dotnet test --filter "FullyQualifiedName~UnitTests"
```

## Test File Organization

**Location:**
- Unit tests: `QuestBoard.UnitTests/` — mirrors Domain and Service structure
- Integration tests: `QuestBoard.IntegrationTests/` — organized by controller type and feature
- Test data helpers: `QuestBoard.IntegrationTests/Helpers/` — shared test utilities

**Naming:**
- Unit tests: `{ClassName}Tests.cs` (e.g., `EmailServiceTests.cs`)
- Integration tests: `{ControllerName}IntegrationTests.cs` (e.g., `QuestControllerIntegrationTests_Comprehensive.cs`)
- Comprehensive test suites use `_Comprehensive` suffix when multiple test classes for one controller

**Structure:**
```
QuestBoard.UnitTests/
├── Authorization/        # Auth filter and handler tests
├── Extensions/           # Extension method tests
├── Helpers/              # Test helper tests
├── Models/               # Domain model tests
├── Services/             # Service unit tests
└── ViewModels/           # ViewModel tests

QuestBoard.IntegrationTests/
├── Controllers/          # HTTP endpoint tests
├── Helpers/              # Test data and authentication helpers
├── Mobile/               # Mobile-specific endpoint tests
├── Security/             # Security/authorization tests
└── Tests/                # Other integration tests
```

## Test Structure

**Suite Organization:**
- Test classes use `IClassFixture<WebApplicationFactoryBase>` for integration tests
- Constructor parameter receives fixture: `public class TestClass(WebApplicationFactoryBase factory)`
- Local field for HttpClient: `private readonly HttpClient _client = factory.CreateNonRedirectingClient();`
- Unit tests create mocks inline or in setup methods

**Example from `EmailServiceTests.cs`:**
```csharp
public class EmailServiceTests
{
    private static EmailService Create(EmailSettings settings)
    {
        var logger = Substitute.For<ILogger<EmailService>>();
        return new EmailService(Options.Create(settings), logger);
    }

    [Fact]
    public void Constructor_WithValidOptions_DoesNotThrow()
    {
        var act = () => Create(new EmailSettings());
        act.Should().NotThrow();
    }
}
```

**Patterns:**
- Arrange-Act-Assert (AAA) structure: explicit comments delineate sections
- One logical assertion per test (multiple related FluentAssertions chained with `.And` are acceptable)
- Test names describe condition and expected outcome: `SendAsync_WhenFromEmailIsEmpty_DoesNotAttemptSmtpConnection()`
- `[Fact]` for single test case, `[Theory]` with `[InlineData]` for parameterized tests

**Async Test Pattern:**
```csharp
[Fact]
public async Task SendAsync_WhenEmailNotConfigured_ReturnsWithoutException()
{
    // Arrange
    var service = Create(new EmailSettings { SmtpUsername = "", FromEmail = "" });

    // Act
    var act = async () => await service.SendAsync("to@example.com", "Test Subject", "<h1>Hello</h1>");

    // Assert
    await act.Should().NotThrowAsync();
}
```

## Mocking

**Framework:** NSubstitute for unit tests

**Patterns:**
```csharp
// Create mock
var logger = Substitute.For<ILogger<EmailService>>();

// Configure behavior
_dependency.SomeMethod(input).Returns("mocked result");

// Verify calls
_dependency.Received(1).SomeMethod(input);
```

**What to Mock:**
- External I/O: ILogger, IEmailService, IHttpClientFactory
- Database access: IRepository interfaces
- Configuration: IOptions<T>
- Identity/Auth: IUserService, IIdentityService

**What NOT to Mock:**
- Domain models (use real instances)
- Enums (use real values)
- Value objects
- Core application services used by the system under test — use real implementations for integration

## Fixtures and Factories

**Test Data:**
- Helper class: `QuestBoard.IntegrationTests/Helpers/TestDataHelper.cs`
- Creates test users, quests, and other entities for test setup
- Methods: `CreateTestQuestAsync()`, `CreateTestUserAsync()`, `ClearDatabaseAsync()`

**Example from `TestDataHelper.cs`:**
```csharp
public static async Task<Quest> CreateTestQuestAsync(
    IServiceProvider services,
    int dungeonMasterId,
    string title = "Test Quest",
    string description = "Test Description")
{
    // Setup scope, get service, create and save entity
}
```

**Authentication Helpers:**
- `AuthenticationHelper.CreateTestUserAsync()` — creates user with unique email/username
- `AuthenticationHelper.CreateAuthenticatedClientAsync()` — creates HttpClient with auth header
- `AuthenticationHelper.CreateAuthenticatedClientWithUserAsync()` — returns both client and user entity
- `AuthenticationHelper.CreateAuthenticatedDMClientAsync()` — creates DungeonMaster-role client
- `AuthenticationHelper.CreateAuthenticatedAdminClientAsync()` — creates Admin-role client
- `AuthenticationHelper.CreateAuthenticatedSuperAdminClientAsync()` — creates SuperAdmin-role client

**Location:**
- Helpers: `QuestBoard.IntegrationTests/Helpers/`
- Used by integration tests to set up test database state

## Coverage

**Requirements:** No enforced coverage target detected

**View Coverage:**
```bash
dotnet test /p:CollectCoverage=true /p:CoverageFormat=opencover
```

## Test Types

**Unit Tests (~20 tests across 4 suites):**
- Scope: Individual services, repositories, models, authorization handlers
- Approach: Mocks all external dependencies
- Location: `QuestBoard.UnitTests/Services/`, `QuestBoard.UnitTests/Authorization/`, `QuestBoard.UnitTests/Models/`
- Examples:
  - `EmailServiceTests.cs` — constructor validation, email sending guards, SMTP deduplication
  - `AdminDashboardAuthFilterTests.cs` — Hangfire dashboard authorization (SuperAdmin-only)
  - `QuestModelTests.cs` — domain model initialization and property assignment

**Integration Tests (~35 tests across 7+ suites):**
- Scope: Full HTTP request/response cycle through to in-memory database
- Approach: Real dependencies; WebApplicationFactory; in-memory EF Core database
- Location: `QuestBoard.IntegrationTests/Controllers/`
- Isolation: Each test gets unique in-memory database instance
- Cleanup: `TestDataHelper.ClearDatabaseAsync()` or `factory.ResetDatabase()` between tests
- Examples:
  - Account/auth flows: login, registration (404), profile access
  - Quest CRUD: create, read, finalize, voter access
  - Group management: multi-tenancy routing, group picker
  - Platform area: SuperAdmin-only endpoints
  - Authorization regressions: quest visibility by role
  - Mobile endpoints: separate test class for mobile-specific views

**E2E Tests:**
- Not implemented — integration tests serve as functional verification
- Docker deployment tested manually via `docker-compose up`

## Common Patterns

**HTTP Status Code Assertions:**
```csharp
response.StatusCode.Should().Be(HttpStatusCode.OK);
response.StatusCode.Should().BeOneOf(
    HttpStatusCode.Redirect,
    HttpStatusCode.Found,
    HttpStatusCode.Unauthorized);
```

**Content Presence:**
```csharp
var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
content.Should().Contain("Expected Text");
```

**Async Testing:**
```csharp
[Fact]
public async Task MethodAsync_ShouldBehavior()
{
    // Arrange
    var input = "test";

    // Act
    var result = await sut.MethodAsync(input);

    // Assert
    result.Should().Be("expected");
}

// Or with exception testing:
var act = async () => await service.SendAsync("to@example.com", "Subject", "<h1>Body</h1>");
await act.Should().NotThrowAsync();
```

**Error Testing:**
```csharp
[Fact]
public async Task SendAsync_WhenFromEmailIsEmpty_DoesNotAttemptSmtpConnection()
{
    // Guard against real SMTP connection via non-routable address (RFC 5737)
    var service = Create(new EmailSettings
    {
        SmtpServer   = "192.0.2.1",  // TEST-NET-1
        SmtpPort     = 587,
        FromEmail    = "",            // Empty guard
    });

    var act = async () => await service.SendAsync("to@example.com", "Subject", "<h1>Body</h1>");
    await act.Should().NotThrowAsync(
        "because an empty FromEmail must prevent any SMTP connection attempt");
}
```

**Theory/Parameterized Tests:**
```csharp
[Theory]
[InlineData(1)]
[InlineData(7)]
[InlineData(30)]
public void DateTime_AddDays_ShouldCalculateFutureDates(int daysToAdd)
{
    // Arrange
    var startDate = DateTime.Now.Date;

    // Act
    var futureDate = startDate.AddDays(daysToAdd);

    // Assert
    futureDate.Should().BeAfter(startDate);
    (futureDate - startDate).Days.Should().Be(daysToAdd);
}
```

## Test Infrastructure

**WebApplicationFactoryBase:**
- Location: `QuestBoard.IntegrationTests/WebApplicationFactoryBase.cs`
- Inherits from `WebApplicationFactory<Program>`
- Provides:
  - Isolated in-memory database per test instance
  - Test authentication scheme (`"Test"` scheme in TestAuthHandler)
  - Antiforgery decorator (validates requests; always succeeds for tests)
  - Hangfire stub (no-op IBackgroundJobClient for Testing environment)
  - NoOp background job client that returns fake job IDs

**Test Database:**
- In-memory EF Core database with unique name per factory: `QuestBoardTest_{Guid}`
- Shared across all DbContext instances within a test run
- Reset via `factory.ResetDatabase()` using `TestDatabase.Reset()`

**Test Authentication:**
- Custom authentication scheme: `TestAuthHandler`
- Test clients encode auth header: `"Test {userId}:{userName}:{email}:{role1,role2,...}"`
- Supports role claims for authorization policy testing
- Falls back to Identity.Application scheme if no Test header (allows cookie-based auth)

**Parallelization:**
- xUnit runner configured for serial execution: `parallelizeAssembly: false`, `parallelizeTestCollections: false`
- Necessary because all tests share in-memory database via ServiceProvider instance
- Concurrent tests would corrupt shared database state

## Test Isolation

**Strategy:**
- Each `WebApplicationFactoryBase` instance creates a new in-memory database
- Tests within a class share the same factory instance (and database)
- Clean state between tests using `TestDataHelper.ClearDatabaseAsync()` or factory-level reset
- Unique user credentials generated per test (GUID suffix) to avoid conflicts

**Cleanup Pattern:**
```csharp
[Fact]
public async Task SomeTest()
{
    // Arrange — clear database before test
    await TestDataHelper.ClearDatabaseAsync(factory.Services);
    var user = await AuthenticationHelper.CreateTestUserAsync(factory.Services, "user", "user@example.com");

    // Act & Assert
}
```

## Global Usings

**Unit Tests** (`GlobalUsings.cs`):
```csharp
global using FluentAssertions;
```

**Integration Tests** (`GlobalUsings.cs`):
```csharp
global using QuestBoard.Repository.Entities;
global using FluentAssertions;
global using Microsoft.AspNetCore.Mvc.Testing;
global using Microsoft.EntityFrameworkCore;
global using Microsoft.Extensions.DependencyInjection;
```

---

*Testing analysis: 2026-07-02*
