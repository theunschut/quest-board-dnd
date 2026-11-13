// Calendar month module for the desktop page. The server writes every entry of every day, each with its own
// hidden detail block, so the page is complete without this file. The module only
//
//  - hides the entries that do not fit a day cell and adds one "+N more" button in their place, from the number of
//    rows the stylesheet allows (--cal-slots on the page wrapper; 0 means the window is too narrow for the fit rule
//    and every entry shows),
//  - shows one shared detail card for an entry on hover, keyboard focus and tap, and
//  - opens one shared day panel, a non-modal dialog, from "+N more" on click, tap, Enter or Space and from nothing else.
//
// The card and the panel are empty shells the server renders; they are moved to the body here and positioned from
// viewport rectangles, because the month card is a blurred panel and a fixed-position child of a blurred element is
// placed against that element, not the window. Text goes in with textContent and cloneNode; nothing builds markup
// from a string, and the only style written is the pair of position custom properties the stylesheet reads. Nothing
// is exposed globally, and any failure leaves every entry visible, which is how the page reads without script.

const FIT_QUERIES = ['(min-width: 992px)', '(min-height: 712px)', '(min-height: 850px)', '(min-height: 980px)'];
const CARD_DELAY = 120; // ms between the pointer arriving on an entry and its card showing
const LEAVE_GRACE = 180; // ms the pointer may spend between an entry and its card
const GAP = 8; // px between an entry and its card, and between the viewport edge and a floating surface
const MOVE_SLOP = 4; // px the pointer must travel before it counts as moving again after Esc

const page = document.querySelector('.calendar-page');

if (page) {
    try {
        enhance(page);
    } catch {
        restoreAll(page);
    }
}

function restoreAll(root) {
    root.querySelectorAll('.cal-more-item').forEach((item) => item.remove());
    root.querySelectorAll('.cal-entries > li[hidden]').forEach((item) => {
        item.hidden = false;
    });
}

function element(tag, className, text) {
    const node = document.createElement(tag);
    if (className) {
        node.className = className;
    }
    if (text !== undefined && text !== null) {
        node.textContent = text;
    }
    return node;
}

function icon(name) {
    const glyph = element('i', `fas ${name}`);
    glyph.setAttribute('aria-hidden', 'true');
    return glyph;
}

function hiddenText(text) {
    return element('span', 'visually-hidden', text);
}

function entriesOf(cell) {
    return Array.from(cell.querySelectorAll(':scope > .cal-entries > li.cal-entry'));
}

function dateTextOf(cell) {
    const time = cell.querySelector('.calendar-day__top time');
    return time ? time.textContent : '';
}

// ----- slots ------------------------------------------------------------------------------------------------------

function readSlots(root) {
    const value = parseInt(getComputedStyle(root).getPropertyValue('--cal-slots'), 10);
    return Number.isFinite(value) && value > 0 ? value : 0;
}

function enhance(root) {
    const card = document.querySelector('.cal-card');
    const panel = document.getElementById('cal-panel');
    if (!card || !panel) {
        return;
    }
    const panelTitle = panel.querySelector('.cal-panel__title');
    const panelCount = panel.querySelector('.cal-panel__count');
    const panelClose = panel.querySelector('.cal-panel__close');
    const panelRows = panel.querySelector('.cal-panel__rows');
    if (!panelTitle || !panelCount || !panelClose || !panelRows) {
        return;
    }

    const moreCells = new WeakMap();
    const pointer = { x: -1, y: -1 };
    const cardState = { anchor: null, showTimer: 0, hideTimer: 0, suppressed: null, restAt: null };
    const panelState = { open: false, opener: null, refocus: null };
    let lastPointerType = 'mouse';

    document.body.append(card, panel);

    // The native tooltip would compete with the card, and the card and the entry's own name carry the same words.
    root.querySelectorAll('.cal-entry').forEach((entry) => entry.removeAttribute('title'));

    function applySlots() {
        closeCard();
        closePanel('silent');
        const slots = readSlots(root);
        root.querySelectorAll('.calendar-day').forEach((cell) => {
            cell.querySelectorAll(':scope > .cal-entries > .cal-more-item').forEach((item) => item.remove());
            const entries = entriesOf(cell);
            entries.forEach((entry) => {
                entry.hidden = false;
            });
            if (slots === 0 || entries.length <= slots) {
                return;
            }
            // The last slot is the "+N more" row, so one entry fewer than the slots shows and "+1 more" never appears.
            const shown = slots - 1;
            entries.forEach((entry, index) => {
                entry.hidden = index >= shown;
            });
            const item = element('li', 'cal-more-item');
            const button = element('button', 'cal-more');
            button.type = 'button';
            button.setAttribute('aria-haspopup', 'dialog');
            button.setAttribute('aria-expanded', 'false');
            button.setAttribute('aria-controls', panel.id);
            button.append(element('span', null, `+${entries.length - shown} more`), hiddenText(` entries on ${dateTextOf(cell)}`));
            moreCells.set(button, cell);
            item.appendChild(button);
            cell.querySelector(':scope > .cal-entries').appendChild(item);
        });
    }

    // ----- positioning ------------------------------------------------------------------------------------------------

    function topLimit() {
        const nav = document.querySelector('.navbar.fixed-top');
        return (nav ? nav.getBoundingClientRect().bottom : 0) + GAP;
    }

    function place(node, x, y) {
        node.style.setProperty('--pop-x', `${Math.round(x)}px`);
        node.style.setProperty('--pop-y', `${Math.round(y)}px`);
    }

    // ----- detail card ------------------------------------------------------------------------------------------------

    function cardVisible() {
        return !card.hidden;
    }

    function cancelCardTimers() {
        clearTimeout(cardState.showTimer);
        clearTimeout(cardState.hideTimer);
    }

    function openCard(entry) {
        cancelCardTimers();
        if (panelState.open) {
            return;
        }
        const detail = entry.querySelector('.cal-entry-detail');
        if (!detail) {
            return;
        }
        const clone = detail.cloneNode(true);
        clone.hidden = false;
        card.replaceChildren(clone);
        card.hidden = false;
        cardState.anchor = entry;
        const anchorRect = entry.getBoundingClientRect();
        const cardRect = card.getBoundingClientRect();
        const vw = document.documentElement.clientWidth;
        const vh = document.documentElement.clientHeight;
        const x = Math.min(Math.max(GAP, anchorRect.left), vw - cardRect.width - GAP);
        let y = anchorRect.bottom + GAP;
        if (y + cardRect.height > vh - GAP) {
            y = anchorRect.top - cardRect.height - GAP;
        }
        place(card, x, Math.max(topLimit(), y));
    }

    function closeCard() {
        cancelCardTimers();
        card.hidden = true;
        cardState.anchor = null;
    }

    function scheduleShow(entry) {
        cancelCardTimers();
        cardState.showTimer = setTimeout(() => openCard(entry), CARD_DELAY);
    }

    function scheduleHide() {
        clearTimeout(cardState.hideTimer);
        cardState.hideTimer = setTimeout(closeCard, LEAVE_GRACE);
    }

    function entryOf(target) {
        const found = target.closest ? target.closest('.cal-entry') : null;
        return found && root.contains(found) ? found : null;
    }

    // ----- day panel --------------------------------------------------------------------------------------------------

    function factsOf(entry) {
        const facts = {};
        entry.querySelectorAll('.cal-detail__facts > dt').forEach((term) => {
            facts[term.textContent] = term.nextElementSibling ? term.nextElementSibling.textContent : '';
        });
        return facts;
    }

    // One row per entry, built from the entry's own marker and detail block. An own or member-linked entry is a link;
    // an entry of a board the viewer cannot open stays a plain block with no link and no way to focus it.
    function buildRow(entry) {
        const link = entry.querySelector('a.cal-entry__body');
        const row = link ? element('a', 'cal-panel__row') : element('div', 'cal-panel__row');
        if (link) {
            row.setAttribute('href', link.getAttribute('href'));
        }
        const linked = entry.classList.contains('linked-entry');
        if (linked) {
            row.classList.add('cal-panel__row--linked');
        }
        if (entry.classList.contains('cancelled') || entry.classList.contains('linked-entry--cancelled')) {
            row.classList.add('cal-panel__row--cancelled');
        }
        const facts = factsOf(entry);
        const marker = entry.querySelector(':scope > .cal-entry__body > .cal-mark');
        if (marker) {
            row.appendChild(marker.cloneNode(true));
        }
        const line1 = element('span', 'cal-panel__row-line1');
        const titleNode = entry.querySelector('.cal-entry__title');
        line1.append(element('span', 'cal-panel__row-time', facts.Time || ''), element('span', 'cal-panel__row-title', titleNode ? titleNode.textContent : ''));
        const line2 = element('span', 'cal-panel__row-line2');
        const status = entry.querySelector('.cal-detail__status');
        const statusWord = status ? status.textContent.trim().replace(/,\s*linked board$/i, '') : '';
        line2.appendChild(element('span', null, statusWord));
        if (facts.DM) {
            line2.appendChild(element('span', null, `DM ${facts.DM}`));
        }
        if (facts.Board) {
            const board = element('span');
            if (linked) {
                board.appendChild(icon('fa-link'));
            }
            board.appendChild(document.createTextNode(facts.Board));
            line2.appendChild(board);
        }
        entry.querySelectorAll('.cal-detail__note').forEach((note) => {
            line2.appendChild(element('span', null, note.textContent));
        });
        const text = element('span', 'cal-panel__row-text');
        text.append(line1, line2);
        row.appendChild(text);
        return row;
    }

    function openPanel(button) {
        if (panelState.open && panelState.opener === button) {
            closePanel('button');
            return;
        }
        if (panelState.open) {
            closePanel('silent');
        }
        closeCard();
        const cell = moreCells.get(button);
        const entries = entriesOf(cell);
        panelTitle.textContent = dateTextOf(cell);
        panelCount.textContent = `${entries.length} entries`;
        panelRows.replaceChildren();
        entries.forEach((entry) => {
            const item = element('li');
            item.appendChild(buildRow(entry));
            panelRows.appendChild(item);
        });
        panel.hidden = false;
        panelRows.scrollTop = 0;

        const cellRect = cell.getBoundingClientRect();
        const panelRect = panel.getBoundingClientRect();
        const vw = document.documentElement.clientWidth;
        const vh = document.documentElement.clientHeight;
        let x = cellRect.left - GAP;
        if (x + panelRect.width > vw - GAP) {
            x = cellRect.right + GAP - panelRect.width;
        }
        x = Math.max(GAP, x);
        const y = Math.max(topLimit(), Math.min(cellRect.top - GAP, vh - panelRect.height - GAP));
        place(panel, x, y);

        panelState.open = true;
        panelState.opener = button;
        panelState.refocus = null;
        button.setAttribute('aria-expanded', 'true');
        panel.focus({ preventScroll: true });
    }

    // why: 'esc' | 'button' | 'outside' | 'silent'. Esc and Close hand focus straight back to the "+N more" that
    // opened the panel; a press outside does it once the press has finished moving focus, and only when it did not
    // land on something focusable.
    function closePanel(why) {
        if (!panelState.open) {
            return;
        }
        const opener = panelState.opener;
        panel.hidden = true;
        panelState.open = false;
        panelState.opener = null;
        opener.setAttribute('aria-expanded', 'false');
        if (why === 'silent' || !opener.isConnected) {
            return;
        }
        if (why === 'outside') {
            panelState.refocus = opener;
        } else {
            opener.focus({ preventScroll: true });
        }
    }

    // ----- events -----------------------------------------------------------------------------------------------------

    document.addEventListener('pointerdown', (event) => {
        lastPointerType = event.pointerType;
        panelState.refocus = null;
        const target = event.target;
        if (panelState.open && !panel.contains(target)) {
            const more = target.closest ? target.closest('.cal-more') : null;
            if (more !== panelState.opener) {
                closePanel('outside');
            }
        }
        if (event.pointerType === 'touch' && cardVisible() && !card.contains(target) && !entryOf(target)) {
            closeCard();
        }
    }, true);

    // The click that ends an outside press arrives after the press has moved focus: a click on empty page hands
    // focus back to the button, a click on a control keeps it there.
    document.addEventListener('click', () => {
        const opener = panelState.refocus;
        panelState.refocus = null;
        if (!opener || !opener.isConnected) {
            return;
        }
        const active = document.activeElement;
        const interactive = active && active.matches('a[href], button, input, select, textarea, summary, [tabindex]:not([tabindex="-1"])');
        if (!interactive) {
            opener.focus({ preventScroll: true });
        }
    }, true);

    document.addEventListener('pointermove', (event) => {
        pointer.x = event.clientX;
        pointer.y = event.clientY;
        if (cardState.restAt && (Math.abs(pointer.x - cardState.restAt.x) >= MOVE_SLOP || Math.abs(pointer.y - cardState.restAt.y) >= MOVE_SLOP)) {
            cardState.restAt = null;
        }
    });

    document.documentElement.addEventListener('pointerleave', () => {
        if (cardVisible()) {
            scheduleHide();
        }
    });

    document.addEventListener('pointerover', (event) => {
        if (event.pointerType === 'touch') {
            return;
        }
        const target = event.target;
        const entry = entryOf(target);
        if (entry) {
            if (cardState.suppressed === entry) {
                return;
            }
            // After Esc, whatever now sits under a pointer that has not moved does not open a card.
            if (cardState.restAt && Math.abs(event.clientX - cardState.restAt.x) < MOVE_SLOP && Math.abs(event.clientY - cardState.restAt.y) < MOVE_SLOP) {
                return;
            }
            clearTimeout(cardState.hideTimer);
            if (cardVisible()) {
                openCard(entry);
            } else {
                scheduleShow(entry);
            }
            return;
        }
        if (card.contains(target)) {
            clearTimeout(cardState.hideTimer);
        }
    });

    document.addEventListener('pointerout', (event) => {
        if (event.pointerType === 'touch') {
            return;
        }
        const target = event.target;
        const to = event.relatedTarget;
        const entry = entryOf(target);
        if (entry) {
            if (to && entry.contains(to)) {
                return;
            }
            if (cardState.suppressed === entry) {
                cardState.suppressed = null;
            }
            clearTimeout(cardState.showTimer);
            if (to && card.contains(to)) {
                return;
            }
            if (cardVisible()) {
                scheduleHide();
            }
            return;
        }
        if (card.contains(target)) {
            if (to && (card.contains(to) || (cardState.anchor && cardState.anchor.contains(to)))) {
                return;
            }
            scheduleHide();
        }
    });

    root.addEventListener('focusin', (event) => {
        const entry = entryOf(event.target);
        if (!entry) {
            return;
        }
        if (cardState.suppressed && cardState.suppressed !== entry) {
            cardState.suppressed = null;
        }
        if (cardState.suppressed === entry) {
            return;
        }
        if (event.target.matches(':focus-visible')) {
            openCard(entry);
        }
    });

    root.addEventListener('focusout', (event) => {
        const entry = entryOf(event.target);
        if (!entry) {
            return;
        }
        if (cardState.suppressed === entry) {
            cardState.suppressed = null;
        }
        if (cardState.anchor === entry) {
            closeCard();
        }
    });

    root.addEventListener('click', (event) => {
        const entry = entryOf(event.target);
        if (entry) {
            // A first tap shows the card; a second tap on the same entry follows its link (an inert entry has none).
            if (lastPointerType === 'touch' && !(cardVisible() && cardState.anchor === entry)) {
                event.preventDefault();
                openCard(entry);
            }
            return;
        }
        const more = event.target.closest ? event.target.closest('.cal-more') : null;
        if (more) {
            openPanel(more);
        }
    });

    document.addEventListener('keydown', (event) => {
        if (event.key !== 'Escape') {
            return;
        }
        if (panelState.open) {
            event.preventDefault();
            closePanel('esc');
            return;
        }
        if (cardVisible()) {
            cardState.suppressed = cardState.anchor;
            cardState.restAt = { x: pointer.x, y: pointer.y };
            closeCard();
        }
    });

    panelClose.addEventListener('click', () => closePanel('button'));

    // Tab and Shift+Tab loop inside the panel while it is open; it is not modal, so the rest of the page stays live.
    panel.addEventListener('keydown', (event) => {
        if (event.key !== 'Tab') {
            return;
        }
        const items = panel.querySelectorAll('button, a[href]');
        const first = items[0];
        const last = items[items.length - 1];
        const active = document.activeElement;
        if (event.shiftKey && (active === panel || active === first)) {
            event.preventDefault();
            last.focus();
        } else if (!event.shiftKey && active === last) {
            event.preventDefault();
            first.focus();
        }
    });

    panel.addEventListener('pointerover', () => clearTimeout(cardState.hideTimer));
    card.addEventListener('pointerover', () => clearTimeout(cardState.hideTimer));

    FIT_QUERIES.forEach((query) => window.matchMedia(query).addEventListener('change', applySlots));
    applySlots();
}
