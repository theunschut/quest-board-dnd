namespace QuestBoard.Service.ViewModels.Shared;

/// <summary>
/// Drives the shared _MarkdownEditor partial. FieldName carries the model-binding name
/// explicitly (e.g. "Description" or "Quest.Description") rather than relying on asp-for,
/// since the same partial is included by multiple forms whose model paths to the Description
/// field differ.
/// </summary>
public class MarkdownEditorViewModel
{
    public string FieldName { get; set; } = string.Empty;

    public string? Value { get; set; }

    public string Label { get; set; } = string.Empty;

    public bool Required { get; set; }

    public string? Placeholder { get; set; }

    /// <summary>
    /// Overrides the DOM id derived from FieldName. Needed when multiple instances of the
    /// same underlying field render on one page (e.g. one editor per item in a list) so each
    /// gets a unique id while still posting under the same FieldName. Leave null for the
    /// default single-instance behavior, which derives the id from FieldName.
    /// </summary>
    public string? ElementId { get; set; }

    /// <summary>
    /// Optional client-side character limit, enforced live by markdown-editor.js and mirrored
    /// in a counter below the field. Leave null for editors with no client-side limit; any
    /// server-side [StringLength] validation on the bound property is unaffected either way.
    /// </summary>
    public int? MaxLength { get; set; }
}
