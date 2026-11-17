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
    // A draft stays a draft whatever it is edited from or to: an edit never publishes.
    [InlineData(ItemStatus.Draft, ItemRarity.Common, ItemRarity.Common, ItemStatus.Draft)]
    [InlineData(ItemStatus.Draft, ItemRarity.Common, ItemRarity.Uncommon, ItemStatus.Draft)]
    [InlineData(ItemStatus.Draft, ItemRarity.Common, ItemRarity.Rare, ItemStatus.Draft)]
    [InlineData(ItemStatus.Draft, ItemRarity.Common, ItemRarity.VeryRare, ItemStatus.Draft)]
    [InlineData(ItemStatus.Draft, ItemRarity.Common, ItemRarity.Legendary, ItemStatus.Draft)]
    [InlineData(ItemStatus.Draft, ItemRarity.Uncommon, ItemRarity.Common, ItemStatus.Draft)]
    [InlineData(ItemStatus.Draft, ItemRarity.Uncommon, ItemRarity.Uncommon, ItemStatus.Draft)]
    [InlineData(ItemStatus.Draft, ItemRarity.Uncommon, ItemRarity.Rare, ItemStatus.Draft)]
    [InlineData(ItemStatus.Draft, ItemRarity.Uncommon, ItemRarity.VeryRare, ItemStatus.Draft)]
    [InlineData(ItemStatus.Draft, ItemRarity.Uncommon, ItemRarity.Legendary, ItemStatus.Draft)]
    [InlineData(ItemStatus.Draft, ItemRarity.Rare, ItemRarity.Common, ItemStatus.Draft)]
    [InlineData(ItemStatus.Draft, ItemRarity.Rare, ItemRarity.Uncommon, ItemStatus.Draft)]
    [InlineData(ItemStatus.Draft, ItemRarity.Rare, ItemRarity.Rare, ItemStatus.Draft)]
    [InlineData(ItemStatus.Draft, ItemRarity.Rare, ItemRarity.VeryRare, ItemStatus.Draft)]
    [InlineData(ItemStatus.Draft, ItemRarity.Rare, ItemRarity.Legendary, ItemStatus.Draft)]
    [InlineData(ItemStatus.Draft, ItemRarity.VeryRare, ItemRarity.Common, ItemStatus.Draft)]
    [InlineData(ItemStatus.Draft, ItemRarity.VeryRare, ItemRarity.Uncommon, ItemStatus.Draft)]
    [InlineData(ItemStatus.Draft, ItemRarity.VeryRare, ItemRarity.Rare, ItemStatus.Draft)]
    [InlineData(ItemStatus.Draft, ItemRarity.VeryRare, ItemRarity.VeryRare, ItemStatus.Draft)]
    [InlineData(ItemStatus.Draft, ItemRarity.VeryRare, ItemRarity.Legendary, ItemStatus.Draft)]
    [InlineData(ItemStatus.Draft, ItemRarity.Legendary, ItemRarity.Common, ItemStatus.Draft)]
    [InlineData(ItemStatus.Draft, ItemRarity.Legendary, ItemRarity.Uncommon, ItemStatus.Draft)]
    [InlineData(ItemStatus.Draft, ItemRarity.Legendary, ItemRarity.Rare, ItemStatus.Draft)]
    [InlineData(ItemStatus.Draft, ItemRarity.Legendary, ItemRarity.VeryRare, ItemStatus.Draft)]
    [InlineData(ItemStatus.Draft, ItemRarity.Legendary, ItemRarity.Legendary, ItemStatus.Draft)]
    // A denied item is resubmitted as a draft whatever it is edited from or to.
    [InlineData(ItemStatus.Denied, ItemRarity.Common, ItemRarity.Common, ItemStatus.Draft)]
    [InlineData(ItemStatus.Denied, ItemRarity.Common, ItemRarity.Uncommon, ItemStatus.Draft)]
    [InlineData(ItemStatus.Denied, ItemRarity.Common, ItemRarity.Rare, ItemStatus.Draft)]
    [InlineData(ItemStatus.Denied, ItemRarity.Common, ItemRarity.VeryRare, ItemStatus.Draft)]
    [InlineData(ItemStatus.Denied, ItemRarity.Common, ItemRarity.Legendary, ItemStatus.Draft)]
    [InlineData(ItemStatus.Denied, ItemRarity.Uncommon, ItemRarity.Common, ItemStatus.Draft)]
    [InlineData(ItemStatus.Denied, ItemRarity.Uncommon, ItemRarity.Uncommon, ItemStatus.Draft)]
    [InlineData(ItemStatus.Denied, ItemRarity.Uncommon, ItemRarity.Rare, ItemStatus.Draft)]
    [InlineData(ItemStatus.Denied, ItemRarity.Uncommon, ItemRarity.VeryRare, ItemStatus.Draft)]
    [InlineData(ItemStatus.Denied, ItemRarity.Uncommon, ItemRarity.Legendary, ItemStatus.Draft)]
    [InlineData(ItemStatus.Denied, ItemRarity.Rare, ItemRarity.Common, ItemStatus.Draft)]
    [InlineData(ItemStatus.Denied, ItemRarity.Rare, ItemRarity.Uncommon, ItemStatus.Draft)]
    [InlineData(ItemStatus.Denied, ItemRarity.Rare, ItemRarity.Rare, ItemStatus.Draft)]
    [InlineData(ItemStatus.Denied, ItemRarity.Rare, ItemRarity.VeryRare, ItemStatus.Draft)]
    [InlineData(ItemStatus.Denied, ItemRarity.Rare, ItemRarity.Legendary, ItemStatus.Draft)]
    [InlineData(ItemStatus.Denied, ItemRarity.VeryRare, ItemRarity.Common, ItemStatus.Draft)]
    [InlineData(ItemStatus.Denied, ItemRarity.VeryRare, ItemRarity.Uncommon, ItemStatus.Draft)]
    [InlineData(ItemStatus.Denied, ItemRarity.VeryRare, ItemRarity.Rare, ItemStatus.Draft)]
    [InlineData(ItemStatus.Denied, ItemRarity.VeryRare, ItemRarity.VeryRare, ItemStatus.Draft)]
    [InlineData(ItemStatus.Denied, ItemRarity.VeryRare, ItemRarity.Legendary, ItemStatus.Draft)]
    [InlineData(ItemStatus.Denied, ItemRarity.Legendary, ItemRarity.Common, ItemStatus.Draft)]
    [InlineData(ItemStatus.Denied, ItemRarity.Legendary, ItemRarity.Uncommon, ItemStatus.Draft)]
    [InlineData(ItemStatus.Denied, ItemRarity.Legendary, ItemRarity.Rare, ItemStatus.Draft)]
    [InlineData(ItemStatus.Denied, ItemRarity.Legendary, ItemRarity.VeryRare, ItemStatus.Draft)]
    [InlineData(ItemStatus.Denied, ItemRarity.Legendary, ItemRarity.Legendary, ItemStatus.Draft)]
    // A published item goes back to review only when the edit raises it from a rarity that needs no review into one
    // that does. Kept at Rare or above, lowered, or kept at Common or Uncommon, it stays on sale.
    [InlineData(ItemStatus.Published, ItemRarity.Common, ItemRarity.Common, ItemStatus.Published)]
    [InlineData(ItemStatus.Published, ItemRarity.Common, ItemRarity.Uncommon, ItemStatus.Published)]
    [InlineData(ItemStatus.Published, ItemRarity.Common, ItemRarity.Rare, ItemStatus.Draft)]
    [InlineData(ItemStatus.Published, ItemRarity.Common, ItemRarity.VeryRare, ItemStatus.Draft)]
    [InlineData(ItemStatus.Published, ItemRarity.Common, ItemRarity.Legendary, ItemStatus.Draft)]
    [InlineData(ItemStatus.Published, ItemRarity.Uncommon, ItemRarity.Common, ItemStatus.Published)]
    [InlineData(ItemStatus.Published, ItemRarity.Uncommon, ItemRarity.Uncommon, ItemStatus.Published)]
    [InlineData(ItemStatus.Published, ItemRarity.Uncommon, ItemRarity.Rare, ItemStatus.Draft)]
    [InlineData(ItemStatus.Published, ItemRarity.Uncommon, ItemRarity.VeryRare, ItemStatus.Draft)]
    [InlineData(ItemStatus.Published, ItemRarity.Uncommon, ItemRarity.Legendary, ItemStatus.Draft)]
    [InlineData(ItemStatus.Published, ItemRarity.Rare, ItemRarity.Common, ItemStatus.Published)]
    [InlineData(ItemStatus.Published, ItemRarity.Rare, ItemRarity.Uncommon, ItemStatus.Published)]
    [InlineData(ItemStatus.Published, ItemRarity.Rare, ItemRarity.Rare, ItemStatus.Published)]
    [InlineData(ItemStatus.Published, ItemRarity.Rare, ItemRarity.VeryRare, ItemStatus.Published)]
    [InlineData(ItemStatus.Published, ItemRarity.Rare, ItemRarity.Legendary, ItemStatus.Published)]
    [InlineData(ItemStatus.Published, ItemRarity.VeryRare, ItemRarity.Common, ItemStatus.Published)]
    [InlineData(ItemStatus.Published, ItemRarity.VeryRare, ItemRarity.Uncommon, ItemStatus.Published)]
    [InlineData(ItemStatus.Published, ItemRarity.VeryRare, ItemRarity.Rare, ItemStatus.Published)]
    [InlineData(ItemStatus.Published, ItemRarity.VeryRare, ItemRarity.VeryRare, ItemStatus.Published)]
    [InlineData(ItemStatus.Published, ItemRarity.VeryRare, ItemRarity.Legendary, ItemStatus.Published)]
    [InlineData(ItemStatus.Published, ItemRarity.Legendary, ItemRarity.Common, ItemStatus.Published)]
    [InlineData(ItemStatus.Published, ItemRarity.Legendary, ItemRarity.Uncommon, ItemStatus.Published)]
    [InlineData(ItemStatus.Published, ItemRarity.Legendary, ItemRarity.Rare, ItemStatus.Published)]
    [InlineData(ItemStatus.Published, ItemRarity.Legendary, ItemRarity.VeryRare, ItemStatus.Published)]
    [InlineData(ItemStatus.Published, ItemRarity.Legendary, ItemRarity.Legendary, ItemStatus.Published)]
    // An archived item follows the same rule, because Reopen would put a raised item back on sale unreviewed.
    [InlineData(ItemStatus.Archived, ItemRarity.Common, ItemRarity.Common, ItemStatus.Archived)]
    [InlineData(ItemStatus.Archived, ItemRarity.Common, ItemRarity.Uncommon, ItemStatus.Archived)]
    [InlineData(ItemStatus.Archived, ItemRarity.Common, ItemRarity.Rare, ItemStatus.Draft)]
    [InlineData(ItemStatus.Archived, ItemRarity.Common, ItemRarity.VeryRare, ItemStatus.Draft)]
    [InlineData(ItemStatus.Archived, ItemRarity.Common, ItemRarity.Legendary, ItemStatus.Draft)]
    [InlineData(ItemStatus.Archived, ItemRarity.Uncommon, ItemRarity.Common, ItemStatus.Archived)]
    [InlineData(ItemStatus.Archived, ItemRarity.Uncommon, ItemRarity.Uncommon, ItemStatus.Archived)]
    [InlineData(ItemStatus.Archived, ItemRarity.Uncommon, ItemRarity.Rare, ItemStatus.Draft)]
    [InlineData(ItemStatus.Archived, ItemRarity.Uncommon, ItemRarity.VeryRare, ItemStatus.Draft)]
    [InlineData(ItemStatus.Archived, ItemRarity.Uncommon, ItemRarity.Legendary, ItemStatus.Draft)]
    [InlineData(ItemStatus.Archived, ItemRarity.Rare, ItemRarity.Common, ItemStatus.Archived)]
    [InlineData(ItemStatus.Archived, ItemRarity.Rare, ItemRarity.Uncommon, ItemStatus.Archived)]
    [InlineData(ItemStatus.Archived, ItemRarity.Rare, ItemRarity.Rare, ItemStatus.Archived)]
    [InlineData(ItemStatus.Archived, ItemRarity.Rare, ItemRarity.VeryRare, ItemStatus.Archived)]
    [InlineData(ItemStatus.Archived, ItemRarity.Rare, ItemRarity.Legendary, ItemStatus.Archived)]
    [InlineData(ItemStatus.Archived, ItemRarity.VeryRare, ItemRarity.Common, ItemStatus.Archived)]
    [InlineData(ItemStatus.Archived, ItemRarity.VeryRare, ItemRarity.Uncommon, ItemStatus.Archived)]
    [InlineData(ItemStatus.Archived, ItemRarity.VeryRare, ItemRarity.Rare, ItemStatus.Archived)]
    [InlineData(ItemStatus.Archived, ItemRarity.VeryRare, ItemRarity.VeryRare, ItemStatus.Archived)]
    [InlineData(ItemStatus.Archived, ItemRarity.VeryRare, ItemRarity.Legendary, ItemStatus.Archived)]
    [InlineData(ItemStatus.Archived, ItemRarity.Legendary, ItemRarity.Common, ItemStatus.Archived)]
    [InlineData(ItemStatus.Archived, ItemRarity.Legendary, ItemRarity.Uncommon, ItemStatus.Archived)]
    [InlineData(ItemStatus.Archived, ItemRarity.Legendary, ItemRarity.Rare, ItemStatus.Archived)]
    [InlineData(ItemStatus.Archived, ItemRarity.Legendary, ItemRarity.VeryRare, ItemStatus.Archived)]
    [InlineData(ItemStatus.Archived, ItemRarity.Legendary, ItemRarity.Legendary, ItemStatus.Archived)]
    public void StatusAfterEdit_NeverPublishesAndSendsBackOnlyAnItemRaisedIntoReview(
        ItemStatus current, ItemRarity oldRarity, ItemRarity newRarity, ItemStatus expected)
    {
        ShopItemReview.StatusAfterEdit(current, oldRarity, newRarity).Should().Be(expected);
    }
}
