using QuestBoard.Domain.Interfaces;

namespace QuestBoard.IntegrationTests.Emails;

/// <summary>
/// Pins the Waitlist Promoted email's rendering of the Markdown Quest Description: it must be
/// rendered as formatted HTML (not HTML-encoded raw Markdown), with images stripped per the
/// Email render target's sanitizer profile.
/// </summary>
public class WaitlistPromotedMarkdownRenderTests(WebApplicationFactoryBase factory) : IClassFixture<WebApplicationFactoryBase>
{
    [Fact]
    public async Task WaitlistPromoted_MarkdownDescription_RendersFormattedHtmlWithImagesStripped()
    {
        // Arrange
        using var scope = factory.Services.CreateScope();
        var emailRenderService = scope.ServiceProvider.GetRequiredService<IEmailRenderService>();
        const string appUrl = "https://example.com";

        // Act
        var html = await emailRenderService.RenderAsync<Service.Components.Emails.WaitlistPromoted>(new()
        {
            [nameof(Service.Components.Emails.WaitlistPromoted.QuestTitle)] = "The Tomb of Annihilation",
            [nameof(Service.Components.Emails.WaitlistPromoted.DmName)] = "Dungeon Master Theomund",
            [nameof(Service.Components.Emails.WaitlistPromoted.QuestDate)] = DateTime.Today.AddDays(7),
            [nameof(Service.Components.Emails.WaitlistPromoted.QuestDescription)] = "**bold description** with an ![logo](http://example.com/x.png) image",
            [nameof(Service.Components.Emails.WaitlistPromoted.PlayerName)] = "Arannis",
            [nameof(Service.Components.Emails.WaitlistPromoted.QuestUrl)] = $"{appUrl}/Quest",
            [nameof(Service.Components.Emails.WaitlistPromoted.ChallengeRating)] = 9,
            [nameof(Service.Components.Emails.WaitlistPromoted.AppUrl)] = appUrl,
        });

        // Assert
        html.Should().Contain("<strong>bold description</strong>");
        html.Should().NotContain("**bold description**");
        // The layout template itself has a static Wax Seal <img>, unrelated to the Description
        // markdown — scope the "images stripped" assertion to the test-supplied image specifically.
        html.Should().NotContain("http://example.com/x.png");
        html.Should().NotContain("alt=\"logo\"");
    }

    [Fact]
    public async Task WaitlistPromoted_MultiBlockMarkdownDescription_KeepsStyledWrapperIntact()
    {
        // Arrange
        using var scope = factory.Services.CreateScope();
        var emailRenderService = scope.ServiceProvider.GetRequiredService<IEmailRenderService>();
        const string appUrl = "https://example.com";

        // Act
        var html = await emailRenderService.RenderAsync<Service.Components.Emails.WaitlistPromoted>(new()
        {
            [nameof(Service.Components.Emails.WaitlistPromoted.QuestTitle)] = "The Tomb of Annihilation",
            [nameof(Service.Components.Emails.WaitlistPromoted.DmName)] = "Dungeon Master Theomund",
            [nameof(Service.Components.Emails.WaitlistPromoted.QuestDate)] = DateTime.Today.AddDays(7),
            [nameof(Service.Components.Emails.WaitlistPromoted.QuestDescription)] = "Paragraph one\n\nParagraph two\n\n## A heading\n\n- item 1\n- item 2",
            [nameof(Service.Components.Emails.WaitlistPromoted.PlayerName)] = "Arannis",
            [nameof(Service.Components.Emails.WaitlistPromoted.QuestUrl)] = $"{appUrl}/Quest",
            [nameof(Service.Components.Emails.WaitlistPromoted.ChallengeRating)] = 9,
            [nameof(Service.Components.Emails.WaitlistPromoted.AppUrl)] = appUrl,
        });

        // Assert
        // A <p> wrapper around multi-block Markdown output is illegal HTML (a <p> cannot legally
        // contain block elements like <h2>/<li>) and gets implicitly closed/emptied by a real
        // browser as soon as it hits the first nested block tag, dropping the styled wrapper's
        // italic/color/text-shadow styling for every paragraph after the first. This asserts
        // directly on the wrapper element itself (must be a <div>, not a <p>) rather than trying
        // to infer browser auto-close behavior from the raw, unparsed render-service output string.
        html.Should().MatchRegex(@"<div\b[^>]*font-style:italic[^>]*>\s*<p[\s>]");
        html.Should().Contain("Paragraph one");
        html.Should().Contain("Paragraph two");
        html.Should().Contain("<h2>A heading</h2>");
        html.Should().Contain("<li>item 1</li>");
    }
}
