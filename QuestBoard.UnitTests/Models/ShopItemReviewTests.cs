using QuestBoard.Domain.Enums;
using QuestBoard.Domain.Models.Shop;

namespace QuestBoard.UnitTests.Models;

// The review rule takes plain enum values, so every cell of it is checked here with no host and no clock.
public class ShopItemReviewTests
{
    [Theory]
    [InlineData(ItemRarity.Common, false)]
    [InlineData(ItemRarity.Uncommon, false)]
    [InlineData(ItemRarity.Rare, true)]
    [InlineData(ItemRarity.VeryRare, true)]
    [InlineData(ItemRarity.Legendary, true)]
    public void NeedsReview_IsFalseOnlyForCommonAndUncommon(ItemRarity rarity, bool expected)
    {
        ShopItemReview.NeedsReview(rarity).Should().Be(expected);
    }

    [Theory]
    [InlineData(ItemRarity.Common, ItemStatus.Published)]
    [InlineData(ItemRarity.Uncommon, ItemStatus.Published)]
    [InlineData(ItemRarity.Rare, ItemStatus.Draft)]
    [InlineData(ItemRarity.VeryRare, ItemStatus.Draft)]
    [InlineData(ItemRarity.Legendary, ItemStatus.Draft)]
    public void StatusOnCreate_PublishesOnlyContentThatNeedsNoReview(ItemRarity rarity, ItemStatus expected)
    {
        ShopItemReview.StatusOnCreate(rarity).Should().Be(expected);
    }

    [Theory]
    // A draft stays a draft whatever it is edited to: an edit never publishes.
    [InlineData(ItemStatus.Draft, ItemRarity.Common, ItemStatus.Draft)]
    [InlineData(ItemStatus.Draft, ItemRarity.Uncommon, ItemStatus.Draft)]
    [InlineData(ItemStatus.Draft, ItemRarity.Rare, ItemStatus.Draft)]
    [InlineData(ItemStatus.Draft, ItemRarity.VeryRare, ItemStatus.Draft)]
    [InlineData(ItemStatus.Draft, ItemRarity.Legendary, ItemStatus.Draft)]
    // A denied item is resubmitted as a draft whatever it is edited to.
    [InlineData(ItemStatus.Denied, ItemRarity.Common, ItemStatus.Draft)]
    [InlineData(ItemStatus.Denied, ItemRarity.Uncommon, ItemStatus.Draft)]
    [InlineData(ItemStatus.Denied, ItemRarity.Rare, ItemStatus.Draft)]
    [InlineData(ItemStatus.Denied, ItemRarity.VeryRare, ItemStatus.Draft)]
    [InlineData(ItemStatus.Denied, ItemRarity.Legendary, ItemStatus.Draft)]
    // A published item stays on sale only at a rarity that needs no review.
    [InlineData(ItemStatus.Published, ItemRarity.Common, ItemStatus.Published)]
    [InlineData(ItemStatus.Published, ItemRarity.Uncommon, ItemStatus.Published)]
    [InlineData(ItemStatus.Published, ItemRarity.Rare, ItemStatus.Draft)]
    [InlineData(ItemStatus.Published, ItemRarity.VeryRare, ItemStatus.Draft)]
    [InlineData(ItemStatus.Published, ItemRarity.Legendary, ItemStatus.Draft)]
    // An archived item stays archived only at a rarity that needs no review, because Reopen puts it back on sale.
    [InlineData(ItemStatus.Archived, ItemRarity.Common, ItemStatus.Archived)]
    [InlineData(ItemStatus.Archived, ItemRarity.Uncommon, ItemStatus.Archived)]
    [InlineData(ItemStatus.Archived, ItemRarity.Rare, ItemStatus.Draft)]
    [InlineData(ItemStatus.Archived, ItemRarity.VeryRare, ItemStatus.Draft)]
    [InlineData(ItemStatus.Archived, ItemRarity.Legendary, ItemStatus.Draft)]
    public void StatusAfterEdit_NeverPublishesAndSendsContentThatNeedsReviewBackToReview(
        ItemStatus current, ItemRarity newRarity, ItemStatus expected)
    {
        ShopItemReview.StatusAfterEdit(current, newRarity).Should().Be(expected);
    }
}
