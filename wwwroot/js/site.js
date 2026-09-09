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

// Reloj con fecha y hora actual en la cabecera
(function () {
    var el = document.getElementById('current-datetime');
    if (!el) {
        return;
    }

    function updateDateTime() {
        var now = new Date();
        var fecha = now.toLocaleDateString('es-ES', { day: '2-digit', month: '2-digit', year: 'numeric' });
        var hora = now.toLocaleTimeString('es-ES', { hour: '2-digit', minute: '2-digit', second: '2-digit' });
        el.textContent = fecha + ' ' + hora;
    }

    updateDateTime();
    setInterval(updateDateTime, 1000);
})();

// Reposiciona el reloj y el botón de actualizar junto a los controles de
// ventana (minimizar/maximizar/cerrar) cuando la PWA está instalada y el
// navegador soporta Window Controls Overlay.
(function () {
    if (!('windowControlsOverlay' in navigator)) {
        return;
    }

    function sincronizarOverlay() {
        document.body.classList.toggle('wco-active', navigator.windowControlsOverlay.visible);
    }

    sincronizarOverlay();
    navigator.windowControlsOverlay.addEventListener('geometrychange', sincronizarOverlay);
})();

// Botón manual de actualizar datos de la página
(function () {
    var boton = document.getElementById('btn-refresh-page');
    if (!boton) {
        return;
    }

    boton.addEventListener('click', function () {
        window.location.reload();
    });
})();
