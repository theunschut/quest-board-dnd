using NSubstitute;
using QuestBoard.Domain.Extensions;
using QuestBoard.Domain.Interfaces;

namespace QuestBoard.UnitTests.Extensions;

public class ActiveGroupContextExtensionsTests
{
    [Fact]
    public void RequireActiveGroupId_WithActiveGroupId_ReturnsValue()
    {
        // Arrange
        var context = Substitute.For<IActiveGroupContext>();
        context.ActiveGroupId.Returns(1);

        // Act
        var result = context.RequireActiveGroupId();

        // Assert
        result.Should().Be(1);
    }

    [Fact]
    public void RequireActiveGroupId_WithNullActiveGroupId_Throws()
    {
        // Arrange
        var context = Substitute.For<IActiveGroupContext>();
        context.ActiveGroupId.Returns((int?)null);

        // Act
        var act = () => context.RequireActiveGroupId();

        // Assert
        act.Should().Throw<InvalidOperationException>();
    }
}
