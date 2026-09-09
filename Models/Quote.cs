namespace CotizacionesBolsaWeb.Models;

public sealed record QuoteConfig(string Name, string Symbol, string? Isin = null, string? CountryCode = null, string? Market = null);

public sealed class Quote
{
    public string Name { get; set; } = string.Empty;
    public string Symbol { get; set; } = string.Empty;
    public string Isin { get; set; } = string.Empty;
    public string CountryCode { get; set; } = string.Empty;
    public string Market { get; set; } = string.Empty;
    public decimal? Price { get; set; }
    public decimal? PercentChange { get; set; }
    public DateTimeOffset? LastUpdated { get; set; }

    public string DisplayPrice => Price.HasValue ? Price.Value.ToString("#,##0.00") : "N/A";
    public string DisplayPercent => PercentChange.HasValue ? $"{PercentChange.Value:0.00}%" : "N/A";
    public string DisplayIsin => string.IsNullOrWhiteSpace(Isin) ? "—" : Isin;
    public string DisplayMarket => string.IsNullOrWhiteSpace(Market) ? "—" : Market;
    public string UpdatedText => LastUpdated.HasValue ? LastUpdated.Value.LocalDateTime.ToString("dd/MM/yyyy HH:mm") : "N/A";
    public string TrendCssClass => PercentChange.HasValue && PercentChange.Value >= 0 ? "text-success" : "text-danger";
    public bool IsMarketOpen => ComputeMarketOpen(CountryCode);
    public string MarketStatusLabel => IsMarketOpen ? "Mercado abierto" : "Mercado cerrado";
    public string FlagUrl => string.IsNullOrWhiteSpace(CountryCode) ? "https://flagcdn.com/w40/gb.png" : CountryCode switch
    {
        "ES" => "https://flagcdn.com/w40/es.png",
        "DE" => "https://flagcdn.com/w40/de.png",
        "EU" => "https://flagcdn.com/w40/eu.png",
        "US" => "https://flagcdn.com/w40/us.png",
        "FR" => "https://flagcdn.com/w40/fr.png",
        "JP" => "https://flagcdn.com/w40/jp.png",
        "IT" => "https://flagcdn.com/w40/it.png",
        "CH" => "https://flagcdn.com/w40/ch.png",
        "LU" => "https://flagcdn.com/w40/lu.png",
        _ => "https://flagcdn.com/w40/gb.png"
    };

    private static bool ComputeMarketOpen(string countryCode)
    {
        var (timeZoneId, openTime, closeTime) = countryCode switch
        {
            "ES" or "FR" => ("Romance Standard Time", new TimeSpan(9, 0, 0), new TimeSpan(17, 30, 0)),
            "DE" or "EU" => ("W. Europe Standard Time", new TimeSpan(9, 0, 0), new TimeSpan(17, 30, 0)),
            "US" => ("Eastern Standard Time", new TimeSpan(9, 30, 0), new TimeSpan(16, 0, 0)),
            "JP" => ("Tokyo Standard Time", new TimeSpan(9, 0, 0), new TimeSpan(15, 0, 0)),
            _ => (string.Empty, TimeSpan.Zero, TimeSpan.Zero)
        };

        if (string.IsNullOrEmpty(timeZoneId))
        {
            return false;
        }

        try
        {
            var timeZone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
            var localNow = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, timeZone);

            if (localNow.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
            {
                return false;
            }

            var timeOfDay = localNow.TimeOfDay;
            return timeOfDay >= openTime && timeOfDay <= closeTime;
        }
        catch (TimeZoneNotFoundException)
        {
            return false;
        }
    }
}
