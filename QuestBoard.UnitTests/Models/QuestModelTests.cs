using QuestBoard.Domain.Models.QuestBoard;

namespace QuestBoard.UnitTests.Models;

public class QuestModelTests
{
    [Fact]
    public void Quest_ShouldInitializeWithDefaultValues()
    {
        // Act
        var quest = new Quest();

        // Assert
        quest.Id.Should().Be(0);
        quest.IsFinalized.Should().BeFalse();
        quest.ProposedDates.Should().BeEmpty();
        quest.PlayerSignups.Should().BeEmpty();
    }

    [Fact]
    public void Quest_ShouldSetProperties()
    {
        // Arrange
        var quest = new Quest();
        var title = "Epic Dragon Quest";
        var description = "Slay the dragon";

        // Act
        quest.Title = title;
        quest.Description = description;
        quest.ChallengeRating = 10;

        // Assert
        quest.Title.Should().Be(title);
        quest.Description.Should().Be(description);
        quest.ChallengeRating.Should().Be(10);
    }

    [Fact]
    public void Quest_ShouldHandleProposedDates()
    {
        // Arrange
        var quest = new Quest
        {
            ProposedDates = new List<ProposedDate>
            {
                new() { Date = DateTime.Now.AddDays(1) },
                new() { Date = DateTime.Now.AddDays(2) }
            }
        };

        // Assert
        quest.ProposedDates.Should().HaveCount(2);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    [InlineData(10)]
    [InlineData(20)]
    public void Quest_ShouldAcceptValidChallengeRatings(int rating)
    {
        // Arrange
        var quest = new Quest();

        // Act
        quest.ChallengeRating = rating;

        // Assert
        quest.ChallengeRating.Should().Be(rating);
    }

    [Fact]
    public void Quest_FinalizedDate_ShouldBeNullByDefault()
    {
        // Act
        var quest = new Quest();

        // Assert
        quest.FinalizedDate.Should().BeNull();
    }
}
