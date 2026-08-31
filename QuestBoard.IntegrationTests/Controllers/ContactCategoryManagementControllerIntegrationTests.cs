using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using QuestBoard.Domain.Interfaces;
using QuestBoard.Domain.Models;
using QuestBoard.IntegrationTests.Helpers;
using QuestBoard.Repository.Entities;
using System.Net;

namespace QuestBoard.IntegrationTests.Controllers;

// Route-level coverage for ContactCategoryManagementController: the single class-level
// authorization gate across all six actions, the duplicate-name path on both Add and Edit, the
// delete-orphan guarantee asserted against the database, both reorder directions with their
// boundaries, and heading escaping.
public class ContactCategoryManagementControllerIntegrationTests(WebApplicationFactoryBase factory) : IClassFixture<WebApplicationFactoryBase>
{
    // Extracts a fresh antiforgery token/cookie pair from a GET of the management index and
    // attaches it to the client, then folds it into the given form fields -- the same pattern
    // already used by ShopControllerIntegrationTests/AdminControllerIntegrationTests.
    private static async Task<FormUrlEncodedContent> BuildAntiForgeryFormAsync(
        HttpClient client, Dictionary<string, string> formData)
    {
        var getResponse = await client.GetAsync("/ContactCategoryManagement", TestContext.Current.CancellationToken);
        var (token, cookieValue) = await AntiForgeryHelper.ExtractAntiForgeryTokenAsync(getResponse);

        if (!string.IsNullOrEmpty(cookieValue))
        {
            client.DefaultRequestHeaders.Remove("Cookie");
            client.DefaultRequestHeaders.Add("Cookie", $".AspNetCore.Antiforgery={cookieValue}");
        }

        return AntiForgeryHelper.CreateFormContentWithAntiForgeryToken(formData, token);
    }

    // Reads categories back through a fresh untracked context ignoring the board query filter --
    // test infrastructure only, confined to this file. Never mirrored in a controller, service or
    // repository.
    private async Task<List<ContactCategoryEntity>> GetOrderedCategoriesAsync(int groupId = 1)
    {
        await using var ctx = factory.Database.CreateContext();
        return await ctx.ContactCategories
            .IgnoreQueryFilters()
            .Where(c => c.GroupId == groupId)
            .OrderBy(c => c.SortOrder)
            .ToListAsync(TestContext.Current.CancellationToken);
    }

    private async Task<ContactEntity?> GetContactByIdAsync(int id)
    {
        await using var ctx = factory.Database.CreateContext();
        return await ctx.Contacts
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(c => c.Id == id, TestContext.Current.CancellationToken);
    }

    // Builds a client bound to a variant host that swaps the real IContactCategoryService for a
    // decorator forcing the exact DbUpdateException a live unique-index violation would raise, once,
    // for the given name. The EF Core InMemory provider backing this whole suite does not enforce
    // HasIndex().IsUnique() at all -- confirmed directly: writing two rows sharing a (GroupId, Name)
    // through both a fresh context and the app's own DI-registered context saves both without error.
    // The database layer this suite runs against therefore never raises the exception the controller's
    // catch exists to handle, so the two duplicate-name facts below force it deterministically instead.
    // That still proves what this task owns -- the controller's reaction to the exception -- while the
    // exception's real source, the unique index's SQL Server column collation, is a database-layer
    // guarantee this InMemory-backed suite was never going to be able to exercise directly.
    private async Task<HttpClient> CreateDuplicateNameClientAsync(string triggerName, string userName, string email)
    {
        var variantFactory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                var descriptor = services.Single(d => d.ServiceType == typeof(IContactCategoryService));
                services.Remove(descriptor);
                services.AddScoped<IContactCategoryService>(sp =>
                {
                    var real = (IContactCategoryService)ActivatorUtilities.CreateInstance(sp, descriptor.ImplementationType!);
                    return new DuplicateNameThrowingContactCategoryService(real, triggerName);
                });
            });
        });

        var (client, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            variantFactory, userName, email, roles: ["DungeonMaster"]);
        return client;
    }

    // Delegates every member to the real service except AddToEndAsync/UpdateAsync for one specific
    // name, which throw the same DbUpdateException shape a unique-index violation produces --
    // matching what GroupController's own catch filter matches on ("unique"/"duplicate", case
    // insensitive, in the inner exception message).
    private sealed class DuplicateNameThrowingContactCategoryService(IContactCategoryService inner, string triggerName) : IContactCategoryService
    {
        public Task<IList<ContactCategory>> GetOrderedAsync(CancellationToken token = default) => inner.GetOrderedAsync(token);
        public Task<IDictionary<int, int>> GetContactCountsAsync(CancellationToken token = default) => inner.GetContactCountsAsync(token);
        public Task<bool> MoveUpAsync(int id, CancellationToken token = default) => inner.MoveUpAsync(id, token);
        public Task<bool> MoveDownAsync(int id, CancellationToken token = default) => inner.MoveDownAsync(id, token);
        public Task DeleteAsync(int id, CancellationToken token = default) => inner.DeleteAsync(id, token);
        public Task AddAsync(ContactCategory model, CancellationToken token = default) => inner.AddAsync(model, token);
        public Task<bool> ExistsAsync(int id, CancellationToken token = default) => inner.ExistsAsync(id, token);
        public Task<IList<ContactCategory>> GetAllAsync(CancellationToken token = default) => inner.GetAllAsync(token);
        public Task<ContactCategory?> GetByIdAsync(int id, CancellationToken token = default) => inner.GetByIdAsync(id, token);
        public Task RemoveAsync(ContactCategory model, CancellationToken token = default) => inner.RemoveAsync(model, token);

        public Task AddToEndAsync(ContactCategory category, CancellationToken token = default) =>
            category.Name == triggerName ? throw MakeDuplicateException() : inner.AddToEndAsync(category, token);

        public Task UpdateAsync(ContactCategory model, CancellationToken token = default) =>
            model.Name == triggerName ? throw MakeDuplicateException() : inner.UpdateAsync(model, token);

        private static DbUpdateException MakeDuplicateException() =>
            new("A duplicate value violates a unique constraint.",
                new InvalidOperationException("Cannot insert duplicate key row in object with unique index."));
    }

    // (1) The authorization gate: a player is refused on the list page and on every write;
    // DungeonMaster and Admin both reach the list page.

    [Fact]
    public async Task Index_Get_PlayerAccess_ShouldBeBlocked()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var (playerClient, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "category_player_index", "category_player_index@example.com", roles: ["Player"]);

        var response = await playerClient.GetAsync("/ContactCategoryManagement", TestContext.Current.CancellationToken);

        response.StatusCode.Should().BeOneOf(HttpStatusCode.Forbidden, HttpStatusCode.Redirect, HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Index_Get_DungeonMasterAccess_ShouldSucceed()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var (dmClient, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "category_dm_index", "category_dm_index@example.com", roles: ["DungeonMaster"]);

        var response = await dmClient.GetAsync("/ContactCategoryManagement", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Index_Get_AdminAccess_ShouldSucceed()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var (adminClient, _) = await AuthenticationHelper.CreateAuthenticatedAdminClientAsync(factory);

        var response = await adminClient.GetAsync("/ContactCategoryManagement", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Add_Post_PlayerAccess_ShouldBeBlocked()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var (playerClient, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "category_player_add", "category_player_add@example.com", roles: ["Player"]);

        var formContent = new FormUrlEncodedContent(new Dictionary<string, string> { ["NewCategory.Name"] = "Should Not Persist" });
        var response = await playerClient.PostAsync("/ContactCategoryManagement/Add", formContent, TestContext.Current.CancellationToken);

        response.StatusCode.Should().BeOneOf(HttpStatusCode.Forbidden, HttpStatusCode.Redirect, HttpStatusCode.Unauthorized);
        (await GetOrderedCategoriesAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task Edit_Post_PlayerAccess_ShouldBeBlocked()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var category = await TestDataHelper.CreateTestContactCategoryAsync(factory.Services, "Untouched Category", sortOrder: 0, groupId: 1);

        var (playerClient, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "category_player_edit", "category_player_edit@example.com", roles: ["Player"]);

        var formContent = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Id"] = category.Id.ToString(),
            ["Name"] = "Renamed By Player"
        });
        var response = await playerClient.PostAsync($"/ContactCategoryManagement/Edit/{category.Id}", formContent, TestContext.Current.CancellationToken);

        response.StatusCode.Should().BeOneOf(HttpStatusCode.Forbidden, HttpStatusCode.Redirect, HttpStatusCode.Unauthorized);
        var categories = await GetOrderedCategoriesAsync();
        categories.Should().ContainSingle(c => c.Name == "Untouched Category");
    }

    [Fact]
    public async Task Delete_Post_PlayerAccess_ShouldBeBlocked()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var category = await TestDataHelper.CreateTestContactCategoryAsync(factory.Services, "Undeleted Category", sortOrder: 0, groupId: 1);

        var (playerClient, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "category_player_delete", "category_player_delete@example.com", roles: ["Player"]);

        var formContent = new FormUrlEncodedContent(new Dictionary<string, string> { ["id"] = category.Id.ToString() });
        var response = await playerClient.PostAsync($"/ContactCategoryManagement/Delete/{category.Id}", formContent, TestContext.Current.CancellationToken);

        response.StatusCode.Should().BeOneOf(HttpStatusCode.Forbidden, HttpStatusCode.Redirect, HttpStatusCode.Unauthorized);
        (await GetOrderedCategoriesAsync()).Should().ContainSingle(c => c.Id == category.Id);
    }

    [Fact]
    public async Task MoveUp_Post_PlayerAccess_ShouldBeBlocked()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var first = await TestDataHelper.CreateTestContactCategoryAsync(factory.Services, "First", sortOrder: 0, groupId: 1);
        var second = await TestDataHelper.CreateTestContactCategoryAsync(factory.Services, "Second", sortOrder: 1, groupId: 1);

        var (playerClient, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "category_player_moveup", "category_player_moveup@example.com", roles: ["Player"]);

        var formContent = new FormUrlEncodedContent(new Dictionary<string, string> { ["id"] = second.Id.ToString() });
        var response = await playerClient.PostAsync($"/ContactCategoryManagement/MoveUp/{second.Id}", formContent, TestContext.Current.CancellationToken);

        response.StatusCode.Should().BeOneOf(HttpStatusCode.Forbidden, HttpStatusCode.Redirect, HttpStatusCode.Unauthorized);
        var categories = await GetOrderedCategoriesAsync();
        categories.Select(c => c.Id).Should().ContainInOrder(first.Id, second.Id);
    }

    [Fact]
    public async Task MoveDown_Post_PlayerAccess_ShouldBeBlocked()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var first = await TestDataHelper.CreateTestContactCategoryAsync(factory.Services, "First", sortOrder: 0, groupId: 1);
        var second = await TestDataHelper.CreateTestContactCategoryAsync(factory.Services, "Second", sortOrder: 1, groupId: 1);

        var (playerClient, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "category_player_movedown", "category_player_movedown@example.com", roles: ["Player"]);

        var formContent = new FormUrlEncodedContent(new Dictionary<string, string> { ["id"] = first.Id.ToString() });
        var response = await playerClient.PostAsync($"/ContactCategoryManagement/MoveDown/{first.Id}", formContent, TestContext.Current.CancellationToken);

        response.StatusCode.Should().BeOneOf(HttpStatusCode.Forbidden, HttpStatusCode.Redirect, HttpStatusCode.Unauthorized);
        var categories = await GetOrderedCategoriesAsync();
        categories.Select(c => c.Id).Should().ContainInOrder(first.Id, second.Id);
    }

    // (2) A DM adding a valid new name is redirected and the category is listed last.

    [Fact]
    public async Task Add_Post_DungeonMasterAccess_ValidName_RedirectsAndCategoryIsListed()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var (dmClient, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "category_dm_add", "category_dm_add@example.com", roles: ["DungeonMaster"]);

        var formContent = await BuildAntiForgeryFormAsync(dmClient, new Dictionary<string, string>
        {
            ["NewCategory.Name"] = "Guild Members"
        });

        var response = await dmClient.PostAsync("/ContactCategoryManagement/Add", formContent, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        (await GetOrderedCategoriesAsync()).Should().ContainSingle(c => c.Name == "Guild Members");
    }

    // (3) Duplicate-name path on Add: submitting a name that collides with an existing category
    // is refused with the ModelState message, never a raw 500, and no second row is written. Runs
    // against the decorator-forced exception documented on CreateDuplicateNameClientAsync above.

    [Fact]
    public async Task Add_Post_DungeonMaster_ContactCategory_DuplicateName_ReturnsMessageWithoutCreatingSecondRow()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        await TestDataHelper.CreateTestContactCategoryAsync(factory.Services, "Guild Members", sortOrder: 0, groupId: 1);

        var dmClient = await CreateDuplicateNameClientAsync(
            "Guild Members", "category_dm_dupadd", "category_dm_dupadd@example.com");

        var formContent = await BuildAntiForgeryFormAsync(dmClient, new Dictionary<string, string>
        {
            ["NewCategory.Name"] = "Guild Members"
        });

        var response = await dmClient.PostAsync("/ContactCategoryManagement/Add", formContent, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        content.Should().Contain("A category with that name already exists. Please choose a different name.");

        (await GetOrderedCategoriesAsync()).Should().ContainSingle();
    }

    // (4) Duplicate-name path on Edit: renaming to an existing name is refused with the same
    // message, and the category's stored name is unchanged. Same decorator-forced exception.

    [Fact]
    public async Task Edit_Post_DungeonMaster_ContactCategory_DuplicateName_ReturnsMessage_NameUnchangedInDatabase()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        await TestDataHelper.CreateTestContactCategoryAsync(factory.Services, "Guild Members", sortOrder: 0, groupId: 1);
        var toRename = await TestDataHelper.CreateTestContactCategoryAsync(factory.Services, "Last Bastion", sortOrder: 1, groupId: 1);

        var dmClient = await CreateDuplicateNameClientAsync(
            "Guild Members", "category_dm_dupedit", "category_dm_dupedit@example.com");

        var formContent = await BuildAntiForgeryFormAsync(dmClient, new Dictionary<string, string>
        {
            ["Id"] = toRename.Id.ToString(),
            ["Name"] = "Guild Members"
        });

        var response = await dmClient.PostAsync($"/ContactCategoryManagement/Edit/{toRename.Id}", formContent, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        content.Should().Contain("A category with that name already exists. Please choose a different name.");

        var categories = await GetOrderedCategoriesAsync();
        categories.Should().ContainSingle(c => c.Id == toRename.Id && c.Name == "Last Bastion");
    }

    // (5) Delete orphans: a category holding two contacts is removed, and both contacts survive
    // with a null category reference, read back through a fresh untracked context.

    [Fact]
    public async Task Delete_Post_DungeonMaster_ContactCategory_DeleteOrphans_ContactsSurviveWithNullCategory()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var (dmClient, dmUser) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "category_dm_delete", "category_dm_delete@example.com", roles: ["DungeonMaster"]);
        var category = await TestDataHelper.CreateTestContactCategoryAsync(factory.Services, "Doomed Category", sortOrder: 0, groupId: 1);

        var firstContact = await TestDataHelper.CreateTestContactAsync(factory.Services, dmUser.Id, "Orphan One", groupId: 1, isRevealed: true);
        var secondContact = await TestDataHelper.CreateTestContactAsync(factory.Services, dmUser.Id, "Orphan Two", groupId: 1, isRevealed: true);

        await using (var ctx = factory.Database.CreateContext())
        {
            var contacts = await ctx.Contacts.IgnoreQueryFilters()
                .Where(c => c.Id == firstContact.Id || c.Id == secondContact.Id)
                .ToListAsync(TestContext.Current.CancellationToken);
            foreach (var contact in contacts)
            {
                contact.CategoryId = category.Id;
            }
            await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var formContent = await BuildAntiForgeryFormAsync(dmClient, new Dictionary<string, string> { ["id"] = category.Id.ToString() });
        var response = await dmClient.PostAsync($"/ContactCategoryManagement/Delete/{category.Id}", formContent, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);

        (await GetOrderedCategoriesAsync()).Should().BeEmpty();

        var persistedFirst = await GetContactByIdAsync(firstContact.Id);
        var persistedSecond = await GetContactByIdAsync(secondContact.Id);
        persistedFirst.Should().NotBeNull();
        persistedFirst!.CategoryId.Should().BeNull();
        persistedSecond.Should().NotBeNull();
        persistedSecond!.CategoryId.Should().BeNull();
    }

    // (6) Reorder: moving the middle of three categories up exchanges it with the first; moving
    // the first up and the last down are boundary no-ops that leave the order unchanged.

    [Fact]
    public async Task MoveUp_Post_DungeonMaster_MiddleCategory_ExchangesWithFirst()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var first = await TestDataHelper.CreateTestContactCategoryAsync(factory.Services, "Alpha", sortOrder: 0, groupId: 1);
        var middle = await TestDataHelper.CreateTestContactCategoryAsync(factory.Services, "Beta", sortOrder: 1, groupId: 1);
        var last = await TestDataHelper.CreateTestContactCategoryAsync(factory.Services, "Gamma", sortOrder: 2, groupId: 1);

        var (dmClient, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "category_dm_moveup_middle", "category_dm_moveup_middle@example.com", roles: ["DungeonMaster"]);

        var formContent = await BuildAntiForgeryFormAsync(dmClient, new Dictionary<string, string> { ["id"] = middle.Id.ToString() });
        var response = await dmClient.PostAsync($"/ContactCategoryManagement/MoveUp/{middle.Id}", formContent, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response.Headers.Location!.OriginalString.Should().Contain($"category-{middle.Id}-row");

        var categories = await GetOrderedCategoriesAsync();
        categories.Select(c => c.Id).Should().ContainInOrder(middle.Id, first.Id, last.Id);
    }

    [Fact]
    public async Task MoveUp_Post_DungeonMaster_FirstCategory_IsNoOp()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var first = await TestDataHelper.CreateTestContactCategoryAsync(factory.Services, "Alpha", sortOrder: 0, groupId: 1);
        var second = await TestDataHelper.CreateTestContactCategoryAsync(factory.Services, "Beta", sortOrder: 1, groupId: 1);

        var (dmClient, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "category_dm_moveup_first", "category_dm_moveup_first@example.com", roles: ["DungeonMaster"]);

        var formContent = await BuildAntiForgeryFormAsync(dmClient, new Dictionary<string, string> { ["id"] = first.Id.ToString() });
        var response = await dmClient.PostAsync($"/ContactCategoryManagement/MoveUp/{first.Id}", formContent, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);

        var categories = await GetOrderedCategoriesAsync();
        categories.Select(c => c.Id).Should().ContainInOrder(first.Id, second.Id);
    }

    [Fact]
    public async Task MoveDown_Post_DungeonMaster_LastCategory_IsNoOp()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var first = await TestDataHelper.CreateTestContactCategoryAsync(factory.Services, "Alpha", sortOrder: 0, groupId: 1);
        var last = await TestDataHelper.CreateTestContactCategoryAsync(factory.Services, "Beta", sortOrder: 1, groupId: 1);

        var (dmClient, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "category_dm_movedown_last", "category_dm_movedown_last@example.com", roles: ["DungeonMaster"]);

        var formContent = await BuildAntiForgeryFormAsync(dmClient, new Dictionary<string, string> { ["id"] = last.Id.ToString() });
        var response = await dmClient.PostAsync($"/ContactCategoryManagement/MoveDown/{last.Id}", formContent, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);

        var categories = await GetOrderedCategoriesAsync();
        categories.Select(c => c.Id).Should().ContainInOrder(first.Id, last.Id);
    }

    // (7) A category name containing markup is HTML-escaped in the rendered list page.

    [Fact]
    public async Task Index_Get_CategoryNameWithMarkup_IsHtmlEscaped()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        await TestDataHelper.CreateTestContactCategoryAsync(
            factory.Services, "<script>alert('xss')</script>", sortOrder: 0, groupId: 1);

        var (dmClient, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "category_dm_escape", "category_dm_escape@example.com", roles: ["DungeonMaster"]);

        var response = await dmClient.GetAsync("/ContactCategoryManagement", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        content.Should().NotContain("<script>alert('xss')</script>");
        content.Should().Contain("&lt;script&gt;");
    }
}
