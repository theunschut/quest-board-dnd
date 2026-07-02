using QuestBoard.Domain.Interfaces;
using QuestBoard.Domain.Models;
using QuestBoard.Service.Components.Emails;
using QuestBoard.Service.Jobs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace QuestBoard.UnitTests.Services;

public class ForgotPasswordEmailJobTests
{
    private readonly IEmailRenderService _renderService;
    private readonly IEmailService _emailService;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ForgotPasswordEmailJob _sut;

    public ForgotPasswordEmailJobTests()
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

        var logger = Substitute.For<ILogger<ForgotPasswordEmailJob>>();
        _sut = new ForgotPasswordEmailJob(_scopeFactory, logger);
    }

    [Fact]
    public async Task ExecuteAsync_CallsRenderAsync_WithCorrectParameters()
    {
        // Arrange
        const string toEmail     = "player@example.com";
        const string callbackUrl = "https://example.com/Account/SetPassword?userId=1&token=abc";

        _renderService
            .RenderAsync<ForgotPassword>(Arg.Any<Dictionary<string, object?>>())
            .Returns(Task.FromResult("<html>forgot-password-body</html>"));

        // Act
        await _sut.ExecuteAsync(toEmail, callbackUrl, TestContext.Current.CancellationToken);

        // Assert: RenderAsync called once with the expected render-parameter dictionary (no UserName key)
        await _renderService.Received(1).RenderAsync<ForgotPassword>(
            Arg.Is<Dictionary<string, object?>>(d =>
                object.Equals(d[nameof(ForgotPassword.CallbackUrl)], callbackUrl) &&
                object.Equals(d[nameof(ForgotPassword.AppUrl)],      "https://example.com")));
    }

    [Fact]
    public async Task ExecuteAsync_CallsSendAsync_WithRenderedHtml()
    {
        // Arrange
        const string toEmail      = "player@example.com";
        const string sentinelHtml = "<html>forgot-password-body</html>";

        _renderService
            .RenderAsync<ForgotPassword>(Arg.Any<Dictionary<string, object?>>())
            .Returns(Task.FromResult(sentinelHtml));

        // Act
        await _sut.ExecuteAsync(toEmail, "https://example.com/Account/SetPassword?userId=1&token=abc", TestContext.Current.CancellationToken);

        // Assert: SendAsync called with exact recipient, subject, and HTML sentinel
        await _emailService.Received(1).SendAsync(
            "player@example.com",
            "Reset your D&D Quest Board password",
            sentinelHtml);
    }
}
