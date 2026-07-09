using QuestBoard.Domain.Interfaces;

namespace QuestBoard.IntegrationTests.Emails;

/// <summary>
/// Pins the Quest Finalized email's rendering of the Markdown Quest Description: it must be
/// rendered as formatted HTML (not HTML-encoded raw Markdown), with images stripped per the
/// Email render target's sanitizer profile.
/// </summary>
public class QuestFinalizedMarkdownRenderTests(WebApplicationFactoryBase factory) : IClassFixture<WebApplicationFactoryBase>
{
    [Fact]
    public async Task QuestFinalized_MarkdownDescription_RendersFormattedHtmlWithImagesStripped()
    {
        // Arrange
        using var scope = factory.Services.CreateScope();
        var emailRenderService = scope.ServiceProvider.GetRequiredService<IEmailRenderService>();
        const string appUrl = "https://example.com";

        // Act
        var html = await emailRenderService.RenderAsync<Service.Components.Emails.QuestFinalized>(new()
        {
            [nameof(Service.Components.Emails.QuestFinalized.QuestTitle)] = "The Tomb of Annihilation",
            [nameof(Service.Components.Emails.QuestFinalized.DmName)] = "Dungeon Master Theomund",
            [nameof(Service.Components.Emails.QuestFinalized.QuestDate)] = DateTime.Today.AddDays(7),
            [nameof(Service.Components.Emails.QuestFinalized.QuestDescription)] = "**bold description** with an ![logo](http://example.com/x.png) image",
            [nameof(Service.Components.Emails.QuestFinalized.ConfirmedPlayerNames)] = new List<string> { "Arannis", "Tordek" },
            [nameof(Service.Components.Emails.QuestFinalized.QuestUrl)] = $"{appUrl}/Quest",
            [nameof(Service.Components.Emails.QuestFinalized.ChallengeRating)] = 9,
            [nameof(Service.Components.Emails.QuestFinalized.AppUrl)] = appUrl,
        });

        // Assert
        html.Should().Contain("<strong>bold description</strong>");
        html.Should().NotContain("**bold description**");
        // The layout template itself has a static Wax Seal <img>, unrelated to the Description
        // markdown — scope the "images stripped" assertion to the test-supplied image specifically.
        html.Should().NotContain("http://example.com/x.png");
        html.Should().NotContain("alt=\"logo\"");
    }
}
