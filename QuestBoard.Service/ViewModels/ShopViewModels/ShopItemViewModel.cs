using QuestBoard.Domain.Enums;
using System.ComponentModel.DataAnnotations;

namespace QuestBoard.Service.ViewModels.ShopViewModels;

public class ShopItemViewModel
{
    public int Id { get; set; }

    [Display(Name = "Item Name")]
    public string Name { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public ItemType Type { get; set; }

    public ItemRarity Rarity { get; set; }

    [Display(Name = "Price (GP)")]
    [DisplayFormat(DataFormatString = "{0:N0}")]
    public decimal Price { get; set; }

    [Display(Name = "Stock")]
    public int Quantity { get; set; }

    public ItemStatus Status { get; set; }

    [Display(Name = "D&D Beyond Link")]
    public string? ReferenceUrl { get; set; }

    [Display(Name = "Denial Reason")]
    public string? DenialReason { get; set; }

    public DateTime? DeniedAt { get; set; }

    [Display(Name = "Created At")]
    [DisplayFormat(DataFormatString = "{0:yyyy-MM-dd HH:mm}")]
    public DateTime CreatedAt { get; set; }

    [Display(Name = "Available From")]
    [DisplayFormat(DataFormatString = "{0:yyyy-MM-dd HH:mm}")]
    public DateTime? AvailableFrom { get; set; }

    [Display(Name = "Available Until")]
    [DisplayFormat(DataFormatString = "{0:yyyy-MM-dd HH:mm}")]
    public DateTime? AvailableUntil { get; set; }

    [Display(Name = "Created By")]
    public string CreatedByDmName { get; set; } = string.Empty;

    // Helper properties for display
    public string RarityDisplayName => Rarity switch
    {
        ItemRarity.Common => "Common",
        ItemRarity.Uncommon => "Uncommon",
        ItemRarity.Rare => "Rare",
        ItemRarity.VeryRare => "Very Rare",
        ItemRarity.Legendary => "Legendary",
        _ => "Unknown"
    };

    public string RarityColorClass => Rarity switch
    {
        ItemRarity.Common => "text-secondary",
        ItemRarity.Uncommon => "text-success",
        ItemRarity.Rare => "text-primary",
        ItemRarity.VeryRare => "text-warning",
        ItemRarity.Legendary => "text-danger",
        _ => "text-muted"
    };

    public string StatusDisplayName => Status switch
    {
        ItemStatus.Draft => "Draft",
        ItemStatus.Published => "Available",
        ItemStatus.Archived => "Archived",
        ItemStatus.Denied => "Denied",
        _ => "Unknown"
    };

    public string StatusColorClass => Status switch
    {
        ItemStatus.Draft => "text-muted",
        ItemStatus.Published => "text-success",
        ItemStatus.Archived => "text-secondary",
        ItemStatus.Denied => "text-danger",
        _ => "text-muted"
    };

    public bool IsAvailable =>
        Status == ItemStatus.Published &&
        (AvailableFrom == null || AvailableFrom <= DateTime.UtcNow) &&
        (AvailableUntil == null || AvailableUntil >= DateTime.UtcNow) &&
        Quantity != 0;
}