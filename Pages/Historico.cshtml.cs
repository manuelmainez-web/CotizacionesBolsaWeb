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
        var labels = points.Select(p => p.Date.LocalDateTime.ToString(dateFormat, CultureInfo.InvariantCulture)).ToList();
        var values = points.Select(p => p.Close).ToList();

        LabelsJson = JsonSerializer.Serialize(labels);
        ValuesJson = JsonSerializer.Serialize(values);
    }
}
