// Class rows for the character create and edit forms. The page renders the rows the character already has, one
// <template id="class-row-template"> holding a blank row, and an Add Another Class button; this module clones the
// template when the button is pressed and removes a row when its Remove button is pressed.
//
// The model binder reads the rows as a list named Classes[0], Classes[1] and so on, and stops at the first gap, so
// after every add or remove the rows present are numbered again from zero. A form with no stored classes therefore
// starts its new rows at the right index. Only the name, id and for values are rewritten; no markup is built from
// strings and nothing is exposed globally.

const list = document.getElementById('class-list');
const template = document.getElementById('class-row-template');
const addButton = document.getElementById('add-class');

if (list && template instanceof HTMLTemplateElement && addButton) {
    addButton.addEventListener('click', () => {
        const row = template.content.firstElementChild.cloneNode(true);
        list.appendChild(row);
        renumber();
        const first = row.querySelector('select, input');
        if (first) {
            first.focus();
        }
    });

    list.addEventListener('click', event => {
        const remove = event.target instanceof Element ? event.target.closest('.remove-class') : null;
        if (!remove) {
            return;
        }

        const row = remove.closest('[data-class-row]');
        if (row && list.contains(row)) {
            row.remove();
            renumber();
            // The pressed button is gone, so focus would fall back to the page; keep it on the Add button.
            addButton.focus();
        }
    });
}

// Gives each row present its position as the index in its control names, ids and label targets.
function renumber() {
    const rows = list.querySelectorAll('[data-class-row]');
    rows.forEach((row, index) => {
        row.querySelectorAll('[name], [id], [for]').forEach(element => {
            for (const attribute of ['name', 'id', 'for']) {
                const value = element.getAttribute(attribute);
                if (value !== null) {
                    element.setAttribute(attribute, withIndex(value, index));
                }
            }
        });
    });
}

function withIndex(value, index) {
    return value.replace(/__index__/g, String(index)).replace(/Classes\[\d+\]/g, `Classes[${index}]`);
}
