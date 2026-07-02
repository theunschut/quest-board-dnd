using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.AspNetCore.Routing;
using QuestBoard.Service.ViewExpanders;

namespace QuestBoard.IntegrationTests.Mobile;

public class MobileViewLocationExpanderTests
{
    private static MobileViewLocationExpander CreateExpander() => new();

    private static ViewLocationExpanderContext CreateExpanderContext(DefaultHttpContext httpContext)
    {
        var actionContext = new ActionContext(httpContext, new RouteData(), new ActionDescriptor());
        var ctx = new ViewLocationExpanderContext(actionContext, "Index", "Home", "Home", "_Layout", false);
        // In .NET 10 the Values dictionary is null after construction; the real RazorViewEngine
        // initialises it before invoking expanders. Initialise it here to match runtime behaviour.
        ctx.Values = new Dictionary<string, string?>(StringComparer.Ordinal);
        return ctx;
    }

    // PopulateValues tests

    [Fact]
    public void PopulateValues_WhenIsMobileIsTrue_WritesIsMobileTrueToValues()
    {
        // Arrange
        var httpContext = new DefaultHttpContext();
        httpContext.Items["IsMobile"] = true;
        var expanderContext = CreateExpanderContext(httpContext);
        var expander = CreateExpander();

        // Act
        expander.PopulateValues(expanderContext);

        // Assert
        expanderContext.Values["isMobile"].Should().Be("True");
    }

    [Fact]
    public void PopulateValues_WhenIsMobileIsFalse_WritesIsMobileFalseToValues()
    {
        // Arrange
        var httpContext = new DefaultHttpContext();
        httpContext.Items["IsMobile"] = false;
        var expanderContext = CreateExpanderContext(httpContext);
        var expander = CreateExpander();

        // Act
        expander.PopulateValues(expanderContext);

        // Assert
        expanderContext.Values["isMobile"].Should().Be("False");
    }

    [Fact]
    public void PopulateValues_WhenIsMobileKeyAbsent_WritesIsMobileFalseToValues()
    {
        // Arrange
        var httpContext = new DefaultHttpContext();
        // IsMobile key is NOT set — simulates static file or health check requests
        var expanderContext = CreateExpanderContext(httpContext);
        var expander = CreateExpander();

        // Act
        expander.PopulateValues(expanderContext);

        // Assert
        expanderContext.Values["isMobile"].Should().Be("False");
    }

    // ExpandViewLocations tests

    [Fact]
    public void ExpandViewLocations_WhenIsMobileTrue_YieldsMobilePathBeforeOriginalPath()
    {
        // Arrange
        var httpContext = new DefaultHttpContext();
        var expanderContext = CreateExpanderContext(httpContext);
        expanderContext.Values["isMobile"] = "True";
        var expander = CreateExpander();
        var inputLocations = new[] { "/Views/{1}/{0}.cshtml" };

        // Act
        var result = expander.ExpandViewLocations(expanderContext, inputLocations).ToList();

        // Assert
        result.Should().Equal(
            "/Views/{1}/{0}.Mobile.cshtml",
            "/Views/{1}/{0}.cshtml");
    }

    [Fact]
    public void ExpandViewLocations_WhenIsMobileFalse_ReturnsInputLocationsUnchanged()
    {
        // Arrange
        var httpContext = new DefaultHttpContext();
        var expanderContext = CreateExpanderContext(httpContext);
        expanderContext.Values["isMobile"] = "False";
        var expander = CreateExpander();
        var inputLocations = new[] { "/Views/{1}/{0}.cshtml" };

        // Act
        var result = expander.ExpandViewLocations(expanderContext, inputLocations).ToList();

        // Assert
        result.Should().Equal("/Views/{1}/{0}.cshtml");
    }

    [Fact]
    public void ExpandViewLocations_WhenIsMobileKeyAbsent_ReturnsInputLocationsUnchanged()
    {
        // Arrange
        var httpContext = new DefaultHttpContext();
        var expanderContext = CreateExpanderContext(httpContext);
        // isMobile key is NOT in Values
        var expander = CreateExpander();
        var inputLocations = new[] { "/Views/{1}/{0}.cshtml", "/Views/Shared/{0}.cshtml" };

        // Act
        var result = expander.ExpandViewLocations(expanderContext, inputLocations).ToList();

        // Assert
        result.Should().Equal("/Views/{1}/{0}.cshtml", "/Views/Shared/{0}.cshtml");
    }

    [Fact]
    public void ExpandViewLocations_WhenMobileWithMultipleLocations_ExpandsAllLocations()
    {
        // Arrange
        var httpContext = new DefaultHttpContext();
        var expanderContext = CreateExpanderContext(httpContext);
        expanderContext.Values["isMobile"] = "True";
        var expander = CreateExpander();
        var inputLocations = new[] { "/Views/{1}/{0}.cshtml", "/Views/Shared/{0}.cshtml" };

        // Act
        var result = expander.ExpandViewLocations(expanderContext, inputLocations).ToList();

        // Assert
        result.Should().Equal(
            "/Views/{1}/{0}.Mobile.cshtml",
            "/Views/{1}/{0}.cshtml",
            "/Views/Shared/{0}.Mobile.cshtml",
            "/Views/Shared/{0}.cshtml");
    }
}
