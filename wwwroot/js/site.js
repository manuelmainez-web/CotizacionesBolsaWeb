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
        imprimirConNumeracion('print-cartera-only');
    });
})();

// Botones "Imprimir" de ING Direct y Trade Republic: imprimen solo las filas
// de la cartera de ese bróker, ocultando el resto de brókeres, cuenta
// corriente, efectivo y los paneles de control de cotizaciones.
(function () {
    var configuraciones = [
        { id: 'btn-print-ing', clase: 'print-broker-ing' },
        { id: 'btn-print-tr', clase: 'print-broker-tr' }
    ];

    configuraciones.forEach(function (config) {
        var boton = document.getElementById(config.id);
        if (!boton) {
            return;
        }

        boton.addEventListener('click', function () {
            imprimirConNumeracion(config.clase);
        });
    });
})();

// Al imprimir (cualquiera de los botones o Ctrl+P), oculta por completo los
// paneles cuya tabla no tenga ninguna fila de datos visible (ni las vacías
// por defecto, ni las descartadas por el filtro de bróker). No se usa
// getComputedStyle porque el navegador no garantiza que los estilos de
// @media print ya estén aplicados en el momento del evento 'beforeprint';
// en su lugar se replica la misma condición que usa el CSS de impresión
// (atributo data-broker + clase del body) directamente en JS.
(function () {
    function filaTieneDatos(fila) {
        if (fila.querySelector('.empty-state-cell')) {
            return false;
        }

        var broker = fila.getAttribute('data-broker');
        if (broker !== null) {
            if (document.body.classList.contains('print-broker-ing') && broker !== 'ING') {
                return false;
            }
            if (document.body.classList.contains('print-broker-tr') && broker !== 'TR') {
                return false;
            }
        }

        return true;
    }

    function ocultarPanelesVacios() {
        document.querySelectorAll('.market-panel').forEach(function (panel) {
            var filas = panel.querySelectorAll('tbody tr');
            if (filas.length === 0) {
                return;
            }

            var algunaVisible = false;

            filas.forEach(function (fila) {
                if (filaTieneDatos(fila)) {
                    algunaVisible = true;
                } else if (fila.hasAttribute('data-broker')) {
                    // Oculta también por JS (además del CSS de @media print)
                    // las filas descartadas por el filtro de bróker, para que
                    // el cálculo de altura/páginas sea fiable sin depender de
                    // que el navegador ya haya aplicado los estilos de
                    // impresión en el momento de 'beforeprint'.
                    fila.setAttribute('data-print-hidden-row', 'true');
                    fila.style.display = 'none';
                }
            });

            if (!algunaVisible) {
                panel.setAttribute('data-print-hidden-empty', 'true');
                panel.style.display = 'none';
            }
        });
    }

    function restaurarPanelesVacios() {
        document.querySelectorAll('[data-print-hidden-empty="true"]').forEach(function (panel) {
            panel.style.display = '';
            panel.removeAttribute('data-print-hidden-empty');
        });
        document.querySelectorAll('[data-print-hidden-row="true"]').forEach(function (fila) {
            fila.style.display = '';
            fila.removeAttribute('data-print-hidden-row');
        });
    }

    window.addEventListener('beforeprint', ocultarPanelesVacios);
    window.addEventListener('afterprint', restaurarPanelesVacios);
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
        imprimirConNumeracion(null);
    });
})();

// Impresión con numeración real de página ("Página - N -") usando paged.js.
// Chrome/Edge no exponen counter(page) ni los márgenes @page al imprimir de
// forma nativa, así que se abre una ventana nueva, se clona el contenido ya
// filtrado (reutilizando el mismo 'beforeprint'/'afterprint' que aplican los
// filtros de bróker/cartera y ocultan paneles vacíos) y se repagina esa copia
// con la librería paged.js, que sí soporta counter(page) en @page, antes de
// invocar la impresión de esa ventana.
function imprimirConNumeracion(bodyClass) {
    if (bodyClass) {
        document.body.classList.add(bodyClass);
    }

    window.dispatchEvent(new Event('beforeprint'));
    var copiaContenido = document.getElementById('market-shell').cloneNode(true);
    window.dispatchEvent(new Event('afterprint'));

    if (bodyClass) {
        document.body.classList.remove(bodyClass);
    }

    var ventana = window.open('', 'ventana-impresion-cartera');
    if (!ventana) {
        // El navegador bloqueó la ventana emergente: recurrir a la impresión normal
        if (bodyClass) {
            document.body.classList.add(bodyClass);
        }
        window.print();
        window.addEventListener('afterprint', function limpiar() {
            if (bodyClass) {
                document.body.classList.remove(bodyClass);
            }
            window.removeEventListener('afterprint', limpiar);
        });
        return;
    }

    var origen = window.location.origin;
    var clasesBody = 'notranslate' + (bodyClass ? ' ' + bodyClass : '');

    ventana.document.open();
    ventana.document.write(
        '<!DOCTYPE html><html lang="es"><head><meta charset="utf-8">' +
        '<title>Imprimiendo...</title></head><body class="' + clasesBody + '"></body></html>'
    );
    ventana.document.close();

    var scriptConfig = ventana.document.createElement('script');
    scriptConfig.textContent = 'window.PagedConfig = { auto: false };';
    ventana.document.head.appendChild(scriptConfig);

    var scriptPaged = ventana.document.createElement('script');
    scriptPaged.src = 'https://unpkg.com/pagedjs/dist/paged.polyfill.js';
    scriptPaged.onload = function () {
        var hojasDeEstilo = [
            origen + '/lib/bootstrap/dist/css/bootstrap.min.css',
            origen + '/css/site.css',
            origen + '/css/print-pagination.css'
        ];
        var previsualizador = new ventana.Paged.Previewer();
        previsualizador.preview(copiaContenido.outerHTML, hojasDeEstilo, ventana.document.body).then(function () {
            ventana.focus();
            ventana.print();
        });
    };
    ventana.document.head.appendChild(scriptPaged);
}
