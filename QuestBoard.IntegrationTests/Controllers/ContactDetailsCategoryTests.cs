using QuestBoard.IntegrationTests.Helpers;
using QuestBoard.Repository.Entities;
using System.Net;

namespace QuestBoard.IntegrationTests.Controllers;

// Proves the Details page's category line: an assigned contact's category name reaches the
// response, an unassigned contact's response carries neither a category line nor the
// "Ungrouped" grouping label, and a category name containing markup renders escaped rather
// than routed through the Markdown pipeline.
public class ContactDetailsCategoryTests(WebApplicationFactoryBase factory) : IClassFixture<WebApplicationFactoryBase>
{
    // TestDataHelper.CreateTestContactAsync has no categoryId parameter -- it predates category
    // grouping. Stamping CategoryId directly through a fresh scope keeps this file's category
    // seeding self-contained without changing a helper other suites also rely on.
    private static async Task AssignContactCategoryAsync(IServiceProvider services, int contactId, int categoryId)
    {
        using var scope = services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<QuestBoardContext>();
        var contact = await context.Contacts.SingleAsync(c => c.Id == contactId);
        contact.CategoryId = categoryId;
        await context.SaveChangesAsync();
    }

    [Fact]
    public async Task ContactDetails_Category_AssignedContactShowsCategoryName()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var (dmClient, dmUser) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "details_cat_assigned_dm", "details_cat_assigned_dm@example.com", roles: ["DungeonMaster"]);
        var category = await TestDataHelper.CreateTestContactCategoryAsync(factory.Services, "Guild Members", sortOrder: 0, groupId: 1);
        var contact = await TestDataHelper.CreateTestContactAsync(
            factory.Services, dmUser.Id, "Categorised Contact", groupId: 1, isRevealed: true);
        await AssignContactCategoryAsync(factory.Services, contact.Id, category.Id);

        var response = await dmClient.GetAsync($"/Contacts/Details/{contact.Id}", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        content.Should().Contain("Guild Members");
    }

    [Fact]
    public async Task ContactDetails_Category_UnassignedContactShowsNoCategoryLineOrUngroupedLabel()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var (dmClient, dmUser) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "details_cat_unassigned_dm", "details_cat_unassigned_dm@example.com", roles: ["DungeonMaster"]);
        var contact = await TestDataHelper.CreateTestContactAsync(
            factory.Services, dmUser.Id, "Uncategorised Contact", groupId: 1, isRevealed: true);

        var response = await dmClient.GetAsync($"/Contacts/Details/{contact.Id}", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        content.Should().NotContain("Ungrouped");
        content.Should().NotContain("fa-tag");
    }

    [Fact]
    public async Task ContactDetails_Category_NameWithAngleBracketsRendersEscaped()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var (dmClient, dmUser) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "details_cat_escape_dm", "details_cat_escape_dm@example.com", roles: ["DungeonMaster"]);
        var category = await TestDataHelper.CreateTestContactCategoryAsync(
            factory.Services, "<script>alert('x')</script>", sortOrder: 0, groupId: 1);
        var contact = await TestDataHelper.CreateTestContactAsync(
            factory.Services, dmUser.Id, "Escaped Category Contact", groupId: 1, isRevealed: true);
        await AssignContactCategoryAsync(factory.Services, contact.Id, category.Id);

        var response = await dmClient.GetAsync($"/Contacts/Details/{contact.Id}", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        content.Should().NotContain("<script>alert('x')</script>");
        content.Should().Contain("&lt;script&gt;");
    }
}
