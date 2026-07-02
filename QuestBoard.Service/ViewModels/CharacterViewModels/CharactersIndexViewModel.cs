namespace QuestBoard.Service.ViewModels.CharacterViewModels;

public class CharactersIndexViewModel
{
    public IList<CharacterViewModel> MyCharacters { get; set; } = [];
    public IList<CharacterViewModel> OtherCharacters { get; set; } = [];
    public int CurrentUserId { get; set; }
}
