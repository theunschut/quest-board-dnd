// Quest Details page module: the signup form's role select. Everything is wired from data
// attributes and form field names rendered by the views; nothing is exposed globally and no
// markup is ever built from strings (nodes are created with createElement and textContent).

const SPECTATOR_ROLE = '1';
const SPECTATOR_MESSAGE = 'As a spectator, you will be automatically approved. No date voting required.';

// Choosing Spectator hides the date voting of that signup form only. The wrapper around the
// calendar months is the one element toggled, so no other card on the page is ever affected,
// and the hidden vote inputs stay in the form and keep posting.
function initRoleSelect(select) {
    const form = select.closest('form');
    if (!form) {
        return;
    }

    const voteSection = form.querySelector('[data-vote-section]');
    const instruction = form.querySelector('#votingInstruction');
    if (!voteSection) {
        return;
    }

    const originalText = instruction ? instruction.textContent : '';
    const originalClass = instruction ? instruction.className : '';

    function apply() {
        const isSpectator = select.value === SPECTATOR_ROLE;
        voteSection.hidden = isSpectator;

        if (!instruction) {
            return;
        }

        if (isSpectator) {
            const icon = document.createElement('i');
            icon.className = 'fas fa-circle-info me-2';
            icon.setAttribute('aria-hidden', 'true');
            const text = document.createTextNode(SPECTATOR_MESSAGE);
            instruction.replaceChildren(icon, text);
            instruction.className = 'alert alert-info mb-3';
        } else {
            instruction.replaceChildren(document.createTextNode(originalText));
            instruction.className = originalClass;
        }
    }

    select.addEventListener('change', apply);
}

document.querySelectorAll('select[name="selectedRole"]').forEach(initRoleSelect);
