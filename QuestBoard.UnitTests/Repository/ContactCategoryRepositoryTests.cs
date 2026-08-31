using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using QuestBoard.Domain.Interfaces;
using QuestBoard.Repository;
using QuestBoard.Repository.Entities;

namespace QuestBoard.UnitTests.Repository;

// Repository-level coverage for contact categories: ordering (with its Id tie-break),
// end-append, manual reordering, contact counting, and the delete-with-dependents-loaded
// orphaning path.
public class ContactCategoryRepositoryTests
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

    private static async Task SeedGroupAsync(QuestBoardContext context, MutableTestGroupContext groupContext, int groupId = 1)
    {
        var originalActiveGroupId = groupContext.ActiveGroupId;
        groupContext.ActiveGroupId = null; // see-all during seeding

        context.Groups.Add(new GroupEntity { Id = groupId, Name = $"Group {groupId}" });
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        groupContext.ActiveGroupId = originalActiveGroupId;
    }

    [Fact]
    public async Task GetOrderedForActiveGroupAsync_OutOfOrderPositions_ReturnsSortedBySortOrder()
    {
        // Arrange: seed positions deliberately out of order — the ordered read is sorted by
        // SortOrder, not insertion/Id order.
        var groupContext = new MutableTestGroupContext { ActiveGroupId = null };
        await using var context = CreateContext("ContactCategoryRepositoryTests." + nameof(GetOrderedForActiveGroupAsync_OutOfOrderPositions_ReturnsSortedBySortOrder), groupContext);
        await SeedGroupAsync(context, groupContext);

        groupContext.ActiveGroupId = null;
        context.ContactCategories.Add(new ContactCategoryEntity { Id = 1, Name = "Merchants", SortOrder = 2, GroupId = 1 });
        context.ContactCategories.Add(new ContactCategoryEntity { Id = 2, Name = "Nobles", SortOrder = 0, GroupId = 1 });
        context.ContactCategories.Add(new ContactCategoryEntity { Id = 3, Name = "Guards", SortOrder = 1, GroupId = 1 });
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var repository = new ContactCategoryRepository(context, CreateMapper());
        groupContext.ActiveGroupId = 1;

        // Act
        var categories = await repository.GetOrderedForActiveGroupAsync(TestContext.Current.CancellationToken);

        // Assert
        categories.Select(c => c.Name).Should().ContainInConsecutiveOrder("Nobles", "Guards", "Merchants");
    }

    [Fact]
    public async Task GetOrderedForActiveGroupAsync_TiedSortOrder_ResolvesByAscendingId()
    {
        // Arrange: two categories share a position — the tie-break is ascending Id (creation order).
        var groupContext = new MutableTestGroupContext { ActiveGroupId = null };
        await using var context = CreateContext("ContactCategoryRepositoryTests." + nameof(GetOrderedForActiveGroupAsync_TiedSortOrder_ResolvesByAscendingId), groupContext);
        await SeedGroupAsync(context, groupContext);

        groupContext.ActiveGroupId = null;
        context.ContactCategories.Add(new ContactCategoryEntity { Id = 5, Name = "Later Created", SortOrder = 0, GroupId = 1 });
        context.ContactCategories.Add(new ContactCategoryEntity { Id = 2, Name = "Earlier Created", SortOrder = 0, GroupId = 1 });
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var repository = new ContactCategoryRepository(context, CreateMapper());
        groupContext.ActiveGroupId = 1;

        // Act
        var categories = await repository.GetOrderedForActiveGroupAsync(TestContext.Current.CancellationToken);

        // Assert: ascending Id, not insertion order
        categories.Select(c => c.Id).Should().ContainInConsecutiveOrder(2, 5);
    }

    [Fact]
    public async Task GetNextSortOrderAsync_EmptyBoard_ReturnsZero()
    {
        // Arrange
        var groupContext = new MutableTestGroupContext { ActiveGroupId = null };
        await using var context = CreateContext("ContactCategoryRepositoryTests." + nameof(GetNextSortOrderAsync_EmptyBoard_ReturnsZero), groupContext);
        await SeedGroupAsync(context, groupContext);

        var repository = new ContactCategoryRepository(context, CreateMapper());
        groupContext.ActiveGroupId = 1;

        // Act
        var nextSortOrder = await repository.GetNextSortOrderAsync(TestContext.Current.CancellationToken);

        // Assert
        nextSortOrder.Should().Be(0);
    }

    [Fact]
    public async Task GetNextSortOrderAsync_ExistingCategories_ReturnsMaxPlusOne()
    {
        // Arrange
        var groupContext = new MutableTestGroupContext { ActiveGroupId = null };
        await using var context = CreateContext("ContactCategoryRepositoryTests." + nameof(GetNextSortOrderAsync_ExistingCategories_ReturnsMaxPlusOne), groupContext);
        await SeedGroupAsync(context, groupContext);

        groupContext.ActiveGroupId = null;
        context.ContactCategories.Add(new ContactCategoryEntity { Id = 1, Name = "First", SortOrder = 0, GroupId = 1 });
        context.ContactCategories.Add(new ContactCategoryEntity { Id = 2, Name = "Second", SortOrder = 3, GroupId = 1 });
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var repository = new ContactCategoryRepository(context, CreateMapper());
        groupContext.ActiveGroupId = 1;

        // Act
        var nextSortOrder = await repository.GetNextSortOrderAsync(TestContext.Current.CancellationToken);

        // Assert
        nextSortOrder.Should().Be(4);
    }

    [Fact]
    public async Task SwapSortOrderAsync_ExchangesPositions()
    {
        // Arrange
        var groupContext = new MutableTestGroupContext { ActiveGroupId = null };
        await using var context = CreateContext("ContactCategoryRepositoryTests." + nameof(SwapSortOrderAsync_ExchangesPositions), groupContext);
        await SeedGroupAsync(context, groupContext);

        groupContext.ActiveGroupId = null;
        context.ContactCategories.Add(new ContactCategoryEntity { Id = 1, Name = "First", SortOrder = 0, GroupId = 1 });
        context.ContactCategories.Add(new ContactCategoryEntity { Id = 2, Name = "Second", SortOrder = 1, GroupId = 1 });
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var repository = new ContactCategoryRepository(context, CreateMapper());
        groupContext.ActiveGroupId = 1;

        // Act
        await repository.SwapSortOrderAsync(1, 2, TestContext.Current.CancellationToken);
        var categories = await repository.GetOrderedForActiveGroupAsync(TestContext.Current.CancellationToken);

        // Assert: re-reading the ordered list shows them exchanged
        categories.Select(c => c.Name).Should().ContainInConsecutiveOrder("Second", "First");
    }

    [Fact]
    public async Task DeleteWithDependentsLoadedAsync_ContactsSurviveWithNullCategory()
    {
        // Arrange: a category holding two contacts
        var groupContext = new MutableTestGroupContext { ActiveGroupId = null };
        await using var context = CreateContext("ContactCategoryRepositoryTests." + nameof(DeleteWithDependentsLoadedAsync_ContactsSurviveWithNullCategory), groupContext);
        await SeedGroupAsync(context, groupContext);

        groupContext.ActiveGroupId = null;
        context.UserEntities.Add(new UserEntity { Id = 1, Name = "Creator One", Email = "creator1@test.com" });
        context.ContactCategories.Add(new ContactCategoryEntity { Id = 1, Name = "Merchants", SortOrder = 0, GroupId = 1 });
        context.Contacts.Add(new ContactEntity { Id = 1, Name = "First Contact", GroupId = 1, CreatedByUserId = 1, CategoryId = 1, CreatedAt = DateTime.UtcNow });
        context.Contacts.Add(new ContactEntity { Id = 2, Name = "Second Contact", GroupId = 1, CreatedByUserId = 1, CategoryId = 1, CreatedAt = DateTime.UtcNow });
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var repository = new ContactCategoryRepository(context, CreateMapper());
        groupContext.ActiveGroupId = 1;

        // Act
        await repository.DeleteWithDependentsLoadedAsync(1, TestContext.Current.CancellationToken);

        // Assert: both contacts still exist with no category reference
        var contactRepository = new ContactRepository(context, CreateMapper());
        var contacts = await contactRepository.GetAllContactsWithDetailsAsync(TestContext.Current.CancellationToken);
        contacts.Should().HaveCount(2);
        contacts.Should().OnlyContain(c => c.CategoryId == null);

        var categories = await repository.GetOrderedForActiveGroupAsync(TestContext.Current.CancellationToken);
        categories.Should().BeEmpty();
    }

    [Fact]
    public async Task GetContactCountsAsync_ReturnsCountsIncludingZeroForEmptyCategory()
    {
        // Arrange: one category with two contacts, one category with none
        var groupContext = new MutableTestGroupContext { ActiveGroupId = null };
        await using var context = CreateContext("ContactCategoryRepositoryTests." + nameof(GetContactCountsAsync_ReturnsCountsIncludingZeroForEmptyCategory), groupContext);
        await SeedGroupAsync(context, groupContext);

        groupContext.ActiveGroupId = null;
        context.UserEntities.Add(new UserEntity { Id = 1, Name = "Creator One", Email = "creator1@test.com" });
        context.ContactCategories.Add(new ContactCategoryEntity { Id = 1, Name = "Merchants", SortOrder = 0, GroupId = 1 });
        context.ContactCategories.Add(new ContactCategoryEntity { Id = 2, Name = "Nobles", SortOrder = 1, GroupId = 1 });
        context.Contacts.Add(new ContactEntity { Id = 1, Name = "First Contact", GroupId = 1, CreatedByUserId = 1, CategoryId = 1, CreatedAt = DateTime.UtcNow });
        context.Contacts.Add(new ContactEntity { Id = 2, Name = "Second Contact", GroupId = 1, CreatedByUserId = 1, CategoryId = 1, CreatedAt = DateTime.UtcNow });
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var repository = new ContactCategoryRepository(context, CreateMapper());
        groupContext.ActiveGroupId = 1;

        // Act
        var counts = await repository.GetContactCountsAsync(TestContext.Current.CancellationToken);

        // Assert
        counts.Should().HaveCount(2);
        counts[1].Should().Be(2);
        counts[2].Should().Be(0);
    }

    private sealed class MutableTestGroupContext : IActiveGroupContext
    {
        public int? ActiveGroupId { get; set; }
    }
}
