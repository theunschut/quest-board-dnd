using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using QuestBoard.Domain.Interfaces;
using QuestBoard.Repository.Entities;

namespace QuestBoard.Migrator;

public enum MigratorExitCode
{
    Ok = 0,
    Error = 1,
    DatabaseAhead = 2,
    NonTransactionalPending = 3,
    CannotConnect = 4,
    BackupFailed = 5,
    ApplyFailedRolledBack = 6
}

/// <summary>
/// The migrator runs outside any request, so there is no active board; a null id is the
/// documented "no board selected" state and the migrator never reads tenant-scoped rows.
/// </summary>
public sealed class NullActiveGroupContext : IActiveGroupContext
{
    public int? ActiveGroupId => null;
}

public sealed class MigratorConfigurationException(string message) : Exception(message);

public static class MigratorCli
{
    public const string ConnectionStringVariable = "ConnectionStrings__DefaultConnection";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static DbContext CreateProductionContext()
    {
        var connectionString = Environment.GetEnvironmentVariable(ConnectionStringVariable);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new MigratorConfigurationException(
                $"environment variable {ConnectionStringVariable} is not set");
        }

        // Deliberately no retrying execution strategy: EF refuses to run work inside a
        // caller-owned transaction when one is configured, and the migrator needs that.
        var options = new DbContextOptionsBuilder<QuestBoardContext>()
            .UseSqlServer(connectionString)
            .ConfigureWarnings(w => w.Ignore(RelationalEventId.MigrationsUserTransactionWarning))
            .Options;

        return new QuestBoardContext(options, new NullActiveGroupContext());
    }

    public static int Run(string[] args, TextWriter stdout, TextWriter stderr, Func<DbContext> contextFactory)
    {
        try
        {
            if (args.Length == 1 && args[0] == "status")
            {
                using var context = contextFactory();
                return RunStatus(context, stdout);
            }

            stderr.WriteLine("usage: QuestBoard.Migrator status");
            return (int)MigratorExitCode.Error;
        }
        catch (MigratorConfigurationException ex)
        {
            stderr.WriteLine(ex.Message);
            return (int)MigratorExitCode.Error;
        }
        catch (MigratorConnectionException ex)
        {
            stderr.WriteLine(ex.SqlErrorNumber is { } number
                ? $"cannot connect to the database (sql error {number})"
                : "cannot connect to the database");
            return (int)MigratorExitCode.CannotConnect;
        }
        catch (Exception ex)
        {
            // Only the type name: exception messages can carry the server, login or connection string.
            stderr.WriteLine($"unexpected failure: {ex.GetType().Name}");
            return (int)MigratorExitCode.Error;
        }
    }

    private static int RunStatus(DbContext context, TextWriter stdout)
    {
        var status = new MigrationRunner(context).GetStatus();
        stdout.WriteLine(JsonSerializer.Serialize(status, JsonOptions));

        if (status.Unknown.Count > 0)
        {
            return (int)MigratorExitCode.DatabaseAhead;
        }

        return status.NonTransactional.Count > 0
            ? (int)MigratorExitCode.NonTransactionalPending
            : (int)MigratorExitCode.Ok;
    }
}
