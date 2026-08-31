namespace QuestBoard.Domain.Models;

// The result of a cross-board agenda read: the page's worth of rows plus whether more rows
// exist beyond this window.
public class CrossBoardAgenda
{
    public IReadOnlyList<AgendaRow> Rows { get; init; } = [];

    public bool HasMore { get; init; }
}
