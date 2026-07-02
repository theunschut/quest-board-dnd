using QuestBoard.Domain.Models.Shop;

namespace QuestBoard.Domain.Interfaces;

public interface IShopRepository : IBaseRepository<ShopItem>
{
    /// <summary>
    /// Returns all published items whose optional availability window includes the current time.
    /// </summary>
    Task<IList<ShopItem>> GetPublishedItemsAsync(CancellationToken token = default);

    /// <summary>
    /// Returns all shop items with the given status (int-encoded ItemStatus), newest first.
    /// </summary>
    Task<IList<ShopItem>> GetItemsByStatusAsync(int status, CancellationToken token = default);

    /// <summary>
    /// Returns published items of the given type (int-encoded ItemType) whose availability window includes the current time.
    /// </summary>
    Task<IList<ShopItem>> GetItemsByTypeAsync(int type, CancellationToken token = default);

    /// <summary>
    /// Returns a single shop item with its creator and transaction history loaded.
    /// </summary>
    Task<ShopItem?> GetItemWithDetailsAsync(int id, CancellationToken token = default);

    /// <summary>
    /// Returns all shop items submitted by the given DM, newest first.
    /// </summary>
    Task<IList<ShopItem>> GetItemsByDmAsync(int dmId, CancellationToken token = default);

    /// <summary>
    /// Returns a page of published, currently-available items filtered by type, rarities, and search text, in the requested sort order.
    /// </summary>
    Task<(IList<ShopItem> Items, int TotalCount)> GetPagedPublishedItemsAsync(
        int? type,
        IList<int>? rarityInts,
        string? sort,
        string? search,
        int page,
        int pageSize,
        CancellationToken token = default);
}
