using QuestBoard.Domain.Models;

namespace QuestBoard.Service.ViewModels.PlatformViewModels;

public class LinkedBoardsIndexViewModel
{
    public List<LinkedBoardSet> Sets { get; set; } = [];

    public List<LinkedBoardCandidate> UnlinkedBoards { get; set; } = [];

    public LinkedBoardSetCreateViewModel Create { get; set; } = new();
}
