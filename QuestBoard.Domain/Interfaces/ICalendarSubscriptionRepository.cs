using QuestBoard.Domain.Models;

namespace QuestBoard.Domain.Interfaces;

public interface ICalendarSubscriptionRepository : IBaseRepository<CalendarSubscription>
{
    /// <summary>
    /// Looks up a subscription by its address. A revoked row is returned, not filtered out --
    /// the caller needs to tell a revoked address apart from one that never existed.
    /// </summary>
    Task<CalendarSubscription?> GetByTokenAsync(string feedToken, CancellationToken token = default);

    /// <summary>
    /// Creates a new live subscription row for the given user with the given name and address,
    /// and returns it with its generated Id populated.
    /// </summary>
    Task<CalendarSubscription> MintAsync(int userId, string name, string feedToken, CancellationToken token = default);

    /// <summary>
    /// Returns every live (not revoked) subscription for the given user, ordered by creation
    /// time.
    /// </summary>
    Task<IList<CalendarSubscription>> GetForUserAsync(int userId, CancellationToken token = default);

    /// <summary>
    /// Renames the subscription matching both <paramref name="id"/> and <paramref name="userId"/>
    /// together, returning whether a row was found. The caller must supply
    /// <paramref name="userId"/> from the authenticated principal and never from request input,
    /// so a caller can never rename another member's subscription.
    /// </summary>
    Task<bool> RenameAsync(int id, int userId, string name, CancellationToken token = default);

    /// <summary>
    /// Retires the subscription matching both <paramref name="id"/> and <paramref name="userId"/>
    /// together by writing <c>RevokedAt</c> rather than removing the row, returning whether a
    /// row was found. The caller must supply <paramref name="userId"/> from the authenticated
    /// principal and never from request input.
    /// </summary>
    Task<bool> RevokeAsync(int id, int userId, DateTime revokedAt, CancellationToken token = default);

    /// <summary>
    /// Records the given fetch time on the subscription. A throttle on how often this actually
    /// writes is added by a later plan; <paramref name="minimumInterval"/> is accepted now so
    /// callers do not need to change when it does.
    /// </summary>
    Task TouchLastFetchedAsync(int id, DateTime fetchedAt, TimeSpan minimumInterval, CancellationToken token = default);

    /// <summary>
    /// Removes every subscription retired strictly before <paramref name="cutoff"/>, returning
    /// the number removed. This is the only method on this repository permitted to remove a
    /// row: a member pressing Delete retires the row so its address can keep answering "gone",
    /// and removing it there would make a just-deleted address indistinguishable from one that
    /// never existed. The purge scans every owner and every board -- this table carries no
    /// query filter, so the caller needs no group scope.
    /// </summary>
    Task<int> PurgeRetiredBeforeAsync(DateTime cutoff, CancellationToken cancellationToken = default);
}
