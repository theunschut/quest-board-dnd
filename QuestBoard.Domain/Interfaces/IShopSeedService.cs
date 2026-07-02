namespace QuestBoard.Domain.Interfaces;

public interface IShopSeedService
{
    /// <summary>
    /// Seeds the given group's shop with a fixed set of basic equipment and magic items, attributed to the given DM.
    /// No-ops if the group already has any published item.
    /// </summary>
    Task SeedBasicEquipmentAsync(int createdByUserId, int groupId);
}