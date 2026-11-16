// Sell to Shop on the item details: keeps the running total in step with the quantity and asks before the sale is
// sent. The details are shown both on their own page and inside the shop's dialog, where the markup is written into
// the page after it has loaded, so nothing inside the details can carry its own script. The listeners therefore sit on
// the document and look for forms marked data-sell-form. The item name and the price of one item are read from the
// form's data attributes as plain text and handed to confirm() and textContent, so a name holding quotes cannot break
// anything, no markup is built from strings and nothing is exposed globally.

function formatGold(amount) {
    return parseInt(amount.toFixed(0), 10).toLocaleString();
}

function totalFor(form) {
    const quantity = form.querySelector('[data-sell-quantity]').value;
    return Number.parseFloat(form.dataset.unitPrice) * quantity;
}

document.addEventListener('input', function (event) {
    if (!(event.target instanceof Element) || !event.target.matches('[data-sell-quantity]')) {
        return;
    }
    const form = event.target.closest('form[data-sell-form]');
    const total = form && form.querySelector('[data-sell-total]');
    if (total) {
        total.textContent = formatGold(totalFor(form));
    }
});

document.addEventListener('submit', function (event) {
    const form = event.target;
    if (!(form instanceof HTMLFormElement) || !form.matches('form[data-sell-form]')) {
        return;
    }
    const quantity = form.querySelector('[data-sell-quantity]').value;
    const message = `Are you sure you want to sell ${quantity}x ${form.dataset.itemName} to the shop for ${formatGold(totalFor(form))} gp?`;
    if (!confirm(message)) {
        event.preventDefault();
    }
});
