using Hangfire;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using QuestBoard.Domain.Enums;
using QuestBoard.Domain.Interfaces;
using QuestBoard.Domain.Models;
using QuestBoard.Service.Areas.Platform.Controllers;
using QuestBoard.Service.ViewModels.PlatformViewModels;

namespace QuestBoard.UnitTests.Controllers;

// A board name is saved without edge whitespace, so the name a delete asks for is the name the board
// list shows and a stray space from a paste never makes a board hard to confirm.
public class GroupControllerBoardNameTests
{
    private static GroupController CreateController(IGroupService groupService, IBoardPageSettingsService? settingsService = null)
    {
        var controller = new GroupController(
            groupService,
            Substitute.For<IUserService>(),
            Substitute.For<IIdentityService>(),
            Substitute.For<IBackgroundJobClient>(),
            settingsService ?? Substitute.For<IBoardPageSettingsService>(),
            NullLogger<GroupController>.Instance);
        controller.TempData = new TempDataDictionary(new DefaultHttpContext(), Substitute.For<ITempDataProvider>());
        return controller;
    }

    [Theory]
    [InlineData("Goblin Night ")]
    [InlineData("  Goblin Night")]
    [InlineData("\uFEFFGoblin Night\u0085")]
    public async Task Create_SavesTheNameWithoutEdgeWhitespace(string posted)
    {
        var groupService = Substitute.For<IGroupService>();
        var controller = CreateController(groupService);

        var result = await controller.Create(new GroupCreateViewModel { Name = posted, BoardType = BoardType.Campaign });

        result.Should().BeOfType<RedirectToActionResult>();
        await groupService.Received(1).AddAsync(
            Arg.Is<Group>(g => g.Name == "Goblin Night"), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Create_WithANameMadeOnlyOfEdgeWhitespace_IsRefusedWithoutSaving()
    {
        var groupService = Substitute.For<IGroupService>();
        var controller = CreateController(groupService);

        var result = await controller.Create(new GroupCreateViewModel { Name = "\uFEFF", BoardType = BoardType.Campaign });

        result.Should().BeOfType<ViewResult>();
        controller.ModelState[nameof(GroupCreateViewModel.Name)]!.Errors.Select(e => e.ErrorMessage)
            .Should().ContainSingle().Which.Should().Be("Group name is required.");
        await groupService.DidNotReceive().AddAsync(Arg.Any<Group>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Edit_SavesTheNameWithoutEdgeWhitespace()
    {
        var group = new Group { Id = 7, Name = "Old Name", BoardType = BoardType.Campaign, HomePage = BoardPage.Quests };
        var groupService = Substitute.For<IGroupService>();
        groupService.GetByIdAsync(7, Arg.Any<CancellationToken>()).Returns(group);
        var settingsService = Substitute.For<IBoardPageSettingsService>();
        settingsService.EvaluateAsync(Arg.Any<Group>(), Arg.Any<BoardPageSettingsRequest>(), Arg.Any<CancellationToken>())
            .Returns(new BoardPageSettingsOutcome(group.DisabledPages, BoardPage.Quests, []));
        var controller = CreateController(groupService, settingsService);

        var result = await controller.Edit(new GroupEditViewModel { Id = 7, Name = " Goblin Night\t" });

        result.Should().BeOfType<RedirectToActionResult>();
        group.Name.Should().Be("Goblin Night");
        await groupService.Received(1).UpdateAsync(group, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Edit_WithANameMadeOnlyOfEdgeWhitespace_IsRefusedWithoutSaving()
    {
        var group = new Group { Id = 7, Name = "Old Name", BoardType = BoardType.Campaign, HomePage = BoardPage.Quests };
        var groupService = Substitute.For<IGroupService>();
        groupService.GetByIdAsync(7, Arg.Any<CancellationToken>()).Returns(group);
        var settingsService = Substitute.For<IBoardPageSettingsService>();
        settingsService.EvaluateAsync(Arg.Any<Group>(), Arg.Any<BoardPageSettingsRequest>(), Arg.Any<CancellationToken>())
            .Returns(new BoardPageSettingsOutcome(group.DisabledPages, BoardPage.Quests, []));
        var controller = CreateController(groupService, settingsService);

        var result = await controller.Edit(new GroupEditViewModel { Id = 7, Name = "\uFEFF" });

        result.Should().BeOfType<ViewResult>();
        group.Name.Should().Be("Old Name");
        await groupService.DidNotReceive().UpdateAsync(Arg.Any<Group>(), Arg.Any<CancellationToken>());
    }
}
