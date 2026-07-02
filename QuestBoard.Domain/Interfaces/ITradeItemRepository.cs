using QuestBoard.Domain.Models.Shop;

namespace QuestBoard.Domain.Interfaces;

public interface ITradeItemRepository : IBaseRepository<TradeItem>
{
    /// <summary>
    /// Returns all trade items currently available for trade, newest listing first.
    /// </summary>
    Task<IList<TradeItem>> GetAvailableTradeItemsAsync(CancellationToken token = default);

    /// <summary>
    /// Returns all trade items offered by the given player, newest listing first.
    /// </summary>
    Task<IList<TradeItem>> GetTradeItemsByPlayerAsync(int playerId, CancellationToken token = default);

    /// <summary>
    /// Returns all trade items with the given status (int-encoded), newest listing first.
    /// </summary>
    Task<IList<TradeItem>> GetTradeItemsByStatusAsync(int status, CancellationToken token = default);

    /// <summary>
    /// Returns a single trade item with the offering player's details loaded.
    /// </summary>
    Task<TradeItem?> GetTradeItemWithDetailsAsync(int id, CancellationToken token = default);
}
