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
            ProfileImage = new CharacterImageEntity { Id = 1, ImageData = [1, 2, 3] }
        };

        var characterInGroup2 = new CharacterEntity
        {
            Id = 2,
            Name = "Group Two Character",
            OwnerId = 2,
            GroupId = 2,
            Level = 1,
            CreatedAt = DateTime.UtcNow,
            ProfileImage = new CharacterImageEntity { Id = 2, ImageData = [4, 5, 6] }
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
        await using var context = CreateContext(nameof(GetAllCharactersWithDetailsAsync_ActiveGroupOne_ExcludesGroupTwoCharacter), groupContext);
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
        await using var context = CreateContext(nameof(GetAllCharactersWithDetailsAsync_NoActiveGroup_ReturnsEmpty), groupContext);
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
        await using var context = CreateContext(nameof(GetCharacterWithDetailsAsync_ForCharacterInDifferentGroup_ReturnsNull), groupContext);
        await SeedTwoGroupCharactersAsync(context, groupContext);

        var repository = new CharacterRepository(context, CreateMapper());
        groupContext.ActiveGroupId = 1;

        // Act: character Id=2 belongs to group 2, viewer's active group is 1
        var character = await repository.GetCharacterWithDetailsAsync(2, TestContext.Current.CancellationToken);

        // Assert
        character.Should().BeNull();
    }

    [Fact]
    public async Task GetCharacterProfilePictureAsync_ForCharacterInDifferentGroup_ReturnsNull()
    {
        // Arrange
        var groupContext = new MutableTestGroupContext { ActiveGroupId = null };
        await using var context = CreateContext(nameof(GetCharacterProfilePictureAsync_ForCharacterInDifferentGroup_ReturnsNull), groupContext);
        await SeedTwoGroupCharactersAsync(context, groupContext);

        var repository = new CharacterRepository(context, CreateMapper());
        groupContext.ActiveGroupId = 1;

        // Act: character Id=2 belongs to group 2, viewer's active group is 1
        var picture = await repository.GetCharacterProfilePictureAsync(2, TestContext.Current.CancellationToken);

        // Assert: proves the rewritten query (rooted at Characters, not CharacterImages) respects the filter
        picture.Should().BeNull();
    }

    [Fact]
    public async Task GetCharacterProfilePictureAsync_ForCharacterInActiveGroup_ReturnsImageData()
    {
        // Arrange
        var groupContext = new MutableTestGroupContext { ActiveGroupId = null };
        await using var context = CreateContext(nameof(GetCharacterProfilePictureAsync_ForCharacterInActiveGroup_ReturnsImageData), groupContext);
        await SeedTwoGroupCharactersAsync(context, groupContext);

        var repository = new CharacterRepository(context, CreateMapper());
        groupContext.ActiveGroupId = 1;

        // Act
        var picture = await repository.GetCharacterProfilePictureAsync(1, TestContext.Current.CancellationToken);

        // Assert
        picture.Should().NotBeNull();
        picture.Should().Equal([1, 2, 3]);
    }

    private sealed class MutableTestGroupContext : IActiveGroupContext
    {
        public int? ActiveGroupId { get; set; }
    }
}
