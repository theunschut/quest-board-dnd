using System.ComponentModel.DataAnnotations;

namespace QuestBoard.Service.ViewModels.PlatformViewModels;

public class IntegrationSettingsViewModel
{
    [Display(Name = "Omphalos URL")]
    [Url(ErrorMessage = "Enter a valid URL, e.g. https://your-omphalos-instance.example.com.")]
    public string OmphalosUrl { get; set; } = string.Empty;

    [Display(Name = "Shared Secret")]
    [StringLength(200)]
    public string? SharedSecret { get; set; }

    [Display(Name = "Integration Enabled")]
    public bool IsEnabled { get; set; }

    // Never carries the real stored secret — only whether one exists, so the GET view can
    // render a configured/not-configured indicator instead of the value itself.
    public bool HasSecretConfigured { get; set; }
}
