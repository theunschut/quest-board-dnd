using System.Reflection;
using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.Logging.Abstractions;
using QuestBoard.Domain.Enums;
using QuestBoard.Domain.Interfaces;
using QuestBoard.Repository;
using QuestBoard.Repository.Entities;
using QuestBoard.UnitTests.Helpers;
using Xunit;

namespace QuestBoard.UnitTests.Repository;

// Shared seed data and assertions for the board-deletion suites. The in-memory store enforces
// no foreign key and runs no database cascade, which is how a delete that breaks in SQL Server
// can still pass here, so every delete fact also runs a model-driven foreign-key sweep.
internal static class BoardDeletionTestGraph
{
    public const int DungeonMasterUserId = 1;
    public const int PlayerUserId = 2;
    public const int SecondPlayerUserId = 3;

    private static readonly DateTime SomeDay = new(2031, 3, 10, 19, 0, 0);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    internal sealed class MutableGroupContext : IActiveGroupContext
    {
        public int? ActiveGroupId { get; set; }
    }

    internal sealed class Store
    {
        private readonly DbContextOptions<QuestBoardContext> options = new DbContextOptionsBuilder<QuestBoardContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        public MutableGroupContext Groups { get; } = new();

        public QuestBoardContext NewContext() => new(options, Groups);
    }

    /// <summary>One board's seeded content: how much of each kind, and the ids other facts need.</summary>
    internal sealed record SeededBoard(
        int GroupId,
        IReadOnlyDictionary<BoardContentKind, int> Counts,
        int OriginalQuestId,
        int FollowUpQuestId,
        int CharacterId,
        int SeriesId);

    /// <summary>Rows that must survive a board delete, counted across the whole store.</summary>
    internal sealed record SurvivorCounts(
        int Users,
        int DungeonMasterProfiles,
        int DungeonMasterProfileImages,
        int TradeItems,
        int CalendarSubscriptions,
        int LinkedBoardSets);

    /// <summary>Rows on other boards that point into the board being deleted.</summary>
    internal sealed record ReachIns(int FollowUpQuestId, int SignupId, int EventId, int OtherQuestId);

    public static GroupRepository CreateRepository(QuestBoardContext context)
    {
        var configuration = new MapperConfiguration(
            cfg => cfg.AddProfile<QuestBoard.Repository.Automapper.EntityProfile>(), NullLoggerFactory.Instance);
        IMapper mapper = configuration.CreateMapper();
        var clock = new FakeBoardClock { Today = DateOnly.FromDateTime(DateTime.Today) };
        return new GroupRepository(context, mapper, clock);
    }

    /// <summary>Adds the boards and the three users every graph refers to.</summary>
    public static async Task SeedBoardsAndUsersAsync(QuestBoardContext context, params int[] groupIds)
    {
        foreach (var groupId in groupIds)
        {
            context.Groups.Add(new GroupEntity { Id = groupId, Name = $"Board {groupId}" });
        }

        for (var id = 1; id <= 3; id++)
        {
            context.UserEntities.Add(new UserEntity
            {
                Id = id,
                Name = $"User {id}",
                Email = $"u{id}@test.com",
                UserName = $"u{id}"
            });
        }

        await context.SaveChangesAsync(Token);
    }

    /// <summary>
    /// Seeds the quest family: a follow-up chain of two quests, proposed dates, signups with
    /// date votes, and a reminder log. Everything is added through the change tracker.
    /// </summary>
    public static async Task<SeededBoard> SeedQuestContentAsync(QuestBoardContext context, int groupId, string prefix)
    {
        var original = new QuestEntity
        {
            Title = $"{prefix} original", Description = "d", GroupId = groupId, DungeonMasterId = DungeonMasterUserId,
            ProposedDates = [new ProposedDateEntity { Date = SomeDay }, new ProposedDateEntity { Date = SomeDay.AddDays(7) }]
        };
        context.Quests.Add(original);
        await context.SaveChangesAsync(Token);

        var followUp = new QuestEntity
        {
            Title = $"{prefix} follow-up", Description = "d", GroupId = groupId, DungeonMasterId = DungeonMasterUserId,
            OriginalQuestId = original.Id
        };
        context.Quests.Add(followUp);

        var dates = original.ProposedDates.ToList();
        var signups = new[] { PlayerUserId, SecondPlayerUserId }
            .Select(playerId => new PlayerSignupEntity
            {
                QuestId = original.Id,
                PlayerId = playerId,
                DateVotes = dates.Select(d => new PlayerDateVoteEntity { ProposedDateId = d.Id, Vote = (int)VoteType.Yes }).ToList()
            })
            .ToList();
        context.PlayerSignups.AddRange(signups);
        context.ReminderLogs.Add(new ReminderLogEntity { QuestId = original.Id, PlayerId = PlayerUserId });
        await context.SaveChangesAsync(Token);

        var counts = new Dictionary<BoardContentKind, int>
        {
            [BoardContentKind.Quests] = 2,
            [BoardContentKind.ProposedDates] = dates.Count,
            [BoardContentKind.DateVotes] = signups.Sum(s => s.DateVotes.Count),
            [BoardContentKind.PlayerSignups] = signups.Count,
            [BoardContentKind.ReminderLogs] = 1
        };
        return new SeededBoard(groupId, counts, original.Id, followUp.Id, 0, 0);
    }

    /// <summary>
    /// Seeds at least one row of every kind of board content, including the rows that hang off
    /// a character or a contact and a return chained to a purchase.
    /// </summary>
    public static async Task<SeededBoard> SeedFullBoardAsync(QuestBoardContext context, int groupId, string prefix)
    {
        var quests = await SeedQuestContentAsync(context, groupId, prefix);
        var counts = new Dictionary<BoardContentKind, int>(quests.Counts);

        var series = new EventSeriesEntity
        {
            Title = $"{prefix} series", GroupId = groupId, AnchorDate = new DateOnly(2031, 3, 10),
            IntervalWeeks = 1, WeekDay = 1, CycleMask = "1"
        };
        context.EventSeries.Add(series);
        await context.SaveChangesAsync(Token);

        var occurrence = new EventEntity
        {
            Title = $"{prefix} occurrence", GroupId = groupId, Date = new DateOnly(2031, 3, 10),
            SeriesId = series.Id, SeriesSlotIndex = 0
        };
        var standalone = new EventEntity { Title = $"{prefix} standalone", GroupId = groupId, Date = new DateOnly(2031, 4, 1) };
        context.Events.AddRange(occurrence, standalone);
        await context.SaveChangesAsync(Token);

        context.EventSignups.AddRange(
            new EventSignupEntity { EventId = occurrence.Id, UserId = PlayerUserId, Availability = (int)VoteType.Yes },
            new EventSignupEntity { EventId = standalone.Id, UserId = SecondPlayerUserId, Availability = (int)VoteType.Maybe });

        var category = new ContactCategoryEntity { Name = $"{prefix} category", GroupId = groupId };
        var tag = new ContactTagEntity { Name = $"{prefix} tag", GroupId = groupId };
        context.ContactCategories.Add(category);
        context.ContactTags.Add(tag);
        await context.SaveChangesAsync(Token);

        var contact = new ContactEntity
        {
            Name = $"{prefix} contact", GroupId = groupId, CreatedByUserId = DungeonMasterUserId, CategoryId = category.Id,
            Tags = [tag],
            Notes = [new ContactNoteEntity { Text = "note", AuthorUserId = DungeonMasterUserId }],
            ProfileImage = new ContactImageEntity { OriginalImageData = [1, 2, 3] }
        };
        context.Contacts.Add(contact);

        var character = new CharacterEntity
        {
            Name = $"{prefix} hero", GroupId = groupId, OwnerId = PlayerUserId,
            Classes = [new CharacterClassEntity { Class = 1, ClassLevel = 3 }],
            ProfileImage = new CharacterImageEntity { OriginalImageData = [4, 5, 6] }
        };
        context.Characters.Add(character);

        var shopItem = new ShopItemEntity
        {
            Name = $"{prefix} potion", Description = "d", Type = 0, Rarity = 0, Price = 50m, GroupId = groupId,
            CreatedByDmId = DungeonMasterUserId
        };
        context.ShopItems.Add(shopItem);
        await context.SaveChangesAsync(Token);

        var purchase = new UserTransactionEntity
        {
            UserId = PlayerUserId, ShopItemId = shopItem.Id, TransactionType = 0, Price = 50m
        };
        context.UserTransactions.Add(purchase);
        await context.SaveChangesAsync(Token);

        context.UserTransactions.Add(new UserTransactionEntity
        {
            UserId = PlayerUserId, ShopItemId = shopItem.Id, TransactionType = 2, Price = 50m,
            OriginalTransactionId = purchase.Id
        });
        await context.SaveChangesAsync(Token);

        counts[BoardContentKind.Events] = 2;
        counts[BoardContentKind.EventSignups] = 2;
        counts[BoardContentKind.EventSeries] = 1;
        counts[BoardContentKind.Characters] = 1;
        counts[BoardContentKind.Contacts] = 1;
        counts[BoardContentKind.ContactNotes] = 1;
        counts[BoardContentKind.ContactCategories] = 1;
        counts[BoardContentKind.ContactTags] = 1;
        counts[BoardContentKind.ShopItems] = 1;
        counts[BoardContentKind.Transactions] = 2;
        return new SeededBoard(groupId, counts, quests.OriginalQuestId, quests.FollowUpQuestId, character.Id, series.Id);
    }

    /// <summary>
    /// Seeds the rows a board delete must never touch: a Dungeon Master profile with its image, a
    /// trade item, a calendar subscription, and a linked board set holding both named boards.
    /// </summary>
    public static async Task SeedSurvivorsAsync(QuestBoardContext context, int doomedGroupId, int otherGroupId)
    {
        context.DungeonMasterProfiles.Add(new DungeonMasterProfileEntity
        {
            Id = DungeonMasterUserId,
            Bio = "bio",
            ProfileImage = new DungeonMasterProfileImageEntity { OriginalImageData = [7, 8, 9] }
        });
        context.TradeItems.Add(new TradeItemEntity
        {
            ItemName = "Ring", Description = "d", OfferedByPlayerId = PlayerUserId
        });
        context.CalendarSubscriptions.Add(new CalendarSubscriptionEntity
        {
            UserId = PlayerUserId, Name = "Phone", Token = Guid.NewGuid().ToString("N")
        });

        var set = new LinkedBoardSetEntity { Name = "Linked", BoardType = 0 };
        context.LinkedBoardSets.Add(set);
        await context.SaveChangesAsync(Token);

        var boards = await context.Groups.Where(g => g.Id == doomedGroupId || g.Id == otherGroupId).ToListAsync(Token);
        foreach (var board in boards)
        {
            board.LinkedBoardSetId = set.Id;
        }

        await context.SaveChangesAsync(Token);
    }

    public static async Task<SurvivorCounts> CountSurvivorsAsync(QuestBoardContext context)
        => new(
            await context.UserEntities.CountAsync(Token),
            await context.DungeonMasterProfiles.IgnoreQueryFilters().CountAsync(Token),
            await context.Set<DungeonMasterProfileImageEntity>().IgnoreQueryFilters().CountAsync(Token),
            await context.TradeItems.IgnoreQueryFilters().CountAsync(Token),
            await context.CalendarSubscriptions.IgnoreQueryFilters().CountAsync(Token),
            await context.LinkedBoardSets.IgnoreQueryFilters().CountAsync(Token));

    /// <summary>
    /// Seeds rows on another board that point into the doomed board: a follow-up quest whose
    /// original is a doomed quest, a signup using a doomed character, and an event placed in a
    /// doomed series.
    /// </summary>
    public static async Task<ReachIns> SeedReachInsAsync(QuestBoardContext context, SeededBoard doomed, int otherGroupId)
    {
        var followUp = new QuestEntity
        {
            Title = "Reach-in follow-up", Description = "d", GroupId = otherGroupId,
            DungeonMasterId = DungeonMasterUserId, OriginalQuestId = doomed.FollowUpQuestId
        };
        var otherQuest = new QuestEntity
        {
            Title = "Reach-in host", Description = "d", GroupId = otherGroupId, DungeonMasterId = DungeonMasterUserId
        };
        context.Quests.AddRange(followUp, otherQuest);
        await context.SaveChangesAsync(Token);

        var signup = new PlayerSignupEntity
        {
            QuestId = otherQuest.Id, PlayerId = PlayerUserId, CharacterId = doomed.CharacterId
        };
        context.PlayerSignups.Add(signup);

        var otherEvent = new EventEntity
        {
            Title = "Reach-in event", GroupId = otherGroupId, Date = new DateOnly(2031, 5, 5),
            SeriesId = doomed.SeriesId, SeriesSlotIndex = 1
        };
        context.Events.Add(otherEvent);
        await context.SaveChangesAsync(Token);

        return new ReachIns(followUp.Id, signup.Id, otherEvent.Id, otherQuest.Id);
    }

    /// <summary>Counts each kind of content on a board, scoped the way the repository scopes it.</summary>
    public static async Task<IReadOnlyDictionary<BoardContentKind, int>> CountBoardRowsAsync(QuestBoardContext context, int groupId)
    {
        return new Dictionary<BoardContentKind, int>
        {
            [BoardContentKind.Quests] = await context.Quests.IgnoreQueryFilters().CountAsync(q => q.GroupId == groupId, Token),
            [BoardContentKind.ProposedDates] = await context.Set<ProposedDateEntity>().IgnoreQueryFilters().CountAsync(pd => pd.Quest.GroupId == groupId, Token),
            [BoardContentKind.DateVotes] = await context.Set<PlayerDateVoteEntity>().IgnoreQueryFilters().CountAsync(v => v.ProposedDate.Quest.GroupId == groupId, Token),
            [BoardContentKind.PlayerSignups] = await context.PlayerSignups.IgnoreQueryFilters().CountAsync(ps => ps.Quest.GroupId == groupId, Token),
            [BoardContentKind.ReminderLogs] = await context.ReminderLogs.IgnoreQueryFilters().CountAsync(r => r.Quest.GroupId == groupId, Token),
            [BoardContentKind.Events] = await context.Events.IgnoreQueryFilters().CountAsync(e => e.GroupId == groupId, Token),
            [BoardContentKind.EventSignups] = await context.EventSignups.IgnoreQueryFilters().CountAsync(es => es.Event.GroupId == groupId, Token),
            [BoardContentKind.EventSeries] = await context.EventSeries.IgnoreQueryFilters().CountAsync(s => s.GroupId == groupId, Token),
            [BoardContentKind.Characters] = await context.Characters.IgnoreQueryFilters().CountAsync(c => c.GroupId == groupId, Token),
            [BoardContentKind.Contacts] = await context.Contacts.IgnoreQueryFilters().CountAsync(c => c.GroupId == groupId, Token),
            [BoardContentKind.ContactNotes] = await context.Set<ContactNoteEntity>().IgnoreQueryFilters().CountAsync(n => n.Contact.GroupId == groupId, Token),
            [BoardContentKind.ContactCategories] = await context.Set<ContactCategoryEntity>().IgnoreQueryFilters().CountAsync(c => c.GroupId == groupId, Token),
            [BoardContentKind.ContactTags] = await context.Set<ContactTagEntity>().IgnoreQueryFilters().CountAsync(t => t.GroupId == groupId, Token),
            [BoardContentKind.ShopItems] = await context.ShopItems.IgnoreQueryFilters().CountAsync(s => s.GroupId == groupId, Token),
            [BoardContentKind.Transactions] = await context.UserTransactions.IgnoreQueryFilters().CountAsync(t => t.ShopItem.GroupId == groupId, Token)
        };
    }

    /// <summary>
    /// A readable description of every row on a board, with its key fields and foreign keys, so a
    /// fact can compare a board before and after another board's delete.
    /// </summary>
    public static async Task<IReadOnlyList<string>> SnapshotBoardAsync(QuestBoardContext context, int groupId)
    {
        var lines = new List<string>();

        lines.AddRange((await context.Quests.IgnoreQueryFilters().Where(q => q.GroupId == groupId).ToListAsync(Token))
            .Select(q => $"Quest|{q.Id}|{q.Title}|original={q.OriginalQuestId}"));
        lines.AddRange((await context.Set<ProposedDateEntity>().IgnoreQueryFilters().Where(pd => pd.Quest.GroupId == groupId).ToListAsync(Token))
            .Select(pd => $"ProposedDate|{pd.Id}|quest={pd.QuestId}|{pd.Date:O}"));
        lines.AddRange((await context.Set<PlayerDateVoteEntity>().IgnoreQueryFilters().Where(v => v.ProposedDate.Quest.GroupId == groupId).ToListAsync(Token))
            .Select(v => $"DateVote|{v.Id}|signup={v.PlayerSignupId}|date={v.ProposedDateId}|{v.Vote}"));
        lines.AddRange((await context.PlayerSignups.IgnoreQueryFilters().Where(ps => ps.Quest.GroupId == groupId).ToListAsync(Token))
            .Select(ps => $"Signup|{ps.Id}|quest={ps.QuestId}|player={ps.PlayerId}|character={ps.CharacterId}"));
        lines.AddRange((await context.ReminderLogs.IgnoreQueryFilters().Where(r => r.Quest.GroupId == groupId).ToListAsync(Token))
            .Select(r => $"Reminder|{r.Id}|quest={r.QuestId}|player={r.PlayerId}"));
        lines.AddRange((await context.Events.IgnoreQueryFilters().Where(e => e.GroupId == groupId).ToListAsync(Token))
            .Select(e => $"Event|{e.Id}|{e.Title}|series={e.SeriesId}|slot={e.SeriesSlotIndex}"));
        lines.AddRange((await context.EventSignups.IgnoreQueryFilters().Where(es => es.Event.GroupId == groupId).ToListAsync(Token))
            .Select(es => $"EventSignup|{es.Id}|event={es.EventId}|user={es.UserId}|{es.Availability}"));
        lines.AddRange((await context.EventSeries.IgnoreQueryFilters().Where(s => s.GroupId == groupId).ToListAsync(Token))
            .Select(s => $"Series|{s.Id}|{s.Title}"));
        lines.AddRange((await context.Characters.IgnoreQueryFilters().Where(c => c.GroupId == groupId).ToListAsync(Token))
            .Select(c => $"Character|{c.Id}|{c.Name}|owner={c.OwnerId}"));
        lines.AddRange((await context.Set<CharacterClassEntity>().IgnoreQueryFilters().Where(cc => cc.Character.GroupId == groupId).ToListAsync(Token))
            .Select(cc => $"CharacterClass|{cc.Id}|character={cc.CharacterId}|{cc.Class}|{cc.ClassLevel}"));
        lines.AddRange((await context.Contacts.IgnoreQueryFilters().Where(c => c.GroupId == groupId).ToListAsync(Token))
            .Select(c => $"Contact|{c.Id}|{c.Name}|category={c.CategoryId}"));
        lines.AddRange((await context.Set<ContactNoteEntity>().IgnoreQueryFilters().Where(n => n.Contact.GroupId == groupId).ToListAsync(Token))
            .Select(n => $"ContactNote|{n.Id}|contact={n.ContactId}|{n.Text}"));
        lines.AddRange((await context.Set<ContactCategoryEntity>().IgnoreQueryFilters().Where(c => c.GroupId == groupId).ToListAsync(Token))
            .Select(c => $"ContactCategory|{c.Id}|{c.Name}"));
        lines.AddRange((await context.Set<ContactTagEntity>().IgnoreQueryFilters().Where(t => t.GroupId == groupId).ToListAsync(Token))
            .Select(t => $"ContactTag|{t.Id}|{t.Name}"));
        lines.AddRange((await context.ShopItems.IgnoreQueryFilters().Where(s => s.GroupId == groupId).ToListAsync(Token))
            .Select(s => $"ShopItem|{s.Id}|{s.Name}|{s.Price}"));
        lines.AddRange((await context.UserTransactions.IgnoreQueryFilters().Where(t => t.ShopItem.GroupId == groupId).ToListAsync(Token))
            .Select(t => $"Transaction|{t.Id}|item={t.ShopItemId}|original={t.OriginalTransactionId}|{t.Price}"));

        return lines.Order(StringComparer.Ordinal).ToList();
    }

    /// <summary>
    /// Fails when any foreign key that SQL Server would enforce points at a missing row.
    /// </summary>
    /// <remarks>
    /// The in-memory store enforces no foreign key and runs no database cascade, so this checks
    /// exactly what SQL Server would reject. Cascade and SetNull keys are skipped because the
    /// database applies those itself.
    /// </remarks>
    public static async Task AssertNoDanglingNonCascadeForeignKeys(QuestBoardContext context)
    {
        var columnCache = new Dictionary<(Type Entity, string Property), IReadOnlyList<object?>>();

        // Reads one column of every row through EF.Property so shadow foreign keys, which have
        // no CLR property, are covered the same as mapped ones.
        async Task<IReadOnlyList<object?>> ColumnAsync(IReadOnlyEntityType entityType, IReadOnlyProperty property)
        {
            var key = (entityType.ClrType, property.Name);
            if (!columnCache.TryGetValue(key, out var values))
            {
                var load = typeof(BoardDeletionTestGraph)
                    .GetMethod(nameof(LoadColumnAsync), BindingFlags.NonPublic | BindingFlags.Static)!
                    .MakeGenericMethod(entityType.ClrType, property.ClrType);
                values = await (Task<IReadOnlyList<object?>>)load.Invoke(null, [context, property.Name])!;
                columnCache[key] = values;
            }

            return values;
        }

        foreach (var entityType in context.Model.GetEntityTypes())
        {
            if (entityType.HasSharedClrType || entityType.IsOwned())
            {
                continue;
            }

            foreach (var foreignKey in entityType.GetForeignKeys())
            {
                if (foreignKey.DeleteBehavior is DeleteBehavior.Cascade or DeleteBehavior.SetNull)
                {
                    continue;
                }

                if (foreignKey.PrincipalEntityType.HasSharedClrType)
                {
                    continue;
                }

                if (foreignKey.Properties.Count != 1 || foreignKey.PrincipalKey.Properties.Count != 1)
                {
                    Assert.Fail($"{entityType.DisplayName()} has a composite foreign key to " +
                        $"{foreignKey.PrincipalEntityType.DisplayName()}; the foreign-key sweep needs extending to cover it.");
                }

                var dependentProperty = foreignKey.Properties[0];
                var principalKeys = (await ColumnAsync(foreignKey.PrincipalEntityType, foreignKey.PrincipalKey.Properties[0]))
                    .ToHashSet();

                foreach (var value in await ColumnAsync(entityType, dependentProperty))
                {
                    if (value is not null && !principalKeys.Contains(value))
                    {
                        Assert.Fail($"{entityType.DisplayName()}.{dependentProperty.Name} = {value} " +
                            $"(foreign key to {foreignKey.PrincipalEntityType.DisplayName()}, {foreignKey.DeleteBehavior}) " +
                            "has no principal row; SQL Server would reject this.");
                    }
                }
            }
        }
    }

    private static async Task<IReadOnlyList<object?>> LoadColumnAsync<TEntity, TProperty>(QuestBoardContext context, string propertyName)
        where TEntity : class
    {
        var values = await context.Set<TEntity>()
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Select(e => EF.Property<TProperty>(e, propertyName))
            .ToListAsync(Token);
        return values.Select(v => (object?)v).ToList();
    }
}
