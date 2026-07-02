using QuestBoard.Domain.Enums;

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
}