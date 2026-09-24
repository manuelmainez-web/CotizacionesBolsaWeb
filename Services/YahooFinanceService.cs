using System.Globalization;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using CotizacionesBolsaWeb.Models;

namespace CotizacionesBolsaWeb.Services;

public sealed class YahooFinanceService
{
    private const string BgfWorldGoldFundIsin = "LU0171306680";
    private const string BgfWorldGoldFundOnvistaUrl = "https://www.onvista.de/fonds/BLACKROCK-GLOBAL-FUNDS-WORLD-GOLD-FUND-E2-EUR-ACC-Fonds-LU0171306680";

    private readonly HttpClient _httpClient;

    public YahooFinanceService(HttpClient? httpClient = null)
    {
        _httpClient = httpClient ?? new HttpClient();
        _httpClient.Timeout = TimeSpan.FromSeconds(20);
        if (!_httpClient.DefaultRequestHeaders.UserAgent.TryParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/126.0.0.0 Safari/537.36"))
        {
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/126.0.0.0 Safari/537.36");
        }

        _httpClient.DefaultRequestHeaders.Accept.Clear();
        _httpClient.DefaultRequestHeaders.Accept.ParseAdd("application/json");
    }

    public async Task<List<Quote>> GetQuotesAsync(IEnumerable<QuoteConfig> configs, CancellationToken cancellationToken = default)
    {
        var items = configs.ToList();
        if (items.Count == 0)
        {
            return new List<Quote>();
        }

        var results = new List<Quote>();
        foreach (var config in items)
        {
            Quote? quote;
            if (string.Equals(config.Isin, BgfWorldGoldFundIsin, StringComparison.OrdinalIgnoreCase))
            {
                quote = await GetQuoteFromOnvistaAsync(config, cancellationToken);
            }
            else
            {
                var resolvedSymbol = ResolveSymbol(config.Symbol);
                quote = await GetQuoteAsync(config, resolvedSymbol, cancellationToken);
            }

            results.Add(quote ?? CreateUnavailable(config));
        }

        return results;
    }

    public async Task<List<(DateTimeOffset Date, decimal Open, decimal High, decimal Low, decimal Close, long Volume)>> GetHistoryAsync(string symbol, string range, string interval, CancellationToken cancellationToken = default)
    {
        var resolvedSymbol = ResolveSymbol(symbol);
        var url = $"https://query1.finance.yahoo.com/v8/finance/chart/{Uri.EscapeDataString(resolvedSymbol)}?range={range}&interval={interval}";

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/126.0.0.0 Safari/537.36");
            request.Headers.Accept.ParseAdd("application/json");

            using var response = await _httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return new();
            }

            var payload = await response.Content.ReadAsStringAsync(cancellationToken);
            using var json = JsonDocument.Parse(payload);

            if (!json.RootElement.TryGetProperty("chart", out var chart) ||
                !chart.TryGetProperty("result", out var resultArray) ||
                resultArray.ValueKind != JsonValueKind.Array ||
                resultArray.GetArrayLength() == 0)
            {
                return new();
            }

            var first = resultArray[0];
            if (!first.TryGetProperty("timestamp", out var timestamps) || timestamps.ValueKind != JsonValueKind.Array)
            {
                return new();
            }

            if (!first.TryGetProperty("indicators", out var indicators) ||
                !indicators.TryGetProperty("quote", out var quoteArray) ||
                quoteArray.ValueKind != JsonValueKind.Array ||
                quoteArray.GetArrayLength() == 0)
            {
                return new();
            }

            var quote0 = quoteArray[0];
            if (!quote0.TryGetProperty("close", out var closes) || closes.ValueKind != JsonValueKind.Array)
            {
                return new();
            }

            quote0.TryGetProperty("open", out var opens);
            quote0.TryGetProperty("high", out var highs);
            quote0.TryGetProperty("low", out var lows);
            quote0.TryGetProperty("volume", out var volumes);

            var points = new List<(DateTimeOffset, decimal, decimal, decimal, decimal, long)>();
            var timestampArray = timestamps.EnumerateArray().ToList();
            var closeArray = closes.EnumerateArray().ToList();
            var openArray = opens.ValueKind == JsonValueKind.Array ? opens.EnumerateArray().ToList() : null;
            var highArray = highs.ValueKind == JsonValueKind.Array ? highs.EnumerateArray().ToList() : null;
            var lowArray = lows.ValueKind == JsonValueKind.Array ? lows.EnumerateArray().ToList() : null;
            var volumeArray = volumes.ValueKind == JsonValueKind.Array ? volumes.EnumerateArray().ToList() : null;

            for (var i = 0; i < timestampArray.Count && i < closeArray.Count; i++)
            {
                if (timestampArray[i].ValueKind != JsonValueKind.Number || closeArray[i].ValueKind != JsonValueKind.Number)
                {
                    continue;
                }

                var date = DateTimeOffset.FromUnixTimeSeconds(timestampArray[i].GetInt64());
                var close = Convert.ToDecimal(closeArray[i].GetDouble());
                var open = (openArray != null && i < openArray.Count && openArray[i].ValueKind == JsonValueKind.Number) ? Convert.ToDecimal(openArray[i].GetDouble()) : close;
                var high = (highArray != null && i < highArray.Count && highArray[i].ValueKind == JsonValueKind.Number) ? Convert.ToDecimal(highArray[i].GetDouble()) : close;
                var low = (lowArray != null && i < lowArray.Count && lowArray[i].ValueKind == JsonValueKind.Number) ? Convert.ToDecimal(lowArray[i].GetDouble()) : close;
                var volume = (volumeArray != null && i < volumeArray.Count && volumeArray[i].ValueKind == JsonValueKind.Number) ? volumeArray[i].GetInt64() : 0L;

                points.Add((date, open, high, low, close, volume));
            }

            return points;
        }
        catch
        {
            return new();
        }
    }

    public async Task<(string Symbol, string Name, string? CountryCode, string? Market)?> SearchSymbolAsync(string query, IReadOnlyCollection<string>? preferredExchanges = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return null;
        }

        var url = $"https://query1.finance.yahoo.com/v1/finance/search?q={Uri.EscapeDataString(query)}&quotesCount=8&newsCount=0";

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/126.0.0.0 Safari/537.36");
            request.Headers.Accept.ParseAdd("application/json");

            using var response = await _httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var payload = await response.Content.ReadAsStringAsync(cancellationToken);
            using var json = JsonDocument.Parse(payload);
            if (!json.RootElement.TryGetProperty("quotes", out var quotes) || quotes.ValueKind != JsonValueKind.Array)
            {
                return null;
            }

            (string Symbol, string Name, string? CountryCode, string? Market)? firstMatch = null;
            (string Symbol, string Name, string? CountryCode, string? Market)? preferredMatch = null;

            foreach (var quote in quotes.EnumerateArray())
            {
                if (!quote.TryGetProperty("symbol", out var symbolElement))
                {
                    continue;
                }

                var symbol = symbolElement.GetString();
                if (string.IsNullOrWhiteSpace(symbol))
                {
                    continue;
                }

                var name = (quote.TryGetProperty("longname", out var longNameElement) ? longNameElement.GetString() : null)
                    ?? (quote.TryGetProperty("shortname", out var shortNameElement) ? shortNameElement.GetString() : null)
                    ?? symbol;

                var exchange = quote.TryGetProperty("exchange", out var exchangeElement) ? exchangeElement.GetString() : null;
                var market = quote.TryGetProperty("exchDisp", out var exchDispElement) ? exchDispElement.GetString() : exchange;
                var countryCode = MapExchangeToCountry(exchange);
                var match = (symbol, name, countryCode, market);

                if (preferredExchanges != null && preferredExchanges.Count > 0 && exchange != null &&
                    preferredExchanges.Contains(exchange, StringComparer.OrdinalIgnoreCase))
                {
                    preferredMatch ??= match;
                    continue;
                }

                // Sin mercado preferido indicado, prioriza el mercado español (Bolsa de Madrid / IBEX-35) si aparece.
                if (preferredExchanges == null && (string.Equals(exchange, "MCE", StringComparison.OrdinalIgnoreCase) ||
                    symbol.EndsWith(".MC", StringComparison.OrdinalIgnoreCase)))
                {
                    return match;
                }

                firstMatch ??= match;
            }

            return preferredMatch ?? firstMatch;
        }
        catch
        {
            return null;
        }
    }

    private static readonly (string Suffix, string Country)[] YahooSuffixCountryMap =
    {
        (".MC", "ES"), (".PA", "FR"), (".DE", "DE"), (".F", "DE"), (".MI", "IT"),
        (".AS", "NL"), (".L", "GB"), (".SW", "CH"), (".HK", "HK"), (".T", "JP"),
        (".BR", "BE"), (".LS", "PT")
    };

    /// <summary>
    /// Búsqueda de respaldo cuando <see cref="SearchSymbolAsync"/> (buscador difuso de Yahoo Finance)
    /// no encuentra un ISIN: consulta la ficha del ETF en justETF (indexa prácticamente cualquier ETF
    /// UCITS por ISIN, a diferencia del buscador de Yahoo) y extrae los tickers "Reuters RIC" de su
    /// tabla de mercados cotizados, que casi siempre coinciden con el formato de símbolo de Yahoo
    /// Finance (TICKER.SUFIJO). Cada candidato se verifica de verdad contra la API de Yahoo Finance
    /// (chart) antes de aceptarlo, para no devolver nunca un símbolo que no cotice.
    /// </summary>
    public async Task<(string Symbol, string Name, string? CountryCode, string? Market)?> SearchIsinViaJustEtfAsync(string isin, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(isin))
        {
            return null;
        }

        var normalizedIsin = isin.Trim().ToUpperInvariant();
        var url = $"https://www.justetf.com/en/etf-profile.html?isin={Uri.EscapeDataString(normalizedIsin)}";

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/126.0.0.0 Safari/537.36");
            request.Headers.Accept.Clear();
            request.Headers.Accept.ParseAdd("text/html");

            using var response = await _httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var html = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!html.Contains(normalizedIsin, StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            var titleMatch = Regex.Match(html, @"<meta property=""og:title"" content=""([^""|]+)");
            var name = titleMatch.Success ? System.Net.WebUtility.HtmlDecode(titleMatch.Groups[1].Value.Trim()) : null;

            var listingMatches = Regex.Matches(html, @"data-testid=""etf-trade-data-panel_row-([a-z0-9]+)_reuters"">([A-Z0-9]+\.[A-Z]{1,4})<");
            if (listingMatches.Count == 0)
            {
                return null;
            }

            var candidates = listingMatches
                .Select(m => (ExchangeCode: m.Groups[1].Value, Symbol: m.Groups[2].Value))
                .Distinct()
                .OrderByDescending(c => c.Symbol.EndsWith(".MC", StringComparison.OrdinalIgnoreCase) || string.Equals(c.ExchangeCode, "xmad", StringComparison.OrdinalIgnoreCase))
                .ToList();

            foreach (var candidate in candidates)
            {
                var config = new QuoteConfig(name ?? normalizedIsin, candidate.Symbol, normalizedIsin);
                var quote = await GetQuoteAsync(config, candidate.Symbol, cancellationToken);
                if (quote?.Price.HasValue == true)
                {
                    var countryCode = YahooSuffixCountryMap
                        .FirstOrDefault(m => candidate.Symbol.EndsWith(m.Suffix, StringComparison.OrdinalIgnoreCase))
                        .Country;

                    return (candidate.Symbol, name ?? quote.Name, countryCode, null);
                }
            }

            return null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Busca el código ISIN de una empresa cotizada consultando Wikidata (propiedad P946 "ISIN"),
    /// ya que las APIs gratuitas de Yahoo Finance / OpenFIGI no exponen este dato.
    /// Devuelve null si no se encuentra ninguna coincidencia con ISIN registrado.
    /// </summary>
    public async Task<string?> LookupIsinByNameAsync(string companyName, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(companyName))
        {
            return null;
        }

        const string suffixPattern = @",?\s*(S\.?A\.?U?\.?|S\.?L\.?U?\.?|PLC|Inc\.?|Corp\.?|Ltd\.?|LLC|N\.?V\.?|AG|SE)\s*$";

        // Wikidata suele indexar las empresas sin la forma jurídica (", S.A.", ", Inc.", etc.),
        // así que se prueba primero con el nombre limpio y, si no hay resultado, con el original.
        var cleanedName = Regex.Replace(companyName, suffixPattern, string.Empty, RegexOptions.IgnoreCase).Trim();

        var candidates = new List<string> { cleanedName };

        if (!string.Equals(cleanedName, companyName, StringComparison.OrdinalIgnoreCase))
        {
            candidates.Add(companyName);
        }

        // Algunos nombres largos vienen con el formato "TICKER, Descripción completa, S.A." (p. ej.
        // "ACS, Actividades de Construcción y Servicios, S.A."), donde Wikidata solo indexa la parte
        // descriptiva ("Actividades de Construcción y Servicios" → Grupo ACS). Se prueba también esa parte.
        var commaIndex = companyName.IndexOf(',');
        if (commaIndex > 0 && commaIndex < companyName.Length - 1)
        {
            var afterComma = Regex.Replace(companyName[(commaIndex + 1)..].Trim(), suffixPattern, string.Empty, RegexOptions.IgnoreCase).Trim();
            if (afterComma.Length > 3 && !candidates.Contains(afterComma, StringComparer.OrdinalIgnoreCase))
            {
                candidates.Add(afterComma);
            }
        }

        foreach (var candidate in candidates)
        {
            var isin = await LookupIsinInternalAsync(candidate, cancellationToken);
            if (isin != null)
            {
                return isin;
            }
        }

        // Si Wikidata no encuentra nada buscando directamente por nombre (su buscador es poco tolerante
        // con nombres largos/con coma), se prueba a localizar el artículo de Wikipedia (cuyo buscador de
        // texto completo es mucho más permisivo) y de ahí se obtiene el elemento de Wikidata asociado.
        foreach (var wikiHost in new[] { "es.wikipedia.org", "en.wikipedia.org" })
        {
            foreach (var candidate in candidates)
            {
                var isin = await LookupIsinViaWikipediaAsync(candidate, wikiHost, cancellationToken);
                if (isin != null)
                {
                    return isin;
                }
            }
        }

        return null;
    }

    private async Task<string?> LookupIsinViaWikipediaAsync(string searchTerm, string wikiHost, CancellationToken cancellationToken)
    {
        try
        {
            var searchUrl = $"https://{wikiHost}/w/api.php?action=query&list=search&format=json&srlimit=1&srsearch={Uri.EscapeDataString(searchTerm)}";
            using var searchRequest = new HttpRequestMessage(HttpMethod.Get, searchUrl);
            searchRequest.Headers.UserAgent.ParseAdd("CotizacionesBolsaWeb/1.0 (contacto: app privada)");
            using var searchResponse = await _httpClient.SendAsync(searchRequest, cancellationToken);
            if (!searchResponse.IsSuccessStatusCode)
            {
                return null;
            }

            var searchPayload = await searchResponse.Content.ReadAsStringAsync(cancellationToken);
            using var searchJson = JsonDocument.Parse(searchPayload);
            if (!searchJson.RootElement.TryGetProperty("query", out var queryElement) ||
                !queryElement.TryGetProperty("search", out var searchResults) ||
                searchResults.ValueKind != JsonValueKind.Array ||
                searchResults.GetArrayLength() == 0)
            {
                return null;
            }

            var title = searchResults[0].GetProperty("title").GetString();
            if (string.IsNullOrWhiteSpace(title))
            {
                return null;
            }

            var pagePropsUrl = $"https://{wikiHost}/w/api.php?action=query&prop=pageprops&format=json&titles={Uri.EscapeDataString(title)}";
            using var pagePropsRequest = new HttpRequestMessage(HttpMethod.Get, pagePropsUrl);
            pagePropsRequest.Headers.UserAgent.ParseAdd("CotizacionesBolsaWeb/1.0 (contacto: app privada)");
            using var pagePropsResponse = await _httpClient.SendAsync(pagePropsRequest, cancellationToken);
            if (!pagePropsResponse.IsSuccessStatusCode)
            {
                return null;
            }

            var pagePropsPayload = await pagePropsResponse.Content.ReadAsStringAsync(cancellationToken);
            using var pagePropsJson = JsonDocument.Parse(pagePropsPayload);
            if (!pagePropsJson.RootElement.TryGetProperty("query", out var pagesQuery) ||
                !pagesQuery.TryGetProperty("pages", out var pages))
            {
                return null;
            }

            string? entityId = null;
            foreach (var page in pages.EnumerateObject())
            {
                if (page.Value.TryGetProperty("pageprops", out var pageProps) &&
                    pageProps.TryGetProperty("wikibase_item", out var wikibaseItemElement))
                {
                    entityId = wikibaseItemElement.GetString();
                    break;
                }
            }

            if (string.IsNullOrWhiteSpace(entityId))
            {
                return null;
            }

            return await GetIsinFromWikidataEntityAsync(entityId, cancellationToken);
        }
        catch
        {
            return null;
        }
    }

    private async Task<string?> LookupIsinInternalAsync(string companyName, CancellationToken cancellationToken)
    {
        try
        {
            var searchUrl = $"https://www.wikidata.org/w/api.php?action=wbsearchentities&search={Uri.EscapeDataString(companyName)}&language=es&format=json&type=item&limit=1";
            using var searchRequest = new HttpRequestMessage(HttpMethod.Get, searchUrl);
            searchRequest.Headers.UserAgent.ParseAdd("CotizacionesBolsaWeb/1.0 (contacto: app privada)");
            using var searchResponse = await _httpClient.SendAsync(searchRequest, cancellationToken);
            if (!searchResponse.IsSuccessStatusCode)
            {
                return null;
            }

            var searchPayload = await searchResponse.Content.ReadAsStringAsync(cancellationToken);
            using var searchJson = JsonDocument.Parse(searchPayload);
            if (!searchJson.RootElement.TryGetProperty("search", out var searchResults) ||
                searchResults.ValueKind != JsonValueKind.Array ||
                searchResults.GetArrayLength() == 0)
            {
                return null;
            }

            var entityId = searchResults[0].GetProperty("id").GetString();
            if (string.IsNullOrWhiteSpace(entityId))
            {
                return null;
            }

            return await GetIsinFromWikidataEntityAsync(entityId, cancellationToken);
        }
        catch
        {
            return null;
        }
    }

    private async Task<string?> GetIsinFromWikidataEntityAsync(string entityId, CancellationToken cancellationToken)
    {
        try
        {
            var claimsUrl = $"https://www.wikidata.org/w/api.php?action=wbgetclaims&entity={Uri.EscapeDataString(entityId)}&property=P946&format=json";
            using var claimsRequest = new HttpRequestMessage(HttpMethod.Get, claimsUrl);
            claimsRequest.Headers.UserAgent.ParseAdd("CotizacionesBolsaWeb/1.0 (contacto: app privada)");
            using var claimsResponse = await _httpClient.SendAsync(claimsRequest, cancellationToken);
            if (!claimsResponse.IsSuccessStatusCode)
            {
                return null;
            }

            var claimsPayload = await claimsResponse.Content.ReadAsStringAsync(cancellationToken);
            using var claimsJson = JsonDocument.Parse(claimsPayload);
            if (!claimsJson.RootElement.TryGetProperty("claims", out var claims) ||
                !claims.TryGetProperty("P946", out var isinClaims) ||
                isinClaims.ValueKind != JsonValueKind.Array ||
                isinClaims.GetArrayLength() == 0)
            {
                return null;
            }

            var isin = isinClaims[0]
                .GetProperty("mainsnak")
                .GetProperty("datavalue")
                .GetProperty("value")
                .GetString();

            return string.IsNullOrWhiteSpace(isin) ? null : isin;
        }
        catch
        {
            return null;
        }
    }

    private static string? MapExchangeToCountry(string? exchange) => exchange switch
    {
        "NMS" or "NYQ" or "NGM" or "PCX" or "ASE" or "BTS" => "US",
        "PAR" => "FR",
        "GER" or "FRA" or "ETR" => "DE",
        "MCE" => "ES",
        "MIL" => "IT",
        "LSE" => "GB",
        "AMS" => "NL",
        "TOR" => "CA",
        "TYO" or "JPX" => "JP",
        "SWX" or "EBS" => "CH",
        "HKG" => "HK",
        _ => null
    };

    private async Task<Quote?> GetQuoteAsync(QuoteConfig config, string symbol, CancellationToken cancellationToken)
    {
        var url = $"https://query1.finance.yahoo.com/v8/finance/chart/{Uri.EscapeDataString(symbol)}?range=1d&interval=1d";

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/126.0.0.0 Safari/537.36");
            request.Headers.Accept.ParseAdd("application/json");

            using var response = await _httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var payload = await response.Content.ReadAsStringAsync(cancellationToken);
            using var json = JsonDocument.Parse(payload);
            if (!json.RootElement.TryGetProperty("chart", out var chart) ||
                !chart.TryGetProperty("result", out var resultArray) ||
                resultArray.ValueKind != JsonValueKind.Array ||
                resultArray.GetArrayLength() == 0)
            {
                return null;
            }

            var first = resultArray[0];
            if (!first.TryGetProperty("meta", out var meta))
            {
                return null;
            }

            var price = GetDecimal(meta, "regularMarketPrice");
            var changePercent = GetDecimal(meta, "regularMarketChangePercent");
            var updated = GetDate(meta, "regularMarketTime");
            var previousClose = GetDecimal(meta, "chartPreviousClose");
            var open = GetFirstArrayValue(first, "open");
            var high = GetFirstArrayValue(first, "high");
            var low = GetFirstArrayValue(first, "low");
            var volume = GetLong(meta, "regularMarketVolume");

            return new Quote
            {
                Name = config.Name,
                Symbol = config.Symbol,
                Isin = config.Isin ?? string.Empty,
                CountryCode = config.CountryCode ?? string.Empty,
                Price = price,
                PercentChange = changePercent,
                Open = open,
                PreviousClose = previousClose,
                High = high,
                Low = low,
                Volume = volume,
                LastUpdated = updated
            };
        }
        catch
        {
            return null;
        }
    }

    private static string ResolveSymbol(string symbol)
    {
        return symbol switch
        {
            "IBEXA.PA" => "IBEXA.MC",
            "IBEXAE.XD" => "IBEXA.MC",
            "DBPE.PA" => "DBPE.DU",
            "LVE.PA" => "LVE.PA",
            "XGDU.L" => "XGDU.MI",
            "USTE.PA" => "USTEC.SW",
            "UST.PA" => "UST.PA",
            "NADQ.SW" => "NADQ-USD.SW",
            "NADQ-USD.SW" => "NADQ-USD.SW",
            "DBPG.PA" => "DBPG.DU",
            _ => symbol
        };
    }

    private async Task<Quote?> GetQuoteFromOnvistaAsync(QuoteConfig config, CancellationToken cancellationToken)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, BgfWorldGoldFundOnvistaUrl);
            request.Headers.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/126.0.0.0 Safari/537.36");
            request.Headers.AcceptLanguage.ParseAdd("de-DE,de;q=0.9");
            request.Headers.Accept.Clear();
            request.Headers.Accept.ParseAdd("text/html");

            using var response = await _httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var html = await response.Content.ReadAsStringAsync(cancellationToken);

            var priceMatch = Regex.Match(html, @"class=""text-neutral-8[^""]*font-bold"" value=""([\d.]+)""");
            var percentMatch = Regex.Match(html, @"class=""text-(?:positive|negative) whitespace-nowrap ml-4"" value=""(-?[\d.]+)""");
            var timeMatch = Regex.Match(html, @"<time dateTime=""([^""]+)""");

            if (!priceMatch.Success)
            {
                return null;
            }

            var price = decimal.Parse(priceMatch.Groups[1].Value, CultureInfo.InvariantCulture);
            decimal? percent = percentMatch.Success
                ? decimal.Parse(percentMatch.Groups[1].Value, CultureInfo.InvariantCulture)
                : null;
            DateTimeOffset? updated = timeMatch.Success && DateTimeOffset.TryParse(timeMatch.Groups[1].Value, CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var parsedDate)
                ? parsedDate
                : null;

            return new Quote
            {
                Name = config.Name,
                Symbol = config.Symbol,
                Isin = config.Isin ?? string.Empty,
                CountryCode = config.CountryCode ?? string.Empty,
                Price = price,
                PercentChange = percent,
                LastUpdated = updated
            };
        }
        catch
        {
            return null;
        }
    }

    private static Quote CreateUnavailable(QuoteConfig config) => new()
    {
        Name = config.Name,
        Symbol = config.Symbol,
        Isin = config.Isin ?? string.Empty,
        CountryCode = config.CountryCode ?? string.Empty,
        Price = null,
        PercentChange = null,
        LastUpdated = null
    };

    private static decimal? GetFirstArrayValue(JsonElement chartResult, string fieldName)
    {
        if (!chartResult.TryGetProperty("indicators", out var indicators) ||
            !indicators.TryGetProperty("quote", out var quoteArray) ||
            quoteArray.ValueKind != JsonValueKind.Array ||
            quoteArray.GetArrayLength() == 0 ||
            !quoteArray[0].TryGetProperty(fieldName, out var values) ||
            values.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        foreach (var value in values.EnumerateArray())
        {
            if (value.ValueKind == JsonValueKind.Number)
            {
                return Convert.ToDecimal(value.GetDouble());
            }
        }

        return null;
    }

    private static decimal? GetDecimal(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var value) || value.ValueKind == JsonValueKind.Null)
            return null;

        if (value.TryGetDecimal(out var decimalValue)) return decimalValue;
        if (value.TryGetDouble(out var doubleValue)) return Convert.ToDecimal(doubleValue);
        return null;
    }

    private static DateTimeOffset? GetDate(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var value) || value.ValueKind == JsonValueKind.Null)
            return null;

        if (value.TryGetInt64(out var unix)) return DateTimeOffset.FromUnixTimeSeconds(unix);
        return null;
    }

    private static long? GetLong(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var value) || value.ValueKind == JsonValueKind.Null)
            return null;

        if (value.TryGetInt64(out var longValue)) return longValue;
        if (value.TryGetDouble(out var doubleValue)) return Convert.ToInt64(doubleValue);
        return null;
    }
}
