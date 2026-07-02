using QuestBoard.Domain.Enums;
using System.ComponentModel.DataAnnotations;

namespace QuestBoard.Service.ViewModels.ShopViewModels;

public class CreateShopItemViewModel
{
    [Required]
    [StringLength(200)]
    [Display(Name = "Item Name")]
    public string Name { get; set; } = string.Empty;

    [Required]
    [Display(Name = "Description")]
    public string Description { get; set; } = string.Empty;

    [Required]
    [Display(Name = "Item Type")]
    public ItemType? Type { get; set; }

    [Required]
    [Display(Name = "Rarity")]
    public ItemRarity? Rarity { get; set; }

    [Required]
    [Display(Name = "Price (GP)")]
    [Range(0, double.MaxValue, ErrorMessage = "Price must be 0 or greater")]
    public decimal Price { get; set; }

    [Display(Name = "Quantity (-1 = unlimited)")]
    [Range(-1, int.MaxValue)]
    public int Quantity { get; set; } = 1;

    [Display(Name = "D&D Beyond Reference URL")]
    [StringLength(500)]
    [Url]
    public string? ReferenceUrl { get; set; }

    [Display(Name = "Available From")]
    [DataType(DataType.DateTime)]
    public DateTime? AvailableFrom { get; set; }

    [Display(Name = "Available Until")]
    [DataType(DataType.DateTime)]
    public DateTime? AvailableUntil { get; set; }
}