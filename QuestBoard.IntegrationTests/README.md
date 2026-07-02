# QuestBoard Integration Tests

This project contains integration tests for the QuestBoard Quest Board application.

## Technologies Used

- **xUnit**: Testing framework
- **FluentAssertions**: Fluent assertion library for more readable tests
- **Microsoft.AspNetCore.Mvc.Testing**: WebApplicationFactory for testing ASP.NET Core applications
- **Microsoft.EntityFrameworkCore.InMemory**: In-memory database for testing

## Test Structure

### Controller Integration Tests
- `HomeControllerIntegrationTests.cs`: Tests for HomeController endpoints
  - HTTP response validation
  - Content type verification
  - Full request/response cycle testing

### Repository Integration Tests
- `QuestRepositoryIntegrationTests.cs`: Tests repository operations with real database context
  - CRUD operations with actual database
  - Data persistence verification
  - Complex queries with includes

### Test Infrastructure
- `WebApplicationFactoryBase.cs`: Base factory for creating test web applications
  - Configures in-memory database
  - Provides isolated test environment
  - Manages service registration for testing

## Running Tests

```bash
# Run all integration tests
dotnet test QuestBoard.IntegrationTests

# Run with detailed output
dotnet test QuestBoard.IntegrationTests --verbosity detailed

# Run specific test class
dotnet test QuestBoard.IntegrationTests --filter "FullyQualifiedName~HomeControllerIntegrationTests"

# Run all tests from solution root
dotnet test --filter "FullyQualifiedName~IntegrationTests"
```

## Writing New Tests

### Example Controller Integration Test

```csharp
public class MyControllerIntegrationTests : IClassFixture<WebApplicationFactoryBase>
{
    private readonly HttpClient _client;
    private readonly WebApplicationFactoryBase _factory;

    public MyControllerIntegrationTests(WebApplicationFactoryBase factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetEndpoint_ShouldReturnOk()
    {
        // Act
        var response = await _client.GetAsync("/api/myendpoint");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
```

### Example Repository Integration Test

```csharp
public class MyRepositoryIntegrationTests : IClassFixture<WebApplicationFactoryBase>
{
    private readonly WebApplicationFactoryBase _factory;

    public MyRepositoryIntegrationTests(WebApplicationFactoryBase factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task CreateAsync_ShouldPersistToDatabase()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<QuestBoardContext>();
        var repository = scope.ServiceProvider.GetRequiredService<IMyRepository>();

        var entity = new MyEntity { Name = "Test" };

        // Act
        await repository.CreateAsync(entity);
        await repository.SaveChangesAsync();

        // Assert
        var saved = await context.MyEntities.FindAsync(entity.Id);
        saved.Should().NotBeNull();
        saved!.Name.Should().Be("Test");
    }
}
```

## Key Differences from Unit Tests

1. **Real Dependencies**: Integration tests use actual implementations, not mocks
2. **Database**: Uses in-memory database that behaves like SQL Server
3. **Full Stack**: Tests the entire application stack from HTTP request to database
4. **Slower**: Integration tests are slower than unit tests
5. **Isolation**: Each test should clean up or use fresh database state

## Test Isolation

The `WebApplicationFactoryBase` creates a new in-memory database for each test run. For tests that need isolation within the same test class, clear the database:

```csharp
using var scope = _factory.Services.CreateScope();
var context = scope.ServiceProvider.GetRequiredService<QuestBoardContext>();
context.Quests.RemoveRange(context.Quests);
await context.SaveChangesAsync();
```

## Best Practices

1. **Test realistic scenarios**: Integration tests should mimic real-world usage
2. **Verify full flow**: Test from HTTP request through to database
3. **Clean state**: Ensure tests don't interfere with each other
4. **Use IClassFixture**: Share WebApplicationFactory across tests in a class
5. **Test edge cases**: Include authentication, authorization, validation failures
6. **Check side effects**: Verify database changes, not just HTTP responses

## Coverage Goals

Integration tests should cover:
- ✅ API endpoints and controllers
- ✅ Database operations end-to-end
- ✅ Authentication and authorization flows
- ✅ Complex business flows across multiple components
- ✅ Data validation and error handling
