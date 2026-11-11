// Quest Manage page module, shared by the desktop and phone layouts. Every action is wired from data
// attributes the views render; nothing is exposed globally and no markup is built from strings: the
// availability status is a span made with createElement and textContent, and a player's name only ever
// reaches the remove confirmation as plain text read from a data attribute.

function antiforgeryToken() {
    const meta = document.querySelector('meta[name="request-verification-token"]');
    return meta ? meta.content : '';
}

function deleteQuest(button) {
    if (!confirm(button.dataset.confirmMessage || '')) {
        return;
    }

    fetch(`/Quest/Delete/${button.dataset.questId}`, {
        method: 'DELETE',
        headers: {
            'RequestVerificationToken': antiforgeryToken()
        }
    }).then(function (res) {
        if (res.ok) {
            window.location.href = window.location.origin;
        } else {
            alert('Delete failed.');
        }
    });
}

function removePlayer(button) {
    const playerName = button.dataset.playerName || '';
    if (!confirm(`Are you sure you want to remove ${playerName} from this quest? This will delete their signup and all associated votes.`)) {
        return;
    }

    fetch(`/Quest/RemovePlayerSignup/${button.dataset.signupId}`, {
        method: 'DELETE',
        headers: {
            'RequestVerificationToken': antiforgeryToken()
        }
    }).then(async function (res) {
        if (res.ok) {
            window.location.reload();
        } else {
            const responseText = await res.text();
            console.error('Response status:', res.status);
            console.error('Response body:', responseText);
            alert(`Failed to remove player. Status: ${res.status}. Check console for details.`);
        }
    }).catch(function (err) {
        console.error('Error removing player:', err);
        alert('An error occurred while removing the player.');
    });
}

const ACTIONS = {
    'delete-quest': deleteQuest,
    'remove-player': removePlayer,
    'reload': function () { window.location.reload(); }
};

document.addEventListener('click', function (event) {
    const target = event.target instanceof Element ? event.target.closest('[data-action]') : null;
    const handler = target ? ACTIONS[target.dataset.action] : null;
    if (handler) {
        handler(target);
    }
});

// A submit button that names a confirmation sentence only submits once the reader agrees.
document.addEventListener('submit', function (event) {
    const submitter = event.submitter;
    const message = submitter && submitter.dataset ? submitter.dataset.confirmMessage : '';
    if (message && !confirm(message)) {
        event.preventDefault();
    }
});

function voteIds(value) {
    return value ? value.split(',').filter(function (id) { return id; }) : [];
}

// What each vote shows beside a player: the text colour class and the words.
const AVAILABILITY = {
    yes: { cssClass: 'text-success', text: '- Available (Yes)', checked: true },
    maybe: { cssClass: 'text-warning', text: '- Maybe available', checked: true },
    no: { cssClass: 'text-danger', text: '- Not available (No)', checked: false },
    none: { cssClass: 'text-muted', text: '- No vote', checked: false }
};

function updatePlayerAvailability() {
    const selected = document.querySelector('.date-selector:checked');
    if (!selected) {
        return;
    }

    const dateContainer = selected.closest('.date-option');
    if (!dateContainer) {
        return;
    }

    const yesVotes = voteIds(dateContainer.dataset.yesVotes);
    const maybeVotes = voteIds(dateContainer.dataset.maybeVotes);
    const noVotes = voteIds(dateContainer.dataset.noVotes);

    document.querySelectorAll('.player-item').forEach(function (playerItem) {
        const playerId = playerItem.dataset.playerId;
        const checkbox = playerItem.querySelector('.player-checkbox');
        const statusSpan = playerItem.querySelector('.availability-status');
        if (!checkbox || !statusSpan) {
            return;
        }

        let state = AVAILABILITY.none;
        if (yesVotes.includes(playerId)) {
            state = AVAILABILITY.yes;
        } else if (maybeVotes.includes(playerId)) {
            state = AVAILABILITY.maybe;
        } else if (noVotes.includes(playerId)) {
            state = AVAILABILITY.no;
        }

        const status = document.createElement('span');
        status.className = state.cssClass;
        status.textContent = state.text;
        statusSpan.replaceChildren(status);

        checkbox.checked = state.checked;
    });
}

// Only Player role checkboxes count toward the quest's player limit.
function initPlayerCount(checkbox, maxPlayers) {
    checkbox.addEventListener('change', function () {
        const selectedPlayerCount = Array.from(document.querySelectorAll('.player-checkbox:checked'))
            .filter(function (box) { return box.closest('[data-role="player"]'); })
            .length;

        if (selectedPlayerCount > maxPlayers) {
            checkbox.checked = false;
            alert('Cannot select more than ' + maxPlayers + ' players for this quest.');
        }
    });
}

updatePlayerAvailability();

document.querySelectorAll('.date-selector').forEach(function (radio) {
    radio.addEventListener('change', updatePlayerAvailability);
});

const limitSource = document.querySelector('[data-max-players]');
const maxPlayers = limitSource ? Number(limitSource.dataset.maxPlayers) : NaN;
if (!Number.isNaN(maxPlayers)) {
    document.querySelectorAll('.player-checkbox').forEach(function (checkbox) {
        initPlayerCount(checkbox, maxPlayers);
    });
}
