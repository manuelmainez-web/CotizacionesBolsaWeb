const test = require('node:test');
const assert = require('node:assert/strict');

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

  if (Array.isArray(incomingRows)) {
    incomingRows.forEach((row) => pushRow(row, true));
  }

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
