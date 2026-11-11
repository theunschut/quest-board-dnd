// Submit guard for the quest forms. A module runs after the document is parsed, so the form is
// already in the DOM when this executes.

// Scoped to the quest form specifically: document.querySelector('form') would instead match the
// navbar's Logout form, which renders earlier in the DOM on every authenticated page (see
// _Layout.cshtml and _Layout.Mobile.cshtml). Pages that are not quest forms but still render the
// shared scripts match nothing here and are left alone.
const form = document.querySelector('form[action*="/Quest/"]');

if (form) {
    form.addEventListener('submit', function (e) {
        // No #proposed-dates container means this form doesn't manage dates at all (a
        // finalized-quest Edit, or a Campaign-board Create/Edit), so there is nothing to require.
        const container = document.getElementById('proposed-dates');
        if (container) {
            // Existing dates render as hidden inputs (Edit), newly added ones as datetime-local
            // (addProposedDate in site.js); both count toward "has a date".
            const dateInputs = container.querySelectorAll('input[type="datetime-local"], input[type="hidden"][name*="ProposedDates"]');
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
