using QuestBoard.Domain.Interfaces;

namespace QuestBoard.IntegrationTests.Helpers;

/// <summary>
/// Settable implementation of IActiveGroupContext for integration tests.
/// Defaults to GroupId = 1 (EuphoriaInn seed group). Tests override as needed.
/// Registered as Singleton in WebApplicationFactoryBase so test code can mutate it directly.
/// </summary>
public class MutableGroupContext : IActiveGroupContext
{
    public int? ActiveGroupId { get; set; } = 1;
}
