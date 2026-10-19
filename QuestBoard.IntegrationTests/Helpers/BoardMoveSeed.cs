using Microsoft.EntityFrameworkCore;
using QuestBoard.Domain.Enums;

namespace QuestBoard.IntegrationTests.Helpers;

/// <summary>
/// Seeding shared by the board-move test classes: boards, linked sets, memberships, quests,
/// signups and characters, all written straight to the store so a test sets up exactly the
/// state it needs.
/// </summary>
public static class BoardMoveSeed
{
    public static async Task SeedBoardAsync(WebApplicationFactoryBase factory, int id, string name, BoardType type)
    {
        await using var ctx = factory.Database.CreateContext();
        if (!ctx.Groups.Any(g => g.Id == id))
        {
            ctx.Groups.Add(new GroupEntity { Id = id, Name = name, CreatedAt = DateTime.UtcNow, BoardType = (int)type });
            await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);
        }
    }

    public static async Task<int> LinkAsync(WebApplicationFactoryBase factory, BoardType type, params int[] boardIds)
    {
        await using var ctx = factory.Database.CreateContext();
        var set = new LinkedBoardSetEntity { Name = "Move Set", BoardType = (int)type, CreatedAt = DateTime.UtcNow };
        ctx.LinkedBoardSets.Add(set);
        await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);

        foreach (var board in ctx.Groups.Where(g => boardIds.Contains(g.Id)).ToList())
        {
            board.LinkedBoardSetId = set.Id;
            board.BoardType = (int)type;
        }
        await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);
        return set.Id;
    }

    public static async Task SeedMembershipAsync(WebApplicationFactoryBase factory, int userId, int groupId, GroupRole role)
    {
        await using var ctx = factory.Database.CreateContext();
        ctx.UserGroups.Add(new UserGroupEntity { UserId = userId, GroupId = groupId, GroupRole = (int)role });
        await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    public static async Task RemoveMembershipAsync(WebApplicationFactoryBase factory, int userId, int groupId)
    {
        await using var ctx = factory.Database.CreateContext();
        var rows = ctx.UserGroups.Where(ug => ug.UserId == userId && ug.GroupId == groupId).ToList();
        ctx.UserGroups.RemoveRange(rows);
        await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    public static async Task<UserEntity> CreateUserAsync(WebApplicationFactoryBase factory, string key, string name, bool emailConfirmed = true)
    {
        var user = await AuthenticationHelper.CreateTestUserAsync(
            factory.Services, key, $"{key}@example.com", name: name);

        if (!emailConfirmed)
        {
            await using var ctx = factory.Database.CreateContext();
            var row = await ctx.UserEntities.FirstAsync(u => u.Id == user.Id, TestContext.Current.CancellationToken);
            row.EmailConfirmed = false;
            await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);
            user.EmailConfirmed = false;
        }

        return user;
    }

    public static async Task<int> SeedQuestAsync(
        WebApplicationFactoryBase factory, int groupId, string title, int dmUserId, DateTime[] proposedDates,
        DateTime? finalizedDate = null, bool isClosed = false, int? originalQuestId = null)
    {
        await using var ctx = factory.Database.CreateContext();
        var quest = new QuestEntity
        {
            Title = title,
            Description = "Board move test quest",
            ChallengeRating = 5,
            DungeonMasterId = dmUserId,
            TotalPlayerCount = 4,
            GroupId = groupId,
            IsFinalized = finalizedDate != null,
            FinalizedDate = finalizedDate,
            IsClosed = isClosed,
            ClosedDate = isClosed ? DateTime.UtcNow : null,
            OriginalQuestId = originalQuestId,
            CreatedAt = DateTime.UtcNow,
            ProposedDates = [.. proposedDates.Select(d => new ProposedDateEntity { Date = d })]
        };
        ctx.Quests.Add(quest);
        await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);
        return quest.Id;
    }

    public static async Task<IReadOnlyList<int>> ProposedDateIdsAsync(WebApplicationFactoryBase factory, int questId)
    {
        await using var ctx = factory.Database.CreateContext();
        return await ctx.Set<ProposedDateEntity>()
            .IgnoreQueryFilters()
            .Where(pd => pd.QuestId == questId)
            .OrderBy(pd => pd.Date)
            .Select(pd => pd.Id)
            .ToListAsync(TestContext.Current.CancellationToken);
    }

    public static async Task<int> SeedCharacterAsync(WebApplicationFactoryBase factory, int ownerId, int groupId, string name)
    {
        await using var ctx = factory.Database.CreateContext();
        var character = new CharacterEntity { Name = name, OwnerId = ownerId, GroupId = groupId, CreatedAt = DateTime.UtcNow };
        ctx.Characters.Add(character);
        await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);
        return character.Id;
    }

    public static async Task<int> SeedSignupAsync(
        WebApplicationFactoryBase factory, int questId, int playerId, SignupRole role, bool isSelected,
        int? characterId, IDictionary<int, VoteType> votes, DateTime signupTime, DateTime? lastVoteChangeTime = null)
    {
        await using var ctx = factory.Database.CreateContext();
        var signup = new PlayerSignupEntity
        {
            QuestId = questId,
            PlayerId = playerId,
            SignupRole = (int)role,
            IsSelected = isSelected,
            CharacterId = characterId,
            SignupTime = signupTime,
            LastVoteChangeTime = lastVoteChangeTime,
            DateVotes = [.. votes.Select(v => new PlayerDateVoteEntity { ProposedDateId = v.Key, Vote = (int)v.Value })]
        };
        ctx.PlayerSignups.Add(signup);
        await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);
        return signup.Id;
    }

    public static async Task<QuestEntity> ReadQuestAsync(WebApplicationFactoryBase factory, int questId)
    {
        await using var ctx = factory.Database.CreateContext();
        return await ctx.Quests
            .IgnoreQueryFilters()
            .AsNoTracking()
            .FirstAsync(q => q.Id == questId, TestContext.Current.CancellationToken);
    }
}
