using CotizacionesBolsaWeb.Models;
using CotizacionesBolsaWeb.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Text.Json;

namespace CotizacionesBolsaWeb.Pages;

public class IndexModel : PageModel
{
    private readonly YahooFinanceService _service = new();
    private readonly string _customIndicesPath;
    private readonly string _customStocksPath;
    private readonly string _customEtfsPath;
    private readonly string _customFundsPath;
    private readonly string _customStockHoldingsPath;
    private readonly string _customEtfControlPath;

    public IndexModel(IWebHostEnvironment environment, IConfiguration configuration)
    {
        var dataFolder = Path.Combine(environment.ContentRootPath, "App_Data");
        Directory.CreateDirectory(dataFolder);
        _customIndicesPath = Path.Combine(dataFolder, "custom-indices.json");
        _customStocksPath = Path.Combine(dataFolder, "custom-stocks.json");
        _customEtfsPath = Path.Combine(dataFolder, "custom-etfs.json");
        _customFundsPath = Path.Combine(dataFolder, "custom-funds.json");
        _customStockHoldingsPath = Path.Combine(dataFolder, "custom-stock-holdings.json");
        _customEtfControlPath = Path.Combine(dataFolder, "custom-etf-control.json");
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

    public List<Quote> Indices { get; private set; } = new();
    public List<Quote> Stocks { get; private set; } = new();
    public List<Quote> Etfs { get; private set; } = new();
    public List<Quote> Funds { get; private set; } = new();
    public List<Quote> PortfolioStocks { get; private set; } = new();
    public List<Quote> EtfsControl { get; private set; } = new();
    public List<EtfHolding> EtfHoldings { get; private set; } = new();
    public List<FundHolding> FundHoldings { get; private set; } = new();
    public List<StockHolding> StockHoldings { get; private set; } = new();

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

    public async Task OnGetAsync()
    {
        if (!System.IO.File.Exists(_customIndicesPath))
        {
            SaveEntries(_customIndicesPath, new List<QuoteConfig>
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

        var indexConfigs = LoadEntries<QuoteConfig>(_customIndicesPath);
        var stockConfigs = LoadEntries<QuoteConfig>(_customStocksPath);

        if (!System.IO.File.Exists(_customEtfsPath))
        {
            SaveEntries(_customEtfsPath, new List<EtfHolding>
            {
                new("Amundi Ibex 35 Doble Apalancado Diario (2x) (IBEXA)", "IBEXAE.XD", "LU1681043941", "ES", 437m, 11194.98m / 437m, "ING"),
                new("db x-trackers LevDAX Daily UCITS 1C", "DBPE.DU", "LU0322252738", "DE", 119m, 18979.98m / 119m, "ING"),
                new("Amundi EURO STOXX 50 Daily (2x) Leveraged UCITS Ac", "LVE.PA", "FR0014005S97", "FR", 394m, 16745.71m / 394m, "ING"),
                new("Xtrackers IE Physical Gold ETC (XGDU)", "XGDU.MI", "IE00B4ND5C91", "IT", 166.200468m, 53.60m, "TR"),
                new("Amundi NASDAQ-100 II UCITS ETF", "UST.PA", "LU1829221024", "FR", 322m, 5101.62m / 322m, "ING"),
                new("db x-trackers S&P 500 2x Leveraged Daily UCITS 1C", "DBPG.DU", "LU0322252886", "DE", 16.319971m, 245.21m, "TR")
            });
        }

        EtfHoldings = LoadEntries<EtfHolding>(_customEtfsPath);
        var etfConfigs = EtfHoldings
            .Select(h => new QuoteConfig(h.Name, h.Symbol, h.Isin, h.CountryCode))
            .ToList();

        if (!System.IO.File.Exists(_customFundsPath))
        {
            SaveEntries(_customFundsPath, new List<FundHolding>
            {
                new("BlackRock Global Funds - World Gold Fund E2 EUR ACC", "0P0000VHO3", "LU0171306680", "LU", 199.911759m, 67.60m, "TR")
            });
        }

        FundHoldings = LoadEntries<FundHolding>(_customFundsPath);
        var fundConfigs = FundHoldings
            .Select(h => new QuoteConfig(h.Name, h.Symbol, h.Isin, h.CountryCode))
            .ToList();

        StockHoldings = LoadEntries<StockHolding>(_customStockHoldingsPath);
        var stockHoldingConfigs = StockHoldings
            .Select(h => new QuoteConfig(h.Name, h.Symbol, h.Isin, h.CountryCode))
            .ToList();

        var etfControlConfigs = LoadEntries<QuoteConfig>(_customEtfControlPath);

        Indices = await _service.GetQuotesAsync(indexConfigs);
        Stocks = stockConfigs.Count > 0 ? await _service.GetQuotesAsync(stockConfigs) : new List<Quote>();
        Etfs = await _service.GetQuotesAsync(etfConfigs);
        Funds = fundConfigs.Count > 0 ? await _service.GetQuotesAsync(fundConfigs) : new List<Quote>();
        PortfolioStocks = stockHoldingConfigs.Count > 0 ? await _service.GetQuotesAsync(stockHoldingConfigs) : new List<Quote>();
        EtfsControl = etfControlConfigs.Count > 0 ? await _service.GetQuotesAsync(etfControlConfigs) : new List<Quote>();

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
    }

    public IActionResult OnPostAddIndex(string name, string symbol, string? countryCode)
    {
        AddCustomEntry(_customIndicesPath, name, symbol, countryCode);
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostAddStockAsync(string name, string? market)
    {
        var preferredExchanges = MapMarketToExchanges(market);
        var match = await _service.SearchSymbolAsync(name, preferredExchanges);
        if (match.HasValue)
        {
            var entries = LoadEntries<QuoteConfig>(_customStocksPath);
            entries.Add(new QuoteConfig(
                match.Value.Name,
                match.Value.Symbol,
                null,
                match.Value.CountryCode,
                match.Value.Market));
            SaveEntries(_customStocksPath, entries);
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
            var entries = LoadEntries<QuoteConfig>(_customEtfControlPath);
            entries.Add(new QuoteConfig(match.Value.Name, match.Value.Symbol, isin.Trim().ToUpperInvariant(), match.Value.CountryCode));
            SaveEntries(_customEtfControlPath, entries);
        }
        else
        {
            EtfControlError = $"No se ha encontrado ning\u00fan ETF con el ISIN \"{isin}\". Comprueba que sea correcto.";
        }

        return RedirectToPage();
    }

    public IActionResult OnPostDeleteEtfControl(string symbol)
    {
        RemoveCustomEntry(_customEtfControlPath, symbol);
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
            var holdings = LoadEntries<FundHolding>(_customFundsPath);
            holdings.Add(new FundHolding(
                match.Value.Name,
                match.Value.Symbol,
                isin.Trim().ToUpperInvariant(),
                match.Value.CountryCode,
                positionCount,
                unitPurchasePrice,
                string.Empty));

            SaveEntries(_customFundsPath, holdings);
        }
        else
        {
            FundError = $"No se ha encontrado ningún fondo con el ISIN \"{isin}\". Comprueba que sea correcto.";
        }

        return RedirectToPage();
    }

    public IActionResult OnPostDeleteFund(string symbol)
    {
        if (!string.IsNullOrWhiteSpace(symbol))
        {
            var holdings = LoadEntries<FundHolding>(_customFundsPath);
            var toRemove = holdings.FirstOrDefault(h => string.Equals(h.Symbol, symbol, StringComparison.OrdinalIgnoreCase));
            if (toRemove != null)
            {
                holdings.Remove(toRemove);
                SaveEntries(_customFundsPath, holdings);
            }
        }

        return RedirectToPage();
    }

    public IActionResult OnPostDeleteIndex(string symbol)
    {
        RemoveCustomEntry(_customIndicesPath, symbol);
        return RedirectToPage();
    }

    public IActionResult OnPostDeleteStock(string symbol)
    {
        RemoveCustomEntry(_customStocksPath, symbol);
        return RedirectToPage();
    }

    public IActionResult OnPostAddEtf(string name, string symbol, string isin, string? countryCode, decimal positionCount, decimal unitPurchasePrice, string broker)
    {
        if (!string.IsNullOrWhiteSpace(name) && !string.IsNullOrWhiteSpace(symbol) &&
            !string.IsNullOrWhiteSpace(isin) && positionCount > 0 && unitPurchasePrice > 0)
        {
            var holdings = LoadEntries<EtfHolding>(_customEtfsPath);
            holdings.Add(new EtfHolding(
                name.Trim(),
                symbol.Trim(),
                isin.Trim().ToUpperInvariant(),
                string.IsNullOrWhiteSpace(countryCode) ? null : countryCode.Trim().ToUpperInvariant(),
                positionCount,
                unitPurchasePrice,
                string.Equals(broker, "TR", StringComparison.OrdinalIgnoreCase) ? "TR" : "ING"));

            SaveEntries(_customEtfsPath, holdings);
        }

        return RedirectToPage();
    }

    public IActionResult OnPostDeleteEtf(string symbol)
    {
        if (!string.IsNullOrWhiteSpace(symbol))
        {
            var holdings = LoadEntries<EtfHolding>(_customEtfsPath);
            var toRemove = holdings.FirstOrDefault(h => string.Equals(h.Symbol, symbol, StringComparison.OrdinalIgnoreCase));
            if (toRemove != null)
            {
                holdings.Remove(toRemove);
                SaveEntries(_customEtfsPath, holdings);
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
                var holdings = LoadEntries<StockHolding>(_customStockHoldingsPath);
                holdings.Add(new StockHolding(
                    match.Value.Name,
                    match.Value.Symbol,
                    string.Empty,
                    match.Value.CountryCode,
                    positionCount,
                    unitPurchasePrice,
                    string.Equals(broker, "TR", StringComparison.OrdinalIgnoreCase) ? "TR" : "ING"));

                SaveEntries(_customStockHoldingsPath, holdings);
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

    public IActionResult OnPostDeleteStockHolding(string symbol)
    {
        if (!string.IsNullOrWhiteSpace(symbol))
        {
            var holdings = LoadEntries<StockHolding>(_customStockHoldingsPath);
            var toRemove = holdings.FirstOrDefault(h => string.Equals(h.Symbol, symbol, StringComparison.OrdinalIgnoreCase));
            if (toRemove != null)
            {
                holdings.Remove(toRemove);
                SaveEntries(_customStockHoldingsPath, holdings);
            }
        }

        return RedirectToPage();
    }

    private static void AddCustomEntry(string path, string name, string symbol, string? countryCode)
    {
        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(symbol))
        {
            return;
        }

        var entries = LoadEntries<QuoteConfig>(path);
        entries.Add(new QuoteConfig(
            name.Trim(),
            symbol.Trim(),
            null,
            string.IsNullOrWhiteSpace(countryCode) ? null : countryCode.Trim().ToUpperInvariant()));

        SaveEntries(path, entries);
    }

    private static void RemoveCustomEntry(string path, string symbol)
    {
        if (string.IsNullOrWhiteSpace(symbol))
        {
            return;
        }

        var entries = LoadEntries<QuoteConfig>(path);
        var toRemove = entries.FirstOrDefault(e => string.Equals(e.Symbol, symbol, StringComparison.OrdinalIgnoreCase));
        if (toRemove != null)
        {
            entries.Remove(toRemove);
            SaveEntries(path, entries);
        }
    }

    private static List<T> LoadEntries<T>(string path)
    {
        if (!System.IO.File.Exists(path))
        {
            return new List<T>();
        }

        try
        {
            var json = System.IO.File.ReadAllText(path);
            return JsonSerializer.Deserialize<List<T>>(json) ?? new List<T>();
        }
        catch (JsonException)
        {
            return new List<T>();
        }
    }

    private static void SaveEntries<T>(string path, List<T> entries)
    {
        var json = JsonSerializer.Serialize(entries, new JsonSerializerOptions { WriteIndented = true });
        System.IO.File.WriteAllText(path, json);
    }
}
