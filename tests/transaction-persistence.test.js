const test = require('node:test');
const assert = require('node:assert/strict');

function mergeUniqueRows(baseRows, incomingRows) {
  const merged = [];
  const seen = new Map();

  function pushRow(row, preferIncoming) {
    if (!row || typeof row !== 'object') {
      return;
    }

    const signature = row.id || [
      String(row.titulo || row.concept || ''),
      String(row.tipoOperacion || ''),
      String(row.numeroTitulos !== undefined ? row.numeroTitulos : row.cantidad || 0),
      String(row.importeTotal !== undefined ? row.importeTotal : 0),
      String(row.fechaOperacion || '')
    ].join('||');

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
