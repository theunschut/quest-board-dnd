using System.Net;
using System.Text.RegularExpressions;
using QuestBoard.Domain.Enums;
using QuestBoard.IntegrationTests.Helpers;

namespace QuestBoard.IntegrationTests.Tests;

/// <summary>
/// Proves the character details page is one view for both layouts: the phone and the desktop
/// user agents get the same title, information, actions and wording, the delete form asks its
/// question through the shared confirm module rather than an inline handler, and a character's
/// picture is requested only when one is stored.
/// </summary>
public class CharacterDetailsRenderTests : IClassFixture<WebApplicationFactoryBase>, IAsyncLifetime
{
    private const string DesktopUserAgent =
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36";

    private const string PhoneUserAgent =
        "Mozilla/5.0 (Linux; Android 13; Pixel 7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Mobile Safari/537.36";

    private const string DeleteSentence = "Are you sure you want to delete this character? This action cannot be undone.";

    private readonly WebApplicationFactoryBase _factory;

    public CharacterDetailsRenderTests(WebApplicationFactoryBase factory)
    {
        _factory = factory;
    }

    public static TheoryData<string, bool> UserAgents => new()
    {
        { DesktopUserAgent, false },
        { PhoneUserAgent, true },
    };

    public async ValueTask InitializeAsync() => await TestDataHelper.ClearDatabaseAsync(_factory.Services);

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private sealed record Seeded(HttpClient Client, int CharacterId, string Name);

    // A character owned by the signed-in player. Every argument that adds something optional is off by default so a
    // test names only what it needs.
    private async Task<Seeded> SeedOwnedAsync(
        string key,
        bool picture = false,
        bool sheetLink = false,
        bool extraClass = false,
        bool noClasses = false,
        string? description = null,
        string? backstory = null,
        CharacterStatus status = CharacterStatus.Active,
        CharacterRole role = CharacterRole.Backup)
    {
        var (client, owner) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            _factory, $"chardet{key}", $"chardet{key}@example.com", name: $"Owner {key}");
        var name = $"Thessaly {key} Brightmantle";
        var character = await TestDataHelper.CreateTestCharacterAsync(
            _factory.Services, owner.Id, name, level: 5, status: (int)status, role: (int)role);
        await ShapeAsync(character.Id, picture, sheetLink, extraClass, noClasses, description, backstory);
        return new Seeded(client, character.Id, name);
    }

    private async Task ShapeAsync(
        int characterId, bool picture, bool sheetLink, bool extraClass, bool noClasses, string? description, string? backstory)
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<QuestBoardContext>();
        var entity = await context.Characters.FindAsync([characterId], TestContext.Current.CancellationToken);
        entity!.SheetLink = sheetLink ? "https://example.invalid/sheets/thessaly" : null;
        entity.Description = description;
        entity.Backstory = backstory;

        if (noClasses)
        {
            context.CharacterClasses.RemoveRange(context.CharacterClasses.Where(c => c.CharacterId == characterId));
        }
        else if (extraClass)
        {
            context.CharacterClasses.Add(new CharacterClassEntity
            {
                CharacterId = characterId,
                Class = (int)DndClass.Cleric,
                ClassLevel = 2,
            });
        }

        if (picture)
        {
            context.Set<CharacterImageEntity>().Add(new CharacterImageEntity
            {
                Id = characterId,
                OriginalImageData = [1, 2, 3, 4],
                CroppedImageData = [9, 9, 9, 9, 9],
            });
        }

        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private static async Task<(HttpStatusCode Status, string Html)> GetDetailsAsync(
        HttpClient client, int characterId, string userAgent)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, $"/Characters/Details/{characterId}");
        request.Headers.TryAddWithoutValidation("User-Agent", userAgent);
        request.Headers.Authorization = client.DefaultRequestHeaders.Authorization;
        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        return (response.StatusCode, await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    private static string WithoutComments(string html) => Regex.Replace(html, @"<!--[\s\S]*?-->", " ");

    private static string PictureUrl(string action, int id) => $"/Characters/{action}/{id}";

    [Theory]
    [MemberData(nameof(UserAgents))]
    public async Task Details_Owner_ServesOneViewWithTitleActionsAndSheetOnBothLayouts(string userAgent, bool isPhone)
    {
        var character = await SeedOwnedAsync($"one{(isPhone ? "p" : "d")}", picture: true, sheetLink: true, extraClass: true);

        var (status, html) = await GetDetailsAsync(character.Client, character.CharacterId, userAgent);

        status.Should().Be(HttpStatusCode.OK);
        if (isPhone)
            html.Should().Contain("mobile-layout");
        else
            html.Should().NotContain("mobile-layout");

        Regex.IsMatch(html, @"<h1\b[^>]*>\s*" + Regex.Escape(character.Name) + @"\s*</h1>").Should().BeTrue(
            "the character's name is the page title");
        html.Should().Contain("Actions");
        html.Should().Contain("Edit Character");
        html.Should().Contain("Character Information");
        html.Should().Contain("character-detail.css");
        html.Should().NotContain("character-detail.mobile.css", "the phone has no separate sheet");
        html.Should().NotContain("character-detail-card", "the phone's own card markup is gone");
    }

    [Theory]
    [MemberData(nameof(UserAgents))]
    public async Task Details_DeleteForm_AsksItsQuestionThroughTheSharedModule(string userAgent, bool isPhone)
    {
        var character = await SeedOwnedAsync($"del{(isPhone ? "p" : "d")}");

        var (_, html) = await GetDetailsAsync(character.Client, character.CharacterId, userAgent);
        var page = WithoutComments(html);

        page.Should().Contain($"data-confirm-message=\"{DeleteSentence}\"");
        Regex.IsMatch(page, @"<script\b[^>]*\btype=""module""[^>]*\bsrc=""/js/confirm-submit\.js[^""]*""").Should().BeTrue(
            "the page loads the confirm module");
        Regex.Matches(page, @"\son[a-z]+\s*=", RegexOptions.IgnoreCase).Select(m => m.Value).Should().BeEmpty(
            "the page holds no inline event handler");
        page.Should().NotContain("onsubmit");
    }

    [Theory]
    [MemberData(nameof(UserAgents))]
    public async Task Details_SectionsFollowTheTitleInformationActionsBackOrder(string userAgent, bool isPhone)
    {
        var character = await SeedOwnedAsync(
            $"ord{(isPhone ? "p" : "d")}", description: "A shield-bearer.", backstory: "The long winter.");

        var (_, html) = await GetDetailsAsync(character.Client, character.CharacterId, userAgent);

        var title = html.IndexOf("<h1", StringComparison.Ordinal);
        var information = html.IndexOf("Character Information", StringComparison.Ordinal);
        var description = html.IndexOf("A shield-bearer.", StringComparison.Ordinal);
        var backstory = html.IndexOf("The long winter.", StringComparison.Ordinal);
        var actions = html.IndexOf("id=\"actions-title\"", StringComparison.Ordinal);
        var back = html.IndexOf("Back to Characters", StringComparison.Ordinal);

        new[] { title, information, description, backstory, actions, back }.Should().OnlyContain(i => i > 0);
        title.Should().BeLessThan(information);
        information.Should().BeLessThan(description);
        description.Should().BeLessThan(backstory);
        backstory.Should().BeLessThan(actions);
        actions.Should().BeLessThan(back);
    }

    [Theory]
    [MemberData(nameof(UserAgents))]
    public async Task Details_CharacterWithoutPicture_RendersThePlaceholderAndRequestsNoPicture(string userAgent, bool isPhone)
    {
        var character = await SeedOwnedAsync($"nop{(isPhone ? "p" : "d")}");

        var (_, html) = await GetDetailsAsync(character.Client, character.CharacterId, userAgent);
        var page = WithoutComments(html);

        page.Should().Contain("character-detail-portrait--placeholder");
        page.Should().NotContain(PictureUrl("GetProfilePicture", character.CharacterId));
        page.Should().NotContain(PictureUrl("GetCroppedPicture", character.CharacterId));
        page.Should().NotContain("onerror");
    }

    [Theory]
    [MemberData(nameof(UserAgents))]
    public async Task Details_CharacterWithPicture_RendersItsPictureUrl(string userAgent, bool isPhone)
    {
        var character = await SeedOwnedAsync($"pic{(isPhone ? "p" : "d")}", picture: true);

        var (_, html) = await GetDetailsAsync(character.Client, character.CharacterId, userAgent);
        var page = WithoutComments(html);

        page.Should().Contain(PictureUrl("GetProfilePicture", character.CharacterId));
        page.Should().NotContain("character-detail-portrait--placeholder");
        page.Should().NotContain("onerror");
    }
}
