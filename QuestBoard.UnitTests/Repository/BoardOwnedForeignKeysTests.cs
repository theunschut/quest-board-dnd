using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using QuestBoard.Domain.Interfaces;
using QuestBoard.Repository.Entities;

namespace QuestBoard.UnitTests.Repository;

// Pins every foreign key that the board delete depends on, by reading the model rather than
// running a delete. The in-memory store enforces no foreign key and runs no cascade, so a new
// table that references a board would pass every InMemory test and then fail a real delete.
// These facts turn that into a test failure with an instruction instead.
public class BoardOwnedForeignKeysTests
{
    private const string ExtendMessage =
        "The model changed in a way the board delete does not know about. Extend " +
        "GroupRepository.DeleteBoardAsync, its preview counts and this list together.";

    private sealed class NoActiveGroup : IActiveGroupContext
    {
        public int? ActiveGroupId { get; set; }
    }

    private static IModel BuildModel()
    {
        var options = new DbContextOptionsBuilder<QuestBoardContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        using var context = new QuestBoardContext(options, new NoActiveGroup());
        return context.Model;
    }

    // Every type whose rows belong to exactly one board, directly or through a parent row.
    // GroupEntity is deliberately not in this list: the board row itself is the root.
    private static readonly Type[] BoardOwned =
    [
        typeof(QuestEntity),
        typeof(CharacterEntity),
        typeof(EventEntity),
        typeof(EventSeriesEntity),
        typeof(ContactEntity),
        typeof(ContactCategoryEntity),
        typeof(ContactTagEntity),
        typeof(ShopItemEntity),
        typeof(ProposedDateEntity),
        typeof(PlayerDateVoteEntity),
        typeof(PlayerSignupEntity),
        typeof(ReminderLogEntity),
        typeof(EventSignupEntity),
        typeof(CharacterClassEntity),
        typeof(CharacterImageEntity),
        typeof(ContactNoteEntity),
        typeof(ContactImageEntity),
        typeof(UserTransactionEntity),
    ];

    private static string Describe(IForeignKey fk)
        => $"{fk.DeclaringEntityType.DisplayName()}.{string.Join(",", fk.Properties.Select(p => p.Name))} -> {fk.PrincipalEntityType.DisplayName()}";

    [Fact]
    public void EveryForeignKeyIntoGroups_IsHandledByTheBoardDelete()
    {
        var model = BuildModel();

        var actual = model.FindEntityType(typeof(GroupEntity))!
            .GetReferencingForeignKeys()
            .Select(fk => $"{fk.DeclaringEntityType.ClrType.Name}:{fk.DeleteBehavior}")
            .OrderBy(s => s, StringComparer.Ordinal)
            .ToList();

        // Memberships cascade with the board; everything else is NoAction, so the delete must
        // remove those rows itself before it removes the board row.
        var expected = new[]
        {
            "CharacterEntity:NoAction",
            "ContactCategoryEntity:NoAction",
            "ContactEntity:NoAction",
            "ContactTagEntity:NoAction",
            "EventEntity:NoAction",
            "EventSeriesEntity:NoAction",
            "QuestEntity:NoAction",
            "ShopItemEntity:NoAction",
            "UserGroupEntity:Cascade",
        }.OrderBy(s => s, StringComparer.Ordinal).ToList();

        actual.Should().Equal(expected, "a table now references Groups. " + ExtendMessage);
    }

    [Fact]
    public void NonCascadeForeignKeysAmongBoardOwnedTables_AreTheHandledSet()
    {
        var model = BuildModel();
        var owned = BoardOwned.ToHashSet();

        var actual = model.GetEntityTypes()
            .SelectMany(e => e.GetDeclaredForeignKeys())
            .Where(fk => owned.Contains(fk.DeclaringEntityType.ClrType) && owned.Contains(fk.PrincipalEntityType.ClrType))
            .Where(fk => fk.DeleteBehavior is not (DeleteBehavior.Cascade or DeleteBehavior.SetNull))
            .Select(Describe)
            .OrderBy(s => s, StringComparer.Ordinal)
            .ToList();

        var expected = new[]
        {
            "PlayerSignupEntity.QuestId -> QuestEntity",
            "ReminderLogEntity.QuestId -> QuestEntity",
            "QuestEntity.OriginalQuestId -> QuestEntity",
            "PlayerSignupEntity.CharacterId -> CharacterEntity",
            "EventEntity.SeriesId -> EventSeriesEntity",
            "UserTransactionEntity.ShopItemId -> ShopItemEntity",
            "UserTransactionEntity.OriginalTransactionId -> UserTransactionEntity",
        }.OrderBy(s => s, StringComparer.Ordinal).ToList();

        actual.Should().Equal(expected,
            "a non-cascade foreign key between board-owned tables was added or removed. " + ExtendMessage);
    }

    [Fact]
    public void NoTableOutsideTheBoardReachesIntoBoardOwnedRows()
    {
        var model = BuildModel();

        // GroupEntity is left out of the principal set on purpose. Foreign keys into Groups,
        // including the membership cascade, are pinned only by
        // EveryForeignKeyIntoGroups_IsHandledByTheBoardDelete, so that fact stays the single owner.
        var principals = BoardOwned.ToHashSet();
        principals.Should().NotContain(typeof(GroupEntity));

        var joinEntity = model.FindEntityType("ContactEntityContactTagEntity");
        joinEntity.Should().NotBeNull("the contact-tag many-to-many is mapped as a shared-type join entity");

        var strangers = model.GetEntityTypes()
            .SelectMany(e => e.GetDeclaredForeignKeys())
            .Where(fk => principals.Contains(fk.PrincipalEntityType.ClrType))
            .Where(fk => !principals.Contains(fk.DeclaringEntityType.ClrType))
            .Where(fk => fk.DeclaringEntityType != joinEntity)
            .Select(Describe)
            .ToList();

        strangers.Should().BeEmpty(
            "a table outside the board-owned set now references a board-owned table. " + ExtendMessage);

        var joinKeys = joinEntity!.GetDeclaredForeignKeys().ToList();
        joinKeys.Should().HaveCount(2);
        joinKeys.Should().OnlyContain(fk => fk.DeleteBehavior == DeleteBehavior.Cascade);
    }

    [Fact]
    public void ImageNoteClassSignupAndVoteForeignKeys_Cascade()
    {
        var model = BuildModel();

        // These cascade in the database, which is why the delete never loads image blobs and
        // why the in-memory store leaves orphan image rows behind that SQL Server removes.
        var cascading = new (Type Dependent, Type Principal)[]
        {
            (typeof(CharacterImageEntity), typeof(CharacterEntity)),
            (typeof(ContactImageEntity), typeof(ContactEntity)),
            (typeof(CharacterClassEntity), typeof(CharacterEntity)),
            (typeof(ContactNoteEntity), typeof(ContactEntity)),
            (typeof(EventSignupEntity), typeof(EventEntity)),
            (typeof(ProposedDateEntity), typeof(QuestEntity)),
            (typeof(PlayerDateVoteEntity), typeof(ProposedDateEntity)),
            (typeof(PlayerDateVoteEntity), typeof(PlayerSignupEntity)),
        };

        foreach (var (dependent, principal) in cascading)
        {
            var fk = model.FindEntityType(dependent)!
                .GetDeclaredForeignKeys()
                .Single(f => f.PrincipalEntityType.ClrType == principal);

            fk.DeleteBehavior.Should().Be(DeleteBehavior.Cascade,
                $"{dependent.Name} to {principal.Name} must cascade. " + ExtendMessage);
        }

        var joinEntity = model.FindEntityType("ContactEntityContactTagEntity")!;
        joinEntity.GetDeclaredForeignKeys()
            .Single(f => f.PrincipalEntityType.ClrType == typeof(ContactEntity))
            .DeleteBehavior.Should().Be(DeleteBehavior.Cascade);
        joinEntity.GetDeclaredForeignKeys()
            .Single(f => f.PrincipalEntityType.ClrType == typeof(ContactTagEntity))
            .DeleteBehavior.Should().Be(DeleteBehavior.Cascade);
    }
}
