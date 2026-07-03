using AutoMapper;
using QuestBoard.Domain.Enums;
using QuestBoard.Domain.Interfaces;
using QuestBoard.Domain.Models;
using QuestBoard.Domain.Models.QuestBoard;
using QuestBoard.Domain.Services;
using NSubstitute;

namespace QuestBoard.UnitTests.Services;

public class QuestServiceTests
{
    private readonly IQuestRepository _repository;
    private readonly IPlayerSignupRepository _playerSignupRepository;
    private readonly IQuestEmailDispatcher _dispatcher;
    private readonly IMapper _mapper;
    private readonly QuestService _sut;

    public QuestServiceTests()
    {
        _repository = Substitute.For<IQuestRepository>();
        _playerSignupRepository = Substitute.For<IPlayerSignupRepository>();
        _dispatcher = Substitute.For<IQuestEmailDispatcher>();
        _mapper = Substitute.For<IMapper>();

        _sut = new QuestService(_repository, _playerSignupRepository, _dispatcher, _mapper);
    }

    // Helper: create a quest with specified signups
    private static Quest MakeQuest(int id, IList<PlayerSignup>? signups = null) =>
        new()
        {
            Id = id,
            Title = "Test Quest",
            Description = "A quest",
            DungeonMaster = new User { Id = 1, Name = "DM Dave", Email = "dm@example.com" },
            PlayerSignups = signups ?? [],
            ProposedDates = []
        };

    private static PlayerSignup MakeSignup(int id, string email, SignupRole role = SignupRole.Player, bool isSelected = true, bool emailConfirmed = true) =>
        new()
        {
            Id = id,
            Role = role,
            IsSelected = isSelected,
            Player = new User { Id = id + 10, Name = $"Player {id}", Email = email, EmailConfirmed = emailConfirmed },
            Quest = new Quest { Id = 1, Title = "T", Description = "D" }
        };

    // ---------------------------------------------------------------------------
    // FinalizeQuestAsync
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task FinalizeQuestAsync_WhenQuestReFetchReturnsNull_SendsNoEmails()
    {
        // Arrange
        _repository.FinalizeQuestAsync(Arg.Any<int>(), Arg.Any<DateTime>(), Arg.Any<IList<int>>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        _repository.GetQuestWithDetailsAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns((Quest?)null);

        // Act
        await _sut.FinalizeQuestAsync(1, DateTime.UtcNow, [42], TestContext.Current.CancellationToken);

        // Assert: dispatcher is not called when quest re-fetch returns null
        _dispatcher.DidNotReceive().EnqueueFinalizedEmail(
            Arg.Any<int>(), Arg.Any<int>(), Arg.Any<DateTime>(), Arg.Any<string[]>(), Arg.Any<string[]>(),
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int>());
    }

    [Fact]
    public async Task FinalizeQuestAsync_WithMixedSelectedAndSpectatorSignups_DispatchesJobWithBothEmails()
    {
        // Arrange: signup 1 is selected player, signup 2 is spectator (auto-included), signup 3 is unselected player
        var signups = new List<PlayerSignup>
        {
            MakeSignup(1, "player1@x.com", SignupRole.Player),
            MakeSignup(2, "spectator@x.com", SignupRole.Spectator),
            MakeSignup(3, "player3@x.com", SignupRole.Player) // NOT in selectedPlayerIds
        };
        var quest = MakeQuest(1, signups);

        _repository.FinalizeQuestAsync(Arg.Any<int>(), Arg.Any<DateTime>(), Arg.Any<IList<int>>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        _repository.GetQuestWithDetailsAsync(1, Arg.Any<CancellationToken>())
            .Returns(quest);

        var selectedIds = new List<int> { 1 }; // Only signup id=1; spectator id=2 is auto-included

        // Act
        await _sut.FinalizeQuestAsync(1, DateTime.UtcNow, selectedIds, TestContext.Current.CancellationToken);

        // Assert: dispatcher receives one enqueue call with 2 emails (player1 + spectator), NOT player3
        _dispatcher.Received(1).EnqueueFinalizedEmail(
            1,
            Arg.Any<int>(),       // groupId — not checked in this test
            Arg.Any<DateTime>(),
            Arg.Is<string[]>(emails => emails.Contains("player1@x.com") && emails.Contains("spectator@x.com") && !emails.Contains("player3@x.com")),
            Arg.Any<string[]>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<int>());
    }

    // ---------------------------------------------------------------------------
    // UpdateQuestPropertiesWithNotificationsAsync
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task UpdateQuestPropertiesWithNotificationsAsync_WithNoAffectedPlayers_ReturnsOkZero()
    {
        // Arrange
        _repository.UpdateQuestPropertiesWithNotificationsAsync(
                Arg.Any<int>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int>(),
                Arg.Any<int>(), Arg.Any<bool>(), Arg.Any<bool>(), Arg.Any<IList<DateTime>?>(),
                Arg.Any<CancellationToken>())
            .Returns([]);

        // Act
        var result = await _sut.UpdateQuestPropertiesWithNotificationsAsync(1, "Title", "Desc", 5, 4, false, token: TestContext.Current.CancellationToken);

        // Assert
        result.Success.Should().BeTrue();
        result.Data.Should().Be(0);
        _dispatcher.DidNotReceive().EnqueueDateChangedEmail(
            Arg.Any<int>(), Arg.Any<string[]>(), Arg.Any<string[]>(),
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<DateTime>(), Arg.Any<DateTime>());
    }

    [Fact]
    public async Task UpdateQuestPropertiesWithNotificationsAsync_WithAffectedPlayers_DispatchesJobAndReturnsCount()
    {
        // Arrange
        var affectedPlayers = new List<User>
        {
            new() { Id = 1, Name = "Alice",   Email = "alice@x.com", EmailConfirmed = true  },
            new() { Id = 2, Name = "Bob",     Email = "bob@x.com",   EmailConfirmed = true  },
            new() { Id = 3, Name = "NoEmail", Email = "",            EmailConfirmed = true  } // should be skipped (empty email)
        };

        _repository.UpdateQuestPropertiesWithNotificationsAsync(
                Arg.Any<int>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int>(),
                Arg.Any<int>(), Arg.Any<bool>(), Arg.Any<bool>(), Arg.Any<IList<DateTime>?>(),
                Arg.Any<CancellationToken>())
            .Returns(affectedPlayers);

        var quest = MakeQuest(1);
        _repository.GetQuestWithDetailsAsync(1, Arg.Any<CancellationToken>())
            .Returns(quest);

        // Act
        var result = await _sut.UpdateQuestPropertiesWithNotificationsAsync(1, "Title", "Desc", 5, 4, false, token: TestContext.Current.CancellationToken);

        // Assert: only players with non-empty email are included in the dispatch
        result.Success.Should().BeTrue();
        result.Data.Should().Be(2, "only Alice and Bob have non-empty emails");

        _dispatcher.Received(1).EnqueueDateChangedEmail(
            1,
            Arg.Is<string[]>(emails => emails.Contains("alice@x.com") && emails.Contains("bob@x.com") && !emails.Contains("")),
            Arg.Is<string[]>(names => names.Contains("Alice") && names.Contains("Bob")),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<DateTime>(),
            Arg.Any<DateTime>());
    }

    // ---------------------------------------------------------------------------
    // CloseQuestAsync / ReopenQuestAsync
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task CloseQuestAsync_DelegatesToRepository_AndSendsNoEmail()
    {
        // Arrange
        _repository.CloseQuestAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        // Act
        await _sut.CloseQuestAsync(1, TestContext.Current.CancellationToken);

        // Assert
        await _repository.Received(1).CloseQuestAsync(1, Arg.Any<CancellationToken>());
        _dispatcher.DidNotReceiveWithAnyArgs().EnqueueFinalizedEmail(
            Arg.Any<int>(), Arg.Any<int>(), Arg.Any<DateTime>(), Arg.Any<string[]>(), Arg.Any<string[]>(),
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int>());
    }

    [Fact]
    public async Task ReopenQuestAsync_DelegatesToRepository_AndSendsNoEmail()
    {
        // Arrange
        _repository.ReopenQuestAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        // Act
        await _sut.ReopenQuestAsync(1, TestContext.Current.CancellationToken);

        // Assert
        await _repository.Received(1).ReopenQuestAsync(1, Arg.Any<CancellationToken>());
        _dispatcher.DidNotReceiveWithAnyArgs().EnqueueFinalizedEmail(
            Arg.Any<int>(), Arg.Any<int>(), Arg.Any<DateTime>(), Arg.Any<string[]>(), Arg.Any<string[]>(),
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int>());
    }

    // ---------------------------------------------------------------------------
    // GetCompletedQuestsAsync
    // ---------------------------------------------------------------------------

    private static Quest MakeCompletedQuestCandidate(
        int id, bool isFinalized, DateTime? finalizedDate, bool isClosed, DateTime? closedDate, bool dungeonMasterSession = false) =>
        new()
        {
            Id = id,
            Title = $"Quest {id}",
            Description = "A quest",
            IsFinalized = isFinalized,
            FinalizedDate = finalizedDate,
            IsClosed = isClosed,
            ClosedDate = closedDate,
            DungeonMasterSession = dungeonMasterSession,
            PlayerSignups = [],
            ProposedDates = []
        };

    [Fact]
    public async Task GetCompletedQuestsAsync_IncludesClosedCampaignQuest_WithNoNextDayWait()
    {
        // Arrange: closed today, never finalized — must still appear immediately (no next-day wait)
        var closedToday = MakeCompletedQuestCandidate(1, isFinalized: false, finalizedDate: null, isClosed: true, closedDate: DateTime.UtcNow);

        _repository.GetQuestsWithDetailsAsync(Arg.Any<CancellationToken>())
            .Returns((IList<Quest>)[closedToday]);

        // Act
        var result = await _sut.GetCompletedQuestsAsync(TestContext.Current.CancellationToken);

        // Assert
        result.Should().ContainSingle(q => q.Id == 1);
    }

    [Fact]
    public async Task GetCompletedQuestsAsync_PreservesOneShotNextDayWait()
    {
        // Arrange: one-shot finalized yesterday (included), one-shot finalized today (excluded — next-day wait)
        var finalizedYesterday = MakeCompletedQuestCandidate(1, isFinalized: true, finalizedDate: DateTime.UtcNow.AddDays(-2), isClosed: false, closedDate: null);
        var finalizedToday = MakeCompletedQuestCandidate(2, isFinalized: true, finalizedDate: DateTime.UtcNow, isClosed: false, closedDate: null);

        _repository.GetQuestsWithDetailsAsync(Arg.Any<CancellationToken>())
            .Returns((IList<Quest>)[finalizedYesterday, finalizedToday]);

        // Act
        var result = await _sut.GetCompletedQuestsAsync(TestContext.Current.CancellationToken);

        // Assert
        result.Should().ContainSingle(q => q.Id == 1);
        result.Should().NotContain(q => q.Id == 2);
    }

    [Fact]
    public async Task GetCompletedQuestsAsync_OrdersClosedAndFinalizedQuestsTogether_ClosedNotSortedAsNull()
    {
        // Arrange: an older one-shot finalized quest and a just-closed campaign quest (ClosedDate=now, FinalizedDate=null)
        // The closed quest must sort by ClosedDate, not fall to the bottom as a null FinalizedDate.
        var olderFinalized = MakeCompletedQuestCandidate(1, isFinalized: true, finalizedDate: DateTime.UtcNow.AddDays(-5), isClosed: false, closedDate: null);
        var justClosed = MakeCompletedQuestCandidate(2, isFinalized: false, finalizedDate: null, isClosed: true, closedDate: DateTime.UtcNow);

        _repository.GetQuestsWithDetailsAsync(Arg.Any<CancellationToken>())
            .Returns((IList<Quest>)[olderFinalized, justClosed]);

        // Act
        var result = await _sut.GetCompletedQuestsAsync(TestContext.Current.CancellationToken);

        // Assert: newest first — justClosed (ClosedDate=now) before olderFinalized (FinalizedDate=5 days ago)
        result.Should().HaveCount(2);
        result[0].Id.Should().Be(2);
        result[1].Id.Should().Be(1);
    }
}
