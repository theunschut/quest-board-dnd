using QuestBoard.Domain.Enums;
using QuestBoard.Service.Helpers;

namespace QuestBoard.UnitTests.Helpers;

public class CrossBoardLinkRegistryTests
{
    [Fact]
    public void TryGetLookupKind_QuestDetailsRoute_ResolvesToQuestKind()
    {
        var found = CrossBoardLinkRegistry.TryGetLookupKind("Quest", "Details", out var kind);

        found.Should().BeTrue();
        kind.Should().Be(CrossBoardLookupKind.Quest);
    }

    [Theory]
    [InlineData("quest", "details")]
    [InlineData("QUEST", "DETAILS")]
    [InlineData("Quest", "details")]
    [InlineData("quest", "Details")]
    public void TryGetLookupKind_IsCaseInsensitiveInBothSegments(string controller, string action)
    {
        var found = CrossBoardLinkRegistry.TryGetLookupKind(controller, action, out var kind);

        found.Should().BeTrue();
        kind.Should().Be(CrossBoardLookupKind.Quest);
    }

    [Fact]
    public void TryGetLookupKind_UnregisteredControllerActionPair_ReturnsFalse()
    {
        var found = CrossBoardLinkRegistry.TryGetLookupKind("Quest", "Manage", out _);

        found.Should().BeFalse();
    }

    [Theory]
    [InlineData(null, "Details")]
    [InlineData("Quest", null)]
    [InlineData(null, null)]
    public void TryGetLookupKind_NullSegment_ReturnsFalse(string? controller, string? action)
    {
        var found = CrossBoardLinkRegistry.TryGetLookupKind(controller, action, out _);

        found.Should().BeFalse();
    }

    [Fact]
    public void RegisteredRoutes_HasExactlyOneEntry()
    {
        CrossBoardLinkRegistry.RegisteredRoutes.Should().HaveCount(1);
    }
}
