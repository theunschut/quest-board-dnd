// Edit toggle for the notes on a contact's details page, the same on the desktop and the phone layout.
//
// Each note is a slip holding its text and controls (.notes-text) and a hidden edit form (.notes-edit-form). The
// Edit note button hides the text and shows the form by toggling the hidden attribute; Cancel reverses it, drops any
// unsaved text and puts focus back on the Edit note button. Only one slip is open at a time. Focus moves to the
// editor when a slip opens.
//
// The Markdown editor is built the first time a slip opens, never while its form is hidden, because the editor
// measures its container when it is built. The editor builder and the preview token are the globals the shared
// Markdown editor script publishes; this module adds no global of its own and builds no markup from strings.

const editors = new Map();

function slipParts(slip) {
    return {
        text: slip.querySelector('.notes-text'),
        form: slip.querySelector('.notes-edit-form'),
        editButton: slip.querySelector('.note-edit-btn'),
    };
}

function collapse(slip) {
    const { text, form } = slipParts(slip);
    const entry = editors.get(slip.dataset.noteId);
    if (entry) {
        entry.instance.value(entry.savedText);
    }
    form.hidden = true;
    text.hidden = false;
}

function expand(slip) {
    const { text, form } = slipParts(slip);
    const noteId = slip.dataset.noteId;
    const textarea = form.querySelector('.markdown-editor-textarea');

    text.hidden = true;
    form.hidden = false;

    let entry = editors.get(noteId);
    if (!entry) {
        const instance = window.initMarkdownEditor(textarea, window.markdownAntiforgeryToken);
        entry = { instance, savedText: textarea.value };
        editors.set(noteId, entry);
    } else {
        entry.instance.codemirror.refresh();
    }
    entry.instance.codemirror.focus();
}

document.querySelectorAll('.notes-slip').forEach(function (slip) {
    const { form, editButton } = slipParts(slip);
    const cancelButton = slip.querySelector('.note-cancel-btn');
    if (!form || !editButton) {
        return;
    }

    editButton.addEventListener('click', function () {
        document.querySelectorAll('.notes-slip').forEach(function (other) {
            const otherForm = other.querySelector('.notes-edit-form');
            if (other !== slip && otherForm && !otherForm.hidden) {
                collapse(other);
            }
        });
        expand(slip);
    });

    cancelButton?.addEventListener('click', function () {
        collapse(slip);
        editButton.focus();
    });
});
