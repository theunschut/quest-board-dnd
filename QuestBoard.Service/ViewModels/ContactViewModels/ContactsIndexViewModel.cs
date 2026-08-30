namespace QuestBoard.Service.ViewModels.ContactViewModels;

public class ContactsIndexViewModel
{
    // The flat list is what renders when the board has no categories at all; CategoryGroups
    // is what renders otherwise.
    public IList<ContactViewModel> Contacts { get; set; } = [];

    // Nested groups, computed once by the controller after the visibility filter runs, in
    // SortOrder with the synthetic Ungrouped bucket pinned last. Empty groups are already
    // dropped by the time this list reaches the view.
    public IList<ContactCategoryGroupViewModel> CategoryGroups { get; set; } = [];

    // Whether the board has any category at all -- this decides between the flat list and
    // CategoryGroups, not whether any group happens to be non-empty.
    public bool HasCategories { get; set; }

    // Current state of the per-group "Show Hidden" session toggle.
    public bool ShowHidden { get; set; }

    // Drives whether the Show Hidden toggle and the "+ Contact" button render.
    public bool ViewerIsDmTier { get; set; }
}
