namespace QuestBoard.Domain.Interfaces;

/// <summary>
/// Provides the active group ID for the current request or execution context.
/// A null value means no board is selected. Every tenant-scoped query filter requires a
/// non-null value that exactly matches a row's group id, so a null value yields zero rows
/// rather than every board's rows. A background job that writes must set a real board id
/// before making any repository call.
/// </summary>
public interface IActiveGroupContext
{
    int? ActiveGroupId { get; }
}
