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
  if (normalizedName.includes('VIX') || normalizedSymbol === '^VIX') return 'VX=F';
  return '';
}

test('los futuros conocidos usan tickers de futuro reales y no los índices', () => {
  assert.equal(resolveFutureSymbol('NASDAQ', '^IXIC'), 'NQ=F');
  assert.equal(resolveFutureSymbol('SP 500', '^GSPC'), 'ES=F');
  assert.equal(resolveFutureSymbol('DOW JONES', '^DJI'), 'YM=F');
  assert.equal(resolveFutureSymbol('CBOE Volatility Index (VIX)', '^VIX'), 'VX=F');
});

test('el archivo de futuros no reutiliza símbolos de índices para los principales futuros', () => {
  const futureSymbols = new Map(futures.map((item) => [item.Name, item.Symbol]));

  assert.equal(futureSymbols.get('NASDAQ'), 'NQ=F');
  assert.equal(futureSymbols.get('SP 500'), 'ES=F');
  assert.equal(futureSymbols.get('DOW JONES'), 'YM=F');
  assert.equal(futureSymbols.get('CBOE Volatility Index (VIX)'), 'VX=F');

  assert.notEqual(futureSymbols.get('NASDAQ'), '^IXIC');
  assert.notEqual(futureSymbols.get('SP 500'), '^GSPC');
  assert.notEqual(futureSymbols.get('DOW JONES'), '^DJI');
  assert.notEqual(futureSymbols.get('CBOE Volatility Index (VIX)'), '^VIX');
});
