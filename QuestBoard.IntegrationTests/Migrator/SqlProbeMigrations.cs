using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace QuestBoard.IntegrationTests.Migrator;

// Test fixtures only: contexts with empty models and hand-written migrations, so the migrator can
// be exercised against a real server without touching the application's own schema.

public abstract class SqlProbeContext(string connectionString) : DbContext
{
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder) =>
        optionsBuilder
            .UseSqlServer(connectionString)
            // The probes have no model snapshot, and the migrator runs migrations inside its own
            // transaction on purpose.
            .ConfigureWarnings(w => w
                .Ignore(RelationalEventId.MigrationsUserTransactionWarning)
                .Ignore(RelationalEventId.PendingModelChangesWarning));
}

public sealed class AtomicityProbeContext(string connectionString) : SqlProbeContext(connectionString);

public sealed class HealthyProbeContext(string connectionString) : SqlProbeContext(connectionString);

public sealed class NonTransactionalSqlProbeContext(string connectionString) : SqlProbeContext(connectionString);

internal static class ProbeTables
{
    public static void Create(MigrationBuilder migrationBuilder, string name) =>
        migrationBuilder.CreateTable(
            name: name,
            columns: table => new
            {
                Id = table.Column<int>(type: "int", nullable: false)
            },
            constraints: table => table.PrimaryKey($"PK_{name}", x => x.Id));
}

[DbContext(typeof(AtomicityProbeContext))]
[Migration("20000101000001_CreateProbeAlpha")]
public sealed class AtomicityAlphaMigration : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) =>
        ProbeTables.Create(migrationBuilder, "MigratorProbeAlpha");
}

[DbContext(typeof(AtomicityProbeContext))]
[Migration("20000101000002_CreateProbeBetaThenFail")]
public sealed class AtomicityBetaThenFailMigration : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        ProbeTables.Create(migrationBuilder, "MigratorProbeBeta");
        migrationBuilder.Sql("THROW 50000, N'deliberate migration failure', 1;");
    }
}

[DbContext(typeof(HealthyProbeContext))]
[Migration("20000101000011_CreateProbeAlpha")]
public sealed class HealthyAlphaMigration : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) =>
        ProbeTables.Create(migrationBuilder, "MigratorProbeAlpha");
}

[DbContext(typeof(HealthyProbeContext))]
[Migration("20000101000012_CreateProbeBeta")]
public sealed class HealthyBetaMigration : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) =>
        ProbeTables.Create(migrationBuilder, "MigratorProbeBeta");
}

[DbContext(typeof(NonTransactionalSqlProbeContext))]
[Migration("20000101000021_CreateProbeAlpha")]
public sealed class NonTransactionalAlphaMigration : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) =>
        ProbeTables.Create(migrationBuilder, "MigratorProbeAlpha");
}

[DbContext(typeof(NonTransactionalSqlProbeContext))]
[Migration("20000101000022_SuppressedTransaction")]
public sealed class NonTransactionalSuppressedMigration : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) =>
        migrationBuilder.Sql("SELECT 1", suppressTransaction: true);
}
