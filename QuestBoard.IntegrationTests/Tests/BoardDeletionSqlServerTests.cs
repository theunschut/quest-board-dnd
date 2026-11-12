using System.Data.Common;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using QuestBoard.Domain.Enums;
using QuestBoard.Domain.Interfaces;
using QuestBoard.Domain.Models.BoardDeletion;
using QuestBoard.IntegrationTests.Helpers;
using QuestBoard.IntegrationTests.Migrator;
using QuestBoard.Repository.Automapper;
using QuestBoard.Repository.Extensions;

namespace QuestBoard.IntegrationTests.Tests;

// The in-memory store enforces no foreign key and runs no database cascade, so it cannot show
// whether a board delete would really succeed. These facts are the only automated proof of the
// real delete rules: they migrate a throwaway database to the production schema, delete a fully
// populated board through the repository, and check what the database itself did. They skip
// unless a SQL Server connection is supplied, and they only ever create and drop their own
// uniquely named scratch database; the application's own database is never touched.
public class BoardDeletionSqlServerTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    // The container clock is irrelevant here; a fixed UTC clock keeps the repository deterministic.
    private sealed class FixedBoardClock : IBoardClock
    {
        public TimeZoneInfo TimeZone => TimeZoneInfo.Utc;
        public bool IsDegraded => false;
        public DateOnly Today => new(2031, 1, 1);
        public DateTime Now => new(2031, 1, 1, 12, 0, 0);
    }

    private sealed record Seeded(
        int BoardA,
        int BoardB,
        int LinkedSetId,
        int CharacterA,
        int ContactA,
        int ContactB,
        int FollowUpOnB,
        int SignupOnBUsingACharacter,
        int EventOnBInASeries);

    private static ServiceProvider BuildProvider(string connectionString, IInterceptor? interceptor = null)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = connectionString
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IActiveGroupContext>(new MutableGroupContext { ActiveGroupId = null });
        services.AddSingleton<IBoardClock>(new FixedBoardClock());
        services.AddAutoMapper(cfg => cfg.AddProfile<EntityProfile>());
        services.AddRepositoryServices(configuration);
        if (interceptor != null)
            services.ConfigureDbContext<QuestBoardContext>(options => options.AddInterceptors(interceptor));
        return services.BuildServiceProvider();
    }

    private static async Task MigrateAsync(ServiceProvider provider)
    {
        await using var scope = provider.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<QuestBoardContext>();
        await context.Database.MigrateAsync(Token);
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
        var player2 = NewUser("player2");
        context.UserEntities.AddRange(dm, player, player2);

        var set = new LinkedBoardSetEntity { Name = $"Set {suffix}", BoardType = 0 };
        var boardA = new GroupEntity { Name = $"Board A {suffix}", LinkedBoardSet = set };
        var boardB = new GroupEntity { Name = $"Board B {suffix}", LinkedBoardSet = set };
        context.Groups.AddRange(boardA, boardB);
        await context.SaveChangesAsync(Token);

        // Board A: one row of every board-owned kind.
        var questA1 = new QuestEntity { Title = "A1", Description = "d", DungeonMaster = dm, Group = boardA };
        var questA2 = new QuestEntity { Title = "A2", Description = "d", DungeonMaster = dm, Group = boardA, OriginalQuest = questA1 };
        var questA3 = new QuestEntity { Title = "A3", Description = "d", DungeonMaster = dm, Group = boardA };
        context.Quests.AddRange(questA1, questA2, questA3);

        var dateOne = new ProposedDateEntity { Quest = questA1, Date = new DateTime(2031, 3, 1, 19, 0, 0) };
        var dateTwo = new ProposedDateEntity { Quest = questA1, Date = new DateTime(2031, 3, 8, 19, 0, 0) };
        context.Set<ProposedDateEntity>().AddRange(dateOne, dateTwo);

        var characterA = new CharacterEntity
        {
            Name = "Aria",
            Owner = player,
            Group = boardA,
            Classes = [new CharacterClassEntity { Class = 1, ClassLevel = 3 }],
            ProfileImage = new CharacterImageEntity { OriginalImageData = [1, 2, 3, 4], CroppedImageData = [1, 2] }
        };
        context.Characters.Add(characterA);

        var signupOne = new PlayerSignupEntity { Quest = questA1, Player = player, Character = characterA };
        var signupTwo = new PlayerSignupEntity { Quest = questA1, Player = player2 };
        context.PlayerSignups.AddRange(signupOne, signupTwo);
        foreach (var signup in new[] { signupOne, signupTwo })
        {
            foreach (var date in new[] { dateOne, dateTwo })
            {
                context.Set<PlayerDateVoteEntity>().Add(new PlayerDateVoteEntity { PlayerSignup = signup, ProposedDate = date, Vote = 2 });
            }
        }

        context.ReminderLogs.Add(new ReminderLogEntity { Quest = questA1, Player = player });

        var seriesA = new EventSeriesEntity
        {
            Title = "Weekly",
            AnchorDate = new DateOnly(2031, 2, 3),
            IntervalWeeks = 1,
            WeekDay = 1,
            CycleMask = "1",
            Group = boardA
        };
        var occurrenceA = new EventEntity { Title = "Occurrence", Date = new DateOnly(2031, 2, 3), Group = boardA, Series = seriesA, SeriesSlotIndex = 0 };
        var standaloneA = new EventEntity { Title = "One off", Date = new DateOnly(2031, 2, 10), Group = boardA };
        context.EventSeries.Add(seriesA);
        context.Events.AddRange(occurrenceA, standaloneA);
        context.EventSignups.AddRange(
            new EventSignupEntity { Event = occurrenceA, User = player, Availability = 2 },
            new EventSignupEntity { Event = standaloneA, User = player, Availability = 1 });

        var categoryA = new ContactCategoryEntity { Name = "Allies", Group = boardA };
        var tagA = new ContactTagEntity { Name = "friendly", Group = boardA };
        var contactA = new ContactEntity
        {
            Name = "Mira",
            CreatedByUser = dm,
            Group = boardA,
            Category = categoryA,
            Tags = [tagA],
            Notes = [new ContactNoteEntity { Text = "Owes a favour", Author = dm }],
            ProfileImage = new ContactImageEntity { OriginalImageData = [9, 8, 7, 6] }
        };
        context.Set<ContactCategoryEntity>().Add(categoryA);
        context.Set<ContactTagEntity>().Add(tagA);
        context.Contacts.Add(contactA);

        var shopItemA = new ShopItemEntity { Name = "Potion", Description = "d", Price = 50m, CreatedByDm = dm, Group = boardA };
        var purchase = new UserTransactionEntity { User = player, ShopItem = shopItemA, TransactionType = 0, Price = 50m };
        var returned = new UserTransactionEntity { User = player, ShopItem = shopItemA, TransactionType = 1, Price = 50m, OriginalTransaction = purchase };
        context.ShopItems.Add(shopItemA);
        context.UserTransactions.AddRange(purchase, returned);

        await context.SaveChangesAsync(Token);

        // Board B: a smaller graph of its own plus the three ways it reaches into board A.
        var questB1 = new QuestEntity { Title = "B1", Description = "d", DungeonMaster = dm, Group = boardB };
        var followUpOnB = new QuestEntity { Title = "B2", Description = "d", DungeonMaster = dm, Group = boardB, OriginalQuest = questA3 };
        context.Quests.AddRange(questB1, followUpOnB);

        var dateB = new ProposedDateEntity { Quest = questB1, Date = new DateTime(2031, 4, 1, 19, 0, 0) };
        context.Set<ProposedDateEntity>().Add(dateB);

        var characterB = new CharacterEntity
        {
            Name = "Borin",
            Owner = player2,
            Group = boardB,
            Classes = [new CharacterClassEntity { Class = 2, ClassLevel = 1 }],
            ProfileImage = new CharacterImageEntity { OriginalImageData = [5, 5, 5] }
        };
        context.Characters.Add(characterB);

        var signupOnB = new PlayerSignupEntity { Quest = questB1, Player = player2, Character = characterA };
        var signupOnBOwn = new PlayerSignupEntity { Quest = questB1, Player = player, Character = characterB };
        context.PlayerSignups.AddRange(signupOnB, signupOnBOwn);
        context.Set<PlayerDateVoteEntity>().Add(new PlayerDateVoteEntity { PlayerSignup = signupOnB, ProposedDate = dateB, Vote = 1 });

        var tagB = new ContactTagEntity { Name = "hostile", Group = boardB };
        var contactB = new ContactEntity { Name = "Garrick", CreatedByUser = dm, Group = boardB, Tags = [tagB] };
        context.Set<ContactTagEntity>().Add(tagB);
        context.Contacts.Add(contactB);

        var shopItemB = new ShopItemEntity { Name = "Rope", Description = "d", Price = 1m, CreatedByDm = dm, Group = boardB };
        context.ShopItems.Add(shopItemB);
        context.UserTransactions.Add(new UserTransactionEntity { User = player, ShopItem = shopItemB, TransactionType = 0, Price = 1m });

        var eventOnB = new EventEntity { Title = "Reaches in", Date = new DateOnly(2031, 2, 17), Group = boardB, Series = seriesA, SeriesSlotIndex = 1 };
        context.Events.Add(eventOnB);

        await context.SaveChangesAsync(Token);

        return new Seeded(
            boardA.Id,
            boardB.Id,
            set.Id,
            characterA.Id,
            contactA.Id,
            contactB.Id,
            followUpOnB.Id,
            signupOnB.Id,
            eventOnB.Id);
    }

    // Row counts per board-owned table, read without the tenant filter. Images are counted
    // without their blobs being loaded.
    private static async Task<Dictionary<string, int>> CountsAsync(ServiceProvider provider, int groupId)
    {
        await using var scope = provider.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<QuestBoardContext>();

        return new Dictionary<string, int>
        {
            ["Quests"] = await context.Quests.IgnoreQueryFilters().CountAsync(q => q.GroupId == groupId, Token),
            ["ProposedDates"] = await context.Set<ProposedDateEntity>().IgnoreQueryFilters().CountAsync(x => x.Quest.GroupId == groupId, Token),
            ["DateVotes"] = await context.Set<PlayerDateVoteEntity>().IgnoreQueryFilters().CountAsync(x => x.ProposedDate.Quest.GroupId == groupId, Token),
            ["PlayerSignups"] = await context.PlayerSignups.IgnoreQueryFilters().CountAsync(x => x.Quest.GroupId == groupId, Token),
            ["ReminderLogs"] = await context.ReminderLogs.IgnoreQueryFilters().CountAsync(x => x.Quest.GroupId == groupId, Token),
            ["Events"] = await context.Events.IgnoreQueryFilters().CountAsync(x => x.GroupId == groupId, Token),
            ["EventSignups"] = await context.EventSignups.IgnoreQueryFilters().CountAsync(x => x.Event.GroupId == groupId, Token),
            ["EventSeries"] = await context.EventSeries.IgnoreQueryFilters().CountAsync(x => x.GroupId == groupId, Token),
            ["Characters"] = await context.Characters.IgnoreQueryFilters().CountAsync(x => x.GroupId == groupId, Token),
            ["CharacterClasses"] = await context.Set<CharacterClassEntity>().IgnoreQueryFilters().CountAsync(x => x.Character.GroupId == groupId, Token),
            ["CharacterImages"] = await context.Set<CharacterImageEntity>().IgnoreQueryFilters().CountAsync(x => x.Character.GroupId == groupId, Token),
            ["Contacts"] = await context.Contacts.IgnoreQueryFilters().CountAsync(x => x.GroupId == groupId, Token),
            ["ContactNotes"] = await context.Set<ContactNoteEntity>().IgnoreQueryFilters().CountAsync(x => x.Contact.GroupId == groupId, Token),
            ["ContactImages"] = await context.Set<ContactImageEntity>().IgnoreQueryFilters().CountAsync(x => x.Contact.GroupId == groupId, Token),
            ["ContactCategories"] = await context.Set<ContactCategoryEntity>().IgnoreQueryFilters().CountAsync(x => x.GroupId == groupId, Token),
            ["ContactTags"] = await context.Set<ContactTagEntity>().IgnoreQueryFilters().CountAsync(x => x.GroupId == groupId, Token),
            ["ShopItems"] = await context.ShopItems.IgnoreQueryFilters().CountAsync(x => x.GroupId == groupId, Token),
            ["Transactions"] = await context.UserTransactions.IgnoreQueryFilters().CountAsync(x => x.ShopItem.GroupId == groupId, Token),
            ["Board"] = await context.Groups.CountAsync(g => g.Id == groupId, Token),
        };
    }

    // The contact-tag join table has no mapped CLR type of its own, so it is read by column name
    // (the names come from the migration snapshot) with the contact id passed as a parameter.
    private static async Task<int> TagJoinRowsForContactAsync(ServiceProvider provider, int contactId)
    {
        await using var scope = provider.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<QuestBoardContext>();
        return await context.Database
            .SqlQuery<int>($"SELECT COUNT(*) AS [Value] FROM [ContactContactTags] WHERE [ContactsId] = {contactId}")
            .SingleAsync(Token);
    }

    [Fact]
    public async Task DeletingAFullyPopulatedBoard_SucceedsAndLeavesTheOtherBoardIntact()
    {
        // Arrange
        var server = MigratorSqlServer.RequireServerConnectionString();
        using var scratch = new ScratchDatabase(server);
        await using var provider = BuildProvider(scratch.ConnectionString);
        await MigrateAsync(provider);
        var seeded = await SeedAsync(provider);

        var boardAbefore = await CountsAsync(provider, seeded.BoardA);
        var boardBbefore = await CountsAsync(provider, seeded.BoardB);
        boardAbefore.Values.Should().OnlyContain(count => count > 0, "every board-owned kind is seeded on the doomed board");
        (await TagJoinRowsForContactAsync(provider, seeded.ContactA)).Should().Be(1);
        (await TagJoinRowsForContactAsync(provider, seeded.ContactB)).Should().Be(1);

        // Act
        BoardDeletionResult result;
        await using (var scope = provider.CreateAsyncScope())
        {
            var repository = scope.ServiceProvider.GetRequiredService<IGroupRepository>();
            result = await repository.DeleteBoardAsync(seeded.BoardA, Token);
        }

        // Assert
        result.Status.Should().Be(BoardDeleteStatus.Deleted);

        var boardAafter = await CountsAsync(provider, seeded.BoardA);
        boardAafter.Values.Should().OnlyContain(count => count == 0, "the delete and the database cascades leave nothing of the board");
        (await TagJoinRowsForContactAsync(provider, seeded.ContactA)).Should().Be(0);

        (await CountsAsync(provider, seeded.BoardB)).Should().Equal(boardBbefore);
        (await TagJoinRowsForContactAsync(provider, seeded.ContactB)).Should().Be(1);

        await using var verify = provider.CreateAsyncScope();
        var context = verify.ServiceProvider.GetRequiredService<QuestBoardContext>();

        var followUp = await context.Quests.IgnoreQueryFilters().SingleAsync(q => q.Id == seeded.FollowUpOnB, Token);
        followUp.OriginalQuestId.Should().BeNull();

        var signup = await context.PlayerSignups.IgnoreQueryFilters().SingleAsync(s => s.Id == seeded.SignupOnBUsingACharacter, Token);
        signup.CharacterId.Should().BeNull();

        var reachingEvent = await context.Events.IgnoreQueryFilters().SingleAsync(e => e.Id == seeded.EventOnBInASeries, Token);
        reachingEvent.SeriesId.Should().BeNull();
        reachingEvent.SeriesSlotIndex.Should().BeNull();

        var set = await context.Set<LinkedBoardSetEntity>().SingleAsync(s => s.Id == seeded.LinkedSetId, Token);
        set.Should().NotBeNull();
        var boardB = await context.Groups.SingleAsync(g => g.Id == seeded.BoardB, Token);
        boardB.LinkedBoardSetId.Should().Be(seeded.LinkedSetId);
    }

    [Fact]
    public async Task AFailureInsideTheSave_RollsEveryRowBack()
    {
        // Arrange
        var server = MigratorSqlServer.RequireServerConnectionString();
        using var scratch = new ScratchDatabase(server);
        await using var provider = BuildProvider(scratch.ConnectionString);
        await MigrateAsync(provider);
        var seeded = await SeedAsync(provider);

        var boardAbefore = await CountsAsync(provider, seeded.BoardA);
        var boardBbefore = await CountsAsync(provider, seeded.BoardB);

        // The board row is the last principal the save removes, so every earlier delete in the
        // same transaction has already run when this trigger fails the statement. The server may
        // refuse the statement itself because the table now has a trigger; either error fails
        // the save at the same point, which is all the rollback check needs.
        MigratorSqlServer.Execute(
            scratch.ConnectionString,
            "CREATE TRIGGER [dbo].[BlockBoardDelete] ON [dbo].[Groups] AFTER DELETE AS " +
            "BEGIN THROW 50001, N'board delete blocked for the test', 1; END");

        // Act
        Func<Task> act = async () =>
        {
            await using var scope = provider.CreateAsyncScope();
            var repository = scope.ServiceProvider.GetRequiredService<IGroupRepository>();
            await repository.DeleteBoardAsync(seeded.BoardA, Token);
        };

        // Assert
        await act.Should().ThrowAsync<Exception>();

        (await CountsAsync(provider, seeded.BoardA)).Should().Equal(boardAbefore);
        (await CountsAsync(provider, seeded.BoardB)).Should().Equal(boardBbefore);
        (await TagJoinRowsForContactAsync(provider, seeded.ContactA)).Should().Be(1);
        (await TagJoinRowsForContactAsync(provider, seeded.ContactB)).Should().Be(1);
    }
    // Stands in for a second super admin adding a member while the delete is saving. Just before the
    // first write of the delete's save runs, it tries to add a member to the board on its own
    // connection with a short lock timeout, and records what the server said.
    private sealed class ConcurrentMemberAdd(string connectionString) : DbCommandInterceptor
    {
        private const int LockRequestTimedOut = 1222;

        private int? _boardId;
        private bool _fired;

        public int? ServerError { get; private set; }
        public bool Added { get; private set; }

        public void Arm(int boardId) => _boardId = boardId;

        public bool WasHeldOffByALock => !Added && ServerError == LockRequestTimedOut;

        public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            await TryAddAsync(command, cancellationToken);
            return result;
        }

        public override async ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            await TryAddAsync(command, cancellationToken);
            return result;
        }

        private async Task TryAddAsync(DbCommand command, CancellationToken token)
        {
            if (_boardId == null || _fired || command.Transaction == null
                || command.CommandText.TrimStart().StartsWith("SELECT", StringComparison.OrdinalIgnoreCase))
                return;

            _fired = true;
            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync(token);
            await using var insert = connection.CreateCommand();
            insert.CommandText =
                "SET LOCK_TIMEOUT 3000; " +
                "INSERT INTO [UserGroups] ([UserId], [GroupId], [GroupRole]) " +
                "SELECT TOP 1 [Id], @boardId, 0 FROM [AspNetUsers] ORDER BY [Id];";
            insert.Parameters.AddWithValue("@boardId", _boardId.Value);
            try
            {
                Added = await insert.ExecuteNonQueryAsync(token) == 1;
            }
            catch (SqlException ex)
            {
                ServerError = ex.Number;
            }
        }
    }

    [Fact]
    public async Task AMemberAddedWhileTheDeleteSaves_IsHeldOffInsteadOfSilentlyErased()
    {
        // Arrange
        var server = MigratorSqlServer.RequireServerConnectionString();
        using var scratch = new ScratchDatabase(server);
        var concurrentAdd = new ConcurrentMemberAdd(scratch.ConnectionString);
        await using var provider = BuildProvider(scratch.ConnectionString, concurrentAdd);
        await MigrateAsync(provider);
        var seeded = await SeedAsync(provider);
        concurrentAdd.Arm(seeded.BoardA);

        // Act
        BoardDeletionResult result;
        await using (var scope = provider.CreateAsyncScope())
        {
            var repository = scope.ServiceProvider.GetRequiredService<IGroupRepository>();
            result = await repository.DeleteBoardAsync(seeded.BoardA, Token);
        }

        // Assert: the member check's lock was still held when the save began, so the add had to wait
        // for the delete to finish rather than land in between and be cascaded away unseen.
        concurrentAdd.WasHeldOffByALock.Should().BeTrue(
            $"the add must block on the delete's membership lock (added: {concurrentAdd.Added}, server error: {concurrentAdd.ServerError})");
        result.Status.Should().Be(BoardDeleteStatus.Deleted);

        await using var verify = provider.CreateAsyncScope();
        var context = verify.ServiceProvider.GetRequiredService<QuestBoardContext>();
        (await context.Groups.AnyAsync(g => g.Id == seeded.BoardA, Token)).Should().BeFalse();
        (await context.UserGroups.AnyAsync(ug => ug.GroupId == seeded.BoardA, Token)).Should().BeFalse();
    }
}
