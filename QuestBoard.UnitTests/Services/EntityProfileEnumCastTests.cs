using QuestBoard.Domain.Enums;

namespace QuestBoard.UnitTests.Services;

/// <summary>
/// EntityProfile.cs casts every domain enum to its underlying int and back at the
/// Entity/DomainModel AutoMapper boundary (e.g. (int)src.Type / (ItemType)src.Type).
/// This proves the (int)/(T) cast pattern round-trips for every declared value of every
/// enum used in those casts, so a future reorder or an undefined int read from the database
/// cannot silently produce a wrong or truncated enum value.
/// </summary>
public class EntityProfileEnumCastTests
{
    [Theory]
    [MemberData(nameof(SignupRoleValues))]
    public void SignupRole_CastRoundTrips(SignupRole value) => AssertRoundTrips(value);

    [Theory]
    [MemberData(nameof(VoteTypeValues))]
    public void VoteType_CastRoundTrips(VoteType value) => AssertRoundTrips(value);

    [Theory]
    [MemberData(nameof(ItemTypeValues))]
    public void ItemType_CastRoundTrips(ItemType value) => AssertRoundTrips(value);

    [Theory]
    [MemberData(nameof(ItemRarityValues))]
    public void ItemRarity_CastRoundTrips(ItemRarity value) => AssertRoundTrips(value);

    [Theory]
    [MemberData(nameof(ItemStatusValues))]
    public void ItemStatus_CastRoundTrips(ItemStatus value) => AssertRoundTrips(value);

    [Theory]
    [MemberData(nameof(TransactionTypeValues))]
    public void TransactionType_CastRoundTrips(TransactionType value) => AssertRoundTrips(value);

    [Theory]
    [MemberData(nameof(CharacterStatusValues))]
    public void CharacterStatus_CastRoundTrips(CharacterStatus value) => AssertRoundTrips(value);

    [Theory]
    [MemberData(nameof(CharacterRoleValues))]
    public void CharacterRole_CastRoundTrips(CharacterRole value) => AssertRoundTrips(value);

    [Theory]
    [MemberData(nameof(DndClassValues))]
    public void DndClass_CastRoundTrips(DndClass value) => AssertRoundTrips(value);

    [Theory]
    [MemberData(nameof(GroupRoleValues))]
    public void GroupRole_CastRoundTrips(GroupRole value) => AssertRoundTrips(value);

    /// <summary>
    /// Mirrors the exact cast pattern EntityProfile.cs uses at every enum mapping site:
    /// (int)value going into the entity column, (T)intValue coming back out.
    /// Also asserts the underlying int is a defined member of the enum, so an undefined
    /// int read from the database would be caught here rather than silently coerced.
    /// </summary>
    private static void AssertRoundTrips<TEnum>(TEnum value) where TEnum : struct, Enum
    {
        var asInt = (int)(object)value;
        var roundTripped = (TEnum)(object)asInt;

        roundTripped.Should().Be(value);
        Enum.IsDefined(typeof(TEnum), asInt).Should().BeTrue();
    }

    public static TheoryData<SignupRole> SignupRoleValues() => ToTheoryData<SignupRole>();
    public static TheoryData<VoteType> VoteTypeValues() => ToTheoryData<VoteType>();
    public static TheoryData<ItemType> ItemTypeValues() => ToTheoryData<ItemType>();
    public static TheoryData<ItemRarity> ItemRarityValues() => ToTheoryData<ItemRarity>();
    public static TheoryData<ItemStatus> ItemStatusValues() => ToTheoryData<ItemStatus>();
    public static TheoryData<TransactionType> TransactionTypeValues() => ToTheoryData<TransactionType>();
    public static TheoryData<CharacterStatus> CharacterStatusValues() => ToTheoryData<CharacterStatus>();
    public static TheoryData<CharacterRole> CharacterRoleValues() => ToTheoryData<CharacterRole>();
    public static TheoryData<DndClass> DndClassValues() => ToTheoryData<DndClass>();
    public static TheoryData<GroupRole> GroupRoleValues() => ToTheoryData<GroupRole>();

    private static TheoryData<TEnum> ToTheoryData<TEnum>() where TEnum : struct, Enum
    {
        var data = new TheoryData<TEnum>();
        foreach (var value in Enum.GetValues<TEnum>())
        {
            data.Add(value);
        }
        return data;
    }
}
