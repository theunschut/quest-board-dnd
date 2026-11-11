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
}
