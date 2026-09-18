using QuestBoard.Domain.Models;

namespace QuestBoard.Domain.Interfaces;

public interface ICalendarSubscriptionService
{
    /// <summary>
    /// Mints a new subscription for the given user with a fresh, cryptographically random
    /// address and a default name. The caller must supply <paramref name="userId"/> from the
    /// authenticated principal and never from request input.
    /// </summary>
    Task<CalendarSubscription> MintForUserAsync(int userId, CancellationToken token = default);

    /// <summary>
    /// Returns every live subscription the given user holds. The caller must supply
    /// <paramref name="userId"/> from the authenticated principal and never from request input.
    /// </summary>
    Task<IList<CalendarSubscription>> GetForUserAsync(int userId, CancellationToken token = default);

    /// <summary>
    /// Renames a subscription owned by the given user. The caller must supply
    /// <paramref name="userId"/> from the authenticated principal and never from request input,
    /// so a caller can never rename another member's subscription.
    /// </summary>
    Task<bool> RenameAsync(int subscriptionId, int userId, string name, CancellationToken token = default);

    /// <summary>
    /// Retires a subscription owned by the given user. The caller must supply
    /// <paramref name="userId"/> from the authenticated principal and never from request input,
    /// so a caller can never revoke another member's subscription.
    /// </summary>
    Task<bool> RevokeAsync(int subscriptionId, int userId, CancellationToken token = default);

    /// <summary>
    /// Resolves a subscription address to its feed body. Membership is read fresh from the
    /// database on every call and never taken from session or claims, because this endpoint
    /// has no session and no active board.
    /// </summary>
    Task<CalendarFeedResult> GetFeedAsync(string feedToken, CancellationToken token = default);
}
