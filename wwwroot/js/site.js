// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Write your JavaScript code.

document.addEventListener('click', function (event) {
    var button = event.target.closest('[data-dialog-target]');
    if (!button) {
        return;
    }

    var dialog = document.getElementById(button.getAttribute('data-dialog-target'));
    if (dialog && typeof dialog.showModal === 'function') {
        dialog.showModal();
    }
});

// Botones de mostrar/ocultar las cajas de resumen por bróker
document.addEventListener('click', function (event) {
    var button = event.target.closest('[data-toggle-target]');
    if (!button) {
        return;
    }

    var target = document.getElementById(button.getAttribute('data-toggle-target'));
    if (!target) {
        return;
    }

    var estaOculto = target.classList.toggle('is-collapsed');
    button.textContent = estaOculto ? 'Mostrar' : 'Ocultar';

    var etiqueta = button.closest('.summary-strip-label');
    if (etiqueta) {
        etiqueta.classList.toggle('is-expanded', !estaOculto);
    }
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

// Fecha y hora de generación del informe, solo visible al imprimir
(function () {
    var el = document.getElementById('print-datetime');
    if (!el) {
        return;
    }

    function updatePrintDateTime() {
        var now = new Date();
        var fecha = now.toLocaleDateString('es-ES', { day: '2-digit', month: '2-digit', year: 'numeric' });
        var hora = now.toLocaleTimeString('es-ES', { hour: '2-digit', minute: '2-digit', second: '2-digit' });
        el.textContent = 'Informe generado el ' + fecha + ' a las ' + hora;
    }

    window.addEventListener('beforeprint', updatePrintDateTime);
})();

// Botón "Imprimir cartera": imprime solo cartera, cuenta corriente, efectivo
// y las casillas de resumen, ocultando los paneles de control de cotizaciones.
(function () {
    var boton = document.getElementById('btn-print-cartera');
    if (!boton) {
        return;
    }

    boton.addEventListener('click', function () {
        document.body.classList.add('print-cartera-only');
        window.print();
    });

    window.addEventListener('afterprint', function () {
        document.body.classList.remove('print-cartera-only');
    });
})();

// Alinea verticalmente los botones de imprimir con la fila de enlaces
// rápidos (Investing/Ingdirect/Trade Republic), manteniéndolos pegados al
// borde derecho de toda la página.
(function () {
    var acciones = document.getElementById('print-actions');
    var quickLinks = document.querySelector('.quick-links');
    var header = document.querySelector('.portfolio-header');
    if (!acciones || !quickLinks || !header) {
        return;
    }

    function reposicionar() {
        var headerRect = header.getBoundingClientRect();
        var quickLinksRect = quickLinks.getBoundingClientRect();
        acciones.style.top = (quickLinksRect.top - headerRect.top) + 'px';
        acciones.style.height = quickLinksRect.height + 'px';
    }

    reposicionar();
    window.addEventListener('resize', reposicionar);
    window.addEventListener('load', reposicionar);

    if (window.ResizeObserver) {
        new ResizeObserver(reposicionar).observe(quickLinks);
    }
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

// Botón de imprimir toda la página
(function () {
    var boton = document.getElementById('btn-print-page');
    if (!boton) {
        return;
    }

    boton.addEventListener('click', function () {
        window.print();
    });
})();
