using Hangfire;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
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

    private static GroupController CreateController(IGroupService groupService, IBoardPageSettingsService settingsService) => new(
        groupService,
        Substitute.For<IUserService>(),
        Substitute.For<IIdentityService>(),
        Substitute.For<IBackgroundJobClient>(),
        settingsService,
        NullLogger<GroupController>.Instance);

    [Fact]
    public async Task Edit_WhenThePageSettingsChangedSinceTheFormWasRendered_RefusesWithoutSavingAndShowsTheStoredSettings()
    {
        var group = new Group
        {
            Id = 7,
            Name = "Old Name",
            BoardType = BoardType.Campaign,
            DisabledPages = BoardPageSet.Empty.With(BoardPage.Shop),
            HomePage = BoardPage.Quests
        };
        var groupService = Substitute.For<IGroupService>();
        groupService.GetByIdAsync(7, Arg.Any<CancellationToken>()).Returns(group);
        var settingsService = Substitute.For<IBoardPageSettingsService>();
        var controller = CreateController(groupService, settingsService);

        // The form was rendered while Shop was still on, then someone else switched it off.
        var result = await controller.Edit(new GroupEditViewModel
        {
            Id = 7,
            Name = "New Name",
            RenderedDisabledPages = BoardPageSet.Empty.Mask,
            RenderedHomePage = BoardPage.Quests,
            Pages = [new BoardPageRowViewModel { Page = BoardPage.Shop, Enabled = true }]
        });

        var model = result.Should().BeOfType<ViewResult>().Subject.Model.Should().BeOfType<GroupEditViewModel>().Subject;
        controller.ModelState[string.Empty]!.Errors.Select(e => e.ErrorMessage).Should().Contain(ConcurrencyMessage);
        model.RenderedDisabledPages.Should().Be(group.DisabledPages.Mask);
        model.Pages.Single(row => row.Page == BoardPage.Shop).Enabled.Should().BeFalse();
        await groupService.DidNotReceive().UpdateAsync(Arg.Any<Group>(), Arg.Any<CancellationToken>());
        await settingsService.DidNotReceive().EvaluateAsync(Arg.Any<Group>(), Arg.Any<BoardPageSettingsRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Edit_WhenTheHomePageChangedSinceTheFormWasRendered_RefusesWithoutSaving()
    {
        var group = new Group { Id = 7, Name = "Old Name", BoardType = BoardType.Campaign, HomePage = BoardPage.Calendar };
        var groupService = Substitute.For<IGroupService>();
        groupService.GetByIdAsync(7, Arg.Any<CancellationToken>()).Returns(group);
        var controller = CreateController(groupService, Substitute.For<IBoardPageSettingsService>());

        var result = await controller.Edit(new GroupEditViewModel
        {
            Id = 7,
            Name = "New Name",
            RenderedDisabledPages = 0,
            RenderedHomePage = BoardPage.Quests,
            HomePage = BoardPage.Quests
        });

        var model = result.Should().BeOfType<ViewResult>().Subject.Model.Should().BeOfType<GroupEditViewModel>().Subject;
        controller.ModelState[string.Empty]!.Errors.Select(e => e.ErrorMessage).Should().Contain(ConcurrencyMessage);
        model.HomePage.Should().Be(BoardPage.Calendar);
        await groupService.DidNotReceive().UpdateAsync(Arg.Any<Group>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Edit_WhenTheRenderedSettingsStillMatchTheStoredBoard_Saves()
    {
        var group = new Group
        {
            Id = 7,
            Name = "Old Name",
            BoardType = BoardType.Campaign,
            DisabledPages = BoardPageSet.Empty.With(BoardPage.Shop),
            HomePage = BoardPage.Quests
        };
        var groupService = Substitute.For<IGroupService>();
        groupService.GetByIdAsync(7, Arg.Any<CancellationToken>()).Returns(group);
        var settingsService = Substitute.For<IBoardPageSettingsService>();
        settingsService.EvaluateAsync(Arg.Any<Group>(), Arg.Any<BoardPageSettingsRequest>(), Arg.Any<CancellationToken>())
            .Returns(new BoardPageSettingsOutcome(group.DisabledPages, BoardPage.Quests, []));
        var controller = CreateController(groupService, settingsService);
        controller.TempData = new TempDataDictionary(new DefaultHttpContext(), Substitute.For<ITempDataProvider>());

        var result = await controller.Edit(new GroupEditViewModel
        {
            Id = 7,
            Name = "New Name",
            RenderedDisabledPages = group.DisabledPages.Mask,
            RenderedHomePage = BoardPage.Quests
        });

        result.Should().BeOfType<RedirectToActionResult>();
        await groupService.Received(1).UpdateAsync(group, Arg.Any<CancellationToken>());
    }
}
