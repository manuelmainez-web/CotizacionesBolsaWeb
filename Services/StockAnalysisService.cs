using System.Text.RegularExpressions;

namespace CotizacionesBolsaWeb.Services;

public sealed class FundamentalData
{
    public string? MarketCap { get; set; }
    public string? PeRatio { get; set; }
    public string? ForwardPe { get; set; }
    public string? Eps { get; set; }
    public string? DividendInfo { get; set; }
    public string? ExDividendDate { get; set; }
    public string? Revenue { get; set; }
    public string? NetIncome { get; set; }
    public string? SharesOutstanding { get; set; }
    public string? Beta { get; set; }
    public string? PriceTarget { get; set; }
    public string? Sector { get; set; }
    public string? Employees { get; set; }
    public string? IpoDate { get; set; }
}

/// <summary>
/// Obtiene datos fundamentales reales (PER, capitalización, dividendo, sector, etc.) haciendo scraping
/// de la página pública de resumen de StockAnalysis.com (server-rendered, sin autenticación), ya que los
/// endpoints oficiales de fundamentales de Yahoo Finance (quoteSummary/v7 finance/quote) devuelven 401
/// sin un flujo de cookie+crumb que además Yahoo bloquea desde IPs de datacenter/cloud (ver notas del
/// proyecto). Solo aplica a acciones (EQUITY); para índices, ETF, materias primas o divisas normalmente
/// no hay página equivalente y se devuelve null (fallback a los datos básicos de Yahoo Finance).
/// </summary>
public sealed class StockAnalysisService
{
    private readonly HttpClient _httpClient;

    public StockAnalysisService(HttpClient? httpClient = null)
    {
        _httpClient = httpClient ?? new HttpClient();
        _httpClient.Timeout = TimeSpan.FromSeconds(15);
        if (!_httpClient.DefaultRequestHeaders.UserAgent.TryParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/126.0.0.0 Safari/537.36"))
        {
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/126.0.0.0 Safari/537.36");
        }
    }

    public async Task<FundamentalData?> GetFundamentalsAsync(string symbol, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(symbol))
        {
            return null;
        }

        // Los índices (^GSPC...), futuros/materias primas (GC=F...) y divisas (EURUSD=X...) no tienen
        // ficha fundamental: no merece la pena intentar la petición.
        if (symbol.StartsWith('^') || symbol.Contains('='))
        {
            return null;
        }

        // StockAnalysis.com usa el ticker "raíz" sin el sufijo de mercado que añade Yahoo Finance
        // (ej. "SAN.MC" -> "SAN"). Es una aproximación de mejor esfuerzo: funciona bien para tickers
        // de EE.UU. (sin sufijo) y para bastantes tickers europeos; si no encuentra la página, se
        // devuelve null y la vista usa solo los datos básicos de Yahoo Finance.
        var tickerBase = symbol.Split('.')[0];

        var data = await TryGetFromStocksPageAsync(tickerBase, cancellationToken);
        return data;
    }

    private async Task<FundamentalData?> TryGetFromStocksPageAsync(string ticker, CancellationToken cancellationToken)
    {
        var url = $"https://stockanalysis.com/stocks/{Uri.EscapeDataString(ticker)}/";

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            using var response = await _httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var html = await response.Content.ReadAsStringAsync(cancellationToken);

            var data = new FundamentalData
            {
                MarketCap = ExtractTableValue(html, "Market Cap"),
                Revenue = ExtractTableValue(html, "Revenue (ttm)") ?? ExtractTableValue(html, "Revenue"),
                NetIncome = ExtractTableValue(html, "Net Income"),
                Eps = ExtractTableValue(html, "EPS"),
                SharesOutstanding = ExtractTableValue(html, "Shares Out"),
                PeRatio = ExtractTableValue(html, "PE Ratio"),
                ForwardPe = ExtractTableValue(html, "Forward PE"),
                DividendInfo = ExtractTableValue(html, "Dividend"),
                ExDividendDate = ExtractTableValue(html, "Ex-Dividend Date"),
                Beta = ExtractTableValue(html, "Beta"),
                PriceTarget = ExtractTableValue(html, "Price Target"),
                Employees = ExtractTableValue(html, "Employees"),
                IpoDate = ExtractTableValue(html, "IPO Date"),
                Sector = ExtractLabeledLink(html, "Sector")
            };

            var tieneAlgunDato = new[] { data.MarketCap, data.Revenue, data.PeRatio, data.Eps, data.NetIncome }
                .Any(v => !string.IsNullOrWhiteSpace(v));

            return tieneAlgunDato ? data : null;
        }
        catch
        {
            return null;
        }
    }

    // Busca "ETIQUETA" (opcionalmente dentro de un <a>) y captura el texto de la siguiente celda <td>,
    // tolerando marcadores de hidratación (comentarios HTML) e iconos intermedios de la maquetación.
    private static string? ExtractTableValue(string html, string label)
    {
        var pattern = $@"{Regex.Escape(label)}(?:</a>)?[\s\S]{{0,300}}?<td[^>]*>\s*([^<]+)";
        var match = Regex.Match(html, pattern);
        if (!match.Success)
        {
            return null;
        }

        var valor = match.Groups[1].Value.Trim();
        return string.IsNullOrWhiteSpace(valor) ? null : valor;
    }

    // Busca "ETIQUETA</span> ... <a ...>VALOR</a>" (patrón usado en la ficha de la empresa: Sector, País...)
    private static string? ExtractLabeledLink(string html, string label)
    {
        var pattern = $@">{Regex.Escape(label)}</span>[\s\S]{{0,150}}?<a[^>]*>([^<]+)<";
        var match = Regex.Match(html, pattern);
        return match.Success ? match.Groups[1].Value.Trim() : null;
    }
}
