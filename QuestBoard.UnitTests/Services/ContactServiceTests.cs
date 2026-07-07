using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using QuestBoard.Domain.Interfaces;
using QuestBoard.Domain.Services;
using QuestBoard.Repository;
using QuestBoard.Repository.Entities;

namespace QuestBoard.UnitTests.Services;

public class ContactServiceTests
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

    // Seeds one group, a creator user, and a single contact with both an original and a
    // cropped image already set, so preserve/clear behaviour can be asserted against a
    // known starting state.
    private static async Task SeedContactWithImagesAsync(QuestBoardContext context, MutableTestGroupContext groupContext, byte[] originalBytes, byte[] croppedBytes)
    {
        var originalActiveGroupId = groupContext.ActiveGroupId;
        groupContext.ActiveGroupId = null; // see-all during seeding

        context.Groups.Add(new GroupEntity { Id = 1, Name = "Group One" });
        context.UserEntities.Add(new UserEntity { Id = 1, Name = "Creator One", Email = "creator1@test.com" });

        context.Contacts.Add(new ContactEntity
        {
            Id = 1,
            Name = "Test Contact",
            GroupId = 1,
            CreatedByUserId = 1,
            IsRevealed = true,
            CreatedAt = DateTime.UtcNow,
            ProfileImage = new ContactImageEntity { Id = 1, OriginalImageData = originalBytes, CroppedImageData = croppedBytes }
        });

        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        groupContext.ActiveGroupId = originalActiveGroupId;
    }

    [Fact]
    public async Task UpdateAsync_NoNewUpload_PreservesExistingCroppedImage()
    {
        // Arrange: seed a contact with original A + crop A
        var groupContext = new MutableTestGroupContext { ActiveGroupId = null };
        await using var context = CreateContext("ContactServiceTests." + nameof(UpdateAsync_NoNewUpload_PreservesExistingCroppedImage), groupContext);
        await SeedContactWithImagesAsync(context, groupContext, [1, 2, 3], [9, 9, 9]);

        var mapper = CreateMapper();
        var repository = new ContactRepository(context, mapper);
        var service = new ContactService(repository, mapper);
        groupContext.ActiveGroupId = 1;

        var contact = await repository.GetContactWithDetailsAsync(1, TestContext.Current.CancellationToken);
        contact.Should().NotBeNull();
        contact!.Description = "Updated description"; // unrelated-field edit
        contact.ContactImageData = [1, 2, 3]; // round-tripped existing original, never null on a no-photo-change edit

        // Act: no new photo uploaded this request
        await service.UpdateAsync(contact, hasNewOriginalUpload: false, TestContext.Current.CancellationToken);

        // Assert: the stored crop survives, only the unrelated field actually changed
        var original = await repository.GetContactOriginalImageAsync(1, TestContext.Current.CancellationToken);
        var cropped = await repository.GetContactCroppedImageAsync(1, TestContext.Current.CancellationToken);
        original.Should().Equal([1, 2, 3]);
        cropped.Should().Equal([9, 9, 9]);

        var updated = await repository.GetContactWithDetailsAsync(1, TestContext.Current.CancellationToken);
        updated!.Description.Should().Be("Updated description");
    }

    [Fact]
    public async Task UpdateAsync_NewOriginalUpload_ClearsStaleCroppedImage()
    {
        // Arrange: seed a contact with original A + crop A
        var groupContext = new MutableTestGroupContext { ActiveGroupId = null };
        await using var context = CreateContext("ContactServiceTests." + nameof(UpdateAsync_NewOriginalUpload_ClearsStaleCroppedImage), groupContext);
        await SeedContactWithImagesAsync(context, groupContext, [1, 2, 3], [9, 9, 9]);

        var mapper = CreateMapper();
        var repository = new ContactRepository(context, mapper);
        var service = new ContactService(repository, mapper);
        groupContext.ActiveGroupId = 1;

        var contact = await repository.GetContactWithDetailsAsync(1, TestContext.Current.CancellationToken);
        contact.Should().NotBeNull();
        contact!.ContactImageData = [70, 71, 72]; // a genuinely new original (B) uploaded this request

        // Act
        await service.UpdateAsync(contact, hasNewOriginalUpload: true, TestContext.Current.CancellationToken);

        // Assert: cropped read falls back to B's original, NOT A's stale crop -- proven
        // through the real service call path, not a direct repository call.
        var cropped = await repository.GetContactCroppedImageAsync(1, TestContext.Current.CancellationToken);
        cropped.Should().Equal([70, 71, 72]);
    }

    [Fact]
    public async Task UpdateAsync_NewCropSupplied_PersistsCrop()
    {
        // Arrange: seed a contact with original A + crop A
        var groupContext = new MutableTestGroupContext { ActiveGroupId = null };
        await using var context = CreateContext("ContactServiceTests." + nameof(UpdateAsync_NewCropSupplied_PersistsCrop), groupContext);
        await SeedContactWithImagesAsync(context, groupContext, [1, 2, 3], [9, 9, 9]);

        var mapper = CreateMapper();
        var repository = new ContactRepository(context, mapper);
        var service = new ContactService(repository, mapper);
        groupContext.ActiveGroupId = 1;

        var contact = await repository.GetContactWithDetailsAsync(1, TestContext.Current.CancellationToken);
        contact.Should().NotBeNull();
        contact!.ContactImageData = [70, 71, 72]; // a genuinely new original (B) uploaded this request

        // Act: caller supplies a real crop of the new original alongside the upload
        await service.UpdateAsync(contact, hasNewOriginalUpload: true, newCroppedImageData: [200, 201, 202], TestContext.Current.CancellationToken);

        // Assert: the supplied crop is persisted directly, not cleared and not re-derived
        var cropped = await repository.GetContactCroppedImageAsync(1, TestContext.Current.CancellationToken);
        cropped.Should().Equal([200, 201, 202]);
    }

    [Fact]
    public async Task UpdateAsync_NoNewFile_RefetchesAndPassesThroughExistingCrop()
    {
        // Arrange: seed a contact with original A + crop A
        var groupContext = new MutableTestGroupContext { ActiveGroupId = null };
        await using var context = CreateContext("ContactServiceTests." + nameof(UpdateAsync_NoNewFile_RefetchesAndPassesThroughExistingCrop), groupContext);
        await SeedContactWithImagesAsync(context, groupContext, [1, 2, 3], [9, 9, 9]);

        var mapper = CreateMapper();
        var repository = new ContactRepository(context, mapper);
        var service = new ContactService(repository, mapper);
        groupContext.ActiveGroupId = 1;

        var contact = await repository.GetContactWithDetailsAsync(1, TestContext.Current.CancellationToken);
        contact.Should().NotBeNull();
        contact!.Description = "Another unrelated edit"; // unrelated-field edit
        contact.ContactImageData = [1, 2, 3]; // round-tripped existing original, never null on a no-photo-change edit

        // Act: no new file, no new crop supplied -- 4-arg overload with null crop
        await service.UpdateAsync(contact, hasNewOriginalUpload: false, newCroppedImageData: null, TestContext.Current.CancellationToken);

        // Assert: the previously-stored crop (fetched via GetContactCroppedImageAsync) is
        // passed through unchanged
        var cropped = await repository.GetContactCroppedImageAsync(1, TestContext.Current.CancellationToken);
        cropped.Should().Equal([9, 9, 9]);
    }

    [Fact]
    public async Task AddAsync_NewCropSupplied_PersistsCrop()
    {
        // Arrange: a fresh group/creator, no contact yet
        var groupContext = new MutableTestGroupContext { ActiveGroupId = null };
        await using var context = CreateContext("ContactServiceTests." + nameof(AddAsync_NewCropSupplied_PersistsCrop), groupContext);

        context.Groups.Add(new GroupEntity { Id = 1, Name = "Group One" });
        context.UserEntities.Add(new UserEntity { Id = 1, Name = "Creator One", Email = "creator1@test.com" });
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var mapper = CreateMapper();
        var repository = new ContactRepository(context, mapper);
        var service = new ContactService(repository, mapper);
        groupContext.ActiveGroupId = 1;

        var contact = new QuestBoard.Domain.Models.Contact
        {
            Name = "Newly Created Contact",
            GroupId = 1,
            CreatedByUserId = 1,
            ContactImageData = [1, 2, 3],
        };

        // Act: Create with a crop submitted alongside the original -- mirrors the real
        // controller flow, where the AutoMapper-built Contact never has CreatedByUser
        // populated (only CreatedByUserId), since ViewModels never carry that navigation.
        await service.AddAsync(contact, newCroppedImageData: [200, 201, 202], TestContext.Current.CancellationToken);

        // Assert: the supplied crop is persisted, not the original and not null
        var original = await repository.GetContactOriginalImageAsync(contact.Id, TestContext.Current.CancellationToken);
        var cropped = await repository.GetContactCroppedImageAsync(contact.Id, TestContext.Current.CancellationToken);
        original.Should().Equal([1, 2, 3]);
        cropped.Should().Equal([200, 201, 202]);
    }

    [Fact]
    public async Task AddAsync_NoCropSupplied_FallsBackToOriginal()
    {
        // Arrange: a fresh group/creator, no contact yet
        var groupContext = new MutableTestGroupContext { ActiveGroupId = null };
        await using var context = CreateContext("ContactServiceTests." + nameof(AddAsync_NoCropSupplied_FallsBackToOriginal), groupContext);

        context.Groups.Add(new GroupEntity { Id = 1, Name = "Group One" });
        context.UserEntities.Add(new UserEntity { Id = 1, Name = "Creator One", Email = "creator1@test.com" });
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var mapper = CreateMapper();
        var repository = new ContactRepository(context, mapper);
        var service = new ContactService(repository, mapper);
        groupContext.ActiveGroupId = 1;

        var contact = new QuestBoard.Domain.Models.Contact
        {
            Name = "Newly Created Contact",
            GroupId = 1,
            CreatedByUserId = 1,
            ContactImageData = [1, 2, 3],
        };

        // Act: Create with no crop submitted -- identical to today's plain Create
        await service.AddAsync(contact, newCroppedImageData: null, TestContext.Current.CancellationToken);

        // Assert: the cropped read-back falls back to the original, no spurious second write
        var cropped = await repository.GetContactCroppedImageAsync(contact.Id, TestContext.Current.CancellationToken);
        cropped.Should().Equal([1, 2, 3]);
    }

    private sealed class MutableTestGroupContext : IActiveGroupContext
    {
        public int? ActiveGroupId { get; set; }
    }
}
