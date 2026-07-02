namespace QuestBoard.Service.ViewModels.AdminViewModels;

public class EmailStatsViewModel
{
    public int Sent { get; set; }
    public int Delivered { get; set; }
    public int Bounced { get; set; }
    public int Failed { get; set; }
    public DateTime? AsOf { get; set; }
    public bool IsMissingKey { get; set; }
    public bool IsApiError { get; set; }

    public static EmailStatsViewModel MissingKey() => new() { IsMissingKey = true };
    public static EmailStatsViewModel ApiError() => new() { IsApiError = true };
}
