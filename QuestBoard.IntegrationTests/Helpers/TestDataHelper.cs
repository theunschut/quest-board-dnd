using Microsoft.AspNetCore.Identity;
using QuestBoard.Domain.Enums;

namespace QuestBoard.IntegrationTests.Helpers;

public static class TestDataHelper
{
    public static async Task<QuestEntity> CreateTestQuestAsync(
        IServiceProvider services,
        int dungeonMasterId,
        string title = "Test Quest",
        string description = "Test Description",
        int challengeRating = 5,
        bool isFinalized = false,
        bool dungeonMasterSession = false,
        DateTime? finalizedDate = null,
        bool isClosed = false,
        DateTime? closedDate = null,
        int groupId = 1)
    {
        using var scope = services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<QuestBoardContext>();

        var quest = new QuestEntity
        {
            Title = title,
            Description = description,
            ChallengeRating = challengeRating,
            DungeonMasterId = dungeonMasterId,
            IsFinalized = isFinalized,
            FinalizedDate = finalizedDate,
            DungeonMasterSession = dungeonMasterSession,
            TotalPlayerCount = 4,
            GroupId = groupId,
            CreatedAt = DateTime.UtcNow,
            IsClosed = isClosed,
            ClosedDate = closedDate
        };

        context.Quests.Add(quest);
        await context.SaveChangesAsync();

        return quest;
    }

    public static async Task<PlayerSignupEntity> CreatePlayerSignupAsync(
        IServiceProvider services,
        int questId,
        int playerId,
        int signupRole = 0,
        bool isSelected = false)
    {
        using var scope = services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<QuestBoardContext>();

        var signup = new PlayerSignupEntity
        {
            QuestId = questId,
            PlayerId = playerId,
            SignupRole = signupRole,
            IsSelected = isSelected,
            SignupTime = DateTime.UtcNow
        };

        context.PlayerSignups.Add(signup);
        await context.SaveChangesAsync();

        return signup;
    }

    public static async Task<ProposedDateEntity> CreateProposedDateAsync(
        IServiceProvider services,
        int questId,
        DateTime date)
    {
        using var scope = services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<QuestBoardContext>();

        var proposedDate = new ProposedDateEntity
        {
            QuestId = questId,
            Date = date
        };

        context.Set<ProposedDateEntity>().Add(proposedDate);
        await context.SaveChangesAsync();

        return proposedDate;
    }

    public static async Task<ShopItemEntity> CreateShopItemAsync(
        IServiceProvider services,
        int createdByDmId,
        string name = "Test Item",
        decimal price = 10.0m,
        int quantity = 5,
        ItemRarity rarity = ItemRarity.Common,
        ItemType type = ItemType.Equipment)
    {
        using var scope = services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<QuestBoardContext>();

        var item = new ShopItemEntity
        {
            Name = name,
            Description = "Test item description",
            Price = price,
            Quantity = quantity,
            Type = (int)type,
            Rarity = (int)rarity,
            Status = 1, // Published status (required for items to show in shop)
            CreatedByDmId = createdByDmId,
            GroupId = 1,
            CreatedAt = DateTime.UtcNow
        };

        context.ShopItems.Add(item);
        await context.SaveChangesAsync();

        return item;
    }

    public static async Task<CharacterEntity> CreateTestCharacterAsync(
        IServiceProvider services,
        int ownerId,
        string name = "Test Character",
        int level = 1,
        int status = 0, // Active
        int role = 1, // Backup
        int dndClass = 5) // Fighter
    {
        using var scope = services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<QuestBoardContext>();

        var character = new CharacterEntity
        {
            Name = name,
            OwnerId = ownerId,
            Level = level,
            Status = status,
            Role = role,
            CreatedAt = DateTime.UtcNow
        };

        context.Characters.Add(character);
        await context.SaveChangesAsync();

        // Add a character class (required)
        var characterClass = new CharacterClassEntity
        {
            CharacterId = character.Id,
            Class = dndClass,
            ClassLevel = level
        };

        context.CharacterClasses.Add(characterClass);
        await context.SaveChangesAsync();

        return character;
    }

    public static async Task ClearDatabaseAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<QuestBoardContext>();

        // Drop and recreate the entire database for a clean slate
        // This works for both InMemory and relational databases
        await context.Database.EnsureDeletedAsync();
        await context.Database.EnsureCreatedAsync();

        // Seed the necessary roles after database creation
        await SeedRolesAsync(services);

        // Seed the default EuphoriaInn group so FK constraints are satisfied for quests/shop items
        await SeedDefaultGroupAsync(services);
    }

    public static async Task SeedDefaultGroupAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<QuestBoardContext>();

        if (!context.Groups.Any(g => g.Id == 1))
        {
            context.Groups.Add(new GroupEntity { Id = 1, Name = "EuphoriaInn", CreatedAt = DateTime.UtcNow });
            await context.SaveChangesAsync();
        }
    }

    public static async Task SeedCampaignGroupAsync(IServiceProvider services, int groupId = 2)
    {
        using var scope = services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<QuestBoardContext>();

        if (!context.Groups.Any(g => g.Id == groupId))
        {
            context.Groups.Add(new GroupEntity
            {
                Id = groupId,
                Name = "Campaign Test Group",
                BoardType = (int)BoardType.Campaign,
                CreatedAt = DateTime.UtcNow
            });
            await context.SaveChangesAsync();
        }
    }

    public static async Task SeedRolesAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<int>>>();

        string[] roleNames = ["Admin", "DungeonMaster", "Player", "SuperAdmin"];

        foreach (var roleName in roleNames)
        {
            var roleExist = await roleManager.RoleExistsAsync(roleName);
            if (!roleExist)
            {
                await roleManager.CreateAsync(new IdentityRole<int>(roleName));
            }
        }
    }
}
