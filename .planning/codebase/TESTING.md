# Testing Patterns

**Analysis Date:** 2026-07-01

## Test Framework

**Runner:**
- xunit.v3 Version 3.2.2
- Config: `QuestBoard.IntegrationTests/xunit.runner.json`
- Key settings:
  - `"parallelizeAssembly": false` — tests run sequentially across assemblies
  - `"parallelizeTestCollections": false` — test methods within a collection run sequentially

**Assertion Library:**
- FluentAssertions Version 8.10.0
- Syntax: `.Should().Be(expected)`, `.Should().BeTrue()`, `.Should().Contain()`, etc.
- Global using: `global using FluentAssertions;` in both test project root files

**Mocking:**
- NSubstitute Version 5.3.0
- Pattern: `Substitute.For<IInterface>()` creates mock, `.Returns()` sets return values, `.Received()` verifies calls

**Run Commands:**
```bash
# Run all tests
dotnet test

# Run specific test project
dotnet test QuestBoard.UnitTests/QuestBoard.UnitTests.csproj
dotnet test QuestBoard.IntegrationTests/QuestBoard.IntegrationTests.csproj

# Watch mode (requires additional tooling)
# Standard dotnet test does not include watch — use IDE or:
dotnet watch test

# Coverage
# Not configured in project files
```

## Test File Organization

**Location — Unit Tests:**
- `QuestBoard.UnitTests/` at same level as main projects
- Organized by type: `Services/`, `Models/`, `Helpers/`, `ViewModels/`
- Example structure:
  - `QuestBoard.UnitTests/Services/EmailServiceTests.cs`
  - `QuestBoard.UnitTests/Services/QuestServiceTests.cs`
  - `QuestBoard.UnitTests/Helpers/DateHelperTests.cs`

**Location — Integration Tests:**
- `QuestBoard.IntegrationTests/` at same level as main projects
- Organized by target layer: `Controllers/`, `Tests/`, `Mobile/`, `Helpers/`
- Example structure:
  - `QuestBoard.IntegrationTests/Controllers/QuestControllerIntegrationTests_Comprehensive.cs`
  - `QuestBoard.IntegrationTests/Controllers/HomeControllerIntegrationTests.cs`
  - `QuestBoard.IntegrationTests/Mobile/MobileDetectionMiddlewareTests.cs`

**Naming:**
- Unit tests: `[TargetClass]Tests.cs`
- Integration tests: `[ControllerOrTarget]IntegrationTests.cs` or with suffix for large suites: `[Target]IntegrationTests_[Descriptor].cs`
- Test methods: `[Method]_[Condition]_[Expected]`
  - Example: `FinalizeQuestAsync_WhenQuestReFetchReturnsNull_SendsNoEmails()`
  - Example: `SendAsync_WhenEmailNotConfigured_ReturnsWithoutException()`

**Structure:**
```
QuestBoard.UnitTests/
├── GlobalUsings.cs          (shared imports)
├── Services/
│   ├── EmailServiceTests.cs
│   ├── QuestServiceTests.cs
│   └── ShopServiceTests.cs
├── Models/
│   └── QuestModelTests.cs
├── Helpers/
│   └── DateHelperTests.cs
└── ViewModels/
    └── CreateQuestViewModelTests.cs

QuestBoard.IntegrationTests/
├── GlobalUsings.cs          (shared imports)
├── WebApplicationFactoryBase.cs     (xUnit test host setup)
├── Controllers/
│   ├── HomeControllerIntegrationTests.cs
│   ├── QuestControllerIntegrationTests_Comprehensive.cs
│   └── ShopControllerIntegrationTests.cs
├── Helpers/
│   ├── TestDatabase.cs              (in-memory EF Core setup)
│   ├── TestDataHelper.cs            (seed data factories)
│   ├── AuthenticationHelper.cs      (test auth setup)
│   └── TestAuthSelectorMiddleware.cs
└── Mobile/
    └── MobileDetectionMiddlewareTests.cs
```

## Test Structure

**Suite Organization:**
```csharp
// Unit test example (QuestBoard.UnitTests/Services/QuestServiceTests.cs)
public class QuestServiceTests
{
    // Fields: dependencies as mocks
    private readonly IQuestRepository _repository;
    private readonly IMapper _mapper;
    private readonly QuestService _sut;  // System Under Test

    // Constructor: setup all mocks
    public QuestServiceTests()
    {
        _repository = Substitute.For<IQuestRepository>();
        _mapper = Substitute.For<IMapper>();
        _sut = new QuestService(_repository, _mapper);
    }

    // Helper: create test data
    private static Quest MakeQuest(int id, IList<PlayerSignup>? signups = null) =>
        new() { Id = id, Title = "Test Quest" };

    // Test methods with [Fact] attribute
    [Fact]
    public async Task FinalizeQuestAsync_WhenQuestReFetchReturnsNull_SendsNoEmails()
    {
        // Arrange
        _repository.GetQuestWithDetailsAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns((Quest?)null);

        // Act
        await _sut.FinalizeQuestAsync(1, DateTime.UtcNow, [42], TestContext.Current.CancellationToken);

        // Assert
        _dispatcher.DidNotReceive().EnqueueFinalizedEmail(...);
    }
}
```

**Integration Test Example:**
```csharp
// Integration test example (QuestBoard.IntegrationTests/Controllers/HomeControllerIntegrationTests.cs)
public class HomeControllerIntegrationTests : IClassFixture<WebApplicationFactoryBase>
{
    private readonly HttpClient _client;
    private readonly WebApplicationFactoryBase _factory;

    public HomeControllerIntegrationTests(WebApplicationFactoryBase factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Index_ShouldReturnSuccessStatusCode()
    {
        // Act
        var response = await _client.GetAsync("/", TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
```

**Patterns:**

1. **Arrange-Act-Assert (AAA):**
   - Separate sections with comments: `// Arrange`, `// Act`, `// Assert`
   - Setup phase prepares mocks and test data
   - Action invokes the target method
   - Assertion verifies results using FluentAssertions

2. **Test Data Factories:**
   - Private helper methods: `MakeQuest()`, `MakeSignup()`
   - Static methods return new test instances with sensible defaults
   - Accept parameters to customize for specific test case

3. **Mock Verification:**
   - `.Received(1).Method(...)` asserts a method was called exactly once
   - `.DidNotReceive().Method(...)` asserts a method was not called
   - `Arg.Any<T>()` matches any argument of type T
   - `Arg.Is<T>(predicate)` matches with custom predicate

4. **Async Pattern:**
   - All async methods use `async Task` test signature
   - Final parameter always: `TestContext.Current.CancellationToken`
   - Async assertions: `.Should().NotThrowAsync()`

## Mocking

**Framework:** NSubstitute

**Patterns:**
```csharp
// Create mock
var repository = Substitute.For<IQuestRepository>();

// Setup return values
repository.GetByIdAsync(1, Arg.Any<CancellationToken>())
    .Returns(new Quest { Id = 1 });

// Setup to return tuple
shopService.GetPagedPublishedItemsAsync(
    Arg.Any<ItemType?>(),
    Arg.Any<IList<ItemRarity>?>(),
    Arg.Any<string?>(),
    Arg.Any<string?>(),
    Arg.Any<int>(),
    Arg.Any<int>(),
    Arg.Any<CancellationToken>())
    .Returns((expectedItems, 42));

// Verify calls
await repository.Received(1).GetQuestWithDetailsAsync(1, Arg.Any<CancellationToken>());

// Verify with argument matching
await shopService.Received(1).GetPagedPublishedItemsAsync(
    ItemType.Equipment,
    Arg.Is<IList<ItemRarity>>(r => r.Contains(ItemRarity.Rare)),
    "price_asc",
    "sword",
    2,
    12,
    Arg.Any<CancellationToken>());
```

**What to Mock:**
- Repository interfaces: `IQuestRepository`, `IPlayerSignupRepository`, `IMapper`
- External services: `IQuestEmailDispatcher`, `IEmailService`
- Logging: `ILogger<T>` via `Substitute.For<ILogger<T>>()`

**What NOT to Mock:**
- Domain models (create real instances)
- View models (create real instances)
- Database context in integration tests (use `WebApplicationFactoryBase`)

## Fixtures and Factories

**Test Data Factories:**
- **Unit tests**: Private static helper methods in test class
  ```csharp
  private static Quest MakeQuest(int id, IList<PlayerSignup>? signups = null) =>
      new() { Id = id, Title = "Test Quest", PlayerSignups = signups ?? [] };
  ```
- **Integration tests**: Static helper class `TestDataHelper`
  ```csharp
  public static async Task<QuestEntity> CreateTestQuestAsync(
      IServiceProvider services,
      int dungeonMasterId,
      string title = "Test Quest",
      string description = "Test Description",
      int challengeRating = 5) { ... }
  ```

**Test Infrastructure:**

1. **WebApplicationFactoryBase** (`QuestBoard.IntegrationTests/WebApplicationFactoryBase.cs`):
   - Inherits from `WebApplicationFactory<Program>`
   - Configures in-memory EF Core database for testing
   - Replaces Hangfire with no-op stub (`NoOpBackgroundJobClient`)
   - Replaces anti-forgery with test decorator that skips validation
   - Adds test authentication scheme ("Test")
   - Usage: `public class TestClass : IClassFixture<WebApplicationFactoryBase>`

2. **TestDatabase** (`QuestBoard.IntegrationTests/Helpers/TestDatabase.cs`):
   - Creates in-memory DbContext with unique name per test run
   - `Reset()` method clears and recreates the database between tests
   - Accessed via `factory.Services` in test helpers

3. **TestDataHelper** (`QuestBoard.IntegrationTests/Helpers/TestDataHelper.cs`):
   - Static methods to create test entities:
     - `CreateTestQuestAsync()` — create a Quest with defaults
     - `CreatePlayerSignupAsync()` — create a PlayerSignup
     - `CreateShopItemAsync()` — create a ShopItem
     - `CreateProposedDateAsync()` — create a ProposedDate
   - `ClearDatabaseAsync()` — wipes all data between tests

4. **AuthenticationHelper** (`QuestBoard.IntegrationTests/Helpers/AuthenticationHelper.cs`):
   - `CreateTestUserAsync()` — creates a user in the identity system
   - `CreateAuthenticatedClientWithUserAsync()` — returns HttpClient with auth cookies
   - `CreateAuthenticatedDMClientAsync()` — creates a DM user and authenticated client

**Location:**
- All test helpers in `QuestBoard.IntegrationTests/Helpers/`
- Shared between all integration test classes via static methods

## Coverage

**Requirements:** Not detected in build configuration

**View Coverage:**
- No coverage report generation configured
- Integration tests include HTML content assertions (`content.Should().Contain("text")`) to verify UI rendering

## Test Types

**Unit Tests:**
- **Scope:** Individual service methods in isolation
- **Location:** `QuestBoard.UnitTests/Services/`, `QuestBoard.UnitTests/Models/`
- **Dependencies:** All mocked via NSubstitute
- **Speed:** Fast (no I/O)
- **Example:** `EmailServiceTests.SendAsync_WhenFromEmailIsEmpty_DoesNotAttemptSmtpConnection()` — verifies SMTP is not called with empty config

**Integration Tests:**
- **Scope:** Controller actions with real EF Core database
- **Location:** `QuestBoard.IntegrationTests/Controllers/`
- **Dependencies:** Real database (in-memory), real middleware
- **Speed:** Slower than unit tests
- **Example:** `QuestControllerIntegrationTests_Comprehensive.Index_Quests_Authenticated_ReturnsOk()` — verifies full MVC pipeline
- **Database:** Fresh in-memory instance per test class (via `IClassFixture<WebApplicationFactoryBase>`)

**Mobile/Rendering Tests:**
- **Scope:** CSS/layout detection and view selection
- **Location:** `QuestBoard.IntegrationTests/Mobile/`
- **Examples:**
  - `MobileDetectionMiddlewareTests.cs` — user-agent detection
  - `MobileLayoutTests.cs` — layout selection
  - `MobileViewsTests.cs` — mobile-specific cshtml rendering

**E2E Tests:**
- **Status:** Not used — integration tests serve as functional tests

## Common Patterns

**Async Testing:**
```csharp
[Fact]
public async Task SendAsync_WhenEmailNotConfigured_ReturnsWithoutException()
{
    // Arrange
    var service = Create(new EmailSettings { FromEmail = "" });

    // Act
    var act = async () => await service.SendAsync("to@example.com", "Subject", "<h1>Body</h1>");

    // Assert
    await act.Should().NotThrowAsync();
}
```

**Error Testing:**
```csharp
[Fact]
public void Constructor_WithValidOptions_DoesNotThrow()
{
    var act = () => Create(new EmailSettings());
    act.Should().NotThrow();
}

// For async
public async Task Method_WithInvalidState_ThrowsException()
{
    // Setup invalid condition
    var act = async () => await _sut.MethodAsync(...);
    await act.Should().ThrowAsync<InvalidOperationException>();
}
```

**Requirement-Linked Tests:**
- Comments reference requirements: `// D-05: /quests is the migrated quest board route`
- Assertions include requirement context: `"EMAIL-02 requires EmailService to inject IOptions<EmailSettings>"`
- Example from `EmailServiceTests.cs`:
  ```csharp
  [Fact]
  public void EmailService_ConstructorUsesIOptionsEmailSettings()
  {
      var constructor = typeof(EmailService).GetConstructors().Single();
      var firstParam = constructor.GetParameters()[0];
      firstParam.ParameterType.Should().Be(typeof(IOptions<EmailSettings>),
          "EMAIL-02 requires EmailService to inject IOptions<EmailSettings>");
  }
  ```

**Database State Reset:**
- Between test classes: `WebApplicationFactoryBase` creates new in-memory database
- Within a test: `await TestDataHelper.ClearDatabaseAsync(factory.Services)` manually clears tables
- Example: 
  ```csharp
  [Fact]
  public async Task Index_WithQuests_ShouldNotDisplayQuestList()
  {
      // Arrange — clear first
      await TestDataHelper.ClearDatabaseAsync(_factory.Services);
      var dm = await AuthenticationHelper.CreateTestUserAsync(...);
  }
  ```

**HTTP Testing:**
```csharp
// Create non-redirecting client for status code verification
var client = _factory.CreateNonRedirectingClient();
var response = await client.GetAsync("/path", TestContext.Current.CancellationToken);
response.StatusCode.Should().Be(HttpStatusCode.OK);

// Read response content
var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
content.Should().Contain("Expected Text");

// Authenticated requests
var (client, user) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(factory, "email");
var response = await client.GetAsync("/protected", TestContext.Current.CancellationToken);
```

## Test Parallelization

**Configuration:** `xunit.runner.json` disables parallel execution
- `"parallelizeAssembly": false` — assemblies run sequentially
- `"parallelizeTestCollections": false` — test collections run sequentially

**Reason:** Shared database state and Hangfire/middleware mocking require sequential execution

---

*Testing analysis: 2026-07-01*
