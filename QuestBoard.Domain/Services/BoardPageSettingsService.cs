using QuestBoard.Domain.Interfaces;
using QuestBoard.Domain.Models;

namespace QuestBoard.Domain.Services;

internal class BoardPageSettingsService(IEventRepository eventRepository, IBoardClock boardClock) : IBoardPageSettingsService
{
    /// <inheritdoc/>
    public Task<BoardPageSettingsOutcome> EvaluateAsync(Group group, BoardPageSettingsRequest request, CancellationToken token = default) =>
        throw new NotImplementedException();
}
