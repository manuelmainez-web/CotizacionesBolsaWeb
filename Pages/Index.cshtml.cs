using CotizacionesBolsaWeb.Models;
using CotizacionesBolsaWeb.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Text.Json;

namespace CotizacionesBolsaWeb.Pages;

[IgnoreAntiforgeryToken]
public class IndexModel : PageModel
{
    private const string TransactionStateKey = "custom-transaction-state";
    private const string ExcelTransactionsKey = "custom-excel-transactions";
    private const string HaciendaKey = "custom-hacienda";
    private const long MaxExcelUploadBytes = 5 * 1024 * 1024;
    private const string IndicesKey = "custom-indices";
    private const string StocksKey = "custom-stocks";
    private const string EtfsKey = "custom-etfs";
    private const string FundsKey = "custom-funds";
    private const string StockHoldingsKey = "custom-stock-holdings";
    private const string EtfControlKey = "custom-etf-control";
    private const string PensionPlansKey = "custom-pensionplans";
    private const string CheckingAccountsKey = "custom-checkingaccounts";
    private const string CashKey = "custom-cash";
    private const string FuturesKey = "custom-futures";
    private const string CommoditiesKey = "custom-commodities";

    private readonly YahooFinanceService _service = new();
    private readonly PensionPlanQuoteService _pensionPlanQuoteService = new();
    private readonly DataStore _dataStore;
    private readonly IConfiguration _configuration;

    public IndexModel(IWebHostEnvironment environment, IConfiguration configuration)
    {
        _dataStore = new DataStore(environment, configuration);
        _configuration = configuration;

        var publicUrl = configuration["Portfolio:PublicUrl"];
        if (string.IsNullOrWhiteSpace(publicUrl))
        {
            publicUrl = configuration["RENDER_EXTERNAL_URL"];
        }

        PublicUrl = publicUrl ?? string.Empty;
    }

    public string PublicUrl { get; private set; }

    private const string RendimientoRefKey = "custom-rendimiento-ref-ing";

    public string RendimientoReferenciaIng { get; private set; } = string.Empty;

    private sealed class RendimientoRefState
    {
        public string Valor { get; set; } = string.Empty;
    }

    public async Task<IActionResult> OnPostRendimientoRefAsync(string? valor)
    {
        var texto = (valor ?? string.Empty).Trim();
        if (!decimal.TryParse(texto, System.Globalization.NumberStyles.Number | System.Globalization.NumberStyles.AllowLeadingSign, System.Globalization.CultureInfo.InvariantCulture, out var importe))
        {
            texto = string.Empty;
        }
        else
        {
            texto = importe.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
        }

        var guardado = await _dataStore.SaveJsonAsync(RendimientoRefKey, new RendimientoRefState { Valor = texto });
        return new JsonResult(new { ok = guardado });
    }
    private Dictionary<string, List<BrokerTransactionRow>> _transactionState = new(StringComparer.OrdinalIgnoreCase);

    private static string TransactionKey(bool sale, string broker) =>
        $"cotizaciones.transactions.{(sale ? "sale" : "purchase")}.{broker}";

    private static string TransactionSignature(BrokerTransactionRow row) =>
        string.Join('|',
            row.Titulo.Trim().ToLowerInvariant(),
            row.TipoOperacion.Trim().ToUpperInvariant(),
            row.FechaOperacion.ToString("yyyy-MM-dd"),
            row.ImporteTotal.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture),
            row.NumeroTitulos.ToString("0.######", System.Globalization.CultureInfo.InvariantCulture));

    private async Task<Dictionary<string, List<BrokerTransactionRow>>> LoadTransactionStateAsync()
    {
        var raw = await _dataStore.LoadJsonAsync<Dictionary<string, List<BrokerTransactionRow>>>(TransactionStateKey)
            ?? new Dictionary<string, List<BrokerTransactionRow>>();

        var state = new Dictionary<string, List<BrokerTransactionRow>>(StringComparer.OrdinalIgnoreCase);
        var changed = false;

        foreach (var pair in raw)
        {
            var rows = new List<BrokerTransactionRow>();
            var seen = new HashSet<string>();

            foreach (var row in pair.Value ?? new List<BrokerTransactionRow>())
            {
                if (row == null)
                {
                    changed = true;
                    continue;
                }

                if (string.IsNullOrWhiteSpace(row.Titulo))
                {
                    row.Titulo = string.IsNullOrWhiteSpace(row.Concept) ? row.TipoOperacion : row.Concept;
                }

                if (string.IsNullOrWhiteSpace(row.TipoOperacion))
                {
                    row.TipoOperacion = pair.Key.Contains(".sale.", StringComparison.OrdinalIgnoreCase) ? "VENTA" : "COMPRA";
                }

                var signature = TransactionSignature(row);
                if (!seen.Add(signature))
                {
                    changed = true;
                    continue;
                }

                if (string.IsNullOrWhiteSpace(row.Id))
                {
                    var hash = System.Security.Cryptography.SHA1.HashData(System.Text.Encoding.UTF8.GetBytes(pair.Key + "|" + signature));
                    row.Id = Convert.ToHexString(hash)[..16].ToLowerInvariant();
                    changed = true;
                }

                rows.Add(row);
            }

            state[pair.Key] = rows;
        }

        if (changed)
        {
            await _dataStore.SaveJsonAsync(TransactionStateKey, state);
        }

        return state;
    }

    private static decimal ParseFormDecimal(string? value)
    {
        var text = (value ?? string.Empty).Replace("\u00A0", string.Empty).Replace("€", string.Empty).Replace(" ", string.Empty).Trim();
        if (text.Length == 0)
        {
            return 0m;
        }

        text = text.Contains(',') ? text.Replace(".", string.Empty).Replace(',', '.') : text;
        return decimal.TryParse(text, System.Globalization.NumberStyles.Number | System.Globalization.NumberStyles.AllowLeadingSign, System.Globalization.CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : 0m;
    }

    private static bool IsValidTransactionBroker(string? broker) =>
        string.Equals(broker, "ING", StringComparison.OrdinalIgnoreCase) || string.Equals(broker, "TR", StringComparison.OrdinalIgnoreCase);

    public async Task<IActionResult> OnPostSaveTransactionAsync(string type, string broker, string? id, string titulo, string numeroTitulos, string importeTotal, DateTime fechaOperacion)
    {
        if (!IsValidTransactionBroker(broker) || string.IsNullOrWhiteSpace(titulo))
        {
            return RedirectToPage();
        }

        var sale = string.Equals(type, "sale", StringComparison.OrdinalIgnoreCase);
        var brokerCode = broker.Trim().ToUpperInvariant();
        var key = TransactionKey(sale, brokerCode);
        var operation = sale ? "VENTA" : "COMPRA";
        var cleanTitle = titulo.Trim();
        var amount = ParseFormDecimal(importeTotal);

        var state = await LoadTransactionStateAsync();
        if (!state.TryGetValue(key, out var rows))
        {
            rows = new List<BrokerTransactionRow>();
            state[key] = rows;
        }

        var existing = string.IsNullOrWhiteSpace(id) ? null : rows.FirstOrDefault(r => r.Id == id);
        if (existing == null)
        {
            existing = new BrokerTransactionRow { Id = Guid.NewGuid().ToString("N") };
            rows.Add(existing);
        }

        existing.Titulo = cleanTitle;
        existing.TipoOperacion = operation;
        existing.NumeroTitulos = ParseFormDecimal(numeroTitulos);
        existing.ImporteTotal = amount;
        existing.FechaOperacion = fechaOperacion.Date;
        existing.Concept = $"{operation} - {cleanTitle}";
        existing.Valor = amount;

        await _dataStore.SaveJsonAsync(TransactionStateKey, state);
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostDeleteTransactionAsync(string type, string broker, string id)
    {
        if (!IsValidTransactionBroker(broker) || string.IsNullOrWhiteSpace(id))
        {
            return RedirectToPage();
        }

        var sale = string.Equals(type, "sale", StringComparison.OrdinalIgnoreCase);
        var key = TransactionKey(sale, broker.Trim().ToUpperInvariant());

        var state = await LoadTransactionStateAsync();
        if (state.TryGetValue(key, out var rows) && rows.RemoveAll(r => r.Id == id) > 0)
        {
            await _dataStore.SaveJsonAsync(TransactionStateKey, state);
        }

        return RedirectToPage();
    }

    [TempData]
    public string? ExcelTransactionsError { get; set; }

    public List<BrokerTransactionRow> ExcelTransactions { get; private set; } = new();

    public List<BrokerTransactionRow> ExcelPurchases => ExcelTransactions
        .Where(r => !IsSaleOperation(r.TipoOperacion))
        .OrderByDescending(r => r.FechaOperacion)
        .ToList();

    public List<BrokerTransactionRow> ExcelSales => ExcelTransactions
        .Where(r => IsSaleOperation(r.TipoOperacion))
        .OrderByDescending(r => r.FechaOperacion)
        .ToList();

    private static bool IsSaleOperation(string? tipoOperacion) =>
        !string.IsNullOrWhiteSpace(tipoOperacion) && tipoOperacion.Trim().StartsWith("VEN", StringComparison.OrdinalIgnoreCase);

    private static string NormalizeHeader(string? value)
    {
        var decomposed = (value ?? string.Empty).Normalize(System.Text.NormalizationForm.FormD);
        var sb = new System.Text.StringBuilder();
        foreach (var ch in decomposed)
        {
            if (System.Globalization.CharUnicodeInfo.GetUnicodeCategory(ch) == System.Globalization.UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            if (char.IsLetterOrDigit(ch))
            {
                sb.Append(char.ToLowerInvariant(ch));
            }
        }

        return sb.ToString();
    }

    public async Task<IActionResult> OnPostImportExcelTransactionsAsync(IFormFile? file)
    {
        if (file == null || file.Length == 0)
        {
            ExcelTransactionsError = "Selecciona un fichero Excel (.xlsx o .xls).";
            return RedirectToPage();
        }

        var extension = Path.GetExtension(file.FileName);
        if (!string.Equals(extension, ".xlsx", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(extension, ".xls", StringComparison.OrdinalIgnoreCase))
        {
            ExcelTransactionsError = "El fichero debe ser un Excel con formato .xlsx o .xls.";
            return RedirectToPage();
        }

        if (file.Length > MaxExcelUploadBytes)
        {
            ExcelTransactionsError = "El fichero Excel supera el tamaño máximo permitido (5 MB).";
            return RedirectToPage();
        }

        List<BrokerTransactionRow> rows;
        try
        {
            await using var stream = file.OpenReadStream();
            rows = ReadTransactionsFromExcel(stream);
        }
        catch
        {
            ExcelTransactionsError = "No se ha podido leer el fichero Excel. Comprueba que es un .xlsx o .xls válido.";
            return RedirectToPage();
        }

        if (rows.Count == 0)
        {
            ExcelTransactionsError = "El fichero Excel no contiene operaciones válidas. Debe tener las columnas VALOR, OPERACIÓN, TÍTULOS, IMPORTE TOTAL (€) y FECHA.";
            return RedirectToPage();
        }

        // Las operaciones ya grabadas se conservan: solo se añaden las filas nuevas del Excel
        // (se omiten las que ya existen con los mismos datos, para no duplicar al recargar el mismo fichero).
        var stored = await _dataStore.LoadEntriesAsync<BrokerTransactionRow>(ExcelTransactionsKey);
        var known = new Dictionary<string, BrokerTransactionRow>();
        foreach (var storedRow in stored)
        {
            known.TryAdd(TransactionSignature(storedRow), storedRow);
        }

        var newRows = new List<BrokerTransactionRow>();
        foreach (var row in rows)
        {
            if (known.TryGetValue(TransactionSignature(row), out var duplicate))
            {
                // Fila ya grabada: se conserva, pero se completa el nombre si aún no lo tenía.
                if (string.IsNullOrWhiteSpace(duplicate.Nombre))
                {
                    duplicate.Nombre = row.Nombre;
                }

                continue;
            }

            known[TransactionSignature(row)] = row;
            newRows.Add(row);
        }

        stored.AddRange(newRows);
        await ResolveTitleNamesAsync(stored);

        await _dataStore.SaveEntriesAsync(ExcelTransactionsKey, stored);
        return RedirectToPage();
    }

    private static bool IsIsinLike(string? value) =>
        !string.IsNullOrWhiteSpace(value) &&
        System.Text.RegularExpressions.Regex.IsMatch(value.Trim(), "^[A-Za-z]{2}[A-Za-z0-9]{9}[0-9]$");

    private static string? FindNameInPortfolio(string isin,
        IEnumerable<StockHolding> stockHoldings, IEnumerable<EtfHolding> etfHoldings,
        IEnumerable<FundHolding> fundHoldings, IEnumerable<Quote> etfControl)
    {
        bool Same(string? candidate) => string.Equals(candidate?.Trim(), isin.Trim(), StringComparison.OrdinalIgnoreCase);

        return etfHoldings.FirstOrDefault(h => Same(h.Isin))?.Name
            ?? stockHoldings.FirstOrDefault(h => Same(h.Isin))?.Name
            ?? fundHoldings.FirstOrDefault(h => Same(h.Isin))?.Name
            ?? etfControl.FirstOrDefault(q => Same(q.Isin))?.Name;
    }

    /// <summary>
    /// ISIN y nombre del título de una operación. Si el título es un ISIN, el nombre sale del guardado o de la
    /// cartera; si el título es un nombre, el ISIN se busca en la cartera por parecido de nombre.
    /// </summary>
    public (string Isin, string Nombre) GetTransactionTitleParts(BrokerTransactionRow row)
    {
        var titulo = (row.Titulo ?? string.Empty).Trim();

        if (IsIsinLike(titulo))
        {
            var name = !string.IsNullOrWhiteSpace(row.Nombre)
                ? row.Nombre
                : FindNameInPortfolio(titulo, StockHoldings, EtfHoldings, FundHoldings, EtfsControl) ?? string.Empty;
            return (titulo.ToUpperInvariant(), name);
        }

        return (FindIsinByName(titulo) ?? string.Empty, titulo);
    }

    private static readonly HashSet<string> NameStopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "etf", "ucits", "ac", "acc", "daily", "the", "de", "del", "la", "el", "fund", "ii"
    };

    private static HashSet<string> NameTokens(string? name) =>
        System.Text.RegularExpressions.Regex.Matches((name ?? string.Empty).ToLowerInvariant(), "[a-z0-9áéíóúñ]+")
            .Select(m => m.Value)
            .Where(t => !NameStopWords.Contains(t))
            .ToHashSet();

    private string? FindIsinByName(string titulo)
    {
        var wanted = NameTokens(titulo);
        if (wanted.Count == 0)
        {
            return null;
        }

        var candidates = EtfHoldings.Select(h => (Name: h.Name, Isin: h.Isin))
            .Concat(StockHoldings.Select(h => (Name: h.Name, Isin: h.Isin)))
            .Concat(FundHoldings.Select(h => (Name: h.Name, Isin: h.Isin)))
            .Concat(EtfsControl.Select(q => (Name: q.Name, Isin: q.Isin)))
            .Where(c => !string.IsNullOrWhiteSpace(c.Isin))
            .ToList();

        var exact = candidates.FirstOrDefault(c => string.Equals(c.Name?.Trim(), titulo, StringComparison.OrdinalIgnoreCase));
        if (exact.Isin != null)
        {
            return exact.Isin;
        }

        // Parecido por palabras: casi todas las palabras del título deben aparecer en el nombre de la cartera
        // (una palabra también coincide si una es prefijo de la otra: "leverage" / "leveraged").
        var scored = candidates
            .Select(c =>
            {
                var tokens = NameTokens(c.Name);
                var hits = wanted.Count(w => tokens.Any(t => t == w || (w.Length >= 5 && t.StartsWith(w)) || (t.Length >= 5 && w.StartsWith(t))));
                return (c.Isin, Score: (double)hits / wanted.Count);
            })
            .Where(s => s.Score >= 0.6)
            .OrderByDescending(s => s.Score)
            .ToList();

        if (scored.Count == 0 || (scored.Count > 1 && scored[0].Score - scored[1].Score < 0.01 && scored[0].Isin != scored[1].Isin))
        {
            return null;
        }

        return scored[0].Isin;
    }

    private async Task ResolveTitleNamesAsync(List<BrokerTransactionRow> rows)
    {
        var pending = rows.Where(r => string.IsNullOrWhiteSpace(r.Nombre) && IsIsinLike(r.Titulo)).ToList();
        if (pending.Count == 0)
        {
            return;
        }

        var stockHoldings = await _dataStore.LoadEntriesAsync<StockHolding>(StockHoldingsKey);
        var etfHoldings = await _dataStore.LoadEntriesAsync<EtfHolding>(EtfsKey);
        var fundHoldings = await _dataStore.LoadEntriesAsync<FundHolding>(FundsKey);
        var etfControl = (await _dataStore.LoadEntriesAsync<QuoteConfig>(EtfControlKey))
            .Select(c => new Quote { Name = c.Name, Isin = c.Isin ?? string.Empty })
            .ToList();

        var cache = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in pending)
        {
            var isin = row.Titulo.Trim().ToUpperInvariant();
            if (!cache.TryGetValue(isin, out var name))
            {
                name = FindNameInPortfolio(isin, stockHoldings, etfHoldings, fundHoldings, etfControl)
                    ?? await _service.LookupNameByIsinAsync(isin)
                    ?? string.Empty;
                cache[isin] = name;
            }

            row.Nombre = name;
        }
    }

    public async Task<IActionResult> OnPostClearExcelTransactionsAsync()
    {
        await _dataStore.SaveEntriesAsync(ExcelTransactionsKey, new List<BrokerTransactionRow>());
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostAddExcelTransactionAsync(string titulo, string tipoOperacion, string numeroTitulos, string importeTotal, DateTime fechaOperacion)
    {
        if (string.IsNullOrWhiteSpace(titulo))
        {
            return RedirectToPage();
        }

        var operation = IsSaleOperation(tipoOperacion) ? "VENTA" : "COMPRA";
        var cleanTitle = titulo.Trim();
        var amount = ParseFormDecimal(importeTotal);

        var rows = await _dataStore.LoadEntriesAsync<BrokerTransactionRow>(ExcelTransactionsKey);
        rows.Add(new BrokerTransactionRow
        {
            Id = Guid.NewGuid().ToString("N"),
            Titulo = cleanTitle,
            TipoOperacion = operation,
            NumeroTitulos = ParseFormDecimal(numeroTitulos),
            ImporteTotal = amount,
            FechaOperacion = fechaOperacion.Date,
            Concept = $"{operation} - {cleanTitle}",
            Valor = amount
        });

        await ResolveTitleNamesAsync(rows);
        await _dataStore.SaveEntriesAsync(ExcelTransactionsKey, rows);
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostEditExcelTransactionAsync(string id, string titulo, string tipoOperacion, string numeroTitulos, string importeTotal, DateTime fechaOperacion)
    {
        if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(titulo))
        {
            return RedirectToPage();
        }

        var rows = await _dataStore.LoadEntriesAsync<BrokerTransactionRow>(ExcelTransactionsKey);
        var existing = rows.FirstOrDefault(r => r.Id == id);
        if (existing == null)
        {
            return RedirectToPage();
        }

        var operation = IsSaleOperation(tipoOperacion) ? "VENTA" : "COMPRA";
        var amount = ParseFormDecimal(importeTotal);

        existing.Titulo = titulo.Trim();
        existing.Nombre = string.Empty;
        existing.TipoOperacion = operation;
        existing.NumeroTitulos = ParseFormDecimal(numeroTitulos);
        existing.ImporteTotal = amount;
        existing.FechaOperacion = fechaOperacion.Date;
        existing.Concept = $"{operation} - {existing.Titulo}";
        existing.Valor = amount;

        await ResolveTitleNamesAsync(rows);
        await _dataStore.SaveEntriesAsync(ExcelTransactionsKey, rows);
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostDeleteExcelTransactionAsync(string id)
    {
        if (!string.IsNullOrWhiteSpace(id))
        {
            var rows = await _dataStore.LoadEntriesAsync<BrokerTransactionRow>(ExcelTransactionsKey);
            if (rows.RemoveAll(r => r.Id == id) > 0)
            {
                await _dataStore.SaveEntriesAsync(ExcelTransactionsKey, rows);
            }
        }

        return RedirectToPage();
    }

    public sealed class HaciendaInputs
    {
        public decimal OtrosRendimientos { get; set; }
        public decimal Retenciones { get; set; }
    }

    public HaciendaInputs HaciendaState { get; private set; } = new();

    private TaxResult? _hacienda;

    public TaxResult Hacienda => _hacienda ??= BuildHacienda();

    private HashSet<string> FundIsins => FundHoldings
        .Select(f => f.Isin?.Trim().ToUpperInvariant() ?? string.Empty)
        .Where(i => i.Length > 0)
        .ToHashSet();

    private TaxResult BuildHacienda()
    {
        var funds = FundIsins;
        var transactions = ExcelTransactions.Select((row, index) =>
        {
            var parts = GetTransactionTitleParts(row);
            var key = string.IsNullOrWhiteSpace(parts.Isin) ? "t:" + parts.Nombre.Trim().ToLowerInvariant() : "i:" + parts.Isin;
            var label = string.IsNullOrWhiteSpace(parts.Nombre) ? parts.Isin : parts.Nombre;
            return new TaxTransaction(key, parts.Isin, label, IsSaleOperation(row.TipoOperacion), row.NumeroTitulos, Math.Abs(row.ImporteTotal), row.FechaOperacion.Date, index);
        }).ToList();

        // Recompra de valores homogéneos: 2 meses en valores cotizados (ETF, acciones) y 1 año en fondos no cotizados.
        return TaxCalculator.Compute(transactions, DateTime.Now.Year,
            isin => !string.IsNullOrWhiteSpace(isin) && funds.Contains(isin.Trim().ToUpperInvariant()) ? 12 : 2,
            HaciendaState.OtrosRendimientos, HaciendaState.Retenciones);
    }

    public async Task<IActionResult> OnPostSaveHaciendaAsync(string? otrosRendimientos, string? retenciones)
    {
        await _dataStore.SaveJsonAsync(HaciendaKey, new HaciendaInputs
        {
            OtrosRendimientos = Math.Max(0m, ParseFormDecimal(otrosRendimientos)),
            Retenciones = Math.Max(0m, ParseFormDecimal(retenciones))
        });
        return RedirectToPage();
    }

    public List<(string Level, string Text)> HaciendaNotices
    {
        get
        {
            var es = System.Globalization.CultureInfo.GetCultureInfo("es-ES");
            string Eur(decimal v) => v.ToString("#,##0.00 €", es);
            var h = Hacienda;
            var notices = new List<(string, string)>();

            if (ExcelTransactions.Count == 0)
            {
                notices.Add(("info", "No hay operaciones cargadas: carga el Excel en el panel de operaciones para calcular el impuesto."));
            }

            if (h.UnmatchedShares > 0)
            {
                notices.Add(("warn", $"Hay ventas de {h.UnmatchedShares.ToString("#,##0.######", es)} títulos sin compras anteriores cargadas: esa parte no se ha podido calcular. Carga o incluye las compras originales para obtener el coste real (FIFO)."));
            }

            if (h.DeferredLosses > 0)
            {
                notices.Add(("warn", $"{Eur(h.DeferredLosses)} de pérdidas NO computan este año por la norma antiaplicación (art. 33.5 f/g LIRPF): recompraste títulos del mismo valor en los 2 meses anteriores o posteriores a la venta con pérdida (1 año si es un fondo no cotizado). La pérdida se recupera cuando vendas los títulos recomprados; aquí se añade a su coste."));
            }

            foreach (var sale in h.OpenWindowSales)
            {
                var pending = -sale.Gain - sale.DeferredLoss;
                notices.Add(("warn", $"Venta de {sale.Label} del {sale.Date:dd/MM/yyyy} con pérdida: si recompras el mismo valor antes del {sale.OpenWindowUntil:dd/MM/yyyy}, se diferirán hasta {Eur(pending)} de esa pérdida."));
            }

            if (h.PriorLossesApplied > 0)
            {
                notices.Add(("info", $"Se han compensado {Eur(h.PriorLossesApplied)} de pérdidas netas de los 4 ejercicios anteriores (deben constar en tus declaraciones de esos años). Quedan sin aplicar {Eur(h.PriorLossesPending)}."));
            }
            else if (h.PriorLossesPending > 0)
            {
                notices.Add(("info", $"Tienes {Eur(h.PriorLossesPending)} de pérdidas netas de años anteriores (hasta 4 años) pendientes de compensar con ganancias futuras."));
            }

            if (h.CarryForwardLoss > 0)
            {
                notices.Add(("info", $"El saldo del ejercicio es negativo: {Eur(h.CarryForwardLoss)} se podrán compensar con ganancias patrimoniales de los 4 años siguientes. Además, hasta el 25 % de dividendos/intereses del año se puede compensar con pérdidas."));
            }

            if (TrCurrentValue > 50_000m)
            {
                notices.Add(("warn", $"Tu cartera en Trade Republic vale {Eur(TrCurrentValue)}: si los valores están custodiados fuera de España, supera los 50.000 € del Modelo 720 (declaración informativa de bienes en el extranjero, del 1 de enero al 31 de marzo)."));
            }
            else
            {
                notices.Add(("info", "Modelo 720: si los valores custodiados en el extranjero (p. ej. Trade Republic) superan 50.000 € a 31 de diciembre, hay que presentarlo (1 enero–31 marzo)."));
            }

            if (TotalGeneralConCuentasYEfectivo > 700_000m)
            {
                notices.Add(("warn", $"Tu patrimonio supera 700.000 €: puede estar sujeto al Impuesto sobre el Patrimonio (según tu comunidad autónoma; hay bonificaciones, p. ej. Madrid) y, por encima de 3 millones, al Impuesto de Solidaridad de las Grandes Fortunas."));
            }

            notices.Add(("info", "No están incluidos si no los indicas abajo: dividendos, intereses de cuentas (Trade Republic, ING), cupones y plan de pensiones. Tributan en la misma base del ahorro y a los mismos tipos."));
            notices.Add(("info", "Los ETF de acumulación no tributan hasta que se venden. Los traspasos entre fondos de inversión no tributan, pero los ETF no pueden traspasarse (vender y comprar sí tributa). El reembolso de fondos españoles lleva retención del 19 %: indícala en 'Retenciones'."));
            notices.Add(("info", "El importe total de cada operación se toma tal cual (si incluye comisiones, ya cuentan como mayor coste o menor transmisión). Solo se consideran las operaciones cargadas. Cálculo orientativo con la escala estatal + autonómica general del ahorro (19–30 %): no sustituye a tu declaración ni a un asesor fiscal."));

            return notices;
        }
    }

    private static List<BrokerTransactionRow> ReadTransactionsFromExcel(Stream input)
    {
        var spanish = System.Globalization.CultureInfo.GetCultureInfo("es-ES");
        var result = new List<BrokerTransactionRow>();

        // Los .xls (BIFF) necesitan las codificaciones antiguas (code pages).
        System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);

        // ExcelDataReader detecta solo el formato (.xls o .xlsx) y necesita un flujo con posición.
        using var buffer = new MemoryStream();
        input.CopyTo(buffer);
        buffer.Position = 0;

        // Cada hoja se carga como cuadrícula de celdas (fila, columna) con el valor tal cual (texto, número o fecha).
        var sheets = new List<List<object?[]>>();
        using (var reader = ExcelDataReader.ExcelReaderFactory.CreateReader(buffer))
        {
            do
            {
                var grid = new List<object?[]>();
                while (reader.Read())
                {
                    var cells = new object?[reader.FieldCount];
                    for (var i = 0; i < cells.Length; i++)
                    {
                        cells[i] = reader.GetValue(i);
                    }

                    grid.Add(cells);
                }

                sheets.Add(grid);
            }
            while (reader.NextResult());
        }

        static object? CellAt(List<object?[]> grid, int row, int col) =>
            row < grid.Count && col < grid[row].Length ? grid[row][col] : null;

        static string CellText(object? value) => value switch
        {
            null => string.Empty,
            DateTime d => d.ToString("dd/MM/yyyy", System.Globalization.CultureInfo.InvariantCulture),
            IFormattable f => f.ToString(null, System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty,
            _ => value.ToString() ?? string.Empty
        };

        // Correspondencia de columnas del Excel con las de las tablas de compras y ventas:
        //   VALOR            -> Título en cartera
        //   OPERACIÓN        -> Tipo de operación
        //   TÍTULOS          -> Número de títulos
        //   IMPORTE TOTAL (€)-> Importe total de la operación
        //   FECHA            -> Fecha de la operación
        // El resto de columnas del fichero se ignoran. La cabecera se localiza por nombre (en cualquier fila
        // de las primeras 30 y en cualquier hoja).
        List<object?[]>? sheet = null;
        var headerRow = -1;
        int colTitulo = -1, colTipo = -1, colNumero = -1, colImporte = -1, colFecha = -1;

        foreach (var candidate in sheets)
        {
            for (var r = 0; r < Math.Min(candidate.Count, 30) && sheet == null; r++)
            {
                int cTitulo = -1, cTipo = -1, cNumero = -1, cImporte = -1, cFecha = -1;
                for (var c = 0; c < candidate[r].Length; c++)
                {
                    switch (NormalizeHeader(CellText(candidate[r][c])))
                    {
                        case "valor": if (cTitulo < 0) cTitulo = c; break;
                        case "operacion": if (cTipo < 0) cTipo = c; break;
                        case "titulos": if (cNumero < 0) cNumero = c; break;
                        case "importetotal": if (cImporte < 0) cImporte = c; break;
                        case "fecha": if (cFecha < 0) cFecha = c; break;
                    }
                }

                if (cTitulo >= 0 && cTipo >= 0 && cNumero >= 0 && cImporte >= 0 && cFecha >= 0)
                {
                    sheet = candidate;
                    headerRow = r;
                    colTitulo = cTitulo; colTipo = cTipo; colNumero = cNumero; colImporte = cImporte; colFecha = cFecha;
                }
            }

            if (sheet != null)
            {
                break;
            }
        }

        if (sheet == null)
        {
            return result;
        }

        for (var r = headerRow + 1; r < sheet.Count; r++)
        {
            var titulo = CellText(CellAt(sheet, r, colTitulo)).Trim();
            if (string.IsNullOrWhiteSpace(titulo))
            {
                continue;
            }

            var tipoRaw = CellText(CellAt(sheet, r, colTipo)).Trim();
            var tipo = IsSaleOperation(tipoRaw) ? "VENTA" : "COMPRA";

            var numeroValue = CellAt(sheet, r, colNumero);
            var numero = numeroValue is double numeroDouble
                ? (decimal)numeroDouble
                : ParseFormDecimal(CellText(numeroValue));

            var importeValue = CellAt(sheet, r, colImporte);
            var importe = importeValue is double importeDouble
                ? (decimal)importeDouble
                : ParseFormDecimal(CellText(importeValue));

            var fechaValue = CellAt(sheet, r, colFecha);
            DateTime fecha;
            if (fechaValue is DateTime fechaDate)
            {
                fecha = fechaDate.Date;
            }
            else if (fechaValue is double fechaSerial && fechaSerial > 1 && fechaSerial < 2958466)
            {
                fecha = DateTime.FromOADate(fechaSerial).Date;
            }
            else if (!DateTime.TryParse(CellText(fechaValue), spanish, System.Globalization.DateTimeStyles.None, out fecha))
            {
                continue;
            }

            result.Add(new BrokerTransactionRow
            {
                Id = Guid.NewGuid().ToString("N"),
                Titulo = titulo,
                TipoOperacion = tipo,
                NumeroTitulos = numero,
                ImporteTotal = importe,
                FechaOperacion = fecha.Date,
                Concept = $"{tipo} - {titulo}",
                Valor = importe
            });
        }

        return result;
    }

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

    [TempData]
    public string? CheckingAccountError { get; set; }

    [TempData]
    public string? CashError { get; set; }

    [TempData]
    public string? CommodityError { get; set; }

    public List<Quote> Indices { get; private set; } = new();
    public List<Quote> Stocks { get; private set; } = new();
    public List<Quote> Etfs { get; private set; } = new();
    public List<Quote> Funds { get; private set; } = new();
    public List<Quote> PortfolioStocks { get; private set; } = new();
    public List<Quote> EtfsControl { get; private set; } = new();
    public List<Quote> Futures { get; private set; } = new();
    public List<Quote> Commodities { get; private set; } = new();
    public List<CommodityHolding> CommodityHoldings { get; private set; } = new();
    public List<EtfHolding> EtfHoldings { get; private set; } = new();
    public List<FundHolding> FundHoldings { get; private set; } = new();
    public List<StockHolding> StockHoldings { get; private set; } = new();
    public List<PensionPlanHolding> PensionPlans { get; private set; } = new();
    public List<CheckingAccountHolding> CheckingAccounts { get; private set; } = new();
    public List<CashHolding> CashHoldings { get; private set; } = new();

    public sealed class BrokerTransactionRow
    {
        public string Id { get; set; } = string.Empty;
        public string Titulo { get; set; } = string.Empty;
        public string Nombre { get; set; } = string.Empty;
        public string TipoOperacion { get; set; } = string.Empty;
        public decimal NumeroTitulos { get; set; }
        public decimal ImporteTotal { get; set; }
        public DateTime FechaOperacion { get; set; } = DateTime.Today;

        public string Concept { get; set; } = string.Empty;
        public decimal Valor { get; set; }
    }

    public sealed class BrokerTransactionGroup
    {
        public string Broker { get; set; } = string.Empty;
        public string BrokerName { get; set; } = string.Empty;
        public List<BrokerTransactionRow> Rows { get; set; } = new();
    }

    public List<BrokerTransactionGroup> PurchaseTransactionsByBroker => BuildBrokerTransactionGroupsFromState(false);
    public List<BrokerTransactionGroup> SaleTransactionsByBroker => BuildBrokerTransactionGroupsFromState(true);

    public List<string> AvailableTransactionTitles => BuildAvailableTransactionTitles();

    private List<string> BuildAvailableTransactionTitles()
    {
        var titles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var holding in StockHoldings)
        {
            if (!string.IsNullOrWhiteSpace(holding.Name))
            {
                titles.Add(holding.Name.Trim());
            }
        }

        foreach (var holding in EtfHoldings)
        {
            if (!string.IsNullOrWhiteSpace(holding.Name))
            {
                titles.Add(holding.Name.Trim());
            }
        }

        foreach (var holding in FundHoldings)
        {
            if (!string.IsNullOrWhiteSpace(holding.Name))
            {
                titles.Add(holding.Name.Trim());
            }
        }

        return titles
            .OrderBy(title => title, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private List<BrokerTransactionGroup> BuildBrokerTransactionGroupsFromState(bool salesOnly)
    {
        var brokerCodes = new[] { "ING", "TR" };
        var state = _transactionState;

        return brokerCodes
            .Select(brokerCode => new BrokerTransactionGroup
            {
                Broker = brokerCode,
                BrokerName = ResolveBrokerDisplayName(brokerCode),
                Rows = state
                    .Where(pair => string.Equals(pair.Key, $"cotizaciones.transactions.{(salesOnly ? "sale" : "purchase")}.{brokerCode}", StringComparison.OrdinalIgnoreCase))
                    .SelectMany(pair => pair.Value ?? new List<BrokerTransactionRow>())
                    .OrderByDescending(row => row.FechaOperacion)
                    .ToList()
            })
            .ToList();
    }

    private static string ResolveBrokerDisplayName(string? broker) => broker?.Trim().ToUpperInvariant() switch
    {
        "TR" => "Trade Republic",
        "OCCIDENT" => "Occident",
        _ => "ING Direct"
    };

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

    public decimal PensionPlansTotalValue => PensionPlans.Sum(p => p.Participaciones * p.ValorLiquidativo);
    public decimal PositionsCurrentValue => EtfsCurrentValue + FundsCurrentValue + PensionPlansTotalValue;
    public decimal CheckingAccountsTotalValue => CheckingAccounts.Sum(a => a.Balance);
    public decimal CashTotalValue => CashHoldings.Sum(c => c.Amount);
    public decimal TotalGeneralConCuentasYEfectivo => PositionsCurrentValue + CheckingAccountsTotalValue + CashTotalValue;

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

    private void ResolvePublicUrl()
    {
        if (!string.IsNullOrWhiteSpace(PublicUrl))
        {
            return;
        }

        var configuredUrl = _configuration["Portfolio:PublicUrl"];
        if (string.IsNullOrWhiteSpace(configuredUrl))
        {
            configuredUrl = _configuration["RENDER_EXTERNAL_URL"];
        }

        if (!string.IsNullOrWhiteSpace(configuredUrl))
        {
            PublicUrl = configuredUrl.Trim();
            return;
        }

        var request = HttpContext?.Request;
        if (request is not null)
        {
            var host = request.Host.HasValue ? request.Host.Value : "localhost";
            var scheme = request.Scheme;
            var pathBase = string.IsNullOrWhiteSpace(request.PathBase.Value) ? string.Empty : request.PathBase.Value;
            PublicUrl = $"{scheme}://{host}{pathBase}";
        }
    }

    public async Task OnGetAsync()
    {
        ResolvePublicUrl();
        _transactionState = await LoadTransactionStateAsync();
        ExcelTransactions = await _dataStore.LoadEntriesAsync<BrokerTransactionRow>(ExcelTransactionsKey);
        HaciendaState = await _dataStore.LoadJsonAsync<HaciendaInputs>(HaciendaKey) ?? new HaciendaInputs();        RendimientoReferenciaIng = (await _dataStore.LoadJsonAsync<RendimientoRefState>(RendimientoRefKey))?.Valor ?? string.Empty;

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
                new("Amundi Ibex 35 Doble Apalancado Diario (2x) (IBEXA)", "IBEXA.MC", "LU1681043941", "ES", 437m, 11194.98m / 437m, "ING"),
                new("db x-trackers LevDAX Daily UCITS 1C", "DBPE.DU", "LU0322252738", "DE", 119m, 18979.98m / 119m, "ING"),
                new("Amundi EURO STOXX 50 Daily (2x) Leveraged UCITS Ac", "LVE.PA", "FR0014005S97", "FR", 394m, 16745.71m / 394m, "ING"),
                new("Xtrackers IE Physical Gold ETC (XGDU)", "XGDU.MI", "IE00B4ND5C91", "IT", 166.200468m, 53.60m, "TR"),
                new("Amundi NASDAQ-100 II UCITS ETF", "UST.PA", "LU1829221024", "FR", 322m, 5101.62m / 322m, "ING"),
                new("db x-trackers S&P 500 2x Leveraged Daily UCITS 1C", "DBPG.DU", "LU0322252886", "DE", 16.319971m, 245.21m, "TR")
            });
        }

        EtfHoldings = await _dataStore.LoadEntriesAsync<EtfHolding>(EtfsKey);

        var ibexaHolding = EtfHoldings.FirstOrDefault(h => h.Symbol == "IBEXAE.XD");
        if (ibexaHolding != null)
        {
            var ibexaIndex = EtfHoldings.IndexOf(ibexaHolding);
            EtfHoldings[ibexaIndex] = ibexaHolding with { Symbol = "IBEXA.MC" };
            await _dataStore.SaveEntriesAsync(EtfsKey, EtfHoldings);
        }

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

        var missingIsinHoldings = StockHoldings.Where(h => string.IsNullOrWhiteSpace(h.Isin)).ToList();
        if (missingIsinHoldings.Count > 0)
        {
            var isinBackfilled = false;
            foreach (var holdingMissingIsin in missingIsinHoldings)
            {
                var lookedUpIsin = await _service.LookupIsinByNameAsync(holdingMissingIsin.Name);
                if (!string.IsNullOrWhiteSpace(lookedUpIsin))
                {
                    var idx = StockHoldings.IndexOf(holdingMissingIsin);
                    StockHoldings[idx] = holdingMissingIsin with { Isin = lookedUpIsin };
                    isinBackfilled = true;
                }
            }

            if (isinBackfilled)
            {
                await _dataStore.SaveEntriesAsync(StockHoldingsKey, StockHoldings);
            }
        }

        var stockHoldingConfigs = StockHoldings
            .Select(h => new QuoteConfig(h.Name, h.Symbol, h.Isin, h.CountryCode))
            .ToList();

        var etfControlConfigs = await _dataStore.LoadEntriesAsync<QuoteConfig>(EtfControlKey);

        Indices = (await _service.GetQuotesAsync(indexConfigs))
            .OrderBy(q => GetIndexRegionOrder(q.CountryCode))
            .ToList();
        Stocks = stockConfigs.Count > 0 ? await _service.GetQuotesAsync(stockConfigs) : new List<Quote>();
        Etfs = await _service.GetQuotesAsync(etfConfigs);
        Funds = fundConfigs.Count > 0 ? await _service.GetQuotesAsync(fundConfigs) : new List<Quote>();
        PortfolioStocks = stockHoldingConfigs.Count > 0 ? await _service.GetQuotesAsync(stockHoldingConfigs) : new List<Quote>();
        EtfsControl = etfControlConfigs.Count > 0 ? await _service.GetQuotesAsync(etfControlConfigs) : new List<Quote>();

        var futuresConfigs = new List<QuoteConfig>();
        foreach (var config in indexConfigs)
        {
            var futureSymbol = await _service.ResolveFutureSymbolByNameAsync(config.Name, config.Symbol);
            if (string.IsNullOrWhiteSpace(futureSymbol))
            {
                futureSymbol = config.Symbol;
            }

            futuresConfigs.Add(new QuoteConfig(
                config.Name,
                futureSymbol,
                config.Isin,
                config.CountryCode,
                config.Market));
        }

        await _dataStore.SaveEntriesAsync(FuturesKey, futuresConfigs);
        var futuresQuotes = futuresConfigs.Count > 0 ? await _service.GetQuotesAsync(futuresConfigs) : new List<Quote>();
        Futures = futuresQuotes
            .Where(quote => quote != null &&
                (quote.Price.HasValue || quote.PercentChange.HasValue || quote.Open.HasValue || quote.PreviousClose.HasValue || quote.LastUpdated.HasValue))
            .ToList();

        if (!await _dataStore.ExistsAsync(CommoditiesKey))
        {
            await _dataStore.SaveEntriesAsync(CommoditiesKey, new List<CommodityHolding>
            {
                new("Oro", "GC=F", "Metales preciosos"),
                new("Plata", "SI=F", "Metales preciosos"),
                new("Petróleo Brent", "BZ=F", "Energía"),
                new("Petróleo Crudo WTI", "CL=F", "Energía")
            });
        }

        CommodityHoldings = await _dataStore.LoadEntriesAsync<CommodityHolding>(CommoditiesKey);
        var commoditiesConfigs = CommodityHoldings
            .Select(h => new QuoteConfig(h.Name, h.Symbol, null, "US"))
            .ToList();
        Commodities = commoditiesConfigs.Count > 0 ? await _service.GetQuotesAsync(commoditiesConfigs) : new List<Quote>();

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
                new("PLAN 2030", 1972.843234m, 19.689456m, 7.18m, "ING", -0.72m, 0m)
            });
        }

        PensionPlans = await _dataStore.LoadEntriesAsync<PensionPlanHolding>(PensionPlansKey);

        var plan2030 = PensionPlans.FirstOrDefault(p => p.Name == "PLAN 2030");
        if (plan2030 != null && (plan2030.RentabilidadUltimoMes == 0m || plan2030.CapitalInvertido == 0m || string.IsNullOrEmpty(plan2030.CodigoDgsfp)))
        {
            var index = PensionPlans.IndexOf(plan2030);
            PensionPlans[index] = plan2030 with { RentabilidadUltimoMes = -0.72m, CapitalInvertido = 27484.40m, CodigoDgsfp = "N3901 / F1399" };
            await _dataStore.SaveEntriesAsync(PensionPlansKey, PensionPlans);
        }

        if (!PensionPlans.Any(p => p.Name == "Gco. Plan De Pensiones De Empleo"))
        {
            PensionPlans.Add(new PensionPlanHolding("Gco. Plan De Pensiones De Empleo", 1576.35894m, 1.00m, 0m, "OCCIDENT", 0m, 0m, "N2408 / N2408"));
            await _dataStore.SaveEntriesAsync(PensionPlansKey, PensionPlans);
        }

        var gcoPlan = PensionPlans.FirstOrDefault(p => p.Name == "Gco. Plan De Pensiones De Empleo");
        if (gcoPlan != null && string.IsNullOrEmpty(gcoPlan.CodigoDgsfp))
        {
            var index = PensionPlans.IndexOf(gcoPlan);
            PensionPlans[index] = gcoPlan with { CodigoDgsfp = "N2408 / N2408" };
            await _dataStore.SaveEntriesAsync(PensionPlansKey, PensionPlans);
        }

        // El valor liquidativo y las rentabilidades a 1 mes/12 meses se calculan
        // dinámicamente consultando la ficha pública del plan (por código DGSFP)
        // en cada carga de página; si la consulta falla o algún dato concreto no
        // se puede extraer (web caída, plan no indexado, etc.) se conserva el
        // último valor guardado para ese dato.
        for (var i = 0; i < PensionPlans.Count; i++)
        {
            var liveData = await _pensionPlanQuoteService.TryGetLiveDataAsync(PensionPlans[i].CodigoDgsfp);
            if (liveData == null)
            {
                continue;
            }

            PensionPlans[i] = PensionPlans[i] with
            {
                ValorLiquidativo = liveData.ValorLiquidativo ?? PensionPlans[i].ValorLiquidativo,
                RentabilidadUltimoMes = liveData.RentabilidadUltimoMes ?? PensionPlans[i].RentabilidadUltimoMes,
                Rentabilidad12Meses = liveData.Rentabilidad12Meses ?? PensionPlans[i].Rentabilidad12Meses
            };
        }

        if (!await _dataStore.ExistsAsync(CheckingAccountsKey))
        {
            await _dataStore.SaveEntriesAsync(CheckingAccountsKey, new List<CheckingAccountHolding>
            {
                new("ING Direct", 0m),
                new("Trade Republic", 0m)
            });
        }

        CheckingAccounts = await _dataStore.LoadEntriesAsync<CheckingAccountHolding>(CheckingAccountsKey);

        if (!await _dataStore.ExistsAsync(CashKey))
        {
            await _dataStore.SaveEntriesAsync(CashKey, new List<CashHolding>
            {
                new("Manuel Máinez Arrojo", 0m)
            });
        }

        CashHoldings = await _dataStore.LoadEntriesAsync<CashHolding>(CashKey);
    }

    private static string ResolveCheckingAccountName(string? entidad) => entidad?.Trim().ToUpperInvariant() switch
    {
        "TR" => "Trade Republic",
        _ => "ING Direct"
    };

    public async Task<IActionResult> OnPostAddCheckingAccountAsync(decimal importe, string entidad)
    {
        var name = ResolveCheckingAccountName(entidad);
        var accounts = await _dataStore.LoadEntriesAsync<CheckingAccountHolding>(CheckingAccountsKey);
        if (accounts.Any(a => a.Name == name))
        {
            CheckingAccountError = $"Ya existe una cuenta corriente para {name}.";
            return RedirectToPage();
        }

        accounts.Add(new CheckingAccountHolding(name, importe));
        await _dataStore.SaveEntriesAsync(CheckingAccountsKey, accounts);

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostEditCheckingAccountAsync(string name, decimal importe, string entidad)
    {
        var accounts = await _dataStore.LoadEntriesAsync<CheckingAccountHolding>(CheckingAccountsKey);
        var existing = accounts.FirstOrDefault(a => string.Equals(a.Name, name, StringComparison.OrdinalIgnoreCase));
        if (existing != null)
        {
            var index = accounts.IndexOf(existing);
            accounts[index] = existing with { Name = ResolveCheckingAccountName(entidad), Balance = importe };
            await _dataStore.SaveEntriesAsync(CheckingAccountsKey, accounts);
        }

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostDeleteCheckingAccountAsync(string name)
    {
        if (!string.IsNullOrWhiteSpace(name))
        {
            var accounts = await _dataStore.LoadEntriesAsync<CheckingAccountHolding>(CheckingAccountsKey);
            var toRemove = accounts.FirstOrDefault(a => string.Equals(a.Name, name, StringComparison.OrdinalIgnoreCase));
            if (toRemove != null)
            {
                accounts.Remove(toRemove);
                await _dataStore.SaveEntriesAsync(CheckingAccountsKey, accounts);
            }
        }

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostAddCashAsync(string name, decimal importe)
    {
        var holdings = await _dataStore.LoadEntriesAsync<CashHolding>(CashKey);
        holdings.Add(new CashHolding(string.IsNullOrWhiteSpace(name) ? "Manuel Máinez Arrojo" : name.Trim(), importe));
        await _dataStore.SaveEntriesAsync(CashKey, holdings);

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostEditCashAsync(int index, string name, decimal importe)
    {
        var holdings = await _dataStore.LoadEntriesAsync<CashHolding>(CashKey);
        if (index >= 0 && index < holdings.Count)
        {
            holdings[index] = holdings[index] with { Name = string.IsNullOrWhiteSpace(name) ? holdings[index].Name : name.Trim(), Amount = importe };
            await _dataStore.SaveEntriesAsync(CashKey, holdings);
        }

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostDeleteCashAsync(int index)
    {
        var holdings = await _dataStore.LoadEntriesAsync<CashHolding>(CashKey);
        if (index >= 0 && index < holdings.Count)
        {
            holdings.RemoveAt(index);
            await _dataStore.SaveEntriesAsync(CashKey, holdings);
        }

        return RedirectToPage();
    }

    public static readonly List<(string Symbol, string Name, string Classification)> AvailableCommodityOptions = new()
    {
        ("GC=F", "Oro", "Metales preciosos"),
        ("SI=F", "Plata", "Metales preciosos"),
        ("PL=F", "Platino", "Metales preciosos"),
        ("PA=F", "Paladio", "Metales preciosos"),
        ("HG=F", "Cobre", "Metales industriales"),
        ("BZ=F", "Petróleo Brent", "Energía"),
        ("CL=F", "Petróleo Crudo WTI", "Energía"),
        ("NG=F", "Gas Natural", "Energía"),
        ("ZC=F", "Maíz", "Agrícolas"),
        ("ZW=F", "Trigo", "Agrícolas"),
        ("ZS=F", "Soja", "Agrícolas"),
        ("KC=F", "Café", "Agrícolas"),
        ("CT=F", "Algodón", "Agrícolas"),
        ("SB=F", "Azúcar", "Agrícolas"),
        ("CC=F", "Cacao", "Agrícolas")
    };

    public static readonly string[] AvailableCommodityClassifications = new[]
    {
        "Metales preciosos",
        "Metales industriales",
        "Energía",
        "Agrícolas"
    };

    public static string ResolveFutureSymbol(string indexName, string? indexSymbol)
    {
        var normalizedName = (indexName ?? string.Empty).Trim();
        var normalizedSymbol = (indexSymbol ?? string.Empty).Trim();

        if (normalizedName.Contains("NASDAQ", StringComparison.OrdinalIgnoreCase) || normalizedSymbol == "^IXIC")
        {
            return "NQ=F";
        }

        if (normalizedName.Contains("SP 500", StringComparison.OrdinalIgnoreCase) || normalizedSymbol == "^GSPC")
        {
            return "ES=F";
        }

        if (normalizedName.Contains("DOW", StringComparison.OrdinalIgnoreCase) || normalizedSymbol == "^DJI")
        {
            return "YM=F";
        }

        if (normalizedName.Contains("DAX", StringComparison.OrdinalIgnoreCase) || normalizedSymbol == "^GDAXI")
        {
            return "Q2JF.DE";
        }

        if (normalizedName.Contains("CAC", StringComparison.OrdinalIgnoreCase) || normalizedSymbol == "^FCHI")
        {
            return "CAFME.PA";
        }

        if (normalizedName.Contains("EURO STOXX", StringComparison.OrdinalIgnoreCase) || normalizedSymbol == "^STOXX50E")
        {
            return "797B.Z";
        }

        if (normalizedName.Contains("NIKKEI", StringComparison.OrdinalIgnoreCase) || normalizedSymbol == "^N225")
        {
            return "^NKFT.OS";
        }

        if (normalizedName.Contains("RUSSELL", StringComparison.OrdinalIgnoreCase) || normalizedSymbol == "^RUT")
        {
            return "RTY=F";
        }

        if (normalizedSymbol.Contains("=F", StringComparison.OrdinalIgnoreCase))
        {
            return normalizedSymbol;
        }

        return string.Empty;
    }

    public async Task<IActionResult> OnPostAddCommodityAsync(string symbol, string classification)
    {
        var option = AvailableCommodityOptions.FirstOrDefault(o => o.Symbol == symbol);
        if (string.IsNullOrWhiteSpace(symbol) || option.Symbol is null)
        {
            CommodityError = "Selecciona una materia prima válida.";
            return RedirectToPage();
        }

        var holdings = await _dataStore.LoadEntriesAsync<CommodityHolding>(CommoditiesKey);
        if (holdings.Any(h => h.Symbol == symbol))
        {
            CommodityError = $"{option.Name} ya está añadida.";
            return RedirectToPage();
        }

        holdings.Add(new CommodityHolding(option.Name, symbol, string.IsNullOrWhiteSpace(classification) ? option.Classification : classification));
        await _dataStore.SaveEntriesAsync(CommoditiesKey, holdings);

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostDeleteCommodityAsync(string symbol)
    {
        if (!string.IsNullOrWhiteSpace(symbol))
        {
            var holdings = await _dataStore.LoadEntriesAsync<CommodityHolding>(CommoditiesKey);
            var toRemove = holdings.FirstOrDefault(h => string.Equals(h.Symbol, symbol, StringComparison.OrdinalIgnoreCase));
            if (toRemove != null)
            {
                holdings.Remove(toRemove);
                await _dataStore.SaveEntriesAsync(CommoditiesKey, holdings);
            }
        }

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostMoveCommodityUpAsync(string symbol)
    {
        await MoveCommodityEntryAsync(symbol, -1);
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostMoveCommodityDownAsync(string symbol)
    {
        await MoveCommodityEntryAsync(symbol, 1);
        return RedirectToPage();
    }

    private static string ResolvePensionBroker(string? broker) => broker?.Trim().ToUpperInvariant() switch
    {
        "TR" => "TR",
        "OCCIDENT" => "OCCIDENT",
        _ => "ING"
    };

    public async Task<IActionResult> OnPostAddPensionPlanAsync(string name, decimal participaciones, string broker, decimal capitalInvertido, string? codigoDgsfp, decimal valorLiquidativo = 0m, decimal rentabilidad12Meses = 0m, decimal rentabilidadUltimoMes = 0m)
    {
        if (string.IsNullOrWhiteSpace(name) || participaciones <= 0)
        {
            PensionPlanError = "Revisa los datos: el nombre y las participaciones son obligatorios.";
            return RedirectToPage();
        }

        var holdings = await _dataStore.LoadEntriesAsync<PensionPlanHolding>(PensionPlansKey);
        holdings.Add(new PensionPlanHolding(
            name.Trim(),
            participaciones,
            valorLiquidativo,
            rentabilidad12Meses,
            ResolvePensionBroker(broker),
            rentabilidadUltimoMes,
            capitalInvertido,
            codigoDgsfp?.Trim() ?? string.Empty));
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

    public async Task<IActionResult> OnPostEditPensionPlanAsync(string name, decimal participaciones, string broker, decimal capitalInvertido, string? codigoDgsfp)
    {
        var holdings = await _dataStore.LoadEntriesAsync<PensionPlanHolding>(PensionPlansKey);
        var existing = holdings.FirstOrDefault(h => string.Equals(h.Name, name, StringComparison.OrdinalIgnoreCase));
        if (existing != null)
        {
            var index = holdings.IndexOf(existing);
            holdings[index] = existing with
            {
                Participaciones = participaciones,
                Broker = ResolvePensionBroker(broker),
                CapitalInvertido = capitalInvertido,
                CodigoDgsfp = codigoDgsfp?.Trim() ?? string.Empty
            };
            await _dataStore.SaveEntriesAsync(PensionPlansKey, holdings);
        }

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostAddIndexAsync(string name, string symbol, string? countryCode)
    {
        await AddCustomEntryAsync(IndicesKey, name, symbol, countryCode);
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostAddEtfControlBySectorAsync(string name, string symbol, string? isin, string? countryCode)
    {
        await AddCustomEntryWithIsinAsync(EtfControlKey, name, symbol, isin, countryCode);
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
        "NIKKEI225" => new[] { "TYO", "JPX" },
        "HANGSENG" => new[] { "HKG" },
        _ => null
    };

    public async Task<IActionResult> OnPostAddEtfControlAsync(string isin)
    {
        // Búsqueda difusa habitual de Yahoo Finance; si no encuentra el ISIN (frecuente en ETF
        // apalancados/inversos o de nicho, ver notas de sesiones anteriores), se intenta un segundo
        // origen (justETF, que indexa prácticamente cualquier ETF UCITS por ISIN) antes de rendirse.
        var match = await _service.SearchSymbolAsync(isin) ?? await _service.SearchIsinViaJustEtfAsync(isin);
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

    public async Task<IActionResult> OnPostMoveEtfControlUpAsync(string symbol)
    {
        await MoveCustomEntryAsync(EtfControlKey, symbol, -1);
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostMoveEtfControlDownAsync(string symbol)
    {
        await MoveCustomEntryAsync(EtfControlKey, symbol, 1);
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

    public async Task<IActionResult> OnPostEditFundAsync(string symbol, decimal positionCount, decimal unitPurchasePrice, string broker)
    {
        var holdings = await _dataStore.LoadEntriesAsync<FundHolding>(FundsKey);
        var existing = holdings.FirstOrDefault(h => string.Equals(h.Symbol, symbol, StringComparison.OrdinalIgnoreCase));
        if (existing != null && positionCount > 0 && unitPurchasePrice > 0)
        {
            var index = holdings.IndexOf(existing);
            holdings[index] = existing with
            {
                PositionCount = positionCount,
                UnitPurchasePrice = unitPurchasePrice,
                Broker = string.Equals(broker, "TR", StringComparison.OrdinalIgnoreCase) ? "TR" : "ING"
            };
            await _dataStore.SaveEntriesAsync(FundsKey, holdings);
        }

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostDeleteIndexAsync(string symbol)
    {
        await RemoveCustomEntryAsync(IndicesKey, symbol);
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostMoveIndexUpAsync(string symbol)
    {
        await MoveCustomEntryAsync(IndicesKey, symbol, -1);
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostMoveIndexDownAsync(string symbol)
    {
        await MoveCustomEntryAsync(IndicesKey, symbol, 1);
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostDeleteStockAsync(string symbol)
    {
        await RemoveCustomEntryAsync(StocksKey, symbol);
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostMoveStockUpAsync(string symbol)
    {
        await MoveCustomEntryAsync(StocksKey, symbol, -1);
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostMoveStockDownAsync(string symbol)
    {
        await MoveCustomEntryAsync(StocksKey, symbol, 1);
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostAddEtfAsync(string name, string? symbol, string isin, string? countryCode, decimal positionCount, decimal unitPurchasePrice, string broker)
    {
        if (!string.IsNullOrWhiteSpace(name) && !string.IsNullOrWhiteSpace(isin) &&
            positionCount > 0 && unitPurchasePrice > 0)
        {
            if (string.IsNullOrWhiteSpace(symbol))
            {
                var trimmedIsin = isin.Trim();
                var match = await _service.SearchSymbolAsync(trimmedIsin) ?? await _service.SearchIsinViaJustEtfAsync(trimmedIsin);
                if (match.HasValue)
                {
                    symbol = match.Value.Symbol;
                    countryCode = string.IsNullOrWhiteSpace(countryCode) ? match.Value.CountryCode : countryCode;
                }
                else
                {
                    var isinQuote = await _service.GetQuoteFromJustEtfAsync(new QuoteConfig(name.Trim(), trimmedIsin.ToUpperInvariant(), trimmedIsin.ToUpperInvariant()));
                    if (isinQuote == null)
                    {
                        return RedirectToPage();
                    }

                    symbol = trimmedIsin.ToUpperInvariant();
                }
            }

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

    public async Task<IActionResult> OnPostEditEtfAsync(string symbol, decimal positionCount, decimal unitPurchasePrice, string broker)
    {
        var holdings = await _dataStore.LoadEntriesAsync<EtfHolding>(EtfsKey);
        var existing = holdings.FirstOrDefault(h => string.Equals(h.Symbol, symbol, StringComparison.OrdinalIgnoreCase));
        if (existing != null && positionCount > 0 && unitPurchasePrice > 0)
        {
            var index = holdings.IndexOf(existing);
            holdings[index] = existing with
            {
                PositionCount = positionCount,
                UnitPurchasePrice = unitPurchasePrice,
                Broker = string.Equals(broker, "TR", StringComparison.OrdinalIgnoreCase) ? "TR" : "ING"
            };
            await _dataStore.SaveEntriesAsync(EtfsKey, holdings);
        }

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostAddStockHoldingAsync(string name, decimal positionCount, decimal unitPurchasePrice, string? market = null, string? broker = null)
    {
        if (!string.IsNullOrWhiteSpace(name) && positionCount > 0 && unitPurchasePrice > 0)
        {
            var preferredExchanges = MapMarketToExchanges(market);
            var match = await _service.SearchSymbolAsync(name, preferredExchanges);
            if (match.HasValue)
            {
                var isin = await _service.LookupIsinByNameAsync(match.Value.Name);

                var holdings = await _dataStore.LoadEntriesAsync<StockHolding>(StockHoldingsKey);
                holdings.Add(new StockHolding(
                    match.Value.Name,
                    match.Value.Symbol,
                    isin ?? string.Empty,
                    match.Value.CountryCode,
                    positionCount,
                    unitPurchasePrice,
                    string.Equals(broker, "TR", StringComparison.OrdinalIgnoreCase) ? "TR" : "ING"));

                await _dataStore.SaveEntriesAsync(StockHoldingsKey, holdings);
            }
            else
            {
                StockHoldingError = $"No se ha encontrado ninguna acción llamada \"{name}\". Prueba con otro nombre.";
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

    public async Task<IActionResult> OnPostEditStockHoldingAsync(string symbol, decimal positionCount, decimal unitPurchasePrice, string broker, string? isin)
    {
        var holdings = await _dataStore.LoadEntriesAsync<StockHolding>(StockHoldingsKey);
        var existing = holdings.FirstOrDefault(h => string.Equals(h.Symbol, symbol, StringComparison.OrdinalIgnoreCase));
        if (existing != null && positionCount > 0 && unitPurchasePrice > 0)
        {
            var trimmedIsin = isin?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(trimmedIsin))
            {
                // Si se deja en blanco, se conserva el ISIN ya guardado (si lo había) para no
                // borrarlo accidentalmente; solo se intenta la búsqueda automática si aún no había ninguno.
                trimmedIsin = !string.IsNullOrWhiteSpace(existing.Isin)
                    ? existing.Isin
                    : await _service.LookupIsinByNameAsync(existing.Name) ?? string.Empty;
            }

            var index = holdings.IndexOf(existing);
            holdings[index] = existing with
            {
                PositionCount = positionCount,
                UnitPurchasePrice = unitPurchasePrice,
                Broker = string.Equals(broker, "TR", StringComparison.OrdinalIgnoreCase) ? "TR" : "ING",
                Isin = trimmedIsin
            };
            var saved = await _dataStore.SaveEntriesAsync(StockHoldingsKey, holdings);
            if (!saved)
            {
                StockHoldingError = "No se han podido guardar los cambios (fallo al escribir en el almacén de datos). Vuelve a intentarlo.";
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

        if (key == IndicesKey)
        {
            entries = entries.OrderBy(e => GetIndexRegionOrder(e.CountryCode)).ToList();
        }

        await _dataStore.SaveEntriesAsync(key, entries);
    }

    /// <summary>
    /// Igual que <see cref="AddCustomEntryAsync"/> pero permitiendo indicar el ISIN
    /// directamente (usado por el alta de ETF por sector, donde el ISIN, símbolo y
    /// país ya se conocen de antemano por catálogo y no hace falta buscarlos).
    /// </summary>
    private async Task AddCustomEntryWithIsinAsync(string key, string name, string symbol, string? isin, string? countryCode)
    {
        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(symbol))
        {
            return;
        }

        var entries = await _dataStore.LoadEntriesAsync<QuoteConfig>(key);
        entries.Add(new QuoteConfig(
            name.Trim(),
            symbol.Trim(),
            string.IsNullOrWhiteSpace(isin) ? null : isin.Trim().ToUpperInvariant(),
            string.IsNullOrWhiteSpace(countryCode) ? null : countryCode.Trim().ToUpperInvariant()));

        await _dataStore.SaveEntriesAsync(key, entries);
    }

    /// <summary>
    /// Agrupa los índices por región: Europa (0), América (1), Asia (2), resto (3).
    /// </summary>
    public int GetIndexRegionOrder(string? countryCode) => countryCode?.Trim().ToUpperInvariant() switch
    {
        "ES" or "DE" or "FR" or "EU" or "GB" or "IT" or "CH" or "LU" => 0,
        "US" => 1,
        "JP" or "HK" => 2,
        _ => 3
    };

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

    /// <summary>
    /// Intercambia de posición una entrada con la anterior (delta -1) o la
    /// siguiente (delta +1) en la lista persistida, para permitir reordenar
    /// filas manualmente con los botones de subir/bajar.
    /// </summary>
    private async Task MoveEntryAsync<T>(string key, string symbol, int delta, Func<T, string?> getSymbol)
    {
        if (string.IsNullOrWhiteSpace(symbol))
        {
            return;
        }

        var entries = await _dataStore.LoadEntriesAsync<T>(key);
        var index = entries.FindIndex(e => string.Equals(getSymbol(e), symbol, StringComparison.OrdinalIgnoreCase));
        var newIndex = index + delta;
        if (index < 0 || newIndex < 0 || newIndex >= entries.Count)
        {
            return;
        }

        (entries[index], entries[newIndex]) = (entries[newIndex], entries[index]);
        await _dataStore.SaveEntriesAsync(key, entries);
    }

    private Task MoveCustomEntryAsync(string key, string symbol, int delta) =>
        MoveEntryAsync<QuoteConfig>(key, symbol, delta, e => e.Symbol);

    private Task MoveCommodityEntryAsync(string symbol, int delta) =>
        MoveEntryAsync<CommodityHolding>(CommoditiesKey, symbol, delta, e => e.Symbol);
}
