using QuestBoard.Domain.Enums;
using QuestBoard.Domain.Interfaces;
using QuestBoard.Domain.Models.Shop;

namespace QuestBoard.Domain.Services;

public class ShopSeedService(IShopService shopService) : IShopSeedService
{
    /// <inheritdoc/>
    public async Task SeedBasicEquipmentAsync(int createdByUserId)
    {
        var existingItems = await shopService.GetPublishedItemsAsync();
        if (existingItems.Any())
        {
            // Already seeded
            return;
        }

        var basicItems = GetBasicEquipmentData();

        foreach (var item in basicItems)
        {
            item.CreatedByDmId = createdByUserId;
            item.Status = item.Rarity <= ItemRarity.Uncommon ? ItemStatus.Published : ItemStatus.Draft;
            await shopService.AddAsync(item);
        }
    }

    private static List<ShopItem> GetBasicEquipmentData()
    {
        return
        [
            // Basic Equipment - Weapons
            new() {
                Name = "Shortsword",
                Description = "A light, versatile blade favored by rogues and duelists. This finely crafted weapon features a straight, double-edged blade about two feet in length with a simple crossguard and leather-wrapped grip.",
                Type = ItemType.Equipment,
                Rarity = ItemRarity.Common,
                Price = 10,
                Quantity = -1, // Unlimited
                ReferenceUrl = "https://www.dndbeyond.com/equipment/shortsword"
            },
            new() {
                Name = "Longbow",
                Description = "A tall bow made of flexible yew wood, ideal for long-range combat. This weapon requires both hands to use effectively and can propel arrows with deadly accuracy at distances up to 600 feet.",
                Type = ItemType.Equipment,
                Rarity = ItemRarity.Common,
                Price = 50,
                Quantity = -1,
                ReferenceUrl = "https://www.dndbeyond.com/equipment/longbow"
            },
            new() {
                Name = "Warhammer",
                Description = "A heavy, one-handed weapon featuring a large hammerhead balanced by a spike or pick on the reverse side. Popular among clerics and dwarven warriors for its versatility and devastating impact.",
                Type = ItemType.Equipment,
                Rarity = ItemRarity.Common,
                Price = 15,
                Quantity = -1,
                ReferenceUrl = "https://www.dndbeyond.com/equipment/warhammer"
            },
            new() {
                Name = "Greatsword",
                Description = "A massive two-handed sword with a blade over four feet long. This weapon requires great strength to wield effectively but delivers devastating slashing attacks that can cleave through multiple foes.",
                Type = ItemType.Equipment,
                Rarity = ItemRarity.Common,
                Price = 50,
                Quantity = -1,
                ReferenceUrl = "https://www.dndbeyond.com/equipment/greatsword"
            },

            // Basic Equipment - Armor
            new() {
                Name = "Leather Armor",
                Description = "Light armor consisting of supple leather pieces carefully fitted to the wearer's body. Popular among rangers, rogues, and other adventurers who value mobility over heavy protection.",
                Type = ItemType.Equipment,
                Rarity = ItemRarity.Common,
                Price = 10,
                Quantity = -1,
                ReferenceUrl = "https://www.dndbeyond.com/equipment/leather-armor"
            },
            new() {
                Name = "Chain Mail",
                Description = "Medium armor made of interlocking metal rings that provide good protection while allowing reasonable mobility. A common choice for fighters and clerics who need balance between defense and agility.",
                Type = ItemType.Equipment,
                Rarity = ItemRarity.Common,
                Price = 75,
                Quantity = -1,
                ReferenceUrl = "https://www.dndbeyond.com/equipment/chain-mail"
            },
            new() {
                Name = "Plate Armor",
                Description = "The pinnacle of heavy armor craftsmanship, featuring interlocking plates of steel that cover the entire body. Expensive but provides unmatched protection for those who can afford it and have the strength to wear it.",
                Type = ItemType.Equipment,
                Rarity = ItemRarity.Uncommon,
                Price = 1500,
                Quantity = 5,
                ReferenceUrl = "https://www.dndbeyond.com/equipment/plate"
            },

            // Basic Equipment - Shields and Tools
            new() {
                Name = "Shield",
                Description = "A sturdy round shield made of wood reinforced with metal bands and a central boss. Essential equipment for any warrior who fights in close combat, providing crucial protection against attacks.",
                Type = ItemType.Equipment,
                Rarity = ItemRarity.Common,
                Price = 10,
                Quantity = -1,
                ReferenceUrl = "https://www.dndbeyond.com/equipment/shield"
            },
            new() {
                Name = "Thieves' Tools",
                Description = "A set of small tools including picks, tension wrenches, small mirrors, and other implements essential for picking locks and disarming traps. Indispensable for any rogue or adventurer who values stealth.",
                Type = ItemType.Equipment,
                Rarity = ItemRarity.Common,
                Price = 25,
                Quantity = -1,
                ReferenceUrl = "https://www.dndbeyond.com/equipment/thieves-tools"
            },
            new() {
                Name = "Healer's Kit",
                Description = "A leather pouch containing bandages, splints, herbs, and other supplies for treating wounds and stabilizing the injured. Each kit contains enough supplies for ten uses before needing to be restocked.",
                Type = ItemType.Equipment,
                Rarity = ItemRarity.Common,
                Price = 5,
                Quantity = -1,
                ReferenceUrl = "https://www.dndbeyond.com/equipment/healers-kit"
            },
            new() {
                Name = "Rope (50 feet)",
                Description = "High-quality hemp rope that's strong, reliable, and essential for any adventuring party. Whether used for climbing, binding prisoners, or crossing treacherous terrain, good rope can mean the difference between life and death.",
                Type = ItemType.Equipment,
                Rarity = ItemRarity.Common,
                Price = 2,
                Quantity = -1,
                ReferenceUrl = "https://www.dndbeyond.com/equipment/rope-hempen-50-feet"
            },

            // Basic Equipment - Consumables
            new() {
                Name = "Potion of Healing",
                Description = "A magical red liquid that glows with inner warmth. When consumed, it immediately closes wounds and restores vitality, healing 2d4+2 hit points. Essential for any adventurer venturing into dangerous territory.",
                Type = ItemType.MagicItem,
                Rarity = ItemRarity.Common,
                Price = 50,
                Quantity = 20,
                ReferenceUrl = "https://www.dndbeyond.com/magic-items/potion-of-healing"
            },
            new() {
                Name = "Potion of Healing (Greater)",
                Description = "A more potent healing elixir that glows with bright crimson light. This enhanced potion restores 4d4+4 hit points when consumed, making it invaluable for serious injuries sustained in combat.",
                Type = ItemType.MagicItem,
                Rarity = ItemRarity.Uncommon,
                Price = 150,
                Quantity = 10,
                ReferenceUrl = "https://www.dndbeyond.com/magic-items/potion-of-healing-greater"
            },
            new() {
                Name = "Antitoxin",
                Description = "A bitter-tasting mixture that provides advantage on saving throws against poison for one hour when consumed. Essential protection against venomous creatures and toxic environments.",
                Type = ItemType.Equipment,
                Rarity = ItemRarity.Common,
                Price = 50,
                Quantity = 15,
                ReferenceUrl = "https://www.dndbeyond.com/equipment/antitoxin"
            },

            // Magic Items (Uncommon)
            new() {
                Name = "Cloak of Protection",
                Description = "A finely woven cloak that shimmers with protective magic. While wearing this cloak, you gain a +1 bonus to AC and saving throws, as the magic weaves around you like invisible armor.",
                Type = ItemType.MagicItem,
                Rarity = ItemRarity.Uncommon,
                Price = 350,
                Quantity = 3,
                ReferenceUrl = "https://www.dndbeyond.com/magic-items/cloak-of-protection"
            },
            new() {
                Name = "Bag of Holding",
                Description = "This seemingly ordinary bag opens into an extradimensional space much larger than its exterior suggests. It can hold up to 500 pounds of equipment in a space that weighs only 15 pounds - a miracle of magical engineering.",
                Type = ItemType.MagicItem,
                Rarity = ItemRarity.Uncommon,
                Price = 400,
                Quantity = 2,
                ReferenceUrl = "https://www.dndbeyond.com/magic-items/bag-of-holding"
            },
            new() {
                Name = "Boots of Elvenkind",
                Description = "These soft leather boots are crafted with elven magic that muffles the wearer's footsteps. While wearing these boots, your steps make no sound, regardless of the surface you're moving across.",
                Type = ItemType.MagicItem,
                Rarity = ItemRarity.Uncommon,
                Price = 300,
                Quantity = 1,
                ReferenceUrl = "https://www.dndbeyond.com/magic-items/boots-of-elvenkind"
            },

            // Rare Magic Items
            new() {
                Name = "Flame Tongue Sword",
                Description = "This magic sword appears as a normal blade until its command word is spoken. When activated, flames erupt along the blade's edge, shedding bright light and dealing an additional 2d6 fire damage on hit.",
                Type = ItemType.MagicItem,
                Rarity = ItemRarity.Rare,
                Price = 2500,
                Quantity = 1,
                ReferenceUrl = "https://www.dndbeyond.com/magic-items/flame-tongue"
            },
            new() {
                Name = "Ring of Spell Storing",
                Description = "A masterwork ring that can store up to five levels of spells, allowing the wearer to cast stored spells later. This ring contains the essence of magical power itself, making it highly sought after by spellcasters.",
                Type = ItemType.MagicItem,
                Rarity = ItemRarity.Rare,
                Price = 4000,
                Quantity = 1,
                ReferenceUrl = "https://www.dndbeyond.com/magic-items/ring-of-spell-storing"
            },

            // Legendary Items (Very Limited)
            new() {
                Name = "Ancient Dragon Scale Armor",
                Description = "Legendary armor crafted from the scales of an ancient dragon. This magnificent suit provides incredible protection and resistance to elemental damage, bearing the wisdom and power of its draconic origin.",
                Type = ItemType.MagicItem,
                Rarity = ItemRarity.Legendary,
                Price = 75000,
                Quantity = 1,
                AvailableFrom = DateTime.UtcNow.AddDays(7), // Available in a week
                AvailableUntil = DateTime.UtcNow.AddDays(30) // Available for limited time
            }
        ];
    }
}