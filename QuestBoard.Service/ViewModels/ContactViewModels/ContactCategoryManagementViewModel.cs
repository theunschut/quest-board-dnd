namespace QuestBoard.Service.ViewModels.ContactViewModels;

// The Manage Categories page's container model.
public class ContactCategoryManagementViewModel
{
    public IList<ContactCategoryViewModel> Categories { get; set; } = [];

    // The inline add form binds to NewCategory.Name, so this must never be null -- including
    // on the re-render after a failed submission.
    public ContactCategoryViewModel NewCategory { get; set; } = new();
}
