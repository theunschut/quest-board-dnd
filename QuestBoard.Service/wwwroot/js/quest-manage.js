// Quest Manage page module, shared by the desktop and phone layouts. Every action is wired from data
// attributes the views render; nothing is exposed globally and no markup is built from strings.

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

const ACTIONS = {
    'delete-quest': deleteQuest
};

document.addEventListener('click', function (event) {
    const target = event.target instanceof Element ? event.target.closest('[data-action]') : null;
    const handler = target ? ACTIONS[target.dataset.action] : null;
    if (handler) {
        handler(target);
    }
});
