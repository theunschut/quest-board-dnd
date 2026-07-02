using QuestBoard.Domain.Models.Shop;

namespace QuestBoard.Domain.Interfaces;

public interface IUserTransactionRepository : IBaseRepository<UserTransaction>
{
    /// <summary>
    /// Returns all transactions for the given user, most recent first.
    /// </summary>
    Task<IList<UserTransaction>> GetTransactionsByUserAsync(int userId, CancellationToken token = default);

    /// <summary>
    /// Returns all transactions for the given shop item, most recent first.
    /// </summary>
    Task<IList<UserTransaction>> GetTransactionsByItemAsync(int itemId, CancellationToken token = default);

    /// <summary>
    /// Returns all transactions of the given type (int-encoded TransactionType), most recent first.
    /// </summary>
    Task<IList<UserTransaction>> GetTransactionsByTypeAsync(int type, CancellationToken token = default);

    /// <summary>
    /// Returns a single transaction with user and shop item (including its creator) details loaded.
    /// </summary>
    Task<UserTransaction?> GetTransactionWithDetailsAsync(int id, CancellationToken token = default);
}
