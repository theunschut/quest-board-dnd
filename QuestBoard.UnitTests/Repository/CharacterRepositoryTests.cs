using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using QuestBoard.Domain.Interfaces;
using QuestBoard.Repository;
using QuestBoard.Repository.Entities;

namespace QuestBoard.UnitTests.Repository;

public class CharacterRepositoryTests
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

    // Seeds two groups, an owner user, one character in each group (with class + profile image),
    // while the context's active group is temporarily null so seeding itself isn't filtered out.
    private static async Task SeedTwoGroupCharactersAsync(QuestBoardContext context, MutableTestGroupContext groupContext)
    {
        var originalActiveGroupId = groupContext.ActiveGroupId;
        groupContext.ActiveGroupId = null; // see-all during seeding

        context.Groups.Add(new GroupEntity { Id = 1, Name = "Group One" });
        context.Groups.Add(new GroupEntity { Id = 2, Name = "Group Two" });
        context.UserEntities.Add(new UserEntity { Id = 1, Name = "Owner One", Email = "owner1@test.com" });
        context.UserEntities.Add(new UserEntity { Id = 2, Name = "Owner Two", Email = "owner2@test.com" });

        var characterInGroup1 = new CharacterEntity
        {
            Id = 1,
            Name = "Group One Character",
            OwnerId = 1,
            GroupId = 1,
            Level = 1,
            CreatedAt = DateTime.UtcNow,
            ProfileImage = new CharacterImageEntity { Id = 1, OriginalImageData = [1, 2, 3] }
        };

        var characterInGroup2 = new CharacterEntity
        {
            Id = 2,
            Name = "Group Two Character",
            OwnerId = 2,
            GroupId = 2,
            Level = 1,
            CreatedAt = DateTime.UtcNow,
            ProfileImage = new CharacterImageEntity { Id = 2, OriginalImageData = [4, 5, 6] }
        };

        context.Characters.Add(characterInGroup1);
        context.Characters.Add(characterInGroup2);

        context.CharacterClasses.Add(new CharacterClassEntity { CharacterId = 1, Class = 5, ClassLevel = 1 });
        context.CharacterClasses.Add(new CharacterClassEntity { CharacterId = 2, Class = 5, ClassLevel = 1 });

        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        groupContext.ActiveGroupId = originalActiveGroupId;
    }

    [Fact]
    public async Task GetAllCharactersWithDetailsAsync_ActiveGroupOne_ExcludesGroupTwoCharacter()
    {
        // Arrange
        var groupContext = new MutableTestGroupContext { ActiveGroupId = null };
        await using var context = CreateContext("CharacterRepositoryTests." + nameof(GetAllCharactersWithDetailsAsync_ActiveGroupOne_ExcludesGroupTwoCharacter), groupContext);
        await SeedTwoGroupCharactersAsync(context, groupContext);

        var repository = new CharacterRepository(context, CreateMapper());
        groupContext.ActiveGroupId = 1;

        // Act
        var characters = await repository.GetAllCharactersWithDetailsAsync(TestContext.Current.CancellationToken);

        // Assert
        characters.Should().ContainSingle();
        characters[0].Name.Should().Be("Group One Character");
    }

    [Fact]
    public async Task GetAllCharactersWithDetailsAsync_NoActiveGroup_ReturnsEmpty()
    {
        // Arrange: SuperAdmin-empty behavior — no cross-group superview for Characters
        var groupContext = new MutableTestGroupContext { ActiveGroupId = null };
        await using var context = CreateContext("CharacterRepositoryTests." + nameof(GetAllCharactersWithDetailsAsync_NoActiveGroup_ReturnsEmpty), groupContext);
        await SeedTwoGroupCharactersAsync(context, groupContext);

        var repository = new CharacterRepository(context, CreateMapper());
        groupContext.ActiveGroupId = null;

        // Act
        var characters = await repository.GetAllCharactersWithDetailsAsync(TestContext.Current.CancellationToken);

        // Assert
        characters.Should().BeEmpty();
    }

    [Fact]
    public async Task GetCharacterWithDetailsAsync_ForCharacterInDifferentGroup_ReturnsNull()
    {
        // Arrange
        var groupContext = new MutableTestGroupContext { ActiveGroupId = null };
        await using var context = CreateContext("CharacterRepositoryTests." + nameof(GetCharacterWithDetailsAsync_ForCharacterInDifferentGroup_ReturnsNull), groupContext);
        await SeedTwoGroupCharactersAsync(context, groupContext);

        var repository = new CharacterRepository(context, CreateMapper());
        groupContext.ActiveGroupId = 1;

        // Act: character Id=2 belongs to group 2, viewer's active group is 1
        var character = await repository.GetCharacterWithDetailsAsync(2, TestContext.Current.CancellationToken);

        // Assert
        character.Should().BeNull();
    }

    [Fact]
    public async Task GetCharacterOriginalPictureAsync_ForCharacterInDifferentGroup_ReturnsNull()
    {
        // Arrange
        var groupContext = new MutableTestGroupContext { ActiveGroupId = null };
        await using var context = CreateContext("CharacterRepositoryTests." + nameof(GetCharacterOriginalPictureAsync_ForCharacterInDifferentGroup_ReturnsNull), groupContext);
        await SeedTwoGroupCharactersAsync(context, groupContext);

        var repository = new CharacterRepository(context, CreateMapper());
        groupContext.ActiveGroupId = 1;

        // Act: character Id=2 belongs to group 2, viewer's active group is 1
        var picture = await repository.GetCharacterOriginalPictureAsync(2, TestContext.Current.CancellationToken);

        // Assert: proves the rewritten query (rooted at Characters, not CharacterImages) respects the filter
        picture.Should().BeNull();
    }

    [Fact]
    public async Task GetCharacterOriginalPictureAsync_ForCharacterInActiveGroup_ReturnsImageData()
    {
        // Arrange
        var groupContext = new MutableTestGroupContext { ActiveGroupId = null };
        await using var context = CreateContext("CharacterRepositoryTests." + nameof(GetCharacterOriginalPictureAsync_ForCharacterInActiveGroup_ReturnsImageData), groupContext);
        await SeedTwoGroupCharactersAsync(context, groupContext);

        var repository = new CharacterRepository(context, CreateMapper());
        groupContext.ActiveGroupId = 1;

        // Act
        var picture = await repository.GetCharacterOriginalPictureAsync(1, TestContext.Current.CancellationToken);

        // Assert
        picture.Should().NotBeNull();
        picture.Should().Equal([1, 2, 3]);
    }

    [Fact]
    public async Task UpdateProfileImageAsync_SetsOriginalImageData()
    {
        // Arrange
        var groupContext = new MutableTestGroupContext { ActiveGroupId = null };
        await using var context = CreateContext("CharacterRepositoryTests." + nameof(UpdateProfileImageAsync_SetsOriginalImageData), groupContext);
        await SeedTwoGroupCharactersAsync(context, groupContext);

        var repository = new CharacterRepository(context, CreateMapper());
        groupContext.ActiveGroupId = 1;

        // Act: upsert a brand-new original with no crop
        await repository.UpdateProfileImageAsync(1, [10, 11, 12], null, TestContext.Current.CancellationToken);
        var original = await repository.GetCharacterOriginalPictureAsync(1, TestContext.Current.CancellationToken);

        // Assert
        original.Should().NotBeNull();
        original.Should().Equal([10, 11, 12]);
    }

    [Fact]
    public async Task GetCharacterCroppedPictureAsync_FallsBackToOriginal_WhenCroppedIsNull()
    {
        // Arrange: seeded character has only OriginalImageData set, CroppedImageData is null
        var groupContext = new MutableTestGroupContext { ActiveGroupId = null };
        await using var context = CreateContext("CharacterRepositoryTests." + nameof(GetCharacterCroppedPictureAsync_FallsBackToOriginal_WhenCroppedIsNull), groupContext);
        await SeedTwoGroupCharactersAsync(context, groupContext);

        var repository = new CharacterRepository(context, CreateMapper());
        groupContext.ActiveGroupId = 1;

        // Act
        var cropped = await repository.GetCharacterCroppedPictureAsync(1, TestContext.Current.CancellationToken);

        // Assert: falls back to the original bytes since no crop was ever saved
        cropped.Should().NotBeNull();
        cropped.Should().Equal([1, 2, 3]);
    }

    [Fact]
    public async Task GetCharacterOriginalAndCroppedPictureAsync_ReturnDistinctValues()
    {
        // Arrange
        var groupContext = new MutableTestGroupContext { ActiveGroupId = null };
        await using var context = CreateContext("CharacterRepositoryTests." + nameof(GetCharacterOriginalAndCroppedPictureAsync_ReturnDistinctValues), groupContext);
        await SeedTwoGroupCharactersAsync(context, groupContext);

        var repository = new CharacterRepository(context, CreateMapper());
        groupContext.ActiveGroupId = 1;

        // Act: set both an original and a distinct crop
        await repository.UpdateProfileImageAsync(1, [20, 21, 22], [30, 31, 32], TestContext.Current.CancellationToken);
        var original = await repository.GetCharacterOriginalPictureAsync(1, TestContext.Current.CancellationToken);
        var cropped = await repository.GetCharacterCroppedPictureAsync(1, TestContext.Current.CancellationToken);

        // Assert: original and cropped are independently retrievable and distinct
        original.Should().Equal([20, 21, 22]);
        cropped.Should().Equal([30, 31, 32]);
    }

    [Fact]
    public async Task UpdateProfileImageAsync_ReplacesBothColumnsAtomically()
    {
        // Arrange: seed with an original+crop already set
        var groupContext = new MutableTestGroupContext { ActiveGroupId = null };
        await using var context = CreateContext("CharacterRepositoryTests." + nameof(UpdateProfileImageAsync_ReplacesBothColumnsAtomically), groupContext);
        await SeedTwoGroupCharactersAsync(context, groupContext);

        var repository = new CharacterRepository(context, CreateMapper());
        groupContext.ActiveGroupId = 1;
        await repository.UpdateProfileImageAsync(1, [1, 1, 1], [2, 2, 2], TestContext.Current.CancellationToken);

        // Act: re-upload with a brand-new original+crop pair
        await repository.UpdateProfileImageAsync(1, [40, 41, 42], [50, 51, 52], TestContext.Current.CancellationToken);
        var original = await repository.GetCharacterOriginalPictureAsync(1, TestContext.Current.CancellationToken);
        var cropped = await repository.GetCharacterCroppedPictureAsync(1, TestContext.Current.CancellationToken);

        // Assert: both columns fully replaced, no trace of either prior upload
        original.Should().Equal([40, 41, 42]);
        cropped.Should().Equal([50, 51, 52]);
    }

    [Fact]
    public async Task UpdateProfileImageAsync_NewOriginalWithoutCrop_ClearsStaleCropped()
    {
        // Arrange: seed with an original+crop already set (upload A)
        var groupContext = new MutableTestGroupContext { ActiveGroupId = null };
        await using var context = CreateContext("CharacterRepositoryTests." + nameof(UpdateProfileImageAsync_NewOriginalWithoutCrop_ClearsStaleCropped), groupContext);
        await SeedTwoGroupCharactersAsync(context, groupContext);

        var repository = new CharacterRepository(context, CreateMapper());
        groupContext.ActiveGroupId = 1;
        await repository.UpdateProfileImageAsync(1, [1, 1, 1], [2, 2, 2], TestContext.Current.CancellationToken);

        // Act: re-upload a new original only (upload B), crop param null (Pitfall 5)
        await repository.UpdateProfileImageAsync(1, [60, 61, 62], null, TestContext.Current.CancellationToken);
        var cropped = await repository.GetCharacterCroppedPictureAsync(1, TestContext.Current.CancellationToken);

        // Assert: cropped-read falls back to B's original, NOT A's stale crop
        cropped.Should().Equal([60, 61, 62]);
    }

    [Fact]
    public async Task GetAllCharactersWithDetailsAsync_ReflectsHasProfilePicture_TrueWithImage_FalseWithout()
    {
        // Arrange: two characters in the same group, one with a stored image and one without.
        var groupContext = new MutableTestGroupContext { ActiveGroupId = null };
        await using var context = CreateContext("CharacterRepositoryTests." + nameof(GetAllCharactersWithDetailsAsync_ReflectsHasProfilePicture_TrueWithImage_FalseWithout), groupContext);

        context.Groups.Add(new GroupEntity { Id = 1, Name = "Group One" });
        context.UserEntities.Add(new UserEntity { Id = 1, Name = "Owner One", Email = "owner1@test.com" });
        context.Characters.Add(new CharacterEntity
        {
            Id = 1,
            Name = "Has Picture",
            OwnerId = 1,
            GroupId = 1,
            Level = 1,
            CreatedAt = DateTime.UtcNow,
            ProfileImage = new CharacterImageEntity { Id = 1, OriginalImageData = [1, 2, 3] }
        });
        context.Characters.Add(new CharacterEntity
        {
            Id = 2,
            Name = "No Picture",
            OwnerId = 1,
            GroupId = 1,
            Level = 1,
            CreatedAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var repository = new CharacterRepository(context, CreateMapper());
        groupContext.ActiveGroupId = 1;

        // Act
        var characters = await repository.GetAllCharactersWithDetailsAsync(TestContext.Current.CancellationToken);

        // Assert
        characters.Single(c => c.Name == "Has Picture").HasProfilePicture.Should().BeTrue();
        characters.Single(c => c.Name == "No Picture").HasProfilePicture.Should().BeFalse();
    }

    [Fact]
    public async Task GetCharacterWithDetailsAsync_ReflectsHasProfilePicture_TrueWithImage()
    {
        // Arrange
        var groupContext = new MutableTestGroupContext { ActiveGroupId = null };
        await using var context = CreateContext("CharacterRepositoryTests." + nameof(GetCharacterWithDetailsAsync_ReflectsHasProfilePicture_TrueWithImage), groupContext);
        await SeedTwoGroupCharactersAsync(context, groupContext);

        var repository = new CharacterRepository(context, CreateMapper());
        groupContext.ActiveGroupId = 1;

        // Act
        var character = await repository.GetCharacterWithDetailsAsync(1, TestContext.Current.CancellationToken);

        // Assert
        character.Should().NotBeNull();
        character!.HasProfilePicture.Should().BeTrue();
    }

    private sealed class MutableTestGroupContext : IActiveGroupContext
    {
        public int? ActiveGroupId { get; set; }
    }
}
