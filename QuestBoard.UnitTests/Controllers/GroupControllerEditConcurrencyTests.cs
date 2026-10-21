using Hangfire;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using QuestBoard.Domain.Enums;
using QuestBoard.Domain.Interfaces;
using QuestBoard.Domain.Models;
using QuestBoard.Service.Areas.Platform.Controllers;
using QuestBoard.Service.ViewModels.PlatformViewModels;

namespace QuestBoard.UnitTests.Controllers;

public class GroupControllerEditConcurrencyTests
{
    private const string ConcurrencyMessage =
        "Someone else changed this board while you were editing. Reload the page and try again.";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Edit_WhenTheBoardIsChangedConcurrently_ReturnsTheFormWithAnErrorInsteadOfFailing()
    {
        var group = new Group { Id = 7, Name = "Old Name", BoardType = BoardType.Campaign };
        var groupService = Substitute.For<IGroupService>();
        groupService.GetByIdAsync(7, Arg.Any<CancellationToken>()).Returns(group);
        groupService.UpdateAsync(Arg.Any<Group>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new DbUpdateConcurrencyException("changed"));

        var settingsService = Substitute.For<IBoardPageSettingsService>();
        settingsService.EvaluateAsync(Arg.Any<Group>(), Arg.Any<BoardPageSettingsRequest>(), Arg.Any<CancellationToken>())
            .Returns(new BoardPageSettingsOutcome(BoardPageSet.Empty, BoardPage.Quests, []));

        var controller = new GroupController(
            groupService,
            Substitute.For<IUserService>(),
            Substitute.For<IIdentityService>(),
            Substitute.For<IBackgroundJobClient>(),
            settingsService,
            NullLogger<GroupController>.Instance);

        var result = await controller.Edit(new GroupEditViewModel { Id = 7, Name = "New Name" });

        var view = result.Should().BeOfType<ViewResult>().Subject;
        controller.ModelState[string.Empty]!.Errors.Select(e => e.ErrorMessage).Should().Contain(ConcurrencyMessage);
        view.Model.Should().BeOfType<GroupEditViewModel>().Which.BoardType.Should().Be(BoardType.Campaign);
    }
}
