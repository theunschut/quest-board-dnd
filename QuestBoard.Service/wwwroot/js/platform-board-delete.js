// Typed-name gate for the board delete confirmation page. The Delete board button is rendered disabled by the
// server and is enabled here only while the typed text equals the board name exactly (ordinal and
// case-sensitive) once both have lost their edge whitespace. This is a convenience: the server compares the
// typed name again on every post and stays the authority. The status line is built from elements and text
// nodes only, never from a markup string, so a board name can never become markup.

const MATCH_MESSAGE = 'Name matches. You can delete the board now.';

// The edge whitespace ignored on both sides: every Unicode White_Space character plus U+FEFF. This is the
// server's set character for character (BoardNameText.EdgeWhitespace), and deliberately not String.trim(),
// whose set differs from .NET's (it strips U+FEFF but keeps U+0085), so the button and the server always agree.
const EDGE_WHITESPACE = '\u0009\u000A\u000B\u000C\u000D\u0020\u0085\u00A0\u1680\u2000\u2001\u2002\u2003\u2004\u2005\u2006\u2007\u2008\u2009\u200A\u2028\u2029\u202F\u205F\u3000\uFEFF';
const EDGE_PATTERN = new RegExp(`^[${EDGE_WHITESPACE}]+|[${EDGE_WHITESPACE}]+$`, 'g');

function trimEdges(value) {
    return (value || '').replace(EDGE_PATTERN, '');
}

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
        const isMatch = trimEdges(input.value) === trimEdges(input.dataset.boardDeleteName);
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
