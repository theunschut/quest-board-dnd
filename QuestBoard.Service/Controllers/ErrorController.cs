using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QuestBoard.Service.Helpers;
using QuestBoard.Service.ViewModels.ErrorViewModels;

namespace QuestBoard.Service.Controllers;

/// <summary>
/// The one endpoint behind every themed error page: the status-code pages middleware re-executes
/// /Error/{code} for a bodiless 4xx or 5xx, and the exception handler re-executes /Error for an
/// unhandled exception. It answers every verb (a re-executed POST or HEAD keeps its method),
/// anonymously, with no antiforgery check, and never needs a board or the database.
/// </summary>
public class ErrorController : Controller
{
    [Route("Error/{code:int?}")]
    [AllowAnonymous]
    [IgnoreAntiforgeryToken]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public IActionResult Show(int? code)
    {
        var status = ChooseStatus(Response.StatusCode, code);
        Response.StatusCode = status;

        // A script, image or calendar client keeps the bare status it always received.
        if (!BrowserNavigation.IsDocumentNavigation(Request))
            return new StatusCodeResult(status);

        var model = ErrorPageViewModel.For(status, User.Identity?.IsAuthenticated == true);
        return View("ErrorPage", model);
    }

    /// <summary>
    /// The status of the response being replaced when it is an error, otherwise the status the route
    /// names when that is an error, otherwise 500 for a visit with no code and 404 for a code that
    /// names no error.
    /// </summary>
    private static int ChooseStatus(int responseStatus, int? routeCode)
    {
        if (IsError(responseStatus))
            return responseStatus;

        if (routeCode is { } named)
            return IsError(named) ? named : 404;

        return 500;
    }

    private static bool IsError(int status) => status is >= 400 and <= 599;
}
