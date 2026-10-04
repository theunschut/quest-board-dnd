using Microsoft.EntityFrameworkCore;
using QuestBoard.Migrator;
using QuestBoard.Repository.Entities;

namespace QuestBoard.UnitTests.Migrator;

public class MigrationPreflightTests
{
    private static QuestBoardContext CreateShippedContext()
    {
        var options = new DbContextOptionsBuilder<QuestBoardContext>()
            .UseSqlServer("Server=unused;Database=unused")
            .Options;
        return new QuestBoardContext(options, new NullActiveGroupContext());
    }

    [Fact]
    public void ShippedMigrations_AreKnownWithoutADatabase()
    {
        // Arrange
        using var context = CreateShippedContext();

        // Act
        var known = context.Database.GetMigrations().ToList();

        // Assert
        known.Should().NotBeEmpty();
    }

    [Fact]
    public void ShippedMigrations_NoneNeedToRunOutsideATransaction()
    {
        // Arrange
        using var context = CreateShippedContext();

        // Act
        var flagged = new MigrationRunner(context).FindNonTransactionalMigrations();

        // Assert
        flagged.Should().BeEmpty();
    }

    [Fact]
    public void ShippedModel_HasNoPendingModelChanges()
    {
        // Arrange
        using var context = CreateShippedContext();

        // Act
        var pending = context.Database.HasPendingModelChanges();

        // Assert
        pending.Should().BeFalse("a model change without a migration would make the migrator's apply step throw");
    }

    [Fact]
    public void Migration_WithSuppressedTransaction_IsFlagged()
    {
        // Arrange
        using var context = new SuppressedTransactionProbeContext();

        // Act
        var flagged = new MigrationRunner(context).FindNonTransactionalMigrations();

        // Assert
        flagged.Should().Equal("20000101000001_SuppressedTransaction");
    }

    [Fact]
    public void Migrations_WithForbiddenStatementText_AreEachFlagged()
    {
        // Arrange
        using var context = new ForbiddenTextProbeContext();

        // Act
        var flagged = new MigrationRunner(context).FindNonTransactionalMigrations();

        // Assert
        flagged.Should().Equal(
            "20000101000002_AlterDatabaseText",
            "20000101000003_FulltextText",
            "20000101000004_MemoryOptimizedText");
    }

    [Fact]
    public void Migration_WithPlainCreateTable_IsNotFlagged()
    {
        // Arrange
        using var context = new PlainProbeContext();

        // Act
        var flagged = new MigrationRunner(context).FindNonTransactionalMigrations();

        // Assert
        flagged.Should().BeEmpty();
    }

    [Fact]
    public void ComposeStatus_MatchesMigrationIdsCaseInsensitively()
    {
        // Arrange
        string[] known = ["A", "B", "C"];
        string[] applied = ["A", "b"];

        // Act
        var status = MigrationRunner.ComposeStatus(true, known, applied, _ => [], canBackup: true);

        // Assert
        status.Pending.Should().Equal("C");
        status.Unknown.Should().BeEmpty();
        status.Applied.Should().Equal("A", "b");
    }

    [Fact]
    public void ComposeStatus_ReportsAppliedMigrationsThisBuildDoesNotKnow()
    {
        // Arrange
        string[] known = ["A", "B"];
        string[] applied = ["A", "Z"];

        // Act
        var status = MigrationRunner.ComposeStatus(true, known, applied, _ => [], canBackup: false);

        // Assert
        status.Unknown.Should().Equal("Z");
        status.Pending.Should().Equal("B");
    }

    [Fact]
    public void ComposeStatus_AsksForNonTransactionalOnlyAmongPendingMigrations()
    {
        // Arrange
        string[] known = ["A", "B"];
        string[] applied = ["A"];
        IEnumerable<string>? seen = null;

        // Act
        var status = MigrationRunner.ComposeStatus(true, known, applied, pending =>
        {
            seen = pending.ToList();
            return ["B"];
        }, canBackup: true);

        // Assert
        seen.Should().Equal("B");
        status.NonTransactional.Should().Equal("B");
    }
}
