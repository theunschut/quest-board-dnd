using QuestBoard.Domain.Enums;

namespace QuestBoard.Service.ViewModels.ShopViewModels;

public class UserTransactionViewModel
{
    // A purchase can be returned for its full price during the first day and can only be sold back for half after that,
    // the same rule the shop service applies when the buyer posts the form.
    private static readonly TimeSpan ReturnWindow = TimeSpan.FromHours(24);

    public int Id { get; set; }
    public int ShopItemId { get; set; }
    public string ItemName { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal Price { get; set; }
    public DateTime TransactionDate { get; set; }
    public TransactionType TransactionType { get; set; }
    public int RemainingQuantity { get; set; } // For purchase transactions, how many can still be returned/sold

    // The age of the purchase is measured against the real current instant (a purchase is stored in UTC), not the
    // board's calendar day, so this is the one clock read the shop's view models make for it.
    public bool CanReturn => DateTime.UtcNow - TransactionDate <= ReturnWindow;

    // What selling the remaining items back to the shop pays: half of the original unit price for each item left, the
    // same figure the shop service pays out. Zero when nothing is left to sell.
    public decimal SellRefund =>
        Quantity > 0 && RemainingQuantity > 0 ? Price / Quantity * RemainingQuantity * 0.5m : 0m;
}
