using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using QuestBoard.Domain.Enums;
using QuestBoard.Domain.Extensions;
using QuestBoard.Domain.Interfaces;
using QuestBoard.IntegrationTests.Helpers;
using QuestBoard.IntegrationTests.Migrator;
using QuestBoard.Repository.Automapper;
using QuestBoard.Repository.Extensions;

namespace QuestBoard.IntegrationTests.Tests;

// The in-memory store enforces no foreign key, so it cannot show whether deleting a quest really succeeds when a
// reminder row or a follow-up quest on another board still points at it. This fact migrates a throwaway database to the
// production schema, deletes such a quest through the quest service, and checks what the database itself did. It skips
// unless a SQL Server connection is supplied, and it only ever creates and drops its own uniquely named scratch
// database; the application's own database is never touched.
public class QuestDeleteSqlServerTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private sealed class FixedBoardClock : IBoardClock
    {
        public TimeZoneInfo TimeZone => TimeZoneInfo.Utc;
        public bool IsDegraded => false;
        public DateOnly Today => new(2031, 1, 1);
        public DateTime Now => new(2031, 1, 1, 12, 0, 0);
    }

    // Deleting a quest never sends mail, so every dispatch is a no-op here.
    private sealed class SilentQuestEmailDispatcher : IQuestEmailDispatcher
    {
        public void EnqueueFinalizedEmail(int questId, int groupId, DateTime finalizedDate, string[] recipientEmails,
            string[] playerNames, string questTitle, string dmName, string questDescription, int challengeRating) { }

        public void EnqueueDateChangedEmail(int questId, string[] recipientEmails, string[] playerNames,
            string questTitle, string dmName, DateTime oldDate, DateTime newDate) { }

        public void EnqueueWaitlistPromotedEmail(int questId, int groupId, DateTime finalizedDate, string recipientEmail,
            string playerName, string questTitle, string dmName, string questDescription, int challengeRating) { }

        public void EnqueueQuestMovedEmail(int questId, int groupId, QuestMovedAudience audience, string recipientEmail,
            string questTitle, string dmName, string boardName, DateTime? sessionDate) { }

        public void EnqueueWaitlistPromotedAfterMoveEmail(int questId, int groupId, DateTime finalizedDate,
            string recipientEmail, string playerName, string questTitle, string dmName, string questDescription,
            int challengeRating, string movedToBoardName) { }
    }

    private sealed record Seeded(int BoardA, int BoardB, int QuestId, int FollowUpOnBoardB, int SignupId);

    private static ServiceProvider BuildProvider(string connectionString, MutableGroupContext groupContext)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = connectionString
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IActiveGroupContext>(groupContext);
        services.AddSingleton<IBoardClock>(new FixedBoardClock());
        services.AddSingleton<IQuestEmailDispatcher>(new SilentQuestEmailDispatcher());
        services.AddAutoMapper(cfg => cfg.AddProfile<EntityProfile>());
        services.AddRepositoryServices(configuration);
        services.AddDomainServices(configuration);
        return services.BuildServiceProvider();
    }

    private static async Task<Seeded> SeedAsync(ServiceProvider provider)
    {
        await using var scope = provider.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<QuestBoardContext>();

        var suffix = Guid.NewGuid().ToString("N")[..8];
        UserEntity NewUser(string name) => new()
        {
            Name = name,
            UserName = $"{name}-{suffix}",
            NormalizedUserName = $"{name}-{suffix}".ToUpperInvariant(),
            Email = $"{name}-{suffix}@example.test",
            NormalizedEmail = $"{name}-{suffix}@example.test".ToUpperInvariant()
        };

        var dm = NewUser("dm");
        var player = NewUser("player");
        context.UserEntities.AddRange(dm, player);

        var boardA = new GroupEntity { Name = $"Board A {suffix}" };
        var boardB = new GroupEntity { Name = $"Board B {suffix}" };
        context.Groups.AddRange(boardA, boardB);
        await context.SaveChangesAsync(Token);

        var quest = new QuestEntity { Title = "Doomed quest", Description = "d", DungeonMaster = dm, Group = boardA };
        context.Quests.Add(quest);
        var date = new ProposedDateEntity { Quest = quest, Date = new DateTime(2031, 3, 1, 19, 0, 0) };
        context.Set<ProposedDateEntity>().Add(date);
        var signup = new PlayerSignupEntity { Quest = quest, Player = player };
        context.PlayerSignups.Add(signup);
        context.Set<PlayerDateVoteEntity>().Add(new PlayerDateVoteEntity { PlayerSignup = signup, ProposedDate = date, Vote = 2 });
        context.ReminderLogs.Add(new ReminderLogEntity { Quest = quest, Player = player });
        context.ReminderLogs.Add(new ReminderLogEntity { Quest = quest, Player = dm });

        var followUp = new QuestEntity { Title = "Follow-up", Description = "d", DungeonMaster = dm, Group = boardB, OriginalQuest = quest };
        context.Quests.Add(followUp);

        await context.SaveChangesAsync(Token);
        return new Seeded(boardA.Id, boardB.Id, quest.Id, followUp.Id, signup.Id);
    }

    [Fact]
    public async Task DeletingAQuestWithReminderRowsAndAFollowUpOnAnotherBoard_SucceedsAndKeepsTheFollowUp()
    {
        // Arrange
        var server = MigratorSqlServer.RequireServerConnectionString();
        using var scratch = new ScratchDatabase(server);
        var groupContext = new MutableGroupContext { ActiveGroupId = null };
        await using var provider = BuildProvider(scratch.ConnectionString, groupContext);
        await using (var migrate = provider.CreateAsyncScope())
        {
            await migrate.ServiceProvider.GetRequiredService<QuestBoardContext>().Database.MigrateAsync(Token);
        }
        var seeded = await SeedAsync(provider);
        groupContext.ActiveGroupId = seeded.BoardA;

        // Act
        await using (var scope = provider.CreateAsyncScope())
        {
            var questService = scope.ServiceProvider.GetRequiredService<IQuestService>();
            var quest = await questService.GetByIdAsync(seeded.QuestId, Token);
            quest.Should().NotBeNull();
            await questService.RemoveAsync(quest!, Token);
        }

        // Assert
        await using var verify = provider.CreateAsyncScope();
        var context = verify.ServiceProvider.GetRequiredService<QuestBoardContext>();
        (await context.Quests.IgnoreQueryFilters().AnyAsync(q => q.Id == seeded.QuestId, Token)).Should().BeFalse("the quest is deleted");
        (await context.PlayerSignups.IgnoreQueryFilters().AnyAsync(s => s.Id == seeded.SignupId, Token)).Should().BeFalse("its signups go with it");
        (await context.ReminderLogs.IgnoreQueryFilters().CountAsync(r => r.QuestId == seeded.QuestId, Token))
            .Should().Be(0, "its reminder rows go with it");

        var followUp = await context.Quests.IgnoreQueryFilters().SingleAsync(q => q.Id == seeded.FollowUpOnBoardB, Token);
        followUp.OriginalQuestId.Should().BeNull("a follow-up on another board keeps its place and only loses the link");
        followUp.GroupId.Should().Be(seeded.BoardB);
    }
}
