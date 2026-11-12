// Fills the board-switch dialog on the cross-board agenda. Each row that belongs to a board other than the
// active one carries a button that opens the dialog and names, in data attributes, the board to switch to and
// the address to land on afterwards. When the dialog opens, this module copies those into the dialog's hidden
// form fields and into the two places the board name is spoken. Values go in as field values and text content
// only, so nothing the server rendered is ever parsed as markup, and nothing is exposed globally.

const dialog = document.getElementById('switchBoardModal');

if (dialog) {
    const groupId = document.getElementById('switchBoardGroupId');
    const returnUrl = document.getElementById('switchBoardReturnUrl');
    const nameLabels = [
        document.getElementById('switchBoardName'),
        document.getElementById('switchBoardNameInline'),
    ];

    dialog.addEventListener('show.bs.modal', function (event) {
        const trigger = event.relatedTarget;
        if (!trigger) {
            return;
        }

        const boardName = trigger.getAttribute('data-board-name') ?? '';
        groupId.value = trigger.getAttribute('data-group-id') ?? '';
        returnUrl.value = trigger.getAttribute('data-return-url') ?? '';
        nameLabels.forEach(function (label) {
            label.textContent = boardName;
        });
    });
}
