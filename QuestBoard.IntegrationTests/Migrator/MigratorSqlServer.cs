using Microsoft.Data.SqlClient;

namespace QuestBoard.IntegrationTests.Migrator;

/// <summary>
/// Access to a real SQL Server for the migrator tests. Migrations cannot run on the InMemory
/// provider, so these tests need a server; they skip when none is supplied and fail instead of
/// skipping when <see cref="RequiredVariable"/> is "1", so a CI job can never report a silent skip
/// as a pass.
///
/// Each backup test leaves one small .bak file in the server's default backup directory, which is
/// harmless in an ephemeral CI container.
/// </summary>
public static class MigratorSqlServer
{
    public const string ConnectionVariable = "QUESTBOARD_MIGRATOR_TEST_CONNECTION";
    public const string RequiredVariable = "QUESTBOARD_MIGRATOR_TEST_REQUIRED";

    private static readonly TimeSpan StartupWait = TimeSpan.FromSeconds(120);
    private static readonly TimeSpan RetryInterval = TimeSpan.FromSeconds(2);

    private static readonly object ReadyLock = new();
    private static bool serverReady;

    /// <summary>
    /// Returns a server-level (master) connection string, skipping or failing the calling test when
    /// none was supplied. A server that is still starting is waited for, which is how a CI service
    /// container is covered without a container health command.
    /// </summary>
    public static string RequireServerConnectionString()
    {
        var supplied = Environment.GetEnvironmentVariable(ConnectionVariable);

        if (string.IsNullOrWhiteSpace(supplied))
        {
            if (Environment.GetEnvironmentVariable(RequiredVariable) == "1")
            {
                Assert.Fail($"{ConnectionVariable} is not set but {RequiredVariable}=1 requires a SQL Server");
            }

            Assert.Skip($"No SQL Server supplied; set {ConnectionVariable} to run this test");
        }

        var builder = new SqlConnectionStringBuilder(supplied)
        {
            InitialCatalog = "master",
            ConnectTimeout = 5,
            ConnectRetryCount = 0
        };
        var connectionString = builder.ConnectionString;

        lock (ReadyLock)
        {
            if (!serverReady)
            {
                WaitForServer(connectionString);
                serverReady = true;
            }
        }

        return connectionString;
    }

    private static void WaitForServer(string connectionString)
    {
        var deadline = DateTime.UtcNow + StartupWait;
        Exception? last;

        do
        {
            try
            {
                using var connection = new SqlConnection(connectionString);
                connection.Open();
                return;
            }
            catch (SqlException ex)
            {
                last = ex;
                Thread.Sleep(RetryInterval);
            }
        }
        while (DateTime.UtcNow < deadline);

        Assert.Fail($"SQL Server did not accept connections within {StartupWait.TotalSeconds:0} seconds: {last?.Message}");
    }

    public static void Execute(string connectionString, string sql)
    {
        using var connection = new SqlConnection(connectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    public static T? Scalar<T>(string connectionString, string sql, params (string Name, object Value)[] parameters)
    {
        using var connection = new SqlConnection(connectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        foreach (var (name, value) in parameters)
        {
            command.Parameters.AddWithValue(name, value);
        }

        var result = command.ExecuteScalar();
        return result is null or DBNull ? default : (T)Convert.ChangeType(result, typeof(T));
    }

    public static bool TableExists(string scratchConnectionString, string table) =>
        Scalar<int>(scratchConnectionString, "SELECT CASE WHEN OBJECT_ID(@name, N'U') IS NULL THEN 0 ELSE 1 END", ("@name", $"dbo.{table}")) == 1;

    /// <summary>The number of recorded migrations, or 0 when the history table does not exist.</summary>
    public static int HistoryRowCount(string scratchConnectionString) =>
        TableExists(scratchConnectionString, "__EFMigrationsHistory")
            ? Scalar<int>(scratchConnectionString, "SELECT COUNT(*) FROM [dbo].[__EFMigrationsHistory]")
            : 0;

    public static IReadOnlyList<string> HistoryIds(string scratchConnectionString)
    {
        if (!TableExists(scratchConnectionString, "__EFMigrationsHistory"))
        {
            return [];
        }

        var ids = new List<string>();
        using var connection = new SqlConnection(scratchConnectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT [MigrationId] FROM [dbo].[__EFMigrationsHistory] ORDER BY [MigrationId]";
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            ids.Add(reader.GetString(0));
        }

        return ids;
    }

    /// <summary>Counts backup sets in msdb with the given name that were taken copy-only.</summary>
    public static int CopyOnlyBackupSetCount(string serverConnectionString, string backupName) =>
        Scalar<int>(
            serverConnectionString,
            "SELECT COUNT(*) FROM msdb.dbo.backupset WHERE name = @name AND is_copy_only = 1",
            ("@name", backupName));
}

/// <summary>
/// A uniquely named database that is dropped when the test is done. The name is derived from a
/// Guid, never from input, so building DDL from it is safe.
/// </summary>
public sealed class ScratchDatabase : IDisposable
{
    private readonly string serverConnectionString;

    public ScratchDatabase(string serverConnectionString)
    {
        this.serverConnectionString = serverConnectionString;
        Name = $"QuestBoardMigratorProbe_{Guid.NewGuid():N}";
        ConnectionString = new SqlConnectionStringBuilder(serverConnectionString)
        {
            InitialCatalog = Name
        }.ConnectionString;
    }

    public string Name { get; }

    public string ConnectionString { get; }

    public string ServerConnectionString => serverConnectionString;

    public ScratchDatabase CreateEmpty()
    {
        MigratorSqlServer.Execute(serverConnectionString, $"CREATE DATABASE [{Name}]");
        return this;
    }

    public void Dispose()
    {
        MigratorSqlServer.Execute(
            serverConnectionString,
            $"IF DB_ID(N'{Name}') IS NOT NULL BEGIN ALTER DATABASE [{Name}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{Name}]; END");
    }
}
