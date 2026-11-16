using QuestBoard.Domain.Enums;
using QuestBoard.Service.ViewModels.ShopViewModels;

namespace QuestBoard.IntegrationTests.Tests;

/// <summary>
/// Pins the one mapping that turns a shop item's rarity, status and type into the words, class keys, fills and
/// icons every shop page shows, and the view model properties that read from it.
/// </summary>
public class ShopItemDisplayTests
{
    [Theory]
    [InlineData(ItemRarity.Common, "Common", "common")]
    [InlineData(ItemRarity.Uncommon, "Uncommon", "uncommon")]
    [InlineData(ItemRarity.Rare, "Rare", "rare")]
    [InlineData(ItemRarity.VeryRare, "Very Rare", "veryrare")]
    [InlineData(ItemRarity.Legendary, "Legendary", "legendary")]
    public void Rarity_MapsToItsWordAndKey(ItemRarity rarity, string word, string key)
    {
        ShopItemDisplay.RarityName(rarity).Should().Be(word);
        ShopItemDisplay.RarityKey(rarity).Should().Be(key);
    }

    [Theory]
    [InlineData(ItemStatus.Draft, "Pending Review", "bg-warning", "fa-hourglass-half")]
    [InlineData(ItemStatus.Published, "Available", "bg-success", "fa-check-circle")]
    [InlineData(ItemStatus.Archived, "Archived", "bg-secondary", "fa-box-archive")]
    [InlineData(ItemStatus.Denied, "Denied", "bg-danger", "fa-ban")]
    public void Status_MapsToItsWordFillAndIcon(ItemStatus status, string word, string fill, string icon)
    {
        ShopItemDisplay.StatusName(status).Should().Be(word);
        ShopItemDisplay.StatusBadgeClass(status).Should().Be(fill);
        ShopItemDisplay.StatusIcon(status).Should().Be(icon);
    }

    [Theory]
    [InlineData(ItemType.Equipment, "Equipment", "fa-hammer")]
    [InlineData(ItemType.MagicItem, "Magic Item", "fa-magic")]
    [InlineData(ItemType.QuestItems, "Quest Item", "fa-scroll")]
    public void Type_MapsToItsWordAndIcon(ItemType type, string word, string icon)
    {
        ShopItemDisplay.TypeName(type).Should().Be(word);
        ShopItemDisplay.TypeIcon(type).Should().Be(icon);
    }

    [Theory]
    [InlineData(ItemStatus.Draft)]
    [InlineData(ItemStatus.Published)]
    [InlineData(ItemStatus.Archived)]
    [InlineData(ItemStatus.Denied)]
    public void ViewModelDisplayNames_ReadFromTheSameMapping(ItemStatus status)
    {
        var item = new ShopItemViewModel { Status = status, Rarity = ItemRarity.VeryRare, Type = ItemType.QuestItems };

        item.StatusDisplayName.Should().Be(ShopItemDisplay.StatusName(status));
        item.RarityDisplayName.Should().Be(ShopItemDisplay.RarityName(ItemRarity.VeryRare));
        item.TypeDisplayName.Should().Be("Quest Item");
    }
}
