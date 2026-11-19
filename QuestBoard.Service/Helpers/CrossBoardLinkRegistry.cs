using QuestBoard.Domain.Enums;
using QuestBoard.Service.Controllers.Characters;
using QuestBoard.Service.Controllers.Contacts;
using QuestBoard.Service.Controllers.DungeonMaster;
using QuestBoard.Service.Controllers.Events;
using QuestBoard.Service.Controllers.QuestBoard;
using QuestBoard.Service.Controllers.Shop;

namespace QuestBoard.Service.Helpers;

/// <summary>
/// The closed table of (area, controller, action) routes that can be resolved across boards. A
/// route absent from this table can never be resolved across boards -- adding an action to the
/// application later never silently gains the ability to repoint a viewer's session; it has to
/// be added here first.
///
/// Each route also states what it asks of the viewer on the board it would take them to: the
/// access level the page needs and the board page it belongs to. A board that does not offer the
/// page, or where the viewer's role cannot open it, is never switched to.
///
/// The area is part of the key rather than an afterthought. Every route registered below sits on
/// the default, area-less route, so an area-scoped controller that happens to reuse one of these
/// controller and action names resolves to its own entry -- or, having none, to nothing at all --
/// instead of silently borrowing the lookup kind of the same-named default route.
///
/// The six image subresources (Characters/GetProfilePicture, Characters/GetCroppedPicture,
/// Contacts/GetContactImage, Contacts/GetCroppedContactImage, DungeonMaster/GetDMProfilePicture,
/// DungeonMaster/GetOriginalDMProfilePicture) are deliberately absent -- they arrive as a
/// non-document fetch destination and fail the navigation gate on their own, and once the page
/// itself has switched the board, the images that page then requests resolve normally. The same
/// is true of Shop/Details's isModal AJAX variant. The right way to exclude a route is to leave
/// it out of this table, never to add a second, parallel exclusion list that would then need
/// keeping in sync with this one.
/// </summary>
internal static class CrossBoardLinkRegistry
{
    private static readonly Dictionary<string, CrossBoardRouteEntry> Routes = new(StringComparer.OrdinalIgnoreCase)
    {
        [DefaultRoute<QuestController>(nameof(QuestController.Details))] = new(CrossBoardLookupKind.Quest, CrossBoardAccess.Member, BoardPage.Quests),
        [DefaultRoute<QuestController>(nameof(QuestController.Edit))] = new(CrossBoardLookupKind.Quest, CrossBoardAccess.QuestOwnerOrAdmin, BoardPage.Quests),
        [DefaultRoute<QuestController>(nameof(QuestController.Manage))] = new(CrossBoardLookupKind.Quest, CrossBoardAccess.DungeonMasterTier, BoardPage.Quests),
        [DefaultRoute<QuestController>(nameof(QuestController.CreateFollowUp))] = new(CrossBoardLookupKind.Quest, CrossBoardAccess.QuestOwnerOrAdmin, BoardPage.Quests),
        // The quest log has no table of its own -- QuestLogController resolves everything
        // through the quest service, so both of its routes reuse the quest kind.
        [DefaultRoute<QuestLogController>(nameof(QuestLogController.Details))] = new(CrossBoardLookupKind.Quest, CrossBoardAccess.CompletedQuest, BoardPage.QuestLog),
        [DefaultRoute<QuestLogController>(nameof(QuestLogController.EditRecap))] = new(CrossBoardLookupKind.Quest, CrossBoardAccess.CompletedQuest, BoardPage.QuestLog),

        [DefaultRoute<EventsController>(nameof(EventsController.Details))] = new(CrossBoardLookupKind.Event, CrossBoardAccess.Member, BoardPage.Events),
        [DefaultRoute<EventsController>(nameof(EventsController.Edit))] = new(CrossBoardLookupKind.Event, CrossBoardAccess.DungeonMasterTier, BoardPage.Events),

        [DefaultRoute<CharactersController>(nameof(CharactersController.Details))] = new(CrossBoardLookupKind.Character, CrossBoardAccess.Member, BoardPage.Characters),
        [DefaultRoute<CharactersController>(nameof(CharactersController.Edit))] = new(CrossBoardLookupKind.Character, CrossBoardAccess.CharacterOwnerOrAdmin, BoardPage.Characters),

        [DefaultRoute<ContactsController>(nameof(ContactsController.Details))] = new(CrossBoardLookupKind.Contact, CrossBoardAccess.ContactVisible, BoardPage.Contacts),
        [DefaultRoute<ContactsController>(nameof(ContactsController.Edit))] = new(CrossBoardLookupKind.Contact, CrossBoardAccess.DungeonMasterTier, BoardPage.Contacts),

        [DefaultRoute<ShopController>(nameof(ShopController.Details))] = new(CrossBoardLookupKind.ShopItem, CrossBoardAccess.ShopItemVisible, BoardPage.Shop),
        [DefaultRoute<ShopManagementController>(nameof(ShopManagementController.Edit))] = new(CrossBoardLookupKind.ShopItem, CrossBoardAccess.DungeonMasterTier, BoardPage.Shop),

        [DefaultRoute<SeriesController>(nameof(SeriesController.Details))] = new(CrossBoardLookupKind.EventSeries, CrossBoardAccess.Member, BoardPage.Events),

        [DefaultRoute<ContactCategoryManagementController>(nameof(ContactCategoryManagementController.Edit))] = new(CrossBoardLookupKind.ContactCategory, CrossBoardAccess.DungeonMasterTier, BoardPage.Contacts),

        [DefaultRoute<DungeonMasterController>(nameof(DungeonMasterController.Profile))] = new(CrossBoardLookupKind.BoardMember, CrossBoardAccess.Member, BoardPage.DungeonMasterProfile),
        [DefaultRoute<DungeonMasterController>(nameof(DungeonMasterController.EditProfile))] = new(CrossBoardLookupKind.BoardMember, CrossBoardAccess.AdminForOtherUser, BoardPage.DungeonMasterProfile)
    };

    // Every route registered above sits on the default route, which carries no area.
    private static string DefaultRoute<TController>(string action) where TController : Microsoft.AspNetCore.Mvc.Controller
        => RouteKey(null, ControllerNameOf<TController>(), action);

    // A missing area and an empty one are the same thing -- the default route omits the route
    // value entirely, while a caller reconstructing a route by hand may well pass "" -- so both
    // spellings have to produce the same key or one of them would silently miss the table.
    private static string RouteKey(string? area, string controller, string action)
        => $"{area ?? string.Empty}/{controller}/{action}";

    // Copied from GroupSessionMiddleware rather than shared, so a rename of either controller
    // remains visible in both files independently -- the call site keeps compiling, but a
    // rename tool will surface this file too instead of silently breaking a runtime lookup.
    private static string ControllerNameOf<TController>() where TController : Microsoft.AspNetCore.Mvc.Controller
    {
        const string suffix = "Controller";
        var name = typeof(TController).Name;
        return name.EndsWith(suffix, StringComparison.Ordinal) ? name[..^suffix.Length] : name;
    }

    internal static bool TryGetLookupKind(string? area, string? controller, string? action, out CrossBoardLookupKind kind)
    {
        if (TryGetRouteEntry(area, controller, action, out var entry))
        {
            kind = entry.Kind;
            return true;
        }

        kind = default;
        return false;
    }

    internal static bool TryGetRouteEntry(string? area, string? controller, string? action, out CrossBoardRouteEntry entry)
    {
        if (controller == null || action == null)
        {
            entry = default!;
            return false;
        }

        return Routes.TryGetValue(RouteKey(area, controller, action), out entry!);
    }

    internal static IReadOnlyCollection<string> RegisteredRoutes => Routes.Keys;
}

/// <summary>What a registered route looks up, what it asks of the viewer, and the board page it belongs to.</summary>
internal sealed record CrossBoardRouteEntry(CrossBoardLookupKind Kind, CrossBoardAccess Access, BoardPage Page);
