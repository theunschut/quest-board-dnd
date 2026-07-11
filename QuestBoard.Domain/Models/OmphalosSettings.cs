namespace QuestBoard.Domain.Models;

/// <summary>
/// The resolved trio of Omphalos integration settings for a given scope (instance-wide
/// default or a group's override), consumed by both settings pages and the token generator.
/// </summary>
public class OmphalosSettings
{
    public string Url { get; set; } = string.Empty;

    /// <summary>
    /// The plaintext shared secret. This DTO may carry the raw value because the token
    /// generator needs it to sign tokens — controllers must never map this property onto a
    /// ViewModel or render it back to the browser; they should read only <see cref="HasSecret"/>.
    /// </summary>
    public string? SharedSecret { get; set; }

    public bool IsEnabled { get; set; }

    public bool HasSecret => !string.IsNullOrEmpty(SharedSecret);
}
