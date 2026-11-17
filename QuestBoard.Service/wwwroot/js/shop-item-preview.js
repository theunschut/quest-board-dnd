// The card preview on the shop item form.
//
// When the Preview dialog opens, the card a shopper would see is built from what is typed in the form right now and
// placed in the dialog. Nothing is saved and nothing is sent anywhere except the description, which is posted to the
// existing Markdown preview endpoint so the excerpt is cut from the same plain text the real card shows.
//
// The card is cloned from a server-rendered template, so it carries the real card classes, and every value is written
// with textContent or by switching a class: no markup is ever built from a string, and the answer from the server is
// parsed into an inert document of which only the text is read. The dialog opens at once with the excerpt taken from
// the raw text; the server's text replaces it when it arrives, and the raw text stays if the request fails. Only the
// answer to the latest opening is applied. The description is read from the Markdown editor when the dialog opens,
// because the editor is built after this module has run, falling back to the plain textarea.

const EXCERPT_LENGTH = 120;

const RARITIES = Object.freeze({
    Common: Object.freeze({ key: 'common', word: 'Common' }),
    Uncommon: Object.freeze({ key: 'uncommon', word: 'Uncommon' }),
    Rare: Object.freeze({ key: 'rare', word: 'Rare' }),
    VeryRare: Object.freeze({ key: 'veryrare', word: 'Very Rare' }),
    Legendary: Object.freeze({ key: 'legendary', word: 'Legendary' }),
});

const modal = document.getElementById('itemPreviewModal');
const form = document.querySelector('form[data-shop-item-form]');

// Each opening of the dialog takes a new number, so an answer that arrives after a newer opening is ignored.
let latestRequestId = 0;

// The same four stock rules as the real card, with the same words.
function stockFor(quantity) {
    if (quantity === -1) {
        return { modifier: 'shop-card__stock--unlimited', icon: 'fa-infinity', text: 'Unlimited' };
    }
    if (!(quantity > 0)) {
        return { modifier: 'shop-card__stock--soldout', icon: 'fa-times-circle', text: 'Sold Out' };
    }
    if (quantity <= 5) {
        return { modifier: 'shop-card__stock--low', icon: 'fa-exclamation-triangle', text: `${quantity} left` };
    }
    return { modifier: null, icon: 'fa-box', text: `${quantity} in stock` };
}

function cutExcerpt(text) {
    return text.length > EXCERPT_LENGTH ? `${text.slice(0, EXCERPT_LENGTH)}...` : text;
}

function collapseWhitespace(text) {
    return text.replace(/\s+/g, ' ').trim();
}

// The server renders Markdown to sanitised HTML. Its top-level blocks are read as text and joined with spaces, as the
// real card does, from a document that is never attached to the page.
function plainTextOf(html) {
    const parsed = new DOMParser().parseFromString(html, 'text/html');
    const parts = Array.from(parsed.body.children)
        .map((element) => element.textContent.trim())
        .filter((text) => text.length > 0);
    return collapseWhitespace(parts.length > 0 ? parts.join(' ') : parsed.body.textContent);
}

function descriptionOf(field) {
    if (!field) {
        return '';
    }
    return typeof field.easyMDE?.value === 'function' ? field.easyMDE.value() : field.value;
}

function fetchPlainText(markdown, token) {
    return fetch('/markdown/preview', {
        method: 'POST',
        headers: {
            'Content-Type': 'application/json',
            'RequestVerificationToken': token,
        },
        body: JSON.stringify({ markdown }),
    }).then((response) => {
        if (!response.ok) {
            throw new Error(`Preview request failed: ${response.status}`);
        }
        return response.text();
    }).then(plainTextOf);
}

function buildCard(values) {
    const template = modal.querySelector('template[data-preview-card]');
    const card = template.content.firstElementChild.cloneNode(true);

    card.querySelector('[data-preview-name]').textContent = values.name || 'Unnamed item';

    const chip = card.querySelector('[data-preview-chip]');
    const rarity = Object.hasOwn(RARITIES, values.rarity) ? RARITIES[values.rarity] : null;
    if (rarity) {
        chip.classList.add(`rarity-${rarity.key}`);
        chip.querySelector('[data-preview-chip-word]').textContent = rarity.word;
    } else {
        chip.remove();
    }

    const price = Number.isFinite(values.price) ? values.price : 0;
    card.querySelector('[data-preview-price]').textContent = `${price.toFixed(2)} gp`;

    const stock = stockFor(values.quantity);
    const stockLabel = card.querySelector('[data-preview-stock]');
    if (stock.modifier) {
        stockLabel.classList.add(stock.modifier);
    }
    card.querySelector('[data-preview-stock-icon]').classList.add(stock.icon);
    card.querySelector('[data-preview-stock-text]').textContent = stock.text;

    return card;
}

function readValues() {
    const read = (name) => form.querySelector(`[name="${name}"]`);
    return {
        name: (read('Name')?.value ?? '').trim(),
        rarity: read('Rarity')?.value ?? '',
        price: Number.parseFloat(read('Price')?.value ?? ''),
        quantity: Number.parseInt(read('Quantity')?.value ?? '', 10),
        description: descriptionOf(read('Description')),
    };
}

function show() {
    const host = modal.querySelector('[data-preview-host]');
    const values = readValues();
    const card = buildCard(values);
    const excerpt = card.querySelector('[data-preview-excerpt]');
    const requestId = ++latestRequestId;

    const rawText = collapseWhitespace(values.description);
    excerpt.textContent = rawText ? cutExcerpt(rawText) : 'No description yet.';
    host.replaceChildren(card);

    const token = document.querySelector('meta[name="request-verification-token"]')?.content;
    if (!rawText || !token) {
        return;
    }

    fetchPlainText(values.description, token)
        .then((text) => {
            if (requestId === latestRequestId) {
                excerpt.textContent = cutExcerpt(text);
            }
        })
        .catch(() => {
            // The excerpt from the raw text stays.
        });
}

if (modal && form) {
    modal.addEventListener('show.bs.modal', show);
}
