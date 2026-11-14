using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using QuestBoard.Domain.Interfaces;
using QuestBoard.Repository;
using QuestBoard.Repository.Entities;
using QuestBoard.UnitTests.Helpers;

namespace QuestBoard.UnitTests.Repository;

// The quest page loaders leave the portrait bytes out on purpose; the pages still need to know
// whether each signup character has a picture, so the repository reports a flag from the stored image.
public class QuestRepositoryPictureFlagTests
{
    private const int BoardId = 1;

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private sealed class MutableGroupContext : IActiveGroupContext
    {
        public int? ActiveGroupId { get; set; } = BoardId;
    }

    private static DbContextOptions<QuestBoardContext> NewOptions() =>
        new DbContextOptionsBuilder<QuestBoardContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

    private static IMapper CreateMapper() =>
        new MapperConfiguration(cfg => cfg.AddProfile<QuestBoard.Repository.Automapper.EntityProfile>(), NullLoggerFactory.Instance)
            .CreateMapper();

    private static QuestRepository CreateRepository(QuestBoardContext context) =>
        new(context, CreateMapper(), new FakeBoardClock());

    private static async Task<int> SeedQuestAsync(DbContextOptions<QuestBoardContext> options, bool withSignups)
    {
        await using var context = new QuestBoardContext(options, new MutableGroupContext());
        context.Groups.Add(new GroupEntity { Id = BoardId, Name = "Alpha" });
        context.UserEntities.Add(new UserEntity { Id = 1, Name = "Dungeon Master", Email = "dm@test.com", UserName = "dm" });
        context.UserEntities.Add(new UserEntity { Id = 2, Name = "Player One", Email = "p1@test.com", UserName = "p1" });
        context.UserEntities.Add(new UserEntity { Id = 3, Name = "Player Two", Email = "p2@test.com", UserName = "p2" });
        context.Characters.Add(new CharacterEntity
        {
            Id = 11,
            Name = "Has Picture",
            OwnerId = 2,
            GroupId = BoardId,
            Level = 1,
            CreatedAt = DateTime.UtcNow,
            ProfileImage = new CharacterImageEntity { Id = 1, OriginalImageData = [1, 2, 3] }
        });
        context.Characters.Add(new CharacterEntity
        {
            Id = 12,
            Name = "No Picture",
            OwnerId = 3,
            GroupId = BoardId,
            Level = 1,
            CreatedAt = DateTime.UtcNow
        });

        var quest = new QuestEntity
        {
            Title = "Flag Quest",
            Description = "d",
            GroupId = BoardId,
            DungeonMasterId = 1
        };
        if (withSignups)
        {
            quest.PlayerSignups.Add(new PlayerSignupEntity { PlayerId = 2, CharacterId = 11 });
            quest.PlayerSignups.Add(new PlayerSignupEntity { PlayerId = 3, CharacterId = 12 });
        }

        context.Quests.Add(quest);
        await context.SaveChangesAsync(Token);
        return quest.Id;
    }

    [Fact]
    public async Task GetQuestWithDetailsAsync_FlagsOnlyTheSignupCharacterThatHasAStoredImage()
    {
        var options = NewOptions();
        var questId = await SeedQuestAsync(options, withSignups: true);

        await using var context = new QuestBoardContext(options, new MutableGroupContext());
        var quest = await CreateRepository(context).GetQuestWithDetailsAsync(questId, Token);

        quest.Should().NotBeNull();
        var characters = quest!.PlayerSignups.Select(s => s.Character!).ToList();
        characters.Single(c => c.Name == "Has Picture").HasProfilePicture.Should().BeTrue();
        characters.Single(c => c.Name == "No Picture").HasProfilePicture.Should().BeFalse();
        characters.Should().OnlyContain(c => c.ProfilePicture == null || c.ProfilePicture.Length == 0);
    }

    [Fact]
    public async Task GetQuestWithManageViewDetailsAsync_FlagsOnlyTheSignupCharacterThatHasAStoredImage()
    {
        var options = NewOptions();
        var questId = await SeedQuestAsync(options, withSignups: true);

        await using var context = new QuestBoardContext(options, new MutableGroupContext());
        var quest = await CreateRepository(context).GetQuestWithManageViewDetailsAsync(questId, Token);

        quest.Should().NotBeNull();
        var characters = quest!.PlayerSignups.Select(s => s.Character!).ToList();
        characters.Single(c => c.Name == "Has Picture").HasProfilePicture.Should().BeTrue();
        characters.Single(c => c.Name == "No Picture").HasProfilePicture.Should().BeFalse();
        characters.Should().OnlyContain(c => c.ProfilePicture == null || c.ProfilePicture.Length == 0);
    }

    [Fact]
    public async Task BothLoaders_MapAQuestWithoutSignups()
    {
        var options = NewOptions();
        var questId = await SeedQuestAsync(options, withSignups: false);

        await using var context = new QuestBoardContext(options, new MutableGroupContext());
        var repository = CreateRepository(context);

        var details = await repository.GetQuestWithDetailsAsync(questId, Token);
        var manage = await repository.GetQuestWithManageViewDetailsAsync(questId, Token);

        details!.PlayerSignups.Should().BeEmpty();
        manage!.PlayerSignups.Should().BeEmpty();
    }

    [Fact]
    public async Task BothLoaders_AnswerNullForAMissingQuest()
    {
        var options = NewOptions();
        await SeedQuestAsync(options, withSignups: true);

        await using var context = new QuestBoardContext(options, new MutableGroupContext());
        var repository = CreateRepository(context);

        (await repository.GetQuestWithDetailsAsync(9999, Token)).Should().BeNull();
        (await repository.GetQuestWithManageViewDetailsAsync(9999, Token)).Should().BeNull();
    }
}
