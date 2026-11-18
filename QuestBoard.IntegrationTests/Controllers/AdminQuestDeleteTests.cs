using System.Net;
using QuestBoard.Domain.Enums;
using QuestBoard.IntegrationTests.Helpers;

namespace QuestBoard.IntegrationTests.Controllers;

/// <summary>
/// Deleting a quest from the Admin quest list. The delete is limited to the active board: a crafted request for a quest
/// of another board answers not found and removes nothing. Deleting a quest of the active board must also remove the
/// reminder rows that point at it and unlink a follow-up quest that lives on another board, or the delete throws on a
/// real database. The in-memory database does not enforce foreign keys, so those two facts assert the rows are gone,
/// not that the delete failed. The facts marked as reproducing an open gap stay skipped until the cleanup exists.
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

    // ---- Pins ---------------------------------------------------------------------------------------

    [Fact]
    public async Task DeleteQuest_ForAQuestOfAnotherBoard_Answers404_AndDeletesNothing()
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

        var response = await adminClient.DeleteAsync($"/Admin/DeleteQuest/{quest.Id}", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound, "the board scope is part of how a quest is found");
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

        var response = await adminClient.DeleteAsync($"/Admin/DeleteQuest/{quest.Id}", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ReadAsync(c => c.Quests.IgnoreQueryFilters().Any(q => q.Id == quest.Id))).Should().BeFalse();
        (await ReadAsync(c => c.PlayerSignups.IgnoreQueryFilters().Any(s => s.Id == signup.Id))).Should().BeFalse();
    }

    // ---- Reproduction facts: what the delete must also clean up ------------------------------------

    [Fact(Skip = "Reproduces an open gap; enabled by its fix")]
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

        var response = await adminClient.DeleteAsync($"/Admin/DeleteQuest/{quest.Id}", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ReadAsync(c => c.Quests.IgnoreQueryFilters().Any(q => q.Id == quest.Id))).Should().BeFalse();
        (await ReadAsync(c => c.ReminderLogs.IgnoreQueryFilters().Count(r => r.QuestId == quest.Id)))
            .Should().Be(0, "a reminder row left pointing at a deleted quest makes the delete fail on a real database");
    }

    [Fact(Skip = "Reproduces an open gap; enabled by its fix")]
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

        var response = await adminClient.DeleteAsync($"/Admin/DeleteQuest/{original.Id}", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ReadAsync(c => c.Quests.IgnoreQueryFilters().Any(q => q.Id == original.Id))).Should().BeFalse();
        (await ReadAsync(c => c.Quests.IgnoreQueryFilters().Single(q => q.Id == followUp.Id).OriginalQuestId))
            .Should().BeNull("a follow-up must not keep pointing at a quest that no longer exists");
    }
}
