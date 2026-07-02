# QuestBoard Unit Tests

This project contains unit tests for the QuestBoard Quest Board application.

## Technologies Used

- **xUnit**: Testing framework
- **FluentAssertions**: Fluent assertion library for more readable tests
- **NSubstitute**: Mocking framework for creating test doubles

## Test Structure

### Services Tests
- `QuestServiceTests.cs`: Tests for QuestService business logic
  - Quest finalization
  - Player selection logic
  - Spectator auto-approval
  - DM quest retrieval

- `EmailServiceTests.cs`: Tests for EmailService
  - Email configuration validation
  - Email sending functionality

### Repository Tests
- `QuestRepositoryTests.cs`: Tests for QuestRepository
  - CRUD operations
  - Data retrieval with includes
  - Quest filtering by DM

## Running Tests

```bash
# Run all unit tests
dotnet test QuestBoard.UnitTests

# Run with detailed output
dotnet test QuestBoard.UnitTests --verbosity detailed

# Run specific test class
dotnet test QuestBoard.UnitTests --filter "FullyQualifiedName~QuestServiceTests"

# Run tests from solution root
dotnet test --filter "FullyQualifiedName~UnitTests"
```

## Writing New Tests

### Example Unit Test Structure

```csharp
public class MyServiceTests
{
    private readonly IMyDependency _dependency;
    private readonly IMyService _service;

    public MyServiceTests()
    {
        // Arrange: Setup mocks
        _dependency = Substitute.For<IMyDependency>();
        _service = new MyService(_dependency);
    }

    [Fact]
    public async Task MethodName_ShouldExpectedBehavior_WhenCondition()
    {
        // Arrange
        var input = "test";
        _dependency.SomeMethod(input).Returns("mocked result");

        // Act
        var result = await _service.MethodUnderTest(input);

        // Assert
        result.Should().Be("expected result");
        _dependency.Received(1).SomeMethod(input);
    }
}
```

### Test Naming Convention

Tests follow the pattern: `MethodName_ShouldExpectedBehavior_WhenCondition`

Examples:
- `FinalizeQuestAsync_ShouldSetIsFinalized_WhenQuestExists`
- `GetQuestsByDmNameAsync_ShouldReturnMappedQuests`
- `SendEmail_ShouldThrowException_WhenEmailIsInvalid`

## Best Practices

1. **Arrange-Act-Assert**: Structure tests in three clear sections
2. **One assertion per test**: Focus on testing one thing at a time
3. **Descriptive names**: Test names should clearly describe what they test
4. **Mock external dependencies**: Use NSubstitute for all external dependencies
5. **Use FluentAssertions**: Write readable assertions with `.Should()` syntax
6. **Test edge cases**: Include tests for null values, empty collections, etc.

## Coverage Goals

Aim to cover:
- ✅ Business logic in services
- ✅ Repository methods (with in-memory database)
- ✅ Edge cases and error handling
- ✅ Validation logic
