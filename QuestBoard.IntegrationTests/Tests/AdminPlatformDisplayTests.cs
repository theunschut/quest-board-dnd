using QuestBoard.Domain.Enums;
using QuestBoard.Service.ViewModels.AdminViewModels;

namespace QuestBoard.IntegrationTests.Tests;

/// <summary>
/// Pins the one mapping the Admin and Platform pages take their role, board type, quest status and account state words,
/// badge fills and icons from, so the two layouts cannot spell a status differently.
/// </summary>
public class AdminPlatformDisplayTests
{
    [Theory]
    [InlineData(GroupRole.Admin, "Admin", "bg-danger", "fa-shield-alt")]
    [InlineData(GroupRole.DungeonMaster, "Dungeon Master", "bg-warning", "fa-crown")]
    [InlineData(GroupRole.Player, "Player", "bg-primary", "fa-dice-d20")]
    public void GroupRole_MapsToItsWordFillAndIcon(GroupRole role, string word, string fill, string icon)
    {
        AdminPlatformDisplay.GroupRoleBadge(role).Should().Be(new AdminPlatformDisplay.Badge(word, fill, icon));
    }

    [Theory]
    [InlineData(BoardType.OneShot, "One-Shot", "bg-primary", "fa-dice-d20")]
    [InlineData(BoardType.Campaign, "Campaign", "bg-info", "fa-book-open")]
    public void BoardType_MapsToItsWordFillAndIcon(BoardType type, string word, string fill, string icon)
    {
        AdminPlatformDisplay.BoardTypeBadge(type).Should().Be(new AdminPlatformDisplay.Badge(word, fill, icon));
    }

    [Theory]
    [InlineData(BoardType.Campaign, true, false, false, "Closed", "bg-dark", "fa-flag-checkered")]
    [InlineData(BoardType.Campaign, false, false, false, "Open", "bg-success", "fa-clock")]
    [InlineData(BoardType.Campaign, false, true, true, "Open", "bg-success", "fa-clock")]
    [InlineData(BoardType.OneShot, false, true, true, "Done", "bg-dark", "fa-flag-checkered")]
    [InlineData(BoardType.OneShot, false, true, false, "Finalized", "bg-primary", "fa-check-circle")]
    [InlineData(BoardType.OneShot, false, false, false, "Open", "bg-success", "fa-clock")]
    [InlineData(BoardType.OneShot, true, false, false, "Open", "bg-success", "fa-clock")]
    public void QuestStatus_FollowsTheBoardTypeAndTheQuestState(
        BoardType type, bool closed, bool finalized, bool played, string word, string fill, string icon)
    {
        AdminPlatformDisplay.QuestStatus(type, closed, finalized, played)
            .Should().Be(new AdminPlatformDisplay.Badge(word, fill, icon));
    }

    [Fact]
    public void AccountAndSetStates_KeepTheirExistingWordsAndCarryAnIcon()
    {
        AdminPlatformDisplay.AccountConfirmation(true).Should().Be(new AdminPlatformDisplay.Badge("Confirmed", "bg-success", "fa-check-circle"));
        AdminPlatformDisplay.AccountConfirmation(false).Should().Be(new AdminPlatformDisplay.Badge("Unconfirmed", "bg-secondary", "fa-question-circle"));
        AdminPlatformDisplay.AccountState(false).Should().Be(new AdminPlatformDisplay.Badge("Active", "bg-success", "fa-check-circle"));
        AdminPlatformDisplay.AccountState(true).Should().Be(new AdminPlatformDisplay.Badge("Disabled", "bg-danger", "fa-user-lock"));
        AdminPlatformDisplay.LinkedSetState(true).Should().Be(new AdminPlatformDisplay.Badge("Active", "bg-success", "fa-check-circle"));
        AdminPlatformDisplay.LinkedSetState(false).Should().Be(new AdminPlatformDisplay.Badge("Inactive", "bg-secondary", "fa-pause-circle"));
    }

    [Fact]
    public void OptionLists_KeepTheEnumNumbersAsValuesAndShowTheMappedWords()
    {
        AdminPlatformDisplay.BoardTypeOptions().Select(o => (o.Value, o.Text))
            .Should().Equal(("0", "One-Shot"), ("1", "Campaign"));
        AdminPlatformDisplay.GroupRoleOptions().Select(o => (o.Value, o.Text))
            .Should().Equal(("0", "Player"), ("1", "Dungeon Master"), ("2", "Admin"));
    }

    [Fact]
    public void HomePageBadge_NamesThePageAfterTheHouseIcon()
    {
        AdminPlatformDisplay.BoardHomeWord("Calendar").Should().Be("Home: Calendar");
        AdminPlatformDisplay.BoardHomeBadgeClass.Should().Be("bg-secondary");
        AdminPlatformDisplay.BoardHomeIcon.Should().Be("fa-house");
    }
}
