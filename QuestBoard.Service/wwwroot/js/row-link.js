// Whole-row mouse navigation for tables whose first column already holds a real link. A row that carries
// data-row-href opens that address when it is clicked anywhere that is not itself interactive, so the link
// in the row stays the keyboard and screen-reader path while a mouse user gets the larger target. The address
// is rendered by the server and is only ever assigned to the location; no markup is built from strings and
// nothing is exposed globally.

const INTERACTIVE = 'a, button, input, select, textarea, label, summary';

function wantsOwnClick(event) {
    return event.defaultPrevented
        || event.button !== 0
        || event.metaKey
        || event.ctrlKey
        || event.shiftKey
        || event.altKey
        || event.target.closest(INTERACTIVE) !== null;
}

function hasSelectedText() {
    const selection = window.getSelection();
    return selection !== null && selection.toString() !== '';
}

function onRowClick(event) {
    if (wantsOwnClick(event) || hasSelectedText()) {
        return;
    }

    const href = event.currentTarget.dataset.rowHref;
    if (href) {
        window.location.href = href;
    }
}

document.querySelectorAll('tr[data-row-href]').forEach(function (row) {
    row.addEventListener('click', onRowClick);
});
