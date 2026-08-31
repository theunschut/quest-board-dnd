using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using QuestBoard.Domain.Interfaces;
using QuestBoard.Domain.Models;
using QuestBoard.Repository;
using QuestBoard.Repository.Entities;

namespace QuestBoard.UnitTests.Repository;

// Repository-level coverage for contacts and their notes: group-scoped reads, alphabetical
// contact ordering, newest-first note ordering, and the dedicated note add/edit/delete methods.
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
            ProfileImage = new ContactImageEntity { Id = 1, OriginalImageData = [1, 2, 3] }
        };

        var contactInGroup2 = new ContactEntity
        {
            Id = 2,
            Name = "Group Two Contact",
            GroupId = 2,
            CreatedByUserId = 2,
            IsRevealed = true,
            CreatedAt = DateTime.UtcNow,
            ProfileImage = new ContactImageEntity { Id = 2, OriginalImageData = [4, 5, 6] }
        };

        context.Contacts.Add(contactInGroup1);
        context.Contacts.Add(contactInGroup2);

        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        groupContext.ActiveGroupId = originalActiveGroupId;
    }

    [Fact]
    public async Task GetAllContactsWithDetailsAsync_MultipleContacts_ReturnsOrderedAlphabeticallyByName()
    {
        // Arrange: seed names deliberately out of alphabetical order — the index is a flat list sorted by Name
        var groupContext = new MutableTestGroupContext { ActiveGroupId = null };
        await using var context = CreateContext("ContactRepositoryTests." + nameof(GetAllContactsWithDetailsAsync_MultipleContacts_ReturnsOrderedAlphabeticallyByName), groupContext);

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
        // CreatedAt (newest first), not Id.
        var groupContext = new MutableTestGroupContext { ActiveGroupId = null };
        await using var context = CreateContext("ContactRepositoryTests." + nameof(GetContactWithDetailsAsync_Notes_ReturnedNewestFirstByCreatedAt), groupContext);

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
        await using var context = CreateContext("ContactRepositoryTests." + nameof(GetContactWithDetailsAsync_ForContactInDifferentGroup_ReturnsNull), groupContext);
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
        await using var context = CreateContext("ContactRepositoryTests." + nameof(GetAllContactsWithDetailsAsync_ActiveGroupOne_ExcludesGroupTwoContact), groupContext);
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
    public async Task GetContactOriginalImageAsync_ForContactInActiveGroup_ReturnsImageData()
    {
        // Arrange: image round-trip — bytes stored on create are returned exactly.
        var groupContext = new MutableTestGroupContext { ActiveGroupId = null };
        await using var context = CreateContext("ContactRepositoryTests." + nameof(GetContactOriginalImageAsync_ForContactInActiveGroup_ReturnsImageData), groupContext);
        await SeedTwoGroupContactsAsync(context, groupContext);

        var repository = new ContactRepository(context, CreateMapper());
        groupContext.ActiveGroupId = 1;

        // Act
        var image = await repository.GetContactOriginalImageAsync(1, TestContext.Current.CancellationToken);

        // Assert
        image.Should().NotBeNull();
        image.Should().Equal([1, 2, 3]);
    }

    [Fact]
    public async Task GetContactOriginalImageAsync_ForContactInDifferentGroup_ReturnsNull()
    {
        // Arrange: image lookup must also respect the group filter (rooted at Contacts, not
        // ContactImages directly), mirroring the Character repository's cross-group test.
        var groupContext = new MutableTestGroupContext { ActiveGroupId = null };
        await using var context = CreateContext("ContactRepositoryTests." + nameof(GetContactOriginalImageAsync_ForContactInDifferentGroup_ReturnsNull), groupContext);
        await SeedTwoGroupContactsAsync(context, groupContext);

        var repository = new ContactRepository(context, CreateMapper());
        groupContext.ActiveGroupId = 1;

        // Act: contact Id=2 belongs to group 2, viewer's active group is 1
        var image = await repository.GetContactOriginalImageAsync(2, TestContext.Current.CancellationToken);

        // Assert
        image.Should().BeNull();
    }

    [Fact]
    public async Task UpdateProfileImageAsync_SetsOriginalImageData()
    {
        // Arrange
        var groupContext = new MutableTestGroupContext { ActiveGroupId = null };
        await using var context = CreateContext("ContactRepositoryTests." + nameof(UpdateProfileImageAsync_SetsOriginalImageData), groupContext);
        await SeedTwoGroupContactsAsync(context, groupContext);

        var repository = new ContactRepository(context, CreateMapper());
        groupContext.ActiveGroupId = 1;

        // Act
        await repository.UpdateProfileImageAsync(1, [10, 11, 12], null, TestContext.Current.CancellationToken);
        var original = await repository.GetContactOriginalImageAsync(1, TestContext.Current.CancellationToken);

        // Assert
        original.Should().NotBeNull();
        original.Should().Equal([10, 11, 12]);
    }

    [Fact]
    public async Task GetContactCroppedImageAsync_FallsBackToOriginal_WhenCroppedIsNull()
    {
        // Arrange: seeded contact has only OriginalImageData set, CroppedImageData is null
        var groupContext = new MutableTestGroupContext { ActiveGroupId = null };
        await using var context = CreateContext("ContactRepositoryTests." + nameof(GetContactCroppedImageAsync_FallsBackToOriginal_WhenCroppedIsNull), groupContext);
        await SeedTwoGroupContactsAsync(context, groupContext);

        var repository = new ContactRepository(context, CreateMapper());
        groupContext.ActiveGroupId = 1;

        // Act
        var cropped = await repository.GetContactCroppedImageAsync(1, TestContext.Current.CancellationToken);

        // Assert: falls back to the original bytes since no crop was ever saved
        cropped.Should().NotBeNull();
        cropped.Should().Equal([1, 2, 3]);
    }

    [Fact]
    public async Task GetContactOriginalAndCroppedImageAsync_ReturnDistinctValues()
    {
        // Arrange
        var groupContext = new MutableTestGroupContext { ActiveGroupId = null };
        await using var context = CreateContext("ContactRepositoryTests." + nameof(GetContactOriginalAndCroppedImageAsync_ReturnDistinctValues), groupContext);
        await SeedTwoGroupContactsAsync(context, groupContext);

        var repository = new ContactRepository(context, CreateMapper());
        groupContext.ActiveGroupId = 1;

        // Act: set both an original and a distinct crop
        await repository.UpdateProfileImageAsync(1, [20, 21, 22], [30, 31, 32], TestContext.Current.CancellationToken);
        var original = await repository.GetContactOriginalImageAsync(1, TestContext.Current.CancellationToken);
        var cropped = await repository.GetContactCroppedImageAsync(1, TestContext.Current.CancellationToken);

        // Assert: original and cropped are independently retrievable and distinct
        original.Should().Equal([20, 21, 22]);
        cropped.Should().Equal([30, 31, 32]);
    }

    [Fact]
    public async Task UpdateProfileImageAsync_ReplacesBothColumnsAtomically()
    {
        // Arrange: seed with an original+crop already set
        var groupContext = new MutableTestGroupContext { ActiveGroupId = null };
        await using var context = CreateContext("ContactRepositoryTests." + nameof(UpdateProfileImageAsync_ReplacesBothColumnsAtomically), groupContext);
        await SeedTwoGroupContactsAsync(context, groupContext);

        var repository = new ContactRepository(context, CreateMapper());
        groupContext.ActiveGroupId = 1;
        await repository.UpdateProfileImageAsync(1, [1, 1, 1], [2, 2, 2], TestContext.Current.CancellationToken);

        // Act: re-upload with a brand-new original+crop pair
        await repository.UpdateProfileImageAsync(1, [40, 41, 42], [50, 51, 52], TestContext.Current.CancellationToken);
        var original = await repository.GetContactOriginalImageAsync(1, TestContext.Current.CancellationToken);
        var cropped = await repository.GetContactCroppedImageAsync(1, TestContext.Current.CancellationToken);

        // Assert: both columns fully replaced, no trace of either prior upload
        original.Should().Equal([40, 41, 42]);
        cropped.Should().Equal([50, 51, 52]);
    }

    [Fact]
    public async Task UpdateProfileImageAsync_NewOriginalWithoutCrop_ClearsStaleCropped()
    {
        // Arrange: seed with an original+crop already set (upload A)
        var groupContext = new MutableTestGroupContext { ActiveGroupId = null };
        await using var context = CreateContext("ContactRepositoryTests." + nameof(UpdateProfileImageAsync_NewOriginalWithoutCrop_ClearsStaleCropped), groupContext);
        await SeedTwoGroupContactsAsync(context, groupContext);

        var repository = new ContactRepository(context, CreateMapper());
        groupContext.ActiveGroupId = 1;
        await repository.UpdateProfileImageAsync(1, [1, 1, 1], [2, 2, 2], TestContext.Current.CancellationToken);

        // Act: re-upload a new original only (upload B), crop param null (Pitfall 5)
        await repository.UpdateProfileImageAsync(1, [60, 61, 62], null, TestContext.Current.CancellationToken);
        var cropped = await repository.GetContactCroppedImageAsync(1, TestContext.Current.CancellationToken);

        // Assert: cropped-read falls back to B's original, NOT A's stale crop
        cropped.Should().Equal([60, 61, 62]);
    }

    [Fact]
    public async Task AddNoteAsync_ThenDeleteNoteAsync_RoundTripsCorrectly()
    {
        // Arrange: notes have dedicated add/delete methods, not folded into the generic UpdateAsync path.
        var groupContext = new MutableTestGroupContext { ActiveGroupId = null };
        await using var context = CreateContext("ContactRepositoryTests." + nameof(AddNoteAsync_ThenDeleteNoteAsync_RoundTripsCorrectly), groupContext);

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

    [Fact]
    public async Task GetAllContactsWithDetailsAsync_ReflectsHasContactImage_TrueWithImage_FalseWithout()
    {
        // Arrange: two contacts in the same group, one with a stored image and one without.
        var groupContext = new MutableTestGroupContext { ActiveGroupId = null };
        await using var context = CreateContext("ContactRepositoryTests." + nameof(GetAllContactsWithDetailsAsync_ReflectsHasContactImage_TrueWithImage_FalseWithout), groupContext);

        context.Groups.Add(new GroupEntity { Id = 1, Name = "Group One" });
        context.UserEntities.Add(new UserEntity { Id = 1, Name = "Creator One", Email = "creator1@test.com" });
        context.Contacts.Add(new ContactEntity
        {
            Id = 1,
            Name = "Has Picture",
            GroupId = 1,
            CreatedByUserId = 1,
            IsRevealed = true,
            CreatedAt = DateTime.UtcNow,
            ProfileImage = new ContactImageEntity { Id = 1, OriginalImageData = [1, 2, 3] }
        });
        context.Contacts.Add(new ContactEntity
        {
            Id = 2,
            Name = "No Picture",
            GroupId = 1,
            CreatedByUserId = 1,
            IsRevealed = true,
            CreatedAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var repository = new ContactRepository(context, CreateMapper());
        groupContext.ActiveGroupId = 1;

        // Act
        var contacts = await repository.GetAllContactsWithDetailsAsync(TestContext.Current.CancellationToken);

        // Assert
        contacts.Single(c => c.Name == "Has Picture").HasContactImage.Should().BeTrue();
        contacts.Single(c => c.Name == "No Picture").HasContactImage.Should().BeFalse();
    }

    [Fact]
    public async Task GetContactWithDetailsAsync_ReflectsHasContactImage_TrueWithImage()
    {
        // Arrange
        var groupContext = new MutableTestGroupContext { ActiveGroupId = null };
        await using var context = CreateContext("ContactRepositoryTests." + nameof(GetContactWithDetailsAsync_ReflectsHasContactImage_TrueWithImage), groupContext);
        await SeedTwoGroupContactsAsync(context, groupContext);

        var repository = new ContactRepository(context, CreateMapper());
        groupContext.ActiveGroupId = 1;

        // Act
        var contact = await repository.GetContactWithDetailsAsync(1, TestContext.Current.CancellationToken);

        // Assert
        contact.Should().NotBeNull();
        contact!.HasContactImage.Should().BeTrue();
    }

    [Fact]
    public async Task GetAllContactsWithDetailsAsync_ContactAssignedToCategory_ReturnsCategoryNamePopulated()
    {
        // Arrange: a contact assigned to a category
        var groupContext = new MutableTestGroupContext { ActiveGroupId = null };
        await using var context = CreateContext("ContactRepositoryTests." + nameof(GetAllContactsWithDetailsAsync_ContactAssignedToCategory_ReturnsCategoryNamePopulated), groupContext);

        context.Groups.Add(new GroupEntity { Id = 1, Name = "Group One" });
        context.UserEntities.Add(new UserEntity { Id = 1, Name = "Creator One", Email = "creator1@test.com" });
        context.ContactCategories.Add(new ContactCategoryEntity { Id = 1, Name = "Merchants", SortOrder = 0, GroupId = 1 });
        context.Contacts.Add(new ContactEntity { Id = 1, Name = "Assigned Contact", GroupId = 1, CreatedByUserId = 1, CategoryId = 1, CreatedAt = DateTime.UtcNow });
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var repository = new ContactRepository(context, CreateMapper());
        groupContext.ActiveGroupId = 1;

        // Act
        var contacts = await repository.GetAllContactsWithDetailsAsync(TestContext.Current.CancellationToken);

        // Assert: category name available with no second query
        contacts.Should().ContainSingle();
        contacts[0].CategoryName.Should().Be("Merchants");
    }

    [Fact]
    public async Task GetAllContactsWithDetailsAsync_UnassignedContact_ReturnsNullCategoryName()
    {
        // Arrange: a contact with no category assigned
        var groupContext = new MutableTestGroupContext { ActiveGroupId = null };
        await using var context = CreateContext("ContactRepositoryTests." + nameof(GetAllContactsWithDetailsAsync_UnassignedContact_ReturnsNullCategoryName), groupContext);

        context.Groups.Add(new GroupEntity { Id = 1, Name = "Group One" });
        context.UserEntities.Add(new UserEntity { Id = 1, Name = "Creator One", Email = "creator1@test.com" });
        context.Contacts.Add(new ContactEntity { Id = 1, Name = "Unassigned Contact", GroupId = 1, CreatedByUserId = 1, CreatedAt = DateTime.UtcNow });
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var repository = new ContactRepository(context, CreateMapper());
        groupContext.ActiveGroupId = 1;

        // Act
        var contacts = await repository.GetAllContactsWithDetailsAsync(TestContext.Current.CancellationToken);

        // Assert
        contacts.Should().ContainSingle();
        contacts[0].CategoryName.Should().BeNull();
    }

    [Fact]
    public async Task ReplaceContactTagsAsync_NewNames_CreatesTagRowsAndAssociations()
    {
        // Arrange: a contact with no tags yet
        var groupContext = new MutableTestGroupContext { ActiveGroupId = null };
        var dbName = "ContactRepositoryTests." + nameof(ReplaceContactTagsAsync_NewNames_CreatesTagRowsAndAssociations);
        await using (var context = CreateContext(dbName, groupContext))
        {
            context.Groups.Add(new GroupEntity { Id = 1, Name = "Group One" });
            context.UserEntities.Add(new UserEntity { Id = 1, Name = "Creator One", Email = "creator1@test.com" });
            context.Contacts.Add(new ContactEntity { Id = 1, Name = "Tagged Contact", GroupId = 1, CreatedByUserId = 1, CreatedAt = DateTime.UtcNow });
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);

            var repository = new ContactRepository(context, CreateMapper());
            groupContext.ActiveGroupId = 1;

            // Act
            await repository.ReplaceContactTagsAsync(1, ["shopkeeper", "quest giver"], TestContext.Current.CancellationToken);
        }

        // Assert: read through a fresh context scope, not the one that just wrote
        groupContext.ActiveGroupId = 1;
        await using (var context = CreateContext(dbName, groupContext))
        {
            var tags = await context.ContactTags.ToListAsync(TestContext.Current.CancellationToken);
            tags.Should().HaveCount(2);
            tags.Select(t => t.Name).Should().BeEquivalentTo(["shopkeeper", "quest giver"]);
        }
    }

    [Fact]
    public async Task ReplaceContactTagsAsync_CaseVariantOfExistingName_ReusesRow()
    {
        // Arrange: an existing "shopkeeper" tag on one contact, and a second, still-untagged
        // contact on the same board. The in-memory provider does not enforce the unique index
        // or the column collation, so this proves the app-level case-insensitive reuse works by
        // construction rather than by relying on a constraint violation.
        var groupContext = new MutableTestGroupContext { ActiveGroupId = null };
        var dbName = "ContactRepositoryTests." + nameof(ReplaceContactTagsAsync_CaseVariantOfExistingName_ReusesRow);
        int existingTagId;
        await using (var context = CreateContext(dbName, groupContext))
        {
            context.Groups.Add(new GroupEntity { Id = 1, Name = "Group One" });
            context.UserEntities.Add(new UserEntity { Id = 1, Name = "Creator One", Email = "creator1@test.com" });
            context.Contacts.Add(new ContactEntity { Id = 1, Name = "Contact A", GroupId = 1, CreatedByUserId = 1, CreatedAt = DateTime.UtcNow });
            context.Contacts.Add(new ContactEntity { Id = 2, Name = "Contact B", GroupId = 1, CreatedByUserId = 1, CreatedAt = DateTime.UtcNow });
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);

            var repository = new ContactRepository(context, CreateMapper());
            groupContext.ActiveGroupId = 1;
            await repository.ReplaceContactTagsAsync(1, ["shopkeeper"], TestContext.Current.CancellationToken);
            existingTagId = await context.ContactTags.Select(t => t.Id).SingleAsync(TestContext.Current.CancellationToken);

            // Act: submit a case-variant name on the second contact
            await repository.ReplaceContactTagsAsync(2, ["Shopkeeper"], TestContext.Current.CancellationToken);
        }

        // Assert: still exactly one tag row on the board, reused rather than duplicated
        groupContext.ActiveGroupId = 1;
        await using (var context = CreateContext(dbName, groupContext))
        {
            var tags = await context.ContactTags.ToListAsync(TestContext.Current.CancellationToken);
            tags.Should().ContainSingle();
            tags[0].Id.Should().Be(existingTagId);
        }
    }

    [Fact]
    public async Task ReplaceContactTagsAsync_DuplicateCaseVariantsInSameSubmission_CreatesSingleRow()
    {
        // Arrange: a contact with no tags yet
        var groupContext = new MutableTestGroupContext { ActiveGroupId = null };
        var dbName = "ContactRepositoryTests." + nameof(ReplaceContactTagsAsync_DuplicateCaseVariantsInSameSubmission_CreatesSingleRow);
        await using (var context = CreateContext(dbName, groupContext))
        {
            context.Groups.Add(new GroupEntity { Id = 1, Name = "Group One" });
            context.UserEntities.Add(new UserEntity { Id = 1, Name = "Creator One", Email = "creator1@test.com" });
            context.Contacts.Add(new ContactEntity { Id = 1, Name = "Contact A", GroupId = 1, CreatedByUserId = 1, CreatedAt = DateTime.UtcNow });
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);

            var repository = new ContactRepository(context, CreateMapper());
            groupContext.ActiveGroupId = 1;

            // Act: three case variants of the same name submitted together in one call
            await repository.ReplaceContactTagsAsync(1, ["shopkeeper", "Shopkeeper", "SHOPKEEPER"], TestContext.Current.CancellationToken);
        }

        // Assert: exactly one tag row and one association
        groupContext.ActiveGroupId = 1;
        await using (var context = CreateContext(dbName, groupContext))
        {
            var tags = await context.ContactTags.ToListAsync(TestContext.Current.CancellationToken);
            tags.Should().ContainSingle();

            var contact = await context.Contacts.Include(c => c.Tags).FirstAsync(c => c.Id == 1, TestContext.Current.CancellationToken);
            contact.Tags.Should().ContainSingle();
        }
    }

    [Fact]
    public async Task ReplaceContactTagsAsync_LastContactDropsTag_DeletesRow()
    {
        // Arrange: a single contact holding a single tag
        var groupContext = new MutableTestGroupContext { ActiveGroupId = null };
        var dbName = "ContactRepositoryTests." + nameof(ReplaceContactTagsAsync_LastContactDropsTag_DeletesRow);
        await using (var context = CreateContext(dbName, groupContext))
        {
            context.Groups.Add(new GroupEntity { Id = 1, Name = "Group One" });
            context.UserEntities.Add(new UserEntity { Id = 1, Name = "Creator One", Email = "creator1@test.com" });
            context.Contacts.Add(new ContactEntity { Id = 1, Name = "Solo Holder", GroupId = 1, CreatedByUserId = 1, CreatedAt = DateTime.UtcNow });
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);

            var repository = new ContactRepository(context, CreateMapper());
            groupContext.ActiveGroupId = 1;
            await repository.ReplaceContactTagsAsync(1, ["temporary"], TestContext.Current.CancellationToken);

            // Act: drop the tag entirely
            await repository.ReplaceContactTagsAsync(1, [], TestContext.Current.CancellationToken);
        }

        // Assert: the tag row is gone from the database, not just detached from the contact
        groupContext.ActiveGroupId = 1;
        await using (var context = CreateContext(dbName, groupContext))
        {
            var tags = await context.ContactTags.ToListAsync(TestContext.Current.CancellationToken);
            tags.Should().BeEmpty();
        }
    }

    [Fact]
    public async Task ReplaceContactTagsAsync_TagStillHeldByAnotherContact_SurvivesPrune()
    {
        // Arrange: two contacts sharing one tag
        var groupContext = new MutableTestGroupContext { ActiveGroupId = null };
        var dbName = "ContactRepositoryTests." + nameof(ReplaceContactTagsAsync_TagStillHeldByAnotherContact_SurvivesPrune);
        await using (var context = CreateContext(dbName, groupContext))
        {
            context.Groups.Add(new GroupEntity { Id = 1, Name = "Group One" });
            context.UserEntities.Add(new UserEntity { Id = 1, Name = "Creator One", Email = "creator1@test.com" });
            context.Contacts.Add(new ContactEntity { Id = 1, Name = "Contact A", GroupId = 1, CreatedByUserId = 1, CreatedAt = DateTime.UtcNow });
            context.Contacts.Add(new ContactEntity { Id = 2, Name = "Contact B", GroupId = 1, CreatedByUserId = 1, CreatedAt = DateTime.UtcNow });
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);

            var repository = new ContactRepository(context, CreateMapper());
            groupContext.ActiveGroupId = 1;
            await repository.ReplaceContactTagsAsync(1, ["shared"], TestContext.Current.CancellationToken);
            await repository.ReplaceContactTagsAsync(2, ["shared"], TestContext.Current.CancellationToken);

            // Act: contact A drops the tag, but contact B still holds it
            await repository.ReplaceContactTagsAsync(1, [], TestContext.Current.CancellationToken);
        }

        // Assert: the tag row survives because contact B still references it
        groupContext.ActiveGroupId = 1;
        await using (var context = CreateContext(dbName, groupContext))
        {
            var tags = await context.ContactTags.ToListAsync(TestContext.Current.CancellationToken);
            tags.Should().ContainSingle();

            var contactB = await context.Contacts.Include(c => c.Tags).FirstAsync(c => c.Id == 2, TestContext.Current.CancellationToken);
            contactB.Tags.Should().ContainSingle(t => t.Name == "shared");
        }
    }

    [Fact]
    public async Task ReplaceContactTagsAsync_ContactOnAnotherBoard_IsSilentNoOp()
    {
        // Arrange: contact 2 belongs to group 2, viewer's active group is 1
        var groupContext = new MutableTestGroupContext { ActiveGroupId = null };
        var dbName = "ContactRepositoryTests." + nameof(ReplaceContactTagsAsync_ContactOnAnotherBoard_IsSilentNoOp);
        await using (var context = CreateContext(dbName, groupContext))
        {
            await SeedTwoGroupContactsAsync(context, groupContext);

            var repository = new ContactRepository(context, CreateMapper());
            groupContext.ActiveGroupId = 1;

            // Act: a cross-board contact id is a silent no-op -- no exception, nothing created
            var act = () => repository.ReplaceContactTagsAsync(2, ["intruder"], TestContext.Current.CancellationToken);
            await act.Should().NotThrowAsync();
        }

        // Assert: nothing was created on either board
        groupContext.ActiveGroupId = null; // see-all
        await using (var context = CreateContext(dbName, groupContext))
        {
            var tags = await context.ContactTags.ToListAsync(TestContext.Current.CancellationToken);
            tags.Should().BeEmpty();
        }
    }

    [Fact]
    public async Task ReplaceContactTagsAsync_ReAddingPrunedName_MintsNewId()
    {
        // Arrange: a tag created, then pruned back to zero contacts
        var groupContext = new MutableTestGroupContext { ActiveGroupId = null };
        var dbName = "ContactRepositoryTests." + nameof(ReplaceContactTagsAsync_ReAddingPrunedName_MintsNewId);
        int prunedTagId;
        await using (var context = CreateContext(dbName, groupContext))
        {
            context.Groups.Add(new GroupEntity { Id = 1, Name = "Group One" });
            context.UserEntities.Add(new UserEntity { Id = 1, Name = "Creator One", Email = "creator1@test.com" });
            context.Contacts.Add(new ContactEntity { Id = 1, Name = "Solo Holder", GroupId = 1, CreatedByUserId = 1, CreatedAt = DateTime.UtcNow });
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);

            var repository = new ContactRepository(context, CreateMapper());
            groupContext.ActiveGroupId = 1;
            await repository.ReplaceContactTagsAsync(1, ["reborn"], TestContext.Current.CancellationToken);
            prunedTagId = await context.ContactTags.Select(t => t.Id).SingleAsync(TestContext.Current.CancellationToken);
            await repository.ReplaceContactTagsAsync(1, [], TestContext.Current.CancellationToken);

            // Act: re-type the same name that was just pruned
            await repository.ReplaceContactTagsAsync(1, ["reborn"], TestContext.Current.CancellationToken);
        }

        // Assert: a fresh row with a different id, not the pruned one resurrected
        groupContext.ActiveGroupId = 1;
        await using (var context = CreateContext(dbName, groupContext))
        {
            var tags = await context.ContactTags.ToListAsync(TestContext.Current.CancellationToken);
            tags.Should().ContainSingle();
            tags[0].Id.Should().NotBe(prunedTagId);
        }
    }

    [Fact]
    public async Task RemoveAsync_ContactWasSoleTagHolder_PrunesTag()
    {
        // Arrange: a contact holding a tag no other contact holds
        var groupContext = new MutableTestGroupContext { ActiveGroupId = null };
        var dbName = "ContactRepositoryTests." + nameof(RemoveAsync_ContactWasSoleTagHolder_PrunesTag);
        await using (var context = CreateContext(dbName, groupContext))
        {
            context.Groups.Add(new GroupEntity { Id = 1, Name = "Group One" });
            context.UserEntities.Add(new UserEntity { Id = 1, Name = "Creator One", Email = "creator1@test.com" });
            context.Contacts.Add(new ContactEntity { Id = 1, Name = "Solo Holder", GroupId = 1, CreatedByUserId = 1, CreatedAt = DateTime.UtcNow });
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);

            var repository = new ContactRepository(context, CreateMapper());
            groupContext.ActiveGroupId = 1;
            await repository.ReplaceContactTagsAsync(1, ["disposable"], TestContext.Current.CancellationToken);
            var contact = await repository.GetContactWithDetailsAsync(1, TestContext.Current.CancellationToken);

            // Act: delete the contact that was the tag's only holder
            await repository.RemoveAsync(contact!, TestContext.Current.CancellationToken);
        }

        // Assert: the tag row is gone along with the contact
        groupContext.ActiveGroupId = 1;
        await using (var context = CreateContext(dbName, groupContext))
        {
            var tags = await context.ContactTags.ToListAsync(TestContext.Current.CancellationToken);
            tags.Should().BeEmpty();
            var contacts = await context.Contacts.ToListAsync(TestContext.Current.CancellationToken);
            contacts.Should().BeEmpty();
        }
    }

    [Fact]
    public async Task RemoveAsync_ContactTagsSharedWithAnotherContact_SurvivesDelete()
    {
        // Arrange: two contacts sharing one tag
        var groupContext = new MutableTestGroupContext { ActiveGroupId = null };
        var dbName = "ContactRepositoryTests." + nameof(RemoveAsync_ContactTagsSharedWithAnotherContact_SurvivesDelete);
        await using (var context = CreateContext(dbName, groupContext))
        {
            context.Groups.Add(new GroupEntity { Id = 1, Name = "Group One" });
            context.UserEntities.Add(new UserEntity { Id = 1, Name = "Creator One", Email = "creator1@test.com" });
            context.Contacts.Add(new ContactEntity { Id = 1, Name = "Contact A", GroupId = 1, CreatedByUserId = 1, CreatedAt = DateTime.UtcNow });
            context.Contacts.Add(new ContactEntity { Id = 2, Name = "Contact B", GroupId = 1, CreatedByUserId = 1, CreatedAt = DateTime.UtcNow });
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);

            var repository = new ContactRepository(context, CreateMapper());
            groupContext.ActiveGroupId = 1;
            await repository.ReplaceContactTagsAsync(1, ["shared"], TestContext.Current.CancellationToken);
            await repository.ReplaceContactTagsAsync(2, ["shared"], TestContext.Current.CancellationToken);
            var contactA = await repository.GetContactWithDetailsAsync(1, TestContext.Current.CancellationToken);

            // Act: delete contact A, which is not the tag's only holder
            await repository.RemoveAsync(contactA!, TestContext.Current.CancellationToken);
        }

        // Assert: the tag survives because contact B still references it
        groupContext.ActiveGroupId = 1;
        await using (var context = CreateContext(dbName, groupContext))
        {
            var tags = await context.ContactTags.ToListAsync(TestContext.Current.CancellationToken);
            tags.Should().ContainSingle();
        }
    }

    private sealed class MutableTestGroupContext : IActiveGroupContext
    {
        public int? ActiveGroupId { get; set; }
    }
}
