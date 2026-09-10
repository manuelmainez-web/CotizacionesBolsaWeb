using CotizacionesBolsaWeb.Models;
using CotizacionesBolsaWeb.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Text.Json;

namespace CotizacionesBolsaWeb.Pages;

public class IndexModel : PageModel
{
    private const string IndicesKey = "custom-indices";
    private const string StocksKey = "custom-stocks";
    private const string EtfsKey = "custom-etfs";
    private const string FundsKey = "custom-funds";
    private const string StockHoldingsKey = "custom-stock-holdings";
    private const string EtfControlKey = "custom-etf-control";
    private const string PensionPlansKey = "custom-pensionplans";

    private readonly YahooFinanceService _service = new();
    private readonly DataStore _dataStore;

    public IndexModel(IWebHostEnvironment environment, IConfiguration configuration)
    {
        _dataStore = new DataStore(environment, configuration);
        PublicUrl = configuration["Portfolio:PublicUrl"] ?? string.Empty;
    }

    public string PublicUrl { get; }

    [TempData]
    public string? StockHoldingError { get; set; }

    [TempData]
    public string? EtfControlError { get; set; }

    [TempData]
    public string? StockError { get; set; }

    [TempData]
    public string? FundError { get; set; }

    [TempData]
    public string? PensionPlanError { get; set; }

    public List<Quote> Indices { get; private set; } = new();
    public List<Quote> Stocks { get; private set; } = new();
    public List<Quote> Etfs { get; private set; } = new();
    public List<Quote> Funds { get; private set; } = new();
    public List<Quote> PortfolioStocks { get; private set; } = new();
    public List<Quote> EtfsControl { get; private set; } = new();
    public List<Quote> Commodities { get; private set; } = new();
    public List<EtfHolding> EtfHoldings { get; private set; } = new();
    public List<FundHolding> FundHoldings { get; private set; } = new();
    public List<StockHolding> StockHoldings { get; private set; } = new();
    public List<PensionPlanHolding> PensionPlans { get; private set; } = new();

    public decimal EtfsPurchaseValue => EtfHoldings.Sum(h => h.PositionCount * h.UnitPurchasePrice);
    public decimal EtfsCurrentValue => EtfHoldings.Sum(h => (Etfs.FirstOrDefault(q => q.Symbol == h.Symbol)?.Price ?? 0m) * h.PositionCount);
    public decimal EtfsGainValue => EtfsCurrentValue - EtfsPurchaseValue;

    public decimal FundsPurchaseValue => FundHoldings.Sum(h => h.PositionCount * h.UnitPurchasePrice);
    public decimal FundsCurrentValue => FundHoldings.Sum(h => (Funds.FirstOrDefault(q => q.Symbol == h.Symbol)?.Price ?? 0m) * h.PositionCount);
    public decimal FundsGainValue => FundsCurrentValue - FundsPurchaseValue;

    public decimal StockHoldingsPurchaseValue => StockHoldings.Sum(h => h.PositionCount * h.UnitPurchasePrice);
    public decimal StockHoldingsCurrentValue => StockHoldings.Sum(h => (PortfolioStocks.FirstOrDefault(q => q.Symbol == h.Symbol)?.Price ?? 0m) * h.PositionCount);
    public decimal StockHoldingsGainValue => StockHoldingsCurrentValue - StockHoldingsPurchaseValue;

    public decimal TotalPurchaseValue => EtfsPurchaseValue + FundsPurchaseValue + StockHoldingsPurchaseValue;
    public decimal TotalGainValue => EtfsGainValue + FundsGainValue + StockHoldingsGainValue;
    public decimal TotalGainPercent => TotalPurchaseValue == 0 ? 0m : (TotalGainValue / TotalPurchaseValue) * 100m;

    private decimal PurchaseValueByBroker(string broker) =>
        EtfHoldings.Where(h => h.Broker == broker).Sum(h => h.PositionCount * h.UnitPurchasePrice) +
        FundHoldings.Where(h => h.Broker == broker).Sum(h => h.PositionCount * h.UnitPurchasePrice) +
        StockHoldings.Where(h => h.Broker == broker).Sum(h => h.PositionCount * h.UnitPurchasePrice);

    private decimal CurrentValueByBroker(string broker) =>
        EtfHoldings.Where(h => h.Broker == broker).Sum(h => (Etfs.FirstOrDefault(q => q.Symbol == h.Symbol)?.Price ?? 0m) * h.PositionCount) +
        FundHoldings.Where(h => h.Broker == broker).Sum(h => (Funds.FirstOrDefault(q => q.Symbol == h.Symbol)?.Price ?? 0m) * h.PositionCount) +
        StockHoldings.Where(h => h.Broker == broker).Sum(h => (PortfolioStocks.FirstOrDefault(q => q.Symbol == h.Symbol)?.Price ?? 0m) * h.PositionCount);

    public decimal IngPurchaseValue => PurchaseValueByBroker("ING");
    public decimal IngCurrentValue => CurrentValueByBroker("ING");
    public decimal IngGainValue => IngCurrentValue - IngPurchaseValue;
    public decimal IngGainPercent => IngPurchaseValue == 0 ? 0m : (IngGainValue / IngPurchaseValue) * 100m;

    public decimal TrPurchaseValue => PurchaseValueByBroker("TR");
    public decimal TrCurrentValue => CurrentValueByBroker("TR");
    public decimal TrGainValue => TrCurrentValue - TrPurchaseValue;
    public decimal TrGainPercent => TrPurchaseValue == 0 ? 0m : (TrGainValue / TrPurchaseValue) * 100m;

    public async Task OnGetAsync()
    {
        if (!await _dataStore.ExistsAsync(IndicesKey))
        {
            await _dataStore.SaveEntriesAsync(IndicesKey, new List<QuoteConfig>
            {
                new("IBEX 35", "^IBEX", null, "ES"),
                new("DAX", "^GDAXI", null, "DE"),
                new("CAC 40", "^FCHI", null, "FR"),
                new("EURO STOXX 50", "^STOXX50E", null, "EU"),
                new("DOW JONES", "^DJI", null, "US"),
                new("NASDAQ", "^IXIC", null, "US"),
                new("SP 500", "^GSPC", null, "US"),
                new("NIKKEI 225", "^N225", null, "JP")
            });
        }

        var indexConfigs = await _dataStore.LoadEntriesAsync<QuoteConfig>(IndicesKey);
        var stockConfigs = await _dataStore.LoadEntriesAsync<QuoteConfig>(StocksKey);

        if (!await _dataStore.ExistsAsync(EtfsKey))
        {
            await _dataStore.SaveEntriesAsync(EtfsKey, new List<EtfHolding>
            {
                new("Amundi Ibex 35 Doble Apalancado Diario (2x) (IBEXA)", "IBEXAE.XD", "LU1681043941", "ES", 437m, 11194.98m / 437m, "ING"),
                new("db x-trackers LevDAX Daily UCITS 1C", "DBPE.DU", "LU0322252738", "DE", 119m, 18979.98m / 119m, "ING"),
                new("Amundi EURO STOXX 50 Daily (2x) Leveraged UCITS Ac", "LVE.PA", "FR0014005S97", "FR", 394m, 16745.71m / 394m, "ING"),
                new("Xtrackers IE Physical Gold ETC (XGDU)", "XGDU.MI", "IE00B4ND5C91", "IT", 166.200468m, 53.60m, "TR"),
                new("Amundi NASDAQ-100 II UCITS ETF", "UST.PA", "LU1829221024", "FR", 322m, 5101.62m / 322m, "ING"),
                new("db x-trackers S&P 500 2x Leveraged Daily UCITS 1C", "DBPG.DU", "LU0322252886", "DE", 16.319971m, 245.21m, "TR")
            });
        }

        EtfHoldings = await _dataStore.LoadEntriesAsync<EtfHolding>(EtfsKey);
        var etfConfigs = EtfHoldings
            .Select(h => new QuoteConfig(h.Name, h.Symbol, h.Isin, h.CountryCode))
            .ToList();

        if (!await _dataStore.ExistsAsync(FundsKey))
        {
            await _dataStore.SaveEntriesAsync(FundsKey, new List<FundHolding>
            {
                new("BlackRock Global Funds - World Gold Fund E2 EUR ACC", "0P0000VHO3", "LU0171306680", "LU", 199.911759m, 67.60m, "TR")
            });
        }

        FundHoldings = await _dataStore.LoadEntriesAsync<FundHolding>(FundsKey);
        var fundConfigs = FundHoldings
            .Select(h => new QuoteConfig(h.Name, h.Symbol, h.Isin, h.CountryCode))
            .ToList();

        StockHoldings = await _dataStore.LoadEntriesAsync<StockHolding>(StockHoldingsKey);
        var stockHoldingConfigs = StockHoldings
            .Select(h => new QuoteConfig(h.Name, h.Symbol, h.Isin, h.CountryCode))
            .ToList();

        var etfControlConfigs = await _dataStore.LoadEntriesAsync<QuoteConfig>(EtfControlKey);

        Indices = await _service.GetQuotesAsync(indexConfigs);
        Stocks = stockConfigs.Count > 0 ? await _service.GetQuotesAsync(stockConfigs) : new List<Quote>();
        Etfs = await _service.GetQuotesAsync(etfConfigs);
        Funds = fundConfigs.Count > 0 ? await _service.GetQuotesAsync(fundConfigs) : new List<Quote>();
        PortfolioStocks = stockHoldingConfigs.Count > 0 ? await _service.GetQuotesAsync(stockHoldingConfigs) : new List<Quote>();
        EtfsControl = etfControlConfigs.Count > 0 ? await _service.GetQuotesAsync(etfControlConfigs) : new List<Quote>();

        var commoditiesConfigs = new List<QuoteConfig>
        {
            new("Oro", "GC=F", null, "US"),
            new("Plata", "SI=F", null, "US"),
            new("Petróleo Brent", "BZ=F", null, "US"),
            new("Petróleo Crudo WTI", "CL=F", null, "US")
        };
        Commodities = await _service.GetQuotesAsync(commoditiesConfigs);

        foreach (var quote in Indices)
        {
            quote.Isin = string.Empty;
        }

        foreach (var quote in Stocks)
        {
            quote.Isin = string.Empty;
            var config = stockConfigs.FirstOrDefault(x => x.Symbol == quote.Symbol);
            quote.Market = config?.Market ?? string.Empty;
        }

        foreach (var quote in Etfs)
        {
            var config = etfConfigs.FirstOrDefault(x => x.Symbol == quote.Symbol);
            quote.Isin = config?.Isin ?? quote.Isin;
        }

        foreach (var quote in Funds)
        {
            var config = fundConfigs.FirstOrDefault(x => x.Symbol == quote.Symbol);
            quote.Isin = config?.Isin ?? quote.Isin;
        }

        foreach (var quote in PortfolioStocks)
        {
            var config = stockHoldingConfigs.FirstOrDefault(x => x.Symbol == quote.Symbol);
            quote.Isin = config?.Isin ?? quote.Isin;
        }

        foreach (var quote in EtfsControl)
        {
            var config = etfControlConfigs.FirstOrDefault(x => x.Symbol == quote.Symbol);
            quote.Isin = config?.Isin ?? quote.Isin;
        }

        if (!await _dataStore.ExistsAsync(PensionPlansKey))
        {
            await _dataStore.SaveEntriesAsync(PensionPlansKey, new List<PensionPlanHolding>
            {
                new("PLAN 2030", 1972.843234m, 19.689456m, 7.18m, "ING")
            });
        }

        PensionPlans = await _dataStore.LoadEntriesAsync<PensionPlanHolding>(PensionPlansKey);
    }

    public async Task<IActionResult> OnPostAddPensionPlanAsync(string name, decimal participaciones, decimal valorLiquidativo, decimal rentabilidad12Meses, string broker)
    {
        if (string.IsNullOrWhiteSpace(name) || participaciones <= 0 || valorLiquidativo <= 0)
        {
            PensionPlanError = "Revisa los datos: el nombre, las participaciones y el valor liquidativo son obligatorios.";
            return RedirectToPage();
        }

        var holdings = await _dataStore.LoadEntriesAsync<PensionPlanHolding>(PensionPlansKey);
        holdings.Add(new PensionPlanHolding(
            name.Trim(),
            participaciones,
            valorLiquidativo,
            rentabilidad12Meses,
            string.Equals(broker, "TR", StringComparison.OrdinalIgnoreCase) ? "TR" : "ING"));
        await _dataStore.SaveEntriesAsync(PensionPlansKey, holdings);

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostDeletePensionPlanAsync(string name)
    {
        if (!string.IsNullOrWhiteSpace(name))
        {
            var holdings = await _dataStore.LoadEntriesAsync<PensionPlanHolding>(PensionPlansKey);
            var toRemove = holdings.FirstOrDefault(h => string.Equals(h.Name, name, StringComparison.OrdinalIgnoreCase));
            if (toRemove != null)
            {
                holdings.Remove(toRemove);
                await _dataStore.SaveEntriesAsync(PensionPlansKey, holdings);
            }
        }

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostAddIndexAsync(string name, string symbol, string? countryCode)
    {
        await AddCustomEntryAsync(IndicesKey, name, symbol, countryCode);
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostAddStockAsync(string name, string? market)
    {
        var preferredExchanges = MapMarketToExchanges(market);
        var match = await _service.SearchSymbolAsync(name, preferredExchanges);
        if (match.HasValue)
        {
            var entries = await _dataStore.LoadEntriesAsync<QuoteConfig>(StocksKey);
            entries.Add(new QuoteConfig(
                match.Value.Name,
                match.Value.Symbol,
                null,
                match.Value.CountryCode,
                match.Value.Market));
            await _dataStore.SaveEntriesAsync(StocksKey, entries);
        }
        else
        {
            StockError = $"No se ha encontrado ninguna acción llamada \"{name}\". Prueba con otro nombre o mercado.";
        }

        return RedirectToPage();
    }

    private static string[]? MapMarketToExchanges(string? market) => market switch
    {
        "IBEX35" => new[] { "MCE" },
        "CAC40" => new[] { "PAR" },
        "DAX" => new[] { "GER", "ETR", "FRA" },
        "EUROSTOXX50" => new[] { "PAR", "GER", "ETR", "FRA", "MCE", "AMS", "MIL", "BRU" },
        "DOWJONES" or "SP500" or "NASDAQ" => new[] { "NMS", "NGM", "NYQ", "ASE", "PCX", "BTS" },
        _ => null
    };

    public async Task<IActionResult> OnPostAddEtfControlAsync(string isin)
    {
        var match = await _service.SearchSymbolAsync(isin);
        if (match.HasValue)
        {
            var entries = await _dataStore.LoadEntriesAsync<QuoteConfig>(EtfControlKey);
            entries.Add(new QuoteConfig(match.Value.Name, match.Value.Symbol, isin.Trim().ToUpperInvariant(), match.Value.CountryCode));
            await _dataStore.SaveEntriesAsync(EtfControlKey, entries);
        }
        else
        {
            EtfControlError = $"No se ha encontrado ningún ETF con el ISIN \"{isin}\". Comprueba que sea correcto.";
        }

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostDeleteEtfControlAsync(string symbol)
    {
        await RemoveCustomEntryAsync(EtfControlKey, symbol);
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostAddFundAsync(string isin, decimal positionCount, decimal unitPurchasePrice)
    {
        if (string.IsNullOrWhiteSpace(isin) || positionCount <= 0 || unitPurchasePrice <= 0)
        {
            FundError = "Revisa los datos: el ISIN, el número de títulos y el precio de compra son obligatorios.";
            return RedirectToPage();
        }

        var match = await _service.SearchSymbolAsync(isin);
        if (match.HasValue)
        {
            var holdings = await _dataStore.LoadEntriesAsync<FundHolding>(FundsKey);
            holdings.Add(new FundHolding(
                match.Value.Name,
                match.Value.Symbol,
                isin.Trim().ToUpperInvariant(),
                match.Value.CountryCode,
                positionCount,
                unitPurchasePrice,
                string.Empty));

            await _dataStore.SaveEntriesAsync(FundsKey, holdings);
        }
        else
        {
            FundError = $"No se ha encontrado ningún fondo con el ISIN \"{isin}\". Comprueba que sea correcto.";
        }

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostDeleteFundAsync(string symbol)
    {
        if (!string.IsNullOrWhiteSpace(symbol))
        {
            var holdings = await _dataStore.LoadEntriesAsync<FundHolding>(FundsKey);
            var toRemove = holdings.FirstOrDefault(h => string.Equals(h.Symbol, symbol, StringComparison.OrdinalIgnoreCase));
            if (toRemove != null)
            {
                holdings.Remove(toRemove);
                await _dataStore.SaveEntriesAsync(FundsKey, holdings);
            }
        }

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostDeleteIndexAsync(string symbol)
    {
        await RemoveCustomEntryAsync(IndicesKey, symbol);
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostDeleteStockAsync(string symbol)
    {
        await RemoveCustomEntryAsync(StocksKey, symbol);
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostAddEtfAsync(string name, string symbol, string isin, string? countryCode, decimal positionCount, decimal unitPurchasePrice, string broker)
    {
        if (!string.IsNullOrWhiteSpace(name) && !string.IsNullOrWhiteSpace(symbol) &&
            !string.IsNullOrWhiteSpace(isin) && positionCount > 0 && unitPurchasePrice > 0)
        {
            var holdings = await _dataStore.LoadEntriesAsync<EtfHolding>(EtfsKey);
            holdings.Add(new EtfHolding(
                name.Trim(),
                symbol.Trim(),
                isin.Trim().ToUpperInvariant(),
                string.IsNullOrWhiteSpace(countryCode) ? null : countryCode.Trim().ToUpperInvariant(),
                positionCount,
                unitPurchasePrice,
                string.Equals(broker, "TR", StringComparison.OrdinalIgnoreCase) ? "TR" : "ING"));

            await _dataStore.SaveEntriesAsync(EtfsKey, holdings);
        }

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostDeleteEtfAsync(string symbol)
    {
        if (!string.IsNullOrWhiteSpace(symbol))
        {
            var holdings = await _dataStore.LoadEntriesAsync<EtfHolding>(EtfsKey);
            var toRemove = holdings.FirstOrDefault(h => string.Equals(h.Symbol, symbol, StringComparison.OrdinalIgnoreCase));
            if (toRemove != null)
            {
                holdings.Remove(toRemove);
                await _dataStore.SaveEntriesAsync(EtfsKey, holdings);
            }
        }

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostAddStockHoldingAsync(string name, string? market, decimal positionCount, decimal unitPurchasePrice, string broker)
    {
        if (!string.IsNullOrWhiteSpace(name) && positionCount > 0 && unitPurchasePrice > 0)
        {
            var preferredExchanges = MapMarketToExchanges(market);
            var match = await _service.SearchSymbolAsync(name, preferredExchanges);
            if (match.HasValue)
            {
                var holdings = await _dataStore.LoadEntriesAsync<StockHolding>(StockHoldingsKey);
                holdings.Add(new StockHolding(
                    match.Value.Name,
                    match.Value.Symbol,
                    string.Empty,
                    match.Value.CountryCode,
                    positionCount,
                    unitPurchasePrice,
                    string.Equals(broker, "TR", StringComparison.OrdinalIgnoreCase) ? "TR" : "ING"));

                await _dataStore.SaveEntriesAsync(StockHoldingsKey, holdings);
            }
            else
            {
                StockHoldingError = $"No se ha encontrado ninguna acción llamada \"{name}\". Prueba con otro nombre o mercado.";
            }
        }
        else
        {
            StockHoldingError = "Revisa los datos: el nombre, el número de títulos y el precio de compra son obligatorios.";
        }

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostDeleteStockHoldingAsync(string symbol)
    {
        if (!string.IsNullOrWhiteSpace(symbol))
        {
            var holdings = await _dataStore.LoadEntriesAsync<StockHolding>(StockHoldingsKey);
            var toRemove = holdings.FirstOrDefault(h => string.Equals(h.Symbol, symbol, StringComparison.OrdinalIgnoreCase));
            if (toRemove != null)
            {
                holdings.Remove(toRemove);
                await _dataStore.SaveEntriesAsync(StockHoldingsKey, holdings);
            }
        }

        return RedirectToPage();
    }

    private async Task AddCustomEntryAsync(string key, string name, string symbol, string? countryCode)
    {
        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(symbol))
        {
            return;
        }

        var entries = await _dataStore.LoadEntriesAsync<QuoteConfig>(key);
        entries.Add(new QuoteConfig(
            name.Trim(),
            symbol.Trim(),
            null,
            string.IsNullOrWhiteSpace(countryCode) ? null : countryCode.Trim().ToUpperInvariant()));

        await _dataStore.SaveEntriesAsync(key, entries);
    }

    private async Task RemoveCustomEntryAsync(string key, string symbol)
    {
        if (string.IsNullOrWhiteSpace(symbol))
        {
            return;
        }

        var entries = await _dataStore.LoadEntriesAsync<QuoteConfig>(key);
        var toRemove = entries.FirstOrDefault(e => string.Equals(e.Symbol, symbol, StringComparison.OrdinalIgnoreCase));
        if (toRemove != null)
        {
            entries.Remove(toRemove);
            await _dataStore.SaveEntriesAsync(key, entries);
        }
    }
}
