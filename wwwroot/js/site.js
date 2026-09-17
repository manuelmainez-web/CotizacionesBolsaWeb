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
    var elementos = [
        document.getElementById('print-datetime'),
        document.getElementById('print-datetime-ing'),
        document.getElementById('print-datetime-tr'),
        document.getElementById('print-datetime-cartera')
    ].filter(Boolean);

    if (elementos.length === 0) {
        return;
    }

    function updatePrintDateTime() {
        var now = new Date();
        var fecha = now.toLocaleDateString('es-ES', { day: '2-digit', month: '2-digit', year: 'numeric' });
        var hora = now.toLocaleTimeString('es-ES', { hour: '2-digit', minute: '2-digit', second: '2-digit' });
        var texto = 'Informe generado el ' + fecha + ' a las ' + hora;
        elementos.forEach(function (el) {
            el.textContent = texto;
        });
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

// Diálogo "Añadir índice": selección guiada por país -> índice, sin
// escribir nombre, símbolo ni país a mano. El país determina las opciones
// del segundo desplegable (catálogo de índices conocidos); al elegir un
// índice se rellenan por JS los campos ocultos que espera el backend
// (name, symbol, countryCode).
(function () {
    var catalogoIndicesPorPais = {
        ES: { etiqueta: 'España', indices: [{ symbol: '^IBEX', name: 'IBEX 35' }] },
        DE: { etiqueta: 'Alemania', indices: [{ symbol: '^GDAXI', name: 'DAX' }] },
        EU: { etiqueta: 'Zona Euro', indices: [{ symbol: '^STOXX50E', name: 'EURO STOXX 50' }] },
        US: {
            etiqueta: 'Estados Unidos',
            indices: [
                { symbol: '^GSPC', name: 'S&P 500' },
                { symbol: '^DJI', name: 'Dow Jones Industrial Average' },
                { symbol: '^IXIC', name: 'Nasdaq Composite' }
            ]
        },
        FR: { etiqueta: 'Francia', indices: [{ symbol: '^FCHI', name: 'CAC 40' }] },
        JP: { etiqueta: 'Japón', indices: [{ symbol: '^N225', name: 'Nikkei 225' }] },
        IT: { etiqueta: 'Italia', indices: [{ symbol: 'FTSEMIB.MI', name: 'FTSE MIB' }] },
        CH: { etiqueta: 'Suiza', indices: [{ symbol: '^SSMI', name: 'SMI' }] },
        GB: { etiqueta: 'Reino Unido', indices: [{ symbol: '^FTSE', name: 'FTSE 100' }] }
    };

    var dialogo = document.getElementById('add-index-dialog');
    var selectPais = document.getElementById('add-index-country');
    var selectIndice = document.getElementById('add-index-select');
    var inputNombre = document.getElementById('add-index-name-hidden');
    var inputSimbolo = document.getElementById('add-index-symbol-hidden');
    var inputPais = document.getElementById('add-index-country-hidden');
    var botonAnadir = document.getElementById('add-index-submit');

    if (!dialogo || !selectPais || !selectIndice || !inputNombre || !inputSimbolo || !inputPais || !botonAnadir) {
        return;
    }

    function poblarPaises() {
        Object.keys(catalogoIndicesPorPais).forEach(function (codigo) {
            var opcion = document.createElement('option');
            opcion.value = codigo;
            opcion.textContent = catalogoIndicesPorPais[codigo].etiqueta;
            selectPais.appendChild(opcion);
        });
    }

    function resetIndice(mensaje) {
        selectIndice.innerHTML = '';
        var opcion = document.createElement('option');
        opcion.value = '';
        opcion.disabled = true;
        opcion.selected = true;
        opcion.textContent = mensaje;
        selectIndice.appendChild(opcion);
        selectIndice.disabled = true;
        inputNombre.value = '';
        inputSimbolo.value = '';
        botonAnadir.disabled = true;
    }

    function resetTodo() {
        selectPais.value = '';
        inputPais.value = '';
        resetIndice('Selecciona primero un país');
    }

    function poblarIndices(codigoPais) {
        var pais = catalogoIndicesPorPais[codigoPais];
        selectIndice.innerHTML = '';

        var opcionVacia = document.createElement('option');
        opcionVacia.value = '';
        opcionVacia.disabled = true;
        opcionVacia.selected = true;
        opcionVacia.textContent = 'Selecciona un índice';
        selectIndice.appendChild(opcionVacia);

        pais.indices.forEach(function (indice) {
            var opcion = document.createElement('option');
            opcion.value = indice.symbol;
            opcion.textContent = indice.name;
            opcion.setAttribute('data-name', indice.name);
            selectIndice.appendChild(opcion);
        });

        selectIndice.disabled = false;
        inputNombre.value = '';
        inputSimbolo.value = '';
        botonAnadir.disabled = true;
    }

    selectPais.addEventListener('change', function () {
        inputPais.value = selectPais.value;
        if (selectPais.value && catalogoIndicesPorPais[selectPais.value]) {
            poblarIndices(selectPais.value);
        } else {
            resetIndice('Selecciona primero un país');
        }
    });

    selectIndice.addEventListener('change', function () {
        var opcionSeleccionada = selectIndice.options[selectIndice.selectedIndex];
        if (!opcionSeleccionada || !opcionSeleccionada.value) {
            inputNombre.value = '';
            inputSimbolo.value = '';
            botonAnadir.disabled = true;
            return;
        }
        inputSimbolo.value = opcionSeleccionada.value;
        inputNombre.value = opcionSeleccionada.getAttribute('data-name') || opcionSeleccionada.textContent;
        botonAnadir.disabled = false;
    });

    // Al cerrar el diálogo (cancelar o tras añadir), resetear la selección
    // para que la próxima vez que se abra empiece limpio.
    dialogo.addEventListener('close', resetTodo);

    poblarPaises();
    resetTodo();
})();

// Diálogo "Añadir acción": selección guiada por índice -> acción, sin
// escribir el nombre a mano. El símbolo se sigue resolviendo en el
// servidor (SearchSymbolAsync) a partir del nombre elegido, priorizando el
// índice seleccionado, igual que antes. NOTA: los catálogos de NASDAQ-100
// y, sobre todo, S&P 500 son listas muy extensas y cambian de vez en
// cuando (altas/bajas trimestrales); esto es un mejor esfuerzo a fecha de
// creación de este catálogo, no una fuente oficial en vivo.
(function () {
    var catalogoAccionesPorIndice = {
        IBEX35: [
            'ACS', 'Acciona', 'Acciona Energía', 'Acerinox', 'Aena', 'Amadeus IT Group',
            'ArcelorMittal', 'Banco de Sabadell', 'Banco Santander', 'Bankinter', 'BBVA',
            'CaixaBank', 'Cellnex Telecom', 'Inmobiliaria Colonial', 'Enagás', 'Endesa',
            'Ferrovial', 'Fluidra', 'Grifols', 'IAG', 'Iberdrola', 'Inditex',
            'Indra Sistemas', 'Logista', 'Mapfre', 'Meliá Hotels International',
            'Merlin Properties', 'Naturgy Energy Group', 'Puig Brands', 'Redeia Corporación',
            'Repsol', 'Laboratorios Farmacéuticos Rovi', 'Sacyr',
            'Solaria Energía y Medio Ambiente', 'Telefónica', 'Unicaja Banco'
        ],
        CAC40: [
            'Air Liquide', 'Airbus', 'ArcelorMittal', 'Axa', 'BNP Paribas', 'Bouygues',
            'Capgemini', 'Carrefour', 'Crédit Agricole', 'Danone', 'Dassault Systèmes',
            'Edenred', 'Engie', 'EssilorLuxottica', 'Eurofins Scientific',
            'Hermès International', 'Kering', 'Legrand', "L'Oréal", 'LVMH', 'Michelin',
            'Orange', 'Pernod Ricard', 'Publicis Groupe', 'Renault', 'Safran',
            'Saint-Gobain', 'Sanofi', 'Schneider Electric', 'Société Générale',
            'STMicroelectronics', 'Stellantis', 'Teleperformance', 'Thales',
            'TotalEnergies', 'Unibail-Rodamco-Westfield', 'Veolia Environnement', 'Vinci',
            'Vivendi', 'Worldline'
        ],
        DAX: [
            'Adidas', 'Airbus', 'Allianz', 'BASF', 'Bayer', 'Beiersdorf', 'BMW',
            'Brenntag', 'Commerzbank', 'Continental', 'Daimler Truck', 'Deutsche Bank',
            'Deutsche Börse', 'Deutsche Post (DHL Group)', 'Deutsche Telekom', 'E.ON',
            'Fresenius', 'Fresenius Medical Care', 'Hannover Rück', 'Heidelberg Materials',
            'Henkel', 'Infineon Technologies', 'Mercedes-Benz Group', 'Merck KGaA',
            'MTU Aero Engines', 'Munich Re', 'Porsche AG', 'Porsche SE', 'Qiagen',
            'Rheinmetall', 'RWE', 'SAP', 'Sartorius', 'Siemens', 'Siemens Energy',
            'Siemens Healthineers', 'Symrise', 'Volkswagen Group', 'Vonovia', 'Zalando'
        ],
        EUROSTOXX50: [
            'Adyen', 'Ahold Delhaize', 'Air Liquide', 'Airbus', 'Allianz', 'ASML Holding',
            'Axa', 'BASF', 'Banco Santander', 'BBVA', 'BMW', 'BNP Paribas', 'CRH',
            'Danone', 'Deutsche Börse', 'Deutsche Post (DHL Group)', 'Deutsche Telekom',
            'Enel', 'Engie', 'Eni', 'EssilorLuxottica', 'Ferrari', 'Iberdrola', 'Inditex',
            'Infineon Technologies', 'Intesa Sanpaolo', 'Kering', "L'Oréal", 'LVMH',
            'Mercedes-Benz Group', 'Munich Re', 'Nokia', 'Pernod Ricard', 'Philips',
            'Prosus', 'Safran', 'Saint-Gobain', 'Sanofi', 'SAP', 'Schneider Electric',
            'Siemens', 'Siemens Healthineers', 'Société Générale', 'Stellantis',
            'TotalEnergies', 'UniCredit', 'Vinci', 'Vivendi', 'Volkswagen Group',
            'Flutter Entertainment'
        ],
        DOWJONES: [
            '3M', 'American Express', 'Amazon', 'Amgen', 'Apple', 'Boeing', 'Caterpillar',
            'Chevron', 'Cisco Systems', 'Coca-Cola', 'Goldman Sachs', 'Home Depot',
            'Honeywell', 'IBM', 'Johnson & Johnson', 'JPMorgan Chase', "McDonald's",
            'Merck & Co.', 'Microsoft', 'Nike', 'Nvidia', 'Procter & Gamble', 'Salesforce',
            'Sherwin-Williams', 'Travelers', 'UnitedHealth Group', 'Verizon Communications',
            'Visa', 'Walmart', 'Walt Disney'
        ],
        NIKKEI225: [
            'Toyota Motor', 'Sony Group', 'Keyence', 'Mitsubishi UFJ Financial Group',
            'Sumitomo Mitsui Financial Group', 'Mizuho Financial Group', 'SoftBank Group',
            'Nintendo', 'Fast Retailing', 'Tokyo Electron', 'Hitachi', 'Honda Motor',
            'Nissan Motor', 'Canon', 'Panasonic Holdings', 'Fujifilm Holdings', 'Fanuc',
            'Daikin Industries', 'Shin-Etsu Chemical', 'Murata Manufacturing', 'Advantest',
            'Recruit Holdings', 'KDDI', 'Nippon Telegraph and Telephone', 'Takeda Pharmaceutical',
            'Astellas Pharma', 'Daiichi Sankyo', 'Chugai Pharmaceutical', 'Mitsubishi Corporation',
            'Mitsui & Co.', 'Sumitomo Corporation', 'Itochu', 'Marubeni', 'Nippon Steel',
            'JFE Holdings', 'Kubota', 'Komatsu', 'Isuzu Motors', 'Subaru', 'Mazda Motor',
            'Bridgestone', 'Suzuki Motor', 'Yamaha Motor', 'Sekisui House', 'Daiwa House Industry',
            'Mitsubishi Estate', 'Mitsui Fudosan', 'Sumitomo Realty & Development',
            'East Japan Railway', 'Central Japan Railway', 'West Japan Railway',
            'ANA Holdings', 'Japan Airlines', 'Seven & i Holdings', 'Rakuten Group',
            'Dentsu Group', 'Shiseido', 'Kao', 'Asahi Group Holdings', 'Kirin Holdings',
            'Japan Tobacco', 'Nomura Holdings', 'Dai-ichi Life Holdings', 'Tokio Marine Holdings',
            'MS&AD Insurance Group', 'ORIX', 'Yamato Holdings', 'Nippon Yusen',
            'Mitsubishi Heavy Industries', 'Kawasaki Heavy Industries', 'IHI Corporation',
            'Sumco', 'Screen Holdings', 'Disco Corporation', 'Terumo', 'Olympus',
            'Konica Minolta', 'Ricoh', 'NEC', 'Fujitsu', 'Toshiba', 'Hoya', 'Nidec'
        ],
        HANGSENG: [
            'Alibaba Group Holding', 'Tencent Holdings', 'AIA Group', 'HSBC Holdings',
            'China Construction Bank', 'Industrial and Commercial Bank of China',
            'Bank of China', 'China Mobile', 'Meituan', 'JD.com', 'Ping An Insurance',
            'China Life Insurance', 'CNOOC', 'PetroChina', 'China Petroleum & Chemical',
            'Xiaomi', 'NetEase', 'BYD Company', 'Li Ning', 'Sands China',
            'Galaxy Entertainment Group', 'Wharf Real Estate Investment',
            'Sun Hung Kai Properties', 'CK Hutchison Holdings', 'CK Asset Holdings',
            'Hang Lung Properties', 'Link REIT', 'MTR Corporation', 'Power Assets Holdings',
            'CLP Holdings', 'Hong Kong Exchanges and Clearing', 'Bank of Communications',
            'China Merchants Bank', 'China CITIC Bank', 'Haier Smart Home', 'Lenovo Group',
            'China Resources Land', 'China Overseas Land & Investment', 'Longfor Group',
            'Shenzhou International', 'ANTA Sports Products', 'Kuaishou Technology',
            'Trip.com Group', 'China Shenhua Energy', 'China Unicom', 'Techtronic Industries',
            'WuXi Biologics', 'Zijin Mining Group', 'China Tower', 'ESR Group',
            'China Hongqiao Group', 'Budweiser Brewing Company APAC',
            'Orient Overseas International', 'Swire Pacific', 'New World Development',
            'Henderson Land Development', 'China Resources Beer', 'Cosco Shipping Holdings'
        ],
        NASDAQ: [
            'Apple', 'Microsoft', 'Amazon', 'Nvidia', 'Alphabet', 'Meta Platforms',
            'Broadcom', 'Tesla', 'Costco Wholesale', 'Netflix', 'Adobe', 'PepsiCo',
            'ASML Holding', 'T-Mobile US', 'Cisco Systems', 'Advanced Micro Devices',
            'Linde', 'Qualcomm', 'Intuit', 'Amgen', 'Texas Instruments',
            'Intuitive Surgical', 'Booking Holdings', 'Honeywell', 'Starbucks',
            'Gilead Sciences', 'Mondelez International', 'Applied Materials',
            'Regeneron Pharmaceuticals', 'Analog Devices', 'Vertex Pharmaceuticals',
            'Micron Technology', 'Lam Research', 'KLA Corporation',
            'Automatic Data Processing', 'Palo Alto Networks', 'Synopsys',
            'Cadence Design Systems', 'CrowdStrike Holdings', 'Marriott International',
            'MercadoLibre', 'Airbnb', 'Fortinet', 'Constellation Energy', 'Cintas',
            'PayPal Holdings', 'PACCAR', 'Workday', 'Monster Beverage',
            'Charter Communications', "O'Reilly Automotive", 'Ross Stores', 'Kraft Heinz',
            'Marvell Technology', 'NXP Semiconductors', 'Datadog', 'Roper Technologies',
            'Fastenal', 'American Electric Power', 'Exelon', 'Xcel Energy',
            'IDEXX Laboratories', 'CSX Corporation', 'GE HealthCare Technologies',
            'Baker Hughes', 'Copart', 'Ansys', 'CoStar Group', 'Verisk Analytics',
            'Old Dominion Freight Line', 'Zscaler', 'The Trade Desk', 'Diamondback Energy',
            'Take-Two Interactive Software', 'MongoDB', 'DoorDash', 'Electronic Arts',
            'Warner Bros Discovery', 'Sirius XM Holdings', 'ON Semiconductor',
            'Skyworks Solutions', 'Atlassian', 'Biogen', 'Align Technology',
            'Lululemon Athletica', 'GlobalFoundries', 'DexCom', 'Keurig Dr Pepper',
            'AppLovin', 'PDD Holdings', 'Ulta Beauty', 'Insulet', 'Illumina',
            'Axon Enterprise', 'Cognizant Technology Solutions', 'Comcast'
        ],
        SP500: [
            'Apple', 'Microsoft', 'Nvidia', 'Alphabet', 'Meta Platforms', 'Amazon',
            'Broadcom', 'Oracle', 'Salesforce', 'Adobe', 'Cisco Systems', 'Accenture',
            'IBM', 'Intel', 'Texas Instruments', 'Qualcomm', 'Advanced Micro Devices',
            'Applied Materials', 'Micron Technology', 'Lam Research', 'KLA Corporation',
            'Synopsys', 'Cadence Design Systems', 'ServiceNow', 'Intuit',
            'Automatic Data Processing', 'Palo Alto Networks', 'Fortinet',
            'CrowdStrike Holdings', 'Workday', 'Autodesk', 'Analog Devices',
            'NXP Semiconductors', 'Marvell Technology', 'ON Semiconductor',
            'Skyworks Solutions', 'Akamai Technologies', 'Juniper Networks',
            'Western Digital', 'Seagate Technology', 'HP Inc.', 'Dell Technologies',
            'Corning', 'Motorola Solutions', 'Zebra Technologies', 'Gartner',
            'Fair Isaac', 'Jack Henry & Associates', 'Global Payments', 'Fiserv',
            'Fidelity National Information Services', 'PayPal Holdings', 'Visa',
            'Mastercard', 'American Express', 'Block',
            'Netflix', 'Walt Disney', 'Comcast', 'Charter Communications', 'T-Mobile US',
            'Verizon Communications', 'AT&T', 'Electronic Arts',
            'Take-Two Interactive Software', 'Warner Bros Discovery', 'News Corporation',
            'Fox Corporation', 'Paramount Global', 'Interpublic Group', 'Omnicom Group',
            'Live Nation Entertainment',
            'Tesla', 'Home Depot', "McDonald's", 'Booking Holdings', 'Nike', "Lowe's",
            'Starbucks', 'TJX Companies', 'Marriott International', 'Chipotle Mexican Grill',
            'General Motors', 'Ford Motor', 'Las Vegas Sands', 'MGM Resorts International',
            'Yum! Brands', 'Ross Stores', 'Best Buy', 'Target', 'Tractor Supply',
            'D.R. Horton', 'Lennar', 'PulteGroup', 'Expedia Group', 'eBay', 'Etsy',
            'Carnival Corporation', 'Royal Caribbean Group', 'Norwegian Cruise Line Holdings',
            'Aptiv', 'Genuine Parts Company', "O'Reilly Automotive", 'AutoZone',
            'Advance Auto Parts', 'Whirlpool', 'Newell Brands', 'Hasbro', 'Mattel',
            'Dollar General', 'Dollar Tree', 'Ulta Beauty', 'Bath & Body Works',
            'Procter & Gamble', 'Coca-Cola', 'PepsiCo', 'Walmart', 'Costco Wholesale',
            'Philip Morris International', 'Altria Group', 'Mondelez International',
            'Colgate-Palmolive', 'Kimberly-Clark', 'General Mills', 'Kellanova', 'Hershey',
            'Conagra Brands', "Campbell's Company", 'McCormick & Company', 'Church & Dwight',
            'Clorox', 'Kroger', 'Sysco', 'Tyson Foods', 'Archer-Daniels-Midland',
            'Bunge Global', 'Constellation Brands', 'Brown-Forman', 'Molson Coors Beverage',
            'Monster Beverage', 'Keurig Dr Pepper', 'Estée Lauder Companies',
            'ExxonMobil', 'Chevron', 'ConocoPhillips', 'SLB', 'EOG Resources',
            'Marathon Petroleum', 'Phillips 66', 'Valero Energy', 'Occidental Petroleum',
            'Williams Companies', 'Kinder Morgan', 'ONEOK', 'Baker Hughes', 'Halliburton',
            'Devon Energy', 'Diamondback Energy', 'Coterra Energy', 'Targa Resources',
            'APA Corporation', 'Hess Corporation', 'Marathon Oil',
            'Berkshire Hathaway', 'JPMorgan Chase', 'Bank of America', 'Wells Fargo',
            'Citigroup', 'Goldman Sachs', 'Morgan Stanley', 'U.S. Bancorp',
            'PNC Financial Services', 'Truist Financial', 'Charles Schwab', 'S&P Global',
            "Moody's", 'Intercontinental Exchange', 'CME Group', 'Nasdaq Inc.',
            'Marsh & McLennan Companies', 'Aon', 'Arthur J. Gallagher & Co.', 'Chubb',
            'Progressive', 'Travelers Companies', 'Allstate', 'MetLife',
            'Prudential Financial', 'Aflac', 'American International Group', 'Blackstone',
            'KKR & Co.', 'Apollo Global Management', 'Ares Management',
            'T. Rowe Price Group', 'Franklin Resources', 'State Street',
            'Bank of New York Mellon', 'Northern Trust', 'Discover Financial Services',
            'Synchrony Financial', 'Capital One Financial', 'Regions Financial',
            'Fifth Third Bancorp', 'KeyCorp', 'Huntington Bancshares', 'M&T Bank',
            'Zions Bancorporation', 'Comerica',
            'UnitedHealth Group', 'Johnson & Johnson', 'Eli Lilly', 'Pfizer', 'AbbVie',
            'Merck & Co.', 'Thermo Fisher Scientific', 'Abbott Laboratories', 'Danaher',
            'Bristol-Myers Squibb', 'Amgen', 'Gilead Sciences', 'Vertex Pharmaceuticals',
            'Regeneron Pharmaceuticals', 'Moderna', 'CVS Health', 'Cigna Group',
            'Elevance Health', 'Humana', 'Centene', 'Molina Healthcare', 'HCA Healthcare',
            'Medtronic', 'Stryker', 'Boston Scientific', 'Becton Dickinson',
            'Edwards Lifesciences', 'Intuitive Surgical', 'Zimmer Biomet',
            'IDEXX Laboratories', 'Illumina', 'Align Technology', 'ResMed', 'DexCom',
            'Baxter International', 'Cardinal Health', 'McKesson', 'Cencora',
            'Waters Corporation', 'Mettler-Toledo', 'Charles River Laboratories',
            'IQVIA Holdings', 'Catalent',
            'Boeing', 'Honeywell International', 'Caterpillar', 'General Electric',
            'GE Aerospace', 'RTX Corporation', 'Lockheed Martin', 'Northrop Grumman',
            'L3Harris Technologies', 'Union Pacific', 'Norfolk Southern', 'CSX Corporation',
            'United Parcel Service', 'FedEx', 'Delta Air Lines', 'United Airlines Holdings',
            'Southwest Airlines', 'American Airlines Group', '3M', 'Parker Hannifin',
            'Illinois Tool Works', 'Emerson Electric', 'Eaton Corporation', 'Cummins',
            'Deere & Company', 'Otis Worldwide', 'Carrier Global',
            'Johnson Controls International', 'Ingersoll Rand', 'Dover Corporation',
            'Roper Technologies', 'Rockwell Automation', 'Xylem', 'PACCAR',
            'Old Dominion Freight Line', 'J.B. Hunt Transport Services', 'Waste Management',
            'Republic Services', 'Cintas', 'Stanley Black & Decker', 'Masco', 'Fastenal',
            'W.W. Grainger', 'Leidos Holdings', 'Textron', 'Howmet Aerospace',
            'TransDigm Group',
            'Linde', 'Air Products and Chemicals', 'Sherwin-Williams', 'Ecolab',
            'Freeport-McMoRan', 'Newmont Corporation', 'Nucor', 'Dow Inc.',
            'DuPont de Nemours', 'LyondellBasell Industries', 'PPG Industries',
            'International Flavors & Fragrances', 'Ball Corporation', 'Avery Dennison',
            'Corteva', 'Mosaic Company', 'CF Industries Holdings', 'Albemarle',
            'Martin Marietta Materials', 'Vulcan Materials', 'International Paper',
            'Packaging Corporation of America', 'Amcor',
            'American Tower', 'Prologis', 'Equinix', 'Crown Castle', 'Public Storage',
            'Simon Property Group', 'Realty Income', 'Digital Realty Trust', 'Welltower',
            'Extra Space Storage', 'AvalonBay Communities', 'Equity Residential',
            'Iron Mountain', 'Ventas', 'Host Hotels & Resorts',
            'Federal Realty Investment Trust', 'SBA Communications', 'Weyerhaeuser',
            'Mid-America Apartment Communities', 'UDR Inc.', 'Essex Property Trust',
            'Camden Property Trust', 'Invitation Homes',
            'NextEra Energy', 'Duke Energy', 'Southern Company', 'Dominion Energy',
            'American Electric Power', 'Sempra', 'Exelon', 'Xcel Energy',
            'WEC Energy Group', 'Consolidated Edison', 'Public Service Enterprise Group',
            'Entergy', 'Edison International', 'FirstEnergy', 'DTE Energy', 'Ameren',
            'CMS Energy', 'Eversource Energy', 'AES Corporation', 'Atmos Energy',
            'CenterPoint Energy', 'NiSource', 'Evergy', 'Pinnacle West Capital',
            'Portland General Electric'
        ]
    };

    var selectMercado = document.getElementById('add-stock-market');
    var selectAccion = document.getElementById('add-stock-name');
    var botonAnadirAccion = document.getElementById('add-stock-submit');
    var dialogoAccion = document.getElementById('add-stock-dialog');

    if (!selectMercado || !selectAccion || !botonAnadirAccion || !dialogoAccion) {
        return;
    }

    function resetAccion(mensaje) {
        selectAccion.innerHTML = '';
        var opcion = document.createElement('option');
        opcion.value = '';
        opcion.disabled = true;
        opcion.selected = true;
        opcion.textContent = mensaje;
        selectAccion.appendChild(opcion);
        selectAccion.disabled = true;
        botonAnadirAccion.disabled = true;
    }

    function poblarAcciones(codigoIndice) {
        var acciones = catalogoAccionesPorIndice[codigoIndice] || [];
        selectAccion.innerHTML = '';

        var opcionVacia = document.createElement('option');
        opcionVacia.value = '';
        opcionVacia.disabled = true;
        opcionVacia.selected = true;
        opcionVacia.textContent = 'Selecciona una acción';
        selectAccion.appendChild(opcionVacia);

        acciones.slice().sort(function (a, b) { return a.localeCompare(b, 'es'); }).forEach(function (nombre) {
            var opcion = document.createElement('option');
            opcion.value = nombre;
            opcion.textContent = nombre;
            selectAccion.appendChild(opcion);
        });

        selectAccion.disabled = acciones.length === 0;
        botonAnadirAccion.disabled = true;
    }

    selectMercado.addEventListener('change', function () {
        if (selectMercado.value && catalogoAccionesPorIndice[selectMercado.value]) {
            poblarAcciones(selectMercado.value);
        } else {
            resetAccion('Selecciona primero un índice');
        }
    });

    selectAccion.addEventListener('change', function () {
        botonAnadirAccion.disabled = !selectAccion.value;
    });

    dialogoAccion.addEventListener('close', function () {
        selectMercado.value = '';
        resetAccion('Selecciona primero un índice');
    });

    resetAccion('Selecciona primero un índice');
})();

// Botón de imprimir toda la página
(function () {
    var boton = document.getElementById('btn-print-page');
    if (!boton) {
        return;
    }

    boton.addEventListener('click', function () {
        imprimirConNumeracion('print-all');
    });
})();

// Reorganiza, para "Imprimir cartera" e "Imprimir todo", las tablas de
// cartera (ETF, Fondos, Acciones, Planes de pensiones) agrupándolas por
// bróker: primero el logotipo de ING Direct seguido de sus tablas (solo
// filas ING), y después el logotipo de Trade Republic seguido de sus
// tablas (solo filas TR). Las tablas sin ninguna fila de ese bróker se
// omiten (igual que el resto de casos de "tabla vacía").
function reorganizarCarteraPorBroker(shellClone) {
    var origen = window.location.origin;

    function filtrarFilasPorBroker(tabla, broker) {
        var algunaVisible = false;
        tabla.querySelectorAll('tbody tr[data-broker]').forEach(function (fila) {
            if (fila.getAttribute('data-broker') === broker) {
                algunaVisible = true;
            } else {
                fila.style.display = 'none';
            }
        });
        return algunaVisible;
    }

    function construirGrupoBroker(paneles, broker, logoSrc, logoAlt, resumen) {
        var grupo = document.createElement('div');
        grupo.className = 'print-broker-group';

        var logo = document.createElement('img');
        logo.className = 'print-broker-group-logo';
        logo.src = logoSrc;
        logo.alt = logoAlt;
        grupo.appendChild(logo);

        if (resumen) {
            grupo.appendChild(resumen);
        }

        paneles.forEach(function (panelOriginal) {
            var panel = panelOriginal.cloneNode(true);
            // Al clonar cada panel dos veces (una por bróker) se duplicarían
            // ids (tbody, etc.), lo que confunde a paged.js al maquetar.
            // Como en el HTML impreso no se usan, se eliminan del clon.
            panel.removeAttribute('id');
            panel.querySelectorAll('[id]').forEach(function (el) {
                el.removeAttribute('id');
            });
            if (filtrarFilasPorBroker(panel, broker)) {
                grupo.appendChild(panel);
            }
        });

        return grupo;
    }

    var paneles = Array.prototype.filter.call(
        shellClone.querySelectorAll('.market-panel'),
        function (panel) { return panel.querySelector('tr[data-broker]'); }
    );

    if (paneles.length === 0) {
        return;
    }

    var primerPanel = paneles[0];
    var resumenIng = shellClone.querySelector('#summary-strip-ing');
    var resumenTr = shellClone.querySelector('#summary-strip-tr');
    var grupoIng = construirGrupoBroker(paneles, 'ING', origen + '/images/brokers/ing-direct-logo.png', 'ING Direct', resumenIng);
    var grupoTr = construirGrupoBroker(paneles, 'TR', origen + '/images/brokers/logotipo-trade-republic.png', 'Trade Republic', resumenTr);

    // Salto de página antes del grupo de Trade Republic para que cada
    // bróker empiece en una página distinta (no se usa ":first-of-type"
    // en CSS porque ese pseudo-selector compara por etiqueta <div>, no por
    // clase, y no distinguiría de forma fiable el primer grupo del resto).
    grupoTr.classList.add('print-broker-group-break');

    primerPanel.parentNode.insertBefore(grupoIng, primerPanel);
    primerPanel.parentNode.insertBefore(grupoTr, primerPanel);
    paneles.forEach(function (panel) {
        panel.remove();
    });
}

// Impresión con numeración real de página ("Página - N -") usando paged.js.
// Chrome/Edge no exponen counter(page) ni los márgenes @page al imprimir de
// forma nativa, así que se clona el contenido ya filtrado (reutilizando el
// mismo 'beforeprint'/'afterprint' que aplican los filtros de bróker/cartera
// y ocultan paneles vacíos), se repagina esa copia con la librería paged.js
// dentro de un <iframe> oculto (no se abre ninguna ventana/pestaña visible)
// y se invoca la impresión de ese iframe. El iframe se elimina solo al
// terminar de imprimir (o cancelar).
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

    if (bodyClass === 'print-cartera-only' || bodyClass === 'print-all') {
        reorganizarCarteraPorBroker(copiaContenido);
    }

    var anterior = document.getElementById('iframe-impresion-cartera');
    if (anterior) {
        anterior.remove();
    }

    var iframe = document.createElement('iframe');
    iframe.id = 'iframe-impresion-cartera';
    iframe.style.position = 'fixed';
    iframe.style.top = '0';
    iframe.style.left = '0';
    iframe.style.width = '0';
    iframe.style.height = '0';
    iframe.style.border = '0';
    iframe.style.opacity = '0';
    iframe.style.pointerEvents = 'none';
    document.body.appendChild(iframe);

    var origen = window.location.origin;
    var clasesBody = 'notranslate' + (bodyClass ? ' ' + bodyClass : '');
    var ventana = iframe.contentWindow;

    ventana.document.open();
    ventana.document.write(
        '<!DOCTYPE html><html lang="es"><head><meta charset="utf-8">' +
        '<title>Imprimiendo...</title></head><body class="' + clasesBody + '"></body></html>'
    );
    ventana.document.close();

    function limpiarIframe() {
        if (iframe && iframe.parentNode) {
            iframe.parentNode.removeChild(iframe);
        }
    }

    ventana.addEventListener('afterprint', limpiarIframe);

    var scriptConfig = ventana.document.createElement('script');
    scriptConfig.textContent = 'window.PagedConfig = { auto: false };';
    ventana.document.head.appendChild(scriptConfig);

    var scriptPaged = ventana.document.createElement('script');
    scriptPaged.src = 'https://unpkg.com/pagedjs/dist/paged.polyfill.js';
    scriptPaged.onload = function () {
        // Se añade "?v=" con la hora actual para evitar que el navegador
        // sirva una copia cacheada antigua de estos CSS (no llevan la
        // versión que sí añade asp-append-version en la página principal).
        var cacheBuster = '?v=' + Date.now();
        var hojasDeEstilo = [
            origen + '/lib/bootstrap/dist/css/bootstrap.min.css' + cacheBuster,
            origen + '/css/site.css' + cacheBuster,
            origen + '/css/print-pagination.css' + cacheBuster
        ];
        var previsualizador = new ventana.Paged.Previewer();
        previsualizador.preview(copiaContenido.outerHTML, hojasDeEstilo, ventana.document.body).then(function () {
            ventana.focus();
            ventana.print();
            // Salvaguarda por si el navegador no dispara 'afterprint' en el iframe
            setTimeout(limpiarIframe, 60000);
        });
    };
    ventana.document.head.appendChild(scriptPaged);
}
