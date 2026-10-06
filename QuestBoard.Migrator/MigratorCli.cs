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

            if (args.Length == 1 && args[0] == "apply")
            {
                using var context = contextFactory();
                return RunApply(context, stdout, stderr);
            }

            if (args.Length >= 1 && args[0] == "backup")
            {
                var label = args.Length == 3 && args[1] == "--label" ? args[2] : null;
                if (!MigrationRunner.IsValidBackupLabel(label))
                {
                    stderr.WriteLine("usage: QuestBoard.Migrator backup --label <label> (label: 1-64 of A-Z a-z 0-9 . _ -)");
                    return (int)MigratorExitCode.Error;
                }

                using var context = contextFactory();
                return RunBackup(context, label!, stdout, stderr);
            }

            stderr.WriteLine("usage: QuestBoard.Migrator status | backup --label <label> | apply");
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

    private static int RunBackup(DbContext context, string label, TextWriter stdout, TextWriter stderr)
    {
        try
        {
            var backupName = new MigrationRunner(context).Backup(label);
            stdout.WriteLine(JsonSerializer.Serialize(new { backupName }, JsonOptions));
            return (int)MigratorExitCode.Ok;
        }
        catch (MigratorBackupException ex)
        {
            // Statement-phase failure: the SQL error number and message come from executing
            // BACKUP, not from the connection, so they are safe to show.
            stderr.WriteLine(DescribeStatementFailure("backup failed", ex.SqlErrorNumber, ex.SqlErrorMessage ?? ex.Message));
            return (int)MigratorExitCode.BackupFailed;
        }
    }

    private static int RunApply(DbContext context, TextWriter stdout, TextWriter stderr)
    {
        try
        {
            var applied = new MigrationRunner(context).ApplyAtomically();
            stdout.WriteLine(JsonSerializer.Serialize(new { applied }, JsonOptions));
            return (int)MigratorExitCode.Ok;
        }
        catch (MigratorDatabaseAheadException ex)
        {
            stderr.WriteLine($"database is ahead of this build; unknown migrations: {string.Join(", ", ex.UnknownMigrations)}");
            return (int)MigratorExitCode.DatabaseAhead;
        }
        catch (MigratorNonTransactionalException ex)
        {
            stderr.WriteLine($"refusing to apply; non-transactional pending migrations: {string.Join(", ", ex.Migrations)}");
            return (int)MigratorExitCode.NonTransactionalPending;
        }
        catch (MigratorApplyException ex)
        {
            stderr.WriteLine(DescribeApplyFailure(ex));
            return (int)MigratorExitCode.ApplyFailedRolledBack;
        }
    }

    /// <summary>
    /// The one line printed for a failed apply. A SQL error shows its number and message, which
    /// come from executing the migration. Any other failure shows only the exception type name,
    /// because its message can carry connection details. A failure while committing says the
    /// outcome is unknown, so nobody reads "rolled back" as a promise the server did not make.
    /// </summary>
    public static string DescribeApplyFailure(MigratorApplyException ex)
    {
        var text = ex.CommitOutcomeUnknown
            ? "apply failed while committing; whether it was kept is unknown, check the database state"
            : "apply failed and was rolled back";

        if (ex.SqlErrorNumber is null && string.IsNullOrWhiteSpace(ex.SqlErrorMessage)
            && !string.IsNullOrWhiteSpace(ex.FailureTypeName))
        {
            return $"{text} ({ex.FailureTypeName})";
        }

        return DescribeStatementFailure(text, ex.SqlErrorNumber, ex.SqlErrorMessage);
    }

    private static string DescribeStatementFailure(string text, int? sqlErrorNumber, string? sqlErrorMessage)
    {
        var line = sqlErrorNumber is { } number ? $"{text} (sql error {number})" : text;
        return string.IsNullOrWhiteSpace(sqlErrorMessage) ? line : $"{line}: {sqlErrorMessage}";
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
