using System.Runtime.CompilerServices;

namespace QuestBoard.IntegrationTests;

/// <summary>
/// Stops every in-process test host from holding inotify watchers.
/// </summary>
/// <remarks>
/// Each test web host watches its configuration files and content root for changes, which on Linux
/// costs one inotify instance per watcher. With many host instances alive in one test run the
/// per-user limit (128 on CI runners) runs out, and requests then fail with an IOException surfaced
/// as a misleading 404 from the exception handler. Tests never edit those files while running, so
/// configuration reload is switched off and the remaining watchers poll instead.
/// </remarks>
internal static class TestHostFileWatching
{
    [ModuleInitializer]
    internal static void DisableInotifyWatchers()
    {
        Environment.SetEnvironmentVariable("DOTNET_hostBuilder__reloadConfigOnChange", "false");
        Environment.SetEnvironmentVariable("DOTNET_USE_POLLING_FILE_WATCHER", "true");
    }
}
