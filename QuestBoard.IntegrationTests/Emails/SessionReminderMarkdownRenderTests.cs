using QuestBoard.Domain.Interfaces;

namespace QuestBoard.IntegrationTests.Emails;

/// <summary>
/// Pins the Session Reminder email's rendering of the Markdown Quest Description: it must be
/// rendered as formatted HTML (not HTML-encoded raw Markdown), with images stripped per the
/// Email render target's sanitizer profile.
/// </summary>
public class SessionReminderMarkdownRenderTests(WebApplicationFactoryBase factory) : IClassFixture<WebApplicationFactoryBase>
{
    [Fact]
    public async Task SessionReminder_MarkdownDescription_RendersFormattedHtmlWithImagesStripped()
    {
        // Arrange
        using var scope = factory.Services.CreateScope();
        var emailRenderService = scope.ServiceProvider.GetRequiredService<IEmailRenderService>();
        const string appUrl = "https://example.com";

        // Act
        var html = await emailRenderService.RenderAsync<Service.Components.Emails.SessionReminder>(new()
        {
            [nameof(Service.Components.Emails.SessionReminder.QuestTitle)] = "The Tomb of Annihilation",
            [nameof(Service.Components.Emails.SessionReminder.DmName)] = "Dungeon Master Theomund",
            [nameof(Service.Components.Emails.SessionReminder.QuestDate)] = DateTime.Today.AddDays(1),
            [nameof(Service.Components.Emails.SessionReminder.QuestDescription)] = "**bold description** with an ![logo](http://example.com/x.png) image",
            [nameof(Service.Components.Emails.SessionReminder.ConfirmedPlayerNames)] = new List<string> { "Arannis", "Tordek" },
            [nameof(Service.Components.Emails.SessionReminder.QuestUrl)] = $"{appUrl}/Quest",
            [nameof(Service.Components.Emails.SessionReminder.ChallengeRating)] = 9,
            [nameof(Service.Components.Emails.SessionReminder.AppUrl)] = appUrl,
        });

        // Assert
        // RenderEmailHtml adds an inline style= attribute to <strong>, so match the tag loosely
        // instead of asserting the exact unstyled markup.
        html.Should().MatchRegex(@"<strong\b[^>]*>bold description</strong>");
        html.Should().NotContain("**bold description**");
        // The layout template itself has a static Wax Seal <img>, unrelated to the Description
        // markdown — scope the "images stripped" assertion to the test-supplied image specifically.
        html.Should().NotContain("http://example.com/x.png");
        html.Should().NotContain("alt=\"logo\"");
    }

    [Fact]
    public async Task SessionReminder_MultiBlockMarkdownDescription_KeepsStyledWrapperIntact()
    {
        // Arrange
        using var scope = factory.Services.CreateScope();
        var emailRenderService = scope.ServiceProvider.GetRequiredService<IEmailRenderService>();
        const string appUrl = "https://example.com";

        // Act
        var html = await emailRenderService.RenderAsync<Service.Components.Emails.SessionReminder>(new()
        {
            [nameof(Service.Components.Emails.SessionReminder.QuestTitle)] = "The Tomb of Annihilation",
            [nameof(Service.Components.Emails.SessionReminder.DmName)] = "Dungeon Master Theomund",
            [nameof(Service.Components.Emails.SessionReminder.QuestDate)] = DateTime.Today.AddDays(1),
            [nameof(Service.Components.Emails.SessionReminder.QuestDescription)] = "Paragraph one\n\nParagraph two\n\n## A heading\n\n- item 1\n- item 2",
            [nameof(Service.Components.Emails.SessionReminder.ConfirmedPlayerNames)] = new List<string> { "Arannis", "Tordek" },
            [nameof(Service.Components.Emails.SessionReminder.QuestUrl)] = $"{appUrl}/Quest",
            [nameof(Service.Components.Emails.SessionReminder.ChallengeRating)] = 9,
            [nameof(Service.Components.Emails.SessionReminder.AppUrl)] = appUrl,
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
        // RenderEmailHtml adds inline style= attributes to <h2>/<li> and an Outlook MSO bullet
        // fallback comment inside each <li>, so match loosely instead of the exact bare tags.
        html.Should().MatchRegex(@"<h2\b[^>]*>A heading</h2>");
        html.Should().MatchRegex(@"<li\b[^>]*>(?:<!--\[if mso\]>[^<]*<!\[endif\]-->)?item 1</li>");
    }
}
