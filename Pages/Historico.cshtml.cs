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

    // Resumen de análisis técnico (calculado a partir de los mismos datos históricos)
    public decimal? LastPrice { get; private set; }
    public decimal? LastSma20 { get; private set; }
    public decimal? LastSma50 { get; private set; }
    public decimal? LastRsi { get; private set; }
    public string TendenciaTexto { get; private set; } = "N/D";
    public string TendenciaCss { get; private set; } = "text-muted";
    public string RsiTexto { get; private set; } = "N/D";
    public string RsiCss { get; private set; } = "text-muted";
    public decimal? PeriodoMax { get; private set; }
    public decimal? PeriodoMin { get; private set; }
    public decimal? VariacionPeriodoPct { get; private set; }
    public long? VolumenMedio { get; private set; }

    // Datos de análisis fundamental disponibles públicamente (sin autenticación) vía Yahoo Finance
    public decimal? FiftyTwoWeekHigh { get; private set; }
    public decimal? FiftyTwoWeekLow { get; private set; }
    public string? Exchange { get; private set; }
    public string? Currency { get; private set; }
    public string? InstrumentTypeTexto { get; private set; }
    public bool HasFundamentalData { get; private set; }

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
        var sma20 = ComputeSma(closes, 20);
        var sma50 = ComputeSma(closes, 50);
        var rsi = ComputeRsi(closes, 14);
        Sma20Json = JsonSerializer.Serialize(sma20);
        Sma50Json = JsonSerializer.Serialize(sma50);
        RsiJson = JsonSerializer.Serialize(rsi);

        ComputeResumenTecnico(closes, points.Select(p => p.Volume).ToList(), sma20, sma50, rsi);

        var meta = await _service.GetQuoteMetaAsync(Symbol);
        if (meta != null)
        {
            HasFundamentalData = true;
            FiftyTwoWeekHigh = meta.FiftyTwoWeekHigh;
            FiftyTwoWeekLow = meta.FiftyTwoWeekLow;
            Exchange = meta.Exchange;
            Currency = meta.Currency;
            InstrumentTypeTexto = meta.InstrumentType switch
            {
                "EQUITY" => "Acción",
                "ETF" => "ETF / Fondo cotizado",
                "INDEX" => "Índice",
                "CURRENCY" => "Divisa",
                "FUTURE" => "Futuro / Materia prima",
                "MUTUALFUND" => "Fondo de inversión",
                _ => meta.InstrumentType
            };
        }
    }

    private void ComputeResumenTecnico(List<decimal> closes, List<long> volumes, List<decimal?> sma20, List<decimal?> sma50, List<decimal?> rsi)
    {
        LastPrice = closes.Count > 0 ? closes[^1] : null;
        LastSma20 = sma20.LastOrDefault(v => v.HasValue);
        LastSma50 = sma50.LastOrDefault(v => v.HasValue);
        LastRsi = rsi.LastOrDefault(v => v.HasValue);

        if (LastPrice.HasValue && LastSma20.HasValue && LastSma50.HasValue)
        {
            if (LastPrice.Value > LastSma20.Value && LastSma20.Value >= LastSma50.Value)
            {
                TendenciaTexto = "Alcista";
                TendenciaCss = "text-success";
            }
            else if (LastPrice.Value < LastSma20.Value && LastSma20.Value <= LastSma50.Value)
            {
                TendenciaTexto = "Bajista";
                TendenciaCss = "text-danger";
            }
            else
            {
                TendenciaTexto = "Lateral / mixta";
                TendenciaCss = "text-muted";
            }
        }

        if (LastRsi.HasValue)
        {
            if (LastRsi.Value >= 70)
            {
                RsiTexto = $"{LastRsi.Value:0.0} · Sobrecompra";
                RsiCss = "text-danger";
            }
            else if (LastRsi.Value <= 30)
            {
                RsiTexto = $"{LastRsi.Value:0.0} · Sobreventa";
                RsiCss = "text-success";
            }
            else
            {
                RsiTexto = $"{LastRsi.Value:0.0} · Neutral";
                RsiCss = "text-muted";
            }
        }

        if (closes.Count > 0)
        {
            PeriodoMax = closes.Max();
            PeriodoMin = closes.Min();
        }

        if (closes.Count >= 2 && closes[0] != 0)
        {
            VariacionPeriodoPct = (closes[^1] - closes[0]) / closes[0] * 100m;
        }

        if (volumes.Count > 0)
        {
            VolumenMedio = (long)volumes.Average();
        }
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
