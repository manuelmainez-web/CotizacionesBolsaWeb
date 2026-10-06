(function (root) {
  function formatCurrency(value) {
    var numberValue = Number(value || 0);
    if (!Number.isFinite(numberValue)) {
      numberValue = 0;
    }

    return new Intl.NumberFormat('es-ES', {
      minimumFractionDigits: 2,
      maximumFractionDigits: 2
    }).format(numberValue);
  }

  function formatTransactionSummaryText(type, totalValue) {
    var label = type === 'purchase' ? 'Importe total compras: ' : 'Importe total ventas: ';
    return '(' + label + formatCurrency(totalValue) + ' €)';
  }

  function getTotalValue(rows) {
    if (!Array.isArray(rows)) {
      return 0;
    }

    return rows.reduce(function (accumulator, row) {
      var value = Number(row && row.importeTotal !== undefined && row.importeTotal !== null ? row.importeTotal : 0);
      return accumulator + (Number.isFinite(value) ? value : 0);
    }, 0);
  }

  function ensureTransactionSummary(card, rows) {
    if (!card || typeof card !== 'object') {
      return null;
    }

    var summary = card.querySelector ? card.querySelector('.transaction-total-summary') : null;
    if (!summary) {
      var name = card.querySelector ? card.querySelector('.transaction-broker-name') : null;
      if (!name) {
        return null;
      }

      if (typeof document !== 'undefined' && document.createElement) {
        summary = document.createElement('span');
        summary.className = 'transaction-total-summary';
        name.appendChild(summary);
      } else if (typeof name.appendChild === 'function') {
        summary = { textContent: '' };
        name.appendChild(summary);
      } else {
        return null;
      }
    }

    var type = card.getAttribute ? card.getAttribute('data-transaction-type') : null;
    if (!type) {
      return summary;
    }

    summary.textContent = formatTransactionSummaryText(type, getTotalValue(rows));
    return summary;
  }

  var api = {
    formatCurrency: formatCurrency,
    formatTransactionSummaryText: formatTransactionSummaryText,
    getTotalValue: getTotalValue,
    ensureTransactionSummary: ensureTransactionSummary
  };

  if (typeof module !== 'undefined' && module.exports) {
    module.exports = api;
  }

  root.TransactionSummary = api;
}(typeof window !== 'undefined' ? window : globalThis));
