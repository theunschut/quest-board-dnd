using QuestBoard.Domain.Interfaces;
using QuestBoard.Domain.Models;
using QuestBoard.Domain.Models.QuestBoard;
using QuestBoard.Service.Jobs;
using QuestBoard.Service.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace QuestBoard.UnitTests.Services;

public class SessionReminderJobTests
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IQuestRepository _questRepository;
    private readonly IReminderLogRepository _reminderLog;
    private readonly IEmailRenderService _renderService;
    private readonly IEmailService _emailService;
    private readonly SessionReminderJob _sut;

    public SessionReminderJobTests()
    {
        _questRepository = Substitute.For<IQuestRepository>();
        _reminderLog     = Substitute.For<IReminderLogRepository>();
        _renderService   = Substitute.For<IEmailRenderService>();
        _emailService    = Substitute.For<IEmailService>();

        var emailOptions = Substitute.For<IOptions<EmailSettings>>();
        emailOptions.Value.Returns(new EmailSettings { AppUrl = "https://example.com" });

        // Build the IServiceScopeFactory → IServiceScope → IServiceProvider chain
        // ActiveGroupContextService must be resolvable — job calls GetRequiredService<ActiveGroupContextService>()
        var httpContextAccessor = Substitute.For<IHttpContextAccessor>();
        var groupContextService = new ActiveGroupContextService(httpContextAccessor);

        var serviceProvider = Substitute.For<IServiceProvider>();
        serviceProvider.GetService(typeof(ActiveGroupContextService)).Returns(groupContextService);
        serviceProvider.GetService(typeof(IQuestRepository)).Returns(_questRepository);
        serviceProvider.GetService(typeof(IReminderLogRepository)).Returns(_reminderLog);
        serviceProvider.GetService(typeof(IEmailRenderService)).Returns(_renderService);
        serviceProvider.GetService(typeof(IEmailService)).Returns(_emailService);
        serviceProvider.GetService(typeof(IOptions<EmailSettings>)).Returns(emailOptions);

        var scope = Substitute.For<IServiceScope>();
        scope.ServiceProvider.Returns(serviceProvider);

        _scopeFactory = Substitute.For<IServiceScopeFactory>();
        _scopeFactory.CreateAsyncScope().Returns(new AsyncServiceScope(scope));

        var logger = Substitute.For<ILogger<SessionReminderJob>>();
        _sut = new SessionReminderJob(_scopeFactory, logger);

        // Default: render returns empty HTML so SendAsync doesn't throw
        _renderService.RenderAsync<QuestBoard.Service.Components.Emails.SessionReminder>(Arg.Any<Dictionary<string, object?>>())
            .Returns(Task.FromResult("<html></html>"));
    }

    // Helper to build a finalized Quest with a player
    private static Quest MakeQuest(int id, bool isFinalized = true, DateTime? finalizedDate = null) =>
        new()
        {
            Id = id,
            Title = "Test Quest",
            Description = "Desc",
            ChallengeRating = 1,
            IsFinalized = isFinalized,
            FinalizedDate = finalizedDate ?? DateTime.Today.AddDays(1),
            DungeonMaster = new User { Id = 99, Name = "DM" },
            PlayerSignups = [],
            ProposedDates = []
        };

    private static PlayerSignup MakeSignup(Quest quest, int playerId, bool isSelected = true, string? email = "player@example.com", bool emailConfirmed = true) =>
        new()
        {
            Player = new User { Id = playerId, Name = $"Player{playerId}", Email = email, EmailConfirmed = emailConfirmed },
            IsSelected = isSelected,
            DateVotes = [],
            Quest = quest
        };

    // ---------------------------------------------------------------------------
    // Per-player dedup via ExistsAsync
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_WhenReminderAlreadySent_AndForceResendFalse_SkipsEmailSend()
    {
        // Arrange
        var quest = MakeQuest(1);
        var signup = MakeSignup(quest, playerId: 10);
        quest.PlayerSignups.Add(signup);

        _questRepository.GetQuestWithDetailsAsync(1, Arg.Any<CancellationToken>()).Returns(quest);
        _reminderLog.ExistsAsync(1, 10, Arg.Any<CancellationToken>()).Returns(true);

        // Act
        await _sut.ExecuteAsync(questId: 1, groupId: 1, forceResend: false, cancellationToken: TestContext.Current.CancellationToken);

        // Assert: email not sent, log entry not added
        await _emailService.DidNotReceive().SendAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>());
        await _reminderLog.DidNotReceive().AddAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenReminderAlreadySent_AndForceResendTrue_SendsEmail()
    {
        // Arrange
        var quest = MakeQuest(1);
        var signup = MakeSignup(quest, playerId: 10);
        quest.PlayerSignups.Add(signup);

        _questRepository.GetQuestWithDetailsAsync(1, Arg.Any<CancellationToken>()).Returns(quest);
        _reminderLog.ExistsAsync(1, 10, Arg.Any<CancellationToken>()).Returns(true);

        // Act
        await _sut.ExecuteAsync(questId: 1, groupId: 1, forceResend: true, cancellationToken: TestContext.Current.CancellationToken);

        // Assert: email IS sent and log IS added despite ExistsAsync returning true
        await _emailService.Received(1).SendAsync("player@example.com", Arg.Any<string>(), Arg.Any<string>());
        await _reminderLog.Received(1).AddAsync(1, 10, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenNoReminderSent_SendsEmailAndLogsEntry()
    {
        // Arrange
        var quest = MakeQuest(1);
        var signup = MakeSignup(quest, playerId: 10);
        quest.PlayerSignups.Add(signup);

        _questRepository.GetQuestWithDetailsAsync(1, Arg.Any<CancellationToken>()).Returns(quest);
        _reminderLog.ExistsAsync(1, 10, Arg.Any<CancellationToken>()).Returns(false);

        // Act
        await _sut.ExecuteAsync(questId: 1, groupId: 1, forceResend: false, cancellationToken: TestContext.Current.CancellationToken);

        // Assert: email sent and log entry added
        await _emailService.Received(1).SendAsync("player@example.com", Arg.Any<string>(), Arg.Any<string>());
        await _reminderLog.Received(1).AddAsync(1, 10, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenPlayerEmailIsNull_SkipsPlayer()
    {
        // Arrange
        var quest = MakeQuest(1);
        var signupNoEmail = MakeSignup(quest, playerId: 20, email: null);
        quest.PlayerSignups.Add(signupNoEmail);

        _questRepository.GetQuestWithDetailsAsync(1, Arg.Any<CancellationToken>()).Returns(quest);
        _reminderLog.ExistsAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(false);

        // Act
        await _sut.ExecuteAsync(questId: 1, groupId: 1, forceResend: false, cancellationToken: TestContext.Current.CancellationToken);

        // Assert: no email sent for player with null email
        await _emailService.DidNotReceive().SendAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenQuestNotFound_ReturnsWithoutException()
    {
        // Arrange
        _questRepository.GetQuestWithDetailsAsync(999, Arg.Any<CancellationToken>()).Returns((Quest?)null);

        // Act — must not throw
        var act = async () => await _sut.ExecuteAsync(questId: 999, groupId: 1, forceResend: false);

        // Assert
        await act.Should().NotThrowAsync();
        await _emailService.DidNotReceive().SendAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>());
    }

    // ---------------------------------------------------------------------------
    // EmailConfirmed guard in SessionReminderJob
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_WhenPlayerEmailNotConfirmed_SkipsPlayer()
    {
        // Arrange: IsSelected=true, email present, but EmailConfirmed=false
        var quest = MakeQuest(1);
        var signup = MakeSignup(quest, playerId: 30, isSelected: true, emailConfirmed: false);
        quest.PlayerSignups.Add(signup);

        _questRepository.GetQuestWithDetailsAsync(1, Arg.Any<CancellationToken>()).Returns(quest);
        _reminderLog.ExistsAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(false);

        // Act
        await _sut.ExecuteAsync(questId: 1, groupId: 1, forceResend: false, cancellationToken: TestContext.Current.CancellationToken);

        // Assert: no email sent for player with unconfirmed email
        await _emailService.DidNotReceive().SendAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenPlayerEmailConfirmed_SendsEmailRegression()
    {
        // Arrange: IsSelected=true, email present, EmailConfirmed=true (regression — happy path)
        var quest = MakeQuest(1);
        var signup = MakeSignup(quest, playerId: 40, isSelected: true, emailConfirmed: true);
        quest.PlayerSignups.Add(signup);

        _questRepository.GetQuestWithDetailsAsync(1, Arg.Any<CancellationToken>()).Returns(quest);
        _reminderLog.ExistsAsync(1, 40, Arg.Any<CancellationToken>()).Returns(false);

        // Act
        await _sut.ExecuteAsync(questId: 1, groupId: 1, forceResend: false, cancellationToken: TestContext.Current.CancellationToken);

        // Assert: email IS sent for confirmed player
        await _emailService.Received(1).SendAsync("player@example.com", Arg.Any<string>(), Arg.Any<string>());
    }

    // ---------------------------------------------------------------------------
    // Null DungeonMaster navigation property
    // ---------------------------------------------------------------------------

    // Verify-and-close: a quest can end up with no DungeonMaster row (e.g. the DM account was
    // removed). The job already reads quest.DungeonMaster?.Name ?? string.Empty, so this test
    // documents/locks that the null-safe read holds rather than fixing a bug.
    [Fact]
    public async Task ExecuteAsync_QuestWithNullDungeonMaster_DoesNotThrow()
    {
        // Arrange: finalized quest with DungeonMaster == null and one selected, email-confirmed signup
        var quest = MakeQuest(1);
        quest.DungeonMaster = null;
        var signup = MakeSignup(quest, playerId: 50, isSelected: true, emailConfirmed: true);
        quest.PlayerSignups.Add(signup);

        _questRepository.GetQuestWithDetailsAsync(1, Arg.Any<CancellationToken>()).Returns(quest);
        _reminderLog.ExistsAsync(1, 50, Arg.Any<CancellationToken>()).Returns(false);

        // Act — must not throw
        var act = async () => await _sut.ExecuteAsync(questId: 1, groupId: 1, forceResend: false, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        await act.Should().NotThrowAsync();
        await _emailService.Received(1).SendAsync("player@example.com", Arg.Any<string>(), Arg.Any<string>());
    }
}
