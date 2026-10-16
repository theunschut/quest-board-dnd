using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QuestBoard.Domain.Enums;
using QuestBoard.Domain.Interfaces;
using QuestBoard.Domain.Models;
using QuestBoard.Service.ViewModels.PlatformViewModels;

namespace QuestBoard.Service.Areas.Platform.Controllers;

[Area("Platform")]
[Authorize(Policy = "SuperAdminOnly")]
public class LinkedBoardsController(ILinkedBoardSetService linkedBoardSetService) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var sets = await linkedBoardSetService.GetAllAsync();
        var unlinkedBoards = await linkedBoardSetService.GetUnlinkedBoardsAsync();
        return View(new LinkedBoardsIndexViewModel
        {
            Sets = sets.ToList(),
            UnlinkedBoards = unlinkedBoards.ToList(),
            Create = new LinkedBoardSetCreateViewModel()
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(LinkedBoardSetCreateViewModel model)
    {
        if (!ModelState.IsValid)
        {
            TempData["Error"] = ModelState.ContainsKey(nameof(model.Name)) && ModelState[nameof(model.Name)]!.Errors.Count > 0
                ? InvalidNameMessage
                : InvalidBoardTypeMessage;
            return RedirectToAction(nameof(Index));
        }

        var outcome = await linkedBoardSetService.CreateAsync(model.Name, model.BoardType!.Value);
        SetToast(outcome, $"Created the linked set \"{outcome.SetName}\".");
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddBoard(int id, int groupId)
    {
        var outcome = await linkedBoardSetService.AddBoardAsync(id, groupId);
        SetToast(outcome, $"Added \"{outcome.BoardName}\" to \"{outcome.SetName}\".");
        return RedirectToAction(nameof(Index));
    }

    private const string InvalidNameMessage = "Enter a set name of up to 100 characters.";
    private const string InvalidBoardTypeMessage = "Choose the board type for the set.";

    private void SetToast(LinkedBoardSetOutcome outcome, string successMessage)
    {
        if (outcome.Result == LinkedBoardSetResult.Success)
        {
            TempData["Success"] = successMessage;
            return;
        }

        TempData["Error"] = ErrorMessage(outcome);
    }

    private static string ErrorMessage(LinkedBoardSetOutcome outcome) => outcome.Result switch
    {
        LinkedBoardSetResult.InvalidName => InvalidNameMessage,
        LinkedBoardSetResult.InvalidBoardType => InvalidBoardTypeMessage,
        LinkedBoardSetResult.DuplicateName =>
            $"A linked set named \"{outcome.SetName}\" already exists. Choose a different name.",
        LinkedBoardSetResult.TypeMismatch =>
            $"\"{outcome.BoardName}\" is a {TypeLabel(outcome.BoardType)} board, but this set links {TypeLabel(outcome.SetBoardType)} boards. Only boards of the same type can be linked.",
        LinkedBoardSetResult.AlreadyLinked =>
            $"\"{outcome.BoardName}\" is already in a linked set. A board can be in only one set.",
        _ => "That set or board no longer exists."
    };

    private static string TypeLabel(BoardType? boardType) =>
        boardType == BoardType.Campaign ? "Campaign" : "One-Shot";
}
