# Deferred Items — Phase 85

Out-of-scope discoveries found during execution of plan 85-02, logged per the executor's
scope-boundary rule rather than fixed inline.

## 1. `CalendarSubscriptionStaticGuardTests.NoPlanningOrTrackingReference_ReachedTheSourceTree` fails on Linux dev environments

**Found during:** Plan 85-02, Task 1/3 verification (`dotnet test QuestBoard.IntegrationTests`).

**Not caused by this phase.** The test and its `ResolveRepoFile` helper predate Phase 85
(shipped in Phase 84, commit `78aa5286`). Neither the test file nor the helper was touched by
plan 85-02.

**Root cause:** `ResolveRepoFile` walks up from `AppContext.BaseDirectory` and returns the
first path segment for which `File.Exists(candidate) || Directory.Exists(candidate)` is true.
On Linux, `dotnet build` emits a native apphost executable literally named `QuestBoard.Service`
(no extension) directly inside `QuestBoard.IntegrationTests/bin/Debug/net10.0/`. That bare file
name collides with the project-folder name `QuestBoard.Service` the helper is actually looking
for, so the walk stops one level too early and returns a *file* path where the caller expects a
*directory* to enumerate — `Directory.EnumerateFiles` then throws `DirectoryNotFoundException`.
On Windows the apphost is named `QuestBoard.Service.exe`, so the bare-name collision never
occurs there — this is why the test was never seen to fail in the project's normal (Windows)
development environment.

**Also observed:** many unrelated integration tests (`HomeControllerIntegrationTests`,
`LayoutNavigationTests`, `CalendarHorizonBannerTests`, `QuestLogControllerIntegrationTests`,
etc.) intermittently fail under a full `dotnet test QuestBoard.IntegrationTests` /
`dotnet test` run in this sandbox with `System.IO.IOException: The configured user limit (128)
on the number of inotify instances has been reached`. This is an OS-level resource ceiling
(`/proc/sys/fs/inotify/max_user_instances = 128`) hit when many parallel
`WebApplicationFactory` hosts each register a `FileSystemWatcher` for static-file
cache-busting. Confirmed unrelated to phase 85: none of the 186 failures in the full-project
run touched `CalendarSubscriptionFeedTests` or `CalendarSubscriptionQuestFeedTests`, both of
which pass consistently when run directly or as part of the full `QuestBoard.UnitTests` /
targeted `QuestBoard.IntegrationTests --filter` runs this plan's own verification uses.

**Action taken:** none — logged per scope boundary. Not fixed, not worked around.

**Suggested follow-up (not part of this phase):** make `ResolveRepoFile` (or a shared test
helper) require `Directory.Exists` specifically when the caller wants a directory, or raise the
sandbox's `fs.inotify.max_user_instances` for Linux CI/dev runs.
