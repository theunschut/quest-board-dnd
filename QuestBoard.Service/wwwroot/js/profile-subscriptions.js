// Behaviour for the calendar subscription list on the Profile page. The desktop and the phone layout both load this one
// module so the two cannot drift: Copy, the Add button's pending label, the Rename dialog fill and the Delete dialog.
//
// Everything here is an enhancement of markup that already works without it. Each lookup is a no-op when its element is
// absent, so the module is safe on a page with no subscriptions, and with the script missing the Delete button simply
// posts its own form.

const COPY_RESTORE_MS = 2000;

const status = document.querySelector('[data-copy-status]');

function announce(message) {
    if (status) {
        status.textContent = message;
    }
}

// ---- Copy ----------------------------------------------------------------------------------------------------------

// The role class a button wears at rest, which Copy swaps for the success class while it shows its result.
const ROLE_CLASS = /\bbtn-(?:primary|secondary|warning|danger|info|light|dark)\b/;

document.querySelectorAll('.calendar-subscription-copy-btn').forEach(function (button) {
    // Captured once, so every button returns to exactly its own rest markup: the phone's icon-only Copy stays icon-only.
    const restHtml = button.innerHTML;
    const restClass = button.className;
    const iconOnly = button.textContent.trim() === '';
    let restoreTimer = 0;

    const restoreSoon = function () {
        window.clearTimeout(restoreTimer);
        restoreTimer = window.setTimeout(function () {
            button.className = restClass;
            button.innerHTML = restHtml;
            // Cleared so the next copy changes the text again and is announced again.
            announce('');
        }, COPY_RESTORE_MS);
    };

    const showManualFallback = function (field) {
        // The address is hidden by default because it is a bearer credential and nothing on the page needs a person to
        // read it. This is the one path that does: reveal it so the selection is visible, then leave it revealed, because
        // the person is mid-copy and hiding it again under them would be hostile.
        field.classList.remove('visually-hidden');
        field.focus();
        field.select();
        if (!iconOnly) {
            button.textContent = 'Press Ctrl+C';
        }
        announce('Press Ctrl+C to copy');
        restoreSoon();
    };

    button.addEventListener('click', function () {
        const targetSelector = button.dataset.addressTarget;
        const field = targetSelector ? document.querySelector(targetSelector) : null;
        if (!field) {
            return;
        }

        if (!navigator.clipboard || !navigator.clipboard.writeText) {
            showManualFallback(field);
            return;
        }

        navigator.clipboard.writeText(field.value).then(function () {
            button.className = restClass.replace(ROLE_CLASS, 'btn-success');
            button.innerHTML = iconOnly
                ? '<i class="fas fa-check" aria-hidden="true"></i>'
                : '<i class="fas fa-check me-2" aria-hidden="true"></i>Copied!';
            announce('Copied!');
            restoreSoon();
        }).catch(function () {
            showManualFallback(field);
        });
    });
});

// ---- Add -----------------------------------------------------------------------------------------------------------

// The disable-and-relabel here is a courtesy for the common case; the actual guard against a double-submitted mint is the
// server-side ten-second window enforced in AccountController, so removing this script would not remove the protection.
const addSubscriptionForm = document.querySelector('.calendar-subscription-add-form');
if (addSubscriptionForm) {
    addSubscriptionForm.addEventListener('submit', function () {
        const submitButton = addSubscriptionForm.querySelector('button[type="submit"]');
        if (submitButton) {
            submitButton.disabled = true;
            submitButton.textContent = 'Adding…';
        }
    });
}

// ---- Rename --------------------------------------------------------------------------------------------------------

const renameDialog = document.getElementById('renameSubscriptionModal');
if (renameDialog) {
    renameDialog.addEventListener('show.bs.modal', function (event) {
        const trigger = event.relatedTarget;
        if (!trigger) {
            return;
        }

        document.getElementById('renameSubscriptionId').value = trigger.dataset.subscriptionId || '';
        document.getElementById('renameSubscriptionName').value = trigger.dataset.subscriptionName || '';
    });

    renameDialog.addEventListener('shown.bs.modal', function () {
        document.getElementById('renameSubscriptionName').focus();
    });
}

// ---- Delete --------------------------------------------------------------------------------------------------------

// Only this module opens the delete dialog. Each row's Delete is a real submit button in its own form, so with no script
// (or no Bootstrap) it posts the revoke exactly as before. Here the submit is cancelled and the dialog opened in the same
// listener, which is what guarantees the dialog is never on screen while a revoke is on its way. Opening through the
// Modal API means Bootstrap will not return focus to the button by itself, so it is done on close below.
const deleteDialog = document.getElementById('deleteSubscriptionModal');
let deleteOpener = null;

document.querySelectorAll('.calendar-subscription-delete-form').forEach(function (form) {
    form.addEventListener('submit', function (event) {
        const Modal = window.bootstrap && window.bootstrap.Modal;
        if (!deleteDialog || !Modal) {
            return;
        }

        event.preventDefault();
        deleteOpener = event.submitter || form.querySelector('button[type="submit"]');
        Modal.getOrCreateInstance(deleteDialog).show(deleteOpener);
    });
});

if (deleteDialog) {
    deleteDialog.addEventListener('show.bs.modal', function (event) {
        const trigger = event.relatedTarget;
        if (!trigger) {
            return;
        }

        // The name is a member-supplied value: it only ever becomes text, never markup.
        deleteDialog.querySelector('[data-subscription-name-target]').textContent = trigger.dataset.subscriptionName || '';
        deleteDialog.querySelector('input[name="id"]').value = trigger.dataset.subscriptionId || '';
    });

    deleteDialog.addEventListener('shown.bs.modal', function () {
        const keep = deleteDialog.querySelector('[data-keep-subscription]');
        if (keep) {
            keep.focus();
        }
    });

    deleteDialog.addEventListener('hidden.bs.modal', function () {
        if (deleteOpener && deleteOpener.isConnected) {
            deleteOpener.focus();
        }
        deleteOpener = null;
    });
}
