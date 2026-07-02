using Hangfire.Dashboard;

namespace QuestBoard.Service.Authorization;

public class AdminDashboardAuthFilter : IDashboardAuthorizationFilter
{
    public bool Authorize(DashboardContext context)
    {
        var httpContext = context.GetHttpContext();

        if (httpContext.User.Identity?.IsAuthenticated != true)
            return false;

        if (!httpContext.User.IsInRole("SuperAdmin"))
            return false;

        return true;
    }
}
