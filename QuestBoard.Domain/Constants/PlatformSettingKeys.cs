namespace QuestBoard.Domain.Constants;

/// <summary>
/// The persisted <c>Key</c> values used with <c>PlatformSetting</c>. Every store/lookup of a
/// platform setting must use these constants rather than a string literal.
/// </summary>
public static class PlatformSettingKeys
{
    public const string OmphalosUrl = "Omphalos.Url";
    public const string OmphalosSharedSecret = "Omphalos.SharedSecret";
    public const string OmphalosEnabled = "Omphalos.Enabled";
}
