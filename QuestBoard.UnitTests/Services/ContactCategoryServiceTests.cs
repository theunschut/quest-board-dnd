using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using QuestBoard.Domain.Interfaces;
using QuestBoard.Domain.Models;
using QuestBoard.Domain.Services;
using QuestBoard.Repository;
using QuestBoard.Repository.Entities;

namespace QuestBoard.UnitTests.Services;

// Service-level coverage for contact categories: end-append stamping the next sort position,
// both reorder directions with their boundary (no-op) cases, and delete delegation.
public class ContactCategoryServiceTests
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

    private static async Task SeedThreeCategoriesAsync(QuestBoardContext context, MutableTestGroupContext groupContext)
    {
        var originalActiveGroupId = groupContext.ActiveGroupId;
        groupContext.ActiveGroupId = null; // see-all during seeding

        context.Groups.Add(new GroupEntity { Id = 1, Name = "Group One" });
        context.ContactCategories.Add(new ContactCategoryEntity { Id = 1, Name = "First", SortOrder = 0, GroupId = 1 });
        context.ContactCategories.Add(new ContactCategoryEntity { Id = 2, Name = "Second", SortOrder = 1, GroupId = 1 });
        context.ContactCategories.Add(new ContactCategoryEntity { Id = 3, Name = "Third", SortOrder = 2, GroupId = 1 });
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        groupContext.ActiveGroupId = originalActiveGroupId;
    }

    [Fact]
    public async Task AddToEndAsync_StampsNextSortPositionBeforePersisting()
    {
        // Arrange
        var groupContext = new MutableTestGroupContext { ActiveGroupId = null };
        await using var context = CreateContext("ContactCategoryServiceTests." + nameof(AddToEndAsync_StampsNextSortPositionBeforePersisting), groupContext);
        await SeedThreeCategoriesAsync(context, groupContext);

        var mapper = CreateMapper();
        var repository = new ContactCategoryRepository(context, mapper);
        var service = new ContactCategoryService(repository, mapper);
        groupContext.ActiveGroupId = 1;

        var newCategory = new ContactCategory { Name = "Fourth", GroupId = 1 };

        // Act: no SortOrder set by the caller -- the service stamps it
        await service.AddToEndAsync(newCategory, TestContext.Current.CancellationToken);

        // Assert: the new category lands after every existing category on its board
        var ordered = await service.GetOrderedAsync(TestContext.Current.CancellationToken);
        ordered.Select(c => c.Name).Should().ContainInConsecutiveOrder("First", "Second", "Third", "Fourth");
        ordered.Last().SortOrder.Should().Be(3);
    }

    [Fact]
    public async Task MoveUpAsync_FirstCategory_ReportsNoMove()
    {
        // Arrange
        var groupContext = new MutableTestGroupContext { ActiveGroupId = null };
        await using var context = CreateContext("ContactCategoryServiceTests." + nameof(MoveUpAsync_FirstCategory_ReportsNoMove), groupContext);
        await SeedThreeCategoriesAsync(context, groupContext);

        var mapper = CreateMapper();
        var repository = new ContactCategoryRepository(context, mapper);
        var service = new ContactCategoryService(repository, mapper);
        groupContext.ActiveGroupId = 1;

        // Act: category 1 is already first
        var moved = await service.MoveUpAsync(1, TestContext.Current.CancellationToken);

        // Assert: no move reported and the order is unchanged
        moved.Should().BeFalse();
        var ordered = await service.GetOrderedAsync(TestContext.Current.CancellationToken);
        ordered.Select(c => c.Name).Should().ContainInConsecutiveOrder("First", "Second", "Third");
    }

    [Fact]
    public async Task MoveDownAsync_LastCategory_ReportsNoMove()
    {
        // Arrange
        var groupContext = new MutableTestGroupContext { ActiveGroupId = null };
        await using var context = CreateContext("ContactCategoryServiceTests." + nameof(MoveDownAsync_LastCategory_ReportsNoMove), groupContext);
        await SeedThreeCategoriesAsync(context, groupContext);

        var mapper = CreateMapper();
        var repository = new ContactCategoryRepository(context, mapper);
        var service = new ContactCategoryService(repository, mapper);
        groupContext.ActiveGroupId = 1;

        // Act: category 3 is already last
        var moved = await service.MoveDownAsync(3, TestContext.Current.CancellationToken);

        // Assert: no move reported and the order is unchanged
        moved.Should().BeFalse();
        var ordered = await service.GetOrderedAsync(TestContext.Current.CancellationToken);
        ordered.Select(c => c.Name).Should().ContainInConsecutiveOrder("First", "Second", "Third");
    }

    [Fact]
    public async Task MoveUpAsync_MiddleCategory_SwapsWithPredecessor()
    {
        // Arrange
        var groupContext = new MutableTestGroupContext { ActiveGroupId = null };
        await using var context = CreateContext("ContactCategoryServiceTests." + nameof(MoveUpAsync_MiddleCategory_SwapsWithPredecessor), groupContext);
        await SeedThreeCategoriesAsync(context, groupContext);

        var mapper = CreateMapper();
        var repository = new ContactCategoryRepository(context, mapper);
        var service = new ContactCategoryService(repository, mapper);
        groupContext.ActiveGroupId = 1;

        // Act: category 2 ("Second") moves up, exchanging with category 1 ("First")
        var moved = await service.MoveUpAsync(2, TestContext.Current.CancellationToken);

        // Assert
        moved.Should().BeTrue();
        var ordered = await service.GetOrderedAsync(TestContext.Current.CancellationToken);
        ordered.Select(c => c.Name).Should().ContainInConsecutiveOrder("Second", "First", "Third");
    }

    [Fact]
    public async Task MoveDownAsync_MiddleCategory_SwapsWithSuccessor()
    {
        // Arrange
        var groupContext = new MutableTestGroupContext { ActiveGroupId = null };
        await using var context = CreateContext("ContactCategoryServiceTests." + nameof(MoveDownAsync_MiddleCategory_SwapsWithSuccessor), groupContext);
        await SeedThreeCategoriesAsync(context, groupContext);

        var mapper = CreateMapper();
        var repository = new ContactCategoryRepository(context, mapper);
        var service = new ContactCategoryService(repository, mapper);
        groupContext.ActiveGroupId = 1;

        // Act: category 2 ("Second") moves down, exchanging with category 3 ("Third")
        var moved = await service.MoveDownAsync(2, TestContext.Current.CancellationToken);

        // Assert
        moved.Should().BeTrue();
        var ordered = await service.GetOrderedAsync(TestContext.Current.CancellationToken);
        ordered.Select(c => c.Name).Should().ContainInConsecutiveOrder("First", "Third", "Second");
    }

    [Fact]
    public async Task DeleteAsync_RemovesCategoryAndOrphansItsContacts()
    {
        // Arrange: category 1 holds a contact
        var groupContext = new MutableTestGroupContext { ActiveGroupId = null };
        await using var context = CreateContext("ContactCategoryServiceTests." + nameof(DeleteAsync_RemovesCategoryAndOrphansItsContacts), groupContext);
        await SeedThreeCategoriesAsync(context, groupContext);

        groupContext.ActiveGroupId = null;
        context.UserEntities.Add(new UserEntity { Id = 1, Name = "Creator One", Email = "creator1@test.com" });
        context.Contacts.Add(new ContactEntity { Id = 1, Name = "Assigned Contact", GroupId = 1, CreatedByUserId = 1, CategoryId = 1, CreatedAt = DateTime.UtcNow });
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var mapper = CreateMapper();
        var repository = new ContactCategoryRepository(context, mapper);
        var service = new ContactCategoryService(repository, mapper);
        groupContext.ActiveGroupId = 1;

        // Act: delegates through to the delete-with-dependents-loaded repository path
        await service.DeleteAsync(1, TestContext.Current.CancellationToken);

        // Assert: category gone, contact survives with no category reference
        var ordered = await service.GetOrderedAsync(TestContext.Current.CancellationToken);
        ordered.Select(c => c.Id).Should().NotContain(1);

        var contactRepository = new ContactRepository(context, mapper);
        var contact = await contactRepository.GetContactWithDetailsAsync(1, TestContext.Current.CancellationToken);
        contact.Should().NotBeNull();
        contact!.CategoryId.Should().BeNull();
    }

    private sealed class MutableTestGroupContext : IActiveGroupContext
    {
        public int? ActiveGroupId { get; set; }
    }
}
