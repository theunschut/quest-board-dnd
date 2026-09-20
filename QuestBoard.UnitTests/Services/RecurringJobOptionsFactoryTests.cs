using QuestBoard.Service.Extensions;
using QuestBoard.UnitTests.Helpers;

namespace QuestBoard.UnitTests.Services;

public class RecurringJobOptionsFactoryTests
{
    [Fact]
    public void ForBoardZone_ReturnsOptions_WithTimeZoneReferenceEqualToClockZone()
    {
        // Arrange
        var zone = TimeZoneInfo.FindSystemTimeZoneById("Etc/GMT-2");
        var clock = new FakeBoardClock { TimeZone = zone };

        // Act
        var options = RecurringJobOptionsFactory.ForBoardZone(clock);

        // Assert
        Assert.Same(zone, options.TimeZone);
    }

    [Fact]
    public void ForBoardZone_WithNonUtcClockZone_DoesNotDefaultToUtc()
    {
        // Arrange — the exact default the three registrations silently took before this change.
        var zone = TimeZoneInfo.FindSystemTimeZoneById("Etc/GMT-2");
        var clock = new FakeBoardClock { TimeZone = zone };

        // Act
        var options = RecurringJobOptionsFactory.ForBoardZone(clock);

        // Assert
        Assert.NotEqual(TimeZoneInfo.Utc, options.TimeZone);
    }
}
