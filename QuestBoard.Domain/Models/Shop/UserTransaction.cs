using QuestBoard.Domain.Enums;
using System.ComponentModel.DataAnnotations;

namespace QuestBoard.Domain.Models.Shop;

public class UserTransaction : IModel
{
    public int Id { get; set; }

    [Required]
    public int UserId { get; set; }

    public User? User { get; set; }

    [Required]
    public int ShopItemId { get; set; }

    public ShopItem? ShopItem { get; set; }

    [Required]
    public TransactionType TransactionType { get; set; }

    [Required]
    public decimal Price { get; set; }

    public int Quantity { get; set; } = 1;

    public DateTime TransactionDate { get; set; } = DateTime.UtcNow;

    [StringLength(500)]
    public string? Notes { get; set; }

    /// <summary>
    /// For return/sell transactions, references the original purchase transaction ID
    /// </summary>
    public int? OriginalTransactionId { get; set; }

    /// <summary>
    /// Navigation property to the original purchase transaction (for returns/sells)
    /// </summary>
    public UserTransaction? OriginalTransaction { get; set; }
}