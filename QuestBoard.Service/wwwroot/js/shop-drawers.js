// The two drawers on the phone shop page, My Transactions and Filter & Sort, are Bootstrap offcanvas panels: Bootstrap
// traps focus inside an open one, closes it on Escape, the Close button or a tap on the backdrop, and puts focus back on
// the button that opened it. It does not keep aria-expanded on that button, so this module mirrors the drawer's state onto
// every button that controls it: true while the drawer is opening or open, false once it has closed. Nothing is built
// from a string, nothing is remembered between visits and nothing is exposed globally.

document.querySelectorAll('.offcanvas[id]').forEach(function (drawer) {
    const openers = document.querySelectorAll(`[aria-controls="${drawer.id}"]`);
    if (openers.length === 0) {
        return;
    }

    function mirror(value) {
        openers.forEach(function (opener) {
            opener.setAttribute('aria-expanded', value);
        });
    }

    drawer.addEventListener('show.bs.offcanvas', function () {
        mirror('true');
    });
    drawer.addEventListener('hidden.bs.offcanvas', function () {
        mirror('false');
    });
});
