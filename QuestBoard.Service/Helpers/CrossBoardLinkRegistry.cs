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
    private static readonly Dictionary<string, CrossBoardLookupKind> Routes = new(StringComparer.OrdinalIgnoreCase)
    {
        [DefaultRoute<QuestController>(nameof(QuestController.Details))] = CrossBoardLookupKind.Quest,
        [DefaultRoute<QuestController>(nameof(QuestController.Edit))] = CrossBoardLookupKind.Quest,
        [DefaultRoute<QuestController>(nameof(QuestController.Manage))] = CrossBoardLookupKind.Quest,
        [DefaultRoute<QuestController>(nameof(QuestController.CreateFollowUp))] = CrossBoardLookupKind.Quest,
        // The quest log has no table of its own -- QuestLogController resolves everything
        // through the quest service, so both of its routes reuse the quest kind.
        [DefaultRoute<QuestLogController>(nameof(QuestLogController.Details))] = CrossBoardLookupKind.Quest,
        [DefaultRoute<QuestLogController>(nameof(QuestLogController.EditRecap))] = CrossBoardLookupKind.Quest,

        [DefaultRoute<EventsController>(nameof(EventsController.Details))] = CrossBoardLookupKind.Event,
        [DefaultRoute<EventsController>(nameof(EventsController.Edit))] = CrossBoardLookupKind.Event,

        [DefaultRoute<CharactersController>(nameof(CharactersController.Details))] = CrossBoardLookupKind.Character,
        [DefaultRoute<CharactersController>(nameof(CharactersController.Edit))] = CrossBoardLookupKind.Character,

        [DefaultRoute<ContactsController>(nameof(ContactsController.Details))] = CrossBoardLookupKind.Contact,
        [DefaultRoute<ContactsController>(nameof(ContactsController.Edit))] = CrossBoardLookupKind.Contact,

        [DefaultRoute<ShopController>(nameof(ShopController.Details))] = CrossBoardLookupKind.ShopItem,
        [DefaultRoute<ShopManagementController>(nameof(ShopManagementController.Edit))] = CrossBoardLookupKind.ShopItem,

        [DefaultRoute<SeriesController>(nameof(SeriesController.Details))] = CrossBoardLookupKind.EventSeries,

        [DefaultRoute<ContactCategoryManagementController>(nameof(ContactCategoryManagementController.Edit))] = CrossBoardLookupKind.ContactCategory,

        [DefaultRoute<DungeonMasterController>(nameof(DungeonMasterController.Profile))] = CrossBoardLookupKind.BoardMember,
        [DefaultRoute<DungeonMasterController>(nameof(DungeonMasterController.EditProfile))] = CrossBoardLookupKind.BoardMember
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
        if (controller == null || action == null)
        {
            kind = default;
            return false;
        }

        return Routes.TryGetValue(RouteKey(area, controller, action), out kind);
    }

    internal static IReadOnlyCollection<string> RegisteredRoutes => Routes.Keys;
}
