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
    /// The agenda's own remembered board filter -- a comma-separated list of board ids
    /// narrowing which of the viewer's boards the cross-board agenda shows. This is the
    /// first key in this file holding more than one value, and it deliberately stays a
    /// plain comma-separated string rather than introducing a serializer, matching every
    /// other key here. The literal "none" means the viewer has deselected every board.
    /// An absent key means "show all of my boards".
    /// </summary>
    public const string AgendaBoardFilter = "AgendaBoardFilter";

    /// <summary>
    /// Per-group session key for the Contacts "Show Hidden" toggle. Scoped by groupId so a user
    /// can have it on for one group and off for another within the same session.
    /// </summary>
    public static string ShowHiddenContactsKey(int groupId) => $"ShowHiddenContacts_{groupId}";
}
