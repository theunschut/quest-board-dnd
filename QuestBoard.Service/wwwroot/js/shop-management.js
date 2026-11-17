// The shop management lists: the Deny dialog and the Bulk Actions notice. Both are driven by data attributes the
// server writes into the page, so no markup is built from strings and nothing is exposed globally.
//
// The Deny dialog is shared by every row. When it opens it reads the item's name and the address of that item's Deny
// action from the button that opened it, writes the name as plain text, points the form at the address and clears
// any reason typed for a previous item. The server still decides who may deny what when the form is posted.
//
// Each Bulk Actions button names its action in data-bulk-action and shows the same notice as before: real bulk
// actions are not built yet.

const denyModal = document.getElementById('denyModal');

if (denyModal) {
    denyModal.addEventListener('show.bs.modal', function (event) {
        const trigger = event.relatedTarget;
        if (!trigger || !trigger.dataset.denyUrl) {
            return;
        }

        const name = denyModal.querySelector('[data-deny-item-name]');
        const form = denyModal.querySelector('form');
        const reason = denyModal.querySelector('textarea[name="denialReason"]');

        if (name) {
            name.textContent = trigger.dataset.denyName ?? '';
        }
        if (form) {
            form.action = trigger.dataset.denyUrl;
        }
        if (reason) {
            reason.value = '';
        }
    });
}

document.addEventListener('click', function (event) {
    const button = event.target.closest('[data-bulk-action]');
    if (!button) {
        return;
    }
    alert(`Bulk ${button.dataset.bulkAction} functionality will be available in a future update.`);
});
