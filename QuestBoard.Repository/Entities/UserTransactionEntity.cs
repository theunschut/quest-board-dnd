using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QuestBoard.Repository.Entities;

[Table("UserTransactions")]
public class UserTransactionEntity : IEntity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    [Required]
    public int UserId { get; set; }

    [ForeignKey(nameof(UserId))]
    public virtual UserEntity User { get; set; } = null!;

    [Required]
    public int ShopItemId { get; set; }

    [ForeignKey(nameof(ShopItemId))]
    public virtual ShopItemEntity ShopItem { get; set; } = null!;

    [Required]
    public int TransactionType { get; set; }

    [Required]
    [Column(TypeName = "decimal(18,2)")]
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
    [ForeignKey(nameof(OriginalTransactionId))]
    public virtual UserTransactionEntity? OriginalTransaction { get; set; }
}