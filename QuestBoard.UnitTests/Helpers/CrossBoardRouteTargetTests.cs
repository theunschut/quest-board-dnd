using Microsoft.AspNetCore.Http;
using QuestBoard.Domain.Enums;
using QuestBoard.Service.Helpers;

namespace QuestBoard.UnitTests.Helpers;

public class CrossBoardRouteTargetTests
{
    [Fact]
    public void TryFromLocalUrl_CanonicalShape_ResolvesToExpectedKindAndId()
    {
        var found = CrossBoardRouteTarget.TryFromLocalUrl("/Quest/Details/42", out var target);

        found.Should().BeTrue();
        target.Should().NotBeNull();
        target!.Kind.Should().Be(CrossBoardLookupKind.Quest);
        target.Id.Should().Be(42);
    }

    [Fact]
    public void TryFromLocalUrl_TrailingSlash_StillResolves()
    {
        var found = CrossBoardRouteTarget.TryFromLocalUrl("/Quest/Details/42/", out var target);

        found.Should().BeTrue();
        target!.Id.Should().Be(42);
    }

    [Fact]
    public void TryFromLocalUrl_QueryStringIsIgnored()
    {
        var found = CrossBoardRouteTarget.TryFromLocalUrl("/Quest/Details/42?from=email", out var target);

        found.Should().BeTrue();
        target!.Id.Should().Be(42);
    }

    [Fact]
    public void TryFromLocalUrl_FragmentIsIgnored()
    {
        var found = CrossBoardRouteTarget.TryFromLocalUrl("/Quest/Details/42#section", out var target);

        found.Should().BeTrue();
        target!.Id.Should().Be(42);
    }

    [Theory]
    [InlineData("/quest/details/42")]
    [InlineData("/QUEST/DETAILS/42")]
    [InlineData("/Quest/details/42")]
    [InlineData("/quest/Details/42")]
    public void TryFromLocalUrl_MixedCaseSegments_Resolves(string url)
    {
        var found = CrossBoardRouteTarget.TryFromLocalUrl(url, out var target);

        found.Should().BeTrue();
        target!.Kind.Should().Be(CrossBoardLookupKind.Quest);
        target.Id.Should().Be(42);
    }

    [Theory]
    [InlineData("/quests")]
    [InlineData("/Quest")]
    [InlineData("/Quest/Details")]
    [InlineData("/Quest/Details/42/extra")]
    public void TryFromLocalUrl_WrongSegmentCount_ResolvesToNothing(string url)
    {
        var found = CrossBoardRouteTarget.TryFromLocalUrl(url, out var target);

        found.Should().BeFalse();
        target.Should().BeNull();
    }

    [Fact]
    public void TryFromLocalUrl_NonNumericId_ResolvesToNothing()
    {
        var found = CrossBoardRouteTarget.TryFromLocalUrl("/Quest/Details/notanumber", out var target);

        found.Should().BeFalse();
        target.Should().BeNull();
    }

    [Fact]
    public void TryFromLocalUrl_OverflowingId_ResolvesToNothing()
    {
        var found = CrossBoardRouteTarget.TryFromLocalUrl("/Quest/Details/99999999999999999999", out var target);

        found.Should().BeFalse();
        target.Should().BeNull();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void TryFromLocalUrl_NullEmptyOrWhitespace_ResolvesToNothing(string? url)
    {
        var found = CrossBoardRouteTarget.TryFromLocalUrl(url, out var target);

        found.Should().BeFalse();
        target.Should().BeNull();
    }

    [Fact]
    public void TryFromLocalUrl_AbsoluteUrl_ResolvesToNothing()
    {
        var found = CrossBoardRouteTarget.TryFromLocalUrl("https://evil.example/Quest/Details/42", out var target);

        found.Should().BeFalse();
        target.Should().BeNull();
    }

    [Fact]
    public void TryFromLocalUrl_ProtocolRelativeUrl_ResolvesToNothing()
    {
        var found = CrossBoardRouteTarget.TryFromLocalUrl("//evil.example/Quest/Details/42", out var target);

        found.Should().BeFalse();
        target.Should().BeNull();
    }

    [Fact]
    public void TryFromLocalUrl_BackslashPrefixedUrl_ResolvesToNothing()
    {
        var found = CrossBoardRouteTarget.TryFromLocalUrl("/\\evil.example/Quest/Details/42", out var target);

        found.Should().BeFalse();
        target.Should().BeNull();
    }

    [Fact]
    public void TryFromLocalUrl_EncodedPath_ResolvesToNothing()
    {
        // Encoded slashes are never decoded a second time here, so this is one long segment
        // rather than three -- it resolves to nothing exactly like any other one-segment path.
        var found = CrossBoardRouteTarget.TryFromLocalUrl("/Quest%2FDetails%2F42", out var target);

        found.Should().BeFalse();
        target.Should().BeNull();
    }

    [Fact]
    public void TryFromLocalUrl_UnregisteredControllerActionPair_ResolvesToNothing()
    {
        // A real route that the registry deliberately leaves out: the image subresources arrive
        // as a non-document fetch and are excluded on purpose, so this stays a meaningful example
        // of "exists in the application, absent from the table" rather than a made-up path.
        var found = CrossBoardRouteTarget.TryFromLocalUrl("/Characters/GetProfilePicture/42", out var target);

        found.Should().BeFalse();
        target.Should().BeNull();
    }

    // TryFromRouteValues reads the area alongside controller and action. A route landing in an
    // area cannot resolve through a same-named default route's registry entry -- the deep-link
    // middleware sits ahead of the query filters, so a wrong lookup kind here would mean an id
    // resolved against the wrong table.
    [Theory]
    [InlineData("Platform")]
    [InlineData("Admin")]
    public void TryFromRouteValues_AreaScopedRouteReusingRegisteredName_ResolvesToNothing(string area)
    {
        var context = new DefaultHttpContext();
        context.Request.RouteValues["area"] = area;
        context.Request.RouteValues["controller"] = "Quest";
        context.Request.RouteValues["action"] = "Details";
        context.Request.RouteValues["id"] = "42";

        var found = CrossBoardRouteTarget.TryFromRouteValues(context, out var target);

        found.Should().BeFalse();
        target.Should().BeNull();
    }

    [Fact]
    public void TryFromRouteValues_DefaultRouteWithNoArea_Resolves()
    {
        var context = new DefaultHttpContext();
        context.Request.RouteValues["controller"] = "Quest";
        context.Request.RouteValues["action"] = "Details";
        context.Request.RouteValues["id"] = "42";

        var found = CrossBoardRouteTarget.TryFromRouteValues(context, out var target);

        found.Should().BeTrue();
        target.Should().NotBeNull();
        target!.Kind.Should().Be(CrossBoardLookupKind.Quest);
        target.Id.Should().Be(42);
    }
}
