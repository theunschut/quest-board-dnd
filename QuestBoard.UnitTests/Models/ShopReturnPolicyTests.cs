using QuestBoard.Domain.Models.Shop;

namespace QuestBoard.UnitTests.Models;

public class ShopReturnPolicyTests
{
    private static readonly DateTime Now = new(2026, 11, 17, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Window_IsOneDay()
    {
        ShopReturnPolicy.Window.Should().Be(TimeSpan.FromHours(24));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(23.99)]
    [InlineData(24)]
    public void IsWithinReturnWindow_IsTrueUpToAndIncludingTheLimit(double hoursAgo)
    {
        ShopReturnPolicy.IsWithinReturnWindow(Now.AddHours(-hoursAgo), Now).Should().BeTrue();
    }

    [Theory]
    [InlineData(24.01)]
    [InlineData(48)]
    public void IsWithinReturnWindow_IsFalseAfterTheLimit(double hoursAgo)
    {
        ShopReturnPolicy.IsWithinReturnWindow(Now.AddHours(-hoursAgo), Now).Should().BeFalse();
    }
}
