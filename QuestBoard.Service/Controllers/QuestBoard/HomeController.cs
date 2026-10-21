using Microsoft.AspNetCore.Mvc;
using QuestBoard.Domain.Interfaces;
using QuestBoard.Service.Helpers;

namespace QuestBoard.Service.Controllers.QuestBoard;

public class HomeController(IBoardPageResolver resolver) : Controller
{
    /// <summary>
    /// The front door. A signed-in visitor goes to the active board's home page, which is the quest
    /// list on a board that has chosen nothing; a signed-out visitor sees the landing page.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken token)
    {
        if (User.Identity?.IsAuthenticated != true)
        {
            return View();
        }

        var (controller, action) = BoardPageRoutes.HomeRoute(await resolver.GetAsync(token));
        return RedirectToAction(action, controller);
    }
}
