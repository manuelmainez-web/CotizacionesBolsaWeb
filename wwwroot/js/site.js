// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Write your JavaScript code.

document.querySelectorAll('[data-dialog-target]').forEach(function (button) {
    button.addEventListener('click', function () {
        var dialog = document.getElementById(button.getAttribute('data-dialog-target'));
        if (dialog && typeof dialog.showModal === 'function') {
            dialog.showModal();
        }
    });
});
