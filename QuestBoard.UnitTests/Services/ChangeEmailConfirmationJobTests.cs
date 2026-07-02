using QuestBoard.Domain.Interfaces;
using QuestBoard.Domain.Models;
using QuestBoard.Service.Components.Emails;
using QuestBoard.Service.Jobs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace QuestBoard.UnitTests.Services;

public class ChangeEmailConfirmationJobTests
{
    private readonly IEmailRenderService _renderService;
    private readonly IEmailService _emailService;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ChangeEmailConfirmationJob _sut;

    public ChangeEmailConfirmationJobTests()
    {
        _renderService = Substitute.For<IEmailRenderService>();
        _emailService  = Substitute.For<IEmailService>();

        var emailOptions = Substitute.For<IOptions<EmailSettings>>();
        emailOptions.Value.Returns(new EmailSettings { AppUrl = "https://example.com" });

        var serviceProvider = Substitute.For<IServiceProvider>();
        serviceProvider.GetService(typeof(IEmailRenderService)).Returns(_renderService);
        serviceProvider.GetService(typeof(IEmailService)).Returns(_emailService);
        serviceProvider.GetService(typeof(IOptions<EmailSettings>)).Returns(emailOptions);

        var scope = Substitute.For<IServiceScope>();
        scope.ServiceProvider.Returns(serviceProvider);

        _scopeFactory = Substitute.For<IServiceScopeFactory>();
        _scopeFactory.CreateAsyncScope().Returns(new AsyncServiceScope(scope));

        var logger = Substitute.For<ILogger<ChangeEmailConfirmationJob>>();
        _sut = new ChangeEmailConfirmationJob(_scopeFactory, logger);
    }

    [Fact]
    public async Task ExecuteAsync_CallsRenderAsync_WithCorrectParameters()
    {
        // Arrange
        const string toEmail     = "new@example.com";
        const string userName    = "TestUser";
        const string callbackUrl = "https://example.com/confirm-email-change?token=abc";

        _renderService
            .RenderAsync<ChangeEmailConfirm>(Arg.Any<Dictionary<string, object?>>())
            .Returns(Task.FromResult("<html>change-email-body</html>"));

        // Act
        await _sut.ExecuteAsync(toEmail, userName, callbackUrl, TestContext.Current.CancellationToken);

        // Assert: RenderAsync called once with the expected render-parameter dictionary
        await _renderService.Received(1).RenderAsync<ChangeEmailConfirm>(
            Arg.Is<Dictionary<string, object?>>(d =>
                object.Equals(d[nameof(ChangeEmailConfirm.UserName)],    "TestUser") &&
                object.Equals(d[nameof(ChangeEmailConfirm.CallbackUrl)], callbackUrl) &&
                object.Equals(d[nameof(ChangeEmailConfirm.AppUrl)],      "https://example.com")));
    }

    [Fact]
    public async Task ExecuteAsync_CallsSendAsync_WithRenderedHtmlAndCorrectSubject()
    {
        // Arrange
        const string toEmail      = "new@example.com";
        const string sentinelHtml = "<html>change-email-body</html>";

        _renderService
            .RenderAsync<ChangeEmailConfirm>(Arg.Any<Dictionary<string, object?>>())
            .Returns(Task.FromResult(sentinelHtml));

        // Act
        await _sut.ExecuteAsync(toEmail, "TestUser", "https://example.com/confirm-email-change?token=abc", TestContext.Current.CancellationToken);

        // Assert: SendAsync called with exact recipient, subject, and HTML sentinel
        await _emailService.Received(1).SendAsync(
            "new@example.com",
            "Confirm your new D&D Quest Board email address",
            sentinelHtml);
    }
}
