namespace QuestBoard.Domain.Interfaces;

public interface IEmailRenderService
{
    /// <summary>
    /// Renders the given Razor component to an HTML string, for use as an email body.
    /// Uses HtmlRenderer rather than IRazorViewEngine so it works in background job contexts.
    /// </summary>
    Task<string> RenderAsync<TComponent>(Dictionary<string, object?> parameters)
        where TComponent : Microsoft.AspNetCore.Components.IComponent;
}
