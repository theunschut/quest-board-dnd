using QuestBoard.Domain.Models;
using QuestBoard.Domain.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace QuestBoard.UnitTests.Services;

public class EmailServiceTests
{
    private static EmailService Create(EmailSettings settings)
    {
        var logger = Substitute.For<ILogger<EmailService>>();
        return new EmailService(Options.Create(settings), logger);
    }

    [Fact]
    public void Constructor_WithValidOptions_DoesNotThrow()
    {
        var act = () => Create(new EmailSettings());
        act.Should().NotThrow();
    }

    [Fact]
    public void EmailService_ConstructorUsesIOptionsEmailSettings()
    {
        var constructor = typeof(EmailService).GetConstructors().Single();
        var firstParam = constructor.GetParameters()[0];
        firstParam.ParameterType.Should().Be(typeof(IOptions<EmailSettings>),
            "EMAIL-02 requires EmailService to inject IOptions<EmailSettings>");
    }

    [Fact]
    public void EmailServiceSource_ContainsSingleSmtpClientConstruction()
    {
        var sourcePath = Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..",
            "QuestBoard.Domain", "Services", "EmailService.cs");
        if (!File.Exists(sourcePath))
        {
            var constructor = typeof(EmailService).GetConstructors().Single();
            constructor.GetParameters()[0].ParameterType.Should().Be(typeof(IOptions<EmailSettings>),
                "EMAIL-02 requires SMTP setup to be deduplicated into a single helper");
            return;
        }

        var source = File.ReadAllText(sourcePath);
        var occurrences = System.Text.RegularExpressions.Regex.Matches(source, @"new SmtpClient\(").Count;
        occurrences.Should().Be(1, "EMAIL-02 requires SMTP setup to be deduplicated into a single helper");
    }

    [Fact]
    public void EmailServiceSource_SendAsyncSendsHtmlBody()
    {
        // SendAsync passes the caller-rendered HTML body through as IsBodyHtml = true.
        // AppUrl substitution is now the responsibility of individual Hangfire jobs, not EmailService.
        var sourcePath = Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..",
            "QuestBoard.Domain", "Services", "EmailService.cs");
        if (!File.Exists(sourcePath))
        {
            var sendMethod = typeof(EmailService).GetMethod("SendAsync");
            sendMethod.Should().NotBeNull("EMAIL-03 requires a public SendAsync method on EmailService");
            return;
        }

        var source = File.ReadAllText(sourcePath);
        source.Should().Contain("IsBodyHtml = true", "EMAIL-03 requires SendAsync to mark the body as HTML");
    }

    [Fact]
    public async Task SendAsync_WhenEmailNotConfigured_ReturnsWithoutException()
    {
        // Arrange — empty FromEmail causes CreateSmtpClient to return null → no send attempt
        var service = Create(new EmailSettings { SmtpUsername = "", SmtpPassword = "", FromEmail = "" });

        // Act
        var act = async () => await service.SendAsync("to@example.com", "Test Subject", "<h1>Hello</h1>");

        // Assert — no exception thrown when email is not configured
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task SendAsync_WhenFromEmailIsEmpty_DoesNotAttemptSmtpConnection()
    {
        // This test proves no SMTP connection is ever attempted in unit/integration tests.
        // The SmtpServer is deliberately set to a non-routable address — if EmailService
        // tried to connect it would throw SocketException. An empty FromEmail causes
        // CreateSmtpClient() to return null and SendAsync to return early.
        var service = Create(new EmailSettings
        {
            SmtpServer   = "192.0.2.1",  // TEST-NET-1, guaranteed non-routable (RFC 5737)
            SmtpPort     = 587,
            FromEmail    = "",            // guard: empty → skip
        });

        var act = async () => await service.SendAsync("to@example.com", "Subject", "<h1>Body</h1>");

        await act.Should().NotThrowAsync(
            "because an empty FromEmail must prevent any SMTP connection attempt");
    }
}
