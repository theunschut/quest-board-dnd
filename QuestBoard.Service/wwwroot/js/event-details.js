// Event Details page module, shared by the desktop and phone layouts: answering availability,
// withdrawing the answer on a one-shot board, and the confirmation that guards the manager's
// delete, cancel and restore forms. Everything is wired from data attributes the view renders
// (data-answer and data-answer-url, data-withdraw-url, data-confirm-message); nothing is exposed
// globally and no markup is built from strings. The confirmation and error wording is shown
// with the browser's own confirm() and alert().

function antiforgeryToken() {
    const meta = document.querySelector('meta[name="request-verification-token"]');
    return meta ? meta.content : '';
}

// Both requests answer with the page's own result: a reload on success, otherwise the server's
// sentence in an alert, and a generic sentence when the request itself could not be made.
function submitAnswer(url, method, fields, failedPrefix, errorMessage) {
    const formData = new FormData();
    formData.append('__RequestVerificationToken', antiforgeryToken());
    Object.keys(fields).forEach(function (name) {
        formData.append(name, fields[name]);
    });

    fetch(url, { method: method, body: formData })
        .then(function (res) {
            if (res.ok) {
                location.reload();
            } else {
                res.text().then(function (text) {
                    alert(failedPrefix + text);
                });
            }
        })
        .catch(function () {
            alert(errorMessage);
        });
}

function setAvailability(button) {
    submitAnswer(
        button.dataset.answerUrl,
        'POST',
        { availability: button.dataset.answer },
        'Failed to set availability: ',
        'An error occurred while setting availability.');
}

function withdrawAvailability(button) {
    if (!confirm(button.dataset.confirmMessage)) {
        return;
    }

    submitAnswer(
        button.dataset.withdrawUrl,
        'DELETE',
        {},
        'Failed to withdraw availability: ',
        'An error occurred while withdrawing availability.');
}

document.querySelectorAll('[data-answer]').forEach(function (button) {
    button.addEventListener('click', function () {
        setAvailability(button);
    });
});

document.querySelectorAll('[data-withdraw-url]').forEach(function (button) {
    button.addEventListener('click', function () {
        withdrawAvailability(button);
    });
});

// The delete, cancel and restore forms each carry the sentence to confirm; declining stops the post.
document.querySelectorAll('form[data-confirm-message]').forEach(function (form) {
    form.addEventListener('submit', function (event) {
        if (!confirm(form.dataset.confirmMessage)) {
            event.preventDefault();
        }
    });
});
