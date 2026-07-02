namespace QuestBoard.Service.Middleware;

public class MobileDetectionMiddleware(RequestDelegate next)
{
    private static readonly string[] MobileKeywords =
        ["Mobi", "Android", "iPhone", "iPad", "Windows Phone", "BlackBerry"];

    public async Task InvokeAsync(HttpContext context)
    {
        var userAgent = context.Request.Headers.UserAgent.ToString();
        var isMobile = MobileKeywords.Any(kw =>
            userAgent.Contains(kw, StringComparison.OrdinalIgnoreCase));

        context.Items["IsMobile"] = isMobile;

        await next(context);
    }
}
