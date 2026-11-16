// The item details dialog on the shop page.
//
// Each card on the shop page has a name link and a Purchase button that open one shared dialog. Both carry the address
// of the item's details fragment in data-item-url, written by the server. When the dialog starts to open, this module
// empties its body at once, marks it as busy, asks for the fragment and puts it in. Without script the name link is an
// ordinary link to the full details page, so the page still works.
//
// Every opening takes a new number. An answer that arrives after a newer opening, or after the dialog was closed, is
// ignored, so the dialog never shows a different item from the one that was opened last. The fragment is parsed into a
// document that is never attached to the page, any script element in it is dropped, and only its nodes are moved into
// the body: no markup is built from a string and nothing is exposed globally. A failed request leaves the body empty.

const modal = document.getElementById('itemDetailsModal');
const body = document.getElementById('itemDetailsBody');

if (modal && body) {
    let latestRequestId = 0;

    function finish() {
        body.removeAttribute('aria-busy');
    }

    modal.addEventListener('show.bs.modal', function (event) {
        latestRequestId += 1;
        const requestId = latestRequestId;

        body.replaceChildren();

        const holder = event.relatedTarget ? event.relatedTarget.closest('[data-item-url]') : null;
        const url = holder ? holder.dataset.itemUrl : null;
        if (!url) {
            finish();
            return;
        }

        body.setAttribute('aria-busy', 'true');

        fetch(url)
            .then(function (response) {
                if (!response.ok || response.redirected) {
                    throw new Error(`Details request failed: ${response.status}`);
                }
                return response.text();
            })
            .then(function (html) {
                if (requestId !== latestRequestId) {
                    return;
                }

                const parsed = new DOMParser().parseFromString(html, 'text/html');
                parsed.querySelectorAll('script').forEach(function (script) {
                    script.remove();
                });
                body.replaceChildren(...Array.from(parsed.body.childNodes));
                finish();
            })
            .catch(function () {
                if (requestId === latestRequestId) {
                    finish();
                }
            });
    });

    modal.addEventListener('hidden.bs.modal', function () {
        latestRequestId += 1;
        body.replaceChildren();
        finish();
    });
}
