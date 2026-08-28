using Microsoft.AspNetCore.Http;
using QuestBoard.Domain.Interfaces;
using QuestBoard.Service.Constants;

namespace QuestBoard.Service.Services;

/// <summary>
/// Reads ActiveGroupId from ASP.NET Core Session for HTTP requests.
/// In Hangfire background threads (no HttpContext), returns null or the override set via SetGroupId.
/// A null value means no board is selected: every tenant-scoped query filter requires a
/// non-null value that exactly matches a row's group id, so a null value yields zero rows
/// rather than every board's rows. A background job must call SetGroupId with a real board id
/// before making any repository call.
/// </summary>
public class ActiveGroupContextService(IHttpContextAccessor httpContextAccessor) : IActiveGroupContext
{
    private int? _overriddenGroupId;
    private bool _groupIdOverridden;

    /// <summary>
    /// Returns the overridden group ID (set by Hangfire jobs via SetGroupId),
    /// or reads from Session for normal HTTP requests.
    /// Returns null when no override is set and HttpContext is absent. A null value yields
    /// zero rows from every tenant-scoped query filter, not every board's rows.
    /// </summary>
    public int? ActiveGroupId =>
        _groupIdOverridden
            ? _overriddenGroupId
            : httpContextAccessor.HttpContext?.Session?.GetInt32(SessionKeys.ActiveGroupId);

    /// <summary>
    /// Called by Hangfire jobs to set the group context before any repository call.
    /// HTTP context is null in background threads; this provides groupId explicitly.
    /// The override takes precedence over Session.
    /// </summary>
    public void SetGroupId(int? groupId)
    {
        _groupIdOverridden = true;
        _overriddenGroupId = groupId;
    }
}
