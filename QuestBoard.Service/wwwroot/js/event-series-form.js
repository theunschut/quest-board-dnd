// Behaviour of the two event forms, loaded by both. Each feature switches itself on only when its
// elements are on the page, so one file serves both.
//
// Create: the "Make this a recurring series" checkbox shows the recurrence section; the cycle strip
// is a row of toggle buttons that writes the hidden CycleMask field ("1,0,1": one entry per week of
// the cycle, 1 for a session); the line under the interval names the weekday the date falls on;
// and a live preview asks the server which dates the series would generate. The preview is advice
// only: the server computes the dates again when the form is saved.
//
// Edit, for an occurrence of a series: saving opens a dialog that asks whether the change applies
// to this event only or to this and every future event. Before it opens, a changed date is checked
// against the series for a session already on that day; that notice is advice too, and a failed
// lookup never stops the save. The hidden EditScope field is rendered with the safe single-event
// value and is only changed by an explicit choice in the dialog.
//
// The page supplies the ids and addresses through data attributes (data-event-id and
// data-collision-url on the form, data-scope-value on the two dialog buttons) and the antiforgery
// token through the request-verification-token meta element. Nothing is exposed globally and no
// markup is built from strings: every node is made with createElement and textContent.

const MAX_CYCLE_LENGTH = 24;
const PREVIEW_DEBOUNCE_MS = 400;
const PREVIEW_PROMPT = 'Enter a start date and cadence to see the dates this series will generate.';
const PREVIEW_FAILED = "Couldn't generate a preview — check your cadence and mask, then try again.";

function antiforgeryToken() {
    const meta = document.querySelector('meta[name="request-verification-token"]');
    return meta ? meta.content : '';
}

function parseCycleMask(value) {
    if (!value) {
        return [true];
    }

    const positions = value.split(',').map(function (token) {
        return token.trim() === '1';
    });

    return positions.length > 0 ? positions : [true];
}

function formatCycleMask(positions) {
    return positions.map(function (on) {
        return on ? '1' : '0';
    }).join(',');
}

function initSeriesForm() {
    const section = document.getElementById('recurrenceSection');
    const strip = document.getElementById('seriesCycleMaskStrip');
    const maskInput = document.getElementById('CycleMask');
    if (!section || !strip || !maskInput) {
        return;
    }

    const enableCheckbox = document.getElementById('enableRecurrence');
    const dateInput = document.getElementById('Date');
    const intervalInput = document.getElementById('IntervalWeeks');
    const endDateInput = document.getElementById('SeriesEndDate');
    const lengthLabel = document.getElementById('seriesCycleLengthLabel');
    const addButton = document.getElementById('seriesCycleAddBtn');
    const removeButton = document.getElementById('seriesCycleRemoveBtn');
    const weekdayLabel = document.getElementById('seriesDerivedWeekday');
    const previewList = document.getElementById('seriesPreviewList');

    let cycleMaskPositions = parseCycleMask(maskInput.value);
    let previewRequestCounter = 0;
    let previewDebounceTimer = null;

    function renderCycleMaskStrip() {
        strip.replaceChildren();

        cycleMaskPositions.forEach(function (on, index) {
            const cell = document.createElement('button');
            cell.type = 'button';
            cell.className = on ? 'cycle-mask-cell on' : 'cycle-mask-cell';
            cell.setAttribute('aria-pressed', on ? 'true' : 'false');
            cell.setAttribute('aria-label', 'Position ' + (index + 1) + ', session ' + (on ? 'on' : 'off'));
            cell.title = on ? 'Session on this position' : 'No session on this position';
            if (on) {
                const check = document.createElement('i');
                check.className = 'fas fa-check';
                check.setAttribute('aria-hidden', 'true');
                cell.appendChild(check);
            }
            cell.addEventListener('click', function () {
                cycleMaskPositions[index] = !cycleMaskPositions[index];
                syncCycleMask();
                // The strip is rebuilt on every change, so the toggled cell is a new element:
                // focus goes back to the cell at the same position.
                const rebuilt = strip.children[index];
                if (rebuilt) {
                    rebuilt.focus();
                }
            });
            strip.appendChild(cell);
        });

        lengthLabel.textContent = 'Cycle length: ' + cycleMaskPositions.length;

        if (cycleMaskPositions.length >= MAX_CYCLE_LENGTH) {
            addButton.disabled = true;
            addButton.title = 'Maximum cycle length reached.';
        } else {
            addButton.disabled = false;
            addButton.title = 'Add a cycle position';
        }

        removeButton.disabled = cycleMaskPositions.length <= 1;
    }

    function syncCycleMask() {
        maskInput.value = formatCycleMask(cycleMaskPositions);
        renderCycleMaskStrip();
        scheduleSeriesPreview();
    }

    function updateDerivedWeekday() {
        const dateValue = dateInput.value;
        const intervalValue = intervalInput.value || '1';

        if (!dateValue) {
            weekdayLabel.textContent = 'Every ' + intervalValue + ' week(s) on --';
            return;
        }

        const parts = dateValue.split('-');
        const parsedDate = new Date(Number(parts[0]), Number(parts[1]) - 1, Number(parts[2]));
        const weekdayName = parsedDate.toLocaleDateString('en-US', { weekday: 'long' });

        weekdayLabel.textContent = 'Every ' + intervalValue + ' week(s) on ' + weekdayName;
    }

    function renderPreviewMessage(message, isError) {
        previewList.replaceChildren();
        const item = document.createElement('li');
        if (isError) {
            item.className = 'text-danger';
        }
        item.textContent = message;
        previewList.appendChild(item);
    }

    function renderPreviewDates(dates, anchorFullyInPast) {
        previewList.replaceChildren();

        if (!dates || dates.length === 0) {
            renderPreviewMessage(PREVIEW_PROMPT, false);
            return;
        }

        if (anchorFullyInPast) {
            const notice = document.createElement('li');
            notice.className = 'mb-1';
            notice.textContent = 'Every date this cadence produces so far is in the past. Showing the next 10 dates from today onward — these are the ones that will actually be created.';
            previewList.appendChild(notice);
        }

        dates.forEach(function (date) {
            const item = document.createElement('li');
            item.textContent = date.label;
            previewList.appendChild(item);
        });
    }

    function refreshSeriesPreview() {
        const anchorDate = dateInput.value;
        const intervalWeeks = intervalInput.value;
        const cycleMask = maskInput.value;
        const endDate = endDateInput.value;

        if (!anchorDate || !cycleMask) {
            renderPreviewMessage(PREVIEW_PROMPT, false);
            return;
        }

        const requestId = ++previewRequestCounter;
        renderPreviewMessage('Calculating…', false);

        const formData = new FormData();
        formData.append('AnchorDate', anchorDate);
        formData.append('IntervalWeeks', intervalWeeks);
        formData.append('CycleMask', cycleMask);
        if (endDate) {
            formData.append('EndDate', endDate);
        }
        formData.append('__RequestVerificationToken', antiforgeryToken());

        fetch('/Events/PreviewSeries', {
            method: 'POST',
            body: formData
        }).then(function (res) {
            return res.json();
        }).then(function (data) {
            // A newer keystroke may have started a later request while this one was in flight.
            // A naive last-response-wins would let this stale response overwrite that newer
            // answer, so any response whose counter no longer matches is dropped.
            if (requestId !== previewRequestCounter) {
                return;
            }

            if (!data.success) {
                renderPreviewMessage(PREVIEW_FAILED, true);
                return;
            }

            renderPreviewDates(data.dates, data.anchorFullyInPast);
        }).catch(function () {
            if (requestId !== previewRequestCounter) {
                return;
            }
            renderPreviewMessage(PREVIEW_FAILED, true);
        });
    }

    function scheduleSeriesPreview() {
        clearTimeout(previewDebounceTimer);
        previewDebounceTimer = setTimeout(refreshSeriesPreview, PREVIEW_DEBOUNCE_MS);
    }

    function showRecurrenceSection() {
        section.classList.remove('d-none');
        scheduleSeriesPreview();
    }

    renderCycleMaskStrip();
    updateDerivedWeekday();

    // A form that comes back with the box already ticked (a validation round trip, or the browser
    // restoring the page) shows its recurrence section instead of hiding fields that hold values.
    if (enableCheckbox.checked) {
        showRecurrenceSection();
    }

    enableCheckbox.addEventListener('change', function () {
        if (enableCheckbox.checked) {
            showRecurrenceSection();
        } else {
            section.classList.add('d-none');
        }
    });

    dateInput.addEventListener('change', function () {
        updateDerivedWeekday();
        scheduleSeriesPreview();
    });

    intervalInput.addEventListener('input', function () {
        updateDerivedWeekday();
        scheduleSeriesPreview();
    });

    endDateInput.addEventListener('change', scheduleSeriesPreview);

    addButton.addEventListener('click', function () {
        if (cycleMaskPositions.length >= MAX_CYCLE_LENGTH) {
            return;
        }
        cycleMaskPositions.push(true);
        syncCycleMask();
    });

    removeButton.addEventListener('click', function () {
        if (cycleMaskPositions.length <= 1) {
            return;
        }
        cycleMaskPositions.pop();
        syncCycleMask();
    });
}

function initSaveScope() {
    const form = document.getElementById('editEventForm');
    const modalElement = document.getElementById('saveScopeModal');
    if (!form || !modalElement) {
        return;
    }

    const scopeInput = document.getElementById('EditScope');
    const originalDateInput = document.getElementById('originalDate');
    const notice = document.getElementById('saveScopeCollisionNotice');
    const onlyButton = document.getElementById('saveScopeOnlyBtn');
    const futureButton = document.getElementById('saveScopeFutureBtn');
    const modal = new bootstrap.Modal(modalElement);

    function hideCollisionNotice() {
        notice.classList.add('d-none');
        notice.textContent = '';
    }

    function showCollisionNotice(dateLabel) {
        notice.textContent = 'Another session in this series already falls on ' + dateLabel + '. Saving will not merge or block it — both will exist.';
        notice.classList.remove('d-none');
    }

    function openModalWithCollisionCheck() {
        hideCollisionNotice();

        const dateValue = document.getElementById('Date').value;

        // Only check the server when the date actually changed: an unchanged date cannot create a
        // new collision.
        if (!originalDateInput || dateValue === originalDateInput.value || !form.dataset.collisionUrl) {
            modal.show();
            return;
        }

        const formData = new FormData();
        formData.append('id', form.dataset.eventId);
        formData.append('date', dateValue);
        formData.append('__RequestVerificationToken', antiforgeryToken());

        fetch(form.dataset.collisionUrl, {
            method: 'POST',
            body: formData
        }).then(function (res) {
            return res.json();
        }).then(function (data) {
            if (data.collision) {
                showCollisionNotice(data.date);
            }
            modal.show();
        }).catch(function () {
            // A failed advisory lookup must never stop a Dungeon Master from saving.
            modal.show();
        });
    }

    function chooseScope(button) {
        scopeInput.value = button.dataset.scopeValue;
        modal.hide();
        form.submit();
    }

    // The submit is held back to ask the scope question. The dialog's own buttons submit the form
    // through form.submit(), which does not raise this event again.
    form.addEventListener('submit', function (event) {
        event.preventDefault();
        openModalWithCollisionCheck();
    });

    onlyButton.addEventListener('click', function () {
        chooseScope(onlyButton);
    });

    futureButton.addEventListener('click', function () {
        chooseScope(futureButton);
    });

    modalElement.addEventListener('shown.bs.modal', function () {
        onlyButton.focus();
    });
}

initSeriesForm();
initSaveScope();
