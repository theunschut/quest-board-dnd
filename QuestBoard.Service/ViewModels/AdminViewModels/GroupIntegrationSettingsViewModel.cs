using System.ComponentModel.DataAnnotations;

namespace QuestBoard.Service.ViewModels.AdminViewModels;

/// <summary>
/// Form and cascade-status view model for the group-scoped Omphalos override page. Never
/// carries the group's raw stored secret, nor the instance-wide default's actual URL or
/// secret — only booleans describing whether a default exists and is enabled.
/// </summary>
public class GroupIntegrationSettingsViewModel
{
    [Display(Name = "Omphalos URL")]
    [Url]
    [StringLength(500)]
    public string OmphalosUrl { get; set; } = string.Empty;

    [Display(Name = "Shared Secret")]
    [StringLength(200)]
    public string? SharedSecret { get; set; }

    [Display(Name = "Integration Enabled")]
    public bool IsEnabled { get; set; }

    /// <summary>Whether the group has its own secret configured. Never the raw value.</summary>
    public bool HasSecretConfigured { get; set; }

    /// <summary>Whether this group currently has its own override rows.</summary>
    public bool HasOverride { get; set; }

    /// <summary>Whether the instance-wide default has any of the three settings configured.</summary>
    public bool InstanceDefaultConfigured { get; set; }

    /// <summary>Whether the instance-wide default is enabled. Never the default's URL/secret.</summary>
    public bool InstanceDefaultEnabled { get; set; }

    /// <summary>A freshly generated secret, shown once via TempData after Generate Secret.</summary>
    public string? GeneratedSecret { get; set; }
}
