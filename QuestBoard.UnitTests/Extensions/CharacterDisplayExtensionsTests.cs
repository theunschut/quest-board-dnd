using QuestBoard.Domain.Enums;
using QuestBoard.Domain.Models;
using QuestBoard.Service.Extensions;

namespace QuestBoard.UnitTests.Extensions;

public class CharacterDisplayExtensionsTests
{
    private static Character MakeCharacter(
        string name = "Thorin",
        int level = 5,
        CharacterStatus status = CharacterStatus.Active,
        IList<CharacterClass>? classes = null)
    {
        return new Character
        {
            Id = 1,
            Name = name,
            Level = level,
            Status = status,
            OwnerId = 1,
            Classes = classes ?? [new CharacterClass { Class = DndClass.Fighter, ClassLevel = level }]
        };
    }

    [Fact]
    public void ToSelectLabel_ForActiveCharacter_ReturnsNameLevelAndClassesWithNoStatusSuffix()
    {
        // Arrange
        var character = MakeCharacter(status: CharacterStatus.Active);

        // Act
        var label = character.ToSelectLabel();

        // Assert
        label.Should().Be("Thorin - Level 5 (Fighter 5)");
    }

    [Fact]
    public void ToSelectLabel_ForRetiredCharacter_AppendsTheStatusName()
    {
        // Arrange
        var character = MakeCharacter(status: CharacterStatus.Retired);

        // Act
        var label = character.ToSelectLabel();

        // Assert
        label.Should().Be("Thorin - Level 5 (Fighter 5) (Retired)");
    }

    [Fact]
    public void ToSelectLabel_ForDeadCharacter_AppendsTheStatusName()
    {
        // Arrange
        var character = MakeCharacter(status: CharacterStatus.Dead);

        // Act
        var label = character.ToSelectLabel();

        // Assert
        label.Should().Be("Thorin - Level 5 (Fighter 5) (Dead)");
    }

    [Fact]
    public void ToSelectLabel_WithMultipleClasses_JoinsThemWithCommaSpace()
    {
        // Arrange
        var character = MakeCharacter(classes: [
            new CharacterClass { Class = DndClass.Fighter, ClassLevel = 3 },
            new CharacterClass { Class = DndClass.Wizard, ClassLevel = 2 }
        ]);

        // Act
        var label = character.ToSelectLabel();

        // Assert
        label.Should().Be("Thorin - Level 5 (Fighter 3, Wizard 2)");
    }

    [Fact]
    public void ToSelectLabel_WithNoClasses_RendersEmptyParentheses()
    {
        // Arrange
        var character = MakeCharacter(classes: []);

        // Act
        var label = character.ToSelectLabel();

        // Assert
        label.Should().Be("Thorin - Level 5 ()");
    }
}
