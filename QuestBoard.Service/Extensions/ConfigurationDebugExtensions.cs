namespace QuestBoard.Service.Extensions;

internal static class ConfigurationDebugExtensions
{
    private static readonly string[] SensitiveKeyParts =
        ["password", "secret", "key", "token", "connectionstring"];

    internal static void DumpConfiguration(this IConfiguration config)
    {
        var entries = config.AsEnumerable()
            .Where(kvp => kvp.Value is not null)
            .OrderBy(kvp => kvp.Key)
            .ToList();

        Console.WriteLine($"\n=== Configuration ({entries.Count} entries) ===");
        foreach (var (key, value) in entries)
        {
            var display = SensitiveKeyParts.Any(s => key.Contains(s, StringComparison.OrdinalIgnoreCase))
                ? (string.IsNullOrEmpty(value) ? "*** (empty)" : "*** (set)")
                : value;
            Console.WriteLine($"  {key} = {display}");
        }
        Console.WriteLine("=== End Configuration ===\n");
    }
}
