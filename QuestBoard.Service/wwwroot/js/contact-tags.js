// Shared client-side enhancement for the contact tag entry field. Loaded per-view via a plain
// <script> include (matching site.js's no-module, no-bundler convention) and initialized per-view
// by calling initContactTags({...}) with that view's element id and suggestion list.

// One reusable initializer any contact create/edit view can call with its own element id and
// whitelist. The underlying <input> is expected to already exist in the view -- this file only
// wires behavior to it.
function initContactTags(config) {
    config = config || {};
    const inputId = config.inputId;
    const whitelist = config.whitelist || [];

    const input = inputId ? document.getElementById(inputId) : null;
    if (!input) {
        // Nothing to wire on this page -- safe no-op so the same script can be included
        // defensively without checking which view is currently rendered.
        return;
    }

    new Tagify(input, {
        whitelist: whitelist,
        enforceWhitelist: false, // a DM can type a genuinely new tag, not just pick from the list
        maxTags: undefined, // no hard cap on tags per contact
        // Keeps the underlying input holding a plain comma-and-space-joined string on every
        // change, so the enhanced and unenhanced (no-JS) submission paths post the exact same
        // value shape and the server only ever has to parse one format.
        originalInputValueFormat: values => values.map(function (v) { return v.value; }).join(', ')
    });
}
