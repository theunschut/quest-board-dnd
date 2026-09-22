// Add additional proposed date input
function addProposedDate() {
    const container = document.getElementById('proposed-dates');
    if (!container) return;
    
    const index = container.children.length;
    
    // Determine the correct field name prefix based on the form context
    // Check if we're in the edit form (has Quest. prefix) or create form (direct ProposedDates)
    // Also check hidden inputs — Edit.cshtml renders existing dates as hidden inputs, not datetime-local
    const existingInput = container.querySelector('input[type="datetime-local"], input[type="hidden"][name*="ProposedDates"]');
    const isEditForm = existingInput && existingInput.name.includes('Quest.ProposedDates');
    const fieldPrefix = isEditForm ? 'Quest.ProposedDates' : 'ProposedDates';
    
    const div = document.createElement('div');
    div.className = 'mb-3 proposed-date-item';
    div.innerHTML = `
        <label class="form-label">Proposed Date ${index + 1}</label>
        <div class="input-group">
            <input type="datetime-local" name="${fieldPrefix}[${index}]" class="form-control" required step="60">
            <button type="button" class="btn btn-danger" onclick="removeProposedDate(this)">
                <i class="fas fa-trash me-1"></i>Remove
            </button>
        </div>
    `;
    
    container.appendChild(div);

    // Pre-fill new input: last filled date + 1 day at 18:00, or today at 18:00
    const newInput = div.querySelector('input[type="datetime-local"]');
    const allInputs = container.querySelectorAll('input[type="datetime-local"]');
    // allInputs now includes the new one; look at all but the last for a prior value
    let baseDate = null;
    for (let i = allInputs.length - 2; i >= 0; i--) {
        if (allInputs[i].value) {
            baseDate = new Date(allInputs[i].value);
            break;
        }
    }
    if (baseDate && !isNaN(baseDate.getTime())) {
        baseDate.setDate(baseDate.getDate() + 1);
        baseDate.setHours(18, 0, 0, 0);
    } else {
        baseDate = new Date();
        baseDate.setHours(18, 0, 0, 0);
    }
    const year = baseDate.getFullYear();
    const month = String(baseDate.getMonth() + 1).padStart(2, '0');
    const day = String(baseDate.getDate()).padStart(2, '0');
    const hours = String(baseDate.getHours()).padStart(2, '0');
    const minutes = String(baseDate.getMinutes()).padStart(2, '0');
    newInput.value = `${year}-${month}-${day}T${hours}:${minutes}`;
}

// Remove proposed date input
function removeProposedDate(button) {
    const container = document.getElementById('proposed-dates');
    if (!container || container.children.length <= 1) return;
    
    button.closest('.proposed-date-item').remove();
    
    // Determine the correct field name prefix based on the form context
    const existingInput = container.querySelector('input[type="datetime-local"], input[type="hidden"][name*="ProposedDates"]');
    const isEditForm = existingInput && existingInput.name.includes('Quest.ProposedDates');
    const fieldPrefix = isEditForm ? 'Quest.ProposedDates' : 'ProposedDates';
    
    // Reindex remaining inputs
    const dateItems = container.querySelectorAll('.proposed-date-item');
    dateItems.forEach((item, index) => {
        const label = item.querySelector('label');
        const visibleInput = item.querySelector('input[type="datetime-local"]');
        const hiddenInput = item.querySelector('input[type="hidden"]');
        
        if (label) label.textContent = `Proposed Date ${index + 1}`;
        if (visibleInput) visibleInput.name = `${fieldPrefix}[${index}]`;
        if (hiddenInput) hiddenInput.name = `${fieldPrefix}[${index}]`;
    });
}


// Calculate optimal number of columns based on container width
function calculateColumns(containerWidth, cardWidth, gap) {
    const minColumns = 1; // Always show at least 1 column
    const maxColumns = Math.floor((containerWidth + gap) / (cardWidth + gap));
    return Math.max(minColumns, maxColumns);
}

// Calculate and set card heights based on image proportions
function setCardHeights() {
    const cards = document.querySelectorAll('.fantasy-quest-card');
    
    cards.forEach(card => {
        const imageWidth = parseInt(card.dataset.imageWidth);
        const imageHeight = parseInt(card.dataset.imageHeight);
        const cardWidth = card.offsetWidth;
        
        if (imageWidth && imageHeight && cardWidth) {
            const proportionalHeight = Math.round(cardWidth * (imageHeight / imageWidth));
            card.style.height = `${proportionalHeight}px`;
        }
    });
}

// JavaScript masonry layout
function layoutMasonry() {
    const container = document.querySelector('.quest-board-container');
    if (!container) return;
    
    const cards = Array.from(container.children);
    if (cards.length === 0) return;
    
    // Reset cards to get natural dimensions
    cards.forEach(card => {
        card.style.position = '';
        card.style.left = '';
        card.style.top = '';
        card.style.width = '';
    });
    
    // Set card heights based on image proportions
    setCardHeights();
    
    // Force a reflow to get updated dimensions
    container.offsetHeight;
    
    const containerWidth = container.offsetWidth;
    
    // Get card width from CSS custom property (handles responsive breakpoints automatically)
    const computedStyle = getComputedStyle(document.documentElement);
    const cardWidthFromCSS = parseInt(computedStyle.getPropertyValue('--card-width').trim());
    
    // Get actual rendered card width (in case CSS media queries changed it)
    const firstCard = cards[0];
    const cardWidth = firstCard ? firstCard.offsetWidth : cardWidthFromCSS || 420;
    
    const gap = 16; // 1rem
    const padding = 16; // 1rem container padding
    
    // Calculate available width inside padding
    const availableWidth = containerWidth - (padding * 2);
    const columnCount = calculateColumns(availableWidth, cardWidth, gap);
    
    // Don't proceed if we can't fit any columns
    if (columnCount <= 0) return;
    
    // Reset container to block layout
    container.style.display = 'block';
    container.style.columnCount = 'auto';
    container.style.position = 'relative';
    
    // Initialize column heights
    const columnHeights = new Array(columnCount).fill(0);
    
    // Center the columns within the available space
    const totalColumnsWidth = (columnCount * cardWidth) + ((columnCount - 1) * gap);
    const leftOffset = Math.max(0, (availableWidth - totalColumnsWidth) / 2) + padding;
    
    // Position each card
    cards.forEach((card, index) => {
        // Find shortest column
        const shortestColumnIndex = columnHeights.indexOf(Math.min(...columnHeights));
        
        // Calculate left position with centering offset
        const leftPosition = leftOffset + (shortestColumnIndex * (cardWidth + gap));
        
        // Position the card
        card.style.position = 'absolute';
        card.style.left = `${leftPosition}px`;
        card.style.top = `${columnHeights[shortestColumnIndex]}px`;
        card.style.width = `${cardWidth}px`;
        
        // Update column height
        const cardHeight = card.offsetHeight || 392; // fallback height
        columnHeights[shortestColumnIndex] += cardHeight + gap;
    });
    
    // Set container height
    const maxHeight = Math.max(...columnHeights) - gap;
    container.style.height = `${maxHeight}px`;
}

// Debounced resize handler
let resizeTimeout;
function handleResize() {
    clearTimeout(resizeTimeout);
    resizeTimeout = setTimeout(() => {
        layoutMasonry();
    }, 100); // Reduced delay for more responsive resizing
}

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
    
    // Add resize listener for masonry layout
    window.addEventListener('resize', handleResize);
    
    // Also listen for orientation changes on mobile
    window.addEventListener('orientationchange', () => {
        setTimeout(layoutMasonry, 200);
    });
    
    // Initialize masonry layout after a short delay
    setTimeout(layoutMasonry, 100);
    
    // Re-layout when images load (in case background images affect sizing)
    window.addEventListener('load', () => {
        setTimeout(layoutMasonry, 100);
    });
    
    // Make date options clickable
    makeDataOptionsClickable();

    // Rewrite every server-rendered real-instant timestamp to the viewer's own timezone.
    hydrateLocalTimes();

    // Rewrite every server-rendered wall-clock (floating local time) timestamp to the viewer's
    // own locale wording -- never their timezone, since a wall-clock value has no UTC instant.
    hydrateWallClockTimes();

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