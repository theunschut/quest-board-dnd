using System.Net;
using System.Text.RegularExpressions;
using QuestBoard.Domain.Enums;
using QuestBoard.IntegrationTests.Helpers;

namespace QuestBoard.IntegrationTests.Controllers;

/// <summary>
/// Deleting a quest from the Admin quest list. The delete is a posted form that answers with a redirect and a toast. It is
/// limited to the active board: a crafted request for a quest of another board answers an Error toast and removes nothing. Deleting a quest of the active board must also remove the
/// reminder rows that point at it and unlink a follow-up quest that lives on another board, or the delete throws on a
/// real database. The in-memory database does not enforce foreign keys, so those two facts assert the rows are gone,
/// not that the delete failed; the real-foreign-key proof is the SQL Server fact.
/// </summary>
public class AdminQuestDeleteTests : IClassFixture<WebApplicationFactoryBase>
{
    private readonly WebApplicationFactoryBase _factory;

    public AdminQuestDeleteTests(WebApplicationFactoryBase factory)
    {
        _factory = factory;
    }

    private async Task<(HttpClient AdminClient, UserEntity DungeonMaster)> ArrangeAsync()
    {
        await TestDataHelper.ClearDatabaseAsync(_factory.Services);
        await TestDataHelper.SeedCampaignGroupAsync(_factory.Services, 2);
        var (adminClient, _) = await AuthenticationHelper.CreateAuthenticatedAdminClientAsync(_factory);
        var dungeonMaster = await AuthenticationHelper.CreateTestUserAsync(
            _factory.Services, "questdelete_dm", "questdelete_dm@example.com", "Test123!", "Quest Delete DM");
        return (adminClient, dungeonMaster);
    }

    // The board filter on quests would hide another board's rows from the assertion, so these reads ignore it.
    private async Task<T> ReadAsync<T>(Func<QuestBoardContext, T> read)
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<QuestBoardContext>();
        return await Task.FromResult(read(context));
    }

    // The toast travels in temp data and is shown once, so it is read from the page the redirect lands on.
    private static async Task<List<string>> ToastsAfterAsync(HttpClient client, HttpResponseMessage response)
    {
        response.StatusCode.Should().Be(HttpStatusCode.Redirect, "a delete answers a redirect back to the quest list");
        response.Headers.Location!.OriginalString.Should().Contain("/Admin/Quests");

        var landed = await client.GetAsync(response.Headers.Location, TestContext.Current.CancellationToken);
        landed.StatusCode.Should().Be(HttpStatusCode.OK);
        var html = WebUtility.HtmlDecode(await landed.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        return Regex.Matches(html, "<strong class=\"me-auto\">(\\w+)</strong>").Select(m => m.Groups[1].Value).ToList();
    }

    private static Task<HttpResponseMessage> PostDeleteAsync(HttpClient client, int questId) =>
        client.PostAsync($"/Admin/DeleteQuest/{questId}", new FormUrlEncodedContent(new Dictionary<string, string>()), TestContext.Current.CancellationToken);

    // ---- Pins ---------------------------------------------------------------------------------------

    [Fact]
    public async Task DeleteQuest_ForAQuestOfAnotherBoard_AnswersAnErrorToast_AndDeletesNothing()
    {
        var (adminClient, dungeonMaster) = await ArrangeAsync();
        var quest = await TestDataHelper.CreateTestQuestAsync(_factory.Services, dungeonMaster.Id, "Other Board Quest", groupId: 2);
        var signup = await TestDataHelper.CreatePlayerSignupAsync(_factory.Services, quest.Id, dungeonMaster.Id);
        using (var scope = _factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<QuestBoardContext>();
            context.ReminderLogs.Add(new ReminderLogEntity { QuestId = quest.Id, PlayerId = dungeonMaster.Id, SentAt = DateTime.UtcNow });
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var response = await PostDeleteAsync(adminClient, quest.Id);

        (await ToastsAfterAsync(adminClient, response)).Should().Contain("Error", "the board scope is part of how a quest is found");
        (await ReadAsync(c => c.Quests.IgnoreQueryFilters().Any(q => q.Id == quest.Id))).Should().BeTrue("the quest of the other board must survive");
        (await ReadAsync(c => c.PlayerSignups.IgnoreQueryFilters().Any(s => s.Id == signup.Id))).Should().BeTrue("its signups must survive");
        (await ReadAsync(c => c.ReminderLogs.IgnoreQueryFilters().Count(r => r.QuestId == quest.Id))).Should().Be(1, "its reminder rows must survive");
    }

    [Fact]
    public async Task DeleteQuest_ForAQuestOfTheActiveBoard_StillRemovesTheQuestAndItsSignups()
    {
        var (adminClient, dungeonMaster) = await ArrangeAsync();
        var quest = await TestDataHelper.CreateTestQuestAsync(_factory.Services, dungeonMaster.Id, "Active Board Quest");
        var signup = await TestDataHelper.CreatePlayerSignupAsync(_factory.Services, quest.Id, dungeonMaster.Id);

        var response = await PostDeleteAsync(adminClient, quest.Id);

        (await ToastsAfterAsync(adminClient, response)).Should().Contain("Success");
        (await ReadAsync(c => c.Quests.IgnoreQueryFilters().Any(q => q.Id == quest.Id))).Should().BeFalse();
        (await ReadAsync(c => c.PlayerSignups.IgnoreQueryFilters().Any(s => s.Id == signup.Id))).Should().BeFalse();
    }

    private const string PhoneUserAgent =
        "Mozilla/5.0 (iPhone; CPU iPhone OS 17_0 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.0 Mobile/15E148 Safari/604.1";

    // Both layouts show the delete as a posted form that carries the antiforgery token and the confirm sentence, and
    // neither carries a script that sends the request itself.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task QuestsPage_RendersTheDeleteAsAConfirmedPostForm_OnBothLayouts(bool phone)
    {
        var (adminClient, dungeonMaster) = await ArrangeAsync();
        var quest = await TestDataHelper.CreateTestQuestAsync(_factory.Services, dungeonMaster.Id, "Rendered Delete Quest");

        using var request = new HttpRequestMessage(HttpMethod.Get, "/Admin/Quests");
        if (phone) request.Headers.TryAddWithoutValidation("User-Agent", PhoneUserAgent);
        var response = await adminClient.SendAsync(request, TestContext.Current.CancellationToken);
        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var form = Regex.Match(html, $"<form[^>]*action=\"/Admin/DeleteQuest/{quest.Id}\"[^>]*>(.*?)</form>", RegexOptions.Singleline);
        form.Success.Should().BeTrue("each quest row carries its own delete form");
        form.Value.Should().Contain("method=\"post\"");
        form.Value.Should().Contain("data-confirm-message=\"Are you sure you want to delete this quest? This action cannot be undone.\"");
        form.Value.Should().Contain("name=\"__RequestVerificationToken\"");
        html.Should().Contain("js/confirm-submit.js");
        html.Should().NotContain("deleteQuest(").And.NotContain("method: \"DELETE\"");
    }

    // ---- Reproduction facts: what the delete must also clean up ------------------------------------

    [Fact]
    public async Task DeleteQuest_RemovesTheReminderRowsOfTheQuest()
    {
        var (adminClient, dungeonMaster) = await ArrangeAsync();
        var quest = await TestDataHelper.CreateTestQuestAsync(_factory.Services, dungeonMaster.Id, "Reminded Quest");
        using (var scope = _factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<QuestBoardContext>();
            context.ReminderLogs.Add(new ReminderLogEntity { QuestId = quest.Id, PlayerId = dungeonMaster.Id, SentAt = DateTime.UtcNow });
            context.ReminderLogs.Add(new ReminderLogEntity { QuestId = quest.Id, PlayerId = dungeonMaster.Id, SentAt = DateTime.UtcNow.AddHours(-1) });
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var response = await PostDeleteAsync(adminClient, quest.Id);

        (await ToastsAfterAsync(adminClient, response)).Should().Contain("Success");
        (await ReadAsync(c => c.Quests.IgnoreQueryFilters().Any(q => q.Id == quest.Id))).Should().BeFalse();
        (await ReadAsync(c => c.ReminderLogs.IgnoreQueryFilters().Count(r => r.QuestId == quest.Id)))
            .Should().Be(0, "a reminder row left pointing at a deleted quest makes the delete fail on a real database");
    }

    [Fact]
    public async Task DeleteQuest_ClearsTheLinkOfAFollowUpQuestOnAnotherBoard()
    {
        var (adminClient, dungeonMaster) = await ArrangeAsync();
        var original = await TestDataHelper.CreateTestQuestAsync(_factory.Services, dungeonMaster.Id, "Original Quest");
        var followUp = await TestDataHelper.CreateTestQuestAsync(_factory.Services, dungeonMaster.Id, "Follow-up On Another Board", groupId: 2);
        using (var scope = _factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<QuestBoardContext>();
            var tracked = context.Quests.IgnoreQueryFilters().Single(q => q.Id == followUp.Id);
            tracked.OriginalQuestId = original.Id;
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var response = await PostDeleteAsync(adminClient, original.Id);

        (await ToastsAfterAsync(adminClient, response)).Should().Contain("Success");
        (await ReadAsync(c => c.Quests.IgnoreQueryFilters().Any(q => q.Id == original.Id))).Should().BeFalse();
        (await ReadAsync(c => c.Quests.IgnoreQueryFilters().Single(q => q.Id == followUp.Id).OriginalQuestId))
            .Should().BeNull("a follow-up must not keep pointing at a quest that no longer exists");
    }
}
