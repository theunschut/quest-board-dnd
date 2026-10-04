using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace QuestBoard.UnitTests.Migrator;

// Test fixtures only: tiny contexts with empty models and hand-written migrations, so the
// migrator's preflight can be exercised without a database and without touching the real model.

public abstract class ProbeContext(string connectionString) : DbContext
{
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder) =>
        optionsBuilder.UseSqlServer(connectionString);
}

public sealed class SuppressedTransactionProbeContext() : ProbeContext("Server=unused;Database=unused");

public sealed class ForbiddenTextProbeContext() : ProbeContext("Server=unused;Database=unused");

public sealed class PlainProbeContext() : ProbeContext("Server=unused;Database=unused");

[DbContext(typeof(SuppressedTransactionProbeContext))]
[Migration("20000101000001_SuppressedTransaction")]
public sealed class SuppressedTransactionMigration : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) =>
        migrationBuilder.Sql("SELECT 1", suppressTransaction: true);
}

[DbContext(typeof(ForbiddenTextProbeContext))]
[Migration("20000101000002_AlterDatabaseText")]
public sealed class AlterDatabaseTextMigration : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) =>
        migrationBuilder.Sql("ALTER DATABASE CURRENT SET RECOVERY SIMPLE");
}

[DbContext(typeof(ForbiddenTextProbeContext))]
[Migration("20000101000003_FulltextText")]
public sealed class FulltextTextMigration : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) =>
        migrationBuilder.Sql("CREATE FULLTEXT CATALOG ProbeCatalog");
}

[DbContext(typeof(ForbiddenTextProbeContext))]
[Migration("20000101000004_MemoryOptimizedText")]
public sealed class MemoryOptimizedTextMigration : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) =>
        migrationBuilder.Sql("CREATE TABLE dbo.Probe (Id int NOT NULL PRIMARY KEY NONCLUSTERED) WITH (MEMORY_OPTIMIZED = ON)");
}

[DbContext(typeof(PlainProbeContext))]
[Migration("20000101000005_PlainCreateTable")]
public sealed class PlainCreateTableMigration : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) =>
        migrationBuilder.CreateTable(
            name: "Probe",
            columns: table => new
            {
                Id = table.Column<int>(type: "int", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_Probe", x => x.Id));
}
