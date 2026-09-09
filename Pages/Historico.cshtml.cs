using CotizacionesBolsaWeb.Models;
using CotizacionesBolsaWeb.Services;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Globalization;
using System.Text.Json;

namespace CotizacionesBolsaWeb.Pages;

public class HistoricoModel : PageModel
{
    private readonly YahooFinanceService _service = new();

    public string Symbol { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public string Range { get; private set; } = "1mo";
    public bool HasData { get; private set; }
    public string LabelsJson { get; private set; } = "[]";
    public string ValuesJson { get; private set; } = "[]";
    public string OpenJson { get; private set; } = "[]";
    public string HighJson { get; private set; } = "[]";
    public string LowJson { get; private set; } = "[]";
    public string CloseJson { get; private set; } = "[]";
    public string VolumeJson { get; private set; } = "[]";
    public string Sma20Json { get; private set; } = "[]";
    public string Sma50Json { get; private set; } = "[]";
    public string RsiJson { get; private set; } = "[]";

    public async Task OnGetAsync(string symbol, string? name, string? range)
    {
        Symbol = symbol ?? string.Empty;
        Name = string.IsNullOrWhiteSpace(name) ? Symbol : name;
        Range = string.IsNullOrWhiteSpace(range) ? "1mo" : range;

        var (yahooRange, yahooInterval, dateFormat) = Range switch
        {
            "1d" => ("1d", "5m", "HH:mm"),
            "5d" => ("5d", "15m", "dd/MM HH:mm"),
            "1mo" => ("1mo", "1d", "dd/MM"),
            "1y" => ("1y", "1wk", "MM/yyyy"),
            "max" => ("max", "1mo", "MM/yyyy"),
            _ => ("1mo", "1d", "dd/MM")
        };

        if (string.IsNullOrWhiteSpace(Symbol))
        {
            return;
        }

        var points = await _service.GetHistoryAsync(Symbol, yahooRange, yahooInterval);

        if (points.Count == 0)
        {
            HasData = false;
            return;
        }

        HasData = true;
        var labels = points.Select(p => TimeZoneInfo.ConvertTime(p.Date, Quote.SpainTimeZone).ToString(dateFormat, CultureInfo.InvariantCulture)).ToList();
        var closes = points.Select(p => p.Close).ToList();

        LabelsJson = JsonSerializer.Serialize(labels);
        ValuesJson = JsonSerializer.Serialize(closes);
        OpenJson = JsonSerializer.Serialize(points.Select(p => p.Open).ToList());
        HighJson = JsonSerializer.Serialize(points.Select(p => p.High).ToList());
        LowJson = JsonSerializer.Serialize(points.Select(p => p.Low).ToList());
        CloseJson = JsonSerializer.Serialize(closes);
        VolumeJson = JsonSerializer.Serialize(points.Select(p => p.Volume).ToList());
        Sma20Json = JsonSerializer.Serialize(ComputeSma(closes, 20));
        Sma50Json = JsonSerializer.Serialize(ComputeSma(closes, 50));
        RsiJson = JsonSerializer.Serialize(ComputeRsi(closes, 14));
    }

    private static List<decimal?> ComputeSma(List<decimal> closes, int period)
    {
        var result = new List<decimal?>(closes.Count);
        for (var i = 0; i < closes.Count; i++)
        {
            if (i < period - 1)
            {
                result.Add(null);
                continue;
            }

            decimal sum = 0;
            for (var j = i - period + 1; j <= i; j++)
            {
                sum += closes[j];
            }

            result.Add(sum / period);
        }

        return result;
    }

    private static List<decimal?> ComputeRsi(List<decimal> closes, int period)
    {
        var result = new List<decimal?>(closes.Count);
        if (closes.Count == 0)
        {
            return result;
        }

        result.Add(null);

        decimal avgGain = 0;
        decimal avgLoss = 0;

        for (var i = 1; i < closes.Count; i++)
        {
            var change = closes[i] - closes[i - 1];
            var gain = change > 0 ? change : 0;
            var loss = change < 0 ? -change : 0;

            if (i <= period)
            {
                avgGain += gain;
                avgLoss += loss;

                if (i < period)
                {
                    result.Add(null);
                    continue;
                }

                avgGain /= period;
                avgLoss /= period;
            }
            else
            {
                avgGain = ((avgGain * (period - 1)) + gain) / period;
                avgLoss = ((avgLoss * (period - 1)) + loss) / period;
            }

            if (avgLoss == 0)
            {
                result.Add(100m);
            }
            else
            {
                var rs = avgGain / avgLoss;
                result.Add(100m - (100m / (1m + rs)));
            }
        }

        return result;
    }
}
