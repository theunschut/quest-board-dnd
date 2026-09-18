using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using QuestBoard.Domain.Enums;
using QuestBoard.Domain.Interfaces;

namespace QuestBoard.Service.Controllers;

// Deliberately top-level, not under Admin/ or QuestBoard/: this is an anonymous,
// token-authenticated, machine-consumed surface, conceptually and security-wise distinct from
// every other controller in this application.
//
// The route is not /calendar/... -- CalendarController already owns that conventional prefix,
// so the feed lives under /feeds/calendar via this explicit route attribute.
//
// This controller is deliberately not added to GroupSessionMiddleware's exempt-path list. A
// real calendar client sends no cookie, and the middleware's first check passes every
// unauthenticated request through untouched before it ever consults that list, so calendar
// clients are unaffected. The only reader affected is a signed-in person opening the address in
// a browser with no board selected, who is sent to the board picker -- and a browser is not the
// tool this address is for. If that redirect is ever judged confusing, the fix is a literal
// "/feeds/calendar" entry in that list, because the list is built from controller names and
// CalendarFeed does not match this path.
[AllowAnonymous]
[Route("feeds/calendar")]
public class CalendarFeedController(ICalendarSubscriptionService subscriptionService) : Controller
{
    [HttpGet("{feedToken}.ics")]
    public async Task<IActionResult> Feed(string feedToken, CancellationToken token = default)
    {
        var result = await subscriptionService.GetFeedAsync(feedToken, token);

        return result.Status switch
        {
            CalendarFeedStatus.NotFound => NotFound(),
            // MVC has no built-in Gone() helper.
            CalendarFeedStatus.Revoked => StatusCode(StatusCodes.Status410Gone),
            _ => Content(result.Body ?? string.Empty, "text/calendar; charset=utf-8")
        };
    }
}
