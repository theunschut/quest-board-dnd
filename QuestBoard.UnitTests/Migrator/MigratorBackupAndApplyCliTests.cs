using Microsoft.EntityFrameworkCore;
using QuestBoard.Migrator;
using QuestBoard.Repository.Entities;

namespace QuestBoard.UnitTests.Migrator;

public class MigratorBackupAndApplyCliTests
{
    private const string UnreachableConnectionString =
        "Server=tcp:127.0.0.1,1;Database=CliProbe;User Id=cli-probe-user;Password=cli-probe-secret;" +
        "Connect Timeout=2;ConnectRetryCount=0;TrustServerCertificate=true";

    private static DbContext CreateUnreachableContext()
    {
        var options = new DbContextOptionsBuilder<QuestBoardContext>()
            .UseSqlServer(UnreachableConnectionString)
            .Options;
        return new QuestBoardContext(options, new NullActiveGroupContext());
    }

    private static DbContext FactoryMustNotBeCalled() =>
        throw new InvalidOperationException("the context factory must not be called");

    [Theory]
    [InlineData("v5.4.0")]
    [InlineData("rehearsal")]
    [InlineData("a_b-c.d")]
    public void IsValidBackupLabel_AcceptsSafeLabels(string label)
    {
        // Arrange / Act
        var valid = MigrationRunner.IsValidBackupLabel(label);

        // Assert
        valid.Should().BeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("a b")]
    [InlineData("x;DROP")]
    [InlineData("../x")]
    [InlineData("a/b")]
    public void IsValidBackupLabel_RejectsUnsafeLabels(string? label)
    {
        // Arrange / Act
        var valid = MigrationRunner.IsValidBackupLabel(label);

        // Assert
        valid.Should().BeFalse();
    }

    [Fact]
    public void IsValidBackupLabel_RejectsLabelLongerThan64Characters()
    {
        // Arrange
        var label = new string('a', 65);

        // Act / Assert
        MigrationRunner.IsValidBackupLabel(label).Should().BeFalse();
        MigrationRunner.IsValidBackupLabel(new string('a', 64)).Should().BeTrue();
    }

    [Fact]
    public void BuildBackupFileName_UsesLabelAndUtcTimestamp()
    {
        // Arrange
        var utcNow = new DateTimeOffset(2026, 10, 5, 10, 15, 0, TimeSpan.Zero);

        // Act
        var name = MigrationRunner.BuildBackupFileName("v5.4.0", utcNow);

        // Assert
        name.Should().Be("questboard-premigration-v5.4.0-20261005T101500Z.bak");
    }

    [Fact]
    public void BuildBackupFileName_ConvertsAnOffsetInstantToUtc()
    {
        // Arrange
        var instant = new DateTimeOffset(2026, 10, 5, 12, 15, 0, TimeSpan.FromHours(2));

        // Act
        var name = MigrationRunner.BuildBackupFileName("x", instant);

        // Assert
        name.Should().Be("questboard-premigration-x-20261005T101500Z.bak");
    }

    [Fact]
    public void Backup_WithInvalidLabel_ReturnsErrorBeforeAnyConnection()
    {
        // Arrange
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        // Act
        var exitCode = MigratorCli.Run(["backup", "--label", "bad label"], stdout, stderr, FactoryMustNotBeCalled);

        // Assert
        exitCode.Should().Be((int)MigratorExitCode.Error);
        stderr.ToString().Should().Contain("usage");
        stdout.ToString().Should().BeEmpty();
    }

    [Fact]
    public void Backup_WithoutLabel_ReturnsErrorWithUsage()
    {
        // Arrange
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        // Act
        var exitCode = MigratorCli.Run(["backup"], stdout, stderr, FactoryMustNotBeCalled);

        // Assert
        exitCode.Should().Be((int)MigratorExitCode.Error);
        stderr.ToString().Should().Contain("usage");
        stderr.ToString().Should().Contain("--label");
    }

    [Theory]
    [InlineData("apply")]
    [InlineData("backup")]
    public void StateChangingCommand_WhenServerIsUnreachable_ReturnsCannotConnectWithoutLeakingConnectionDetails(string command)
    {
        // Arrange
        var stdout = new StringWriter();
        var stderr = new StringWriter();
        string[] args = command == "backup" ? ["backup", "--label", "ok"] : ["apply"];

        // Act
        var exitCode = MigratorCli.Run(args, stdout, stderr, CreateUnreachableContext);

        // Assert
        exitCode.Should().Be((int)MigratorExitCode.CannotConnect);
        var output = stdout + stderr.ToString();
        output.Should().NotContain("cli-probe-user");
        output.Should().NotContain("cli-probe-secret");
        output.Should().NotContain("127.0.0.1");
        output.Should().NotContain("CliProbe");
    }
}
