using System.Reflection;
using Microsoft.AspNetCore.Mvc;

namespace QuestBoard.IntegrationTests.Security;

/// <summary>
/// Reflection-only sweep (no HTTP round-trip, no WebApplicationFactory) proving every state-changing
/// [HttpPost] controller action carries CSRF protection. A live HTTP test cannot detect a missing
/// [ValidateAntiForgeryToken] because the integration test factory installs a TestAntiforgeryDecorator
/// that always passes validation.
/// </summary>
public class AntiForgeryTokenCoverageTests
{
    [Fact]
    public void AllHttpPostActions_CarryValidateAntiForgeryToken()
    {
        // Arrange
        var controllerTypes = typeof(Program).Assembly.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false } && typeof(Controller).IsAssignableFrom(t));

        var violations = new List<string>();

        foreach (var controllerType in controllerTypes)
        {
            var hasClassLevelAutoValidate = controllerType.GetCustomAttribute<AutoValidateAntiforgeryTokenAttribute>() != null;

            var postActions = controllerType
                .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Where(m => m.GetCustomAttribute<HttpPostAttribute>() != null);

            foreach (var action in postActions)
            {
                var hasActionLevelValidate = action.GetCustomAttribute<ValidateAntiForgeryTokenAttribute>() != null;

                if (!hasActionLevelValidate && !hasClassLevelAutoValidate)
                {
                    violations.Add($"{controllerType.Name}.{action.Name}");
                }
            }
        }

        // Assert
        violations.Should().BeEmpty(
            "every [HttpPost] action must carry [ValidateAntiForgeryToken] (or the controller must carry " +
            "class-level [AutoValidateAntiforgeryToken]) to prevent CSRF; offending actions: " +
            string.Join(", ", violations));
    }
}
