using QuestBoard.Domain.Interfaces;

namespace QuestBoard.Domain.Services;

internal class MarkdownService : IMarkdownService
{
    /// <inheritdoc/>
    public string RenderToHtml(string? markdown, MarkdownRenderTarget target = MarkdownRenderTarget.Web)
    {
        throw new NotImplementedException();
    }
}
