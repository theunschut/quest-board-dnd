using QuestBoard.Domain.Enums;

namespace QuestBoard.Service.ViewModels.EventViewModels;

// The roster this phase renders shows a plain Yes / Maybe / No per person and does not
// distinguish an automatic default from a deliberately chosen answer -- that distinction
// stays out of this view model on purpose, so this view can never accidentally render it.
public class EventSignupViewModel
{
    public int UserId { get; set; }

    public string UserName { get; set; } = string.Empty;

    public VoteType Availability { get; set; }
}
