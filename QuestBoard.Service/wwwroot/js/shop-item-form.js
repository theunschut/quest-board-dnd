// The shop item form: the pricing guide, the two price helper buttons and the availability window.
//
// A form opts in with data-shop-item-form and everything the module touches is found inside that form, so no other
// form on the page (the navigation bar's sign-out form, a dialog) can be reached by it. The helper buttons carry
// data-random-price and data-calculated-price, and the checkbox that opens the availability window carries
// data-availability-toggle. A form without the buttons (the phone layout has none) works the same way: the module
// only acts on what is there. The hint under the price is written with textContent and a created icon element, never
// from a markup string, and nothing is exposed globally.

// The guide's ranges and the fixed prices of the shop's own price calculation. Held once, here.
const RARITIES = Object.freeze({
    Common: Object.freeze({ min: 50, max: 100, text: 'Common items typically cost 50-100 gp', calculated: 100 }),
    Uncommon: Object.freeze({ min: 101, max: 500, text: 'Uncommon items typically cost 101-500 gp', calculated: 500 }),
    Rare: Object.freeze({ min: 501, max: 5000, text: 'Rare items typically cost 501-5,000 gp', calculated: 5000 }),
    VeryRare: Object.freeze({ min: 5001, max: 50000, text: 'Very Rare items typically cost 5,001-50,000 gp', calculated: 50000 }),
    Legendary: Object.freeze({ min: 50001, max: 200000, text: 'Legendary items typically cost 50,001+ gp', calculated: 200000 }),
});

function guideFor(rarity) {
    return Object.hasOwn(RARITIES, rarity) ? RARITIES[rarity] : null;
}

function showGuide(hint, guide) {
    const range = document.createElement('span');
    const icon = document.createElement('i');
    icon.className = 'fas fa-coins me-1';
    icon.setAttribute('aria-hidden', 'true');
    range.append(icon, guide.text);

    const recommended = document.createElement('span');
    recommended.textContent = `Tasha's recommended: ${guide.calculated} gp`;

    hint.replaceChildren(range, recommended);
}

function wirePricing(form) {
    const rarity = form.querySelector('[name="Rarity"]');
    const type = form.querySelector('[name="Type"]');
    const price = form.querySelector('[name="Price"]');
    const hint = form.querySelector('#price-suggestion');
    const randomButton = form.querySelector('[data-random-price]');
    const calculatedButton = form.querySelector('[data-calculated-price]');
    if (!rarity || !price) {
        return;
    }

    // The server writes the line that asks for a rarity; it is put back when the rarity is cleared.
    const emptyHint = hint ? hint.textContent.trim() : '';

    function refresh() {
        const guide = guideFor(rarity.value);
        const isEquipment = type !== null && type.value === 'Equipment';

        if (hint) {
            if (isEquipment) {
                hint.replaceChildren();
            } else if (guide) {
                showGuide(hint, guide);
            } else {
                hint.textContent = emptyHint;
            }
        }

        const enabled = isEquipment || guide !== null;
        for (const button of [randomButton, calculatedButton]) {
            if (button) {
                button.disabled = !enabled;
            }
        }
    }

    rarity.addEventListener('change', refresh);
    if (type) {
        type.addEventListener('change', refresh);
    }

    randomButton?.addEventListener('click', function () {
        const guide = guideFor(rarity.value);
        if (guide) {
            price.value = Math.floor(Math.random() * (guide.max - guide.min + 1)) + guide.min;
        }
    });

    calculatedButton?.addEventListener('click', function () {
        const guide = guideFor(rarity.value);
        if (guide) {
            price.value = guide.calculated;
        }
    });

    refresh();
}

function wireAvailability(form) {
    const toggle = form.querySelector('[data-availability-toggle]');
    const from = form.querySelector('[name="AvailableFrom"]');
    const until = form.querySelector('[name="AvailableUntil"]');
    if (!toggle || !from || !until) {
        return;
    }

    toggle.addEventListener('change', function () {
        from.disabled = !toggle.checked;
        until.disabled = !toggle.checked;
        if (!toggle.checked) {
            from.value = '';
            until.value = '';
        }
    });
}

for (const form of document.querySelectorAll('form[data-shop-item-form]')) {
    wirePricing(form);
    wireAvailability(form);
}
