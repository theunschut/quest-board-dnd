using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Logging;
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
public class CalendarFeedController(ICalendarSubscriptionService subscriptionService, ILogger<CalendarFeedController> logger) : Controller
{
    // The address in the route is the entire authentication mechanism for this endpoint, has no
    // expiry, and cannot be un-leaked once a calendar service has stored it on its own servers.
    // A log line is the one exposure this phase can actually prevent, so every logging call
    // below writes only the subscription's integer id, never the address value -- in full, in
    // part, or as a truncated prefix.
    [HttpGet("{feedToken}.ics")]
    [EnableRateLimiting("calendar-feed")]
    public async Task<IActionResult> Feed(string feedToken, CancellationToken token = default)
    {
        var result = await subscriptionService.GetFeedAsync(feedToken, token);

        // Telling a retired address apart from one that never existed is an accepted cost: the
        // request path can never remove a row, because doing so would make a just-retired
        // address indistinguishable from one that never existed. The visible control on the
        // Profile page is still called Delete; what it performs is a retirement. The row is
        // removed later, and only by the retention sweep, once the address has been retired
        // long enough that any client still holding it has stopped asking. The two different
        // answers also confirm to anyone probing that a given address was once real, which a
        // single answer for both would not give away -- accepted, because the address space
        // makes guessing infeasible regardless of which status confirms a hit.
        switch (result.Status)
        {
            case CalendarFeedStatus.NotFound:
                // No log line here at all, deliberately. A stream of unknown addresses is
                // exactly what a guessing attempt looks like, and logging each one would turn
                // an attacker's traffic into an unbounded log write -- a second denial-of-service
                // surface on top of the one the guess itself represents.
                return NotFound();

            case CalendarFeedStatus.Revoked:
                logger.LogInformation("Calendar feed request for revoked subscription {SubscriptionId}.", result.SubscriptionId);
                // MVC has no built-in Gone() helper.
                return StatusCode(StatusCodes.Status410Gone);

            default:
                return Content(result.Body ?? string.Empty, "text/calendar; charset=utf-8");
        }
    }
}
