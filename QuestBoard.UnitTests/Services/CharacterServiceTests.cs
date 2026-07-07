using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using QuestBoard.Domain.Interfaces;
using QuestBoard.Domain.Models;
using QuestBoard.Domain.Services;
using QuestBoard.Repository;
using QuestBoard.Repository.Entities;

namespace QuestBoard.UnitTests.Services;

public class CharacterServiceTests
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

    // Seeds one group, an owner user, and a single character with both an original and a
    // cropped image already set, so preserve/clear behaviour can be asserted against a
    // known starting state.
    private static async Task SeedCharacterWithImagesAsync(QuestBoardContext context, MutableTestGroupContext groupContext, byte[] originalBytes, byte[] croppedBytes)
    {
        var originalActiveGroupId = groupContext.ActiveGroupId;
        groupContext.ActiveGroupId = null; // see-all during seeding

        context.Groups.Add(new GroupEntity { Id = 1, Name = "Group One" });
        context.UserEntities.Add(new UserEntity { Id = 1, Name = "Owner One", Email = "owner1@test.com" });

        context.Characters.Add(new CharacterEntity
        {
            Id = 1,
            Name = "Test Character",
            OwnerId = 1,
            GroupId = 1,
            Level = 1,
            CreatedAt = DateTime.UtcNow,
            ProfileImage = new CharacterImageEntity { Id = 1, OriginalImageData = originalBytes, CroppedImageData = croppedBytes }
        });

        context.CharacterClasses.Add(new CharacterClassEntity { CharacterId = 1, Class = 5, ClassLevel = 1 });

        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        groupContext.ActiveGroupId = originalActiveGroupId;
    }

    [Fact]
    public async Task UpdateAsync_NoNewUpload_PreservesExistingCroppedImage()
    {
        // Arrange: seed a character with original A + crop A
        var groupContext = new MutableTestGroupContext { ActiveGroupId = null };
        await using var context = CreateContext("CharacterServiceTests." + nameof(UpdateAsync_NoNewUpload_PreservesExistingCroppedImage), groupContext);
        await SeedCharacterWithImagesAsync(context, groupContext, [1, 2, 3], [9, 9, 9]);

        var mapper = CreateMapper();
        var repository = new CharacterRepository(context, mapper);
        var service = new CharacterService(repository, mapper);
        groupContext.ActiveGroupId = 1;

        var character = await repository.GetCharacterWithDetailsAsync(1, TestContext.Current.CancellationToken);
        character.Should().NotBeNull();
        character!.Level = 5; // unrelated-field edit
        character.ProfilePicture = [1, 2, 3]; // round-tripped existing original, never null on a no-photo-change edit

        // Act: no new photo uploaded this request
        await service.UpdateAsync(character, hasNewOriginalUpload: false, TestContext.Current.CancellationToken);

        // Assert: the stored crop survives, only the unrelated field actually changed
        var original = await repository.GetCharacterOriginalPictureAsync(1, TestContext.Current.CancellationToken);
        var cropped = await repository.GetCharacterCroppedPictureAsync(1, TestContext.Current.CancellationToken);
        original.Should().Equal([1, 2, 3]);
        cropped.Should().Equal([9, 9, 9]);

        var updated = await repository.GetCharacterWithDetailsAsync(1, TestContext.Current.CancellationToken);
        updated!.Level.Should().Be(5);
    }

    [Fact]
    public async Task UpdateAsync_NewOriginalUpload_ClearsStaleCroppedImage()
    {
        // Arrange: seed a character with original A + crop A
        var groupContext = new MutableTestGroupContext { ActiveGroupId = null };
        await using var context = CreateContext("CharacterServiceTests." + nameof(UpdateAsync_NewOriginalUpload_ClearsStaleCroppedImage), groupContext);
        await SeedCharacterWithImagesAsync(context, groupContext, [1, 2, 3], [9, 9, 9]);

        var mapper = CreateMapper();
        var repository = new CharacterRepository(context, mapper);
        var service = new CharacterService(repository, mapper);
        groupContext.ActiveGroupId = 1;

        var character = await repository.GetCharacterWithDetailsAsync(1, TestContext.Current.CancellationToken);
        character.Should().NotBeNull();
        character!.ProfilePicture = [70, 71, 72]; // a genuinely new original (B) uploaded this request

        // Act
        await service.UpdateAsync(character, hasNewOriginalUpload: true, TestContext.Current.CancellationToken);

        // Assert: cropped read falls back to B's original, NOT A's stale crop -- proven
        // through the real service call path, not a direct repository call.
        var cropped = await repository.GetCharacterCroppedPictureAsync(1, TestContext.Current.CancellationToken);
        cropped.Should().Equal([70, 71, 72]);
    }

    [Fact]
    public async Task UpdateAsync_NewCropSupplied_PersistsCrop()
    {
        // Arrange: seed a character with original A + crop A
        var groupContext = new MutableTestGroupContext { ActiveGroupId = null };
        await using var context = CreateContext("CharacterServiceTests." + nameof(UpdateAsync_NewCropSupplied_PersistsCrop), groupContext);
        await SeedCharacterWithImagesAsync(context, groupContext, [1, 2, 3], [9, 9, 9]);

        var mapper = CreateMapper();
        var repository = new CharacterRepository(context, mapper);
        var service = new CharacterService(repository, mapper);
        groupContext.ActiveGroupId = 1;

        var character = await repository.GetCharacterWithDetailsAsync(1, TestContext.Current.CancellationToken);
        character.Should().NotBeNull();
        character!.ProfilePicture = [70, 71, 72]; // a genuinely new original (B) uploaded this request

        // Act: caller supplies a real crop of the new original alongside the upload
        await service.UpdateAsync(character, hasNewOriginalUpload: true, newCroppedImageData: [200, 201, 202], TestContext.Current.CancellationToken);

        // Assert: the supplied crop is persisted directly, not cleared and not re-derived
        var cropped = await repository.GetCharacterCroppedPictureAsync(1, TestContext.Current.CancellationToken);
        cropped.Should().Equal([200, 201, 202]);
    }

    [Fact]
    public async Task UpdateAsync_NoNewFile_RefetchesAndPassesThroughExistingCrop()
    {
        // Arrange: seed a character with original A + crop A
        var groupContext = new MutableTestGroupContext { ActiveGroupId = null };
        await using var context = CreateContext("CharacterServiceTests." + nameof(UpdateAsync_NoNewFile_RefetchesAndPassesThroughExistingCrop), groupContext);
        await SeedCharacterWithImagesAsync(context, groupContext, [1, 2, 3], [9, 9, 9]);

        var mapper = CreateMapper();
        var repository = new CharacterRepository(context, mapper);
        var service = new CharacterService(repository, mapper);
        groupContext.ActiveGroupId = 1;

        var character = await repository.GetCharacterWithDetailsAsync(1, TestContext.Current.CancellationToken);
        character.Should().NotBeNull();
        character!.Level = 7; // unrelated-field edit
        character.ProfilePicture = [1, 2, 3]; // round-tripped existing original, never null on a no-photo-change edit

        // Act: no new file, no new crop supplied -- 4-arg overload with null crop
        await service.UpdateAsync(character, hasNewOriginalUpload: false, newCroppedImageData: null, TestContext.Current.CancellationToken);

        // Assert: the previously-stored crop (fetched via GetCharacterCroppedPictureAsync) is
        // passed through unchanged
        var cropped = await repository.GetCharacterCroppedPictureAsync(1, TestContext.Current.CancellationToken);
        cropped.Should().Equal([9, 9, 9]);
    }

    private sealed class MutableTestGroupContext : IActiveGroupContext
    {
        public int? ActiveGroupId { get; set; }
    }
}
