using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using QuestBoard.Domain.Interfaces;
using QuestBoard.Domain.Services;
using QuestBoard.Repository;
using QuestBoard.Repository.Entities;

namespace QuestBoard.UnitTests.Services;

public class DungeonMasterProfileServiceTests
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

    // Seeds a single DM profile with both an original and a cropped image already set, so
    // preserve/clear behaviour can be asserted against a known starting state.
    private static async Task SeedDungeonMasterProfileWithImagesAsync(QuestBoardContext context, int userId, byte[] originalBytes, byte[] croppedBytes)
    {
        context.UserEntities.Add(new UserEntity { Id = userId, Name = "DM One", Email = $"dm{userId}@test.com" });

        context.DungeonMasterProfiles.Add(new DungeonMasterProfileEntity
        {
            Id = userId,
            Bio = "Original bio.",
            ProfileImage = new DungeonMasterProfileImageEntity { Id = userId, OriginalImageData = originalBytes, CroppedImageData = croppedBytes }
        });

        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task UpsertProfileAsync_BioOnlyEdit_PreservesExistingCroppedImage()
    {
        // Arrange: seed a DM profile with original A + crop A
        var groupContext = new MutableTestGroupContext { ActiveGroupId = 1 };
        await using var context = CreateContext(nameof(UpsertProfileAsync_BioOnlyEdit_PreservesExistingCroppedImage), groupContext);
        await SeedDungeonMasterProfileWithImagesAsync(context, 1, [1, 2, 3], [9, 9, 9]);

        var mapper = CreateMapper();
        var repository = new DungeonMasterProfileRepository(context, mapper);
        var service = new DungeonMasterProfileService(repository, mapper);

        // Act: bio-only edit -- no new image bytes, no removal requested
        await service.UpsertProfileAsync(1, "Updated bio.", imageBytes: null, removeImage: false, TestContext.Current.CancellationToken);

        // Assert: the stored crop survives, only the bio actually changed
        var original = await repository.GetOriginalPictureAsync(1, TestContext.Current.CancellationToken);
        var cropped = await repository.GetCroppedPictureAsync(1, TestContext.Current.CancellationToken);
        original.Should().Equal([1, 2, 3]);
        cropped.Should().Equal([9, 9, 9]);

        var updated = await repository.GetProfileByUserIdAsync(1, TestContext.Current.CancellationToken);
        updated!.Bio.Should().Be("Updated bio.");
    }

    [Fact]
    public async Task UpsertProfileAsync_NewImageUpload_ClearsStaleCroppedImage()
    {
        // Arrange: seed a DM profile with original A + crop A
        var groupContext = new MutableTestGroupContext { ActiveGroupId = 1 };
        await using var context = CreateContext(nameof(UpsertProfileAsync_NewImageUpload_ClearsStaleCroppedImage), groupContext);
        await SeedDungeonMasterProfileWithImagesAsync(context, 1, [1, 2, 3], [9, 9, 9]);

        var mapper = CreateMapper();
        var repository = new DungeonMasterProfileRepository(context, mapper);
        var service = new DungeonMasterProfileService(repository, mapper);

        // Act: a genuinely new original (B) uploaded this request
        await service.UpsertProfileAsync(1, "Original bio.", imageBytes: [70, 71, 72], removeImage: false, TestContext.Current.CancellationToken);

        // Assert: cropped read falls back to B's original, NOT A's stale crop -- proven
        // through the real service call path, not a direct repository call.
        var cropped = await repository.GetCroppedPictureAsync(1, TestContext.Current.CancellationToken);
        cropped.Should().Equal([70, 71, 72]);
    }

    private sealed class MutableTestGroupContext : IActiveGroupContext
    {
        public int? ActiveGroupId { get; set; }
    }
}
