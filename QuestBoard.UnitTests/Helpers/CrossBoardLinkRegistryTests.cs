using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using QuestBoard.Domain.Enums;
using QuestBoard.Service.Filters;
using QuestBoard.Service.Helpers;

namespace QuestBoard.UnitTests.Helpers;

public class CrossBoardLinkRegistryTests
{
    [Fact]
    public void TryGetLookupKind_QuestDetailsRoute_ResolvesToQuestKind()
    {
        var found = CrossBoardLinkRegistry.TryGetLookupKind(null, "Quest", "Details", out var kind);

        found.Should().BeTrue();
        kind.Should().Be(CrossBoardLookupKind.Quest);
    }

    [Theory]
    [InlineData("quest", "details")]
    [InlineData("QUEST", "DETAILS")]
    [InlineData("Quest", "details")]
    [InlineData("quest", "Details")]
    public void TryGetLookupKind_IsCaseInsensitiveInBothSegments(string controller, string action)
    {
        var found = CrossBoardLinkRegistry.TryGetLookupKind(null, controller, action, out var kind);

        found.Should().BeTrue();
        kind.Should().Be(CrossBoardLookupKind.Quest);
    }

    [Fact]
    public void TryGetLookupKind_UnregisteredControllerActionPair_ReturnsFalse()
    {
        var found = CrossBoardLinkRegistry.TryGetLookupKind(null, "Quest", "SomeUnregisteredAction", out _);

        found.Should().BeFalse();
    }

    [Theory]
    [InlineData(null, "Details")]
    [InlineData("Quest", null)]
    [InlineData(null, null)]
    public void TryGetLookupKind_NullSegment_ReturnsFalse(string? controller, string? action)
    {
        var found = CrossBoardLinkRegistry.TryGetLookupKind(null, controller, action, out _);

        found.Should().BeFalse();
    }

    [Fact]
    public void TryGetRouteEntry_QuestEdit_ResolvesToQuestKindOwnerOrAdminAccessAndTheQuestsPage()
    {
        var found = CrossBoardLinkRegistry.TryGetRouteEntry(null, "Quest", "Edit", out var entry);

        found.Should().BeTrue();
        entry.Kind.Should().Be(CrossBoardLookupKind.Quest);
        entry.Access.Should().Be(CrossBoardAccess.QuestOwnerOrAdmin);
        entry.Page.Should().Be(BoardPage.Quests);
    }

    [Fact]
    public void RegisteredRoutes_HasExactly18Entries()
    {
        CrossBoardLinkRegistry.RegisteredRoutes.Should().HaveCount(18);
    }

    // The full 18-route mapping named by the phase's route list, exactly. Each pair resolves to
    // its named kind -- an addition, a removal or a mis-mapped kind all surface here as a failing
    // row rather than as a route that quietly stops (or starts) resolving.
    public static IEnumerable<object[]> RegisteredRoutePairs()
    {
        yield return ["Quest", "Details", CrossBoardLookupKind.Quest];
        yield return ["Quest", "Edit", CrossBoardLookupKind.Quest];
        yield return ["Quest", "Manage", CrossBoardLookupKind.Quest];
        yield return ["Quest", "CreateFollowUp", CrossBoardLookupKind.Quest];
        yield return ["QuestLog", "Details", CrossBoardLookupKind.Quest];
        yield return ["QuestLog", "EditRecap", CrossBoardLookupKind.Quest];
        yield return ["Events", "Details", CrossBoardLookupKind.Event];
        yield return ["Events", "Edit", CrossBoardLookupKind.Event];
        yield return ["Characters", "Details", CrossBoardLookupKind.Character];
        yield return ["Characters", "Edit", CrossBoardLookupKind.Character];
        yield return ["Contacts", "Details", CrossBoardLookupKind.Contact];
        yield return ["Contacts", "Edit", CrossBoardLookupKind.Contact];
        yield return ["Shop", "Details", CrossBoardLookupKind.ShopItem];
        yield return ["ShopManagement", "Edit", CrossBoardLookupKind.ShopItem];
        yield return ["Series", "Details", CrossBoardLookupKind.EventSeries];
        yield return ["ContactCategoryManagement", "Edit", CrossBoardLookupKind.ContactCategory];
        yield return ["DungeonMaster", "Profile", CrossBoardLookupKind.BoardMember];
        yield return ["DungeonMaster", "EditProfile", CrossBoardLookupKind.BoardMember];
    }

    [Theory]
    [MemberData(nameof(RegisteredRoutePairs))]
    public void TryGetLookupKind_EachRegisteredRoute_ResolvesToItsNamedKind(string controller, string action, CrossBoardLookupKind expectedKind)
    {
        var found = CrossBoardLinkRegistry.TryGetLookupKind(null, controller, action, out var kind);

        found.Should().BeTrue();
        kind.Should().Be(expectedKind);
    }

    // What each route asks of the viewer on the board it would take them to, and the page it
    // belongs to. A change to any row has to be made here on purpose: a wrong access level either
    // switches a viewer to a page that refuses them, or stops switching one that would have served.
    public static IEnumerable<object[]> RegisteredRouteEntries()
    {
        yield return ["Quest", "Details", CrossBoardLookupKind.Quest, CrossBoardAccess.Member, BoardPage.Quests];
        yield return ["Quest", "Edit", CrossBoardLookupKind.Quest, CrossBoardAccess.QuestOwnerOrAdmin, BoardPage.Quests];
        yield return ["Quest", "Manage", CrossBoardLookupKind.Quest, CrossBoardAccess.DungeonMasterTier, BoardPage.Quests];
        yield return ["Quest", "CreateFollowUp", CrossBoardLookupKind.Quest, CrossBoardAccess.QuestOwnerOrAdmin, BoardPage.Quests];
        yield return ["QuestLog", "Details", CrossBoardLookupKind.Quest, CrossBoardAccess.CompletedQuest, BoardPage.QuestLog];
        yield return ["QuestLog", "EditRecap", CrossBoardLookupKind.Quest, CrossBoardAccess.CompletedQuest, BoardPage.QuestLog];
        yield return ["Events", "Details", CrossBoardLookupKind.Event, CrossBoardAccess.Member, BoardPage.Events];
        yield return ["Events", "Edit", CrossBoardLookupKind.Event, CrossBoardAccess.DungeonMasterTier, BoardPage.Events];
        yield return ["Characters", "Details", CrossBoardLookupKind.Character, CrossBoardAccess.Member, BoardPage.Characters];
        yield return ["Characters", "Edit", CrossBoardLookupKind.Character, CrossBoardAccess.CharacterOwnerOrAdmin, BoardPage.Characters];
        yield return ["Contacts", "Details", CrossBoardLookupKind.Contact, CrossBoardAccess.ContactVisible, BoardPage.Contacts];
        yield return ["Contacts", "Edit", CrossBoardLookupKind.Contact, CrossBoardAccess.DungeonMasterTier, BoardPage.Contacts];
        yield return ["Shop", "Details", CrossBoardLookupKind.ShopItem, CrossBoardAccess.ShopItemVisible, BoardPage.Shop];
        yield return ["ShopManagement", "Edit", CrossBoardLookupKind.ShopItem, CrossBoardAccess.DungeonMasterTier, BoardPage.Shop];
        yield return ["Series", "Details", CrossBoardLookupKind.EventSeries, CrossBoardAccess.Member, BoardPage.Events];
        yield return ["ContactCategoryManagement", "Edit", CrossBoardLookupKind.ContactCategory, CrossBoardAccess.DungeonMasterTier, BoardPage.Contacts];
        yield return ["DungeonMaster", "Profile", CrossBoardLookupKind.BoardMember, CrossBoardAccess.Member, BoardPage.DungeonMasterProfile];
        yield return ["DungeonMaster", "EditProfile", CrossBoardLookupKind.BoardMember, CrossBoardAccess.AdminForOtherUser, BoardPage.DungeonMasterProfile];
    }

    [Fact]
    public void RouteEntryTable_CoversEveryRegisteredRoute()
    {
        RegisteredRouteEntries().Should().HaveCount(18);
        RegisteredRouteEntries()
            .Select(row => $"/{row[0]}/{row[1]}")
            .Should().BeEquivalentTo(CrossBoardLinkRegistry.RegisteredRoutes);
    }

    [Theory]
    [MemberData(nameof(RegisteredRouteEntries))]
    public void TryGetRouteEntry_EachRegisteredRoute_ResolvesToItsKindAccessAndPage(
        string controller, string action, CrossBoardLookupKind kind, CrossBoardAccess access, BoardPage page)
    {
        var found = CrossBoardLinkRegistry.TryGetRouteEntry(null, controller, action, out var entry);

        found.Should().BeTrue();
        entry.Kind.Should().Be(kind);
        entry.Access.Should().Be(access);
        entry.Page.Should().Be(page);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Platform")]
    public void TryGetRouteEntry_AreaScopedRoute_FindsNoEntryUnlessTheAreaIsAbsent(string? area)
    {
        var found = CrossBoardLinkRegistry.TryGetRouteEntry(area, "Quest", "Edit", out _);

        found.Should().Be(string.IsNullOrEmpty(area));
    }

    // The registry names the page a route belongs to, and so does the page attribute the gate
    // reads on the controller. Two places naming the same fact drift apart; this reads the
    // attributes by reflection and compares them, so a later change to either side fails here
    // rather than reopening a way to learn that a record exists. The pages named on an action and
    // its controller together must be exactly the registered page, because the gate requires every
    // named page, and no overload of the action may be exempt from the gate.
    [Theory]
    [MemberData(nameof(RegisteredRouteEntries))]
    public void EachRegisteredRoute_CarriesThePageItsActionIsGatedBy(
        string controller, string action, CrossBoardLookupKind kind, CrossBoardAccess access, BoardPage page)
    {
        _ = kind;
        _ = access;
        CrossBoardLinkRegistry.TryGetRouteEntry(null, controller, action, out var entry).Should().BeTrue();
        var registeredPage = entry.Page;
        registeredPage.Should().Be(page);

        var controllerType = ControllerTypeNamed(controller);
        var overloads = controllerType
            .GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(method => string.Equals(method.Name, action, StringComparison.Ordinal))
            .ToList();
        overloads.Should().NotBeEmpty($"{controller}/{action} must be an action of {controllerType.Name}");

        var gatedPages = overloads
            .SelectMany(method => method.GetCustomAttributes<BoardPageAttribute>(inherit: true))
            .Concat(controllerType.GetCustomAttributes<BoardPageAttribute>(inherit: true))
            .Select(attribute => attribute.Page)
            .Distinct()
            .ToList();

        gatedPages.Should().BeEquivalentTo([registeredPage], $"{controller}/{action} is gated by exactly the page the registry names");
        overloads.Should().OnlyContain(
            method => method.GetCustomAttribute<BoardPageExemptAttribute>() == null,
            $"{controller}/{action} must not be exempt from the page gate, or the board narrowing would refuse a page that serves");
    }

    private static Type ControllerTypeNamed(string controller)
    {
        var matches = typeof(CrossBoardLinkRegistry).Assembly.GetTypes()
            .Where(type => typeof(Controller).IsAssignableFrom(type)
                && !type.IsAbstract
                && string.Equals(type.Name, controller + "Controller", StringComparison.Ordinal)
                && type.Namespace?.Contains(".Areas.", StringComparison.Ordinal) != true)
            .ToList();
        matches.Should().ContainSingle($"{controller}Controller is the one default-route controller of that name");
        return matches[0];
    }

    // The six image subresources arrive as a non-document fetch destination and fail the
    // navigation gate on their own -- they must never be registered here, since a second,
    // independent way to exclude them would need to be kept in sync with this list.
    public static IEnumerable<object[]> ImageRoutes()
    {
        yield return ["Characters", "GetProfilePicture"];
        yield return ["Characters", "GetCroppedPicture"];
        yield return ["Contacts", "GetContactImage"];
        yield return ["Contacts", "GetCroppedContactImage"];
        yield return ["DungeonMaster", "GetDMProfilePicture"];
        yield return ["DungeonMaster", "GetOriginalDMProfilePicture"];
    }

    [Theory]
    [MemberData(nameof(ImageRoutes))]
    public void TryGetLookupKind_ImageRoute_ReturnsFalse(string controller, string action)
    {
        var found = CrossBoardLinkRegistry.TryGetLookupKind(null, controller, action, out _);

        found.Should().BeFalse();
    }

    // An area-scoped route that reuses a registered controller and action name resolves to
    // nothing, rather than borrowing the same-named default route's lookup kind. Platform is the
    // one area this application actually has; the invented ones stand in for any area added
    // later. No collision exists today -- these rows exist so that one cannot appear silently.
    public static IEnumerable<object[]> AreaScopedRoutesWithCollidingNames()
    {
        yield return ["Platform", "Quest", "Details"];
        yield return ["Platform", "Characters", "Edit"];
        yield return ["Platform", "DungeonMaster", "Profile"];
        yield return ["Admin", "Quest", "Manage"];
        yield return ["Reporting", "Shop", "Details"];
    }

    [Theory]
    [MemberData(nameof(AreaScopedRoutesWithCollidingNames))]
    public void TryGetLookupKind_AreaScopedRouteReusingRegisteredName_ReturnsFalse(string area, string controller, string action)
    {
        var found = CrossBoardLinkRegistry.TryGetLookupKind(area, controller, action, out _);

        found.Should().BeFalse();
    }

    // The default route omits the area route value entirely, but a caller reconstructing a route
    // by hand may pass an empty string -- both have to mean the same area-less route.
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void TryGetLookupKind_NullAndEmptyArea_BothMeanTheDefaultRoute(string? area)
    {
        var found = CrossBoardLinkRegistry.TryGetLookupKind(area, "Quest", "Details", out var kind);

        found.Should().BeTrue();
        kind.Should().Be(CrossBoardLookupKind.Quest);
    }

    [Fact]
    public void TryGetLookupKind_AreaIsCaseInsensitiveLikeTheOtherSegments()
    {
        // Not a registered route in any casing -- the point is that an area cannot be smuggled
        // past the key by changing its case either.
        var found = CrossBoardLinkRegistry.TryGetLookupKind("PLATFORM", "Quest", "Details", out _);

        found.Should().BeFalse();
    }

    // A handful of ordinary non-participating routes -- a list, an index, a delete -- to prove
    // the registry stays closed rather than resolving by controller name alone.
    public static IEnumerable<object[]> OrdinaryNonParticipatingRoutes()
    {
        yield return ["Quest", "Index"];
        yield return ["Events", "Index"];
        yield return ["Characters", "Index"];
        yield return ["Contacts", "Delete"];
        yield return ["Shop", "Index"];
    }

    [Theory]
    [MemberData(nameof(OrdinaryNonParticipatingRoutes))]
    public void TryGetLookupKind_OrdinaryNonParticipatingRoute_ReturnsFalse(string controller, string action)
    {
        var found = CrossBoardLinkRegistry.TryGetLookupKind(null, controller, action, out _);

        found.Should().BeFalse();
    }
}
