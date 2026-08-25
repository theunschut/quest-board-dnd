using QuestBoard.Domain.Enums;
using QuestBoard.Domain.Models;

namespace QuestBoard.Service.Extensions;

/// <summary>
/// Turns a character into the text shown in a signup character picker. This is the one
/// place that happens, so every dropdown on the Details page renders the same label for
/// the same character instead of each drifting toward its own copy of the format.
/// </summary>
public static class CharacterDisplayExtensions
{
    /// <summary>
    /// Builds the picker option text for a character: name, level, and classes, with a
    /// status suffix appended for any character that is not Active. Active characters keep
    /// the exact text this codebase's dropdowns have always shown, so this change is silent
    /// to a player who only ever plays Active characters.
    /// </summary>
    public static string ToSelectLabel(this Character character)
    {
        var classes = character.Classes ?? [];
        var classList = string.Join(", ", classes.Select(c => $"{c.Class} {c.ClassLevel}"));
        var label = $"{character.Name} - Level {character.Level} ({classList})";

        if (character.Status != CharacterStatus.Active)
        {
            label += $" ({character.Status})";
        }

        return label;
    }
}
