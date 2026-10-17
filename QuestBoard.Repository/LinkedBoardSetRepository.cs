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
            // Removing the still-pending row stops the tracker retrying it on the next save.
            dbContext.LinkedBoardSets.Remove(entity);

            // Only a name another set now holds is a duplicate that slipped past the pre-check.
            // Asking the database, rather than decoding a provider error number, keeps this true on
            // every provider; any other failure is real and must not be reported as a taken name.
            if (await NameExistsAsync(name, null, token)) return null;
            throw;
        }

        return entity.Id;
    }

    /// <inheritdoc/>
    public async Task<LinkedBoardSetResult> AddBoardAsync(int setId, int groupId, CancellationToken token = default)
    {
        var group = await dbContext.Groups.FirstOrDefaultAsync(g => g.Id == groupId, token);
        if (group == null) return LinkedBoardSetResult.NotFound;
        if (group.LinkedBoardSetId != null) return LinkedBoardSetResult.AlreadyLinked;

        group.LinkedBoardSetId = setId;
        try
        {
            await dbContext.SaveChangesAsync(token);
        }
        catch (DbUpdateConcurrencyException)
        {
            // The board joined a set, or was deleted, after it was read. The write was refused
            // rather than moving the board out of the set it joined.
            group.LinkedBoardSetId = null;
            return await BoardExistsAsync(groupId, token)
                ? LinkedBoardSetResult.AlreadyLinked
                : LinkedBoardSetResult.NotFound;
        }
        catch (DbUpdateException)
        {
            // The foreign key refuses a set that was deleted after the caller checked it.
            group.LinkedBoardSetId = null;
            if (!await SetExistsAsync(setId, token)) return LinkedBoardSetResult.NotFound;
            throw;
        }

        return LinkedBoardSetResult.Success;
    }

    /// <inheritdoc/>
    public async Task<LinkedBoardSetResult> RemoveBoardAsync(int setId, int groupId, CancellationToken token = default)
    {
        var group = await dbContext.Groups.FirstOrDefaultAsync(g => g.Id == groupId, token);
        if (group == null) return LinkedBoardSetResult.NotFound;
        if (group.LinkedBoardSetId != setId) return LinkedBoardSetResult.NotInSet;

        group.LinkedBoardSetId = null;
        try
        {
            await dbContext.SaveChangesAsync(token);
        }
        catch (DbUpdateConcurrencyException)
        {
            // The board left the set, or was deleted, after it was read.
            group.LinkedBoardSetId = setId;
            return await BoardExistsAsync(groupId, token)
                ? LinkedBoardSetResult.NotInSet
                : LinkedBoardSetResult.NotFound;
        }

        return LinkedBoardSetResult.Success;
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
                BoardType = (BoardType)g.BoardType
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
    public async Task<LinkedBoardSetResult> RenameAsync(int setId, string name, CancellationToken token = default)
    {
        var set = await dbContext.LinkedBoardSets.FirstOrDefaultAsync(s => s.Id == setId, token);
        if (set == null) return LinkedBoardSetResult.NotFound;

        // Putting the stored name back after a refused save keeps the tracker from retrying the
        // rejected one. It is kept from the read, so restoring it cannot fail when the row is gone.
        var storedName = set.Name;
        set.Name = name;
        try
        {
            await dbContext.SaveChangesAsync(token);
        }
        catch (DbUpdateConcurrencyException)
        {
            // The set was deleted after it was read, so there was no row left to rename.
            set.Name = storedName;
            return LinkedBoardSetResult.NotFound;
        }
        catch (DbUpdateException)
        {
            set.Name = storedName;

            // Only a name another set now holds is a duplicate that slipped past the pre-check.
            if (await NameExistsAsync(name, setId, token)) return LinkedBoardSetResult.DuplicateName;
            throw;
        }

        return LinkedBoardSetResult.Success;
    }

    /// <inheritdoc/>
    public async Task<bool> DeleteAsync(int setId, CancellationToken token = default)
    {
        var set = await dbContext.LinkedBoardSets.FirstOrDefaultAsync(s => s.Id == setId, token);
        if (set == null) return false;

        // On SQL Server the foreign key un-links every board in the same statement that deletes the
        // set, so no board row is written here and none can be refused because another super admin
        // moved it a moment earlier. The in-memory provider applies SetNull only to dependents it
        // already tracks, so there the boards are loaded and un-linked explicitly.
        if (!dbContext.Database.IsRelational())
        {
            var boards = await dbContext.Groups.Where(g => g.LinkedBoardSetId == setId).ToListAsync(token);
            foreach (var board in boards)
                board.LinkedBoardSetId = null;
        }

        dbContext.LinkedBoardSets.Remove(set);
        try
        {
            await dbContext.SaveChangesAsync(token);
        }
        catch (DbUpdateConcurrencyException)
        {
            // Another super admin deleted the set after it was read.
            return false;
        }

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

    private Task<bool> BoardExistsAsync(int groupId, CancellationToken token) =>
        dbContext.Groups.AnyAsync(g => g.Id == groupId, token);

    private Task<bool> SetExistsAsync(int setId, CancellationToken token) =>
        dbContext.LinkedBoardSets.AnyAsync(s => s.Id == setId, token);
}
