using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using QuestBoard.Service.Helpers;

namespace QuestBoard.Service.Middleware;

/// <summary>
/// Decides, per request, whether a bodiless error status may be replaced by the themed error page.
///
/// The status-code pages middleware ahead of this one re-executes every bodiless 4xx and 5xx. That
/// is right for a browser showing a page and wrong for everything else: a script reading the response
/// text into an alert, an image, a stylesheet, a calendar client or a health probe would receive HTML
/// where it expects a bare status. So the feature is switched off, for this request only, when:
///   1. the request is not a document navigation (see BrowserNavigation), or
///   2. the path belongs to the job dashboard or the health endpoint, which keep their own answers.
/// The status-code middleware reads the flag after the rest of the pipeline has run, so switching it
/// off here, before calling on, is enough.
/// </summary>
public class ErrorPageGateMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var excluded = context.Request.Path.StartsWithSegments("/hangfire")
            || context.Request.Path.StartsWithSegments("/health");

        if (excluded || !BrowserNavigation.IsDocumentNavigation(context.Request))
        {
            var feature = context.Features.Get<IStatusCodePagesFeature>();
            if (feature != null)
                feature.Enabled = false;
        }

        await next(context);
    }
}
