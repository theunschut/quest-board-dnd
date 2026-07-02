using QuestBoard.Domain.Enums;
using System.ComponentModel.DataAnnotations;

namespace QuestBoard.Domain.Models.Shop;

public class ShopItem : IModel
{
    public int Id { get; set; }

    [Required]
    [StringLength(200)]
    public string Name { get; set; } = string.Empty;

    [Required]
    public string Description { get; set; } = string.Empty;

    [Required]
    public ItemType Type { get; set; }

    [Required]
    public ItemRarity Rarity { get; set; }

    [Required]
    public decimal Price { get; set; }

    public int Quantity { get; set; } = 0;

    [Required]
    public ItemStatus Status { get; set; } = ItemStatus.Draft;

    [StringLength(500)]
    public string? ReferenceUrl { get; set; }

    [StringLength(1000)]
    public string? DenialReason { get; set; }

    public DateTime? DeniedAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? AvailableFrom { get; set; }

    public DateTime? AvailableUntil { get; set; }

    public int GroupId { get; set; }

    public int CreatedByDmId { get; set; }

    public User? CreatedByDm { get; set; }

    public IList<UserTransaction> Transactions { get; set; } = [];
}