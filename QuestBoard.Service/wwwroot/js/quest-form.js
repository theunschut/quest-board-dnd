// Scripts for the quest forms: the proposed-date rows and the submit guard. A module runs after the
// document is parsed, so the form is already in the DOM when this executes.

const DATES_CONTAINER_ID = 'proposed-dates';
const DATE_INPUT_SELECTOR = 'input[type="datetime-local"]';
const DATE_VALUE_SELECTOR = 'input[type="datetime-local"], input[type="hidden"][name*="ProposedDates"]';

// Ids for rows added in the browser; the label of each row points at its own input.
let addedRowCount = 0;

// The Edit form binds the list as Quest.ProposedDates, the Create form as ProposedDates. Existing
// dates on Edit are hidden inputs, so those count when deciding which form this is.
function fieldPrefix(container) {
    const existing = container.querySelector(DATE_VALUE_SELECTOR);
    return existing && existing.name.includes('Quest.ProposedDates') ? 'Quest.ProposedDates' : 'ProposedDates';
}

function dateLabelText(index) {
    return 'Proposed Date ' + (index + 1);
}

function removeLabelText(index) {
    return 'Remove proposed date ' + (index + 1);
}

// The value a new row starts with: the day after the last filled date at 18:00, or today at 18:00.
function nextDateValue(container, newInput) {
    const inputs = Array.from(container.querySelectorAll(DATE_INPUT_SELECTOR)).filter(function (input) {
        return input !== newInput;
    });

    let baseDate = null;
    for (let i = inputs.length - 1; i >= 0; i--) {
        if (inputs[i].value) {
            baseDate = new Date(inputs[i].value);
            break;
        }
    }

    if (baseDate && !isNaN(baseDate.getTime())) {
        baseDate.setDate(baseDate.getDate() + 1);
        baseDate.setHours(18, 0, 0, 0);
    } else {
        baseDate = new Date();
        baseDate.setHours(18, 0, 0, 0);
    }

    const pad = function (n) { return String(n).padStart(2, '0'); };
    return baseDate.getFullYear() + '-' + pad(baseDate.getMonth() + 1) + '-' + pad(baseDate.getDate())
        + 'T' + pad(baseDate.getHours()) + ':' + pad(baseDate.getMinutes());
}

// Built with DOM calls and property assignment only: no markup string and no handler attribute, so the
// row stays inside the content security policy and nothing is parsed as HTML.
function buildDateRow(prefix, index) {
    const inputId = 'proposed-date-added-' + (++addedRowCount);

    const item = document.createElement('div');
    item.className = 'mb-3 proposed-date-item';

    const label = document.createElement('label');
    label.className = 'form-label';
    label.htmlFor = inputId;
    label.textContent = dateLabelText(index);

    const group = document.createElement('div');
    group.className = 'input-group';

    const input = document.createElement('input');
    input.type = 'datetime-local';
    input.id = inputId;
    input.name = prefix + '[' + index + ']';
    input.className = 'form-control';
    input.required = true;
    input.step = '60';

    const remove = document.createElement('button');
    remove.type = 'button';
    remove.className = 'btn btn-danger';
    remove.dataset.action = 'remove-proposed-date';
    remove.setAttribute('aria-label', removeLabelText(index));

    const icon = document.createElement('i');
    icon.className = 'fas fa-trash me-2';
    icon.setAttribute('aria-hidden', 'true');
    remove.append(icon, document.createTextNode('Remove'));

    group.append(input, remove);
    item.append(label, group);
    return item;
}

function addProposedDate() {
    const container = document.getElementById(DATES_CONTAINER_ID);
    if (!container) return;

    const row = buildDateRow(fieldPrefix(container), container.children.length);
    container.appendChild(row);
    row.querySelector(DATE_INPUT_SELECTOR).value = nextDateValue(container, row.querySelector(DATE_INPUT_SELECTOR));
}

function removeProposedDate(button) {
    const container = document.getElementById(DATES_CONTAINER_ID);
    if (!container || container.children.length <= 1) return;

    const row = button.closest('.proposed-date-item');
    if (!row) return;
    row.remove();

    // Names are positional, so the rows that are left are renumbered from zero.
    const prefix = fieldPrefix(container);
    container.querySelectorAll('.proposed-date-item').forEach(function (item, index) {
        const label = item.querySelector('.form-label');
        const visibleInput = item.querySelector(DATE_INPUT_SELECTOR);
        const hiddenInput = item.querySelector('input[type="hidden"]');
        const removeButton = item.querySelector('[data-action="remove-proposed-date"]');

        if (label) label.textContent = dateLabelText(index);
        if (visibleInput) visibleInput.name = prefix + '[' + index + ']';
        if (hiddenInput) hiddenInput.name = prefix + '[' + index + ']';
        if (removeButton) removeButton.setAttribute('aria-label', removeLabelText(index));
    });
}

document.addEventListener('click', function (e) {
    const trigger = e.target instanceof Element ? e.target.closest('[data-action]') : null;
    if (!trigger) return;

    if (trigger.dataset.action === 'add-proposed-date') {
        addProposedDate();
    } else if (trigger.dataset.action === 'remove-proposed-date') {
        removeProposedDate(trigger);
    }
});

// Scoped to the quest form specifically: document.querySelector('form') would instead match the
// navbar's Logout form, which renders earlier in the DOM on every authenticated page (see
// _Layout.cshtml and _Layout.Mobile.cshtml). Pages that are not quest forms but still render the
// shared scripts match nothing here and are left alone.
const form = document.querySelector('form[action*="/Quest/"]');

if (form) {
    form.addEventListener('submit', function (e) {
        // No #proposed-dates container means this form doesn't manage dates at all (a
        // finalized-quest Edit, or a Campaign-board Create/Edit), so there is nothing to require.
        const container = document.getElementById(DATES_CONTAINER_ID);
        if (container) {
            // Existing dates render as hidden inputs (Edit), newly added ones as datetime-local
            // (addProposedDate above); both count toward "has a date".
            const dateInputs = container.querySelectorAll(DATE_VALUE_SELECTOR);
            let hasValidDate = false;

            dateInputs.forEach(function (input) {
                if (input.value) {
                    hasValidDate = true;
                }
            });

            if (!hasValidDate) {
                e.preventDefault();
                alert('Please provide at least one proposed date and time.');
                return false;
            }
        }

        const submitButton = form.querySelector('button[type="submit"]');
        if (submitButton) {
            submitButton.disabled = true;
            const originalText = submitButton.textContent;
            const isEdit = originalText.includes('Update');
            submitButton.textContent = isEdit ? 'Updating Quest...' : 'Creating Quest...';
        }
    });
}
