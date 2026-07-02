using QuestBoard.Domain.Interfaces;
using QuestBoard.Domain.Models;
using QuestBoard.Service.Components.Emails;
using QuestBoard.Service.Jobs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace QuestBoard.UnitTests.Services;

public class WelcomeEmailJobTests
{
    private readonly IEmailRenderService _renderService;
    private readonly IEmailService _emailService;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly WelcomeEmailJob _sut;

    public WelcomeEmailJobTests()
    {
        _renderService = Substitute.For<IEmailRenderService>();
        _emailService  = Substitute.For<IEmailService>();

        var emailOptions = Substitute.For<IOptions<EmailSettings>>();
        emailOptions.Value.Returns(new EmailSettings { AppUrl = "https://example.com" });

        // Build the IServiceScopeFactory → IServiceScope → IServiceProvider chain
        var serviceProvider = Substitute.For<IServiceProvider>();
        serviceProvider.GetService(typeof(IEmailRenderService)).Returns(_renderService);
        serviceProvider.GetService(typeof(IEmailService)).Returns(_emailService);
        serviceProvider.GetService(typeof(IOptions<EmailSettings>)).Returns(emailOptions);

        var scope = Substitute.For<IServiceScope>();
        scope.ServiceProvider.Returns(serviceProvider);

        _scopeFactory = Substitute.For<IServiceScopeFactory>();
        _scopeFactory.CreateAsyncScope().Returns(new AsyncServiceScope(scope));

        var logger = Substitute.For<ILogger<WelcomeEmailJob>>();
        _sut = new WelcomeEmailJob(_scopeFactory, logger);
    }

    [Fact]
    public async Task ExecuteAsync_CallsRenderAsync_WithCorrectParameters()
    {
        // Arrange
        const string toEmail     = "player@example.com";
        const string userName    = "TestUser";
        const string callbackUrl = "https://example.com/Account/SetPassword?userId=1&token=abc";

        _renderService
            .RenderAsync<Welcome>(Arg.Any<Dictionary<string, object?>>())
            .Returns(Task.FromResult("<html>welcome-body</html>"));

        // Act
        await _sut.ExecuteAsync(toEmail, userName, callbackUrl, isNewAccount: true, cancellationToken: TestContext.Current.CancellationToken);

        // Assert: RenderAsync called once with the expected render-parameter dictionary
        await _renderService.Received(1).RenderAsync<Welcome>(
            Arg.Is<Dictionary<string, object?>>(d =>
                object.Equals(d[nameof(Welcome.UserName)],      "TestUser") &&
                object.Equals(d[nameof(Welcome.CallbackUrl)],   callbackUrl) &&
                object.Equals(d[nameof(Welcome.AppUrl)],        "https://example.com") &&
                object.Equals(d[nameof(Welcome.IsNewAccount)],  true)));
    }

    [Fact]
    public async Task ExecuteAsync_WhenNotNewAccount_PassesIsNewAccountFalse()
    {
        // Arrange — legacy account (already has a password, resending welcome to confirm email)
        const string toEmail     = "player@example.com";
        const string userName    = "TestUser";
        const string callbackUrl = "https://example.com/Account/SetPassword?userId=1&token=abc";

        _renderService
            .RenderAsync<Welcome>(Arg.Any<Dictionary<string, object?>>())
            .Returns(Task.FromResult("<html>welcome-body</html>"));

        // Act
        await _sut.ExecuteAsync(toEmail, userName, callbackUrl, isNewAccount: false, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        await _renderService.Received(1).RenderAsync<Welcome>(
            Arg.Is<Dictionary<string, object?>>(d =>
                object.Equals(d[nameof(Welcome.IsNewAccount)], false)));
    }

    [Fact]
    public async Task ExecuteAsync_CallsSendAsync_WithRenderedHtml()
    {
        // Arrange
        const string toEmail      = "player@example.com";
        const string sentinelHtml = "<html>welcome-body</html>";

        _renderService
            .RenderAsync<Welcome>(Arg.Any<Dictionary<string, object?>>())
            .Returns(Task.FromResult(sentinelHtml));

        // Act
        await _sut.ExecuteAsync(toEmail, "TestUser", "https://example.com/Account/SetPassword?userId=1&token=abc", isNewAccount: true, cancellationToken: TestContext.Current.CancellationToken);

        // Assert: SendAsync called with exact recipient, subject, and HTML sentinel
        await _emailService.Received(1).SendAsync(
            "player@example.com",
            "Welcome to the D&D Quest Board — set your password",
            sentinelHtml);
    }
}
