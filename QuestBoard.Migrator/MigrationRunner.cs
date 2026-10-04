using System.Data.Common;
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
/// Inspects migrations for any <see cref="DbContext"/>. Kept generic over the context so the
/// logic can be exercised in tests with small hand-written contexts and no database.
/// </summary>
public sealed class MigrationRunner(DbContext context)
{
    // SQL Server refuses these inside a user transaction, or they change database-wide settings
    // that a rolled-back transaction cannot restore, so a migration using them cannot be applied atomically.
    private static readonly Regex ForbiddenText = new(
        @"ALTER\s+DATABASE|FULLTEXT|MEMORY_OPTIMIZED",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

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
