using System.Text.RegularExpressions;

namespace QuestBoard.UnitTests.Architecture;

// A move rewrites an entry's board, its signups and the people around it. If that were spread
// over more than one save, a failure part-way through would leave an entry on one board with the
// people of another. These read the production source so a later edit cannot quietly add a second
// save, bypass the board filter, or push the save up into a service.
public class BoardMoveRepositorySingleSaveTests
{
    private const string SaveCall = "SaveChangesAsync(";

    public static IEnumerable<object[]> MoveRepositoryPaths() =>
    [
        ["QuestBoard.Repository/QuestMoveRepository.cs"],
        ["QuestBoard.Repository/EventMoveRepository.cs"],
    ];

    public static IEnumerable<object[]> MoveServicePaths() =>
    [
        ["QuestBoard.Domain/Services/QuestMoveService.cs"],
        ["QuestBoard.Domain/Services/EventMoveService.cs"],
        ["QuestBoard.Domain/Services/MoveControlService.cs"],
    ];

    private static string ResolveRepoRelativePath(string relativePath)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        var normalizedRelativePath = relativePath.Replace('/', Path.DirectorySeparatorChar);

        while (dir != null)
        {
            var candidate = Path.Combine(dir.FullName, normalizedRelativePath);
            if (File.Exists(candidate))
                return candidate;

            dir = dir.Parent;
        }

        throw new FileNotFoundException(
            $"'{relativePath}' not found. Searched upward from '{AppContext.BaseDirectory}' for a repo " +
            "root containing that file.",
            relativePath);
    }

    // Removes every comment form the scanned files carry, so prose that mentions a save or a
    // filter bypass can never fail the guard and a real call can never hide inside a comment.
    private static string StripComments(string source)
    {
        var withoutRazorBlocks = Regex.Replace(source, @"@\*.*?\*@", string.Empty, RegexOptions.Singleline);
        var withoutBlockComments = Regex.Replace(withoutRazorBlocks, @"/\*.*?\*/", string.Empty, RegexOptions.Singleline);

        var lines = withoutBlockComments.Replace("\r\n", "\n").Split('\n');
        var kept = new List<string>(lines.Length);
        foreach (var line in lines)
        {
            if (line.TrimStart().StartsWith('*'))
                continue;

            var lineCommentIndex = line.IndexOf("//", StringComparison.Ordinal);
            kept.Add(lineCommentIndex >= 0 ? line[..lineCommentIndex] : line);
        }

        return string.Join('\n', kept);
    }

    private static int CountOccurrences(string haystack, string needle)
    {
        var count = 0;
        var index = 0;
        while ((index = haystack.IndexOf(needle, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += needle.Length;
        }

        return count;
    }

    [Theory]
    [MemberData(nameof(MoveRepositoryPaths))]
    public void MoveRepository_SavesExactlyOnce(string relativePath)
    {
        var stripped = StripComments(File.ReadAllText(ResolveRepoRelativePath(relativePath)));

        CountOccurrences(stripped, SaveCall).Should().Be(1,
            because: $"'{relativePath}' must apply a move as one unit of work: a second save would let a " +
                     "failure part-way through leave the entry on one board with the signups of the other");
    }

    [Theory]
    [MemberData(nameof(MoveRepositoryPaths))]
    public void MoveRepository_NeverBypassesTheBoardQueryFilter(string relativePath)
    {
        var stripped = StripComments(File.ReadAllText(ResolveRepoRelativePath(relativePath)));

        stripped.Should().NotContain("IgnoreQueryFilters",
            because: $"'{relativePath}' runs inside one board's scope; lifting the board filter would let a " +
                     "move read or rewrite rows that belong to a board the actor never opened");
    }

    [Theory]
    [MemberData(nameof(MoveServicePaths))]
    public void MoveService_NeverSavesItself(string relativePath)
    {
        var stripped = StripComments(File.ReadAllText(ResolveRepoRelativePath(relativePath)));

        stripped.Should().NotContain(SaveCall,
            because: $"'{relativePath}' decides what a move does and hands the plan to the repository, " +
                     "which is the only place allowed to save, so the whole move stays one unit of work");
    }

    [Fact]
    public void CrossBoardLinkRegistry_NamesNoMoveRoute()
    {
        var content = File.ReadAllText(ResolveRepoRelativePath("QuestBoard.Service/Helpers/CrossBoardLinkRegistry.cs"));

        content.Should().NotContain("MoveController",
            because: "the registry turns a link into a switch onto another board; a Move URL is an action " +
                     "taken on the board the actor is standing on and must never be resolved across boards");
    }
}
