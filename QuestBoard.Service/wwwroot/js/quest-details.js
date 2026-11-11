// Quest Details page module, shared by the desktop and phone layouts: the signup form's role
// select, revoking a signup, changing a vote, the update form toggle, the join forms'
// character copy and the character-select modal. Everything is wired from data attributes and
// field names the views render; nothing is exposed globally and no markup is built from
// strings (nodes are created with createElement and textContent).

const SPECTATOR_ROLE = '1';
const SPECTATOR_MESSAGE = 'As a spectator, you will be automatically approved. No date voting required.';

function antiforgeryToken() {
    const meta = document.querySelector('meta[name="request-verification-token"]');
    return meta ? meta.content : '';
}

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
            instruction.replaceChildren(icon, document.createTextNode(SPECTATOR_MESSAGE));
            instruction.className = 'alert alert-info mb-3';
        } else {
            instruction.replaceChildren(document.createTextNode(originalText));
            instruction.className = originalClass;
        }
    }

    select.addEventListener('change', apply);

    // A browser can restore the select's value (Back navigation, soft reload) without firing
    // change, so the state is applied once now and again when the page is shown.
    apply();
    window.addEventListener('pageshow', apply);
}

// The character picked above the three join forms is copied into each form's hidden field, so
// whichever role button is pressed posts the same character.
function initJoinCharacter(source) {
    function copy() {
        document.querySelectorAll('[data-join-character-target]').forEach(function (target) {
            target.value = source.value;
        });
    }

    source.addEventListener('change', copy);

    // A restored select value does not fire change, so the copy also runs at start-up and on
    // page show to keep the hidden fields in step with what the select displays.
    copy();
    window.addEventListener('pageshow', copy);
}

function setUpdateFormOpen(open) {
    const view = document.getElementById('viewSignupCalendar');
    const update = document.getElementById('updateSignupForm');
    if (view) {
        view.hidden = open;
    }
    if (update) {
        update.hidden = !open;
    }
    document.querySelectorAll('[data-action="show-update-form"]').forEach(function (button) {
        button.hidden = open;
    });
}

function revokeSignup(button) {
    const message = button.dataset.confirmMessage || '';
    if (!confirm(message)) {
        return;
    }

    const formData = new FormData();
    formData.append('__RequestVerificationToken', antiforgeryToken());

    fetch(`/Quest/RevokeSignup/${button.dataset.questId}`, {
        method: 'DELETE',
        body: formData
    }).then(function (res) {
        if (res.ok) {
            location.reload();
        } else {
            res.text().then(function (text) {
                alert(`Failed to revoke signup: ${text}`);
            });
        }
    }).catch(function () {
        alert('An error occurred while revoking signup.');
    });
}

function changeVote(button) {
    const formData = new FormData();
    formData.append('__RequestVerificationToken', antiforgeryToken());
    formData.append('vote', button.dataset.vote);

    fetch(`/Quest/ChangeVote/${button.dataset.questId}`, {
        method: 'POST',
        body: formData
    }).then(function (res) {
        if (res.ok) {
            location.reload();
        } else {
            res.text().then(function (text) {
                alert(`Failed to change vote: ${text}`);
            });
        }
    }).catch(function () {
        alert('An error occurred while changing vote.');
    });
}

const ACTIONS = {
    'revoke-signup': revokeSignup,
    'change-vote': changeVote,
    'show-update-form': function () { setUpdateFormOpen(true); },
    'hide-update-form': function () { setUpdateFormOpen(false); }
};

document.addEventListener('click', function (event) {
    const target = event.target instanceof Element ? event.target.closest('[data-action]') : null;
    const handler = target ? ACTIONS[target.dataset.action] : null;
    if (handler) {
        handler(target);
    }
});

// One modal instance serves every row that offers a character change; each trigger primes it
// from its own data attributes when it opens.
function initCharacterModal(modal) {
    const select = document.getElementById('characterSelect');
    const questIdInput = document.getElementById('characterSelectQuestId');
    const removeButton = document.getElementById('characterSelectRemoveBtn');
    const title = document.getElementById('characterSelectModalTitle');
    const form = document.getElementById('characterSelectForm');
    if (!select || !questIdInput || !removeButton || !title || !form) {
        return;
    }

    modal.addEventListener('show.bs.modal', function (event) {
        const trigger = event.relatedTarget;
        if (!trigger) {
            return;
        }

        const currentCharacterId = trigger.dataset.currentCharacterId || '';
        const currentCharacterLabel = trigger.dataset.currentCharacterLabel || '';

        questIdInput.value = trigger.dataset.questId || '';

        // A previous open may have left the select disabled and not required from the Remove
        // path, and may have injected a stand-in option for a different row's current
        // character. Reset both before priming for this trigger.
        select.disabled = false;
        select.required = true;
        const previousCurrentOption = document.getElementById('characterSelectCurrentOption');
        if (previousCurrentOption) {
            previousCurrentOption.remove();
        }

        if (currentCharacterId) {
            let matchingOption = Array.from(select.options).find(function (option) {
                return option.value === currentCharacterId;
            });

            if (!matchingOption) {
                // The signup's current character is not in the option list (for example it
                // belongs to another board's roster). Inject it as a real option rather than
                // assigning select.value with no matching option, which would fall back to the
                // placeholder and make an untouched Save wipe the player's character.
                matchingOption = document.createElement('option');
                matchingOption.id = 'characterSelectCurrentOption';
                matchingOption.value = currentCharacterId;
                matchingOption.textContent = currentCharacterLabel || 'Current character';
                select.options[0].after(matchingOption);
            }

            select.value = currentCharacterId;
        } else {
            select.value = '';
        }

        removeButton.hidden = !currentCharacterId;
        title.textContent = currentCharacterId
            ? 'Change Character on Your Signup'
            : 'Add Character to Signup';
    });

    removeButton.addEventListener('click', function () {
        if (!confirm('Remove this character from your signup?')) {
            return;
        }

        select.required = false;
        select.value = '';
        // A disabled control is excluded from form submission, so characterId arrives absent
        // on the server and binds to null, which is the existing clear path. Submitting the
        // form directly also bypasses constraint validation on the emptied select.
        select.disabled = true;
        form.submit();
    });
}

document.querySelectorAll('select[name="selectedRole"]').forEach(initRoleSelect);
document.querySelectorAll('[data-join-character-source]').forEach(initJoinCharacter);

const characterModal = document.getElementById('characterSelectModal');
if (characterModal) {
    initCharacterModal(characterModal);
}
