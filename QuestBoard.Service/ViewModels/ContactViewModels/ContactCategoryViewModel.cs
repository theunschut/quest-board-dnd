using System.ComponentModel.DataAnnotations;

namespace QuestBoard.Service.ViewModels.ContactViewModels;

// A single row on the Manage Categories page, and the shape the add/rename forms bind to.
public class ContactCategoryViewModel
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Category name is required")]
    [StringLength(60, ErrorMessage = "Category name cannot exceed 60 characters")]
    public string Name { get; set; } = string.Empty;

    public int SortOrder { get; set; }

    // ContactCount, IsFirst and IsLast are computed in the controller from the ordered list
    // and the count map, never mapped -- the same imperative style already used to set
    // ContactViewModel.CanManage.
    public int ContactCount { get; set; }

    public bool IsFirst { get; set; }

    public bool IsLast { get; set; }
}
