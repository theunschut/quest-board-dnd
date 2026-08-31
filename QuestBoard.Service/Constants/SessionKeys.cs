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
    /// other key here. The stored "none" sentinel below means the viewer has deselected every
    /// board. An absent key means "show all of my boards".
    /// </summary>
    public const string AgendaBoardFilter = "AgendaBoardFilter";

    /// <summary>
    /// Querystring sentinel meaning "forget my remembered selection and show every board
    /// again". Declared here rather than repeated as a literal because the controller compares
    /// it and both agenda views produce it, and a typo in any one of those places fails
    /// silently rather than loudly: an unrecognised value parses to an empty board list and
    /// renders a plausible "all boards filtered out" page instead of an error.
    /// </summary>
    public const string AgendaBoardFilterResetSentinel = "all";

    /// <summary>
    /// Stored value meaning the viewer deselected every board. Distinct from an absent key,
    /// which means "show all of my boards" -- without this marker those two states would be
    /// indistinguishable in session.
    /// </summary>
    public const string AgendaBoardFilterNoneSentinel = "none";

    /// <summary>
    /// Per-group session key for the Contacts "Show Hidden" toggle. Scoped by groupId so a user
    /// can have it on for one group and off for another within the same session.
    /// </summary>
    public static string ShowHiddenContactsKey(int groupId) => $"ShowHiddenContacts_{groupId}";
}
