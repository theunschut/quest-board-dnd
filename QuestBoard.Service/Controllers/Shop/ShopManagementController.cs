using AutoMapper;
using QuestBoard.Domain.Enums;
using QuestBoard.Domain.Interfaces;
using QuestBoard.Domain.Models.Shop;
using QuestBoard.Service.ViewModels.ShopViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace QuestBoard.Service.Controllers.Shop;

[Authorize(Policy = "DungeonMasterOnly")]
public class ShopManagementController(
    IAuthorizationService authorizationService,
    IShopService shopService,
    IUserService userService,
    IMapper mapper
    ) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken token = default)
    {
        var currentUser = await userService.GetUserAsync(User);
        if (currentUser == null)
        {
            return Challenge();
        }

        var allItems = await shopService.GetAllAsync(token);
        var myItems = allItems.Where(i => i.CreatedByDmId == currentUser.Id);
        var draftItems = allItems.Where(i => i.CreatedByDmId != currentUser.Id && i.Status == ItemStatus.Draft);
        var publishedItems = allItems.Where(i => i.CreatedByDmId != currentUser.Id && i.Status == ItemStatus.Published);
        var archivedItems = allItems.Where(i => i.CreatedByDmId != currentUser.Id && i.Status == ItemStatus.Archived);

        var viewModel = new ShopManagementIndexViewModel
        {
            MyItems = mapper.Map<IList<ShopItemViewModel>>(myItems),
            ItemsForReview = mapper.Map<IList<ShopItemViewModel>>(draftItems),
            AllOtherItems = mapper.Map<IList<ShopItemViewModel>>(publishedItems.Concat(archivedItems))
        };

        return View(viewModel);
    }

    [HttpGet]
    public Task<IActionResult> Create(CancellationToken token = default)
    {
        var viewModel = new CreateShopItemViewModel();
        return Task.FromResult<IActionResult>(View(viewModel));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateShopItemViewModel viewModel, CancellationToken token = default)
    {
        if (!ModelState.IsValid)
        {
            return View(viewModel);
        }

        var currentUser = await userService.GetUserAsync(User);
        if (currentUser == null)
        {
            return Challenge();
        }

        var shopItem = new ShopItem
        {
            Name = viewModel.Name,
            Description = viewModel.Description,
            Type = viewModel.Type!.Value,
            Rarity = viewModel.Rarity!.Value,
            Price = viewModel.Price,
            Quantity = viewModel.Quantity,
            ReferenceUrl = viewModel.ReferenceUrl,
            Status = ItemStatus.Draft,
            CreatedByDmId = currentUser.Id,
            AvailableFrom = viewModel.AvailableFrom,
            AvailableUntil = viewModel.AvailableUntil
        };

        if (shopItem.Status == ItemStatus.Draft && (shopItem.Rarity == ItemRarity.Common || shopItem.Rarity == ItemRarity.Uncommon))
        {
            // Auto-publish common and uncommon items
            shopItem.Status = ItemStatus.Published;
        }

        await shopService.AddAsync(shopItem, token);

        TempData["Success"] = "Item created successfully!";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id, CancellationToken token = default)
    {
        var item = await shopService.GetItemWithDetailsAsync(id, token);
        if (item == null)
        {
            return NotFound();
        }

        var currentUser = await userService.GetUserAsync(User);
        if (currentUser == null)
        {
            return Challenge();
        }

        var viewModel = mapper.Map<EditShopItemViewModel>(item);
        return View(viewModel);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, EditShopItemViewModel viewModel, CancellationToken token = default)
    {
        if (!ModelState.IsValid)
        {
            return View(viewModel);
        }

        var item = await shopService.GetByIdAsync(id, token);
        if (item == null)
        {
            return NotFound();
        }

        var currentUser = await userService.GetUserAsync(User);
        if (currentUser == null)
        {
            return Challenge();
        }

        item.Name = viewModel.Name;
        item.Description = viewModel.Description;
        item.Type = viewModel.Type;
        item.Rarity = viewModel.Rarity;
        item.Price = viewModel.Price;
        item.Quantity = viewModel.Quantity;
        item.ReferenceUrl = viewModel.ReferenceUrl;
        item.AvailableFrom = viewModel.AvailableFrom;
        item.AvailableUntil = viewModel.AvailableUntil;

        // If item was denied, reset it to draft when edited
        if (item.Status == ItemStatus.Denied)
        {
            item.Status = ItemStatus.Draft;
            item.DenialReason = null;
            item.DeniedAt = null;
        }

        if (item.Status == ItemStatus.Draft && (item.Rarity == ItemRarity.Common || item.Rarity == ItemRarity.Uncommon))
        {
            // Auto-publish common and uncommon items upon editing
            item.Status = ItemStatus.Published;
        }

        await shopService.UpdateAsync(item, token);

        TempData["Success"] = item.Status == ItemStatus.Draft 
            ? "Item updated and resubmitted for review!" 
            : "Item updated successfully!";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Publish(int id, CancellationToken token = default)
    {
        var item = await shopService.GetByIdAsync(id, token);
        if (item == null)
        {
            return NotFound();
        }

        var currentUser = await userService.GetUserAsync(User);
        if (currentUser == null || item.CreatedByDmId == currentUser.Id)
        {
            return Forbid();
        }

        if (item.Status != ItemStatus.Draft)
        {
            TempData["Error"] = "This item is already published or archived.";
            return RedirectToAction(nameof(Index));
        }

        await shopService.PublishItemAsync(id, token);

        TempData["Success"] = "Item published successfully!";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Archive(int id, CancellationToken token = default)
    {
        var item = await shopService.GetByIdAsync(id, token);
        if (item == null)
        {
            return NotFound();
        }

        await shopService.ArchiveItemAsync(id, token);

        TempData["Success"] = "Item archived successfully!";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Deny(int id, string denialReason, CancellationToken token = default)
    {
        if (string.IsNullOrWhiteSpace(denialReason))
        {
            TempData["Error"] = "Please provide a reason for denying this item.";
            return RedirectToAction(nameof(Index));
        }

        var item = await shopService.GetByIdAsync(id, token);
        if (item == null)
        {
            return NotFound();
        }

        var currentUser = await userService.GetUserAsync(User);
        if (currentUser == null || item.CreatedByDmId == currentUser.Id)
        {
            TempData["Error"] = "You cannot deny your own items.";
            return RedirectToAction(nameof(Index));
        }

        if (item.Status != ItemStatus.Draft)
        {
            TempData["Error"] = "Only draft items can be denied.";
            return RedirectToAction(nameof(Index));
        }

        await shopService.DenyItemAsync(id, denialReason, token);

        TempData["Success"] = $"Item '{item.Name}' has been denied.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reopen(int id, CancellationToken token = default)
    {
        var item = await shopService.GetByIdAsync(id, token);
        if (item == null)
        {
            return NotFound();
        }

        if (item.Status != ItemStatus.Archived)
        {
            TempData["Error"] = "Only archived items can be reopened.";
            return RedirectToAction(nameof(Index));
        }

        await shopService.PublishItemAsync(id, token);

        TempData["Success"] = "Item reopened successfully!";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id, CancellationToken token = default)
    {
        var item = await shopService.GetByIdAsync(id, token);
        if (item == null)
        {
            return NotFound();
        }

        // Only allow deletion if item is still in draft or denied status
        var isAdmin = (await authorizationService.AuthorizeAsync(User, "AdminOnly")).Succeeded;
        if (!isAdmin)
        {
            if (item.Status != ItemStatus.Draft && item.Status != ItemStatus.Denied)
            {
                TempData["Error"] = "Cannot delete items that have been published.";
                return RedirectToAction(nameof(Index));
            }
        }

        await shopService.RemoveAsync(item, token);

        TempData["Success"] = "Item deleted successfully!";
        return RedirectToAction(nameof(Index));
    }
}