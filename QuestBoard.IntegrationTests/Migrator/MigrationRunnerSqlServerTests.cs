using QuestBoard.Migrator;

namespace QuestBoard.IntegrationTests.Migrator;

/// <summary>
/// Proves against a real SQL Server what the DB-less tests cannot: that apply is all-or-nothing,
/// that unsafe batches are refused before any write, and that backup is copy-only.
/// </summary>
public class MigrationRunnerSqlServerTests
{
    [Fact]
    public void ApplyAtomically_WhenALaterMigrationFails_RollsBackSchemaAndHistory()
    {
        // Arrange
        var server = MigratorSqlServer.RequireServerConnectionString();
        using var db = new ScratchDatabase(server).CreateEmpty();
        using var context = new AtomicityProbeContext(db.ConnectionString);
        var runner = new MigrationRunner(context);

        // Act
        var act = () => runner.ApplyAtomically();

        // Assert
        var failure = act.Should().Throw<MigratorApplyException>().Which;
        failure.SqlErrorNumber.Should().Be(50000);
        MigratorSqlServer.TableExists(db.ConnectionString, "MigratorProbeAlpha").Should().BeFalse();
        MigratorSqlServer.TableExists(db.ConnectionString, "MigratorProbeBeta").Should().BeFalse();
        MigratorSqlServer.HistoryRowCount(db.ConnectionString).Should().Be(0);
    }

    [Fact]
    public void ApplyAtomically_WithHealthyMigrations_AppliesAllAndRecordsHistory()
    {
        // Arrange
        var server = MigratorSqlServer.RequireServerConnectionString();
        using var db = new ScratchDatabase(server).CreateEmpty();
        using var context = new HealthyProbeContext(db.ConnectionString);
        var runner = new MigrationRunner(context);

        // Act
        var applied = runner.ApplyAtomically();

        // Assert
        applied.Should().Equal("20000101000011_CreateProbeAlpha", "20000101000012_CreateProbeBeta");
        MigratorSqlServer.TableExists(db.ConnectionString, "MigratorProbeAlpha").Should().BeTrue();
        MigratorSqlServer.TableExists(db.ConnectionString, "MigratorProbeBeta").Should().BeTrue();
        MigratorSqlServer.HistoryIds(db.ConnectionString)
            .Should().Equal("20000101000011_CreateProbeAlpha", "20000101000012_CreateProbeBeta");
        runner.GetStatus().Pending.Should().BeEmpty();
    }

    [Fact]
    public void ApplyAtomically_WithNothingPending_ReturnsEmptyList()
    {
        // Arrange
        var server = MigratorSqlServer.RequireServerConnectionString();
        using var db = new ScratchDatabase(server).CreateEmpty();
        using var context = new HealthyProbeContext(db.ConnectionString);
        var runner = new MigrationRunner(context);
        runner.ApplyAtomically();

        // Act
        var applied = runner.ApplyAtomically();

        // Assert
        applied.Should().BeEmpty();
    }

    [Fact]
    public void ApplyAtomically_WithNonTransactionalPendingMigration_RefusesAndAppliesNothing()
    {
        // Arrange
        var server = MigratorSqlServer.RequireServerConnectionString();
        using var db = new ScratchDatabase(server).CreateEmpty();
        using var context = new NonTransactionalSqlProbeContext(db.ConnectionString);
        var runner = new MigrationRunner(context);

        // Act
        var act = () => runner.ApplyAtomically();

        // Assert
        act.Should().Throw<MigratorNonTransactionalException>()
            .Which.Migrations.Should().Equal("20000101000022_SuppressedTransaction");
        MigratorSqlServer.TableExists(db.ConnectionString, "MigratorProbeAlpha").Should().BeFalse();
        MigratorSqlServer.HistoryRowCount(db.ConnectionString).Should().Be(0);
    }

    [Fact]
    public void GetStatus_WithHistoryRowTheBuildDoesNotKnow_ReportsDatabaseAhead()
    {
        // Arrange
        var server = MigratorSqlServer.RequireServerConnectionString();
        using var db = new ScratchDatabase(server).CreateEmpty();
        using (var seed = new HealthyProbeContext(db.ConnectionString))
        {
            new MigrationRunner(seed).ApplyAtomically();
        }

        MigratorSqlServer.Execute(
            db.ConnectionString,
            "INSERT INTO [dbo].[__EFMigrationsHistory] ([MigrationId], [ProductVersion]) VALUES (N'29990101000000_FromTheFuture', N'10.0.0')");

        using var context = new HealthyProbeContext(db.ConnectionString);
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        // Act
        var status = new MigrationRunner(context).GetStatus();
        var exitCode = MigratorCli.Run(["status"], stdout, stderr, () => new HealthyProbeContext(db.ConnectionString));

        // Assert
        status.Unknown.Should().Contain("29990101000000_FromTheFuture");
        exitCode.Should().Be((int)MigratorExitCode.DatabaseAhead);
    }

    [Fact]
    public void ApplyAtomically_WhenDatabaseIsAhead_RefusesAndLeavesItUntouched()
    {
        // Arrange
        var server = MigratorSqlServer.RequireServerConnectionString();
        using var db = new ScratchDatabase(server).CreateEmpty();
        using (var seed = new HealthyProbeContext(db.ConnectionString))
        {
            new MigrationRunner(seed).ApplyAtomically();
        }

        MigratorSqlServer.Execute(
            db.ConnectionString,
            "INSERT INTO [dbo].[__EFMigrationsHistory] ([MigrationId], [ProductVersion]) VALUES (N'29990101000000_FromTheFuture', N'10.0.0')");
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        // Act
        var exitCode = MigratorCli.Run(["apply"], stdout, stderr, () => new HealthyProbeContext(db.ConnectionString));

        // Assert
        exitCode.Should().Be((int)MigratorExitCode.DatabaseAhead);
        stderr.ToString().Should().Contain("29990101000000_FromTheFuture");
        MigratorSqlServer.HistoryRowCount(db.ConnectionString).Should().Be(3);
    }

    [Fact]
    public void Backup_WritesACopyOnlyBackupWithTheDocumentedName()
    {
        // Arrange
        var server = MigratorSqlServer.RequireServerConnectionString();
        using var db = new ScratchDatabase(server).CreateEmpty();
        using var context = new HealthyProbeContext(db.ConnectionString);
        var runner = new MigrationRunner(context);

        // Act
        var name = runner.Backup("probe-label");

        // Assert
        name.Should().MatchRegex(@"^questboard-premigration-probe-label-\d{8}T\d{6}Z\.bak$");
        MigratorSqlServer.CopyOnlyBackupSetCount(server, name).Should().Be(1);
    }

    [Fact]
    public void BackupCommand_PrintsOnlyTheBackupFileName()
    {
        // Arrange
        var server = MigratorSqlServer.RequireServerConnectionString();
        using var db = new ScratchDatabase(server).CreateEmpty();
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        // Act
        var exitCode = MigratorCli.Run(
            ["backup", "--label", "cli-probe"],
            stdout,
            stderr,
            () => new HealthyProbeContext(db.ConnectionString));

        // Assert
        exitCode.Should().Be((int)MigratorExitCode.Ok);
        stdout.ToString().Should().MatchRegex(@"^\{""backupName"":""questboard-premigration-cli-probe-\d{8}T\d{6}Z\.bak""\}\s*$");
        stderr.ToString().Should().BeEmpty();
    }

    [Fact]
    public void Backup_WhenDatabaseDoesNotExist_ReturnsBackupFailed()
    {
        // Arrange
        var server = MigratorSqlServer.RequireServerConnectionString();
        using var db = new ScratchDatabase(server);
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        // Act
        var exitCode = MigratorCli.Run(
            ["backup", "--label", "missing"],
            stdout,
            stderr,
            () => new HealthyProbeContext(db.ConnectionString));

        // Assert
        exitCode.Should().Be((int)MigratorExitCode.BackupFailed);
        stdout.ToString().Should().BeEmpty();
    }

    [Fact]
    public void ApplyAtomically_WhenDatabaseDoesNotExistYet_CreatesItAndAppliesEverything()
    {
        // Arrange
        var server = MigratorSqlServer.RequireServerConnectionString();
        using var db = new ScratchDatabase(server);
        using var context = new HealthyProbeContext(db.ConnectionString);
        var runner = new MigrationRunner(context);
        runner.GetStatus().DatabaseExists.Should().BeFalse();

        // Act
        var applied = runner.ApplyAtomically();

        // Assert
        applied.Should().HaveCount(2);
        MigratorSqlServer.TableExists(db.ConnectionString, "MigratorProbeAlpha").Should().BeTrue();
        MigratorSqlServer.TableExists(db.ConnectionString, "MigratorProbeBeta").Should().BeTrue();
    }

    [Fact]
    public void GetStatus_ForTheServerLogin_ReportsCanBackup()
    {
        // Arrange
        var server = MigratorSqlServer.RequireServerConnectionString();
        using var db = new ScratchDatabase(server).CreateEmpty();
        using var context = new HealthyProbeContext(db.ConnectionString);

        // Act
        var status = new MigrationRunner(context).GetStatus();

        // Assert
        status.DatabaseExists.Should().BeTrue();
        status.CanBackup.Should().BeTrue();
    }
}
