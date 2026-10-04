using Microsoft.EntityFrameworkCore;
using QuestBoard.Migrator;
using QuestBoard.Repository.Entities;

namespace QuestBoard.UnitTests.Migrator;

public class MigratorCliTests
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

    [Fact]
    public void Status_WhenServerIsUnreachable_ReturnsCannotConnectWithoutLeakingConnectionDetails()
    {
        // Arrange
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        // Act
        var exitCode = MigratorCli.Run(["status"], stdout, stderr, CreateUnreachableContext);

        // Assert
        exitCode.Should().Be((int)MigratorExitCode.CannotConnect);
        var output = stdout + stderr.ToString();
        output.Should().NotContain("cli-probe-user");
        output.Should().NotContain("cli-probe-secret");
        output.Should().NotContain("127.0.0.1");
        output.Should().NotContain("CliProbe");
    }

    [Fact]
    public void UnknownCommand_ReturnsErrorWithUsage()
    {
        // Arrange
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        // Act
        var exitCode = MigratorCli.Run(["frobnicate"], stdout, stderr, () => throw new InvalidOperationException("must not be called"));

        // Assert
        exitCode.Should().Be((int)MigratorExitCode.Error);
        stderr.ToString().Should().Contain("usage");
        stdout.ToString().Should().BeEmpty();
    }

    [Fact]
    public void MissingConnectionString_ReturnsErrorNamingOnlyTheVariable()
    {
        // Arrange
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        // Act
        var exitCode = MigratorCli.Run(
            ["status"],
            stdout,
            stderr,
            () => throw new MigratorConfigurationException($"environment variable {MigratorCli.ConnectionStringVariable} is not set"));

        // Assert
        exitCode.Should().Be((int)MigratorExitCode.Error);
        stderr.ToString().Should().Contain("ConnectionStrings__DefaultConnection");
        stdout.ToString().Should().BeEmpty();
    }

    [Fact]
    public void UnexpectedFailure_ReportsOnlyTheExceptionTypeName()
    {
        // Arrange
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        // Act
        var exitCode = MigratorCli.Run(
            ["status"],
            stdout,
            stderr,
            () => throw new InvalidOperationException("Server=secret-host;Password=hunter2"));

        // Assert
        exitCode.Should().Be((int)MigratorExitCode.Error);
        stderr.ToString().Should().Contain("InvalidOperationException");
        stderr.ToString().Should().NotContain("secret-host");
        stderr.ToString().Should().NotContain("hunter2");
    }
}
