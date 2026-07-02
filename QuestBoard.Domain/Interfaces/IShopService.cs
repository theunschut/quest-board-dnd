using QuestBoard.Domain.Enums;
using QuestBoard.Domain.Models;
using QuestBoard.Domain.Models.Shop;

namespace QuestBoard.Domain.Interfaces;

public interface IShopService : IBaseService<ShopItem>
{
    /// <summary>
    /// Returns all shop items regardless of status.
    /// </summary>
    Task<IList<ShopItem>> GetAllItemsAsync(CancellationToken token = default);

    /// <summary>
    /// Returns all shop items with Published status.
    /// </summary>
    Task<IList<ShopItem>> GetPublishedItemsAsync(CancellationToken token = default);

    /// <summary>
    /// Returns all shop items with the given status.
    /// </summary>
    Task<IList<ShopItem>> GetItemsByStatusAsync(ItemStatus status, CancellationToken token = default);

    /// <summary>
    /// Returns all shop items of the given type.
    /// </summary>
    Task<IList<ShopItem>> GetItemsByTypeAsync(ItemType type, CancellationToken token = default);

    /// <summary>
    /// Returns a single shop item with its full detail graph loaded.
    /// </summary>
    Task<ShopItem?> GetItemWithDetailsAsync(int id, CancellationToken token = default);

    /// <summary>
    /// Returns all shop items submitted by the given DM.
    /// </summary>
    Task<IList<ShopItem>> GetItemsByDmAsync(int dmId, CancellationToken token = default);

    /// <summary>
    /// Returns the suggested price for an item of the given rarity, per Tasha's Cauldron pricing guidelines.
    /// </summary>
    Task<decimal> CalculateItemPriceAsync(ItemRarity rarity, CancellationToken token = default);

    /// <summary>
    /// Marks a shop item as Published, making it purchasable.
    /// </summary>
    Task PublishItemAsync(int itemId, CancellationToken token = default);

    /// <summary>
    /// Purchases a quantity of a published item for the user, decrementing stock (unless unlimited) and recording a transaction.
    /// Throws InvalidOperationException if the item is unpublished, sold out, or has insufficient stock.
    /// </summary>
    Task<UserTransaction> PurchaseItemAsync(int itemId, int quantity, User user, CancellationToken token = default);

    /// <summary>
    /// Returns or sells back a previously purchased quantity, refunding full price within 24 hours or half price after.
    /// Throws InvalidOperationException if the original transaction is invalid or the quantity exceeds what remains unreturned.
    /// </summary>
    Task<UserTransaction> ReturnOrSellItemAsync(int transactionId, int quantity, User user, CancellationToken token = default);

    /// <summary>
    /// Sells a quantity of an item to the shop at half its listed price, without an originating purchase.
    /// </summary>
    Task<UserTransaction> SellItemToShopAsync(int itemId, int quantity, User user, CancellationToken token = default);

    /// <summary>
    /// Marks a shop item as Archived, removing it from active listings.
    /// </summary>
    Task ArchiveItemAsync(int itemId, CancellationToken token = default);

    /// <summary>
    /// Marks a shop item as Denied with the given reason, recording the denial timestamp.
    /// </summary>
    Task DenyItemAsync(int itemId, string denialReason, CancellationToken token = default);

    /// <summary>
    /// Returns all transactions for the given user.
    /// </summary>
    Task<IList<UserTransaction>> GetUserTransactionsAsync(int userId, CancellationToken token = default);

    /// <summary>
    /// Returns the user's purchase transactions paired with the quantity still eligible for return/sell-back.
    /// </summary>
    Task<IReadOnlyList<TransactionWithRemaining>> GetUserTransactionsWithRemainingAsync(int userId, CancellationToken token = default);

    /// <summary>
    /// Returns all transactions across all users.
    /// </summary>
    Task<IList<UserTransaction>> GetAllTransactionsAsync(CancellationToken token = default);

    /// <summary>
    /// Returns a page of published items filtered by type, rarities, and search text, in the requested sort order.
    /// </summary>
    Task<(IList<ShopItem> Items, int TotalCount)> GetPagedPublishedItemsAsync(
        ItemType? type,
        IList<ItemRarity>? rarities,
        string? sort,
        string? search,
        int page,
        int pageSize,
        CancellationToken token = default);
}