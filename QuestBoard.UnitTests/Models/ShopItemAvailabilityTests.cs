using QuestBoard.Domain.Enums;
using QuestBoard.Domain.Models.Shop;

namespace QuestBoard.UnitTests.Models;

// The window values are board-local wall-clock times, so every value here is a plain wall-clock DateTime and no clock
// is read: the rule is handed the board's local time by its caller.
public class ShopItemAvailabilityTests
{
    private static readonly DateTime BoardNow = new(2026, 9, 6, 0, 30, 0);

    private static ShopItem Item(ItemStatus status = ItemStatus.Published, DateTime? from = null, DateTime? until = null) =>
        new() { Id = 1, Name = "Lantern", Status = status, AvailableFrom = from, AvailableUntil = until };

    [Fact]
    public void APublishedItemWithNoWindow_IsOnSaleAndHasOpened()
    {
        var item = Item();

        ShopItemAvailability.HasOpened(item, BoardNow).Should().BeTrue();
        ShopItemAvailability.IsOnSale(item, BoardNow).Should().BeTrue();
    }

    [Fact]
    public void AStartExactlyAtNow_HasOpenedAndIsOnSale()
    {
        var item = Item(from: new DateTime(2026, 9, 6, 0, 30, 0));

        ShopItemAvailability.HasOpened(item, BoardNow).Should().BeTrue("the start counts as inside the window");
        ShopItemAvailability.IsOnSale(item, BoardNow).Should().BeTrue();
    }

    [Fact]
    public void AStartOneMinuteAhead_HasNotOpenedAndIsNotOnSale()
    {
        var item = Item(from: new DateTime(2026, 9, 6, 0, 31, 0));

        ShopItemAvailability.HasOpened(item, BoardNow).Should().BeFalse();
        ShopItemAvailability.IsOnSale(item, BoardNow).Should().BeFalse();
    }

    [Fact]
    public void AStartAtMidnightTheSameBoardDay_IsOnSale()
    {
        var item = Item(from: new DateTime(2026, 9, 6, 0, 0, 0));

        ShopItemAvailability.HasOpened(item, BoardNow).Should().BeTrue();
        ShopItemAvailability.IsOnSale(item, BoardNow).Should().BeTrue();
    }

    [Fact]
    public void AnEndExactlyAtNow_IsOnSale()
    {
        var item = Item(until: new DateTime(2026, 9, 6, 0, 30, 0));

        ShopItemAvailability.IsOnSale(item, BoardNow).Should().BeTrue("the end counts as inside the window");
    }

    [Theory]
    [InlineData(2026, 9, 6, 0, 29)]
    [InlineData(2026, 9, 5, 23, 59)]
    public void AnEndBeforeNow_IsNotOnSaleButHasOpened(int year, int month, int day, int hour, int minute)
    {
        var item = Item(until: new DateTime(year, month, day, hour, minute, 0));

        ShopItemAvailability.HasOpened(item, BoardNow).Should().BeTrue("a closed sale was open once");
        ShopItemAvailability.IsOnSale(item, BoardNow).Should().BeFalse();
    }

    [Theory]
    [InlineData(ItemStatus.Draft)]
    [InlineData(ItemStatus.Denied)]
    [InlineData(ItemStatus.Archived)]
    public void AnItemThatIsNotPublished_IsNotOnSaleInsideItsWindow(ItemStatus status)
    {
        var item = Item(status, from: new DateTime(2026, 9, 1), until: new DateTime(2026, 9, 30));

        ShopItemAvailability.IsOnSale(item, BoardNow).Should().BeFalse();
    }

    [Theory]
    [InlineData(2026, 9, 6, 0, 30)]
    [InlineData(2026, 9, 7, 0, 0)]
    [InlineData(2026, 9, 1, 0, 0)]
    public void AStartAfterTheEnd_IsNeverOnSale(int year, int month, int day, int hour, int minute)
    {
        var item = Item(from: new DateTime(2026, 9, 6, 12, 0, 0), until: new DateTime(2026, 9, 6, 6, 0, 0));

        ShopItemAvailability.IsOnSale(item, new DateTime(year, month, day, hour, minute, 0)).Should().BeFalse();
    }

    [Theory]
    [InlineData(ItemStatus.Published, true)]
    [InlineData(ItemStatus.Archived, true)]
    [InlineData(ItemStatus.Draft, false)]
    [InlineData(ItemStatus.Denied, false)]
    public void VisibleToMembers_WithNoWindow_DependsOnlyOnTheStatus(ItemStatus status, bool expected)
    {
        ShopItemAvailability.IsVisibleToMembers(Item(status), BoardNow).Should().Be(expected);
    }

    [Theory]
    [InlineData(ItemStatus.Published)]
    [InlineData(ItemStatus.Archived)]
    public void VisibleToMembers_AStartAtOrBeforeNow_IsVisible(ItemStatus status)
    {
        ShopItemAvailability.IsVisibleToMembers(Item(status, from: BoardNow), BoardNow).Should().BeTrue("the start counts as inside the window");
        ShopItemAvailability.IsVisibleToMembers(Item(status, from: BoardNow.AddMinutes(-1)), BoardNow).Should().BeTrue();
    }

    [Theory]
    [InlineData(ItemStatus.Published)]
    [InlineData(ItemStatus.Archived)]
    [InlineData(ItemStatus.Draft)]
    [InlineData(ItemStatus.Denied)]
    public void VisibleToMembers_AStartAhead_IsNeverVisible(ItemStatus status)
    {
        ShopItemAvailability.IsVisibleToMembers(Item(status, from: BoardNow.AddMinutes(1)), BoardNow).Should().BeFalse();
    }

    [Fact]
    public void VisibleToMembers_APublishedItemWhoseSaleEnded_StaysVisible()
    {
        var item = Item(from: new DateTime(2026, 9, 1), until: new DateTime(2026, 9, 5));

        ShopItemAvailability.IsOnSale(item, BoardNow).Should().BeFalse();
        ShopItemAvailability.IsVisibleToMembers(item, BoardNow).Should().BeTrue("it was on sale once, and a purchase refused at the last moment lands on its page");
    }
}
