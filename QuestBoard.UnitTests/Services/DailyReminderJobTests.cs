using QuestBoard.Domain.Interfaces;
using QuestBoard.Domain.Models;
using QuestBoard.Domain.Models.QuestBoard;
using QuestBoard.Service.Jobs;
using Hangfire;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace QuestBoard.UnitTests.Services;

public class DailyReminderJobTests
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IQuestRepository _questRepository;
    private readonly IBackgroundJobClient _backgroundJobClient;
    private readonly DailyReminderJob _sut;

    public DailyReminderJobTests()
    {
        _questRepository = Substitute.For<IQuestRepository>();
        _backgroundJobClient = Substitute.For<IBackgroundJobClient>();

        var serviceProvider = Substitute.For<IServiceProvider>();
        serviceProvider.GetService(typeof(IQuestRepository)).Returns(_questRepository);

        var scope = Substitute.For<IServiceScope>();
        scope.ServiceProvider.Returns(serviceProvider);

        _scopeFactory = Substitute.For<IServiceScopeFactory>();
        _scopeFactory.CreateAsyncScope().Returns(new AsyncServiceScope(scope));

        var logger = Substitute.For<ILogger<DailyReminderJob>>();
        _sut = new DailyReminderJob(_scopeFactory, _backgroundJobClient, logger);
    }

    private static Quest MakeQuest(int id, int groupId = 1) =>
        new()
        {
            Id = id,
            GroupId = groupId,
            Title = $"Quest {id}",
            Description = "Desc",
            IsFinalized = true,
            FinalizedDate = DateTime.Today.AddDays(1),
            DungeonMaster = new User { Id = 99, Name = "DM" },
            PlayerSignups = [],
            ProposedDates = []
        };

    // ---------------------------------------------------------------------------
    // Date-filter and enqueue behavior
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_WhenTwoQuestsForTomorrow_EnqueuesTwoJobs()
    {
        // Arrange
        var quests = new List<Quest> { MakeQuest(1), MakeQuest(2) };
        _questRepository.GetQuestsForTomorrowAllGroupsAsync(Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(quests);

        // Act
        await _sut.ExecuteAsync(TestContext.Current.CancellationToken);

        // Assert: Enqueue<T> is an extension method; it delegates to IBackgroundJobClient.Create.
        // Verify Create was called twice (once per quest).
        _backgroundJobClient.Received(2).Create(Arg.Any<Hangfire.Common.Job>(), Arg.Any<Hangfire.States.IState>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenNoQuestsForTomorrow_EnqueuesNoJobs()
    {
        // Arrange
        _questRepository.GetQuestsForTomorrowAllGroupsAsync(Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(new List<Quest>());

        // Act
        await _sut.ExecuteAsync(TestContext.Current.CancellationToken);

        // Assert: no Create calls when there are no quests
        _backgroundJobClient.DidNotReceive().Create(Arg.Any<Hangfire.Common.Job>(), Arg.Any<Hangfire.States.IState>());
    }
}
