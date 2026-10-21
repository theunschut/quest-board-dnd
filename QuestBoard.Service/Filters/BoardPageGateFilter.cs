using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using QuestBoard.Domain.Enums;
using QuestBoard.Domain.Interfaces;
using QuestBoard.Service.Helpers;

namespace QuestBoard.Service.Filters;

/// <summary>
/// Marks a controller or action as serving a board page. Every page named on an endpoint must be
/// available on the active board for the request to pass.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
public sealed class BoardPageAttribute(BoardPage page) : Attribute
{
    public BoardPage Page { get; } = page;
}

/// <summary>
/// Keeps one action open while the page of its controller is switched off, for endpoints that
/// must keep working regardless of which pages a board offers.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class BoardPageExemptAttribute : Attribute;

/// <summary>
/// Refuses a request for a page the active board does not offer, on every HTTP verb and for every
/// role, and sends the visitor to the board's home page with a short notice. It runs after
/// authorization, so anonymous visitors and users without the required role still get the login or
/// access-denied response they got before.
/// </summary>
public sealed class BoardPageGateFilter(IBoardPageResolver resolver, ITempDataDictionaryFactory tempDataFactory)
    : IAsyncResourceFilter
{
    public async Task OnResourceExecutionAsync(ResourceExecutingContext context, ResourceExecutionDelegate next)
    {
        var metadata = context.HttpContext.GetEndpoint()?.Metadata;
        var pages = metadata?.GetOrderedMetadata<BoardPageAttribute>();
        if (metadata is null || pages is null || pages.Count == 0 || metadata.GetMetadata<BoardPageExemptAttribute>() is not null)
        {
            await next();
            return;
        }

        // Without a resolved board type the group-session middleware owns the no-board state and
        // the board query filters fail closed, so there is nothing for this gate to decide.
        var availability = await resolver.GetAsync(context.HttpContext.RequestAborted);
        if (availability is null)
        {
            await next();
            return;
        }

        var refused = pages.FirstOrDefault(attribute => !availability.IsAvailable(attribute.Page));
        if (refused is null)
        {
            await next();
            return;
        }

        var tempData = tempDataFactory.GetTempData(context.HttpContext);
        tempData["Info"] = BoardPageRoutes.NotUsedMessage(refused.Page);

        // The home route is an available page by construction, so this redirect cannot loop.
        var route = BoardPageRoutes.HomeRoute(availability);
        context.Result = new RedirectToActionResult(route.Action, route.Controller, new { area = "" });
    }
}
