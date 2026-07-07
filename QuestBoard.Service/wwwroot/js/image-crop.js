// Shared client-side crop pipeline for every photo-upload form (character, contact, DM profile).
// Loaded per-view via a plain <script> include (matching site.js's no-module, no-bundler
// convention) and initialized per-view by calling initImageCrop({...}) with that view's element IDs.

// Decode the selected file with EXIF orientation baked into pixels, then downscale it so no
// canvas Cropper.js touches ever approaches iOS Safari's ~16.7M-pixel canvas ceiling.
async function prepareImageForCropper(file, maxDimension) {
    maxDimension = maxDimension || 2400;

    const bitmap = await createImageBitmap(file, { imageOrientation: 'from-image' });

    let width = bitmap.width;
    let height = bitmap.height;
    const longEdge = Math.max(width, height);
    if (longEdge > maxDimension) {
        const scale = maxDimension / longEdge;
        width = Math.round(width * scale);
        height = Math.round(height * scale);
    }

    const canvas = document.createElement('canvas');
    canvas.width = width;
    canvas.height = height;
    const ctx = canvas.getContext('2d');
    ctx.drawImage(bitmap, 0, 0, width, height);

    // Free the ImageBitmap's backing memory right away rather than waiting on GC -- WebKit is
    // documented to hold canvas-related memory longer than expected.
    bitmap.close();

    const outputType = file.type === 'image/png' ? 'image/png' : 'image/jpeg';

    return new Promise(function (resolve) {
        canvas.toBlob(function (blob) {
            // Release this scratch canvas's memory too, now that we have the blob.
            canvas.width = 0;
            canvas.height = 0;
            resolve(blob);
        }, outputType, 0.92);
    });
}

// Extract the current crop-selection as a JPEG blob via Cropper.js v2's $toCanvas() API.
async function extractCroppedBlob(cropperSelectionEl) {
    const canvas = await cropperSelectionEl.$toCanvas();
    return new Promise(function (resolve) {
        canvas.toBlob(function (blob) {
            resolve(blob);
        }, 'image/jpeg', 0.9);
    });
}

// Populate a hidden <input type="file"> with a single generated File, since FileList has no
// public constructor -- DataTransfer is the standards-based workaround for assigning .files.
function setCroppedFileInput(hiddenInputEl, blob, originalFileName) {
    if (!hiddenInputEl || !blob) {
        return;
    }

    const croppedFile = new File([blob], 'cropped-' + originalFileName, {
        type: blob.type,
        lastModified: Date.now()
    });

    const dt = new DataTransfer();
    dt.items.add(croppedFile);
    hiddenInputEl.files = dt.files;
}

// One reusable initializer any upload view can call with its own element IDs. The crop modal
// markup (#cropPhotoModal, #cropperImageEl, #cropperSelectionEl, #cropConfirmBtn, #cropCancelBtn)
// is expected to already exist in the view -- this file only wires behavior to it.
function initImageCrop(config) {
    config = config || {};
    const fileInputId = config.fileInputId;
    const hiddenCroppedInputName = config.hiddenCroppedInputName;
    const aspectRatio = config.aspectRatio || 1;

    const fileInput = fileInputId ? document.getElementById(fileInputId) : null;
    if (!fileInput) {
        // Nothing to wire on this page -- safe no-op so the same script can be included
        // defensively without checking which view is currently rendered.
        return;
    }

    const modalEl = document.getElementById('cropPhotoModal');
    const cropperImageEl = document.getElementById('cropperImageEl');
    const cropperSelectionEl = document.getElementById('cropperSelectionEl');
    const confirmBtn = document.getElementById('cropConfirmBtn');
    const cancelBtn = document.getElementById('cropCancelBtn');
    const hiddenInput = hiddenCroppedInputName
        ? document.querySelector('input[type="file"][name="' + hiddenCroppedInputName + '"]')
        : null;

    if (!modalEl || !cropperImageEl || !cropperSelectionEl || !hiddenInput) {
        // Required crop-modal markup isn't present on this view -- bail out quietly rather
        // than throwing, so initImageCrop stays safe to call speculatively.
        return;
    }

    if (aspectRatio) {
        cropperSelectionEl.setAttribute('aspect-ratio', String(aspectRatio));
    }

    let currentObjectUrl = null;
    let currentOriginalFileName = '';

    function revokeCurrentObjectUrl() {
        if (currentObjectUrl) {
            URL.revokeObjectURL(currentObjectUrl);
            currentObjectUrl = null;
        }
    }

    function getErrorDiv() {
        // Reuse this form's existing #fileSizeError-style error element, if present, so the
        // "couldn't be read" message renders through the same convention as the file-size/type
        // checks already on the page.
        return document.getElementById('fileSizeError');
    }

    function showReadError() {
        const errorDiv = getErrorDiv();
        if (errorDiv) {
            errorDiv.textContent = "This image couldn't be read. Please choose a different photo.";
            errorDiv.style.display = 'block';
        }
    }

    function clearReadError() {
        const errorDiv = getErrorDiv();
        if (errorDiv) {
            errorDiv.style.display = 'none';
            errorDiv.textContent = '';
        }
    }

    async function populateHiddenInputFromCurrentSelection() {
        const blob = await extractCroppedBlob(cropperSelectionEl);
        setCroppedFileInput(hiddenInput, blob, currentOriginalFileName);
    }

    function resetCropState() {
        revokeCurrentObjectUrl();
        cropperImageEl.removeAttribute('src');
        currentOriginalFileName = '';

        // Clear the hidden cropped input so a discarded selection never lingers into a later
        // unrelated submit.
        const dt = new DataTransfer();
        hiddenInput.files = dt.files;
    }

    fileInput.addEventListener('change', async function (e) {
        const file = e.target.files && e.target.files[0];
        if (!file) {
            return;
        }

        clearReadError();
        currentOriginalFileName = file.name || 'photo';

        let correctedBlob;
        try {
            correctedBlob = await prepareImageForCropper(file, 2400);
        } catch (err) {
            showReadError();
            fileInput.value = '';
            return;
        }

        revokeCurrentObjectUrl();
        currentObjectUrl = URL.createObjectURL(correctedBlob);
        cropperImageEl.src = currentObjectUrl;

        const modal = bootstrap.Modal.getOrCreateInstance(modalEl);

        // Bootstrap defers the modal's actual display:block until after the backdrop's fade
        // transition finishes, so modal.show() returning does NOT mean the canvas has real
        // layout dimensions yet. Cropper.js v2's own auto-fit (CropperImage.$handleLoad's
        // $center('contain'), fired from the <img>'s load event) and the selection's one-time
        // initial-coverage sizing (CropperSelection.connectedCallback's $initSelection) both
        // run against a still-hidden (0x0) canvas -- the image ends up unscaled at native
        // pixel size and the selection collapses to a zero-size box pinned at (0,0). Re-running
        // both once the modal is confirmed visible fixes both with the canvas's real size.
        // Populate the hidden input from the (currently default, centered) selection immediately,
        // before any user interaction, so the surrounding form never blocks on an explicit
        // "confirm crop" click -- a value already exists even if the modal is dismissed untouched.
        async function fitImageAndSelectionToVisibleCanvas() {
            cropperImageEl.$center('contain');
            cropperSelectionEl.$initSelection(true, true);
            try {
                await populateHiddenInputFromCurrentSelection();
            } catch (err) {
                // Can only fail if the Cropper.js element hasn't finished initializing yet;
                // "Use This Crop" below re-extracts regardless.
            }
        }
        if (modalEl.classList.contains('show')) {
            await fitImageAndSelectionToVisibleCanvas();
        } else {
            modalEl.addEventListener('shown.bs.modal', function onShown() {
                modalEl.removeEventListener('shown.bs.modal', onShown);
                fitImageAndSelectionToVisibleCanvas();
            });
        }
        modal.show();
    });

    if (confirmBtn) {
        confirmBtn.addEventListener('click', async function () {
            try {
                await populateHiddenInputFromCurrentSelection();
            } catch (err) {
                showReadError();
                return;
            }

            const modal = bootstrap.Modal.getOrCreateInstance(modalEl);
            modal.hide();
        });
    }

    if (cancelBtn) {
        cancelBtn.addEventListener('click', function () {
            fileInput.value = '';
            resetCropState();
        });
    }
}
