using AutoMapper;
using QuestBoard.Domain.Enums;
using QuestBoard.Domain.Interfaces;
using QuestBoard.Domain.Models;
using QuestBoard.Domain.Models.QuestBoard;
using QuestBoard.Domain.Services;
using NSubstitute;

namespace QuestBoard.UnitTests.Services;

/// <summary>
/// Proves that QuestService excludes unconfirmed recipients from both
/// string[]-array dispatch sites (QuestFinalizedEmailJob and QuestDateChangedEmailJob).
/// </summary>
public class EmailConfirmationJobGuardTests
{
    private readonly IQuestRepository _repository;
    private readonly IPlayerSignupRepository _playerSignupRepository;
    private readonly IQuestEmailDispatcher _dispatcher;
    private readonly IMapper _mapper;
    private readonly QuestService _sut;

    public EmailConfirmationJobGuardTests()
    {
        _repository = Substitute.For<IQuestRepository>();
        _playerSignupRepository = Substitute.For<IPlayerSignupRepository>();
        _dispatcher = Substitute.For<IQuestEmailDispatcher>();
        _mapper = Substitute.For<IMapper>();

        _sut = new QuestService(_repository, _playerSignupRepository, _dispatcher, _mapper);
    }

    // Helper: build a quest with the given signups
    private static Quest MakeQuest(int id, IList<PlayerSignup>? signups = null) =>
        new()
        {
            Id = id,
            Title = "Guard Test Quest",
            Description = "Description",
            DungeonMaster = new User { Id = 99, Name = "DM", Email = "dm@example.com", EmailConfirmed = true },
            PlayerSignups = signups ?? [],
            ProposedDates = []
        };

    private static PlayerSignup MakeSignup(int id, string email, bool emailConfirmed, SignupRole role = SignupRole.Player, bool isSelected = true) =>
        new()
        {
            Id = id,
            Role = role,
            IsSelected = isSelected,
            Player = new User { Id = id + 100, Name = $"Player{id}", Email = email, EmailConfirmed = emailConfirmed },
            Quest = new Quest { Id = 1, Title = "T", Description = "D" }
        };

    // ---------------------------------------------------------------------------
    // FinalizeQuestAsync — EmailConfirmed guard at QuestFinalizedEmailJob call site
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task FinalizeQuestAsync_ExcludesUnconfirmedPlayerEmail_FromRecipientArray()
    {
        // Arrange: confirmed player signup (id=1) and unconfirmed player signup (id=2)
        var confirmed   = MakeSignup(1, "confirmed@example.com",   emailConfirmed: true);
        var unconfirmed = MakeSignup(2, "unconfirmed@example.com", emailConfirmed: false);
        var quest = MakeQuest(1, [confirmed, unconfirmed]);

        _repository.FinalizeQuestAsync(Arg.Any<int>(), Arg.Any<DateTime>(), Arg.Any<IList<int>>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        _repository.GetQuestWithDetailsAsync(1, Arg.Any<CancellationToken>())
            .Returns(quest);

        // Both signup ids are in the selected list
        var selectedIds = new List<int> { 1, 2 };

        // Act
        await _sut.FinalizeQuestAsync(1, DateTime.UtcNow, selectedIds, TestContext.Current.CancellationToken);

        // Assert: dispatcher receives the confirmed email but NOT the unconfirmed one
        _dispatcher.Received(1).EnqueueFinalizedEmail(
            1,
            Arg.Any<int>(),       // groupId — not checked in this test
            Arg.Any<DateTime>(),
            Arg.Is<string[]>(emails =>
                emails.Contains("confirmed@example.com") &&
                !emails.Contains("unconfirmed@example.com")),
            Arg.Any<string[]>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<int>());
    }

    [Fact]
    public async Task FinalizeQuestAsync_WhenAllPlayersUnconfirmed_DoesNotDispatch()
    {
        // Arrange: only unconfirmed signups
        var unconfirmed1 = MakeSignup(1, "u1@example.com", emailConfirmed: false);
        var unconfirmed2 = MakeSignup(2, "u2@example.com", emailConfirmed: false);
        var quest = MakeQuest(1, [unconfirmed1, unconfirmed2]);

        _repository.FinalizeQuestAsync(Arg.Any<int>(), Arg.Any<DateTime>(), Arg.Any<IList<int>>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        _repository.GetQuestWithDetailsAsync(1, Arg.Any<CancellationToken>())
            .Returns(quest);

        var selectedIds = new List<int> { 1, 2 };

        // Act
        await _sut.FinalizeQuestAsync(1, DateTime.UtcNow, selectedIds, TestContext.Current.CancellationToken);

        // Assert: dispatcher is never called when no confirmed recipients remain
        _dispatcher.DidNotReceive().EnqueueFinalizedEmail(
            Arg.Any<int>(), Arg.Any<int>(), Arg.Any<DateTime>(), Arg.Any<string[]>(), Arg.Any<string[]>(),
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int>());
    }

    // ---------------------------------------------------------------------------
    // UpdateQuestPropertiesWithNotificationsAsync — EmailConfirmed guard at QuestDateChangedEmailJob call site
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task UpdateQuestPropertiesWithNotificationsAsync_ExcludesUnconfirmedPlayerEmail_FromDateChangedDispatch()
    {
        // Arrange: one confirmed and one unconfirmed affected player
        var affectedPlayers = new List<User>
        {
            new() { Id = 1, Name = "Confirmed",   Email = "confirmed@example.com",   EmailConfirmed = true  },
            new() { Id = 2, Name = "Unconfirmed", Email = "unconfirmed@example.com", EmailConfirmed = false }
        };

        _repository.UpdateQuestPropertiesWithNotificationsAsync(
                Arg.Any<int>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<int>(),
                Arg.Any<int>(), Arg.Any<bool>(), Arg.Any<bool>(), Arg.Any<IList<DateTime>?>(),
                Arg.Any<CancellationToken>())
            .Returns(affectedPlayers);

        var quest = MakeQuest(1);
        _repository.GetQuestWithDetailsAsync(1, Arg.Any<CancellationToken>())
            .Returns(quest);

        // Act
        var result = await _sut.UpdateQuestPropertiesWithNotificationsAsync(
            1, "Title", "Desc", null, 5, 4, false, token: TestContext.Current.CancellationToken);

        // Assert: only the confirmed player's email is dispatched
        result.Success.Should().BeTrue();
        result.Data.Should().Be(1, "only the confirmed player should be counted");

        _dispatcher.Received(1).EnqueueDateChangedEmail(
            1,
            Arg.Is<string[]>(emails =>
                emails.Contains("confirmed@example.com") &&
                !emails.Contains("unconfirmed@example.com")),
            Arg.Any<string[]>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<DateTime>(),
            Arg.Any<DateTime>());
    }

    [Fact]
    public async Task UpdateQuestPropertiesWithNotificationsAsync_WhenAllPlayersUnconfirmed_DoesNotDispatch()
    {
        // Arrange: all affected players are unconfirmed
        var affectedPlayers = new List<User>
        {
            new() { Id = 1, Name = "Unconfirmed1", Email = "u1@example.com", EmailConfirmed = false },
            new() { Id = 2, Name = "Unconfirmed2", Email = "u2@example.com", EmailConfirmed = false }
        };

        _repository.UpdateQuestPropertiesWithNotificationsAsync(
                Arg.Any<int>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<int>(),
                Arg.Any<int>(), Arg.Any<bool>(), Arg.Any<bool>(), Arg.Any<IList<DateTime>?>(),
                Arg.Any<CancellationToken>())
            .Returns(affectedPlayers);

        var quest = MakeQuest(1);
        _repository.GetQuestWithDetailsAsync(1, Arg.Any<CancellationToken>())
            .Returns(quest);

        // Act
        var result = await _sut.UpdateQuestPropertiesWithNotificationsAsync(
            1, "Title", "Desc", null, 5, 4, false, token: TestContext.Current.CancellationToken);

        // Assert: no dispatch when all affected players are unconfirmed
        result.Success.Should().BeTrue();
        result.Data.Should().Be(0);

        _dispatcher.DidNotReceive().EnqueueDateChangedEmail(
            Arg.Any<int>(), Arg.Any<string[]>(), Arg.Any<string[]>(),
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<DateTime>(), Arg.Any<DateTime>());
    }
}
