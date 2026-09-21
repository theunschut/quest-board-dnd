using QuestBoard.Domain.Enums;
using QuestBoard.Service.Controllers.Characters;
using QuestBoard.Service.Controllers.Contacts;
using QuestBoard.Service.Controllers.DungeonMaster;
using QuestBoard.Service.Controllers.Events;
using QuestBoard.Service.Controllers.QuestBoard;
using QuestBoard.Service.Controllers.Shop;

namespace QuestBoard.Service.Helpers;

/// <summary>
/// The closed table of (controller, action) routes that can be resolved across boards. A route
/// absent from this table can never be resolved across boards -- adding an action to the
/// application later never silently gains the ability to repoint a viewer's session; it has to
/// be added here first.
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
        [$"{ControllerNameOf<QuestController>()}/{nameof(QuestController.Details)}"] = CrossBoardLookupKind.Quest,
        [$"{ControllerNameOf<QuestController>()}/{nameof(QuestController.Edit)}"] = CrossBoardLookupKind.Quest,
        [$"{ControllerNameOf<QuestController>()}/{nameof(QuestController.Manage)}"] = CrossBoardLookupKind.Quest,
        [$"{ControllerNameOf<QuestController>()}/{nameof(QuestController.CreateFollowUp)}"] = CrossBoardLookupKind.Quest,
        // The quest log has no table of its own -- QuestLogController resolves everything
        // through the quest service, so both of its routes reuse the quest kind.
        [$"{ControllerNameOf<QuestLogController>()}/{nameof(QuestLogController.Details)}"] = CrossBoardLookupKind.Quest,
        [$"{ControllerNameOf<QuestLogController>()}/{nameof(QuestLogController.EditRecap)}"] = CrossBoardLookupKind.Quest,

        [$"{ControllerNameOf<EventsController>()}/{nameof(EventsController.Details)}"] = CrossBoardLookupKind.Event,
        [$"{ControllerNameOf<EventsController>()}/{nameof(EventsController.Edit)}"] = CrossBoardLookupKind.Event,

        [$"{ControllerNameOf<CharactersController>()}/{nameof(CharactersController.Details)}"] = CrossBoardLookupKind.Character,
        [$"{ControllerNameOf<CharactersController>()}/{nameof(CharactersController.Edit)}"] = CrossBoardLookupKind.Character,

        [$"{ControllerNameOf<ContactsController>()}/{nameof(ContactsController.Details)}"] = CrossBoardLookupKind.Contact,
        [$"{ControllerNameOf<ContactsController>()}/{nameof(ContactsController.Edit)}"] = CrossBoardLookupKind.Contact,

        [$"{ControllerNameOf<ShopController>()}/{nameof(ShopController.Details)}"] = CrossBoardLookupKind.ShopItem,
        [$"{ControllerNameOf<ShopManagementController>()}/{nameof(ShopManagementController.Edit)}"] = CrossBoardLookupKind.ShopItem,

        [$"{ControllerNameOf<SeriesController>()}/{nameof(SeriesController.Details)}"] = CrossBoardLookupKind.EventSeries,

        [$"{ControllerNameOf<ContactCategoryManagementController>()}/{nameof(ContactCategoryManagementController.Edit)}"] = CrossBoardLookupKind.ContactCategory,

        [$"{ControllerNameOf<DungeonMasterController>()}/{nameof(DungeonMasterController.Profile)}"] = CrossBoardLookupKind.BoardMember,
        [$"{ControllerNameOf<DungeonMasterController>()}/{nameof(DungeonMasterController.EditProfile)}"] = CrossBoardLookupKind.BoardMember
    };

    // Copied from GroupSessionMiddleware rather than shared, so a rename of either controller
    // remains visible in both files independently -- the call site keeps compiling, but a
    // rename tool will surface this file too instead of silently breaking a runtime lookup.
    private static string ControllerNameOf<TController>() where TController : Microsoft.AspNetCore.Mvc.Controller
    {
        const string suffix = "Controller";
        var name = typeof(TController).Name;
        return name.EndsWith(suffix, StringComparison.Ordinal) ? name[..^suffix.Length] : name;
    }

    internal static bool TryGetLookupKind(string? controller, string? action, out CrossBoardLookupKind kind)
    {
        if (controller == null || action == null)
        {
            kind = default;
            return false;
        }

        return Routes.TryGetValue($"{controller}/{action}", out kind);
    }

    internal static IReadOnlyCollection<string> RegisteredRoutes => Routes.Keys;
}
