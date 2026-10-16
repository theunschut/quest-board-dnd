using Microsoft.EntityFrameworkCore;
using QuestBoard.Domain.Enums;
using QuestBoard.Domain.Interfaces;
using QuestBoard.Domain.Models;
using QuestBoard.Repository.Entities;

namespace QuestBoard.Repository;

// Groups and LinkedBoardSets carry no tenant query filter, so nothing here needs to bypass one.
// Reads project straight to domain models and writes load a tracked row, change it and save
// through the change tracker.
internal sealed class LinkedBoardSetRepository(QuestBoardContext dbContext) : ILinkedBoardSetRepository
{
    /// <inheritdoc/>
    public async Task<IList<LinkedBoardSet>> GetAllAsync(CancellationToken token = default)
    {
        return await dbContext.LinkedBoardSets
            .AsNoTracking()
            .OrderBy(s => s.Name)
            .Select(s => new LinkedBoardSet
            {
                Id = s.Id,
                Name = s.Name,
                BoardType = (BoardType)s.BoardType,
                CreatedAt = s.CreatedAt,
                Boards = s.Boards
                    .OrderBy(b => b.Name)
                    .Select(b => new LinkedBoardSetBoard { Id = b.Id, Name = b.Name })
                    .ToList()
            })
            .ToListAsync(token);
    }

    /// <inheritdoc/>
    public async Task<LinkedBoardSet?> GetByIdAsync(int setId, CancellationToken token = default)
    {
        return await dbContext.LinkedBoardSets
            .AsNoTracking()
            .Where(s => s.Id == setId)
            .Select(s => new LinkedBoardSet
            {
                Id = s.Id,
                Name = s.Name,
                BoardType = (BoardType)s.BoardType,
                CreatedAt = s.CreatedAt,
                Boards = s.Boards
                    .OrderBy(b => b.Name)
                    .Select(b => new LinkedBoardSetBoard { Id = b.Id, Name = b.Name })
                    .ToList()
            })
            .FirstOrDefaultAsync(token);
    }

    /// <inheritdoc/>
    public async Task<int?> CreateAsync(string name, BoardType boardType, CancellationToken token = default)
    {
        var entity = new LinkedBoardSetEntity
        {
            Name = name,
            BoardType = (int)boardType,
            CreatedAt = DateTime.UtcNow
        };

        dbContext.LinkedBoardSets.Add(entity);
        try
        {
            await dbContext.SaveChangesAsync(token);
        }
        catch (DbUpdateException)
        {
            // The unique index on the name rejected a duplicate that slipped past the pre-check.
            // Removing the still-pending row stops the tracker retrying it on the next save.
            dbContext.LinkedBoardSets.Remove(entity);
            return null;
        }

        return entity.Id;
    }

    /// <inheritdoc/>
    public async Task<bool> AssignBoardAsync(int groupId, int? setId, CancellationToken token = default)
    {
        var group = await dbContext.Groups.FirstOrDefaultAsync(g => g.Id == groupId, token);
        if (group == null) return false;

        group.LinkedBoardSetId = setId;
        await dbContext.SaveChangesAsync(token);
        return true;
    }

    /// <inheritdoc/>
    public async Task<IList<LinkedBoardCandidate>> GetUnlinkedBoardsAsync(CancellationToken token = default)
    {
        return await dbContext.Groups
            .AsNoTracking()
            .Where(g => g.LinkedBoardSetId == null)
            .OrderBy(g => g.Name)
            .Select(g => new LinkedBoardCandidate
            {
                Id = g.Id,
                Name = g.Name,
                BoardType = (BoardType)g.BoardType,
                LinkedBoardSetId = g.LinkedBoardSetId
            })
            .ToListAsync(token);
    }

    /// <inheritdoc/>
    public async Task<LinkedBoardCandidate?> GetBoardAsync(int groupId, CancellationToken token = default)
    {
        return await dbContext.Groups
            .AsNoTracking()
            .Where(g => g.Id == groupId)
            .Select(g => new LinkedBoardCandidate
            {
                Id = g.Id,
                Name = g.Name,
                BoardType = (BoardType)g.BoardType,
                LinkedBoardSetId = g.LinkedBoardSetId
            })
            .FirstOrDefaultAsync(token);
    }

    /// <inheritdoc/>
    public async Task<bool> NameExistsAsync(string name, int? excludingSetId, CancellationToken token = default)
    {
        var upperName = name.ToUpper();
        return await dbContext.LinkedBoardSets
            .AnyAsync(s => s.Name.ToUpper() == upperName && (excludingSetId == null || s.Id != excludingSetId), token);
    }

    /// <inheritdoc/>
    public async Task<bool> RenameAsync(int setId, string name, CancellationToken token = default)
    {
        var set = await dbContext.LinkedBoardSets.FirstOrDefaultAsync(s => s.Id == setId, token);
        if (set == null) return false;

        set.Name = name;
        try
        {
            await dbContext.SaveChangesAsync(token);
        }
        catch (DbUpdateException)
        {
            // The unique index on the name rejected a duplicate that slipped past the pre-check.
            // Putting the stored name back keeps the tracker from retrying the rejected one.
            set.Name = await dbContext.LinkedBoardSets
                .AsNoTracking()
                .Where(s => s.Id == setId)
                .Select(s => s.Name)
                .FirstAsync(token);
            return false;
        }

        return true;
    }

    /// <inheritdoc/>
    public async Task<bool> DeleteAsync(int setId, CancellationToken token = default)
    {
        var set = await dbContext.LinkedBoardSets
            .Include(s => s.Boards)
            .FirstOrDefaultAsync(s => s.Id == setId, token);
        if (set == null) return false;

        // The foreign key un-links boards on SQL Server, but the in-memory provider only does so
        // for dependents it already tracks, so the boards are un-linked explicitly as well.
        foreach (var board in set.Boards)
            board.LinkedBoardSetId = null;

        dbContext.LinkedBoardSets.Remove(set);
        await dbContext.SaveChangesAsync(token);
        return true;
    }

    /// <inheritdoc/>
    public async Task<ViewerLinkedBoards> GetLinkedBoardsForViewerAsync(int activeGroupId, int viewerUserId, CancellationToken token = default)
    {
        var active = await dbContext.Groups
            .AsNoTracking()
            .Where(g => g.Id == activeGroupId)
            .Select(g => new { g.LinkedBoardSetId, g.BoardType })
            .FirstOrDefaultAsync(token);
        if (active?.LinkedBoardSetId == null) return ViewerLinkedBoards.Empty;

        var setId = active.LinkedBoardSetId.Value;
        var setType = await dbContext.LinkedBoardSets
            .AsNoTracking()
            .Where(s => s.Id == setId)
            .Select(s => (int?)s.BoardType)
            .FirstOrDefaultAsync(token);
        if (setType == null || setType.Value != active.BoardType) return ViewerLinkedBoards.Empty;

        var type = setType.Value;

        // Membership here is the same UserGroups row the deep-link resolver checks, which is why
        // a link that is rendered always works when followed.
        var rows = await dbContext.Groups
            .AsNoTracking()
            .Where(g => g.LinkedBoardSetId == setId && g.Id != activeGroupId && g.BoardType == type)
            .OrderBy(g => g.Name)
            .Select(g => new
            {
                g.Id,
                g.Name,
                Role = g.UserGroups
                    .Where(ug => ug.UserId == viewerUserId)
                    .Select(ug => (int?)ug.GroupRole)
                    .FirstOrDefault()
            })
            .ToListAsync(token);

        return new ViewerLinkedBoards
        {
            SetBoardType = (BoardType)type,
            Boards = rows
                .Select(r => new LinkedBoard
                {
                    Id = r.Id,
                    Name = r.Name,
                    ViewerRole = r.Role == null ? null : (GroupRole)r.Role.Value
                })
                .ToList()
        };
    }
}
