namespace QuestBoard.Service.ViewModels.ContactViewModels;

public class ContactsIndexViewModel
{
    // Flat, alphabetical list — Contacts have no owner concept, so unlike Characters there is
    // no "My/Other" split.
    public IList<ContactViewModel> Contacts { get; set; } = [];

    // Current state of the per-group "Show Hidden" session toggle.
    public bool ShowHidden { get; set; }

    // Drives whether the Show Hidden toggle and the "+ Contact" button render.
    public bool ViewerIsDmTier { get; set; }
}
