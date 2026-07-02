using QuestBoard.Domain.Extensions;
using QuestBoard.Domain.Models;

namespace QuestBoard.UnitTests.Services;

public class UserExtensionsTests
{
    [Fact]
    public void WhereEmailConfirmed_WithMixedUsers_ReturnsOnlyConfirmed()
    {
        // Arrange
        var users = new List<User>
        {
            new() { Id = 1, Name = "Alice", EmailConfirmed = true },
            new() { Id = 2, Name = "Bob",   EmailConfirmed = false },
            new() { Id = 3, Name = "Carol", EmailConfirmed = true },
        };

        // Act
        var result = users.WhereEmailConfirmed().ToList();

        // Assert
        result.Should().HaveCount(2);
        result.Should().OnlyContain(u => u.EmailConfirmed);
    }

    [Fact]
    public void WhereEmailConfirmed_AllUnconfirmed_ReturnsEmptySequence()
    {
        // Arrange
        var users = new List<User>
        {
            new() { Id = 1, Name = "Alice", EmailConfirmed = false },
            new() { Id = 2, Name = "Bob",   EmailConfirmed = false },
        };

        // Act
        var result = users.WhereEmailConfirmed().ToList();

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void WhereEmailConfirmed_EmptyInput_ReturnsEmptySequenceWithoutException()
    {
        // Arrange
        var users = new List<User>();

        // Act
        var result = users.WhereEmailConfirmed().ToList();

        // Assert
        result.Should().BeEmpty();
    }
}
