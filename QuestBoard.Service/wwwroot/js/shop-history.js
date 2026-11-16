// My Transactions on the shop page: the toggle is a disclosure button. It flips aria-expanded and the hidden attribute of
// the pane it controls, so a collapsed pane holds no tab stop and the page opens with the pane closed every time. Focus
// stays on the toggle. Nothing here builds markup, and nothing is remembered between visits.

document.querySelectorAll('[data-history-toggle]').forEach(function (button) {
    const pane = document.getElementById(button.getAttribute('aria-controls'));
    if (!pane) {
        return;
    }

    button.addEventListener('click', function () {
        const open = button.getAttribute('aria-expanded') === 'true';
        button.setAttribute('aria-expanded', open ? 'false' : 'true');
        pane.hidden = open;
    });
});
