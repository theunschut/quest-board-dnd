using System.Net;
using System.Text.RegularExpressions;
using QuestBoard.Domain.Enums;
using QuestBoard.IntegrationTests.Helpers;

namespace QuestBoard.IntegrationTests.Tests;

/// <summary>
/// Proves the rendered quest pages show a character's placeholder straight away when no picture is
/// stored, without asking for a picture URL that would answer 404, and still render the image with
/// its hidden placeholder sibling when one is.
/// </summary>
public class CharacterAvatarRenderTests : IClassFixture<WebApplicationFactoryBase>, IAsyncLifetime
{
    private const string DesktopUserAgent =
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36";

    private static readonly Regex PlaceholderSpan = new(
        "<span class=\"avatar-32 avatar-32--placeholder[^\"]*\"([^>]*)>", RegexOptions.Compiled);

    private readonly WebApplicationFactoryBase _factory;

    public CharacterAvatarRenderTests(WebApplicationFactoryBase factory)
    {
        _factory = factory;
    }

    public async ValueTask InitializeAsync() => await TestDataHelper.ClearDatabaseAsync(_factory.Services);

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private sealed record Signed(string Name, int CharacterId);

    // A signup for one player whose character has (or lacks) a stored picture.
    private async Task<Signed> SeedSignupAsync(
        int questId, string key, bool withPicture, bool isSelected, SignupRole role)
    {
        var player = await AuthenticationHelper.CreateTestUserAsync(
            _factory.Services, $"avatar{key}", $"avatar{key}@example.com");
        var name = withPicture ? $"Pictured {key} Hero" : $"Pictureless {key} Hero";
        var character = await TestDataHelper.CreateTestCharacterAsync(_factory.Services, player.Id, name);
        if (withPicture)
        {
            await SeedPictureAsync(character.Id);
        }

        await TestDataHelper.CreatePlayerSignupAsync(
            _factory.Services, questId, player.Id, signupRole: (int)role, isSelected: isSelected, characterId: character.Id);
        return new Signed(name, character.Id);
    }

    private async Task SeedPictureAsync(int characterId)
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<QuestBoardContext>();
        context.Set<CharacterImageEntity>().Add(new CharacterImageEntity
        {
            Id = characterId,
            OriginalImageData = [1, 2, 3, 4],
            CroppedImageData = [9, 9, 9, 9, 9]
        });
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private async Task<QuestEntity> SeedFinalizedQuestAsync(string key, DateTime finalizedDate)
    {
        var dm = await AuthenticationHelper.CreateTestUserAsync(
            _factory.Services, $"avatardm{key}", $"avatardm{key}@example.com");
        var quest = await TestDataHelper.CreateTestQuestAsync(
            _factory.Services, dm.Id, $"Avatar Quest {key}", isFinalized: true, finalizedDate: finalizedDate);
        await TestDataHelper.CreateProposedDateAsync(_factory.Services, quest.Id, finalizedDate);
        return quest;
    }

    private static async Task<string> GetHtmlAsync(HttpClient client, string url)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.TryAddWithoutValidation("User-Agent", DesktopUserAgent);
        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
    }

    // The table row, or list item, that holds the character's name.
    private static string RowOf(string html, string characterName, string tag)
    {
        var rows = Regex.Matches(html, $"<{tag}\\b.*?</{tag}>", RegexOptions.Singleline)
            .Select(m => m.Value)
            .Where(r => r.Contains(characterName, StringComparison.Ordinal))
            .ToList();
        rows.Should().ContainSingle($"the page lists {characterName} once, in a {tag}");
        return rows[0];
    }

    // The bare hidden attribute, not the hyphenated aria-hidden.
    private static bool HasHiddenAttribute(Match placeholder) =>
        Regex.IsMatch(placeholder.Groups[1].Value, @"(^|\s)hidden(\s|=|$)");

    private static string PictureUrl(int characterId) => $"GetCroppedPicture/{characterId}\"";

    private static void AssertPlaceholderOnly(string html, string row, Signed character)
    {
        var placeholder = PlaceholderSpan.Matches(row);
        placeholder.Should().ContainSingle();
        HasHiddenAttribute(placeholder[0]).Should().BeFalse("the placeholder is the avatar here");
        placeholder[0].Value.Should().Contain("aria-hidden=\"true\"");
        row.Should().Contain("fa-user");
        row.Should().NotContain("<img");
        html.Should().NotContain(PictureUrl(character.CharacterId), "a character without a picture asks for none");
    }

    private static void AssertPictureWithHiddenPlaceholder(string row, Signed character)
    {
        row.Should().Contain(PictureUrl(character.CharacterId));
        var placeholder = PlaceholderSpan.Matches(row);
        placeholder.Should().ContainSingle();
        HasHiddenAttribute(placeholder[0]).Should().BeTrue("avatar-fallback.js reveals it if the image later fails");
    }

    [Fact]
    public async Task QuestDetails_ParticipantRows_PlaceholderWithoutPicture_ImageWithPicture()
    {
        // Arrange
        var quest = await SeedFinalizedQuestAsync("pd", DateTime.UtcNow.AddDays(7));
        var withPicture = await SeedSignupAsync(quest.Id, "pdpic", withPicture: true, isSelected: true, SignupRole.Player);
        var withoutPicture = await SeedSignupAsync(quest.Id, "pdnone", withPicture: false, isSelected: true, SignupRole.Player);
        var (client, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            _factory, "avatarviewerpd", "avatarviewerpd@example.com");

        // Act
        var html = await GetHtmlAsync(client, $"/Quest/Details/{quest.Id}");

        // Assert
        AssertPlaceholderOnly(html, RowOf(html, withoutPicture.Name, "tr"), withoutPicture);
        AssertPictureWithHiddenPlaceholder(RowOf(html, withPicture.Name, "tr"), withPicture);
    }
}
