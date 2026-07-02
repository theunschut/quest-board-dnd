using Microsoft.AspNetCore.Mvc;

namespace QuestBoard.Service.Controllers.QuestBoard;

public class HomeController : Controller
{
    [HttpGet]
    public IActionResult Index() =>
        User.Identity?.IsAuthenticated == true
            ? RedirectToAction("Index", "Quest")
            : View();
}
