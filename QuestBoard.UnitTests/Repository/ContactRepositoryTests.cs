using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using QuestBoard.Domain.Interfaces;
using QuestBoard.Repository;
using QuestBoard.Repository.Entities;

namespace QuestBoard.UnitTests.Repository;

// Wave 0 RED scaffold (Phase 57, Plan 01): this file intentionally references production
// symbols (ContactEntity, ContactImageEntity, ContactNoteEntity, ContactRepository, Contact,
// ContactNote) that do not exist yet. It will compile-fail until Plans 02-03 land — that is the
// intended state for this test-first scaffold. Once those plans exist, this file must compile
// and every fact below must pass.
public class ContactRepositoryTests
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

    // Seeds two groups, a creator user, and one contact in each group (with image), while the
    // context's active group is temporarily null so seeding itself isn't filtered out.
    private static async Task SeedTwoGroupContactsAsync(QuestBoardContext context, MutableTestGroupContext groupContext)
    {
        var originalActiveGroupId = groupContext.ActiveGroupId;
        groupContext.ActiveGroupId = null; // see-all during seeding

        context.Groups.Add(new GroupEntity { Id = 1, Name = "Group One" });
        context.Groups.Add(new GroupEntity { Id = 2, Name = "Group Two" });
        context.UserEntities.Add(new UserEntity { Id = 1, Name = "Creator One", Email = "creator1@test.com" });
        context.UserEntities.Add(new UserEntity { Id = 2, Name = "Creator Two", Email = "creator2@test.com" });

        var contactInGroup1 = new ContactEntity
        {
            Id = 1,
            Name = "Zeddicus the Smith",
            GroupId = 1,
            CreatedByUserId = 1,
            IsRevealed = true,
            CreatedAt = DateTime.UtcNow,
            ContactImage = new ContactImageEntity { Id = 1, ImageData = [1, 2, 3] }
        };

        var contactInGroup2 = new ContactEntity
        {
            Id = 2,
            Name = "Group Two Contact",
            GroupId = 2,
            CreatedByUserId = 2,
            IsRevealed = true,
            CreatedAt = DateTime.UtcNow,
            ContactImage = new ContactImageEntity { Id = 2, ImageData = [4, 5, 6] }
        };

        context.Contacts.Add(contactInGroup1);
        context.Contacts.Add(contactInGroup2);

        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        groupContext.ActiveGroupId = originalActiveGroupId;
    }

    [Fact]
    public async Task GetAllContactsWithDetailsAsync_MultipleContacts_ReturnsOrderedAlphabeticallyByName()
    {
        // Arrange: seed names deliberately out of alphabetical order (D-17: flat list, alphabetical by Name)
        var groupContext = new MutableTestGroupContext { ActiveGroupId = null };
        await using var context = CreateContext(nameof(GetAllContactsWithDetailsAsync_MultipleContacts_ReturnsOrderedAlphabeticallyByName), groupContext);

        context.Groups.Add(new GroupEntity { Id = 1, Name = "Group One" });
        context.UserEntities.Add(new UserEntity { Id = 1, Name = "Creator One", Email = "creator1@test.com" });

        context.Contacts.Add(new ContactEntity { Id = 1, Name = "Zorlath", GroupId = 1, CreatedByUserId = 1, IsRevealed = true, CreatedAt = DateTime.UtcNow });
        context.Contacts.Add(new ContactEntity { Id = 2, Name = "Aldric", GroupId = 1, CreatedByUserId = 1, IsRevealed = true, CreatedAt = DateTime.UtcNow });
        context.Contacts.Add(new ContactEntity { Id = 3, Name = "Mira", GroupId = 1, CreatedByUserId = 1, IsRevealed = true, CreatedAt = DateTime.UtcNow });

        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var repository = new ContactRepository(context, CreateMapper());
        groupContext.ActiveGroupId = 1;

        // Act
        var contacts = await repository.GetAllContactsWithDetailsAsync(TestContext.Current.CancellationToken);

        // Assert: ascending alphabetical order, not insertion/Id order
        contacts.Should().HaveCount(3);
        contacts.Select(c => c.Name).Should().ContainInConsecutiveOrder("Aldric", "Mira", "Zorlath");
    }

    [Fact]
    public async Task GetContactWithDetailsAsync_Notes_ReturnedNewestFirstByCreatedAt()
    {
        // Arrange: Id order and CreatedAt order deliberately disagree, proving the sort key is
        // CreatedAt (D-10: newest first), not Id.
        var groupContext = new MutableTestGroupContext { ActiveGroupId = null };
        await using var context = CreateContext(nameof(GetContactWithDetailsAsync_Notes_ReturnedNewestFirstByCreatedAt), groupContext);

        context.Groups.Add(new GroupEntity { Id = 1, Name = "Group One" });
        context.UserEntities.Add(new UserEntity { Id = 1, Name = "Creator One", Email = "creator1@test.com" });
        context.UserEntities.Add(new UserEntity { Id = 2, Name = "Note Author", Email = "author@test.com" });

        var contact = new ContactEntity { Id = 1, Name = "Notable Contact", GroupId = 1, CreatedByUserId = 1, IsRevealed = true, CreatedAt = DateTime.UtcNow };
        context.Contacts.Add(contact);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var now = DateTime.UtcNow;
        // Insertion order (Id 1,2,3) intentionally does NOT match CreatedAt order (oldest,newest,middle).
        context.ContactNotes.Add(new ContactNoteEntity { Id = 1, ContactId = contact.Id, AuthorUserId = 2, Text = "Oldest note", CreatedAt = now.AddDays(-2) });
        context.ContactNotes.Add(new ContactNoteEntity { Id = 2, ContactId = contact.Id, AuthorUserId = 2, Text = "Newest note", CreatedAt = now });
        context.ContactNotes.Add(new ContactNoteEntity { Id = 3, ContactId = contact.Id, AuthorUserId = 2, Text = "Middle note", CreatedAt = now.AddDays(-1) });
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var repository = new ContactRepository(context, CreateMapper());
        groupContext.ActiveGroupId = 1;

        // Act
        var result = await repository.GetContactWithDetailsAsync(contact.Id, TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result!.Notes.Select(n => n.Text).Should().ContainInConsecutiveOrder("Newest note", "Middle note", "Oldest note");
    }

    [Fact]
    public async Task GetContactWithDetailsAsync_ForContactInDifferentGroup_ReturnsNull()
    {
        // Arrange: group scoping (fail-closed) — a contact in group 2 is not visible when the
        // active group context resolves to group 1.
        var groupContext = new MutableTestGroupContext { ActiveGroupId = null };
        await using var context = CreateContext(nameof(GetContactWithDetailsAsync_ForContactInDifferentGroup_ReturnsNull), groupContext);
        await SeedTwoGroupContactsAsync(context, groupContext);

        var repository = new ContactRepository(context, CreateMapper());
        groupContext.ActiveGroupId = 1;

        // Act: contact Id=2 belongs to group 2, viewer's active group is 1
        var contact = await repository.GetContactWithDetailsAsync(2, TestContext.Current.CancellationToken);

        // Assert
        contact.Should().BeNull();
    }

    [Fact]
    public async Task GetAllContactsWithDetailsAsync_ActiveGroupOne_ExcludesGroupTwoContact()
    {
        // Arrange
        var groupContext = new MutableTestGroupContext { ActiveGroupId = null };
        await using var context = CreateContext(nameof(GetAllContactsWithDetailsAsync_ActiveGroupOne_ExcludesGroupTwoContact), groupContext);
        await SeedTwoGroupContactsAsync(context, groupContext);

        var repository = new ContactRepository(context, CreateMapper());
        groupContext.ActiveGroupId = 1;

        // Act
        var contacts = await repository.GetAllContactsWithDetailsAsync(TestContext.Current.CancellationToken);

        // Assert
        contacts.Should().ContainSingle();
        contacts[0].Name.Should().Be("Zeddicus the Smith");
    }

    [Fact]
    public async Task GetContactImageAsync_ForContactInActiveGroup_ReturnsImageData()
    {
        // Arrange: image round-trip — bytes stored on create are returned exactly.
        var groupContext = new MutableTestGroupContext { ActiveGroupId = null };
        await using var context = CreateContext(nameof(GetContactImageAsync_ForContactInActiveGroup_ReturnsImageData), groupContext);
        await SeedTwoGroupContactsAsync(context, groupContext);

        var repository = new ContactRepository(context, CreateMapper());
        groupContext.ActiveGroupId = 1;

        // Act
        var image = await repository.GetContactImageAsync(1, TestContext.Current.CancellationToken);

        // Assert
        image.Should().NotBeNull();
        image.Should().Equal([1, 2, 3]);
    }

    [Fact]
    public async Task GetContactImageAsync_ForContactInDifferentGroup_ReturnsNull()
    {
        // Arrange: image lookup must also respect the group filter (rooted at Contacts, not
        // ContactImages directly), mirroring GetCharacterProfilePictureAsync's cross-group test.
        var groupContext = new MutableTestGroupContext { ActiveGroupId = null };
        await using var context = CreateContext(nameof(GetContactImageAsync_ForContactInDifferentGroup_ReturnsNull), groupContext);
        await SeedTwoGroupContactsAsync(context, groupContext);

        var repository = new ContactRepository(context, CreateMapper());
        groupContext.ActiveGroupId = 1;

        // Act: contact Id=2 belongs to group 2, viewer's active group is 1
        var image = await repository.GetContactImageAsync(2, TestContext.Current.CancellationToken);

        // Assert
        image.Should().BeNull();
    }

    [Fact]
    public async Task AddNoteAsync_ThenDeleteNoteAsync_RoundTripsCorrectly()
    {
        // Arrange: D-08/D-09 — dedicated note methods, not folded into the generic UpdateAsync path.
        var groupContext = new MutableTestGroupContext { ActiveGroupId = null };
        await using var context = CreateContext(nameof(AddNoteAsync_ThenDeleteNoteAsync_RoundTripsCorrectly), groupContext);

        context.Groups.Add(new GroupEntity { Id = 1, Name = "Group One" });
        context.UserEntities.Add(new UserEntity { Id = 1, Name = "Creator One", Email = "creator1@test.com" });
        context.UserEntities.Add(new UserEntity { Id = 2, Name = "Note Author", Email = "author@test.com" });
        var contact = new ContactEntity { Id = 1, Name = "Notable Contact", GroupId = 1, CreatedByUserId = 1, IsRevealed = true, CreatedAt = DateTime.UtcNow };
        context.Contacts.Add(contact);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var repository = new ContactRepository(context, CreateMapper());
        groupContext.ActiveGroupId = 1;

        var note = new ContactNote
        {
            ContactId = contact.Id,
            AuthorUserId = 2,
            Text = "A freeform note about this contact.",
            CreatedAt = DateTime.UtcNow
        };

        // Act: add
        await repository.AddNoteAsync(note, TestContext.Current.CancellationToken);
        var afterAdd = await repository.GetContactWithDetailsAsync(contact.Id, TestContext.Current.CancellationToken);

        // Assert: retrievable after add
        afterAdd.Should().NotBeNull();
        afterAdd!.Notes.Should().ContainSingle(n => n.Text == "A freeform note about this contact.");
        var addedNoteId = afterAdd.Notes.Single().Id;

        // Act: delete
        await repository.DeleteNoteAsync(addedNoteId, TestContext.Current.CancellationToken);
        var afterDelete = await repository.GetContactWithDetailsAsync(contact.Id, TestContext.Current.CancellationToken);

        // Assert: gone after delete
        afterDelete.Should().NotBeNull();
        afterDelete!.Notes.Should().BeEmpty();
    }

    private sealed class MutableTestGroupContext : IActiveGroupContext
    {
        public int? ActiveGroupId { get; set; }
    }
}
