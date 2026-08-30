namespace QuestBoard.Service.ViewModels.ContactViewModels;

// A single heading and the contacts under it, computed by the controller after the
// visibility filter runs. It carries no numeric total of any kind: a true total discloses
// how many hidden contacts a category holds, and a viewer-scoped total would visibly change
// when a DM toggles Show Hidden, which reads as a bug.
public class ContactCategoryGroupViewModel
{
    public string Title { get; set; } = string.Empty;

    // True for the synthetic "Ungrouped" bucket, so the view can mute the heading without
    // string-comparing the title.
    public bool IsUngrouped { get; set; }

    public IList<ContactViewModel> Contacts { get; set; } = [];
}
