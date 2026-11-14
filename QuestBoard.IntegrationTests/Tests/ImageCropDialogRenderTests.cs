using System.Net;
using System.Text.RegularExpressions;
using QuestBoard.Domain.Interfaces;
using QuestBoard.IntegrationTests.Helpers;

namespace QuestBoard.IntegrationTests.Tests;

/// <summary>
/// Pins the shared photo crop dialog on every page that takes a picture, under both layouts: each of the five
/// host pages renders the one shared dialog (its title, the instruction, the focusable selection, the named
/// control row and the two footer buttons), links the crop sheet, loads the Cropper script as a classic script
/// and the module that drives it, and calls no inline initialiser. The dialog itself carries no colour utility,
/// no white close variant and no style attribute, and the cropped-picture input is hidden by the hidden attribute
/// rather than an inline style. The layout is chosen from the request's User-Agent header, so every fact states
/// which one it sends.
/// </summary>
public class ImageCropDialogRenderTests(WebApplicationFactoryBase factory)
    : IClassFixture<WebApplicationFactoryBase>, IAsyncLifetime
{
    private const string DesktopUserAgent =
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36";

    private const string MobileUserAgent =
        "Mozilla/5.0 (iPhone; CPU iPhone OS 17_0 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.0 Mobile/15E148 Safari/604.1";

    private static readonly string[] ControlNames =
        ["Move up", "Move down", "Move left", "Move right"];

    private static readonly string[] ControlWords = ["Larger", "Smaller", "Reset"];

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;

    public ValueTask DisposeAsync()
    {
        factory.TestGroupContext.ActiveGroupId = 1;
        return ValueTask.CompletedTask;
    }

    private static string AgentFor(bool mobile) => mobile ? MobileUserAgent : DesktopUserAgent;

    private sealed record Hosts(HttpClient Client, int CharacterId, int ContactId, int DungeonMasterId);

    // One account that owns a character with a stored picture, a contact with a stored picture and a Dungeon
    // Master profile with a stored picture, so every page, including the re-crop buttons, renders.
    private async Task<Hosts> SeedHostsAsync()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var (client, user) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "cropdialogadmin", "cropdialogadmin@example.com", roles: ["Admin"]);

        var character = await TestDataHelper.CreateTestCharacterAsync(
            factory.Services, user.Id, "Cropped Hero", groupId: 1);
        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<QuestBoardContext>();
            context.Set<CharacterImageEntity>().Add(new CharacterImageEntity
            {
                Id = character.Id,
                OriginalImageData = [1, 2, 3, 4],
                CroppedImageData = [9, 9, 9, 9, 9]
            });
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var contact = await TestDataHelper.CreateTestContactAsync(
            factory.Services, user.Id, "Pictured Contact", groupId: 1, imageData: [1, 2, 3, 4]);

        using (var scope = factory.Services.CreateScope())
        {
            var profiles = scope.ServiceProvider.GetRequiredService<IDungeonMasterProfileService>();
            await profiles.UpsertProfileAsync(
                user.Id, bio: null, imageBytes: [1, 2, 3, 4], newCroppedImageData: [9, 9, 9, 9, 9],
                token: TestContext.Current.CancellationToken);
        }

        return new Hosts(client, character.Id, contact.Id, user.Id);
    }

    private static async Task<string> GetHtmlAsync(HttpClient client, string url, bool mobile)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.TryAddWithoutValidation("User-Agent", AgentFor(mobile));
        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        response.StatusCode.Should().Be(HttpStatusCode.OK, $"{url} must render");
        return await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
    }

    // The dialog from its root element to the end of the Use This Crop button.
    private static string DialogOf(string html)
    {
        var start = html.IndexOf("id=\"cropPhotoModal\"", StringComparison.Ordinal);
        start.Should().BeGreaterThan(-1, "the page renders the crop dialog");
        var confirm = html.IndexOf("id=\"cropConfirmBtn\"", start, StringComparison.Ordinal);
        confirm.Should().BeGreaterThan(start);
        var end = html.IndexOf("</button>", confirm, StringComparison.Ordinal);
        return html[start..(end + "</button>".Length)];
    }

    private static string UrlFor(string host, Hosts seeded) => host switch
    {
        "character-create" => "/Characters/Create",
        "character-edit" => $"/Characters/Edit/{seeded.CharacterId}",
        "contact-create" => "/Contacts/Create",
        "contact-edit" => $"/Contacts/Edit/{seeded.ContactId}",
        "dm-edit" => $"/DungeonMaster/EditProfile/{seeded.DungeonMasterId}",
        _ => throw new ArgumentOutOfRangeException(nameof(host))
    };

    public static TheoryData<string, bool> EveryHostUnderBothLayouts()
    {
        var data = new TheoryData<string, bool>();
        foreach (var host in new[] { "character-create", "character-edit", "contact-create", "contact-edit", "dm-edit" })
        {
            data.Add(host, false);
            data.Add(host, true);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(EveryHostUnderBothLayouts))]
    public async Task Host_RendersTheSharedDialogWithTheControlRowAndTheFocusableSelection(string host, bool mobile)
    {
        var seeded = await SeedHostsAsync();

        var html = await GetHtmlAsync(seeded.Client, UrlFor(host, seeded), mobile);
        var dialog = DialogOf(html);

        dialog.Should().Contain("aria-labelledby=\"cropPhotoModalLabel\"");
        dialog.Should().Contain("id=\"cropPhotoModalLabel\"");
        dialog.Should().Contain("Crop Your Photo");
        dialog.Should().Contain("class=\"crop-instruction\"");
        dialog.Should().Contain("class=\"crop-stage\"");
        dialog.Should().Contain("aria-label=\"Adjust crop\"");
        foreach (var name in ControlNames)
        {
            dialog.Should().Contain($"aria-label=\"{name}\"");
        }

        foreach (var word in ControlWords)
        {
            dialog.Should().Contain($"</i>{word}</button>");
        }

        dialog.Should().Contain("class=\"crop-status visually-hidden\"");
        dialog.Should().Contain("role=\"status\"");
        dialog.Should().Contain("id=\"cropCancelBtn\"");
        dialog.Should().Contain("Discard Photo");
        dialog.Should().Contain("id=\"cropConfirmBtn\"");
        dialog.Should().Contain("Use This Crop");

        var selection = Regex.Match(dialog, "<cropper-selection\\b[^>]*>", RegexOptions.Singleline);
        selection.Success.Should().BeTrue();
        selection.Value.Should().Contain("tabindex=\"0\"");
        selection.Value.Should().Contain("id=\"cropperSelectionEl\"");
        selection.Value.Should().NotContain("keyboard", "Cropper's own document-wide key handling stays off");
    }

    [Theory]
    [MemberData(nameof(EveryHostUnderBothLayouts))]
    public async Task Host_DialogCarriesNoColourUtilityWhiteCloseOrStyleAttribute(string host, bool mobile)
    {
        var seeded = await SeedHostsAsync();

        var dialog = DialogOf(await GetHtmlAsync(seeded.Client, UrlFor(host, seeded), mobile));

        dialog.Should().NotContain("bg-dark");
        dialog.Should().NotContain("text-light");
        dialog.Should().NotContain("text-muted");
        dialog.Should().NotContain("btn-close-white");
        dialog.Should().NotContain("style=");
    }

    [Theory]
    [MemberData(nameof(EveryHostUnderBothLayouts))]
    public async Task Host_LinksTheSheetLoadsTheModuleAndCallsNoInlineInitialiser(string host, bool mobile)
    {
        var seeded = await SeedHostsAsync();

        var html = await GetHtmlAsync(seeded.Client, UrlFor(host, seeded), mobile);

        Regex.IsMatch(html, "<link\\b[^>]*href=\"/css/image-crop\\.css[^\"]*\"").Should().BeTrue(
            "the page links the crop sheet itself");
        Regex.IsMatch(html, "<script\\b[^>]*type=\"module\"[^>]*src=\"/js/image-crop\\.js[^\"]*\"").Should().BeTrue(
            "the module drives the dialog");
        var cropper = Regex.Match(html, "<script\\b[^>]*src=\"/lib/cropperjs-2\\.1\\.1/cropper\\.min\\.js\"[^>]*>");
        cropper.Success.Should().BeTrue("the vendored Cropper script loads first");
        cropper.Value.Should().NotContain("type=\"module\"", "Cropper is a classic script");
        html.Should().NotContain("initImageCrop");
    }

    [Theory]
    [MemberData(nameof(EveryHostUnderBothLayouts))]
    public async Task Host_FileInputCarriesTheCropDataAndTheCroppedInputIsHiddenByAttribute(string host, bool mobile)
    {
        var seeded = await SeedHostsAsync();

        var html = await GetHtmlAsync(seeded.Client, UrlFor(host, seeded), mobile);

        var fileInput = Regex.Match(html, "<input\\b[^>]*data-crop-input[^>]*>", RegexOptions.Singleline);
        fileInput.Success.Should().BeTrue("the picture input is marked for the module");
        fileInput.Value.Should().Contain("type=\"file\"");
        fileInput.Value.Should().Contain("data-crop-output-name=\"CroppedPictureFile\"");
        fileInput.Value.Should().Contain("data-crop-aspect-ratio=\"1\"");

        var cropped = Regex.Match(html, "<input\\b[^>]*\\sname=\"CroppedPictureFile\"[^>]*>");
        cropped.Success.Should().BeTrue("the form keeps the input the cropped picture is written to");
        cropped.Value.Should().Contain("type=\"file\"");
        Regex.IsMatch(cropped.Value, @"\shidden(\s|=|/|>)").Should().BeTrue("it is hidden by the hidden attribute");
        cropped.Value.Should().NotContain("style=");
        cropped.Value.Should().NotContain("disabled", "a disabled input would not post");

        var chip = Regex.Match(html, "<div id=\"fileSizeError\"[^>]*>", RegexOptions.Singleline);
        chip.Success.Should().BeTrue();
        Regex.IsMatch(chip.Value, @"\shidden(\s|=|>)").Should().BeTrue("the refusal chip shows by removing hidden");
        chip.Value.Should().Contain("role=\"alert\"");
        chip.Value.Should().NotContain("style=");
    }

    [Theory]
    [InlineData("character-edit", false)]
    [InlineData("character-edit", true)]
    [InlineData("contact-edit", false)]
    [InlineData("contact-edit", true)]
    [InlineData("dm-edit", false)]
    [InlineData("dm-edit", true)]
    public async Task EditHost_RecropButtonCarriesTheStoredOriginalUrl(string host, bool mobile)
    {
        var seeded = await SeedHostsAsync();

        var html = await GetHtmlAsync(seeded.Client, UrlFor(host, seeded), mobile);

        var button = Regex.Match(html, "<button\\b[^>]*id=\"recropPhotoBtn\"[^>]*>", RegexOptions.Singleline);
        button.Success.Should().BeTrue("a stored picture offers Re-crop Photo");
        button.Value.Should().MatchRegex("data-crop-original-url=\"/[^\"]+\"");
    }
}
