using System.Reflection;

namespace QuestBoard.Service.Helpers;

public static class AppVersion
{
    public static string Current { get; } = ExtractVersion(
        Assembly.GetEntryAssembly()
            ?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion);

    public static string ExtractVersion(string? informationalVersion)
    {
        // Strip the +<git-sha> metadata suffix the SDK appends automatically.
        return informationalVersion?.Split('+')[0] ?? "dev";
    }
}
