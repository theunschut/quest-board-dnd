using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using QuestBoard.Domain.Interfaces;
using QuestBoard.Service.Helpers;
using QuestBoard.Service.ViewModels.ErrorViewModels;

namespace QuestBoard.Service.Controllers;

/// <summary>
/// The one endpoint behind every themed error page: the status-code pages middleware re-executes
/// /Error/{code} for a bodiless 4xx or 5xx, and the exception handler re-executes /Error for an
/// unhandled exception. It answers every verb (a re-executed POST or HEAD keeps its method),
/// anonymously, with no antiforgery check, and never needs a board or the database.
/// </summary>
public class ErrorController(ILogger<ErrorController> logger) : Controller
{
    [Route("Error/{code:int?}")]
    [AllowAnonymous]
    [IgnoreAntiforgeryToken]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> Show(int? code)
    {
        var status = ChooseStatus(Response.StatusCode, code);
        Response.StatusCode = status;

        // A script, image or calendar client keeps the bare status it always received, and a HEAD
        // asks for the headers only.
        if (!BrowserNavigation.IsDocumentNavigation(Request) || HttpMethods.IsHead(Request.Method))
            return new StatusCodeResult(status);

        var isAuthenticated = User.Identity?.IsAuthenticated == true;
        var model = ErrorPageViewModel.For(
            status,
            isAuthenticated,
            backHref: BrowserNavigation.SameSiteLocalReferer(Request),
            showSwitchBoard: status == 404 && isAuthenticated && await ViewerBelongsToSeveralBoardsAsync(),
            loginReturnUrl: isAuthenticated ? null : LocalReturnUrl(status));
        return View("ErrorPage", model);
    }

    /// <summary>
    /// True when the signed-in viewer's own memberships number two or more. Nothing about the record
    /// that was asked for is read, and any failure answers false: the error page must never fail
    /// because a side read did.
    /// </summary>
    private async Task<bool> ViewerBelongsToSeveralBoardsAsync()
    {
        try
        {
            if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
                return false;

            var groupService = HttpContext.RequestServices.GetRequiredService<IGroupService>();
            var memberships = await groupService.GetGroupsForUserAsync(userId, HttpContext.RequestAborted);
            return memberships.Count >= 2;
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "The board switch button was left out because the membership read failed.");
            return false;
        }
    }

    /// <summary>
    /// The page an anonymous viewer was refused, as a local path and query, so signing in can bring
    /// them back to it. Only a GET that was not a 404 qualifies: a missing page is not somewhere to
    /// return to, and the address of a POST is not a page at all. With no original request (a direct
    /// visit to the error address) there is nothing to return to.
    /// </summary>
    private string? LocalReturnUrl(int status)
    {
        if (status == 404 || !HttpMethods.IsGet(Request.Method))
            return null;

        var original = HttpContext.Features.Get<IStatusCodeReExecuteFeature>();
        if (original == null)
            return null;

        var target = original.OriginalPathBase + original.OriginalPath + original.OriginalQueryString;
        return Url.IsLocalUrl(target) ? target : null;
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
