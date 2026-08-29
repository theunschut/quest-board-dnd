namespace QuestBoard.Service.ViewModels.AgendaViewModels;

// One entry in the board filter checklist.
public class AgendaBoardOptionViewModel
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public bool IsSelected { get; set; }
}
