using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuestBoard.Domain.Interfaces;
using QuestBoard.Domain.Models;
using QuestBoard.Service.ViewModels.ContactViewModels;

namespace QuestBoard.Service.Controllers.Contacts;

[Authorize(Policy = "DungeonMasterOnly")]
public class ContactCategoryManagementController(
    IContactCategoryService contactCategoryService,
    IActiveGroupContext activeGroupContext,
    IUserService userService,
    IMapper mapper) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken token = default)
    {
        var currentUser = await userService.GetUserAsync(User);
        if (currentUser.Id == 0)
        {
            return Challenge();
        }

        var viewModel = await BuildManagementViewModelAsync(token);
        return View("Manage", viewModel);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Add(ContactCategoryManagementViewModel viewModel, CancellationToken token = default)
    {
        var currentUser = await userService.GetUserAsync(User);
        if (currentUser.Id == 0)
        {
            return Challenge();
        }

        // A SuperAdmin has no active group by design, so there is no board to stamp onto
        // the new category. Send them to pick one rather than letting the write throw.
        if (activeGroupContext.ActiveGroupId is not { } activeGroupId)
        {
            return RedirectToAction("Index", "GroupPicker");
        }

        if (!ModelState.IsValid)
        {
            var repopulated = await BuildManagementViewModelAsync(token);
            repopulated.NewCategory = viewModel.NewCategory;
            return View("Manage", repopulated);
        }

        var category = mapper.Map<ContactCategory>(viewModel.NewCategory);
        category.GroupId = activeGroupId;

        try
        {
            await contactCategoryService.AddToEndAsync(category, token);
        }
        catch (DbUpdateException ex) when (
            ex.InnerException?.Message.Contains("unique", StringComparison.OrdinalIgnoreCase) == true ||
            ex.InnerException?.Message.Contains("duplicate", StringComparison.OrdinalIgnoreCase) == true)
        {
            ModelState.AddModelError("NewCategory.Name", "A category with that name already exists. Please choose a different name.");
            var repopulated = await BuildManagementViewModelAsync(token);
            repopulated.NewCategory = viewModel.NewCategory;
            return View("Manage", repopulated);
        }

        TempData["Success"] = "Category added.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id, CancellationToken token = default)
    {
        var category = await contactCategoryService.GetByIdAsync(id, token);
        if (category == null)
        {
            return NotFound();
        }

        var viewModel = mapper.Map<ContactCategoryViewModel>(category);
        return View(viewModel);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, ContactCategoryViewModel viewModel, CancellationToken token = default)
    {
        if (id != viewModel.Id)
        {
            return BadRequest();
        }

        var existingCategory = await contactCategoryService.GetByIdAsync(id, token);
        if (existingCategory == null)
        {
            return NotFound();
        }

        if (!ModelState.IsValid)
        {
            return View(viewModel);
        }

        // Only the name is writable here -- the sort position changes exclusively through the
        // reorder buttons, and the board id is never client-supplied.
        existingCategory.Name = viewModel.Name;

        try
        {
            await contactCategoryService.UpdateAsync(existingCategory, token);
        }
        catch (DbUpdateException ex) when (
            ex.InnerException?.Message.Contains("unique", StringComparison.OrdinalIgnoreCase) == true ||
            ex.InnerException?.Message.Contains("duplicate", StringComparison.OrdinalIgnoreCase) == true)
        {
            ModelState.AddModelError(nameof(viewModel.Name), "A category with that name already exists. Please choose a different name.");
            return View(viewModel);
        }

        TempData["Success"] = "Category renamed.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id, CancellationToken token = default)
    {
        // The configured delete behaviour orphans any dependents; no further write is needed
        // or performed here.
        await contactCategoryService.DeleteAsync(id, token);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MoveUp(int id, CancellationToken token = default)
    {
        // Redirect the same way whether or not a swap actually happened, so a no-op at a
        // boundary is indistinguishable from a successful move to the browser.
        await contactCategoryService.MoveUpAsync(id, token);
        return RedirectToAction(nameof(Index), fragment: $"category-{id}-row");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MoveDown(int id, CancellationToken token = default)
    {
        await contactCategoryService.MoveDownAsync(id, token);
        return RedirectToAction(nameof(Index), fragment: $"category-{id}-row");
    }

    // Shared by Index and the failed-Add path so a rejected submission re-renders the same
    // populated list without duplicating the ordering/count projection.
    private async Task<ContactCategoryManagementViewModel> BuildManagementViewModelAsync(CancellationToken token)
    {
        var ordered = await contactCategoryService.GetOrderedAsync(token);
        var counts = await contactCategoryService.GetContactCountsAsync(token);

        var categoryViewModels = mapper.Map<List<ContactCategoryViewModel>>(ordered);
        for (var i = 0; i < categoryViewModels.Count; i++)
        {
            var category = categoryViewModels[i];
            category.ContactCount = counts.TryGetValue(category.Id, out var count) ? count : 0;
            category.IsFirst = i == 0;
            category.IsLast = i == categoryViewModels.Count - 1;
        }

        return new ContactCategoryManagementViewModel
        {
            Categories = categoryViewModels,
            NewCategory = new ContactCategoryViewModel()
        };
    }
}
