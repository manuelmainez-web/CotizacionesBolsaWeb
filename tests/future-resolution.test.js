const test = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');

const futuresFile = path.join(__dirname, '..', 'App_Data', 'custom-futures.json');
const futures = JSON.parse(fs.readFileSync(futuresFile, 'utf8'));

function resolveFutureSymbol(name, symbol) {
  const normalizedName = (name || '').trim();
  const normalizedSymbol = (symbol || '').trim();

  if (normalizedName.includes('NASDAQ') || normalizedSymbol === '^IXIC') return 'NQ=F';
  if (normalizedName.includes('SP 500') || normalizedSymbol === '^GSPC') return 'ES=F';
  if (normalizedName.includes('DOW') || normalizedSymbol === '^DJI') return 'YM=F';
  if (normalizedName.includes('DAX') || normalizedSymbol === '^GDAXI') return 'Q2JF.DE';
  if (normalizedName.includes('CAC') || normalizedSymbol === '^FCHI') return 'CAFME.PA';
  if (normalizedName.includes('EURO STOXX') || normalizedSymbol === '^STOXX50E') return '797B.Z';
  if (normalizedName.includes('NIKKEI') || normalizedSymbol === '^N225') return '^NKFT.OS';
  if (normalizedName.includes('IBEX') || normalizedSymbol === '^IBEX') return '';
  if (normalizedName.includes('VIX') || normalizedSymbol === '^VIX') return '';
  return normalizedSymbol.includes('=F') ? normalizedSymbol : '';
}

test('los futuros conocidos usan tickers de futuro reales y no los índices', () => {
  assert.equal(resolveFutureSymbol('NASDAQ', '^IXIC'), 'NQ=F');
  assert.equal(resolveFutureSymbol('SP 500', '^GSPC'), 'ES=F');
  assert.equal(resolveFutureSymbol('DOW JONES', '^DJI'), 'YM=F');
  assert.equal(resolveFutureSymbol('DAX', '^GDAXI'), 'Q2JF.DE');
  assert.equal(resolveFutureSymbol('CAC 40', '^FCHI'), 'CAFME.PA');
  assert.equal(resolveFutureSymbol('EURO STOXX 50', '^STOXX50E'), '797B.Z');
  assert.equal(resolveFutureSymbol('NIKKEI 225', '^N225'), '^NKFT.OS');
});

test('la tabla de futuros no reutiliza símbolos de índice como si fueran futuros', () => {
  const futureSymbols = new Map(futures.map((item) => [item.Name, item.Symbol]));

  assert.equal(futureSymbols.get('NASDAQ'), 'NQ=F');
  assert.equal(futureSymbols.get('SP 500'), 'ES=F');
  assert.equal(futureSymbols.get('DOW JONES'), 'YM=F');
  assert.equal(resolveFutureSymbol('IBEX 35', '^IBEX'), '');
  assert.equal(resolveFutureSymbol('CBOE Volatility Index (VIX)', '^VIX'), '');
  assert.notEqual(resolveFutureSymbol('CAC 40', '^FCHI'), '^FCHI');
  assert.notEqual(resolveFutureSymbol('NIKKEI 225', '^N225'), '^N225');
});

test('si Yahoo falla, la resolución por nombre toma un símbolo de futuro y no el índice', () => {
  assert.equal(resolveFutureSymbol('DAX', '^GDAXI'), 'Q2JF.DE');
  assert.equal(resolveFutureSymbol('CAC 40', '^FCHI'), 'CAFME.PA');
  assert.equal(resolveFutureSymbol('EURO STOXX 50', '^STOXX50E'), '797B.Z');
});
