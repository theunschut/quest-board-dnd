// The shared photo crop dialog for every upload form (character, contact, Dungeon Master profile).
//
// Loaded as a module after the Cropper.js classic script and Bootstrap. It wires itself: every file input marked
// data-crop-input opens the dialog when a picture is chosen, and every element carrying data-crop-original-url opens it
// on the stored original. Per input it reads data-crop-output-name (the hidden file input the cropped picture goes
// to) and data-crop-aspect-ratio (width over height, 1 by default). The selection can be dragged, moved and resized
// with the button row, or moved and resized with the arrow keys while it has focus.
//
// Cropper's own keyboard option is deliberately never switched on: it listens on the whole document, moves one pixel
// at a time and lets Delete remove the selection. The keys here are heard on the selection element only.

const STEP = 8;
const MIN_SIZE = 32;
const STATUS_INTERVAL_MS = 500;
const MAX_FILE_BYTES = 5 * 1024 * 1024;
const MAX_DECODE_EDGE = 2400;
const ALLOWED_TYPES = ['image/jpeg', 'image/jpg', 'image/png', 'image/gif'];
// Layout reports edges with a little floating-point noise; anything within this many pixels counts as on the edge.
const EDGE_TOLERANCE = 0.01;
const ARROW_KEYS = new Set(['ArrowUp', 'ArrowDown', 'ArrowLeft', 'ArrowRight']);

// Decode the picture with its EXIF orientation baked into the pixels, then downscale it so no canvas Cropper touches
// comes near the ceiling some mobile browsers put on canvas size.
async function prepareImageForCropper(file) {
    const bitmap = await createImageBitmap(file, { imageOrientation: 'from-image' });

    let width = bitmap.width;
    let height = bitmap.height;
    const longEdge = Math.max(width, height);
    if (longEdge > MAX_DECODE_EDGE) {
        const scale = MAX_DECODE_EDGE / longEdge;
        width = Math.round(width * scale);
        height = Math.round(height * scale);
    }

    const canvas = document.createElement('canvas');
    canvas.width = width;
    canvas.height = height;
    canvas.getContext('2d').drawImage(bitmap, 0, 0, width, height);
    // Free the bitmap's memory now rather than waiting for the garbage collector.
    bitmap.close();

    const outputType = file.type === 'image/png' ? 'image/png' : 'image/jpeg';
    return new Promise(resolve => {
        canvas.toBlob(blob => {
            canvas.width = 0;
            canvas.height = 0;
            resolve(blob);
        }, outputType, 0.92);
    });
}

// The current selection as a JPEG. Always JPEG, so an animated GIF must never be routed through here.
async function extractCroppedBlob(selection) {
    const canvas = await selection.$toCanvas();
    return new Promise(resolve => canvas.toBlob(resolve, 'image/jpeg', 0.9));
}

// A file input cannot be assigned a File directly; a DataTransfer is the standard way to give it one.
function fillFileInput(hiddenInput, blob, originalFileName) {
    if (!hiddenInput || !blob) {
        return;
    }
    const baseName = (originalFileName || 'photo').replace(/\.[^.]+$/, '');
    const croppedFile = new File([blob], 'cropped-' + baseName + '.jpg', { type: blob.type, lastModified: Date.now() });
    const transfer = new DataTransfer();
    transfer.items.add(croppedFile);
    hiddenInput.files = transfer.files;
}

function clamp(value, low, high) {
    return Math.min(Math.max(value, low), Math.max(low, high));
}

function initCropDialog() {
    const modalEl = document.getElementById('cropPhotoModal');
    const image = document.getElementById('cropperImageEl');
    const selection = document.getElementById('cropperSelectionEl');
    const confirmBtn = document.getElementById('cropConfirmBtn');
    const cancelBtn = document.getElementById('cropCancelBtn');
    const statusEl = modalEl ? modalEl.querySelector('.crop-status') : null;
    const canvas = selection ? selection.closest('cropper-canvas') : null;
    if (!modalEl || !image || !selection || !canvas) {
        return;
    }

    let objectUrl = null;
    let originalFileName = '';
    let hiddenInput = null;
    let activeFileInput = null;
    let ratio = 1;
    let lastStatusAt = 0;
    let statusTimer = 0;

    // Where the picture sits, in the canvas's own coordinates. Read at every press: the canvas is 0 by 0 until the
    // dialog is shown, and the picture is re-fitted each time it opens. Rounded inward so Cropper's own rounding of the
    // selection can never push an edge past the picture.
    function imageBounds() {
        const stage = canvas.getBoundingClientRect();
        const picture = image.getBoundingClientRect();
        return {
            left: Math.ceil(picture.left - stage.left - EDGE_TOLERANCE),
            top: Math.ceil(picture.top - stage.top - EDGE_TOLERANCE),
            right: Math.floor(picture.right - stage.left + EDGE_TOLERANCE),
            bottom: Math.floor(picture.bottom - stage.top + EDGE_TOLERANCE),
        };
    }

    function announce() {
        if (!statusEl) {
            return;
        }
        const bounds = imageBounds();
        const sentence = 'Selection ' + Math.round(selection.width) + ' by ' + Math.round(selection.height)
            + ' pixels, left ' + Math.round(selection.x - bounds.left) + ', top ' + Math.round(selection.y - bounds.top);
        const write = () => {
            statusTimer = 0;
            lastStatusAt = Date.now();
            statusEl.textContent = sentence;
        };
        // At most one sentence per interval, so a held key does not flood a screen reader; the latest one wins.
        window.clearTimeout(statusTimer);
        const wait = STATUS_INTERVAL_MS - (Date.now() - lastStatusAt);
        if (wait <= 0) {
            write();
        } else {
            statusTimer = window.setTimeout(write, wait);
        }
    }

    function moveBy(dx, dy) {
        const bounds = imageBounds();
        const x = clamp(selection.x + dx, bounds.left, bounds.right - selection.width);
        const y = clamp(selection.y + dy, bounds.top, bounds.bottom - selection.height);
        selection.$moveTo(x, y);
        announce();
    }

    // Grow or shrink about the centre, never below the minimum or past the largest box of this ratio the picture holds.
    // Cropper applies the element's aspect ratio itself, so the height passed is only a starting point.
    function resizeBy(delta) {
        const bounds = imageBounds();
        const largest = Math.min(bounds.right - bounds.left, Math.floor((bounds.bottom - bounds.top) * ratio));
        const width = clamp(selection.width + delta, MIN_SIZE, Math.max(MIN_SIZE, largest));
        const height = Math.round(width / ratio);
        const x = clamp(selection.x - (width - selection.width) / 2, bounds.left, bounds.right - width);
        const y = clamp(selection.y - (height - selection.height) / 2, bounds.top, bounds.bottom - height);
        selection.$change(x, y, width, height);
        announce();
    }

    // The first selection is a fraction of the stage, which can be larger than the picture (a wide picture in a short
    // stage). Fit it inside the picture, keeping its centre, so no box ever starts outside what will be cropped.
    function fitSelectionToImage() {
        const bounds = imageBounds();
        const largest = Math.min(bounds.right - bounds.left, Math.floor((bounds.bottom - bounds.top) * ratio));
        const width = Math.min(selection.width, Math.max(MIN_SIZE, largest));
        const height = Math.round(width / ratio);
        const x = clamp(selection.x + (selection.width - width) / 2, bounds.left, bounds.right - width);
        const y = clamp(selection.y + (selection.height - height) / 2, bounds.top, bounds.bottom - height);
        selection.$change(x, y, width, height);
    }

    // The picture fitted to the stage and the selection centred at its default size.
    function placeDefaultSelection() {
        image.$center('contain');
        selection.$initSelection(true, true);
        fitSelectionToImage();
    }

    // Back to how the dialog opened.
    function resetSelection() {
        placeDefaultSelection();
        announce();
    }

    const actions = {
        up: () => moveBy(0, -STEP),
        down: () => moveBy(0, STEP),
        left: () => moveBy(-STEP, 0),
        right: () => moveBy(STEP, 0),
        grow: () => resizeBy(STEP),
        shrink: () => resizeBy(-STEP),
        reset: resetSelection,
    };

    modalEl.querySelectorAll('[data-crop-action]').forEach(button => {
        const action = actions[button.dataset.cropAction];
        if (action) {
            button.addEventListener('click', action);
        }
    });

    // Keys are heard on the selection only. Arrow moves it, Shift plus arrow resizes it (up and right grow, down and
    // left shrink); anything with Ctrl, Alt or Meta, and every other key, is left alone.
    selection.addEventListener('keydown', event => {
        if (event.ctrlKey || event.altKey || event.metaKey || !ARROW_KEYS.has(event.key)) {
            return;
        }
        event.preventDefault();
        if (event.shiftKey) {
            resizeBy(event.key === 'ArrowUp' || event.key === 'ArrowRight' ? STEP : -STEP);
            return;
        }
        moveBy(
            event.key === 'ArrowLeft' ? -STEP : event.key === 'ArrowRight' ? STEP : 0,
            event.key === 'ArrowUp' ? -STEP : event.key === 'ArrowDown' ? STEP : 0);
    });

    function revokeObjectUrl() {
        if (objectUrl) {
            URL.revokeObjectURL(objectUrl);
            objectUrl = null;
        }
    }

    function resetCropState() {
        revokeObjectUrl();
        image.removeAttribute('src');
        originalFileName = '';
        if (hiddenInput) {
            // A discarded selection must never linger into a later submit.
            hiddenInput.files = new DataTransfer().files;
        }
    }

    function errorElement() {
        return document.getElementById('fileSizeError');
    }

    function showError(message) {
        const chip = errorElement();
        if (!chip) {
            return;
        }
        const text = chip.querySelector('[data-crop-error-text]') || chip;
        text.textContent = message;
        chip.hidden = false;
    }

    function clearError() {
        const chip = errorElement();
        if (!chip) {
            return;
        }
        (chip.querySelector('[data-crop-error-text]') || chip).textContent = '';
        chip.hidden = true;
    }

    function showReadError() {
        showError("This image couldn't be read. Please choose a different photo.");
    }

    async function populateHiddenInput() {
        fillFileInput(hiddenInput, await extractCroppedBlob(selection), originalFileName);
    }

    // The dialog is only laid out once Bootstrap has shown it, so the picture is fitted and the first selection made
    // then. The hidden input is filled straight away from that default square, so a form never waits on an explicit
    // confirm: dismissing the dialog untouched still submits the centred crop.
    modalEl.addEventListener('shown.bs.modal', async () => {
        try {
            await image.$ready();
        } catch (error) {
            return;
        }
        placeDefaultSelection();
        if (statusEl) {
            statusEl.textContent = '';
        }
        try {
            await populateHiddenInput();
        } catch (error) {
            // Cropper has not finished starting; Use This Crop extracts again regardless.
        }
    });

    async function openWithBlob(blob, input, config) {
        activeFileInput = input;
        hiddenInput = document.querySelector('input[type="file"][name="' + CSS.escape(config.outputName) + '"]');
        ratio = config.aspectRatio;
        selection.setAttribute('aspect-ratio', String(ratio));
        revokeObjectUrl();
        objectUrl = URL.createObjectURL(blob);
        image.src = objectUrl;
        bootstrap.Modal.getOrCreateInstance(modalEl).show();
    }

    function configOf(element) {
        return {
            outputName: element.dataset.cropOutputName || 'CroppedPictureFile',
            aspectRatio: Number(element.dataset.cropAspectRatio) || 1,
        };
    }

    function wireFileInput(input) {
        input.addEventListener('change', async () => {
            const file = input.files && input.files[0];
            if (!file) {
                return;
            }
            clearError();

            if (file.size > MAX_FILE_BYTES) {
                const sizeMb = (file.size / 1024 / 1024).toFixed(2);
                const maxMb = (MAX_FILE_BYTES / 1024 / 1024).toFixed(0);
                showError('File size (' + sizeMb + ' MB) exceeds the maximum allowed size of ' + maxMb + ' MB. Please choose a smaller image.');
                input.value = '';
                return;
            }
            if (!ALLOWED_TYPES.includes(file.type)) {
                showError('Only image files (JPG, PNG, GIF) are allowed.');
                input.value = '';
                return;
            }
            if (file.type === 'image/gif') {
                // The crop is always saved as a still JPEG, which would flatten an animated GIF. The original goes up as
                // it is and the server shows it unchanged.
                hiddenInput = document.querySelector('input[type="file"][name="' + CSS.escape(configOf(input).outputName) + '"]');
                resetCropState();
                return;
            }

            originalFileName = file.name || 'photo';
            let prepared;
            try {
                prepared = await prepareImageForCropper(file);
            } catch (error) {
                showReadError();
                input.value = '';
                return;
            }
            await openWithBlob(prepared, input, configOf(input));
        });
    }

    function wireRecropButton(button) {
        button.addEventListener('click', async () => {
            clearError();
            let response;
            try {
                response = await fetch(button.dataset.cropOriginalUrl, { cache: 'no-store' });
            } catch (error) {
                showReadError();
                return;
            }
            if (!response.ok) {
                showReadError();
                return;
            }
            const contentType = response.headers.get('Content-Type') || '';
            if (contentType.indexOf('image/gif') !== -1) {
                showError("This photo is an animated GIF and can't be re-cropped without losing its animation.");
                return;
            }
            const original = new File([await response.blob()], 'photo', { type: contentType });
            originalFileName = 'photo';
            let prepared;
            try {
                prepared = await prepareImageForCropper(original);
            } catch (error) {
                showReadError();
                return;
            }
            const input = document.querySelector('input[type="file"][data-crop-input]');
            await openWithBlob(prepared, input, configOf(button));
        });
    }

    document.querySelectorAll('input[type="file"][data-crop-input]').forEach(wireFileInput);
    document.querySelectorAll('[data-crop-original-url]').forEach(wireRecropButton);

    confirmBtn.addEventListener('click', async () => {
        try {
            await populateHiddenInput();
        } catch (error) {
            showReadError();
            return;
        }
        bootstrap.Modal.getOrCreateInstance(modalEl).hide();
    });

    cancelBtn.addEventListener('click', () => {
        if (activeFileInput) {
            activeFileInput.value = '';
        }
        resetCropState();
    });
}

initCropDialog();
