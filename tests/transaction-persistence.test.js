const test = require('node:test');
const assert = require('node:assert/strict');
const { ensureTransactionSummary, formatTransactionSummaryText } = require('../wwwroot/js/transaction-summary.js');

function buildLegacyRowIdentity(row) {
  if (!row || typeof row !== 'object') {
    return '';
  }

  if (row.id && String(row.id).trim()) {
    return `id:${String(row.id).trim()}`;
  }

  const title = String(row.titulo || row.concept || '').trim().toLowerCase();
  const operation = String(row.tipoOperacion || '').trim().toUpperCase();
  const date = String(row.fechaOperacion || '').trim();
  const concept = String(row.concept || '').trim().toLowerCase();

  return ['legacy', title, operation, date, concept].join('|');
}

function ensureRowId(row, index) {
  if (!row || typeof row !== 'object') {
    return row;
  }

  if (row.id && String(row.id).trim()) {
    return row;
  }

  const seed = [
    String(row.titulo || row.concept || ''),
    String(row.tipoOperacion || ''),
    String(row.fechaOperacion || ''),
    String(row.concept || '')
  ].join('|');

  const hash = Array.from(seed).reduce((total, character) => total + character.charCodeAt(0), 0).toString(16);
  row.id = `tx-${hash}`;
  return row;
}

function mergeUniqueRows(baseRows, incomingRows) {
  const currentRows = Array.isArray(incomingRows) ? incomingRows : Array.isArray(baseRows) ? baseRows : [];

  if (currentRows.length === 0) {
    return [];
  }

  const merged = [];
  const seen = new Map();

  function pushRow(row, preferIncoming) {
    if (!row || typeof row !== 'object') {
      return;
    }

    ensureRowId(row, merged.length);
    const signature = buildLegacyRowIdentity(row);

    if (seen.has(signature)) {
      const index = seen.get(signature);
      if (preferIncoming) {
        merged[index] = row;
      }
      return;
    }

    seen.set(signature, merged.length);
    merged.push(row);
  }

  if (Array.isArray(baseRows)) {
    baseRows.forEach((row) => pushRow(row, false));
  }

  currentRows.forEach((row) => pushRow(row, true));

  return merged;
}

test('edición de una operación mantiene el mismo id y reemplaza la fila antigua', () => {
  const baseRows = [
    {
      id: 'tx-1',
      titulo: 'Acciona, S.A.',
      tipoOperacion: 'COMPRA',
      numeroTitulos: 10,
      importeTotal: 1500,
      fechaOperacion: '2026-10-06',
      concept: 'COMPRA - Acciona, S.A.',
      valor: 1500
    }
  ];

  const incomingRows = [
    {
      id: 'tx-1',
      titulo: 'Acciona, S.A.',
      tipoOperacion: 'COMPRA',
      numeroTitulos: 12,
      importeTotal: 1800,
      fechaOperacion: '2026-10-07',
      concept: 'COMPRA - Acciona, S.A.',
      valor: 1800
    }
  ];

  const result = mergeUniqueRows(baseRows, incomingRows);

  assert.deepStrictEqual(result, incomingRows);
});

test('las filas sin título pero con concepto válido no desaparecen al recargar la página móvil', () => {
  const rows = [
    {
      titulo: '',
      tipoOperacion: 'COMPRA',
      numeroTitulos: 3,
      importeTotal: 123.45,
      fechaOperacion: '2026-10-07',
      concept: 'COMPRA',
      valor: 123.45
    }
  ];

  const normalized = rows.map((row) => {
    const title = String(row.titulo || row.concept || '').trim() || String(row.tipoOperacion || '').trim() || 'Movimiento';
    row.titulo = title;
    row.concept = row.concept || title;
    return row;
  });

  assert.equal(normalized.length, 1);
  assert.equal(normalized[0].titulo, 'COMPRA');
  assert.equal(normalized[0].concept, 'COMPRA');
});

test('si se elimina la última fila, la lista actual queda vacía y no reaparece con el estado anterior', () => {
  const baseRows = [{ id: 'tx-1', titulo: 'A', tipoOperacion: 'COMPRA', numeroTitulos: 1, importeTotal: 10, fechaOperacion: '2026-10-01' }];
  const currentRows = [];

  const result = mergeUniqueRows(baseRows, currentRows);

  assert.deepStrictEqual(result, []);
});

test('las filas no relacionadas se mantienen separadas y no se eliminan', () => {
  const baseRows = [
    { id: 'tx-1', titulo: 'A', tipoOperacion: 'COMPRA', numeroTitulos: 1, importeTotal: 10, fechaOperacion: '2026-10-01' },
    { id: 'tx-2', titulo: 'B', tipoOperacion: 'VENTA', numeroTitulos: 2, importeTotal: 20, fechaOperacion: '2026-10-02' }
  ];

  const incomingRows = [
    { id: 'tx-3', titulo: 'C', tipoOperacion: 'COMPRA', numeroTitulos: 3, importeTotal: 30, fechaOperacion: '2026-10-03' }
  ];

  const result = mergeUniqueRows(baseRows, incomingRows);

  assert.equal(result.length, 3);
  assert.deepStrictEqual(result.map((row) => row.id), ['tx-1', 'tx-2', 'tx-3']);
});

test('modificar el importe de una operación antigua sin id la reemplaza como la misma fila', () => {
  const baseRows = [
    {
      titulo: 'Acciona, S.A.',
      tipoOperacion: 'COMPRA',
      numeroTitulos: 10,
      importeTotal: 1500,
      fechaOperacion: '2026-10-06',
      concept: 'COMPRA - Acciona, S.A.'
    }
  ];

  const incomingRows = [
    {
      titulo: 'Acciona, S.A.',
      tipoOperacion: 'COMPRA',
      numeroTitulos: 10,
      importeTotal: 1800,
      fechaOperacion: '2026-10-06',
      concept: 'COMPRA - Acciona, S.A.'
    }
  ];

  const result = mergeUniqueRows(baseRows, incomingRows);

  assert.equal(result.length, 1);
  assert.equal(result[0].importeTotal, 1800);
  assert.equal(result[0].numeroTitulos, 10);
});

test('las tarjetas de Trade Republic deben conservar siempre su resumen visible', () => {
  const name = {
    appendChild(child) {
      this.child = child;
      return child;
    }
  };

  const card = {
    getAttribute(attributeName) {
      if (attributeName === 'data-transaction-type') {
        return 'purchase';
      }
      return null;
    },
    querySelector(selector) {
      if (selector === '.transaction-broker-name') {
        return name;
      }
      return null;
    }
  };

  const result = ensureTransactionSummary(card, [{ importeTotal: 123.45 }, { importeTotal: 234.55 }]);

  assert.ok(result);
  assert.equal(result.textContent, '(Importe total compras: 358,00 €)');
  assert.equal(formatTransactionSummaryText('purchase', 358), '(Importe total compras: 358,00 €)');
});

test('el diálogo de compras y ventas usa un fallback para navegadores móviles sin showModal', () => {
  const html = require('node:fs').readFileSync(require('node:path').join(__dirname, '..', 'Pages', 'Index.cshtml'), 'utf8');

  assert.match(html, /function openDialogSafely\s*\(/);
  assert.match(html, /setAttribute\(\s*['\"]open['\"],\s*['\"]open['\"]\s*\)/);
  assert.match(html, /function closeDialogSafely\s*\(/);
  assert.match(html, /removeAttribute\(\s*['\"]open['\"]\s*\)/);
});

test('los bloques de compra y venta se guardan y eliminan en el servidor como el resto de tablas', () => {
  const html = require('node:fs').readFileSync(require('node:path').join(__dirname, '..', 'Pages', 'Index.cshtml'), 'utf8');
  const source = require('node:fs').readFileSync(require('node:path').join(__dirname, '..', 'Pages', 'Index.cshtml.cs'), 'utf8');

  assert.match(html, /asp-page-handler="SaveTransaction"/);
  assert.match(html, /asp-page-handler="DeleteTransaction"/);
  assert.match(source, /OnPostSaveTransactionAsync/);
  assert.match(source, /OnPostDeleteTransactionAsync/);
  assert.doesNotMatch(html, /localStorage/);
  assert.doesNotMatch(html, /handler=SaveTransactionState|handler=TransactionState/);
});

test('el QR local se genera desde el host actual cuando no hay PublicUrl configurada', () => {
  const source = require('node:fs').readFileSync(require('node:path').join(__dirname, '..', 'Pages', 'Index.cshtml.cs'), 'utf8');

  assert.match(source, /ResolvePublicUrl\s*\(/);
  assert.match(source, /HttpContext\?\.Request/);
  assert.match(source, /scheme\s*==|request\.Scheme/);
});

test('las operaciones de compra y venta se cargan una sola vez desde el estado del servidor y se limpian duplicados', () => {
  const source = require('node:fs').readFileSync(require('node:path').join(__dirname, '..', 'Pages', 'Index.cshtml.cs'), 'utf8');

  assert.match(source, /LoadTransactionStateAsync/);
  assert.match(source, /_transactionState = await LoadTransactionStateAsync\(\)/);
  assert.match(source, /seen\.Add\(signature\)/);
  assert.match(source, /cotizaciones\.transactions\./);
  assert.match(source, /LoadJsonAsync<Dictionary<string, List<BrokerTransactionRow>>>\(TransactionStateKey\)/);
  assert.match(source, /rows\.RemoveAll\(r => r\.Id == id\)/);
});

test('el estado de transacciones debe serializarse y leerse con nombres de propiedad compatibles entre navegador y servidor', () => {
  const source = require('node:fs').readFileSync(require('node:path').join(__dirname, '..', 'Services', 'DataStore.cs'), 'utf8');

  assert.match(source, /PropertyNameCaseInsensitive\s*\=\s*true/);
  assert.match(source, /PropertyNamingPolicy\s*\=\s*JsonNamingPolicy\.CamelCase/);
});

test('las tablas de compra-venta se refrescan automáticamente junto al resto de tablas', () => {
  const html = require('node:fs').readFileSync(require('node:path').join(__dirname, '..', 'Pages', 'Index.cshtml'), 'utf8');

  assert.match(html, /'transaction-broker-grid-ing'/);
  assert.match(html, /'transaction-broker-grid-tr'/);
});
