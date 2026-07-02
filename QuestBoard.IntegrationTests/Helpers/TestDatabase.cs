namespace QuestBoard.IntegrationTests.Helpers;

public class TestDatabase : IDisposable
{
    public string DatabaseName { get; }
    private readonly DbContextOptions<QuestBoardContext> _options;

    public TestDatabase(string databaseName)
    {
        DatabaseName = databaseName;

        _options = new DbContextOptionsBuilder<QuestBoardContext>()
            .UseInMemoryDatabase(DatabaseName)
            .EnableSensitiveDataLogging()
            .Options;

        using var context = CreateContext();
        context.Database.EnsureCreated();
    }

    public QuestBoardContext CreateContext()
    {
        // null = see all records; TestDatabase seeds outside DI
        return new QuestBoardContext(_options, new MutableGroupContext { ActiveGroupId = null });
    }

    public void Reset()
    {
        try
        {
            using var context = CreateContext();
            context.Database.EnsureDeleted();
            context.Database.EnsureCreated();
        }
        catch
        {
            // Ignore errors
        }
    }

    public void Dispose() { }
}
