using System.Security.Claims;
using Hangfire;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using QuestBoard.Domain.Enums;
using QuestBoard.Domain.Interfaces;
using QuestBoard.Domain.Models;
using QuestBoard.Domain.Models.BoardDeletion;
using QuestBoard.Service.Areas.Platform.Controllers;
using QuestBoard.Service.Constants;
using QuestBoard.Service.ViewModels.PlatformViewModels;

namespace QuestBoard.UnitTests.Controllers;

public class GroupControllerBoardDeletionTests
{
    private const int BoardId = 7;
    private const string BoardName = "Dragon Heist";

    private static (GroupController Controller, FakeSession Session, ListLogger Log) CreateController(
        IGroupService groupService, bool failSessionLoad = false)
    {
        var log = new ListLogger();
        var controller = new GroupController(
            groupService,
            Substitute.For<IUserService>(),
            Substitute.For<IIdentityService>(),
            Substitute.For<IBackgroundJobClient>(),
            Substitute.For<IBoardPageSettingsService>(),
            log);

        var session = new FakeSession { FailLoad = failSessionLoad };
        var httpContext = new DefaultHttpContext
        {
            Session = session,
            User = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(ClaimTypes.NameIdentifier, "42")], "test"))
        };
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
        controller.TempData = new TempDataDictionary(httpContext, Substitute.For<ITempDataProvider>());
        return (controller, session, log);
    }

    private static Group Board() => new() { Id = BoardId, Name = BoardName, BoardType = BoardType.Campaign };

    private static BoardDeletionPreview Preview() => new()
    {
        Items = [new BoardDeletionItem { Kind = BoardContentKind.Quests, Count = 3, Names = ["A", "B", "C"] }]
    };

    private static IGroupService ServiceReturning(BoardDeletionResult result)
    {
        var service = Substitute.For<IGroupService>();
        service.DeleteBoardAsync(BoardId, Arg.Any<string?>(), Arg.Any<CancellationToken>()).Returns(result);
        return service;
    }

    private static void ActiveBoardIs(FakeSession session, int id)
    {
        session.SetInt32(SessionKeys.ActiveGroupId, id);
        session.SetString(SessionKeys.ActiveGroupName, "name");
        session.SetString(SessionKeys.ActiveGroupValidatedAtUtc, DateTime.UtcNow.ToString("O"));
    }

    private static void AssertRedirectsTo(IActionResult result, string action)
        => result.Should().BeOfType<RedirectToActionResult>().Which.ActionName.Should().Be(action);

    [Fact]
    public async Task Post_WhenTheDeleteThrows_ShowsTheSaveFailureToastAndReturnsToTheDeletePage()
    {
        var service = Substitute.For<IGroupService>();
        service.DeleteBoardAsync(BoardId, Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("boom"));
        var (controller, _, _) = CreateController(service);

        var result = await controller.DeleteConfirmed(BoardId, BoardName);

        controller.TempData["Error"].Should().Be("The board could not be deleted and nothing was changed. Try again.");
        var redirect = result.Should().BeOfType<RedirectToActionResult>().Subject;
        redirect.ActionName.Should().Be("Delete");
        redirect.RouteValues!["id"].Should().Be(BoardId);
    }

    [Fact]
    public async Task Post_WithAWrongName_ShowsTheDeletePageAgainWithTheRefusalAndFreshCounts()
    {
        var service = ServiceReturning(BoardDeletionResult.Refused(BoardDeleteStatus.NameMismatch, BoardName));
        service.GetByIdAsync(BoardId, Arg.Any<CancellationToken>()).Returns(Board());
        service.GetDeletionPreviewAsync(BoardId, Arg.Any<CancellationToken>()).Returns(Preview());
        var (controller, _, _) = CreateController(service);

        var result = await controller.DeleteConfirmed(BoardId, "dragon heist");

        var view = result.Should().BeOfType<ViewResult>().Subject;
        view.ViewName.Should().Be("Delete");
        var model = view.Model.Should().BeOfType<BoardDeletePageViewModel>().Subject;
        model.NameMismatch.Should().BeTrue();
        model.Name.Should().Be(BoardName);
        model.DeletedRows.Should().ContainSingle().Which.Text.Should().Be("3 quests");
        await service.Received(1).GetDeletionPreviewAsync(BoardId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Post_WithAWrongNameForABoardThatVanishedMeanwhile_ShowsTheGoneToastAndReturnsToTheList()
    {
        var service = ServiceReturning(BoardDeletionResult.Refused(BoardDeleteStatus.NameMismatch, BoardName));
        service.GetByIdAsync(BoardId, Arg.Any<CancellationToken>()).Returns((Group?)null);
        var (controller, _, _) = CreateController(service);

        var result = await controller.DeleteConfirmed(BoardId, "wrong");

        controller.TempData["Info"].Should().Be("That board no longer exists, so there was nothing to delete.");
        AssertRedirectsTo(result, "Index");
    }

    [Fact]
    public async Task Post_WhenMembersWereAddedSinceThePreview_ShowsTheMembersToastAndReturnsToTheList()
    {
        var service = ServiceReturning(BoardDeletionResult.Refused(BoardDeleteStatus.HasMembers, BoardName));
        var (controller, _, _) = CreateController(service);

        var result = await controller.DeleteConfirmed(BoardId, BoardName);

        controller.TempData["Error"].Should().Be("\"Dragon Heist\" still has members, so it was not deleted. Remove them first.");
        AssertRedirectsTo(result, "Index");
    }

    [Fact]
    public async Task Post_WhenTheBoardIsAlreadyGone_ShowsTheGoneToastAndReturnsToTheList()
    {
        var service = ServiceReturning(BoardDeletionResult.Refused(BoardDeleteStatus.NotFound));
        var (controller, _, _) = CreateController(service);

        var result = await controller.DeleteConfirmed(BoardId, BoardName);

        controller.TempData["Info"].Should().Be("That board no longer exists, so there was nothing to delete.");
        AssertRedirectsTo(result, "Index");
    }

    private static BoardDeletionResult Deleted() => new()
    {
        Status = BoardDeleteStatus.Deleted,
        BoardName = BoardName,
        RemovedCounts = new Dictionary<BoardContentKind, int>
        {
            [BoardContentKind.Quests] = 3,
            [BoardContentKind.Events] = 0,
            [BoardContentKind.Characters] = 2
        }
    };

    [Fact]
    public async Task Post_WithTheExactName_ShowsTheSuccessToastAndReturnsToTheList()
    {
        var (controller, _, _) = CreateController(ServiceReturning(Deleted()));

        var result = await controller.DeleteConfirmed(BoardId, BoardName);

        controller.TempData["Success"].Should().Be("Board \"Dragon Heist\" was deleted.");
        AssertRedirectsTo(result, "Index");
    }

    [Fact]
    public async Task Post_WhenTheAdminsSessionNamesTheDeletedBoard_ClearsTheSessionsActiveBoard()
    {
        var (controller, session, _) = CreateController(ServiceReturning(Deleted()));
        ActiveBoardIs(session, BoardId);

        await controller.DeleteConfirmed(BoardId, BoardName);

        session.TryGetValue(SessionKeys.ActiveGroupId, out _).Should().BeFalse();
        session.TryGetValue(SessionKeys.ActiveGroupName, out _).Should().BeFalse();
        session.TryGetValue(SessionKeys.ActiveGroupValidatedAtUtc, out _).Should().BeFalse();
    }

    [Fact]
    public async Task Post_WhenTheAdminsSessionNamesAnotherBoard_LeavesTheSessionAlone()
    {
        var (controller, session, _) = CreateController(ServiceReturning(Deleted()));
        ActiveBoardIs(session, 99);

        await controller.DeleteConfirmed(BoardId, BoardName);

        session.GetInt32(SessionKeys.ActiveGroupId).Should().Be(99);
        session.TryGetValue(SessionKeys.ActiveGroupName, out _).Should().BeTrue();
        session.TryGetValue(SessionKeys.ActiveGroupValidatedAtUtc, out _).Should().BeTrue();
    }

    [Fact]
    public async Task Post_WhenTheSessionCannotBeCleared_StillReportsTheCommittedDeleteAsASuccess()
    {
        var (controller, _, log) = CreateController(ServiceReturning(Deleted()), failSessionLoad: true);

        var result = await controller.DeleteConfirmed(BoardId, BoardName);

        controller.TempData["Success"].Should().Be("Board \"Dragon Heist\" was deleted.");
        AssertRedirectsTo(result, "Index");
        log.Entries.Should().Contain(e => e.Level == LogLevel.Warning);
    }

    [Fact]
    public async Task Post_WithTheExactName_WritesOneAuditLineWithTheActorTheBoardAndTheNonZeroCounts()
    {
        var (controller, _, log) = CreateController(ServiceReturning(Deleted()));

        await controller.DeleteConfirmed(BoardId, BoardName);

        var line = log.Entries.Should().ContainSingle(e => e.Level == LogLevel.Information).Subject.Message;
        line.Should().Contain("7").And.Contain("Dragon Heist").And.Contain("42");
        line.Should().Contain("Quests=3").And.Contain("Characters=2");
        line.Should().NotContain("Events=");
    }

    [Fact]
    public async Task Get_ForABoardWithMembers_ShowsTheMembersToastAndReturnsToTheList()
    {
        var service = Substitute.For<IGroupService>();
        service.GetByIdAsync(BoardId, Arg.Any<CancellationToken>()).Returns(Board());
        service.HasMembersAsync(BoardId, Arg.Any<CancellationToken>()).Returns(true);
        var (controller, _, _) = CreateController(service);

        var result = await controller.Delete(BoardId);

        controller.TempData["Error"].Should().Be("\"Dragon Heist\" still has members, so it was not deleted. Remove them first.");
        AssertRedirectsTo(result, "Index");
    }

    [Fact]
    public async Task Get_ForAMissingBoard_ShowsTheGoneToastAndReturnsToTheList()
    {
        var service = Substitute.For<IGroupService>();
        service.GetByIdAsync(BoardId, Arg.Any<CancellationToken>()).Returns((Group?)null);
        var (controller, _, _) = CreateController(service);

        var result = await controller.Delete(BoardId);

        controller.TempData["Info"].Should().Be("That board no longer exists, so there was nothing to delete.");
        AssertRedirectsTo(result, "Index");
    }

    [Fact]
    public async Task Get_ForAnEmptyBoard_ShowsThePageWithoutTheRefusal()
    {
        var service = Substitute.For<IGroupService>();
        service.GetByIdAsync(BoardId, Arg.Any<CancellationToken>()).Returns(Board());
        service.HasMembersAsync(BoardId, Arg.Any<CancellationToken>()).Returns(false);
        service.GetDeletionPreviewAsync(BoardId, Arg.Any<CancellationToken>()).Returns(Preview());
        var (controller, _, _) = CreateController(service);

        var result = await controller.Delete(BoardId);

        var view = result.Should().BeOfType<ViewResult>().Subject;
        view.Model.Should().BeOfType<BoardDeletePageViewModel>().Which.NameMismatch.Should().BeFalse();
    }

    /// <summary>A logger that keeps what it was asked to write so the tests can read it back.</summary>
    private sealed class ListLogger : ILogger<GroupController>
    {
        public List<(LogLevel Level, string Message)> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
            => Entries.Add((logLevel, formatter(state, exception)));

        private sealed class NullScope : IDisposable
        {
            public static readonly NullScope Instance = new();

            public void Dispose()
            {
            }
        }
    }

    /// <summary>Dictionary-backed session, so the keys can be read back without a cache or cookie round-trip.</summary>
    private sealed class FakeSession : ISession
    {
        private readonly Dictionary<string, byte[]> _store = [];

        public bool FailLoad { get; init; }

        public bool IsAvailable => true;
        public string Id => "test-session";
        public IEnumerable<string> Keys => _store.Keys;

        public void Clear() => _store.Clear();

        public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task LoadAsync(CancellationToken cancellationToken = default)
            => FailLoad ? throw new InvalidOperationException("session store unavailable") : Task.CompletedTask;

        public void Remove(string key) => _store.Remove(key);

        public void Set(string key, byte[] value) => _store[key] = value;

        public bool TryGetValue(string key, out byte[] value) => _store.TryGetValue(key, out value!);
    }
}
