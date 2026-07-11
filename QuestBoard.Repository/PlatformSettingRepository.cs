using AutoMapper;
using Microsoft.EntityFrameworkCore;
using QuestBoard.Domain.Interfaces;
using QuestBoard.Domain.Models;
using QuestBoard.Repository.Entities;

namespace QuestBoard.Repository;

internal class PlatformSettingRepository(QuestBoardContext dbContext, IMapper mapper)
    : BaseRepository<PlatformSetting, PlatformSettingEntity>(dbContext, mapper), IPlatformSettingRepository
{
    /// <inheritdoc/>
    public async Task<PlatformSetting?> GetForScopeAsync(string key, int? groupId, CancellationToken token = default)
    {
        var entity = groupId is int gid
            ? await DbSet.FirstOrDefaultAsync(s => s.Key == key && s.GroupId == gid, token)
            : await DbSet.FirstOrDefaultAsync(s => s.Key == key && s.GroupId == null, token);
        return entity == null ? null : Mapper.Map<PlatformSetting>(entity);
    }

    /// <inheritdoc/>
    public async Task<string?> GetCascadeValueAsync(string key, int? groupId, CancellationToken token = default)
    {
        if (groupId is int gid)
        {
            var groupRow = await DbSet.FirstOrDefaultAsync(s => s.Key == key && s.GroupId == gid, token);
            if (groupRow != null) return groupRow.Value;
        }

        var defaultRow = await DbSet.FirstOrDefaultAsync(s => s.Key == key && s.GroupId == null, token);
        return defaultRow?.Value;
    }

    /// <inheritdoc/>
    public async Task UpsertAsync(string key, string value, int? groupId, CancellationToken token = default)
    {
        var entity = groupId is int gid
            ? await DbSet.FirstOrDefaultAsync(s => s.Key == key && s.GroupId == gid, token)
            : await DbSet.FirstOrDefaultAsync(s => s.Key == key && s.GroupId == null, token);

        if (entity != null)
        {
            entity.Value = value;
        }
        else
        {
            DbSet.Add(new PlatformSettingEntity { Key = key, Value = value, GroupId = groupId });
        }

        await DbContext.SaveChangesAsync(token);
    }

    /// <inheritdoc/>
    public async Task<bool> HasAnyForScopeAsync(int? groupId, IEnumerable<string> keys, CancellationToken token = default)
    {
        return groupId is int gid
            ? await DbSet.AnyAsync(s => s.GroupId == gid && keys.Contains(s.Key), token)
            : await DbSet.AnyAsync(s => s.GroupId == null && keys.Contains(s.Key), token);
    }

    /// <inheritdoc/>
    public async Task ClearScopeAsync(int? groupId, IEnumerable<string> keys, CancellationToken token = default)
    {
        var entities = groupId is int gid
            ? await DbSet.Where(s => s.GroupId == gid && keys.Contains(s.Key)).ToListAsync(token)
            : await DbSet.Where(s => s.GroupId == null && keys.Contains(s.Key)).ToListAsync(token);

        if (entities.Count == 0) return;

        DbSet.RemoveRange(entities);
        await DbContext.SaveChangesAsync(token);
    }
}
