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
    public string DisplaySymbol => Symbol.TrimStart('^');
    public string Name { get; private set; } = string.Empty;
    public string Range { get; private set; } = "1mo";
    public bool HasData { get; private set; }
    public string? CountryCode { get; private set; }
    public string FlagUrl => Quote.GetFlagUrl(CountryCode);

    // Etiquetas de "Rentabilidad por periodo" que corresponden al periodo seleccionado en el desplegable
    // de "Cotización histórica" (Día/Semana/Mes/Año/Desde el principio), para resaltar las casillas a juego.
    // Mes resalta también 3/6 meses, y Año resalta también 5 años (periodos "contenidos" en la selección).
    public HashSet<string> RangeEtiquetasEquivalentes => Range switch
    {
        "1d" => new HashSet<string> { "1 día" },
        "5d" => new HashSet<string> { "1 semana" },
        "1mo" => new HashSet<string> { "1 mes", "3 meses", "6 meses" },
        "1y" => new HashSet<string> { "1 año", "5 años" },
        "max" => new HashSet<string> { "Desde el principio" },
        _ => new HashSet<string>()
    };

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
    public decimal? LastSma200 { get; private set; }
    public decimal? LastRsi { get; private set; }
    public string TendenciaTexto { get; private set; } = "N/D";
    public string TendenciaCss { get; private set; } = "text-muted";
    public string TendenciaLargoPlazoTexto { get; private set; } = "N/D";
    public string TendenciaLargoPlazoCss { get; private set; } = "text-muted";
    public string RsiTexto { get; private set; } = "N/D";
    public string RsiCss { get; private set; } = "text-muted";
    public string MacdTexto { get; private set; } = "N/D";
    public string MacdCss { get; private set; } = "text-muted";
    public string EstocasticoTexto { get; private set; } = "N/D";
    public string EstocasticoCss { get; private set; } = "text-muted";
    public string WilliamsRTexto { get; private set; } = "N/D";
    public string WilliamsRCss { get; private set; } = "text-muted";
    public string BollingerTexto { get; private set; } = "N/D";
    public string MomentumTexto { get; private set; } = "N/D";
    public string MomentumCss { get; private set; } = "text-muted";
    public string AtrTexto { get; private set; } = "N/D";
    public string VolumenRelativoTexto { get; private set; } = "N/D";
    public string VolumenRelativoCss { get; private set; } = "text-muted";
    public string SoporteResistenciaTexto { get; private set; } = "N/D";
    public decimal? PeriodoMax { get; private set; }
    public decimal? PeriodoMin { get; private set; }
    public decimal? VariacionPeriodoPct { get; private set; }
    public long? VolumenMedio { get; private set; }

    // Señales de compra/venta agregadas (estilo "resumen técnico" de Investing.com)
    public string SenalMediasMovilesTexto { get; private set; } = "N/D";
    public string SenalMediasMovilesCss { get; private set; } = "text-muted";
    public string SenalOsciladoresTexto { get; private set; } = "N/D";
    public string SenalOsciladoresCss { get; private set; } = "text-muted";
    public string RecomendacionGlobalTexto { get; private set; } = "N/D";
    public string RecomendacionGlobalCss { get; private set; } = "text-amber";

    // Datos de análisis fundamental. Rango 52 semanas / mercado / divisa vía Yahoo Finance (siempre disponibles);
    // el resto (PER, capitalización, dividendo, etc.) vía StockAnalysisService cuando el instrumento es una acción.
    public decimal? FiftyTwoWeekHigh { get; private set; }
    public decimal? FiftyTwoWeekLow { get; private set; }
    public string? Exchange { get; private set; }
    public string? Currency { get; private set; }
    public string? InstrumentTypeTexto { get; private set; }
    public bool HasFundamentalData { get; private set; }
    public string? MarketCap { get; private set; }
    public string? PeRatio { get; private set; }
    public string? ForwardPe { get; private set; }
    public string? Eps { get; private set; }
    public string? DividendInfo { get; private set; }
    public string? ExDividendDate { get; private set; }
    public string? Revenue { get; private set; }
    public string? NetIncome { get; private set; }
    public string? SharesOutstanding { get; private set; }
    public string? Beta { get; private set; }
    public string? PriceTarget { get; private set; }
    public string? Sector { get; private set; }
    public string? Employees { get; private set; }
    public string? IpoDate { get; private set; }
    public string? FundamentalSourceNote { get; private set; }

    // Rentabilidad desglosada por periodo (independiente del rango seleccionado para la gráfica)
    public List<RentabilidadPeriodo> RentabilidadPeriodos { get; private set; } = new();

    private readonly StockAnalysisService _fundamentalService = new();

    public async Task OnGetAsync(string symbol, string? name, string? range, string? countryCode)
    {
        Symbol = symbol ?? string.Empty;
        Name = string.IsNullOrWhiteSpace(name) ? Symbol : name;
        Range = string.IsNullOrWhiteSpace(range) ? "1mo" : range;
        CountryCode = countryCode;

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
        var highs = points.Select(p => p.High).ToList();
        var lows = points.Select(p => p.Low).ToList();
        var volumes = points.Select(p => p.Volume).ToList();

        LabelsJson = JsonSerializer.Serialize(labels);
        ValuesJson = JsonSerializer.Serialize(closes);
        OpenJson = JsonSerializer.Serialize(points.Select(p => p.Open).ToList());
        HighJson = JsonSerializer.Serialize(highs);
        LowJson = JsonSerializer.Serialize(lows);
        CloseJson = JsonSerializer.Serialize(closes);
        VolumeJson = JsonSerializer.Serialize(volumes);
        var sma20 = ComputeSma(closes, 20);
        var sma50 = ComputeSma(closes, 50);
        var rsi = ComputeRsi(closes, 14);
        Sma20Json = JsonSerializer.Serialize(sma20);
        Sma50Json = JsonSerializer.Serialize(sma50);
        RsiJson = JsonSerializer.Serialize(rsi);

        ComputeResumenTecnico(closes, highs, lows, volumes, sma20, sma50, rsi);

        // Rentabilidad desglosada por periodo (1d/1sem/1mes/3m/6m/1a/5a/desde el principio): se calcula
        // sobre históricos propios, independientes del rango elegido para la gráfica, para que sean fiables
        // aunque el usuario esté viendo "Día". Yahoo Finance downsamplea a velas MENSUALES cuando se pide
        // range=max en símbolos con histórico muy largo (ej. acciones desde los 80), lo que hacía que
        // "1 semana"/"1 mes"/"3 meses" salieran mal (todos "enganchaban" al mismo punto mensual más cercano).
        // Por eso se piden dos históricos: uno de 10 años en diario (fiable hasta 5 años) y el "max" en
        // mensual solo para el primer punto disponible (inicio real de cotización).
        var historialDiario10y = await _service.GetHistoryAsync(Symbol, "10y", "1d");
        var historialMensualCompleto = await _service.GetHistoryAsync(Symbol, "max", "1mo");
        RentabilidadPeriodos = ComputeRentabilidadPeriodos(
            historialDiario10y.Count > 0 ? historialDiario10y : points,
            historialMensualCompleto);

        // Fundamentales: Yahoo Finance (rango 52 semanas / mercado / divisa) siempre que se pueda,
        // ampliados con StockAnalysisService (PER, capitalización, dividendo, etc.) para acciones.
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

        var fundamentals = await _fundamentalService.GetFundamentalsAsync(Symbol);
        if (fundamentals != null)
        {
            HasFundamentalData = true;
            MarketCap = fundamentals.MarketCap;
            PeRatio = fundamentals.PeRatio;
            ForwardPe = fundamentals.ForwardPe;
            Eps = fundamentals.Eps;
            DividendInfo = fundamentals.DividendInfo;
            ExDividendDate = fundamentals.ExDividendDate;
            Revenue = fundamentals.Revenue;
            NetIncome = fundamentals.NetIncome;
            SharesOutstanding = fundamentals.SharesOutstanding;
            Beta = fundamentals.Beta;
            PriceTarget = fundamentals.PriceTarget;
            Sector = fundamentals.Sector;
            Employees = fundamentals.Employees;
            IpoDate = fundamentals.IpoDate;
            FundamentalSourceNote = "Datos de mercado/cotización vía Yahoo Finance. Datos fundamentales (PER, capitalización, dividendo, etc.) vía StockAnalysis.com — fuente pública de referencia, no oficial.";
        }
        else if (HasFundamentalData)
        {
            FundamentalSourceNote = "Solo hay disponibles datos básicos de mercado (rango 52 semanas, bolsa, divisa). Los datos fundamentales detallados (PER, capitalización, dividendo...) no están disponibles públicamente para este tipo de instrumento o símbolo.";
        }
    }

    private void ComputeResumenTecnico(List<decimal> closes, List<decimal> highs, List<decimal> lows, List<long> volumes, List<decimal?> sma20, List<decimal?> sma50, List<decimal?> rsi)
    {
        // Contadores de señales para el resumen técnico global (estilo "medias móviles" / "osciladores" de Investing.com)
        var senalesMediasMoviles = new List<int>(); // 1 = compra, -1 = venta, 0 = neutral
        var senalesOsciladores = new List<int>();

        LastPrice = closes.Count > 0 ? closes[^1] : null;
        LastSma20 = sma20.LastOrDefault(v => v.HasValue);
        LastSma50 = sma50.LastOrDefault(v => v.HasValue);

        var sma200 = ComputeSma(closes, 200);
        LastSma200 = sma200.LastOrDefault(v => v.HasValue);
        LastRsi = rsi.LastOrDefault(v => v.HasValue);

        if (LastPrice.HasValue && LastSma20.HasValue)
        {
            senalesMediasMoviles.Add(LastPrice.Value > LastSma20.Value ? 1 : -1);
        }

        if (LastPrice.HasValue && LastSma50.HasValue)
        {
            senalesMediasMoviles.Add(LastPrice.Value > LastSma50.Value ? 1 : -1);
        }

        if (LastPrice.HasValue && LastSma20.HasValue && LastSma50.HasValue)
        {
            if (LastPrice.Value > LastSma20.Value && LastSma20.Value >= LastSma50.Value)
            {
                TendenciaTexto = "Compra (cruce alcista SMA 20/50)";
                TendenciaCss = "text-success";
            }
            else if (LastPrice.Value < LastSma20.Value && LastSma20.Value <= LastSma50.Value)
            {
                TendenciaTexto = "Venta (cruce bajista SMA 20/50)";
                TendenciaCss = "text-danger";
            }
            else
            {
                TendenciaTexto = "Neutral (señales mixtas)";
                TendenciaCss = "text-muted";
            }
        }

        if (LastPrice.HasValue && LastSma200.HasValue)
        {
            senalesMediasMoviles.Add(LastPrice.Value > LastSma200.Value ? 1 : -1);

            if (LastPrice.Value > LastSma200.Value)
            {
                TendenciaLargoPlazoTexto = $"Compra (precio sobre SMA 200: {LastSma200.Value:#,##0.00})";
                TendenciaLargoPlazoCss = "text-success";
            }
            else
            {
                TendenciaLargoPlazoTexto = $"Venta (precio bajo SMA 200: {LastSma200.Value:#,##0.00})";
                TendenciaLargoPlazoCss = "text-danger";
            }
        }
        else
        {
            TendenciaLargoPlazoTexto = "Requiere más histórico (usa Año o Desde el principio)";
        }

        if (LastRsi.HasValue)
        {
            if (LastRsi.Value >= 70)
            {
                RsiTexto = $"{LastRsi.Value:0.0} · Venta (sobrecompra)";
                RsiCss = "text-danger";
                senalesOsciladores.Add(-1);
            }
            else if (LastRsi.Value <= 30)
            {
                RsiTexto = $"{LastRsi.Value:0.0} · Compra (sobreventa)";
                RsiCss = "text-success";
                senalesOsciladores.Add(1);
            }
            else
            {
                RsiTexto = $"{LastRsi.Value:0.0} · Neutral";
                RsiCss = "text-muted";
                senalesOsciladores.Add(0);
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

            if (VolumenMedio.Value > 0)
            {
                var ultimoVolumen = volumes[^1];
                var ratio = (decimal)ultimoVolumen / VolumenMedio.Value * 100m;
                VolumenRelativoTexto = $"{ratio:0}% del medio ({ultimoVolumen:#,##0})";
                VolumenRelativoCss = ratio >= 150 ? "text-success" : ratio <= 50 ? "text-danger" : "text-muted";
            }
        }

        // MACD (12, 26, 9)
        var ema12 = ComputeEma(closes, 12);
        var ema26 = ComputeEma(closes, 26);
        var macdLine = new List<decimal?>(closes.Count);
        for (var i = 0; i < closes.Count; i++)
        {
            macdLine.Add(ema12[i].HasValue && ema26[i].HasValue ? ema12[i]!.Value - ema26[i]!.Value : null);
        }

        var macdValues = macdLine.Where(v => v.HasValue).Select(v => v!.Value).ToList();
        var signalLine = ComputeEma(macdValues, 9);
        var lastMacd = macdLine.LastOrDefault(v => v.HasValue);
        var lastSignal = signalLine.Count > 0 ? signalLine.LastOrDefault(v => v.HasValue) : null;

        if (lastMacd.HasValue && lastSignal.HasValue)
        {
            var histograma = lastMacd.Value - lastSignal.Value;
            var esAlcista = histograma > 0;
            MacdTexto = $"{lastMacd.Value:0.00} vs señal {lastSignal.Value:0.00} · {(esAlcista ? "Compra (cruce alcista)" : "Venta (cruce bajista)")}";
            MacdCss = esAlcista ? "text-success" : "text-danger";
            senalesOsciladores.Add(esAlcista ? 1 : -1);
        }

        // Bandas de Bollinger (20, 2 desviaciones típicas)
        var bollingerMedia = sma20.LastOrDefault(v => v.HasValue);
        if (bollingerMedia.HasValue && closes.Count >= 20)
        {
            var ultimos20 = closes.Skip(closes.Count - 20).ToList();
            var media = ultimos20.Average();
            var varianza = ultimos20.Select(c => (c - media) * (c - media)).Sum() / ultimos20.Count;
            var desviacion = (decimal)Math.Sqrt((double)varianza);
            var bandaSuperior = media + (2 * desviacion);
            var bandaInferior = media - (2 * desviacion);

            var posicion = LastPrice.HasValue
                ? (LastPrice.Value >= bandaSuperior ? "Venta (sobre banda superior)"
                    : LastPrice.Value <= bandaInferior ? "Compra (bajo banda inferior)"
                    : "Neutral (dentro de las bandas)")
                : "N/D";

            BollingerTexto = $"{bandaInferior:#,##0.00} - {bandaSuperior:#,##0.00} · {posicion}";
        }

        // Estocástico (14, 3)
        if (closes.Count >= 14 && highs.Count == closes.Count && lows.Count == closes.Count)
        {
            var estocasticoK = ComputeStochasticK(highs, lows, closes, 14);
            var kSuavizado = ComputeSma(estocasticoK.Where(v => v.HasValue).Select(v => v!.Value).ToList(), 3);
            var lastK = estocasticoK.LastOrDefault(v => v.HasValue);
            var lastD = kSuavizado.Count > 0 ? kSuavizado.LastOrDefault(v => v.HasValue) : null;

            if (lastK.HasValue)
            {
                var dTexto = lastD.HasValue ? $" / %D {lastD.Value:0.0}" : string.Empty;
                if (lastK.Value >= 80)
                {
                    EstocasticoTexto = $"%K {lastK.Value:0.0}{dTexto} · Venta (sobrecompra)";
                    EstocasticoCss = "text-danger";
                    senalesOsciladores.Add(-1);
                }
                else if (lastK.Value <= 20)
                {
                    EstocasticoTexto = $"%K {lastK.Value:0.0}{dTexto} · Compra (sobreventa)";
                    EstocasticoCss = "text-success";
                    senalesOsciladores.Add(1);
                }
                else
                {
                    EstocasticoTexto = $"%K {lastK.Value:0.0}{dTexto} · Neutral";
                    EstocasticoCss = "text-muted";
                    senalesOsciladores.Add(0);
                }
            }

            // Williams %R (14)
            var williamsR = ComputeWilliamsR(highs, lows, closes, 14);
            var lastWilliams = williamsR.LastOrDefault(v => v.HasValue);
            if (lastWilliams.HasValue)
            {
                if (lastWilliams.Value >= -20)
                {
                    WilliamsRTexto = $"{lastWilliams.Value:0.0} · Venta (sobrecompra)";
                    WilliamsRCss = "text-danger";
                    senalesOsciladores.Add(-1);
                }
                else if (lastWilliams.Value <= -80)
                {
                    WilliamsRTexto = $"{lastWilliams.Value:0.0} · Compra (sobreventa)";
                    WilliamsRCss = "text-success";
                    senalesOsciladores.Add(1);
                }
                else
                {
                    WilliamsRTexto = $"{lastWilliams.Value:0.0} · Neutral";
                    WilliamsRCss = "text-muted";
                    senalesOsciladores.Add(0);
                }
            }

            // ATR (14) - volatilidad media
            var atr = ComputeAtr(highs, lows, closes, 14);
            var lastAtr = atr.LastOrDefault(v => v.HasValue);
            if (lastAtr.HasValue)
            {
                var atrPct = LastPrice.HasValue && LastPrice.Value != 0 ? lastAtr.Value / LastPrice.Value * 100m : (decimal?)null;
                AtrTexto = atrPct.HasValue ? $"{lastAtr.Value:0.00} ({atrPct.Value:0.0}% del precio)" : $"{lastAtr.Value:0.00}";
            }
        }

        // Momentum / Rate of Change (10 periodos)
        const int periodoMomentum = 10;
        if (closes.Count > periodoMomentum && closes[^(periodoMomentum + 1)] != 0)
        {
            var referencia = closes[^(periodoMomentum + 1)];
            var roc = (closes[^1] - referencia) / referencia * 100m;
            var esPositivo = roc >= 0;
            MomentumTexto = $"{roc:+0.00;-0.00}% · {(esPositivo ? "Compra" : "Venta")}";
            MomentumCss = esPositivo ? "text-success" : "text-danger";
            senalesOsciladores.Add(esPositivo ? 1 : -1);
        }

        // Soporte / resistencia recientes (mínimo y máximo de las últimas 20 sesiones)
        const int periodoSoporte = 20;
        if (highs.Count > 0 && lows.Count > 0)
        {
            var n = Math.Min(periodoSoporte, closes.Count);
            var resistencia = highs.Skip(highs.Count - n).Max();
            var soporte = lows.Skip(lows.Count - n).Min();
            SoporteResistenciaTexto = $"Soporte {soporte:#,##0.00} · Resistencia {resistencia:#,##0.00}";
        }

        // Resumen de señales de medias móviles (SMA 20 / SMA 50 / SMA 200 vs. precio)
        AsignarResumenSenal(senalesMediasMoviles, valor =>
        {
            SenalMediasMovilesTexto = valor.Texto;
            SenalMediasMovilesCss = valor.Css;
        });

        // Resumen de señales de osciladores (RSI, MACD, Estocástico, Williams %R, Momentum)
        AsignarResumenSenal(senalesOsciladores, valor =>
        {
            SenalOsciladoresTexto = valor.Texto;
            SenalOsciladoresCss = valor.Css;
        });

        // Recomendación técnica global: combina medias móviles + osciladores (estilo "resumen técnico" de Investing.com)
        var todasLasSenales = senalesMediasMoviles.Concat(senalesOsciladores).ToList();
        if (todasLasSenales.Count > 0)
        {
            var compras = todasLasSenales.Count(s => s > 0);
            var ventas = todasLasSenales.Count(s => s < 0);
            var neutrales = todasLasSenales.Count(s => s == 0);
            var total = todasLasSenales.Count;

            string etiqueta;
            string css;
            if (compras >= total * 0.7m)
            {
                etiqueta = "COMPRA FUERTE";
                css = "text-success";
            }
            else if (compras > ventas)
            {
                etiqueta = "COMPRA";
                css = "text-success";
            }
            else if (ventas >= total * 0.7m)
            {
                etiqueta = "VENTA FUERTE";
                css = "text-danger";
            }
            else if (ventas > compras)
            {
                etiqueta = "VENTA";
                css = "text-danger";
            }
            else
            {
                etiqueta = "NEUTRAL";
                css = "text-amber";
            }

            RecomendacionGlobalTexto = $"{etiqueta} · {compras} compra(s), {ventas} venta(s), {neutrales} neutral(es) de {total} señales";
            RecomendacionGlobalCss = css;
        }
    }

    private static void AsignarResumenSenal(List<int> senales, Action<(string Texto, string Css)> asignar)
    {
        if (senales.Count == 0)
        {
            asignar(("N/D", "text-muted"));
            return;
        }

        var compras = senales.Count(s => s > 0);
        var ventas = senales.Count(s => s < 0);
        var total = senales.Count;

        if (compras > ventas)
        {
            asignar(($"Compra ({compras}/{total} señales)", "text-success"));
        }
        else if (ventas > compras)
        {
            asignar(($"Venta ({ventas}/{total} señales)", "text-danger"));
        }
        else
        {
            asignar(($"Neutral ({compras} compra / {ventas} venta de {total})", "text-muted"));
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

    private static List<decimal?> ComputeEma(List<decimal> values, int period)
    {
        var result = new List<decimal?>(values.Count);
        if (values.Count == 0)
        {
            return result;
        }

        var multiplicador = 2m / (period + 1);
        decimal? emaAnterior = null;

        for (var i = 0; i < values.Count; i++)
        {
            if (i < period - 1)
            {
                result.Add(null);
                continue;
            }

            if (i == period - 1)
            {
                emaAnterior = values.Take(period).Average();
                result.Add(emaAnterior);
                continue;
            }

            emaAnterior = ((values[i] - emaAnterior!.Value) * multiplicador) + emaAnterior.Value;
            result.Add(emaAnterior);
        }

        return result;
    }

    private static List<decimal?> ComputeStochasticK(List<decimal> highs, List<decimal> lows, List<decimal> closes, int period)
    {
        var result = new List<decimal?>(closes.Count);
        for (var i = 0; i < closes.Count; i++)
        {
            if (i < period - 1)
            {
                result.Add(null);
                continue;
            }

            var maxHigh = highs.Skip(i - period + 1).Take(period).Max();
            var minLow = lows.Skip(i - period + 1).Take(period).Min();
            var rango = maxHigh - minLow;
            result.Add(rango == 0 ? 50m : (closes[i] - minLow) / rango * 100m);
        }

        return result;
    }

    private static List<decimal?> ComputeWilliamsR(List<decimal> highs, List<decimal> lows, List<decimal> closes, int period)
    {
        var result = new List<decimal?>(closes.Count);
        for (var i = 0; i < closes.Count; i++)
        {
            if (i < period - 1)
            {
                result.Add(null);
                continue;
            }

            var maxHigh = highs.Skip(i - period + 1).Take(period).Max();
            var minLow = lows.Skip(i - period + 1).Take(period).Min();
            var rango = maxHigh - minLow;
            result.Add(rango == 0 ? -50m : (maxHigh - closes[i]) / rango * -100m);
        }

        return result;
    }

    private static List<decimal?> ComputeAtr(List<decimal> highs, List<decimal> lows, List<decimal> closes, int period)
    {
        var trueRanges = new List<decimal>(closes.Count);
        for (var i = 0; i < closes.Count; i++)
        {
            if (i == 0)
            {
                trueRanges.Add(highs[i] - lows[i]);
                continue;
            }

            var rangoActual = highs[i] - lows[i];
            var altoVsCierrePrevio = Math.Abs(highs[i] - closes[i - 1]);
            var bajoVsCierrePrevio = Math.Abs(lows[i] - closes[i - 1]);
            trueRanges.Add(Math.Max(rangoActual, Math.Max(altoVsCierrePrevio, bajoVsCierrePrevio)));
        }

        var result = new List<decimal?>(closes.Count);
        decimal? atrAnterior = null;

        for (var i = 0; i < trueRanges.Count; i++)
        {
            if (i < period - 1)
            {
                result.Add(null);
                continue;
            }

            if (i == period - 1)
            {
                atrAnterior = trueRanges.Take(period).Average();
                result.Add(atrAnterior);
                continue;
            }

            atrAnterior = ((atrAnterior!.Value * (period - 1)) + trueRanges[i]) / period;
            result.Add(atrAnterior);
        }

        return result;
    }

    private static List<RentabilidadPeriodo> ComputeRentabilidadPeriodos(
        List<(DateTimeOffset Date, decimal Open, decimal High, decimal Low, decimal Close, long Volume)> historialReciente,
        List<(DateTimeOffset Date, decimal Open, decimal High, decimal Low, decimal Close, long Volume)> historialCompleto)
    {
        var resultado = new List<RentabilidadPeriodo>();
        if (historialReciente.Count == 0)
        {
            return resultado;
        }

        var ultimo = historialReciente[^1];
        var ahora = ultimo.Date;

        decimal? PctDesde(DateTimeOffset limite)
        {
            var referencia = historialReciente.LastOrDefault(p => p.Date <= limite);
            if (referencia.Date == default || referencia.Close == 0)
            {
                return null;
            }

            return (ultimo.Close - referencia.Close) / referencia.Close * 100m;
        }

        decimal? rentabilidad1Dia = historialReciente.Count >= 2 && historialReciente[^2].Close != 0
            ? (ultimo.Close - historialReciente[^2].Close) / historialReciente[^2].Close * 100m
            : null;

        resultado.Add(new RentabilidadPeriodo("1 día", rentabilidad1Dia));
        resultado.Add(new RentabilidadPeriodo("1 semana", PctDesde(ahora.AddDays(-7))));
        resultado.Add(new RentabilidadPeriodo("1 mes", PctDesde(ahora.AddMonths(-1))));
        resultado.Add(new RentabilidadPeriodo("3 meses", PctDesde(ahora.AddMonths(-3))));
        resultado.Add(new RentabilidadPeriodo("6 meses", PctDesde(ahora.AddMonths(-6))));
        resultado.Add(new RentabilidadPeriodo("1 año", PctDesde(ahora.AddYears(-1))));
        resultado.Add(new RentabilidadPeriodo("5 años", PctDesde(ahora.AddYears(-5))));

        var primero = historialCompleto.Count > 0 ? historialCompleto[0] : historialReciente[0];
        resultado.Add(new RentabilidadPeriodo("Desde el principio", primero.Close != 0 ? (ultimo.Close - primero.Close) / primero.Close * 100m : null));

        return resultado;
    }
}

public sealed record RentabilidadPeriodo(string Etiqueta, decimal? Porcentaje);
