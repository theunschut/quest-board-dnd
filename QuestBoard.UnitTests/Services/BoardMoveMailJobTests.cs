using QuestBoard.Domain.Enums;
using QuestBoard.Domain.Interfaces;
using QuestBoard.Domain.Models;
using QuestBoard.Service.Components.Emails;
using QuestBoard.Service.Jobs;
using QuestBoard.Service.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace QuestBoard.UnitTests.Services;

public class BoardMoveMailJobTests
{
    private const int QuestId = 42;
    private const int TargetGroupId = 7;
    private const string AppUrl = "https://example.com";

    private readonly IEmailRenderService _renderService;
    private readonly IEmailService _emailService;
    private readonly ActiveGroupContextService _groupContext;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly QuestMovedEmailJob _movedJob;
    private readonly QuestWaitlistPromotedEmailJob _promotedJob;

    public BoardMoveMailJobTests()
    {
        _renderService = Substitute.For<IEmailRenderService>();
        _emailService = Substitute.For<IEmailService>();
        _renderService.RenderAsync<QuestMoved>(Arg.Any<Dictionary<string, object?>>())
            .Returns(Task.FromResult("<html>moved</html>"));
        _renderService.RenderAsync<WaitlistPromoted>(Arg.Any<Dictionary<string, object?>>())
            .Returns(Task.FromResult("<html>promoted</html>"));

        var emailOptions = Substitute.For<IOptions<EmailSettings>>();
        emailOptions.Value.Returns(new EmailSettings { AppUrl = AppUrl });

        _groupContext = new ActiveGroupContextService(Substitute.For<IHttpContextAccessor>());

        var serviceProvider = Substitute.For<IServiceProvider>();
        serviceProvider.GetService(typeof(ActiveGroupContextService)).Returns(_groupContext);
        serviceProvider.GetService(typeof(IEmailRenderService)).Returns(_renderService);
        serviceProvider.GetService(typeof(IEmailService)).Returns(_emailService);
        serviceProvider.GetService(typeof(IOptions<EmailSettings>)).Returns(emailOptions);

        var scope = Substitute.For<IServiceScope>();
        scope.ServiceProvider.Returns(serviceProvider);

        _scopeFactory = Substitute.For<IServiceScopeFactory>();
        _scopeFactory.CreateScope().Returns(scope);

        _movedJob = new QuestMovedEmailJob(_scopeFactory, Substitute.For<ILogger<QuestMovedEmailJob>>());
        _promotedJob = new QuestWaitlistPromotedEmailJob(_scopeFactory, Substitute.For<ILogger<QuestWaitlistPromotedEmailJob>>());
    }

    [Theory]
    [InlineData(QuestMovedAudience.Player, "Quest moved to The Silver Tankard: The Tomb")]
    [InlineData(QuestMovedAudience.DungeonMaster, "Quest moved to The Silver Tankard: The Tomb")]
    [InlineData(QuestMovedAudience.Removed, "Your signup was removed: The Tomb")]
    public async Task QuestMoved_RendersParametersAndSendsToTheOneRecipient(QuestMovedAudience audience, string expectedSubject)
    {
        var sessionDate = new DateTime(2031, 6, 14, 19, 0, 0, DateTimeKind.Unspecified);

        await _movedJob.ExecuteAsync(
            QuestId, TargetGroupId, audience, "player@example.com",
            "The Tomb", "Theomund", "The Silver Tankard", sessionDate,
            TestContext.Current.CancellationToken);

        await _renderService.Received(1).RenderAsync<QuestMoved>(
            Arg.Is<Dictionary<string, object?>>(d =>
                object.Equals(d[nameof(QuestMoved.QuestTitle)], "The Tomb") &&
                object.Equals(d[nameof(QuestMoved.DmName)], "Theomund") &&
                object.Equals(d[nameof(QuestMoved.BoardName)], "The Silver Tankard") &&
                object.Equals(d[nameof(QuestMoved.QuestUrl)], $"{AppUrl}/Quest/Details/{QuestId}") &&
                object.Equals(d[nameof(QuestMoved.AppUrl)], AppUrl) &&
                object.Equals(d[nameof(QuestMoved.Audience)], audience) &&
                object.Equals(d[nameof(QuestMoved.SessionDate)], sessionDate) &&
                ((DateTime)d[nameof(QuestMoved.SessionDate)]!).Kind == sessionDate.Kind));

        await _emailService.Received(1).SendAsync("player@example.com", expectedSubject, "<html>moved</html>");
        _groupContext.ActiveGroupId.Should().Be(TargetGroupId);
    }

    [Fact]
    public async Task QuestMoved_NullSessionDate_PassesNullThrough()
    {
        await _movedJob.ExecuteAsync(
            QuestId, TargetGroupId, QuestMovedAudience.Removed, "player@example.com",
            "The Tomb", "Theomund", "The Silver Tankard", null,
            TestContext.Current.CancellationToken);

        await _renderService.Received(1).RenderAsync<QuestMoved>(
            Arg.Is<Dictionary<string, object?>>(d => d[nameof(QuestMoved.SessionDate)] == null));
    }

    [Fact]
    public async Task WaitlistPromotedAfterMove_RendersMovedBoardNameUnderGivenGroup()
    {
        var finalized = new DateTime(2031, 6, 14, 19, 0, 0);

        await _promotedJob.ExecuteAfterMoveAsync(
            QuestId, TargetGroupId, finalized, "player@example.com", "Arannis",
            "The Tomb", "Theomund", "Desc", 9, "The Silver Tankard",
            TestContext.Current.CancellationToken);

        await _renderService.Received(1).RenderAsync<WaitlistPromoted>(
            Arg.Is<Dictionary<string, object?>>(d =>
                object.Equals(d[nameof(WaitlistPromoted.MovedToBoardName)], "The Silver Tankard") &&
                object.Equals(d[nameof(WaitlistPromoted.QuestTitle)], "The Tomb") &&
                object.Equals(d[nameof(WaitlistPromoted.QuestDate)], finalized)));
        await _emailService.Received(1).SendAsync("player@example.com", "A seat opened up: The Tomb", "<html>promoted</html>");
        _groupContext.ActiveGroupId.Should().Be(TargetGroupId);
    }

    [Fact]
    public async Task WaitlistPromoted_ExistingEntryPoint_RendersWithoutBoardName()
    {
        await _promotedJob.ExecuteAsync(
            QuestId, TargetGroupId, new DateTime(2031, 6, 14, 19, 0, 0), "player@example.com", "Arannis",
            "The Tomb", "Theomund", "Desc", 9,
            TestContext.Current.CancellationToken);

        await _renderService.Received(1).RenderAsync<WaitlistPromoted>(
            Arg.Is<Dictionary<string, object?>>(d =>
                !d.ContainsKey(nameof(WaitlistPromoted.MovedToBoardName)) &&
                object.Equals(d[nameof(WaitlistPromoted.PlayerName)], "Arannis")));
        await _emailService.Received(1).SendAsync("player@example.com", "A seat opened up: The Tomb", "<html>promoted</html>");
    }
}
