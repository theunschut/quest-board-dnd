using QuestBoard.Domain.Interfaces;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.Logging;

namespace QuestBoard.Service.Services;

public class RazorEmailRenderService(
    IServiceProvider serviceProvider,
    ILoggerFactory loggerFactory) : IEmailRenderService
{
    /// <inheritdoc/>
    public async Task<string> RenderAsync<TComponent>(
        Dictionary<string, object?> parameters)
        where TComponent : IComponent
    {
        await using var htmlRenderer = new HtmlRenderer(serviceProvider, loggerFactory);

        return await htmlRenderer.Dispatcher.InvokeAsync(async () =>
        {
            var paramView = ParameterView.FromDictionary(parameters);
            var output = await htmlRenderer.RenderComponentAsync<TComponent>(paramView);
            return output.ToHtmlString();
        });
    }
}
