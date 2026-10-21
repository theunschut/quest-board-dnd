using FluentAssertions;
using QuestBoard.Domain.Enums;
using QuestBoard.Domain.Interfaces;
using QuestBoard.Service.Extensions;

namespace QuestBoard.UnitTests.Services;

// Pins the difference between Allows and Serves: Allows needs a resolved board, Serves lets an
// unresolved board through, and both follow the board's own switches and type ceiling.
public class BoardPageAvailabilityExtensionsTests
{
    public static TheoryData<BoardPage> AllPages()
    {
        var data = new TheoryData<BoardPage>();
        foreach (var page in Enum.GetValues<BoardPage>())
        {
            data.Add(page);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(AllPages))]
    public void Allows_WithNoResolvedBoard_IsFalse(BoardPage page)
    {
        BoardPageAvailability? availability = null;

        availability.Allows(page).Should().BeFalse();
    }

    [Theory]
    [MemberData(nameof(AllPages))]
    public void Serves_WithNoResolvedBoard_IsTrue(BoardPage page)
    {
        BoardPageAvailability? availability = null;

        availability.Serves(page).Should().BeTrue();
    }

    [Fact]
    public void SwitchedOffPage_IsNeitherAllowedNorServed_WhileOtherPagesAre()
    {
        var availability = new BoardPageAvailability(
            BoardType.OneShot, BoardPageSet.Empty.With(BoardPage.Shop), BoardPage.Quests);

        availability.Allows(BoardPage.Shop).Should().BeFalse();
        availability.Serves(BoardPage.Shop).Should().BeFalse();
        availability.Allows(BoardPage.Calendar).Should().BeTrue();
        availability.Serves(BoardPage.Calendar).Should().BeTrue();
    }

    [Theory]
    [InlineData(BoardPage.Players)]
    [InlineData(BoardPage.Shop)]
    [InlineData(BoardPage.DungeonMasterProfile)]
    public void CampaignBoardWithNothingStored_DoesNotOfferOneShotOnlyPages(BoardPage page)
    {
        var availability = new BoardPageAvailability(BoardType.Campaign, BoardPageSet.Empty, BoardPage.Quests);

        availability.Allows(page).Should().BeFalse();
        availability.Serves(page).Should().BeFalse();
    }
}
