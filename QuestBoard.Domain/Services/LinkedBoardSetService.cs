using QuestBoard.Domain.Enums;
using QuestBoard.Domain.Interfaces;
using QuestBoard.Domain.Models;

namespace QuestBoard.Domain.Services;

internal class LinkedBoardSetService(ILinkedBoardSetRepository repository) : ILinkedBoardSetService
{
    private const int MaxNameLength = 100;

    /// <inheritdoc/>
    public async Task<IList<LinkedBoardSet>> GetAllAsync(CancellationToken token = default)
        => await repository.GetAllAsync(token);

    /// <inheritdoc/>
    public async Task<IList<LinkedBoardCandidate>> GetUnlinkedBoardsAsync(CancellationToken token = default)
        => await repository.GetUnlinkedBoardsAsync(token);

    /// <inheritdoc/>
    public async Task<LinkedBoardSetOutcome> CreateAsync(string name, BoardType boardType, CancellationToken token = default)
    {
        var trimmed = NormalizeName(name);
        if (trimmed == null)
        {
            return new LinkedBoardSetOutcome(LinkedBoardSetResult.InvalidName);
        }

        if (!Enum.IsDefined(boardType))
        {
            return new LinkedBoardSetOutcome(LinkedBoardSetResult.InvalidBoardType, trimmed);
        }

        if (await repository.NameExistsAsync(trimmed, null, token))
        {
            return new LinkedBoardSetOutcome(LinkedBoardSetResult.DuplicateName, trimmed);
        }

        // A null id means the unique index rejected the name after the pre-check, a concurrent create.
        var setId = await repository.CreateAsync(trimmed, boardType, token);
        return setId == null
            ? new LinkedBoardSetOutcome(LinkedBoardSetResult.DuplicateName, trimmed)
            : new LinkedBoardSetOutcome(LinkedBoardSetResult.Success, trimmed, SetBoardType: boardType);
    }

    /// <inheritdoc/>
    public async Task<LinkedBoardSetOutcome> AddBoardAsync(int setId, int groupId, CancellationToken token = default)
    {
        var set = await repository.GetByIdAsync(setId, token);
        var board = await repository.GetBoardAsync(groupId, token);
        if (set == null || board == null)
        {
            return new LinkedBoardSetOutcome(LinkedBoardSetResult.NotFound);
        }

        if (board.BoardType != set.BoardType)
        {
            return new LinkedBoardSetOutcome(
                LinkedBoardSetResult.TypeMismatch, set.Name, board.Name, board.BoardType, set.BoardType);
        }

        if (board.LinkedBoardSetId != null)
        {
            return new LinkedBoardSetOutcome(LinkedBoardSetResult.AlreadyLinked, set.Name, board.Name);
        }

        // The checks above give the clearest message; the write itself re-checks, because another
        // super admin can link the board or delete the set between the two.
        return await repository.AddBoardAsync(setId, groupId, token) switch
        {
            LinkedBoardSetResult.Success => new LinkedBoardSetOutcome(LinkedBoardSetResult.Success, set.Name, board.Name),
            LinkedBoardSetResult.AlreadyLinked => new LinkedBoardSetOutcome(LinkedBoardSetResult.AlreadyLinked, set.Name, board.Name),
            _ => new LinkedBoardSetOutcome(LinkedBoardSetResult.NotFound)
        };
    }

    /// <inheritdoc/>
    public async Task<LinkedBoardSetOutcome> RenameAsync(int setId, string name, CancellationToken token = default)
    {
        var trimmed = NormalizeName(name);
        if (trimmed == null)
        {
            return new LinkedBoardSetOutcome(LinkedBoardSetResult.InvalidName);
        }

        var set = await repository.GetByIdAsync(setId, token);
        if (set == null)
        {
            return new LinkedBoardSetOutcome(LinkedBoardSetResult.NotFound);
        }

        if (await repository.NameExistsAsync(trimmed, setId, token))
        {
            return new LinkedBoardSetOutcome(LinkedBoardSetResult.DuplicateName, trimmed);
        }

        // The write re-checks both the set and the name, for a delete or a rename that lands meanwhile.
        return await repository.RenameAsync(setId, trimmed, token) switch
        {
            LinkedBoardSetResult.Success => new LinkedBoardSetOutcome(LinkedBoardSetResult.Success, trimmed),
            LinkedBoardSetResult.DuplicateName => new LinkedBoardSetOutcome(LinkedBoardSetResult.DuplicateName, trimmed),
            _ => new LinkedBoardSetOutcome(LinkedBoardSetResult.NotFound)
        };
    }

    /// <inheritdoc/>
    public async Task<LinkedBoardSetOutcome> RemoveBoardAsync(int setId, int groupId, CancellationToken token = default)
    {
        var set = await repository.GetByIdAsync(setId, token);
        var board = await repository.GetBoardAsync(groupId, token);
        if (set == null || board == null)
        {
            return new LinkedBoardSetOutcome(LinkedBoardSetResult.NotFound);
        }

        if (board.LinkedBoardSetId != setId)
        {
            return new LinkedBoardSetOutcome(LinkedBoardSetResult.NotInSet, set.Name, board.Name);
        }

        return await repository.RemoveBoardAsync(setId, groupId, token) switch
        {
            LinkedBoardSetResult.Success => new LinkedBoardSetOutcome(LinkedBoardSetResult.Success, set.Name, board.Name),
            LinkedBoardSetResult.NotInSet => new LinkedBoardSetOutcome(LinkedBoardSetResult.NotInSet, set.Name, board.Name),
            _ => new LinkedBoardSetOutcome(LinkedBoardSetResult.NotFound)
        };
    }

    /// <inheritdoc/>
    public async Task<LinkedBoardSetOutcome> DeleteAsync(int setId, CancellationToken token = default)
    {
        var set = await repository.GetByIdAsync(setId, token);
        if (set == null)
        {
            return new LinkedBoardSetOutcome(LinkedBoardSetResult.NotFound);
        }

        return await repository.DeleteAsync(setId, token)
            ? new LinkedBoardSetOutcome(LinkedBoardSetResult.Success, set.Name)
            : new LinkedBoardSetOutcome(LinkedBoardSetResult.NotFound);
    }

    private static string? NormalizeName(string? name)
    {
        var trimmed = name?.Trim();
        return string.IsNullOrEmpty(trimmed) || trimmed.Length > MaxNameLength ? null : trimmed;
    }
}
