using AutoMapper;
using QuestBoard.Domain.Enums;
using QuestBoard.Domain.Interfaces;
using QuestBoard.Service.ViewModels.ShopViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace QuestBoard.Service.Controllers.Shop;

[Authorize]
public class ShopController(IShopService shopService, IUserService userService, IMapper mapper) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(
        ItemType? type = null,
        IList<ItemRarity>? rarity = null,
        string? sort = null,
        string? search = null,
        int page = 1,
        CancellationToken token = default)
    {
        const int pageSize = 12;

        var (items, totalCount) = await shopService.GetPagedPublishedItemsAsync(
            type, rarity, sort, search, page, pageSize, token);

        var totalPages = Math.Max(1, (int)Math.Ceiling(totalCount / (double)pageSize));
        page = Math.Max(1, Math.Min(page, totalPages));

        var viewModel = new ShopIndexViewModel
        {
            Items            = mapper.Map<IList<ShopItemViewModel>>(items),
            SelectedType     = type,
            SelectedRarities = rarity ?? [],
            SelectedSort     = sort,
            SearchQuery      = search,
            CurrentPage      = page,
            TotalPages       = totalPages,
            TotalItems       = totalCount
        };

        if (User.Identity?.IsAuthenticated == true)
        {
            var currentUser = await userService.GetUserAsync(User);
            if (currentUser != null)
            {
                var enriched = await shopService.GetUserTransactionsWithRemainingAsync(currentUser.Id, token);
                viewModel.UserPurchases = enriched
                    .OrderByDescending(e => e.Transaction.TransactionDate)
                    .Select(e =>
                    {
                        var vm = mapper.Map<UserTransactionViewModel>(e.Transaction);
                        vm.RemainingQuantity = e.RemainingQuantity;
                        return vm;
                    })
                    .ToList();
            }
        }

        return View(viewModel);
    }

    [HttpGet]
    public async Task<IActionResult> Details(int id, bool isModal = false, CancellationToken token = default)
    {
        var item = await shopService.GetItemWithDetailsAsync(id, token);
        if (item == null)
        {
            return NotFound();
        }

        var viewModel = mapper.Map<ShopItemDetailsViewModel>(item);

        if (isModal)
        {
            ViewBag.IsModal = true;
            return PartialView(viewModel);
        }

        return View(viewModel);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Purchase(int id, int quantity = 1, CancellationToken token = default)
    {
        try
        {
            var currentUser = await userService.GetUserAsync(User);
            if (currentUser == null)
            {
                return Challenge();
            }

            var transaction = await shopService.PurchaseItemAsync(id, quantity, currentUser, token);

            TempData["Success"] = $"Successfully purchased {quantity}x {transaction.ShopItem?.Name ?? "item"} for {transaction.Price} gp!";

            return RedirectToAction(nameof(Index));
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
            return RedirectToAction(nameof(Details), new { id });
        }
        catch (Exception)
        {
            TempData["Error"] = "An error occurred while processing your purchase. Please try again.";
            return RedirectToAction(nameof(Details), new { id });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Sell(int id, int quantity = 1, CancellationToken token = default)
    {
        try
        {
            var currentUser = await userService.GetUserAsync(User);
            if (currentUser == null)
            {
                return Challenge();
            }

            var transaction = await shopService.ReturnOrSellItemAsync(id, quantity, currentUser, token);

            // Calculate if it was a return or sell based on the refund amount
            var originalTransaction = await shopService.GetUserTransactionsAsync(currentUser.Id, token);
            var original = originalTransaction.FirstOrDefault(t => t.Id == id);

            if (original != null)
            {
                var originalUnitPrice = original.Price / original.Quantity;
                var expectedReturnPrice = originalUnitPrice * quantity;
                var isReturn = Math.Abs(transaction.Price - expectedReturnPrice) < 0.01m;

                var actionType = isReturn ? "returned" : "sold";
                TempData["Success"] = $"Successfully {actionType} {quantity}x {original.ShopItem?.Name ?? "item"} for {transaction.Price} gp!";
            }
            else
            {
                TempData["Success"] = $"Item processed successfully for {transaction.Price} gp!";
            }

            return RedirectToAction(nameof(Index));
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
            return RedirectToAction(nameof(Index));
        }
        catch (Exception)
        {
            TempData["Error"] = "An error occurred while processing your request. Please try again.";
            return RedirectToAction(nameof(Index));
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SellToShop(int id, int quantity = 1, CancellationToken token = default)
    {
        try
        {
            var currentUser = await userService.GetUserAsync(User);
            if (currentUser == null)
            {
                return Challenge();
            }

            var transaction = await shopService.SellItemToShopAsync(id, quantity, currentUser, token);
            var item = await shopService.GetItemWithDetailsAsync(id, token);

            TempData["GoldReceived"] = transaction.Price.ToString("N0");
            TempData["Success"] = $"You sold {quantity}x {item?.Name ?? "item"} to the shop";

            return RedirectToAction(nameof(Details), new { id });
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
            return RedirectToAction(nameof(Details), new { id });
        }
        catch (Exception)
        {
            TempData["Error"] = "An error occurred while processing your request. Please try again.";
            return RedirectToAction(nameof(Details), new { id });
        }
    }
}