using QuestBoard.Domain.Enums;

namespace QuestBoard.Domain.Models.Shop;

/// <summary>
/// The one definition of which shop items need another Dungeon Master's review before they go on sale, the status a new
/// item starts in, and the status an edit leaves an item in. Creating and editing an item both ask it, so the two can
/// never disagree about which rarities need review.
/// </summary>
/// <remarks>
/// Only Common and Uncommon content goes on sale without review. Anything rarer goes on sale only when a Dungeon Master
/// other than its creator publishes it; a Dungeon Master never approves their own item.
/// </remarks>
public static class ShopItemReview
{
    /// <summary>
    /// True when content at <paramref name="rarity"/> must be reviewed by another Dungeon Master before it goes on sale.
    /// Every rarity except Common and Uncommon needs review, so a rarity added later needs review until decided otherwise.
    /// </summary>
    public static bool NeedsReview(ItemRarity rarity) =>
        rarity is not (ItemRarity.Common or ItemRarity.Uncommon);

    /// <summary>
    /// The status a newly created item starts in: on sale at once when its rarity needs no review, otherwise a draft
    /// waiting for another Dungeon Master to publish it.
    /// </summary>
    public static ItemStatus StatusOnCreate(ItemRarity rarity) =>
        NeedsReview(rarity) ? ItemStatus.Draft : ItemStatus.Published;

    /// <summary>
    /// The status an item is left in after an edit that changes its rarity from <paramref name="oldRarity"/>, the rarity
    /// stored before the edit, to <paramref name="newRarity"/>.
    /// </summary>
    /// <remarks>
    /// An edit never publishes. A draft stays a draft, and editing a denied item resubmits it as a draft. A published or
    /// archived item goes back to review only when the edit raises it from a rarity that needs no review into one that
    /// does: nobody has reviewed it at that rarity, and an archived item left as it is would go back on sale through
    /// Reopen. An item that was already at a rarity that needs review was published by another Dungeon Master at that
    /// level, so an edit that keeps or lowers it there, a restock or a typo fix, leaves it on sale or archived. The
    /// price of that choice is that the content of a reviewed item can change while it stays on sale; that is accepted
    /// so that routine upkeep does not take items off the shelf.
    /// </remarks>
    public static ItemStatus StatusAfterEdit(ItemStatus current, ItemRarity oldRarity, ItemRarity newRarity) => current switch
    {
        ItemStatus.Published or ItemStatus.Archived when RaisedIntoReview(oldRarity, newRarity) => ItemStatus.Draft,
        ItemStatus.Published or ItemStatus.Archived => current,
        _ => ItemStatus.Draft
    };

    private static bool RaisedIntoReview(ItemRarity oldRarity, ItemRarity newRarity) =>
        !NeedsReview(oldRarity) && NeedsReview(newRarity);
}
