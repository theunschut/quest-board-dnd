using QuestBoard.Domain.Enums;

namespace QuestBoard.Service.ViewModels.ShopViewModels;

/// <summary>
/// The one place that turns a shop item's rarity, status and type into the words, class keys and icons the shop
/// pages show. Views and partials call these instead of formatting an enum name themselves, so a label is spelled
/// the same way on every page and in both layouts.
/// </summary>
public static class ShopItemDisplay
{
    /// <summary>The word shown for a rarity.</summary>
    public static string RarityName(ItemRarity rarity) => rarity switch
    {
        ItemRarity.Common => "Common",
        ItemRarity.Uncommon => "Uncommon",
        ItemRarity.Rare => "Rare",
        ItemRarity.VeryRare => "Very Rare",
        ItemRarity.Legendary => "Legendary",
        _ => "Unknown"
    };

    /// <summary>The lower-case key that names a rarity in a class (<c>rarity-veryrare</c>).</summary>
    public static string RarityKey(ItemRarity rarity) => rarity switch
    {
        ItemRarity.Common => "common",
        ItemRarity.Uncommon => "uncommon",
        ItemRarity.Rare => "rare",
        ItemRarity.VeryRare => "veryrare",
        ItemRarity.Legendary => "legendary",
        _ => "common"
    };

    /// <summary>The word shown for a status; a draft reads as waiting for review.</summary>
    public static string StatusName(ItemStatus status) => status switch
    {
        ItemStatus.Draft => "Pending Review",
        ItemStatus.Published => "Available",
        ItemStatus.Archived => "Archived",
        ItemStatus.Denied => "Denied",
        _ => "Unknown"
    };

    /// <summary>The badge fill class for a status.</summary>
    public static string StatusBadgeClass(ItemStatus status) => status switch
    {
        ItemStatus.Draft => "bg-warning",
        ItemStatus.Published => "bg-success",
        ItemStatus.Archived => "bg-secondary",
        ItemStatus.Denied => "bg-danger",
        _ => "bg-secondary"
    };

    /// <summary>The Font Awesome icon class for a status, so a status is never colour alone.</summary>
    public static string StatusIcon(ItemStatus status) => status switch
    {
        ItemStatus.Draft => "fa-hourglass-half",
        ItemStatus.Published => "fa-check-circle",
        ItemStatus.Archived => "fa-box-archive",
        ItemStatus.Denied => "fa-ban",
        _ => "fa-circle-question"
    };

    /// <summary>The word shown for an item type.</summary>
    public static string TypeName(ItemType type) => type switch
    {
        ItemType.Equipment => "Equipment",
        ItemType.MagicItem => "Magic Item",
        ItemType.QuestItems => "Quest Item",
        _ => "Item"
    };

    /// <summary>The Font Awesome icon class for an item type.</summary>
    public static string TypeIcon(ItemType type) => type switch
    {
        ItemType.Equipment => "fa-hammer",
        ItemType.MagicItem => "fa-magic",
        ItemType.QuestItems => "fa-scroll",
        _ => "fa-box"
    };
}
