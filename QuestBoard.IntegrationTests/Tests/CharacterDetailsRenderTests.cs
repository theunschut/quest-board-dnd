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

    // The title card, the badge row and the actions card, cut from the page so a word elsewhere cannot satisfy a check.
    private static string Between(string html, string startMarker, string endMarker)
    {
        var start = html.IndexOf(startMarker, StringComparison.Ordinal);
        start.Should().BeGreaterThanOrEqualTo(0, $"the page holds {startMarker}");
        var end = html.IndexOf(endMarker, start, StringComparison.Ordinal);
        end.Should().BeGreaterThan(start, $"{endMarker} follows {startMarker}");
        return html[start..end];
    }

    private async Task<(Seeded Character, HttpClient Viewer)> SeedForViewerAsync(string key, bool admin)
    {
        var owner = await AuthenticationHelper.CreateTestUserAsync(
            _factory.Services, $"chardetowner{key}", $"chardetowner{key}@example.com", "Test123!", "Character Owner");
        var name = $"Viewed {key} Hero";
        var character = await TestDataHelper.CreateTestCharacterAsync(_factory.Services, owner.Id, name, groupId: 1);
        var (viewer, _) = admin
            ? await AuthenticationHelper.CreateAuthenticatedAdminClientAsync(
                _factory, $"chardetadmin{key}", $"chardetadmin{key}@example.com")
            : await AuthenticationHelper.CreateAuthenticatedDMClientAsync(
                _factory, $"chardetdm{key}", $"chardetdm{key}@example.com");
        return (new Seeded(viewer, character.Id, name), viewer);
    }

    [Theory]
    [MemberData(nameof(UserAgents))]
    public async Task Details_AdminViewingAnotherPlayersCharacter_SeesTheActionsOnBothLayouts(string userAgent, bool isPhone)
    {
        var (character, viewer) = await SeedForViewerAsync($"adm{(isPhone ? "p" : "d")}", admin: true);

        var (_, html) = await GetDetailsAsync(viewer, character.CharacterId, userAgent);

        html.Should().Contain("id=\"actions-title\"");
        html.Should().Contain("Edit Character");
        html.Should().Contain("Retire Character");
        html.Should().Contain("Delete Character");
    }

    [Theory]
    [MemberData(nameof(UserAgents))]
    public async Task Details_DungeonMasterViewingAnotherPlayersCharacter_SeesNoActionsOnBothLayouts(string userAgent, bool isPhone)
    {
        var (character, viewer) = await SeedForViewerAsync($"dm{(isPhone ? "p" : "d")}", admin: false);

        var (status, html) = await GetDetailsAsync(viewer, character.CharacterId, userAgent);

        status.Should().Be(HttpStatusCode.OK);
        html.Should().Contain(character.Name);
        html.Should().NotContain("id=\"actions-title\"");
        html.Should().NotContain("Edit Character");
        html.Should().NotContain("data-confirm-message");
    }

    [Theory]
    [MemberData(nameof(UserAgents))]
    public async Task Details_DeadCharacter_ShowsTheDeadBadgeWithIconAndWordAndOffersNoRetirement(string userAgent, bool isPhone)
    {
        var character = await SeedOwnedAsync($"dead{(isPhone ? "p" : "d")}", picture: true, status: CharacterStatus.Dead);

        var (_, html) = await GetDetailsAsync(character.Client, character.CharacterId, userAgent);

        var badges = Between(html, "character-detail-badges", "character-detail-main");
        badges.Should().Contain("fa-skull");
        badges.Should().Contain("Dead");
        badges.Should().NotContain("fa-check-circle");
        html.Should().Contain("character-detail-portrait--dead", "only the picture is desaturated");
        html.Should().NotContain("Retire Character");
        html.Should().NotContain("Reactivate Character");
        html.Should().Contain("Delete Character");
    }

    [Theory]
    [MemberData(nameof(UserAgents))]
    public async Task Details_RetiredCharacter_ShowsTheRetiredBadgeAndOffersReactivation(string userAgent, bool isPhone)
    {
        var character = await SeedOwnedAsync($"ret{(isPhone ? "p" : "d")}", picture: true, status: CharacterStatus.Retired);

        var (_, html) = await GetDetailsAsync(character.Client, character.CharacterId, userAgent);

        var badges = Between(html, "character-detail-badges", "character-detail-main");
        badges.Should().Contain("fa-moon");
        badges.Should().Contain("Retired");
        html.Should().Contain("character-detail-portrait--retired");
        html.Should().Contain("Reactivate Character");
    }

    [Theory]
    [MemberData(nameof(UserAgents))]
    public async Task Details_MainCharacter_ShowsTheStaticMainBadgeWithIconAndWord(string userAgent, bool isPhone)
    {
        var character = await SeedOwnedAsync($"main{(isPhone ? "p" : "d")}", role: CharacterRole.Main);

        var (_, html) = await GetDetailsAsync(character.Client, character.CharacterId, userAgent);

        var badges = Between(html, "character-detail-badges", "character-detail-main");
        badges.Should().Contain("badge-main");
        badges.Should().Contain("fa-star");
        badges.Should().Contain("Main Character");
        badges.Should().Contain("Active");
    }

    [Theory]
    [MemberData(nameof(UserAgents))]
    public async Task Details_BareCharacter_ShowsTheVerbatimEmptiesAndThePlaceholder(string userAgent, bool isPhone)
    {
        var character = await SeedOwnedAsync($"bare{(isPhone ? "p" : "d")}", noClasses: true);

        var (_, html) = await GetDetailsAsync(character.Client, character.CharacterId, userAgent);

        html.Should().Contain("<dd>Not provided</dd>");
        html.Should().Contain("<dd>No classes defined</dd>");
        html.Should().Contain("character-detail-portrait--placeholder");
        html.Should().NotContain("/Characters/GetProfilePicture/");
        html.Should().NotContain("id=\"description-title\"", "a character with no description has no description card");
        html.Should().NotContain("id=\"backstory-title\"", "a character with no backstory has no backstory card");
    }

    [Theory]
    [MemberData(nameof(UserAgents))]
    public async Task Details_SheetLink_OpensANewTabSafelyAndSaysSo(string userAgent, bool isPhone)
    {
        var character = await SeedOwnedAsync($"sheet{(isPhone ? "p" : "d")}", sheetLink: true);

        var (_, html) = await GetDetailsAsync(character.Client, character.CharacterId, userAgent);

        var link = Regex.Match(html, @"<a\b[^>]*href=""https://example\.invalid/sheets/thessaly""[^>]*>[\s\S]*?</a>");
        link.Success.Should().BeTrue("the sheet link renders");
        link.Value.Should().Contain("target=\"_blank\"");
        link.Value.Should().Contain("rel=\"noopener noreferrer\"");
        link.Value.Should().Contain("<span class=\"visually-hidden\"> (opens in a new tab)</span>");
    }

    [Theory]
    [MemberData(nameof(UserAgents))]
    public async Task Details_ReadOnlyValues_AreADefinitionListAndNoLabelWrapsOne(string userAgent, bool isPhone)
    {
        var character = await SeedOwnedAsync(
            $"dl{(isPhone ? "p" : "d")}", sheetLink: true, extraClass: true, description: "Described.", backstory: "Storied.");

        var (_, html) = await GetDetailsAsync(character.Client, character.CharacterId, userAgent);
        var page = WithoutComments(html);

        var list = Regex.Match(page, @"<dl\b[^>]*>[\s\S]*?</dl>");
        list.Success.Should().BeTrue("the information is a definition list");
        Regex.Matches(list.Value, @"<dt>").Count.Should().Be(3);
        Regex.Matches(list.Value, @"<dd>").Count.Should().Be(3);
        list.Value.Should().Contain("<dt>Level</dt>");
        list.Value.Should().Contain("<dt>Character Sheet</dt>");
        list.Value.Should().Contain("<dt>Classes</dt>");

        var content = Between(page, "character-detail-page", "character-detail-back");
        content.Should().NotContain("<label", "a read-only value is not a form field");
        content.Should().NotContain("form-control-plaintext");
    }
}
