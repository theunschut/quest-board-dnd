namespace QuestBoard.Service.ViewModels.AccountViewModels;

public class CalendarSubscriptionViewModel
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    // Built in the controller from the configured application address, never derivable from the
    // domain model alone -- see the AutoMapper map's ignore comments in ViewModelProfile.
    public string HttpsAddress { get; set; } = string.Empty;

    public string WebcalAddress { get; set; } = string.Empty;

    // Null when code generation failed; the row still renders every other way of getting the
    // address, and the view simply omits this region rather than showing a broken image.
    public string? QrCodeSvg { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? LastFetchedAt { get; set; }
}
