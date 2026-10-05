using System.Data.Common;
using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage;

namespace QuestBoard.Migrator;

/// <summary>
/// What the migrator knows about a database relative to the migrations compiled into this build.
/// </summary>
/// <param name="DatabaseExists">False on a fresh host where the database has not been created yet.</param>
/// <param name="Applied">Migration ids recorded in the database's history table.</param>
/// <param name="Pending">Migration ids this build ships that the database has not applied.</param>
/// <param name="Unknown">Migration ids the database holds that this build does not ship (the database is ahead).</param>
/// <param name="NonTransactional">Pending migrations that cannot safely run inside a single transaction.</param>
/// <param name="CanBackup">Whether the connected login may run BACKUP DATABASE.</param>
public sealed record MigrationStatus(
    bool DatabaseExists,
    IReadOnlyList<string> Applied,
    IReadOnlyList<string> Pending,
    IReadOnlyList<string> Unknown,
    IReadOnlyList<string> NonTransactional,
    bool CanBackup);

/// <summary>
/// The database could not be reached. The message is fixed on purpose: nothing from the
/// underlying exception (which can carry the server name or login) may travel further.
/// </summary>
public sealed class MigratorConnectionException : Exception
{
    public MigratorConnectionException(int? sqlErrorNumber = null)
        : base("cannot connect to the database")
    {
        SqlErrorNumber = sqlErrorNumber;
    }

    public int? SqlErrorNumber { get; }
}

/// <summary>
/// The database holds migrations this build does not ship, so applying would run an older
/// schema over newer data. Carries migration ids only.
/// </summary>
public sealed class MigratorDatabaseAheadException(IReadOnlyList<string> unknownMigrations)
    : Exception("the database holds migrations this build does not know")
{
    public IReadOnlyList<string> UnknownMigrations { get; } = unknownMigrations;
}

/// <summary>
/// A pending migration cannot run inside a single transaction, so the batch is refused before
/// anything is written. Carries migration ids only.
/// </summary>
public sealed class MigratorNonTransactionalException(IReadOnlyList<string> migrations)
    : Exception("pending migrations cannot be applied atomically")
{
    public IReadOnlyList<string> Migrations { get; } = migrations;
}

/// <summary>The pre-migration backup could not be taken.</summary>
public sealed class MigratorBackupException : Exception
{
    public MigratorBackupException(string message, int? sqlErrorNumber = null, string? sqlErrorMessage = null)
        : base(message)
    {
        SqlErrorNumber = sqlErrorNumber;
        SqlErrorMessage = sqlErrorMessage;
    }

    public int? SqlErrorNumber { get; }

    public string? SqlErrorMessage { get; }
}

/// <summary>
/// Applying migrations failed. Before the commit the transaction is rolled back, so nothing
/// changed. When the failure came while committing, whether the server kept the batch is not
/// known, which <see cref="CommitOutcomeUnknown"/> records.
/// </summary>
public sealed class MigratorApplyException : Exception
{
    public MigratorApplyException(
        int? sqlErrorNumber = null,
        string? sqlErrorMessage = null,
        string? failureTypeName = null,
        bool commitOutcomeUnknown = false)
        : base(commitOutcomeUnknown
            ? "applying migrations failed while committing, so the outcome is unknown"
            : "applying migrations failed and was rolled back")
    {
        SqlErrorNumber = sqlErrorNumber;
        SqlErrorMessage = sqlErrorMessage;
        FailureTypeName = failureTypeName;
        CommitOutcomeUnknown = commitOutcomeUnknown;
    }

    public int? SqlErrorNumber { get; }

    public string? SqlErrorMessage { get; }

    /// <summary>
    /// The type name of a failure that was not a SQL error. Only the type: an exception message
    /// from outside the SQL statement can carry the server, login or connection string.
    /// </summary>
    public string? FailureTypeName { get; }

    /// <summary>True when the failure came while committing, so the batch may have been kept.</summary>
    public bool CommitOutcomeUnknown { get; }
}

/// <summary>
/// Inspects migrations for any <see cref="DbContext"/>. Kept generic over the context so the
/// logic can be exercised in tests with small hand-written contexts and no database.
/// </summary>
public sealed class MigrationRunner(DbContext context, TimeProvider? timeProvider = null)
{
    // SQL Server refuses these inside a user transaction, or they change database-wide settings
    // that a rolled-back transaction cannot restore, so a migration using them cannot be applied atomically.
    private static readonly Regex ForbiddenText = new(
        @"ALTER\s+DATABASE|FULLTEXT|MEMORY_OPTIMIZED",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex BackupLabelPattern = new(
        @"^[A-Za-z0-9._-]{1,64}$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly TimeSpan BackupCommandTimeout = TimeSpan.FromMinutes(30);

    private static readonly TimeSpan ApplyCommandTimeout = TimeSpan.FromMinutes(10);

    private readonly TimeProvider clock = timeProvider ?? TimeProvider.System;

    public static bool IsValidBackupLabel(string? label) =>
        label is not null && BackupLabelPattern.IsMatch(label);

    public static string BuildBackupFileName(string label, DateTimeOffset utcNow) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"questboard-premigration-{label}-{utcNow.UtcDateTime:yyyyMMdd'T'HHmmss'Z'}.bak");

    /// <summary>
    /// Takes a copy-only backup so the regular backup chain is not disturbed. BACKUP cannot run
    /// inside a transaction, so this never opens one. The file name is bare, which makes SQL
    /// Server write it to the instance's default backup directory.
    /// </summary>
    public string Backup(string label)
    {
        if (!IsValidBackupLabel(label))
        {
            throw new ArgumentException("backup label must match ^[A-Za-z0-9._-]{1,64}$", nameof(label));
        }

        try
        {
            // The existence check talks to master, so it also works when the target database is missing.
            if (!context.GetService<IRelationalDatabaseCreator>().Exists())
            {
                throw new MigratorBackupException("the database does not exist, so there is nothing to back up");
            }

            context.Database.OpenConnection();
        }
        catch (DbException ex)
        {
            throw new MigratorConnectionException((ex as SqlException)?.Number);
        }

        try
        {
            context.Database.SetCommandTimeout(BackupCommandTimeout);

            var databaseName = context.Database.GetDbConnection().Database;
            var fileName = BuildBackupFileName(label, clock.GetUtcNow());

            // Interpolation turns every value into a SQL parameter; BACKUP accepts variables for
            // the database, the device and the set name.
            context.Database.ExecuteSql(
                $"BACKUP DATABASE {databaseName} TO DISK = {fileName} WITH COPY_ONLY, CHECKSUM, INIT, NAME = {fileName}");

            return fileName;
        }
        catch (DbException ex)
        {
            throw new MigratorBackupException("the backup failed", (ex as SqlException)?.Number, ex.Message);
        }
        finally
        {
            context.Database.CloseConnection();
        }
    }

    /// <summary>
    /// Applies every pending migration or none. EF Core commits after each migration unless the
    /// caller owns the transaction, so the runner opens one around the whole batch; owning it
    /// also skips EF's own migration lock, which is safe only because the app is stopped and the
    /// installer holds its own lock. Refusals happen before anything is written.
    /// </summary>
    public IReadOnlyList<string> ApplyAtomically()
    {
        var status = GetStatus();

        if (status.Unknown.Count > 0)
        {
            throw new MigratorDatabaseAheadException(status.Unknown);
        }

        if (status.NonTransactional.Count > 0)
        {
            throw new MigratorNonTransactionalException(status.NonTransactional);
        }

        if (status.Pending.Count == 0)
        {
            return [];
        }

        if (!status.DatabaseExists)
        {
            try
            {
                // CREATE DATABASE cannot run inside a transaction. If the batch then fails, a fresh
                // host is left with an empty database, which the previous release can still start on.
                context.GetService<IRelationalDatabaseCreator>().Create();
            }
            catch (DbException ex)
            {
                throw new MigratorApplyException((ex as SqlException)?.Number, ex.Message);
            }
        }

        try
        {
            context.Database.OpenConnection();
        }
        catch (DbException ex)
        {
            throw new MigratorConnectionException((ex as SqlException)?.Number);
        }

        var committing = false;
        try
        {
            context.Database.SetCommandTimeout(ApplyCommandTimeout);

            using var transaction = context.Database.BeginTransaction();
            context.Database.Migrate();
            committing = true;
            transaction.Commit();
        }
        catch (Exception ex)
        {
            // Leaving the using block without Commit disposes the transaction, which rolls back
            // the schema changes and the history rows together. A failure of Commit itself is
            // different: the server may have applied the batch even though the acknowledgement
            // was lost, so the caller is told the outcome is unknown rather than rolled back.
            var sql = ex as SqlException ?? ex.InnerException as SqlException;
            throw new MigratorApplyException(
                sql?.Number,
                sql?.Message,
                sql is null ? ex.GetType().Name : null,
                committing);
        }
        finally
        {
            context.Database.CloseConnection();
        }

        return status.Pending;
    }

    public MigrationStatus GetStatus()
    {
        var known = context.Database.GetMigrations().ToList();

        bool exists;
        IReadOnlyList<string> applied = [];
        var canBackup = false;

        try
        {
            exists = context.GetService<IRelationalDatabaseCreator>().Exists();

            if (exists)
            {
                applied = context.Database.GetAppliedMigrations().ToList();
                canBackup = context.Database
                    .SqlQueryRaw<int>("SELECT ISNULL(HAS_PERMS_BY_NAME(DB_NAME(), 'DATABASE', 'BACKUP DATABASE'), 0) AS [Value]")
                    .ToList()
                    .SingleOrDefault() == 1;
            }
        }
        catch (DbException ex)
        {
            throw new MigratorConnectionException((ex as SqlException)?.Number);
        }

        return ComposeStatus(exists, known, applied, FindNonTransactionalMigrations, canBackup);
    }

    public static MigrationStatus ComposeStatus(
        bool databaseExists,
        IEnumerable<string> known,
        IEnumerable<string> applied,
        Func<IEnumerable<string>, IReadOnlyList<string>> nonTransactionalOf,
        bool canBackup)
    {
        var knownList = known.ToList();
        var appliedList = applied.ToList();

        // EF compares history ids case-insensitively, so the diff must as well.
        var unknown = appliedList.Except(knownList, StringComparer.OrdinalIgnoreCase)
            .OrderBy(id => id, StringComparer.Ordinal).ToList();
        var pending = knownList.Except(appliedList, StringComparer.OrdinalIgnoreCase)
            .OrderBy(id => id, StringComparer.Ordinal).ToList();

        return new MigrationStatus(
            databaseExists,
            appliedList.OrderBy(id => id, StringComparer.Ordinal).ToList(),
            pending,
            unknown,
            nonTransactionalOf(pending),
            canBackup);
    }

    public IReadOnlyList<string> FindNonTransactionalMigrations() =>
        FindNonTransactionalMigrations(context.Database.GetMigrations());

    /// <summary>
    /// Generates each migration's SQL without a database and flags the ones that cannot run in a
    /// transaction. The SQL generator is the source of truth; the text scan is a second net for
    /// raw SQL that the generator passes through untouched.
    /// </summary>
    public IReadOnlyList<string> FindNonTransactionalMigrations(IEnumerable<string> migrationIds)
    {
        var assembly = context.GetService<IMigrationsAssembly>();
        var generator = context.GetService<IMigrationsSqlGenerator>();
        var initializer = context.GetService<IModelRuntimeInitializer>();
        var providerName = context.Database.ProviderName!;

        var flagged = new List<string>();

        foreach (var id in migrationIds.OrderBy(id => id, StringComparer.Ordinal))
        {
            if (!assembly.Migrations.TryGetValue(id, out var type))
            {
                continue;
            }

            var migration = assembly.CreateMigration(type, providerName);
            IModel? model = migration.TargetModel is null ? null : initializer.Initialize(migration.TargetModel);

            var commands = generator.Generate(migration.UpOperations, model);
            if (commands.Any(c => c.TransactionSuppressed || ForbiddenText.IsMatch(c.CommandText)))
            {
                flagged.Add(id);
            }
        }

        return flagged;
    }
}
