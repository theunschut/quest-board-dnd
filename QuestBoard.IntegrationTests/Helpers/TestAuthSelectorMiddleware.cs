using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;

namespace QuestBoard.IntegrationTests.Helpers;

/// <summary>
/// Middleware that selects the Test authentication scheme when a Test authorization header is present
/// </summary>
public class TestAuthSelectorMiddleware(RequestDelegate next, IWebHostEnvironment environment)
{
    public async Task InvokeAsync(HttpContext context)
    {
        // Only allow test authentication in non-production environments
        if (!environment.IsProduction())
        {
            var authHeader = context.Request.Headers.Authorization.ToString();

            if (!string.IsNullOrEmpty(authHeader) && authHeader.StartsWith("Test ", StringComparison.Ordinal))
            {
                // Use Test authentication scheme for this request
                var result = await context.AuthenticateAsync("Test");
                if (result.Succeeded)
                {
                    context.User = result.Principal;
                }
            }
        }

        await next(context);
    }
}