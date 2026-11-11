// Set default date and time to 18:00 for datetime-local inputs
function setDefaultDateTime(input) {
    if (!input || input.value) return; // Don't override existing values

    const now = new Date();
    now.setHours(18, 0, 0, 0); // Set to 18:00:00 today

    // Format as YYYY-MM-DDTHH:MM for datetime-local input
    const year = now.getFullYear();
    const month = String(now.getMonth() + 1).padStart(2, '0');
    const day = String(now.getDate()).padStart(2, '0');
    const hours = String(now.getHours()).padStart(2, '0');
    const minutes = String(now.getMinutes()).padStart(2, '0');

    input.value = `${year}-${month}-${day}T${hours}:${minutes}`;
}

// Clean up datetime value to remove seconds and milliseconds
function cleanDateTimeValue(input) {
    if (!input || !input.value) return;
    
    // Parse the current value and remove seconds/milliseconds
    const date = new Date(input.value);
    if (isNaN(date.getTime())) return;
    
    // Set seconds and milliseconds to 0
    date.setSeconds(0, 0);
    
    // Format as YYYY-MM-DDTHH:MM for datetime-local input
    const year = date.getFullYear();
    const month = String(date.getMonth() + 1).padStart(2, '0');
    const day = String(date.getDate()).padStart(2, '0');
    const hours = String(date.getHours()).padStart(2, '0');
    const minutes = String(date.getMinutes()).padStart(2, '0');
    
    input.value = `${year}-${month}-${day}T${hours}:${minutes}`;
}

// Rewrites every server-rendered <time class="local-time"> element's text to the viewer's own
// browser locale and timezone. The server already rendered the board's own zone as a safe
// first-paint value, so this is a correction pass, not a fill of empty content -- a page with
// no such elements, or a browser that cannot format one of them, is left exactly as the server
// rendered it.
// The one client-side style table, shared by both hydration passes below exactly as the server
// shares a single format dictionary between BuildLocalTime and BuildWallClock. Keep it in sync
// with that dictionary in HtmlHelperExtensions -- the two sides must agree on what each style
// name means. Defining it once means a new style can never reach one pass but not the other.
const TIMESTAMP_FORMATS = {
    'date': { year: 'numeric', month: 'short', day: 'numeric' },
    'date-time': { year: 'numeric', month: 'short', day: 'numeric', hour: 'numeric', minute: '2-digit' },
    'date-compact': { month: 'short', day: 'numeric' },
    'date-time-compact': { month: 'short', day: 'numeric', hour: 'numeric', minute: '2-digit' },
};

function hydrateLocalTimes() {
    const elements = document.querySelectorAll('time.local-time[datetime]');
    elements.forEach(el => {
        try {
            const style = el.getAttribute('data-style');
            const options = TIMESTAMP_FORMATS[style];
            if (!options) {
                // Unknown or missing style: leave the server-rendered board-zone text alone
                // rather than guessing at a default granularity.
                return;
            }
            el.textContent = new Intl.DateTimeFormat(undefined, options).format(new Date(el.getAttribute('datetime')));
        } catch {
            // A malformed datetime or an Intl throw skips this element only -- one bad value
            // must never abort hydration for the rest of the page.
        }
    });
}

// Rewrites every server-rendered <time class="wall-clock"> element's text to the viewer's own
// browser locale -- but never their timezone, because a wall-clock value (a game night, a
// proposed date) has no UTC instant to convert. The datetime attribute's year/month/day/hour/
// minute components are re-anchored through Date.UTC(...) and formatted with timeZone: 'UTC',
// which makes the displayed components arithmetically identical to the parsed ones -- the
// viewer's own zone cannot enter the calculation at all. A page with no such elements, or a
// browser that cannot format one of them, is left exactly as the server rendered it.
function hydrateWallClockTimes() {
    const elements = document.querySelectorAll('time.wall-clock[datetime]');
    elements.forEach(el => {
        try {
            const style = el.getAttribute('data-style');
            const options = TIMESTAMP_FORMATS[style];
            if (!options) {
                // Unknown or missing style: leave the server-rendered invariant-culture text
                // alone rather than guessing at a default granularity.
                return;
            }

            const m = /^(\d{4})-(\d{2})-(\d{2})T(\d{2}):(\d{2})$/.exec(el.getAttribute('datetime'));
            if (!m) {
                // A datetime that does not match the expected floating-local shape is left
                // untouched rather than handed to the Date constructor, which would silently
                // reinterpret it.
                return;
            }

            // Anchor in UTC and format in UTC: the viewer's own zone is structurally unable to
            // shift these components. Do NOT construct the Date from separate local
            // year/month/day/hour/minute arguments -- that builds the value in the viewer's own
            // local zone and can shift it across a DST gap.
            const anchored = new Date(Date.UTC(+m[1], +m[2] - 1, +m[3], +m[4], +m[5]));
            el.textContent = new Intl.DateTimeFormat(undefined, { ...options, timeZone: 'UTC' }).format(anchored);
        } catch {
            // A malformed datetime or an Intl throw skips this element only -- one bad value
            // must never abort hydration for the rest of the page.
        }
    });
}

// Make date blocks clickable for radio selection
function makeDataOptionsClickable() {
    // Handle Details page custom radio buttons
    const dateOptions = document.querySelectorAll('.date-option');
    dateOptions.forEach(dateOption => {
        dateOption.addEventListener('click', function(e) {
            // Prevent double-clicking on actual radio buttons/labels
            if (e.target.matches('input[type="radio"], .custom-radio-label, .custom-radio-label *')) {
                return;
            }
            
            // Find the first radio button in this date option
            const radioButtons = this.querySelectorAll('input[type="radio"]');
            if (radioButtons.length > 0) {
                // For custom radio groups, select the "Yes" option (value="2")
                const yesRadio = this.querySelector('input[type="radio"][value="2"]');
                if (yesRadio) {
                    yesRadio.checked = true;
                    yesRadio.dispatchEvent(new Event('change', { bubbles: true }));
                }
            }
        });
    });
    
    // Handle Manage page radio buttons
    const manageDateOptions = document.querySelectorAll('.manage-date-option');
    manageDateOptions.forEach(dateOption => {
        dateOption.addEventListener('click', function(e) {
            // Prevent double-clicking on actual radio buttons/labels
            if (e.target.matches('input[type="radio"], .form-check-label')) {
                return;
            }
            
            // Find the radio button in this date option
            const radioButton = this.querySelector('input[type="radio"]');
            if (radioButton) {
                radioButton.checked = true;
                radioButton.dispatchEvent(new Event('change', { bubbles: true }));
            }
        });
    });
}

// Locks a form's submit buttons the moment it is submitted, so a second click cannot send the same
// request twice. Opt in with the data-disable-on-submit attribute on the form: a page's CSP forbids
// an inline onsubmit handler, so the behaviour lives here instead. A page restored from the
// browser's back-forward cache keeps its old DOM, so the buttons are unlocked again on restore.
function disableSubmitButtonsOnSubmit() {
    const forms = document.querySelectorAll('form[data-disable-on-submit]');
    forms.forEach(form => {
        form.addEventListener('submit', () => {
            form.querySelectorAll('button[type="submit"], input[type="submit"]').forEach(button => {
                button.disabled = true;
            });
        });
    });
}

window.addEventListener('pageshow', event => {
    if (!event.persisted) {
        return;
    }

    document.querySelectorAll('form[data-disable-on-submit] button[type="submit"], form[data-disable-on-submit] input[type="submit"]')
        .forEach(button => {
            button.disabled = false;
        });
});

// Initialize on page load
document.addEventListener('DOMContentLoaded', function() {
    // Handle datetime-local inputs
    const datetimeInputs = document.querySelectorAll('input[type="datetime-local"]');
    datetimeInputs.forEach(input => {
        // For edit pages, clean existing values to remove seconds/milliseconds
        if (input.value) {
            cleanDateTimeValue(input);
        } else {
            // For create pages, set default time
            setDefaultDateTime(input);
        }
    });
    
    // Make date options clickable
    makeDataOptionsClickable();

    // Lock opted-in forms against a second submit.
    disableSubmitButtonsOnSubmit();

    // Rewrite every server-rendered real-instant timestamp to the viewer's own timezone.
    hydrateLocalTimes();

    // Rewrite every server-rendered wall-clock (floating local time) timestamp to the viewer's
    // own locale wording -- never their timezone, since a wall-clock value has no UTC instant.
    hydrateWallClockTimes();

    // An offcanvas panel does not report its state on the button that opens it (a collapse does), so a
    // toggler that declares aria-expanded is kept in step here.
    ['show', 'hidden'].forEach(function(phase) {
        document.addEventListener(phase + '.bs.offcanvas', function(event) {
            if (!event.target.id) return;
            document.querySelectorAll('[data-bs-toggle="offcanvas"][aria-expanded]').forEach(function(toggler) {
                if (toggler.getAttribute('data-bs-target') === '#' + event.target.id) {
                    toggler.setAttribute('aria-expanded', phase === 'show' ? 'true' : 'false');
                }
            });
        });
    });

    // Initialize toasts
    const toastElements = document.querySelectorAll('.toast');
    toastElements.forEach(function(toastElement) {
        const toast = new bootstrap.Toast(toastElement);
        toast.show();
    });

    // Initialize Bootstrap tooltips (e.g. the Markdown paragraph-break hint) app-wide -- Bootstrap
    // does not auto-activate tooltips, they require this one-time explicit init per element.
    const tooltipElements = document.querySelectorAll('[data-bs-toggle="tooltip"]');
    tooltipElements.forEach(function(tooltipElement) {
        new bootstrap.Tooltip(tooltipElement);
    });
});