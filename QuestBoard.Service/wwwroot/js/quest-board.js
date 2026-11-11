// Desktop quest board layout. Posters carry their own height through CSS aspect-ratio, so this module only reads
// layout and writes positions: the container gets is-masonry and three custom properties, each poster gets its
// x and y. Without this script (or if it throws) the stylesheet's CSS columns still show every poster.

const RESIZE_DELAY_MS = 100;

function numberOf(value) {
    const parsed = parseFloat(value);
    return Number.isFinite(parsed) ? parsed : 0;
}

function layoutMasonry() {
    const container = document.querySelector('.quest-board-container');
    if (!container) return;

    const posters = Array.from(container.querySelectorAll(':scope > .quest-poster'));
    if (posters.length === 0) return;

    // Measure in the CSS column flow, where the stylesheet decides the card width for this window.
    container.classList.remove('is-masonry');
    const posterWidth = posters[0].offsetWidth;
    if (!posterWidth) return;

    const style = getComputedStyle(container);
    const gap = numberOf(style.columnGap) || 16;
    const padLeft = numberOf(style.paddingLeft);
    const padRight = numberOf(style.paddingRight);
    const padTop = numberOf(style.paddingTop);
    const padBottom = numberOf(style.paddingBottom);
    const available = container.clientWidth - padLeft - padRight;

    const columns = Math.max(1, Math.floor((available + gap) / (posterWidth + gap)));
    const used = columns * posterWidth + (columns - 1) * gap;
    const leftOffset = padLeft + Math.max(0, (available - used) / 2);

    container.style.setProperty('--poster-w', posterWidth + 'px');
    container.classList.add('is-masonry');

    const heights = new Array(columns).fill(0);
    posters.forEach(poster => {
        const column = heights.indexOf(Math.min(...heights));
        poster.style.setProperty('--poster-x', leftOffset + column * (posterWidth + gap) + 'px');
        poster.style.setProperty('--poster-y', padTop + heights[column] + 'px');
        heights[column] += poster.offsetHeight + gap;
    });

    container.style.setProperty('--board-height', padTop + Math.max(...heights) - gap + padBottom + 'px');
}

function layoutSafely() {
    try {
        layoutMasonry();
    } catch {
        // A failed layout leaves the board in its CSS column flow rather than half positioned.
        const container = document.querySelector('.quest-board-container');
        if (container) container.classList.remove('is-masonry');
    }
}

let resizeTimer;
function onResize() {
    clearTimeout(resizeTimer);
    resizeTimer = setTimeout(layoutSafely, RESIZE_DELAY_MS);
}

layoutSafely();
window.addEventListener('resize', onResize);
window.addEventListener('orientationchange', onResize);
window.addEventListener('load', layoutSafely);
if (document.fonts && document.fonts.ready) document.fonts.ready.then(layoutSafely);
