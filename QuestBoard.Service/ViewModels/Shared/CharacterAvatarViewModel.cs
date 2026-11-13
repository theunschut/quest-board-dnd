namespace QuestBoard.Service.ViewModels.Shared;

/// <summary>
/// Drives the shared _CharacterAvatar partial: the 32px portrait of a character, or the placeholder
/// disc when the character has no stored picture, so a page never asks for a picture that is not there.
/// </summary>
public class CharacterAvatarViewModel
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    /// <summary>True when the character has a stored picture; false renders the placeholder and no image request.</summary>
    public bool HasPicture { get; set; }

    /// <summary>True when the name is printed beside the avatar, so the image carries an empty alt.</summary>
    public bool Decorative { get; set; }

    /// <summary>Extra classes for the avatar, such as spacing utilities.</summary>
    public string? CssClass { get; set; }
}
