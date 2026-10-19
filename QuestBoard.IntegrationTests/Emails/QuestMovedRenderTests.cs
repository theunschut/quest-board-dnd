using System.Net;
using QuestBoard.Domain.Enums;
using QuestBoard.Domain.Interfaces;
using QuestBoard.Service.Components.Emails;
using QuestBoard.IntegrationTests.Helpers;

namespace QuestBoard.IntegrationTests.Emails;

/// <summary>
/// Pins the Quest Moved email's three audiences, its wall-clock session date formatting, HTML
/// encoding of user text, and the optional moved line on the Waitlist Promoted email.
/// </summary>
public class QuestMovedRenderTests(WebApplicationFactoryBase factory) : IClassFixture<WebApplicationFactoryBase>
{
    private const string AppUrl = "https://example.com";
    private const string QuestUrl = "https://example.com/Quest/Details/7";

    private async Task<string> RenderMovedAsync(
        QuestMovedAudience audience,
        DateTime? sessionDate = null,
        string title = "The Tomb of Annihilation",
        string board = "The Silver Tankard")
    {
        using var scope = factory.Services.CreateScope();
        var renderService = scope.ServiceProvider.GetRequiredService<IEmailRenderService>();

        return await renderService.RenderAsync<QuestMoved>(new Dictionary<string, object?>
        {
            [nameof(QuestMoved.QuestTitle)] = title,
            [nameof(QuestMoved.DmName)] = "Dungeon Master Theomund",
            [nameof(QuestMoved.BoardName)] = board,
            [nameof(QuestMoved.SessionDate)] = sessionDate,
            [nameof(QuestMoved.QuestUrl)] = QuestUrl,
            [nameof(QuestMoved.AppUrl)] = AppUrl,
            [nameof(QuestMoved.Audience)] = audience,
        });
    }

    private async Task<string> RenderPromotedAsync(bool setBoardName, string? boardName)
    {
        using var scope = factory.Services.CreateScope();
        var renderService = scope.ServiceProvider.GetRequiredService<IEmailRenderService>();

        var parameters = new Dictionary<string, object?>
        {
            [nameof(WaitlistPromoted.QuestTitle)] = "The Tomb of Annihilation",
            [nameof(WaitlistPromoted.DmName)] = "Dungeon Master Theomund",
            [nameof(WaitlistPromoted.QuestDate)] = new DateTime(2031, 6, 14, 19, 0, 0),
            [nameof(WaitlistPromoted.QuestDescription)] = "A short description.",
            [nameof(WaitlistPromoted.PlayerName)] = "Arannis",
            [nameof(WaitlistPromoted.QuestUrl)] = QuestUrl,
            [nameof(WaitlistPromoted.ChallengeRating)] = 9,
            [nameof(WaitlistPromoted.AppUrl)] = AppUrl,
        };
        if (setBoardName)
            parameters[nameof(WaitlistPromoted.MovedToBoardName)] = boardName;

        return await renderService.RenderAsync<WaitlistPromoted>(parameters);
    }

    [Fact]
    public async Task Player_RendersHeadingNotesAndCta()
    {
        var html = await RenderMovedAsync(QuestMovedAudience.Player, new DateTime(2031, 6, 14, 19, 0, 0));

        html.Should().Contain("Quest Moved");
        html.Should().Contain("The Tomb of Annihilation");
        html.Should().Contain("has moved to The Silver Tankard.");
        html.Should().Contain("New Board:");
        html.Should().Contain("Dungeon Master:");
        html.Should().Contain("Session Date:");
        html.Should().Contain("Your signup and date votes came with it.");
        html.Should().Contain("View Quest Details");
        html.Should().Contain(QuestUrl);
    }

    [Fact]
    public async Task DungeonMaster_RendersStillDmNoteAndCta()
    {
        var html = await RenderMovedAsync(QuestMovedAudience.DungeonMaster, new DateTime(2031, 6, 14, 19, 0, 0));

        html.Should().Contain("Quest Moved");
        html.Should().Contain("You are still the Dungeon Master.");
        html.Should().Contain("View Quest Details");
        html.Should().Contain(QuestUrl);
    }

    [Fact]
    public async Task Removed_RendersRemovalCopyWithoutCta()
    {
        var html = await RenderMovedAsync(QuestMovedAudience.Removed);

        html.Should().Contain("Signup Removed");
        html.Should().Contain("You are not a member of that board, so your signup was removed.");
        html.Should().Contain("Moved To:");
        html.Should().Contain("ask your Dungeon Master or an admin to add you to The Silver Tankard");
        html.Should().NotContain("View Quest Details");
        html.Should().NotContain(QuestUrl);
    }

    [Fact]
    public async Task SessionDate_RendersAsWallClockDayAndMonth()
    {
        var date = new DateTime(2031, 6, 14, 19, 0, 0);

        var html = await RenderMovedAsync(QuestMovedAudience.Player, date);

        html.Should().Contain(date.ToString("dddd, MMMM d"));
        html.Should().NotContain("Still being decided");
    }

    [Fact]
    public async Task NullSessionDate_RendersStillBeingDecided()
    {
        var html = await RenderMovedAsync(QuestMovedAudience.Player, sessionDate: null);

        html.Should().Contain("Still being decided");
    }

    [Fact]
    public async Task TitleWithMarkup_IsHtmlEncoded()
    {
        var html = await RenderMovedAsync(QuestMovedAudience.Player, title: "<b>x</b>");

        html.Should().NotContain("<b>x</b>");
        html.Should().Contain("&lt;b&gt;x&lt;/b&gt;");
    }

    [Fact]
    public async Task WaitlistPromoted_WithMovedToBoardName_RendersMovedNote()
    {
        var html = await RenderPromotedAsync(setBoardName: true, boardName: "The Silver Tankard");

        html.Should().Contain("This quest has moved to The Silver Tankard.");
        html.Should().Contain("pick one from The Silver Tankard again on the quest page.");
    }

    [Fact]
    public async Task WaitlistPromoted_WithoutMovedToBoardName_RendersNoMovedNote_IdenticalToExplicitNull()
    {
        var unset = await RenderPromotedAsync(setBoardName: false, boardName: null);
        var explicitNull = await RenderPromotedAsync(setBoardName: true, boardName: null);

        unset.Should().NotContain("has moved to");
        unset.Should().Be(explicitNull);
    }

    [Theory]
    [InlineData("QuestMovedPlayer")]
    [InlineData("QuestMovedDungeonMaster")]
    [InlineData("QuestMovedRemoved")]
    [InlineData("WaitlistPromotedAfterMove")]
    public async Task EmailPreview_NewActions_ReturnOkForAdmin(string action)
    {
        factory.TestGroupContext.ActiveGroupId = 1;
        var (client, _) = await AuthenticationHelper.CreateAuthenticatedAdminClientAsync(
            factory, $"moved_preview_{action}".ToLowerInvariant(), $"moved_preview_{action}@example.com".ToLowerInvariant());

        var response = await client.GetAsync($"/EmailPreview/{action}", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
