namespace QuestBoard.Service.ViewModels.ErrorViewModels;

/// <summary>
/// Everything the error page renders. The copy is fixed per status and never built from the
/// request: no path, query, id or route value ever reaches this model, so two failing requests for
/// different records produce the same page.
/// </summary>
public class ErrorPageViewModel
{
    public int StatusCode { get; init; }

    /// <summary>A short lower-case token naming the variant: a status such as "404", or "generic".</summary>
    public string Variant { get; init; } = "generic";

    public string Heading { get; init; } = string.Empty;

    public string Docket { get; init; } = string.Empty;

    public string Sentence { get; init; } = string.Empty;

    /// <summary>True for a signed-in viewer: the page offers Log out instead of Log in.</summary>
    public bool IsAuthenticated { get; init; }

    public static ErrorPageViewModel For(int statusCode, bool isAuthenticated)
    {
        var (variant, heading, sentence) = statusCode switch
        {
            404 => ("404", "Page not found",
                "It may have been removed, or it may belong to a board you're not viewing right now."),
            403 => ("403", "You Don't Have Permission",
                isAuthenticated
                    ? "If you believe you should have access to this page, please contact an administrator."
                    : "Please log in to access this page."),
            400 => ("400", "That request could not be processed",
                "Something about that request was not valid. Go back and try again."),
            409 => ("409", "Your board session has changed",
                "Pick your board again, then retry. Nothing was saved."),
            429 => ("429", "Please wait a moment",
                "Too many requests. Please try again later."),
            500 => ("500", "Something went wrong",
                "We hit a problem on our side. Please try again in a moment."),
            _ => ("generic", "Something went wrong",
                "That request could not be completed. Go back or return home."),
        };

        return new ErrorPageViewModel
        {
            StatusCode = statusCode,
            Variant = variant,
            Heading = heading,
            Docket = $"Error {statusCode}",
            Sentence = sentence,
            IsAuthenticated = isAuthenticated,
        };
    }
}
