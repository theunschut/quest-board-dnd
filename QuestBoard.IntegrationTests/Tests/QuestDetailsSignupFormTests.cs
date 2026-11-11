using System.Net;
using System.Text.RegularExpressions;
using FluentAssertions;
using QuestBoard.Domain.Enums;
using QuestBoard.IntegrationTests.Helpers;

namespace QuestBoard.IntegrationTests.Tests;

/// <summary>
/// Pins what the open-quest signup form renders. Choosing Spectator must hide only the date voting,
/// so the voting sits inside one marked wrapper that the page script toggles and that no other card
/// carries. Hiding itself needs a browser; what a rendered request can hold is that the marker is
/// in the right place and the instruction line is a polite live region.
/// </summary>
public class QuestDetailsSignupFormTests : IClassFixture<WebApplicationFactoryBase>
{
    private const string DesktopUserAgent =
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36";

    private const string MobileUserAgent =
        "Mozilla/5.0 (iPhone; CPU iPhone OS 17_0 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.0 Mobile/15E148 Safari/604.1";

    private static readonly Regex HiddenInput = new(@"<input\b[^>]*\btype=""hidden""[^>]*>", RegexOptions.Compiled);
    private static readonly Regex NameAttribute = new(@"\bname=""(?<v>[^""]*)""", RegexOptions.Compiled);
    private static readonly Regex ValueAttribute = new(@"\bvalue=""(?<v>[^""]*)""", RegexOptions.Compiled);

    private static readonly Regex SignupFormStart = new(
        @"<form\b[^>]*\baction=""/Quest/Details(/\d+)?""[^>]*>",
        RegexOptions.Compiled);

    private readonly WebApplicationFactoryBase _factory;

    public QuestDetailsSignupFormTests(WebApplicationFactoryBase factory)
    {
        _factory = factory;
    }

    private static async Task<string> GetHtmlAsync(HttpClient client, string url, string userAgent)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.TryAddWithoutValidation("User-Agent", userAgent);
        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        response.StatusCode.Should().Be(HttpStatusCode.OK, $"{url} should render for this user");
        return await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
    }

    private static string SignupForm(string html)
    {
        var start = SignupFormStart.Match(html);
        start.Success.Should().BeTrue("the open quest renders the signup form");
        var end = html.IndexOf("</form>", start.Index, StringComparison.Ordinal);
        end.Should().BeGreaterThan(start.Index);
        return html.Substring(start.Index, end - start.Index);
    }

    private async Task<int> SeedOpenQuestAsync(string dmName, string title)
    {
        var dm = await AuthenticationHelper.CreateTestUserAsync(
            _factory.Services, dmName, $"{dmName}@test.com", name: dmName);
        var quest = await TestDataHelper.CreateTestQuestAsync(_factory.Services, dm.Id, title);
        await TestDataHelper.CreateProposedDateAsync(_factory.Services, quest.Id, DateTime.UtcNow.AddDays(10));
        await TestDataHelper.CreateProposedDateAsync(_factory.Services, quest.Id, DateTime.UtcNow.AddDays(11));
        return quest.Id;
    }

    [Fact]
    public async Task DesktopOpenQuest_VoteSectionIsOneWrapperInsideTheSignupFormAndOutsideDmControls()
    {
        var questId = await SeedOpenQuestAsync("signupform_dm1", "Signup Form Desktop Quest");
        var (client, _) = await AuthenticationHelper.CreateAuthenticatedAdminClientAsync(
            _factory, "signupform_admin1", "signupform_admin1@test.com", name: "Signupform Admin1");

        var html = await GetHtmlAsync(client, $"/Quest/Details/{questId}", DesktopUserAgent);

        Regex.Matches(html, "data-vote-section").Count.Should().Be(1, "one wrapper marks the date voting");
        SignupForm(html).Should().Contain("data-vote-section");

        var dmControls = html.IndexOf("DM Controls", StringComparison.Ordinal);
        dmControls.Should().BeGreaterThan(-1, "an admin who is not the quest's DM still sees the DM Controls card");
        html[dmControls..].Should().NotContain("data-vote-section");

        Regex.IsMatch(html, @"id=""votingInstruction""[^>]*role=""status""|role=""status""[^>]*id=""votingInstruction""")
            .Should().BeTrue("the instruction line is a polite live region");
    }
    private static async Task<HttpClient> SignedInPlayerAsync(
        WebApplicationFactoryBase factory, string userName, int? characterOwnerSeed = null)
    {
        var (client, user) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, userName, $"{userName}@test.com", name: userName);
        if (characterOwnerSeed.HasValue)
        {
            await TestDataHelper.CreateTestCharacterAsync(factory.Services, user.Id, $"{userName} Hero");
        }
        return client;
    }

    private static void AssertLabelNamesSelect(string html, string id)
    {
        html.Should().Contain($"<label for=\"{id}\"", $"the label of #{id} names its select");
        Regex.IsMatch(html, $@"<select\b[^>]*\bid=""{id}""").Should().BeTrue($"the select #{id} carries the id its label points at");
    }

    [Theory]
    [InlineData(DesktopUserAgent, "signupform_dm2", "signupform_player2")]
    [InlineData(MobileUserAgent, "signupform_dm3", "signupform_player3")]
    public async Task OpenQuest_BothLayouts_LabelsNameTheirSelectsAndDescribeThem(string userAgent, string dmName, string playerName)
    {
        var questId = await SeedOpenQuestAsync(dmName, $"Signup Form Labels {dmName}");
        var client = await SignedInPlayerAsync(_factory, playerName, characterOwnerSeed: 1);

        var form = SignupForm(await GetHtmlAsync(client, $"/Quest/Details/{questId}", userAgent));

        AssertLabelNamesSelect(form, "signupCharacter");
        AssertLabelNamesSelect(form, "selectedRole");
        form.Should().Contain("aria-describedby=\"signupCharacter-help\"").And.Contain("id=\"signupCharacter-help\"");
        form.Should().Contain("aria-describedby=\"selectedRole-help\"").And.Contain("id=\"selectedRole-help\"");
    }

    [Fact]
    public async Task PhoneOpenQuest_RendersCharacterAndRoleSelectsBeforeTheVoting()
    {
        var questId = await SeedOpenQuestAsync("signupform_dm4", "Signup Form Phone Quest");
        var client = await SignedInPlayerAsync(_factory, "signupform_player4", characterOwnerSeed: 1);

        var html = await GetHtmlAsync(client, $"/Quest/Details/{questId}", MobileUserAgent);
        var form = SignupForm(html);

        var character = form.IndexOf("name=\"CharacterId\"", StringComparison.Ordinal);
        var role = form.IndexOf("name=\"selectedRole\"", StringComparison.Ordinal);
        var voting = form.IndexOf("data-vote-section", StringComparison.Ordinal);
        character.Should().BeGreaterThan(-1, "the phone offers the character select");
        role.Should().BeGreaterThan(character);
        voting.Should().BeGreaterThan(role, "both selects sit above the date voting");
        form.Should().Contain("Select Your Role").And.Contain("Vote on Proposed Dates");
        form.Should().Contain("signupform_player4 Hero");
        Regex.Matches(html, "data-vote-section").Count.Should().Be(1);
    }

    [Fact]
    public async Task PhoneOpenQuest_PlayerWithoutCharacters_StillOffersTheNoCharacterOption()
    {
        var questId = await SeedOpenQuestAsync("signupform_dm5", "Signup Form No Characters Quest");
        var client = await SignedInPlayerAsync(_factory, "signupform_player5");

        var form = SignupForm(await GetHtmlAsync(client, $"/Quest/Details/{questId}", MobileUserAgent));

        form.Should().Contain("-- Sign up without character --");
    }

    [Theory]
    [InlineData(DesktopUserAgent, "signupform_dm6", "signupform_player6")]
    [InlineData(MobileUserAgent, "signupform_dm7", "signupform_player7")]
    public async Task OpenQuest_BoardWithoutCharacters_OffersNoCharacterSelect(string userAgent, string dmName, string playerName)
    {
        var questId = await SeedOpenQuestAsync(dmName, $"Signup Form Characters Off {dmName}");
        var client = await SignedInPlayerAsync(_factory, playerName);
        await TestDataHelper.SeedDefaultGroupAsync(_factory.Services);
        await BoardPageSeed.SetAsync(_factory, 1, BoardPageSet.Empty.With(BoardPage.Characters), BoardPage.Quests);
        try
        {
            var form = SignupForm(await GetHtmlAsync(client, $"/Quest/Details/{questId}", userAgent));

            form.Should().NotContain("name=\"CharacterId\"");
            form.Should().Contain("name=\"selectedRole\"");
        }
        finally
        {
            await BoardPageSeed.ResetAsync(_factory, 1);
        }
    }

    private async Task<(int Role, bool Selected)> PostSpectatorSignupAsync(string userAgent, string dmName, string playerName)
    {
        var questId = await SeedOpenQuestAsync(dmName, $"Signup Form Spectator Post {dmName}");
        var (client, player) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            _factory, playerName, $"{playerName}@test.com", name: playerName);

        var html = await GetHtmlAsync(client, $"/Quest/Details/{questId}", userAgent);
        var formStart = SignupFormStart.Match(html);
        var form = SignupForm(html);

        // Everything the browser would post for a Spectator: every hidden input the page rendered
        // (token, ids and the hidden vote inputs the Spectator choice hides but never removes)
        // plus the chosen role.
        var fields = new List<KeyValuePair<string, string>>();
        foreach (Match input in HiddenInput.Matches(form))
        {
            fields.Add(new(
                System.Net.WebUtility.HtmlDecode(NameAttribute.Match(input.Value).Groups["v"].Value),
                System.Net.WebUtility.HtmlDecode(ValueAttribute.Match(input.Value).Groups["v"].Value)));
        }
        fields.Should().Contain(f => f.Key.StartsWith("DateVotes[") && f.Key.EndsWith("].ProposedDateId"),
            "the hidden vote inputs are still in the form while Spectator is chosen");
        fields.Add(new("selectedRole", "1"));

        var action = Regex.Match(formStart.Value, @"action=""(?<v>[^""]+)""").Groups["v"].Value;
        var response = await client.PostAsync(action, new FormUrlEncodedContent(fields), TestContext.Current.CancellationToken);
        response.StatusCode.Should().Be(HttpStatusCode.Found);

        using var scope = _factory.Services.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<QuestBoardContext>();
        var signup = ctx.PlayerSignups.Single(ps => ps.QuestId == questId && ps.PlayerId == player.Id);
        return (signup.SignupRole, signup.IsSelected);
    }

    [Fact]
    public async Task SpectatorSignup_PostedFromThePhoneForm_IsRecordedExactlyAsFromTheDesktopForm()
    {
        var desktop = await PostSpectatorSignupAsync(DesktopUserAgent, "signupform_dm8", "signupform_player8");
        var phone = await PostSpectatorSignupAsync(MobileUserAgent, "signupform_dm9", "signupform_player9");

        desktop.Role.Should().Be((int)SignupRole.Spectator);
        phone.Should().Be(desktop);
    }
}
