namespace QuestBoard.UnitTests.Helpers;

public class DateHelperTests
{
    [Fact]
    public void DateTime_ShouldFormatCorrectly()
    {
        // Arrange
        var date = new DateTime(2024, 1, 15, 14, 30, 0);

        // Act
        var formatted = date.ToString("yyyy-MM-dd HH:mm");

        // Assert
        formatted.Should().Be("2024-01-15 14:30");
    }

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

    [Fact]
    public void DateTime_Comparison_ShouldWork()
    {
        // Arrange
        var earlier = new DateTime(2024, 1, 1);
        var later = new DateTime(2024, 12, 31);

        // Assert
        earlier.Should().BeBefore(later);
        later.Should().BeAfter(earlier);
    }
}
