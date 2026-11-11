// Typed-name gate for the board delete confirmation page. The Delete board button is rendered disabled by the
// server and is enabled here only while the typed text, trimmed, equals the board name exactly (ordinal and
// case-sensitive). This is a convenience: the server compares the typed name again on every post and stays
// the authority. The status line is built from elements and text nodes only, never from a markup string, so a
// board name can never become markup.

const MATCH_MESSAGE = 'Name matches. You can delete the board now.';

function init() {
    const input = document.querySelector('[data-board-delete-name]');
    const button = document.querySelector('[data-board-delete-submit]');
    const status = document.getElementById('board-delete-status');
    const form = input ? input.form : null;

    if (!input || !button || !status || !form) {
        return;
    }

    let submitted = false;
    let matched = false;

    function showStatus(isMatch) {
        if (isMatch === matched) {
            return;
        }

        matched = isMatch;

        if (!isMatch) {
            status.replaceChildren();
            return;
        }

        const icon = document.createElement('i');
        icon.className = 'fas fa-circle-check me-2';
        icon.setAttribute('aria-hidden', 'true');
        status.replaceChildren(icon, document.createTextNode(MATCH_MESSAGE));
    }

    function sync() {
        const isMatch = input.value.trim() === input.dataset.boardDeleteName;
        button.disabled = submitted || !isMatch;
        showStatus(isMatch);
    }

    input.addEventListener('input', sync);

    form.addEventListener('submit', event => {
        if (button.disabled) {
            event.preventDefault();
            return;
        }

        submitted = true;
    });

    // A page restored from the back-forward cache keeps its old DOM and site.js re-enables the submit
    // buttons on restore. This listener is registered after that one, so it runs last and wins.
    window.addEventListener('pageshow', event => {
        if (event.persisted) {
            submitted = false;
        }

        sync();
    });

    sync();
}

init();
