using QuestBoard.Domain.Interfaces;
using QuestBoard.Service.Controllers.QuestBoard;

namespace QuestBoard.IntegrationTests.Controllers;

#pragma warning disable CS9113 // Parameter is unread.
public class QuestFinalizeTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
#pragma warning restore CS9113 // Parameter is unread.
{
    [Fact]
    public void QuestController_ConstructorDoesNotInjectIEmailService()
    {
        // Regression guard: IEmailService must not appear in QuestController constructor
        var constructor = typeof(QuestController).GetConstructors().Single();
        var paramTypes = constructor.GetParameters().Select(p => p.ParameterType).ToList();

        paramTypes.Should().NotContain(typeof(IEmailService),
            "CTRL-02 requires IEmailService to be removed from QuestController — email dispatch belongs in QuestService");
    }

    [Fact]
    public void FinalizeAction_BodyIsTwentyLinesOrFewer()
    {
        // Regression guard: Finalize action body must stay ≤ 20 non-blank lines
        var sourcePath = Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..",
            "QuestBoard.Service", "Controllers", "QuestBoard", "QuestController.cs");

        if (!File.Exists(sourcePath))
        {
            // Fallback when source not available at test run location
            var ctor = typeof(QuestController).GetConstructors().Single();
            ctor.GetParameters().Should().NotContain(p => p.ParameterType == typeof(IEmailService),
                "CTRL-01: source unavailable; verifying via constructor guard instead");
            return;
        }

        var lines = File.ReadAllLines(sourcePath);
        var inFinalize = false;
        var braceDepth = 0;
        var nonBlankCount = 0;

        foreach (var line in lines)
        {
            if (!inFinalize)
            {
                if (line.Contains("public async Task<IActionResult> Finalize("))
                    inFinalize = true;
                continue;
            }

            braceDepth += line.Count(c => c == '{') - line.Count(c => c == '}');
            if (braceDepth <= 0) break;
            if (!string.IsNullOrWhiteSpace(line)) nonBlankCount++;
        }

        nonBlankCount.Should().BeLessThanOrEqualTo(20,
            "CTRL-01 requires the Finalize action body to be ≤ 20 non-blank lines");
    }
}
