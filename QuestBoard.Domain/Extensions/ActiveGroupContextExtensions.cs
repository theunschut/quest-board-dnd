using QuestBoard.Domain.Interfaces;

namespace QuestBoard.Domain.Extensions;

public static class ActiveGroupContextExtensions
{
    /// <summary>
    /// Opt-in fail-fast accessor for request-scoped callers that require a concrete active group.
    /// Returns the active group ID, or throws if it is unexpectedly null.
    /// Do NOT use this on the SuperAdmin/see-all/seeding paths, where a null ActiveGroupId is
    /// intentional (e.g. the EF Core query filter, or Hangfire's cross-group sweep jobs) —
    /// calling it there would incorrectly turn valid "see all" behavior into an error.
    /// </summary>
    public static int RequireActiveGroupId(this IActiveGroupContext context) =>
        context.ActiveGroupId
            ?? throw new InvalidOperationException("Active group context is not initialized. This request requires a selected group.");
}
