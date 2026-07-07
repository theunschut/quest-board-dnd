using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using QuestBoard.Domain.Interfaces;
using QuestBoard.Repository;
using QuestBoard.Repository.Entities;

namespace QuestBoard.UnitTests.Repository;

public class DungeonMasterProfileRepositoryTests
{
    private static QuestBoardContext CreateContext(string databaseName, MutableTestGroupContext groupContext)
    {
        var options = new DbContextOptionsBuilder<QuestBoardContext>()
            .UseInMemoryDatabase(databaseName)
            .Options;

        return new QuestBoardContext(options, groupContext);
    }

    private static IMapper CreateMapper()
    {
        var configuration = new MapperConfiguration(cfg => cfg.AddProfile<QuestBoard.Repository.Automapper.EntityProfile>(), NullLoggerFactory.Instance);
        return configuration.CreateMapper();
    }

    // DM profiles have no group-scoping query filter, so a single-profile seed is sufficient --
    // no two-group split needed the way Character/Contact tests require.
    private static async Task SeedDungeonMasterProfileAsync(QuestBoardContext context, int userId, byte[] originalBytes)
    {
        context.UserEntities.Add(new UserEntity { Id = userId, Name = "DM One", Email = $"dm{userId}@test.com" });

        context.DungeonMasterProfiles.Add(new DungeonMasterProfileEntity
        {
            Id = userId,
            Bio = "An experienced Dungeon Master.",
            ProfileImage = new DungeonMasterProfileImageEntity { Id = userId, OriginalImageData = originalBytes }
        });

        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task GetOriginalPictureAsync_ForExistingProfile_ReturnsImageData()
    {
        // Arrange
        var groupContext = new MutableTestGroupContext { ActiveGroupId = 1 };
        await using var context = CreateContext(nameof(GetOriginalPictureAsync_ForExistingProfile_ReturnsImageData), groupContext);
        await SeedDungeonMasterProfileAsync(context, 1, [1, 2, 3]);

        var repository = new DungeonMasterProfileRepository(context, CreateMapper());

        // Act
        var picture = await repository.GetOriginalPictureAsync(1, TestContext.Current.CancellationToken);

        // Assert
        picture.Should().NotBeNull();
        picture.Should().Equal([1, 2, 3]);
    }

    [Fact]
    public async Task UpsertProfileImageAsync_SetsOriginalImageData()
    {
        // Arrange
        var groupContext = new MutableTestGroupContext { ActiveGroupId = 1 };
        await using var context = CreateContext(nameof(UpsertProfileImageAsync_SetsOriginalImageData), groupContext);
        await SeedDungeonMasterProfileAsync(context, 1, [1, 2, 3]);

        var repository = new DungeonMasterProfileRepository(context, CreateMapper());

        // Act
        await repository.UpsertProfileImageAsync(1, [10, 11, 12], null, TestContext.Current.CancellationToken);
        var original = await repository.GetOriginalPictureAsync(1, TestContext.Current.CancellationToken);

        // Assert
        original.Should().NotBeNull();
        original.Should().Equal([10, 11, 12]);
    }

    [Fact]
    public async Task GetCroppedPictureAsync_FallsBackToOriginal_WhenCroppedIsNull()
    {
        // Arrange: seeded profile has only OriginalImageData set, CroppedImageData is null
        var groupContext = new MutableTestGroupContext { ActiveGroupId = 1 };
        await using var context = CreateContext(nameof(GetCroppedPictureAsync_FallsBackToOriginal_WhenCroppedIsNull), groupContext);
        await SeedDungeonMasterProfileAsync(context, 1, [1, 2, 3]);

        var repository = new DungeonMasterProfileRepository(context, CreateMapper());

        // Act
        var cropped = await repository.GetCroppedPictureAsync(1, TestContext.Current.CancellationToken);

        // Assert: falls back to the original bytes since no crop was ever saved
        cropped.Should().NotBeNull();
        cropped.Should().Equal([1, 2, 3]);
    }

    [Fact]
    public async Task GetOriginalAndCroppedPictureAsync_ReturnDistinctValues()
    {
        // Arrange
        var groupContext = new MutableTestGroupContext { ActiveGroupId = 1 };
        await using var context = CreateContext(nameof(GetOriginalAndCroppedPictureAsync_ReturnDistinctValues), groupContext);
        await SeedDungeonMasterProfileAsync(context, 1, [1, 2, 3]);

        var repository = new DungeonMasterProfileRepository(context, CreateMapper());

        // Act: set both an original and a distinct crop
        await repository.UpsertProfileImageAsync(1, [20, 21, 22], [30, 31, 32], TestContext.Current.CancellationToken);
        var original = await repository.GetOriginalPictureAsync(1, TestContext.Current.CancellationToken);
        var cropped = await repository.GetCroppedPictureAsync(1, TestContext.Current.CancellationToken);

        // Assert: original and cropped are independently retrievable and distinct
        original.Should().Equal([20, 21, 22]);
        cropped.Should().Equal([30, 31, 32]);
    }

    [Fact]
    public async Task UpsertProfileImageAsync_ReplacesBothColumnsAtomically()
    {
        // Arrange: seed with an original+crop already set
        var groupContext = new MutableTestGroupContext { ActiveGroupId = 1 };
        await using var context = CreateContext(nameof(UpsertProfileImageAsync_ReplacesBothColumnsAtomically), groupContext);
        await SeedDungeonMasterProfileAsync(context, 1, [1, 2, 3]);

        var repository = new DungeonMasterProfileRepository(context, CreateMapper());
        await repository.UpsertProfileImageAsync(1, [1, 1, 1], [2, 2, 2], TestContext.Current.CancellationToken);

        // Act: re-upload with a brand-new original+crop pair
        await repository.UpsertProfileImageAsync(1, [40, 41, 42], [50, 51, 52], TestContext.Current.CancellationToken);
        var original = await repository.GetOriginalPictureAsync(1, TestContext.Current.CancellationToken);
        var cropped = await repository.GetCroppedPictureAsync(1, TestContext.Current.CancellationToken);

        // Assert: both columns fully replaced, no trace of either prior upload
        original.Should().Equal([40, 41, 42]);
        cropped.Should().Equal([50, 51, 52]);
    }

    [Fact]
    public async Task UpsertProfileImageAsync_NewOriginalWithoutCrop_ClearsStaleCropped()
    {
        // Arrange: seed with an original+crop already set (upload A)
        var groupContext = new MutableTestGroupContext { ActiveGroupId = 1 };
        await using var context = CreateContext(nameof(UpsertProfileImageAsync_NewOriginalWithoutCrop_ClearsStaleCropped), groupContext);
        await SeedDungeonMasterProfileAsync(context, 1, [1, 2, 3]);

        var repository = new DungeonMasterProfileRepository(context, CreateMapper());
        await repository.UpsertProfileImageAsync(1, [1, 1, 1], [2, 2, 2], TestContext.Current.CancellationToken);

        // Act: re-upload a new original only (upload B), crop param null (Pitfall 5)
        await repository.UpsertProfileImageAsync(1, [60, 61, 62], null, TestContext.Current.CancellationToken);
        var cropped = await repository.GetCroppedPictureAsync(1, TestContext.Current.CancellationToken);

        // Assert: cropped-read falls back to B's original, NOT A's stale crop
        cropped.Should().Equal([60, 61, 62]);
    }

    private sealed class MutableTestGroupContext : IActiveGroupContext
    {
        public int? ActiveGroupId { get; set; }
    }
}
