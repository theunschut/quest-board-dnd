using AutoMapper;
using QuestBoard.Domain.Interfaces;
using QuestBoard.Domain.Models.Shop;
using QuestBoard.Repository.Entities;
using Microsoft.EntityFrameworkCore;

namespace QuestBoard.Repository;

internal class UserTransactionRepository(QuestBoardContext dbContext, IMapper mapper) : BaseRepository<UserTransaction, UserTransactionEntity>(dbContext, mapper), IUserTransactionRepository
{
    /// <inheritdoc/>
    public override async Task<IList<UserTransaction>> GetAllAsync(CancellationToken token = default)
    {
        var entities = await DbContext.UserTransactions
            .Include(t => t.User)
            .Include(t => t.ShopItem)
            .OrderByDescending(t => t.TransactionDate)
            .ToListAsync(cancellationToken: token);
        return Mapper.Map<IList<UserTransaction>>(entities);
    }

    /// <inheritdoc/>
    public async Task<IList<UserTransaction>> GetTransactionsByUserAsync(int userId, CancellationToken token = default)
    {
        var entities = await DbContext.UserTransactions
            .Include(t => t.User)
            .Include(t => t.ShopItem)
            .Where(t => t.UserId == userId)
            .OrderByDescending(t => t.TransactionDate)
            .ToListAsync(cancellationToken: token);
        return Mapper.Map<IList<UserTransaction>>(entities);
    }

    /// <inheritdoc/>
    public async Task<IList<UserTransaction>> GetTransactionsByItemAsync(int itemId, CancellationToken token = default)
    {
        var entities = await DbContext.UserTransactions
            .Include(t => t.User)
            .Include(t => t.ShopItem)
            .Where(t => t.ShopItemId == itemId)
            .OrderByDescending(t => t.TransactionDate)
            .ToListAsync(cancellationToken: token);
        return Mapper.Map<IList<UserTransaction>>(entities);
    }

    /// <inheritdoc/>
    public async Task<IList<UserTransaction>> GetTransactionsByTypeAsync(int type, CancellationToken token = default)
    {
        var entities = await DbContext.UserTransactions
            .Include(t => t.User)
            .Include(t => t.ShopItem)
            .Where(t => t.TransactionType == type)
            .OrderByDescending(t => t.TransactionDate)
            .ToListAsync(cancellationToken: token);
        return Mapper.Map<IList<UserTransaction>>(entities);
    }

    /// <inheritdoc/>
    public async Task<UserTransaction?> GetTransactionWithDetailsAsync(int id, CancellationToken token = default)
    {
        var entity = await DbContext.UserTransactions
            .Include(t => t.User)
            .Include(t => t.ShopItem)
                .ThenInclude(si => si.CreatedByDm)
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken: token);
        return entity == null ? null : Mapper.Map<UserTransaction>(entity);
    }
}
