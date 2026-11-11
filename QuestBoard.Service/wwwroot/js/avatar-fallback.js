// Replaces a character portrait that fails to load with its placeholder. The portrait is an img.avatar-32 whose next
// sibling is a hidden placeholder of the same size; this module hides the image and shows the placeholder. Only the
// hidden attribute is toggled, so no markup is ever built from data.

function showPlaceholder(image) {
    image.hidden = true;
    const placeholder = image.nextElementSibling;
    if (placeholder && placeholder.classList.contains('avatar-32--placeholder')) {
        placeholder.hidden = false;
    }
}

// An error event does not bubble, so it is caught on the way down.
document.addEventListener('error', function (event) {
    const target = event.target;
    if (target instanceof HTMLImageElement && target.matches('img.avatar-32')) {
        showPlaceholder(target);
    }
}, true);

// An image can fail before this module runs: sweep the ones that already finished loading with no pixels.
document.querySelectorAll('img.avatar-32').forEach(function (image) {
    if (image.complete && image.naturalWidth === 0) {
        showPlaceholder(image);
    }
});
