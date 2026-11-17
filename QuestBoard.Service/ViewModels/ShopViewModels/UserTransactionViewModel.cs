using QuestBoard.Domain.Enums;
using QuestBoard.Domain.Models.Shop;

namespace QuestBoard.Service.ViewModels.ShopViewModels;

public class UserTransactionViewModel
{
    public int Id { get; set; }
    public int ShopItemId { get; set; }
    public string ItemName { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal Price { get; set; }
    public DateTime TransactionDate { get; set; }
    public TransactionType TransactionType { get; set; }
    public int RemainingQuantity { get; set; } // For purchase transactions, how many can still be returned/sold

    // The age of the purchase is measured against the real current instant (a purchase is stored in UTC), not the
    // board's calendar day, so this is the one clock read the shop's view models make for it. The window itself is the
    // one the shop service applies when the buyer posts the form: a purchase can be returned for its full price inside
    // it and can only be sold back for half after that.
    public bool CanReturn => ShopReturnPolicy.IsWithinReturnWindow(TransactionDate, DateTime.UtcNow);

    // What selling the remaining items back to the shop pays: half of the original unit price for each item left, the
    // same figure the shop service pays out. Zero when nothing is left to sell.
    public decimal SellRefund =>
        Quantity > 0 && RemainingQuantity > 0 ? Price / Quantity * RemainingQuantity * 0.5m : 0m;
}
