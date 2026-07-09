// Shared EasyMDE init module for every Markdown-authoring field (Quest Description today,
// remaining fields in later phases). Loaded per-view via a plain <script> include (matching
// site.js's no-module, no-bundler convention) and self-initializes every matching textarea on
// DOMContentLoaded -- callers do not need to invoke anything manually.

// Build one EasyMDE instance bound to a single textarea. previewRender POSTs the current text
// to the server so Preview renders through the exact same sanitizer/parser as the saved output
// (no second, client-side Markdown parser) -- see /markdown/preview.
function initMarkdownEditor(textarea, antiforgeryToken) {
    // Tracks the most recently issued preview request per editor instance, so an
    // earlier-issued-but-later-resolving response (normal network jitter, or a retry after a
    // failed request) can't overwrite a newer preview with stale content.
    let latestRequestId = 0;

    return new EasyMDE({
        element: textarea,
        // Font Awesome 6 is already loaded app-wide (plus its v4-shim for EasyMDE's default
        // icon classes) -- skip EasyMDE's own font-detection/download logic entirely.
        autoDownloadFontAwesome: false,
        spellChecker: false,
        status: false,
        // Exact toolbar item names as defined by EasyMDE itself -- "quote" is the internal name
        // for the Blockquote button; "blockquote" is not recognized and silently renders nothing.
        toolbar: ["bold", "italic", "heading", "unordered-list", "link", "quote", "preview"],
        previewRender: function (plainText, previewElement) {
            const requestId = ++latestRequestId;

            fetch('/markdown/preview', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    'RequestVerificationToken': antiforgeryToken
                },
                body: JSON.stringify({ markdown: plainText })
            })
                .then(function (response) {
                    return response.text();
                })
                .then(function (html) {
                    if (requestId === latestRequestId) {
                        previewElement.innerHTML = html;
                    }
                })
                .catch(function () {
                    if (requestId === latestRequestId) {
                        previewElement.innerHTML = '<p class="text-danger">Preview failed to load.</p>';
                    }
                });

            // EasyMDE renders this return value synchronously while the fetch above is still in
            // flight, then leaves previewElement.innerHTML alone once the async work finishes and
            // sets it directly (per EasyMDE's own previewRender contract).
            return previewElement.innerHTML || '<p>Loading preview...</p>';
        }
    });
}

// Class-selector-driven, not ID-driven: asp-for generates a different id per form
// (asp-for="Description" -> id="Description" on Create/CreateFollowUp, asp-for="Quest.Description"
// -> id="Quest_Description" on Edit). A single shared class lets one init loop cover every form.
document.addEventListener('DOMContentLoaded', function () {
    document.querySelectorAll('.markdown-editor-textarea').forEach(function (textarea) {
        initMarkdownEditor(textarea, window.markdownAntiforgeryToken);
    });
});
