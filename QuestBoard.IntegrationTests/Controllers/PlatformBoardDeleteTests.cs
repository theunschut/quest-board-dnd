using System.Net;
using QuestBoard.Domain.Enums;
using QuestBoard.IntegrationTests.Helpers;

namespace QuestBoard.IntegrationTests.Controllers;

/// <summary>
/// End-to-end tests for deleting a board from the Platform area: the typed name is checked on the
/// server, a board with members is refused, and a delete removes the board with its quest content.
/// </summary>
public class PlatformBoardDeleteTests : IClassFixture<WebApplicationFactoryBase>
{
    private const int BoardId = 61;
    private const string BoardName = "Doomed Quest Table";

    private readonly WebApplicationFactoryBase _factory;

    public PlatformBoardDeleteTests(WebApplicationFactoryBase factory)
    {
        _factory = factory;
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static FormUrlEncodedContent Form(params (string Key, string Value)[] fields)
        => new(fields.ToDictionary(f => f.Key, f => f.Value));

    private static async Task<HttpResponseMessage> PostDeleteAsync(HttpClient client, params (string Key, string Value)[] fields)
        => await client.PostAsync($"/platform/Group/Delete/{BoardId}", Form(fields), Token);

    private sealed record SeededBoard(int QuestId, int SignupId, int ProposedDateId, int ReminderLogId, int PlayerId);

    private async Task<SeededBoard> SeedBoardWithQuestContentAsync()
    {
        await BoardMoveSeed.SeedBoardAsync(_factory, BoardId, BoardName, BoardType.OneShot);
        var dungeonMaster = await BoardMoveSeed.CreateUserAsync(_factory, "deleteDm", "Delete Dm");
        var player = await BoardMoveSeed.CreateUserAsync(_factory, "deletePlayer", "Delete Player");

        var questId = await BoardMoveSeed.SeedQuestAsync(
            _factory, BoardId, "Doomed Quest", dungeonMaster.Id, [new DateTime(2030, 1, 5, 18, 0, 0)]);
        var proposedDateId = (await BoardMoveSeed.ProposedDateIdsAsync(_factory, questId)).Single();
        var signupId = await BoardMoveSeed.SeedSignupAsync(
            _factory, questId, player.Id, SignupRole.Player, isSelected: false, characterId: null,
            new Dictionary<int, VoteType> { [proposedDateId] = VoteType.Yes }, DateTime.UtcNow);

        int reminderLogId;
        await using (var context = _factory.Database.CreateContext())
        {
            var log = new ReminderLogEntity { QuestId = questId, PlayerId = player.Id, SentAt = DateTime.UtcNow };
            context.ReminderLogs.Add(log);
            await context.SaveChangesAsync(Token);
            reminderLogId = log.Id;
        }

        return new SeededBoard(questId, signupId, proposedDateId, reminderLogId, player.Id);
    }

    private async Task<(bool Board, bool Quest, bool ProposedDate, bool Signup, bool Vote, bool ReminderLog)> ReadPresenceAsync(SeededBoard seeded)
    {
        await using var context = _factory.Database.CreateContext();
        return (
            await context.Groups.AnyAsync(g => g.Id == BoardId, Token),
            await context.Quests.IgnoreQueryFilters().AnyAsync(q => q.Id == seeded.QuestId, Token),
            await context.Set<ProposedDateEntity>().IgnoreQueryFilters().AnyAsync(pd => pd.Id == seeded.ProposedDateId, Token),
            await context.PlayerSignups.IgnoreQueryFilters().AnyAsync(ps => ps.Id == seeded.SignupId, Token),
            await context.Set<PlayerDateVoteEntity>().IgnoreQueryFilters().AnyAsync(v => v.PlayerSignupId == seeded.SignupId, Token),
            await context.ReminderLogs.IgnoreQueryFilters().AnyAsync(r => r.Id == seeded.ReminderLogId, Token));
    }

    [Fact]
    public async Task Post_WithTheExactName_DeletesTheBoardAndItsQuestContent()
    {
        await TestDataHelper.ClearDatabaseAsync(_factory.Services);
        var (client, _) = await AuthenticationHelper.CreateAuthenticatedSuperAdminClientAsync(_factory);
        var seeded = await SeedBoardWithQuestContentAsync();

        var response = await PostDeleteAsync(client, ("confirmName", BoardName));

        response.StatusCode.Should().BeOneOf(HttpStatusCode.Redirect, HttpStatusCode.Found);
        response.Headers.Location!.ToString().Should().EndWith("/platform");
        var presence = await ReadPresenceAsync(seeded);
        presence.Should().Be((false, false, false, false, false, false));
    }

    [Fact]
    public async Task Post_WithANameInTheWrongCase_KeepsTheBoardAndItsContent()
    {
        await TestDataHelper.ClearDatabaseAsync(_factory.Services);
        var (client, _) = await AuthenticationHelper.CreateAuthenticatedSuperAdminClientAsync(_factory);
        var seeded = await SeedBoardWithQuestContentAsync();

        var response = await PostDeleteAsync(client, ("confirmName", BoardName.ToLowerInvariant()));

        response.StatusCode.Should().BeOneOf(HttpStatusCode.Redirect, HttpStatusCode.Found);
        response.Headers.Location!.ToString().Should().EndWith($"/platform/Group/Delete/{BoardId}");
        var presence = await ReadPresenceAsync(seeded);
        presence.Should().Be((true, true, true, true, true, true));
    }

    [Fact]
    public async Task Post_WithoutAName_KeepsTheBoard()
    {
        await TestDataHelper.ClearDatabaseAsync(_factory.Services);
        var (client, _) = await AuthenticationHelper.CreateAuthenticatedSuperAdminClientAsync(_factory);
        var seeded = await SeedBoardWithQuestContentAsync();

        var response = await PostDeleteAsync(client);

        response.StatusCode.Should().BeOneOf(HttpStatusCode.Redirect, HttpStatusCode.Found);
        response.Headers.Location!.ToString().Should().EndWith($"/platform/Group/Delete/{BoardId}");
        var presence = await ReadPresenceAsync(seeded);
        presence.Should().Be((true, true, true, true, true, true));
    }

    [Fact]
    public async Task Post_WhenTheBoardHasMembers_KeepsEverything()
    {
        await TestDataHelper.ClearDatabaseAsync(_factory.Services);
        var (client, _) = await AuthenticationHelper.CreateAuthenticatedSuperAdminClientAsync(_factory);
        var seeded = await SeedBoardWithQuestContentAsync();
        await BoardMoveSeed.SeedMembershipAsync(_factory, seeded.PlayerId, BoardId, GroupRole.Player);

        var response = await PostDeleteAsync(client, ("confirmName", BoardName));

        response.StatusCode.Should().BeOneOf(HttpStatusCode.Redirect, HttpStatusCode.Found);
        response.Headers.Location!.ToString().Should().EndWith("/platform");
        var presence = await ReadPresenceAsync(seeded);
        presence.Should().Be((true, true, true, true, true, true));
    }

    private const string PhoneUserAgent =
        "Mozilla/5.0 (iPhone; CPU iPhone OS 17_0 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.0 Mobile/15E148 Safari/604.1";

    private async Task<string> GetDeletePageAsync(HttpClient client, string? userAgent = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, $"/platform/Group/Delete/{BoardId}");
        if (userAgent != null)
        {
            request.Headers.TryAddWithoutValidation("User-Agent", userAgent);
        }

        var response = await client.SendAsync(request, Token);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return await response.Content.ReadAsStringAsync(Token);
    }

    private async Task<int> SeedShopItemWithOnePurchaseAsync(int dungeonMasterId, int buyerId)
    {
        await using var context = _factory.Database.CreateContext();
        var item = new ShopItemEntity
        {
            Name = "Healing Draught",
            Description = "Restores health",
            Type = 0,
            Rarity = 0,
            Price = 10m,
            Quantity = 3,
            Status = 0,
            CreatedByDmId = dungeonMasterId,
            GroupId = BoardId
        };
        context.Set<ShopItemEntity>().Add(item);
        await context.SaveChangesAsync(Token);

        context.Set<UserTransactionEntity>().Add(new UserTransactionEntity
        {
            UserId = buyerId,
            ShopItemId = item.Id,
            TransactionType = 0,
            Price = 10m,
            Quantity = 1,
            TransactionDate = DateTime.UtcNow
        });
        await context.SaveChangesAsync(Token);
        return item.Id;
    }

    [Fact]
    public async Task Get_ShowsEachNonEmptyTypeWithItsCountAndCollapsedNames()
    {
        await TestDataHelper.ClearDatabaseAsync(_factory.Services);
        var (client, _) = await AuthenticationHelper.CreateAuthenticatedSuperAdminClientAsync(_factory);
        await BoardMoveSeed.SeedBoardAsync(_factory, BoardId, BoardName, BoardType.OneShot);
        var dungeonMaster = await BoardMoveSeed.CreateUserAsync(_factory, "previewDm", "Preview Dm");
        await BoardMoveSeed.SeedQuestAsync(_factory, BoardId, "Bravo Road", dungeonMaster.Id, [new DateTime(2030, 1, 5, 18, 0, 0)]);
        await BoardMoveSeed.SeedQuestAsync(_factory, BoardId, "alpha gate", dungeonMaster.Id, [new DateTime(2030, 1, 6, 18, 0, 0)]);
        await BoardMoveSeed.SeedCharacterAsync(_factory, dungeonMaster.Id, BoardId, "Thorin Stonefist");
        await SeedShopItemWithOnePurchaseAsync(dungeonMaster.Id, dungeonMaster.Id);

        var html = await GetDeletePageAsync(client);

        html.Should().Contain("2 quests");
        html.Should().Contain("Show quest titles");
        html.Should().Contain("1 character");
        html.Should().Contain("Show character names");
        html.Should().Contain("1 shop item");
        html.Should().Contain("1 purchase or return");
        html.IndexOf("alpha gate", StringComparison.Ordinal).Should().BeLessThan(html.IndexOf("Bravo Road", StringComparison.Ordinal));
        html.Should().Contain("<details class=\"board-delete-names\">");
        html.Should().NotContain("<details open");
        html.Should().NotContain("Show event titles");
        html.Should().NotContain("0 events");
    }

    [Fact]
    public async Task Get_ShowsRowsOnOtherBoardsSeparately()
    {
        await TestDataHelper.ClearDatabaseAsync(_factory.Services);
        var (client, _) = await AuthenticationHelper.CreateAuthenticatedSuperAdminClientAsync(_factory);
        await BoardMoveSeed.SeedBoardAsync(_factory, BoardId, BoardName, BoardType.OneShot);
        await BoardMoveSeed.SeedBoardAsync(_factory, 62, "Neighbour Table", BoardType.OneShot);
        var dungeonMaster = await BoardMoveSeed.CreateUserAsync(_factory, "unlinkDm", "Unlink Dm");
        var originalId = await BoardMoveSeed.SeedQuestAsync(
            _factory, BoardId, "Original Tale", dungeonMaster.Id, [new DateTime(2030, 1, 5, 18, 0, 0)]);
        await BoardMoveSeed.SeedQuestAsync(
            _factory, 62, "Sequel Tale", dungeonMaster.Id, [new DateTime(2030, 2, 5, 18, 0, 0)], originalQuestId: originalId);

        var html = WebUtility.HtmlDecode(await GetDeletePageAsync(client));

        html.Should().Contain("Kept, but unlinked");
        html.Should().Contain("1 follow-up quest on another board");
        html.Should().Contain("Sequel Tale (on Neighbour Table)");
    }

    [Fact]
    public async Task Get_HidesTheUnlinkedSectionWhenNothingIsUnlinked()
    {
        await TestDataHelper.ClearDatabaseAsync(_factory.Services);
        var (client, _) = await AuthenticationHelper.CreateAuthenticatedSuperAdminClientAsync(_factory);
        await SeedBoardWithQuestContentAsync();

        var html = await GetDeletePageAsync(client);

        html.Should().NotContain("Kept, but unlinked");
        html.Should().NotContain("fa-link-slash");
    }

    [Fact]
    public async Task Get_EmptyBoard_SaysNothingIsStored()
    {
        await TestDataHelper.ClearDatabaseAsync(_factory.Services);
        var (client, _) = await AuthenticationHelper.CreateAuthenticatedSuperAdminClientAsync(_factory);
        await BoardMoveSeed.SeedBoardAsync(_factory, BoardId, BoardName, BoardType.OneShot);

        var html = await GetDeletePageAsync(client);

        html.Should().Contain("Nothing is stored on this board. Only the board itself will be deleted.");
        html.Should().NotContain("board-delete-row--deleted");
        html.Should().Contain("name=\"confirmName\"");
    }

    [Fact]
    public async Task Get_AlwaysShowsTheNotAffectedRow()
    {
        await TestDataHelper.ClearDatabaseAsync(_factory.Services);
        var (client, _) = await AuthenticationHelper.CreateAuthenticatedSuperAdminClientAsync(_factory);
        await BoardMoveSeed.SeedBoardAsync(_factory, BoardId, BoardName, BoardType.OneShot);

        var html = await GetDeletePageAsync(client);

        html.Should().Contain("Not affected");
        html.Should().Contain("board-delete-row--kept");
        html.Should().Contain("User accounts, Dungeon Master profiles, trade items and calendar subscriptions are not touched.");
        html.Should().Contain("it leaves the set and the set stays.");
    }

    [Fact]
    public async Task Get_ShowsTheTypedNameField()
    {
        await TestDataHelper.ClearDatabaseAsync(_factory.Services);
        var (client, _) = await AuthenticationHelper.CreateAuthenticatedSuperAdminClientAsync(_factory);
        await BoardMoveSeed.SeedBoardAsync(_factory, BoardId, BoardName, BoardType.OneShot);

        var html = await GetDeletePageAsync(client);
        var decoded = WebUtility.HtmlDecode(html);

        html.Should().Contain("name=\"confirmName\"");
        html.Should().Contain($"data-board-delete-name=\"{BoardName}\"");
        html.Should().Contain("data-disable-on-submit");
        decoded.Should().Contain("Type the board name to confirm");
        decoded.Should().Contain($"Type \"{BoardName}\" exactly, including capitals and spaces. The delete button unlocks when it matches.");
        html.Should().NotContain("board-delete-refusal");
        html.Should().NotContain("aria-invalid");
    }

    [Fact]
    public async Task Get_EncodesUserAuthoredNames()
    {
        await TestDataHelper.ClearDatabaseAsync(_factory.Services);
        var (client, _) = await AuthenticationHelper.CreateAuthenticatedSuperAdminClientAsync(_factory);
        await BoardMoveSeed.SeedBoardAsync(_factory, BoardId, BoardName, BoardType.OneShot);
        var dungeonMaster = await BoardMoveSeed.CreateUserAsync(_factory, "encodeDm", "Encode Dm");
        await BoardMoveSeed.SeedQuestAsync(_factory, BoardId, "<b>x</b>", dungeonMaster.Id, [new DateTime(2030, 1, 5, 18, 0, 0)]);

        var html = await GetDeletePageAsync(client);

        html.Should().Contain("&lt;b&gt;x&lt;/b&gt;");
        html.Should().NotContain("<li><b>x</b></li>");
    }

    [Fact]
    public async Task Get_PhoneUserAgent_RendersTheSameBodyInThePhoneCard()
    {
        await TestDataHelper.ClearDatabaseAsync(_factory.Services);
        var (client, _) = await AuthenticationHelper.CreateAuthenticatedSuperAdminClientAsync(_factory);
        await SeedBoardWithQuestContentAsync();

        var html = await GetDeletePageAsync(client, PhoneUserAgent);

        html.Should().Contain("container-fluid px-2 mt-2");
        html.Should().Contain("<h1 class=\"mb-0\">");
        html.Should().Contain("1 quest");
        html.Should().Contain("Show quest titles");
        html.Should().Contain("Not affected");
        html.Should().Contain("name=\"confirmName\"");
        html.Should().NotContain("platform-group-card-mobile");
        html.Should().NotContain("platform-group.mobile.css");
    }
}
