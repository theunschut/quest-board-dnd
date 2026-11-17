namespace QuestBoard.Domain.Models.Shop;

/// <summary>
/// The one definition of how long after a purchase it can be returned for its full price. The shop service decides
/// a posted return or sell-back with it and the history list decides whether to offer a Return with it, so the two
/// cannot drift apart.
/// </summary>
public static class ShopReturnPolicy
{
    /// <summary>How long after a purchase it can be returned for its full price. After this it can only be sold back for half.</summary>
    public static readonly TimeSpan Window = TimeSpan.FromHours(24);

    /// <summary>
    /// True while a purchase made at <paramref name="purchasedAtUtc"/> can still be returned for its full price at
    /// <paramref name="nowUtc"/>. Both are real UTC instants, not board calendar days.
    /// </summary>
    public static bool IsWithinReturnWindow(DateTime purchasedAtUtc, DateTime nowUtc) => nowUtc - purchasedAtUtc <= Window;
}
