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

    const easyMDE = new EasyMDE({
        element: textarea,
        // Font Awesome 6 is already loaded app-wide (plus its v4-shim for EasyMDE's default
        // icon classes) -- skip EasyMDE's own font-detection/download logic entirely.
        autoDownloadFontAwesome: false,
        spellChecker: false,
        status: false,
        // EasyMDE's 300px default towers over a short-form field (a quick Note, a card
        // Description) -- 150px keeps a few lines of typing comfortable without dominating the
        // page, matching the "short-form content, not a document" framing these fields share.
        minHeight: '150px',
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
                    if (!response.ok) {
                        throw new Error('Preview request failed: ' + response.status);
                    }
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

    // CodeMirror's own fromTextArea() (which EasyMDE builds on) only syncs its content back to
    // the underlying <textarea> right before the owning form's native "submit" event fires. But
    // the browser's own required-field validation runs BEFORE that submit event -- against the
    // still-stale (often empty) raw textarea -- and silently blocks the whole submission with no
    // visible error (the textarea is display:none, so no validation bubble can anchor to it).
    // A user can type a full note, click Save/Add Note, and see literally nothing happen.
    // Force a sync on every submit button's click, which always fires before the browser's
    // validity check, so validation (and the eventual POST) see the real, current content.
    const form = textarea.closest('form');
    if (form) {
        form.querySelectorAll('button, input[type="submit"]').forEach(function (submitControl) {
            // A <button> inside a <form> with no type attribute at all defaults to
            // type="submit" per the HTML spec -- skip only buttons that explicitly opt out
            // (type="button"/"reset") rather than requiring an explicit type="submit".
            if (submitControl.tagName === 'BUTTON' && submitControl.type !== 'submit') {
                return;
            }
            submitControl.addEventListener('click', function () {
                easyMDE.codemirror.save();
            });
        });
    }

    return easyMDE;
}

// Class-selector-driven, not ID-driven: asp-for generates a different id per form
// (asp-for="Description" -> id="Description" on Create/CreateFollowUp, asp-for="Quest.Description"
// -> id="Quest_Description" on Edit). A single shared class lets one init loop cover every form.
document.addEventListener('DOMContentLoaded', function () {
    document.querySelectorAll('.markdown-editor-textarea').forEach(function (textarea) {
        // A textarea inside a display:none ancestor (e.g. a collapsed/hidden container) has no
        // offsetParent -- skip it here and leave it for whatever reveal handler shows the
        // container to lazy-init it at that point instead.
        if (textarea.offsetParent === null) {
            return;
        }

        // Stash the built editor on the textarea itself so a page-local script can look up the
        // live editor for a visible field by element id (the raw textarea is hidden by EasyMDE,
        // so bespoke submit handlers can't just read its .value directly).
        textarea.easyMDE = initMarkdownEditor(textarea, window.markdownAntiforgeryToken);
    });
});
