using QuestBoard.Domain.Enums;

namespace QuestBoard.Domain.Models.Shop;

/// <summary>
/// The one definition of whether a shop item is on sale. The shop list, a purchase and a sale to the shop all ask it,
/// so the window means the same thing on every path.
/// </summary>
/// <remarks>
/// An item's optional window holds the board-local wall-clock times a Dungeon Master typed, with no zone attached. The
/// moment it is judged against must therefore be the board's own local time from the board clock, never a UTC instant
/// or the host's clock, which would open and close items an hour or two off. Both ends of the window count as inside,
/// as the shop list has always filtered.
/// </remarks>
public static class ShopItemAvailability
{
    /// <summary>
    /// True when the item's sale has started at <paramref name="boardNow"/>: it has no start, or its start is at or
    /// before that board-local time. Whatever the item's status, one that has not opened has never been on sale.
    /// </summary>
    public static bool HasOpened(ShopItem item, DateTime boardNow) =>
        item.AvailableFrom == null || item.AvailableFrom <= boardNow;

    /// <summary>
    /// True when a member of the board may open the item's page at <paramref name="boardNow"/>: it is Published or
    /// Archived and its sale has started. Archived items stay open because they were on sale once, and an item whose
    /// sale has ended stays open for the same reason. A Draft, a Denied item and one whose sale has not started are
    /// shown only to the board's Dungeon Masters and Admins.
    /// </summary>
    public static bool IsVisibleToMembers(ShopItem item, DateTime boardNow) =>
        (item.Status == ItemStatus.Published || item.Status == ItemStatus.Archived)
        && HasOpened(item, boardNow);

    /// <summary>
    /// True when the item can be bought or sold to the shop at <paramref name="boardNow"/>, the board's local time:
    /// it is Published, its sale has started, and it has no end or its end is at or after that time.
    /// </summary>
    public static bool IsOnSale(ShopItem item, DateTime boardNow) =>
        item.Status == ItemStatus.Published
        && HasOpened(item, boardNow)
        && (item.AvailableUntil == null || item.AvailableUntil >= boardNow);
}
