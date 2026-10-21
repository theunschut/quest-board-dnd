using QuestBoard.Domain.Enums;
using QuestBoard.Domain.Interfaces;
using QuestBoard.Service.Helpers;

namespace QuestBoard.UnitTests.Services;

/// <summary>
/// Pins the route table that redirects and the board's front door are built from, and the exact
/// toast sentences shown when a visitor is turned away from a switched-off page.
/// </summary>
public class BoardPageRoutesTests
{
    private static BoardPageSet AllHomeCandidatesOff()
    {
        var set = BoardPageSet.Empty;
        foreach (var page in new[]
                 {
                     BoardPage.Quests, BoardPage.Calendar, BoardPage.Events, BoardPage.QuestLog,
                     BoardPage.Characters, BoardPage.Contacts, BoardPage.Players, BoardPage.Shop
                 })
        {
            set = set.With(page);
        }

        return set;
    }

    [Theory]
    [InlineData(BoardPage.Quests, "Quest", "Index")]
    [InlineData(BoardPage.Calendar, "Calendar", "Index")]
    [InlineData(BoardPage.Events, "Events", "Index")]
    [InlineData(BoardPage.QuestLog, "QuestLog", "Index")]
    [InlineData(BoardPage.Characters, "Characters", "Index")]
    [InlineData(BoardPage.Contacts, "Contacts", "Index")]
    [InlineData(BoardPage.Players, "Players", "Index")]
    [InlineData(BoardPage.Shop, "Shop", "Index")]
    public void RouteFor_HomeCandidate_MapsToItsControllersIndex(BoardPage page, string controller, string action)
    {
        BoardPageRoutes.RouteFor(page).Should().Be((controller, action));
    }

    [Fact]
    public void RouteFor_DmProfile_Throws()
    {
        var act = () => BoardPageRoutes.RouteFor(BoardPage.DungeonMasterProfile);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void HomeRoute_WithoutAvailability_KeepsTheQuestList()
    {
        BoardPageRoutes.HomeRoute(null).Should().Be(("Quest", "Index"));
    }

    [Fact]
    public void HomeRoute_WhenNoCandidateIsAvailable_FallsBackToTheAccountPage()
    {
        var availability = new BoardPageAvailability(BoardType.OneShot, AllHomeCandidatesOff(), BoardPage.Quests);

        BoardPageRoutes.HomeRoute(availability).Should().Be(("Account", "Profile"));
    }

    [Fact]
    public void HomeRoute_StoredHomeIsCalendar_LeadsToTheCalendar()
    {
        var availability = new BoardPageAvailability(BoardType.OneShot, BoardPageSet.Empty, BoardPage.Calendar);

        BoardPageRoutes.HomeRoute(availability).Should().Be(("Calendar", "Index"));
    }

    [Fact]
    public void HomeRoute_StoredHomeSwitchedOff_LeadsToTheQuestList()
    {
        var availability = new BoardPageAvailability(
            BoardType.OneShot, BoardPageSet.Empty.With(BoardPage.Calendar), BoardPage.Calendar);

        BoardPageRoutes.HomeRoute(availability).Should().Be(("Quest", "Index"));
    }

    [Theory]
    [InlineData(BoardPage.Quests, "The quest list isn't used on this board.")]
    [InlineData(BoardPage.Calendar, "The calendar isn't used on this board.")]
    [InlineData(BoardPage.Events, "Events aren't used on this board.")]
    [InlineData(BoardPage.QuestLog, "The Quest Log isn't used on this board.")]
    [InlineData(BoardPage.Characters, "Characters aren't used on this board.")]
    [InlineData(BoardPage.Contacts, "Contacts aren't used on this board.")]
    [InlineData(BoardPage.Players, "The Players page isn't used on this board.")]
    [InlineData(BoardPage.Shop, "Shop isn't used on this board.")]
    [InlineData(BoardPage.DungeonMasterProfile, "DM profiles aren't used on this board.")]
    public void NotUsedMessage_ReturnsTheExactSentence(BoardPage page, string expected)
    {
        BoardPageRoutes.NotUsedMessage(page).Should().Be(expected);
    }
}
