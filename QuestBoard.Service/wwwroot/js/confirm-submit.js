// Asks before a form is submitted. Every form that carries data-confirm-message shows that sentence in the
// browser's own confirm box when it is submitted; answering no cancels the submit and nothing is sent. The
// sentence is rendered by the server and handed to confirm() as plain text, so no markup is built from strings
// and nothing is exposed globally.

document.querySelectorAll('form[data-confirm-message]').forEach(function (form) {
    form.addEventListener('submit', function (event) {
        if (!confirm(form.dataset.confirmMessage)) {
            event.preventDefault();
        }
    });
});
