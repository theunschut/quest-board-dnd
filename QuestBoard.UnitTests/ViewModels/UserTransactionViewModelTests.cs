using QuestBoard.Domain.Enums;
using QuestBoard.Service.ViewModels.ShopViewModels;

namespace QuestBoard.UnitTests.ViewModels;

public class UserTransactionViewModelTests
{
    private static UserTransactionViewModel Purchase(double hoursAgo, int quantity = 3, int remaining = 2, decimal price = 300m) => new()
    {
        Id = 1,
        ItemName = "Plain Lantern",
        TransactionType = TransactionType.Purchase,
        Quantity = quantity,
        RemainingQuantity = remaining,
        Price = price,
        TransactionDate = DateTime.UtcNow.AddHours(-hoursAgo),
    };

    [Theory]
    [InlineData(0.5, true)]
    [InlineData(2, true)]
    [InlineData(23, true)]
    [InlineData(25, false)]
    [InlineData(30, false)]
    [InlineData(24 * 30, false)]
    public void CanReturn_IsTrueOnlyDuringTheFirstDay(double hoursAgo, bool expected)
    {
        Purchase(hoursAgo).CanReturn.Should().Be(expected);
    }

    [Fact]
    public void SellRefund_IsHalfTheOriginalUnitPriceForEachItemLeft()
    {
        // Three units for 300 gp is 100 gp each; two are left, so the shop pays half of 200.
        Purchase(30, quantity: 3, remaining: 2, price: 300m).SellRefund.Should().Be(100m);
    }

    [Fact]
    public void SellRefund_IsZeroWhenNothingIsLeftOrTheQuantityIsZero()
    {
        Purchase(30, quantity: 3, remaining: 0, price: 300m).SellRefund.Should().Be(0m);
        Purchase(30, quantity: 0, remaining: 0, price: 300m).SellRefund.Should().Be(0m);
        Purchase(30, quantity: 0, remaining: 2, price: 300m).SellRefund.Should().Be(0m, "a purchase of no items has no unit price to divide by");
    }
}
