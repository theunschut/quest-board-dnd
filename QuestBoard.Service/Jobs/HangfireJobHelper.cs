using QuestBoard.Service.Services;
using Microsoft.Extensions.DependencyInjection;

namespace QuestBoard.Service.Jobs;

/// <summary>
/// Formalizes the scope-create + group-context + resolve-services sequence that every
/// Hangfire job repeats, since scoped services cannot be constructor-injected into jobs.
/// </summary>
internal static class HangfireJobHelper
{
    /// <summary>
    /// Creates a fresh DI scope for a Hangfire job execution, optionally sets the active
    /// group context, then invokes <paramref name="action"/> with the scoped provider.
    /// When <paramref name="groupId"/> is null, the group context is left untouched so a
    /// job can intentionally run a cross-group sweep.
    /// </summary>
    internal static async Task RunInScopeAsync(
        IServiceScopeFactory scopeFactory,
        int? groupId,
        Func<IServiceProvider, Task> action)
    {
        await using var scope = scopeFactory.CreateAsyncScope();

        if (groupId is not null)
        {
            // Inject concrete type to call SetGroupId before any repository call
            var groupContext = scope.ServiceProvider.GetRequiredService<ActiveGroupContextService>();
            groupContext.SetGroupId(groupId);
        }

        await action(scope.ServiceProvider);
    }
}
