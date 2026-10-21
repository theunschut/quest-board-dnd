using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using QuestBoard.Domain.Enums;
using QuestBoard.Domain.Interfaces;
using QuestBoard.Domain.Models;
using QuestBoard.Repository;
using QuestBoard.Repository.Entities;
using QuestBoard.UnitTests.Helpers;

namespace QuestBoard.UnitTests.Repository;

/// <summary>
/// Pins the stored contract for a board's page settings: the two int columns map to and from the
/// domain Group, and saving settings through the repository never touches the board's link.
/// </summary>
public class BoardPageSettingsMappingTests
{
    private static QuestBoardContext CreateContext(string databaseName)
    {
        var options = new DbContextOptionsBuilder<QuestBoardContext>()
            .UseInMemoryDatabase(databaseName)
            .Options;

        return new QuestBoardContext(options, new SeeAllGroupContext());
    }

    private static IMapper CreateMapper()
    {
        var configuration = new MapperConfiguration(cfg => cfg.AddProfile<QuestBoard.Repository.Automapper.EntityProfile>(), NullLoggerFactory.Instance);
        return configuration.CreateMapper();
    }

    private static GroupRepository CreateRepository(QuestBoardContext context) =>
        new(context, CreateMapper(), new FakeBoardClock { Today = DateOnly.FromDateTime(DateTime.Today) });

    [Fact]
    public void Map_GroupEntityWithSettings_ProducesGroupWithSettings()
    {
        var entity = new GroupEntity
        {
            Id = 1,
            Name = "Board",
            DisabledPages = BoardPageSet.Empty.With(BoardPage.Shop).Mask,
            HomePage = (int)BoardPage.Calendar
        };

        var group = CreateMapper().Map<Group>(entity);

        group.DisabledPages.Contains(BoardPage.Shop).Should().BeTrue();
        group.DisabledPages.Contains(BoardPage.Calendar).Should().BeFalse();
        group.HomePage.Should().Be(BoardPage.Calendar);
    }

    [Fact]
    public void Map_GroupEntityWithNoSettings_ProducesEmptySetAndTheQuestList()
    {
        var entity = new GroupEntity { Id = 1, Name = "Board" };

        var group = CreateMapper().Map<Group>(entity);

        group.DisabledPages.Should().Be(BoardPageSet.Empty);
        group.HomePage.Should().Be(BoardPage.Quests);
    }

    [Fact]
    public void Map_GroupWithSettings_ProducesEntityWithBothInts()
    {
        var group = new Group
        {
            Id = 1,
            Name = "Board",
            DisabledPages = BoardPageSet.Empty.With(BoardPage.Events).With(BoardPage.Contacts),
            HomePage = BoardPage.Characters
        };

        var entity = CreateMapper().Map<GroupEntity>(group);

        entity.DisabledPages.Should().Be((1 << (int)BoardPage.Events) | (1 << (int)BoardPage.Contacts));
        entity.HomePage.Should().Be((int)BoardPage.Characters);
    }

    [Fact]
    public async Task UpdateAsync_WritesBothSettingsAndLeavesTheLinkedBoardSetAtItsSeededValue()
    {
        var dbName = nameof(UpdateAsync_WritesBothSettingsAndLeavesTheLinkedBoardSetAtItsSeededValue);
        var ct = TestContext.Current.CancellationToken;

        await using (var seed = CreateContext(dbName))
        {
            seed.LinkedBoardSets.Add(new LinkedBoardSetEntity { Id = 7, Name = "Set", BoardType = (int)BoardType.OneShot });
            seed.Groups.Add(new GroupEntity
            {
                Id = 1,
                Name = "Board",
                BoardType = (int)BoardType.OneShot,
                LinkedBoardSetId = 7
            });
            await seed.SaveChangesAsync(ct);
        }

        await using (var context = CreateContext(dbName))
        {
            var repository = CreateRepository(context);
            var group = await repository.GetByIdAsync(1, ct);
            group.Should().NotBeNull();

            group!.DisabledPages = BoardPageSet.Empty.With(BoardPage.Shop).With(BoardPage.Players);
            group.HomePage = BoardPage.Calendar;
            await repository.UpdateAsync(group, ct);
        }

        await using (var verify = CreateContext(dbName))
        {
            var row = await verify.Groups.SingleAsync(g => g.Id == 1, ct);

            row.DisabledPages.Should().Be((1 << (int)BoardPage.Shop) | (1 << (int)BoardPage.Players));
            row.HomePage.Should().Be((int)BoardPage.Calendar);
            row.LinkedBoardSetId.Should().Be(7);
        }
    }

    private sealed class SeeAllGroupContext : IActiveGroupContext
    {
        public int? ActiveGroupId => null;
    }
}
