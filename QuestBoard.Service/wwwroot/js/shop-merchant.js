// The merchant on the shop page. Pressing the merchant button picks one of the quotes the server wrote into
// template[data-merchant-quotes] (the one deliberate random on the page: which quote, not how anything looks) and
// shows it twice: as a toast in the page's toast container, cloned from template[data-merchant-toast], and in the
// visually hidden polite live region that is on the page before any click, which is what a screen reader announces.
// The quote is always written with textContent, so no quote lives in this file and no markup is built from strings.
// Only one merchant toast exists at a time: a second press replaces its quote and restarts its timer. Bootstrap's
// toast hides itself after six seconds and holds it open while the pointer is over it or focus is inside it, so the
// quote can be read at any pace. A page without a toast container still fills the live region.

const TOAST_DELAY_MS = 6000;
const ANNOUNCE_GAP_MS = 50;

const button = document.querySelector('[data-merchant-button]');
const quotes = document.querySelector('template[data-merchant-quotes]');
const toastTemplate = document.querySelector('template[data-merchant-toast]');
const liveRegion = document.querySelector('[data-merchant-live]');

function pickQuote() {
    const choices = quotes.content.querySelectorAll('p');
    if (choices.length === 0) {
        return null;
    }
    return choices[Math.floor(Math.random() * choices.length)].textContent;
}

// Emptying the region first makes a quote that repeats still count as a change, so it is announced again.
function announce(text) {
    if (!liveRegion) {
        return;
    }
    liveRegion.textContent = '';
    window.setTimeout(function () {
        liveRegion.textContent = text;
    }, ANNOUNCE_GAP_MS);
}

function showToast(text) {
    const container = document.querySelector('.toast-container');
    const Toast = window.bootstrap && window.bootstrap.Toast;
    if (!container || !toastTemplate || !Toast) {
        return;
    }

    let toast = container.querySelector('[data-merchant-toast-open]');
    if (!toast) {
        toast = toastTemplate.content.firstElementChild.cloneNode(true);
        toast.setAttribute('data-merchant-toast-open', '');
        container.appendChild(toast);
        toast.addEventListener('hidden.bs.toast', function () {
            toast.remove();
        });
    }

    toast.querySelector('[data-merchant-quote]').textContent = text;
    Toast.getOrCreateInstance(toast, { autohide: true, delay: TOAST_DELAY_MS }).show();
}

if (button && quotes) {
    button.addEventListener('click', function () {
        const text = pickQuote();
        if (text === null) {
            return;
        }
        showToast(text);
        announce(text);
    });
}
