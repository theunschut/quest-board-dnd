// Shared EasyMDE init module for every Markdown-authoring field (Quest Description today,
// remaining fields in later phases). Loaded per-view via a plain <script> include (matching
// site.js's no-module, no-bundler convention) and self-initializes every matching textarea on
// DOMContentLoaded -- callers do not need to invoke anything manually.

// The field's name for assistive technology: the server-rendered data-editor-label when the
// shared partial produced the textarea, otherwise the text of its label (without the required
// marker). Returns an empty string when neither exists.
function editorAccessibleName(textarea) {
    if (textarea.dataset.editorLabel) {
        return textarea.dataset.editorLabel.trim();
    }
    if (!textarea.id) {
        return '';
    }
    const label = document.querySelector('label[for="' + CSS.escape(textarea.id) + '"]');
    return label ? label.textContent.replace(/\*/g, '').trim() : '';
}

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

    // EasyMDE binds Tab to list indentation, which traps a keyboard user inside the field with
    // no way to reach the next control. Releasing Tab and Shift-Tab hands them back to the
    // browser so focus moves on like it does from any other field. Enter still continues a
    // Markdown list.
    const codemirror = easyMDE.codemirror;
    codemirror.setOption('extraKeys', Object.assign({}, codemirror.getOption('extraKeys'), {
        'Tab': false,
        'Shift-Tab': false
    }));

    // The editable surface is CodeMirror's own input, not the textarea the label points at, so
    // give it its own accessible name and tie it to the visible hint under the field.
    const inputField = codemirror.getInputField();
    const accessibleName = editorAccessibleName(textarea);
    if (accessibleName) {
        codemirror.setOption('screenReaderLabel', accessibleName);
    }
    const hint = textarea.id ? document.getElementById(textarea.id + '-help') : null;
    if (hint) {
        inputField.setAttribute('aria-describedby', hint.id);
    }

    // Native <textarea maxlength> only guards typing into the raw textarea, but EasyMDE/CodeMirror
    // intercepts every keystroke itself and never touches the textarea until submit -- so the
    // browser's built-in limit never fires while editing. Re-implement it against CodeMirror's own
    // change event, reading the same maxlength attribute so the markup stays the single source of
    // truth. textarea.maxLength is -1 when no maxlength attribute is present, so this is a no-op
    // for every editor that doesn't opt in.
    const maxLength = textarea.maxLength;
    if (maxLength >= 0) {
        const fieldContainer = textarea.closest('.mb-3');
        const charCountCurrent = fieldContainer ? fieldContainer.querySelector('.markdown-editor-char-count-current') : null;

        easyMDE.codemirror.on('change', function () {
            const value = easyMDE.value();
            if (value.length > maxLength) {
                // Setting the value re-fires this same 'change' handler with the now-truncated
                // text, which updates the counter below -- no separate update needed here.
                easyMDE.value(value.slice(0, maxLength));
                return;
            }
            if (charCountCurrent) {
                charCountCurrent.textContent = value.length;
            }
        });
    }

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

    // The textarea is hidden by EasyMDE, so the browser cannot anchor its own "fill this in" bubble
    // to it and would otherwise cancel the submit without a word. The browser still fires "invalid"
    // on the hidden textarea, which is the cue to say what is wrong in the field's own message
    // element, mark the editable surface invalid and hand it focus. The textarea keeps its
    // required attribute, so the native check and the server-side rule are untouched.
    if (textarea.required) {
        const container = textarea.closest('.mb-3');
        const message = container ? container.querySelector('[data-valmsg-for]') : null;
        if (message) {
            const describedBy = inputField.getAttribute('aria-describedby');
            const clearRequiredMessage = function () {
                if (!message.hasChildNodes()) {
                    return;
                }
                message.replaceChildren();
                inputField.removeAttribute('aria-invalid');
                if (describedBy) {
                    inputField.setAttribute('aria-describedby', describedBy);
                } else {
                    inputField.removeAttribute('aria-describedby');
                }
            };

            textarea.addEventListener('invalid', function (event) {
                if (!textarea.validity.valueMissing) {
                    return;
                }
                event.preventDefault();

                const icon = document.createElement('i');
                icon.className = 'fas fa-circle-exclamation me-1';
                icon.setAttribute('aria-hidden', 'true');
                const name = editorAccessibleName(textarea) || 'This field';
                message.replaceChildren(icon, document.createTextNode(name + ' is required.'));

                inputField.setAttribute('aria-invalid', 'true');
                if (message.id) {
                    inputField.setAttribute('aria-describedby', (describedBy ? describedBy + ' ' : '') + message.id);
                }

                // Only the first invalid control takes focus, as the browser would do itself, so
                // an earlier field with its own native bubble is not stolen from.
                const firstInvalid = form ? form.querySelector(':invalid') : textarea;
                if (firstInvalid === textarea) {
                    codemirror.focus();
                }
            });

            codemirror.on('change', clearRequiredMessage);
        }
    }

    return easyMDE;
}

// The antiforgery token reaches this script through a meta element rather than an inline
// script, so a page that has no inline script can still preview. A page that already set the
// global keeps its value.
if (!window.markdownAntiforgeryToken) {
    const tokenMeta = document.querySelector('meta[name="request-verification-token"]');
    if (tokenMeta) {
        window.markdownAntiforgeryToken = tokenMeta.content;
    }
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
