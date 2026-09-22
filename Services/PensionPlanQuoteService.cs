using System.Globalization;
using System.Text.RegularExpressions;

namespace CotizacionesBolsaWeb.Services;

/// <summary>
/// Datos en vivo de un plan de pensiones obtenidos de comparadorfondos.com.
/// Cualquiera de las propiedades puede ser null si ese dato concreto no se
/// pudo extraer de la página (se conserva el valor guardado en ese caso).
/// </summary>
public sealed record PensionPlanLiveData(decimal? ValorLiquidativo, decimal? RentabilidadUltimoMes, decimal? Rentabilidad12Meses);

/// <summary>
/// Obtiene el valor liquidativo y las rentabilidades a corto plazo (1 mes y 12
/// meses) de un plan de pensiones español a partir de su código DGSFP (ej.
/// "N3901"), usando la ficha pública de comparadorfondos.com
/// (https://comparadorfondos.com/plans/{codigo}). No existe una fuente/API
/// oficial que cubra cualquier plan por código DGSFP (a diferencia de Yahoo
/// Finance para acciones/ETF), así que esto es un scraping de una web de
/// terceros: si su estructura HTML cambia o el plan no está indexado, se
/// devuelve null y el llamador debe conservar el último valor conocido/manual.
/// </summary>
public sealed class PensionPlanQuoteService
{
    private static readonly Regex DgsfpCodeRegex = new(@"\bN\d{3,5}\b", RegexOptions.Compiled);
    private static readonly Regex PriceRegex = new(@"tabular-nums"">\s*([\d.,]+)\s*€", RegexOptions.Compiled);
    private static readonly Regex RentabilidadUltimoMesRegex = BuildRentabilidadRegex("1M");
    private static readonly Regex Rentabilidad12MesesRegex = BuildRentabilidadRegex("1A");

    private readonly HttpClient _httpClient;

    public PensionPlanQuoteService(HttpClient? httpClient = null)
    {
        _httpClient = httpClient ?? new HttpClient();
        _httpClient.Timeout = TimeSpan.FromSeconds(10);
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/126.0.0.0 Safari/537.36");
    }

    private static Regex BuildRentabilidadRegex(string label) => new(
        $@"mb-1"">{Regex.Escape(label)}</span><span class=""text-sm font-semibold tabular-nums[^""]*"">([+-]?)(?:<!--\s*-->\s*)?([\d.,]+)\s*%",
        RegexOptions.Compiled);

    /// <summary>
    /// Extrae el código de plan DGSFP (formato "N" + dígitos) de un texto libre
    /// como "N3901 / F1399" y consulta sus datos en vivo. Devuelve null si no
    /// se encuentra código o la petición falla; si la petición tiene éxito
    /// pero algún dato concreto no se pudo extraer, esa propiedad queda null.
    /// </summary>
    public async Task<PensionPlanLiveData?> TryGetLiveDataAsync(string? codigoDgsfp, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(codigoDgsfp))
        {
            return null;
        }

        var codeMatch = DgsfpCodeRegex.Match(codigoDgsfp);
        if (!codeMatch.Success)
        {
            return null;
        }

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, $"https://comparadorfondos.com/plans/{codeMatch.Value}");
            request.Headers.Accept.ParseAdd("text/html");

            using var response = await _httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var html = await response.Content.ReadAsStringAsync(cancellationToken);

            return new PensionPlanLiveData(
                ParseEuros(PriceRegex.Match(html)),
                ParsePercent(RentabilidadUltimoMesRegex.Match(html)),
                ParsePercent(Rentabilidad12MesesRegex.Match(html)));
        }
        catch
        {
            return null;
        }
    }

    private static decimal? ParseEuros(Match match)
    {
        if (!match.Success)
        {
            return null;
        }

        var rawValue = match.Groups[1].Value.Replace(".", string.Empty).Replace(",", ".");
        return decimal.TryParse(rawValue, NumberStyles.Number, CultureInfo.InvariantCulture, out var value) ? value : null;
    }

    private static decimal? ParsePercent(Match match)
    {
        if (!match.Success)
        {
            return null;
        }

        var rawValue = match.Groups[2].Value.Replace(".", string.Empty).Replace(",", ".");
        if (!decimal.TryParse(rawValue, NumberStyles.Number, CultureInfo.InvariantCulture, out var value))
        {
            return null;
        }

        return match.Groups[1].Value == "-" ? -value : value;
    }
}
