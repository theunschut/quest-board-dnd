namespace QuestBoard.Service.Services;

/// <summary>
/// Service keys under which the administrator-action limiters are registered, so a limiter that guards one action
/// is never mistaken for the one that guards another.
/// </summary>
public static class AdminRateLimitKeys
{
    /// <summary>The per-administrator limit on creating accounts.</summary>
    public const string CreateUser = "admin-create-user";
}
