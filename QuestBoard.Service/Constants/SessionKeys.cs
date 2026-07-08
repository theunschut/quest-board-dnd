namespace QuestBoard.Service.Constants;

/// <summary>
/// Centralised session key constants. Reference this class everywhere a session key string is needed.
/// </summary>
public static class SessionKeys
{
    public const string ActiveGroupId = "ActiveGroupId";
    public const string ActiveGroupName = "ActiveGroupName";
    public const string ActiveGroupValidatedAtUtc = "ActiveGroupValidatedAtUtc";

    /// <summary>
    /// Per-group session key for the Contacts "Show Hidden" toggle. Scoped by groupId so a user
    /// can have it on for one group and off for another within the same session.
    /// </summary>
    public static string ShowHiddenContactsKey(int groupId) => $"ShowHiddenContacts_{groupId}";
}
