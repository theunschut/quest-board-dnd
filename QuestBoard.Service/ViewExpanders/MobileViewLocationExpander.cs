using Microsoft.AspNetCore.Mvc.Razor;

namespace QuestBoard.Service.ViewExpanders;

public class MobileViewLocationExpander : IViewLocationExpander
{
    private const string IsMobileKey = "isMobile";

    public void PopulateValues(ViewLocationExpanderContext context)
    {
        var isMobile = context.ActionContext.HttpContext.Items["IsMobile"] is true;
        context.Values[IsMobileKey] = isMobile.ToString();
    }

    public IEnumerable<string> ExpandViewLocations(
        ViewLocationExpanderContext context,
        IEnumerable<string> viewLocations)
    {
        if (!context.Values.TryGetValue(IsMobileKey, out var isMobileStr)
            || isMobileStr != "True")
        {
            return viewLocations;
        }

        return ExpandForMobile(viewLocations);
    }

    private static IEnumerable<string> ExpandForMobile(IEnumerable<string> viewLocations)
    {
        foreach (var location in viewLocations)
        {
            yield return location.Replace(".cshtml", ".Mobile.cshtml",
                StringComparison.OrdinalIgnoreCase);
            yield return location;
        }
    }
}
