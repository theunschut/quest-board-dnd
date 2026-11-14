// Tag entry for the contact create and edit forms. The module switches itself on when the page has
// a tag input carrying a suggestion list, so the four contact forms share it without any call from
// the view.
//
// The page supplies the board's own tag names as a JSON array in the data-whitelist attribute of
// the #TagsInput element (Razor encodes the attribute) and loads the self-hosted Tagify script
// before this file. A missing or unreadable list falls back to no suggestions, and a page without
// the input is left untouched. Nothing is exposed globally and no markup is built from strings.
//
// Tagify reads an attribute of this very name itself, splitting it on commas, which would cut the
// JSON array into fragments and replace the list handed to it below. So the module takes the
// attribute off the input before it builds Tagify.

const input = document.querySelector('#TagsInput[data-whitelist]');

if (input && typeof Tagify === 'function') {
    const whitelist = readWhitelist(input.getAttribute('data-whitelist'));
    input.removeAttribute('data-whitelist');

    new Tagify(input, {
        whitelist: whitelist,
        enforceWhitelist: false, // a DM can type a genuinely new tag, not just pick from the list
        maxTags: undefined, // no hard cap on tags per contact
        // Keeps the underlying input holding a plain comma-and-space-joined string on every
        // change, so the enhanced and unenhanced (no-JS) submission paths post the exact same
        // value shape and the server only ever has to parse one format.
        originalInputValueFormat: values => values.map(v => v.value).join(', ')
    });
}

function readWhitelist(raw) {
    try {
        const parsed = JSON.parse(raw);
        return Array.isArray(parsed) ? parsed.filter(name => typeof name === 'string') : [];
    } catch {
        return [];
    }
}
