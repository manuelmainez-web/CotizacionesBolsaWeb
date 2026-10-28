namespace CotizacionesBolsaWeb.Services;

/// <summary>Operación de compra/venta de un título (importes siempre en positivo).</summary>
public sealed record TaxTransaction(string Key, string Isin, string Label, bool IsSale, decimal Shares, decimal Amount, DateTime Date, int Order);

/// <summary>Resultado fiscal de una venta calculado con el criterio FIFO.</summary>
public sealed class TaxSaleResult
{
    public string Key { get; init; } = string.Empty;
    public string Isin { get; init; } = string.Empty;
    public string Label { get; init; } = string.Empty;
    public DateTime Date { get; init; }
    public decimal Shares { get; init; }
    public decimal Proceeds { get; init; }
    public decimal Cost { get; init; }
    public decimal Gain { get; init; }
    public decimal DeferredLoss { get; set; }
    public decimal UnmatchedShares { get; init; }
    public List<TaxLotMatch> Lots { get; init; } = new();
    public List<TaxPurchaseRef> WindowPurchases { get; } = new();
    public int WashMonths { get; init; }
    public DateTime? OpenWindowUntil { get; set; }

    /// <summary>Importe que computa en el ejercicio (ganancia, o pérdida no diferida).</summary>
    public decimal Computable => Gain >= 0 ? Gain : Gain + DeferredLoss;
}

/// <summary>Compra consumida por una venta (FIFO): fecha, títulos usados de los comprados y coste imputado.</summary>
public sealed record TaxLotMatch(DateTime Date, decimal SharesUsed, decimal SharesBought, decimal Cost, int Order);

/// <summary>Compra de valores homogéneos dentro de la ventana antiaplicación de una venta con pérdida.</summary>
public sealed record TaxPurchaseRef(DateTime Date, decimal Shares, decimal Amount);

public sealed record TaxBracketRow(decimal From, decimal? To, decimal Rate, decimal AmountInBracket, decimal Tax, bool IsReached, bool IsMarginal);

public sealed class TaxResult
{
    public int Year { get; init; }
    public List<TaxSaleResult> Sales { get; init; } = new();
    public decimal Gains { get; init; }
    public decimal Losses { get; init; }
    public decimal DeferredLosses { get; init; }
    public decimal NetResult { get; init; }
    public decimal PriorLossesApplied { get; init; }
    public decimal PriorLossesPending { get; init; }
    public decimal CarryForwardLoss { get; init; }
    public decimal OtherIncome { get; init; }
    public decimal OtherIncomeCompensation { get; init; }
    public decimal SavingsBase { get; init; }
    public List<TaxBracketRow> Brackets { get; init; } = new();
    public decimal Tax { get; init; }
    public decimal Withholdings { get; init; }
    public decimal ToPay { get; init; }
    public decimal MarginalRate { get; init; }
    public decimal AverageRate { get; init; }
    public decimal UnmatchedShares { get; init; }
    public List<TaxSaleResult> OpenWindowSales { get; init; } = new();
}

/// <summary>
/// Cálculo orientativo del IRPF (base del ahorro) de las operaciones de compra-venta de valores/ETF/fondos:
/// transmisiones por FIFO (art. 37.2 LIRPF), norma antiaplicación de recompras (art. 33.5 f/g: 2 meses valores
/// cotizados, 1 año fondos no cotizados), compensación de pérdidas de los 4 años anteriores (art. 49) y escala
/// del ahorro estatal + autonómica general (art. 66 y 76, vigente desde 2025: 19/21/23/27/30 %).
/// </summary>
public static class TaxCalculator
{
    private static readonly (decimal From, decimal? To, decimal Rate)[] SavingsScale =
    {
        (0m, 6_000m, 0.19m),
        (6_000m, 50_000m, 0.21m),
        (50_000m, 200_000m, 0.23m),
        (200_000m, 300_000m, 0.27m),
        (300_000m, null, 0.30m)
    };

    private sealed class Lot
    {
        public DateTime Date { get; init; }
        public decimal Shares { get; set; }
        public decimal Cost { get; set; }
        public decimal SharesBought { get; init; }
        public int Order { get; init; }
    }

    private sealed class Deferral
    {
        public required TaxSaleResult Sale { get; init; }
        public DateTime WindowEnd { get; init; }
        public decimal SharesPending { get; set; }
        public decimal LossPending { get; set; }
    }

    public static decimal SavingsTaxOn(decimal amount)
    {
        var tax = 0m;
        foreach (var (from, to, rate) in SavingsScale)
        {
            tax += Math.Max(0m, Math.Min(amount, to ?? decimal.MaxValue) - from) * rate;
        }

        return tax;
    }

    public static TaxResult Compute(IEnumerable<TaxTransaction> transactions, int year, Func<string, int> washMonthsFor,
        decimal otherIncome, decimal withholdings)
    {
        var allSales = new List<TaxSaleResult>();

        foreach (var group in transactions.GroupBy(t => t.Key))
        {
            var months = washMonthsFor(group.First().Isin);
            var events = group
                .OrderBy(t => t.Date)
                .ThenBy(t => t.IsSale ? 1 : 0)
                .ThenBy(t => t.Order)
                .ToList();

            var lots = new List<Lot>();
            var deferrals = new List<Deferral>();

            foreach (var ev in events)
            {
                if (!ev.IsSale)
                {
                    var lot = new Lot { Date = ev.Date, Shares = ev.Shares, Cost = ev.Amount, SharesBought = ev.Shares, Order = ev.Order };
                    var toAttach = ev.Shares;

                    foreach (var deferral in deferrals.Where(d => d.SharesPending > 0 && ev.Date > d.Sale.Date && ev.Date <= d.WindowEnd))
                    {
                        if (toAttach <= 0)
                        {
                            break;
                        }

                        var allocated = Math.Min(toAttach, deferral.SharesPending);
                        var loss = deferral.LossPending * (allocated / deferral.SharesPending);
                        lot.Cost += loss;
                        deferral.Sale.DeferredLoss += loss;
                        deferral.LossPending -= loss;
                        deferral.SharesPending -= allocated;
                        toAttach -= allocated;
                    }

                    lots.Add(lot);
                    continue;
                }

                // Venta: se consumen los lotes más antiguos primero (FIFO).
                var remaining = ev.Shares;
                var matchedShares = 0m;
                var matchedCost = 0m;
                var matchedLots = new List<TaxLotMatch>();
                foreach (var lot in lots.Where(l => l.Shares > 0).OrderBy(l => l.Date))
                {
                    if (remaining <= 0)
                    {
                        break;
                    }

                    var take = Math.Min(lot.Shares, remaining);
                    var cost = lot.Cost * (take / lot.Shares);
                    lot.Cost -= cost;
                    lot.Shares -= take;
                    matchedShares += take;
                    matchedCost += cost;
                    matchedLots.Add(new TaxLotMatch(lot.Date, take, lot.SharesBought, cost, lot.Order));
                    remaining -= take;
                }

                var fraction = ev.Shares == 0 ? 0m : matchedShares / ev.Shares;
                var proceeds = ev.Amount * fraction;
                var sale = new TaxSaleResult
                {
                    Key = ev.Key,
                    Isin = ev.Isin,
                    Label = ev.Label,
                    Date = ev.Date,
                    Shares = ev.Shares,
                    Proceeds = proceeds,
                    Cost = matchedCost,
                    Gain = proceeds - matchedCost,
                    UnmatchedShares = Math.Max(0m, remaining),
                    Lots = matchedLots,
                    WashMonths = months
                };
                allSales.Add(sale);

                if (sale.Gain < 0)
                {
                    var from = ev.Date.AddMonths(-months);
                    var to = ev.Date.AddMonths(months);
                    var usedOrders = matchedLots.Select(m => m.Order).ToHashSet();
                    foreach (var purchase in events.Where(p => !p.IsSale && p.Date >= from && p.Date <= to && !usedOrders.Contains(p.Order)))
                    {
                        sale.WindowPurchases.Add(new TaxPurchaseRef(purchase.Date, purchase.Shares, purchase.Amount));
                    }
                }

                if (sale.Gain < 0 && matchedShares > 0)
                {
                    var loss = -sale.Gain;
                    var windowStart = ev.Date.AddMonths(-months);
                    var windowEnd = ev.Date.AddMonths(months);

                    // Valores homogéneos comprados en los meses anteriores que siguen en cartera.
                    var heldBefore = lots.Where(l => l.Shares > 0 && l.Date >= windowStart).ToList();
                    var heldShares = heldBefore.Sum(l => l.Shares);
                    var blockedNow = Math.Min(matchedShares, heldShares);
                    if (blockedNow > 0)
                    {
                        var blockedLoss = loss * (blockedNow / matchedShares);
                        foreach (var lot in heldBefore)
                        {
                            lot.Cost += blockedLoss * (lot.Shares / heldShares);
                        }

                        sale.DeferredLoss += blockedLoss;
                    }

                    var pendingShares = matchedShares - blockedNow;
                    if (pendingShares > 0)
                    {
                        deferrals.Add(new Deferral
                        {
                            Sale = sale,
                            WindowEnd = windowEnd,
                            SharesPending = pendingShares,
                            LossPending = loss * (pendingShares / matchedShares)
                        });
                        sale.OpenWindowUntil = windowEnd;
                    }
                }
            }
        }

        return BuildResult(allSales, year, otherIncome, withholdings);
    }

    private static TaxResult BuildResult(List<TaxSaleResult> allSales, int year, decimal otherIncome, decimal withholdings)
    {
        var yearSales = allSales.Where(s => s.Date.Year == year).OrderBy(s => s.Label, StringComparer.OrdinalIgnoreCase).ThenBy(s => s.Date).ToList();
        var netByYear = allSales.GroupBy(s => s.Date.Year).ToDictionary(g => g.Key, g => g.Sum(s => s.Computable));

        // Pérdidas netas de los 4 ejercicios anteriores, compensables con ganancias netas posteriores (art. 49).
        var pool = new List<(int Origin, decimal Amount)>();
        for (var y = year - 4; y < year; y++)
        {
            var net = netByYear.GetValueOrDefault(y);
            if (net > 0)
            {
                var available = net;
                for (var i = 0; i < pool.Count && available > 0; i++)
                {
                    if (y - pool[i].Origin > 4 || pool[i].Amount <= 0)
                    {
                        continue;
                    }

                    var used = Math.Min(available, pool[i].Amount);
                    pool[i] = (pool[i].Origin, pool[i].Amount - used);
                    available -= used;
                }
            }
            else if (net < 0)
            {
                pool.Add((y, -net));
            }
        }

        var gains = yearSales.Where(s => s.Computable > 0).Sum(s => s.Computable);
        var losses = yearSales.Where(s => s.Computable < 0).Sum(s => s.Computable);
        var deferred = yearSales.Sum(s => s.Gain < 0 ? s.DeferredLoss : 0m);
        var netResult = gains + losses;

        var priorPending = pool.Where(p => year - p.Origin <= 4).Sum(p => p.Amount);
        var priorApplied = 0m;
        if (netResult > 0)
        {
            var available = netResult;
            for (var i = 0; i < pool.Count && available > 0; i++)
            {
                if (year - pool[i].Origin > 4 || pool[i].Amount <= 0)
                {
                    continue;
                }

                var used = Math.Min(available, pool[i].Amount);
                available -= used;
                priorApplied += used;
            }
        }

        var otherCompensation = 0m;
        var carryForward = 0m;
        decimal savingsBase;
        if (netResult < 0)
        {
            otherCompensation = Math.Min(-netResult, Math.Max(0m, otherIncome) * 0.25m);
            carryForward = -netResult - otherCompensation;
            savingsBase = Math.Max(0m, otherIncome - otherCompensation);
        }
        else
        {
            savingsBase = Math.Max(0m, netResult - priorApplied) + Math.Max(0m, otherIncome);
        }

        var brackets = new List<TaxBracketRow>();
        var tax = 0m;
        var marginalRate = 0m;
        foreach (var (from, to, rate) in SavingsScale)
        {
            var top = to ?? decimal.MaxValue;
            var inBracket = Math.Max(0m, Math.Min(savingsBase, top) - from);
            var bracketTax = inBracket * rate;
            tax += bracketTax;
            var reached = savingsBase > from;
            if (reached)
            {
                marginalRate = rate;
            }

            brackets.Add(new TaxBracketRow(from, to, rate, inBracket, bracketTax, reached, false));
        }

        var lastReached = brackets.FindLastIndex(b => b.IsReached);
        if (lastReached >= 0)
        {
            brackets[lastReached] = brackets[lastReached] with { IsMarginal = true };
        }

        tax = Math.Round(tax, 2);
        var today = DateTime.Today;

        return new TaxResult
        {
            Year = year,
            Sales = yearSales,
            Gains = gains,
            Losses = losses,
            DeferredLosses = deferred,
            NetResult = netResult,
            PriorLossesApplied = priorApplied,
            PriorLossesPending = priorPending - priorApplied,
            CarryForwardLoss = carryForward,
            OtherIncome = otherIncome,
            OtherIncomeCompensation = otherCompensation,
            SavingsBase = savingsBase,
            Brackets = brackets,
            Tax = tax,
            Withholdings = withholdings,
            ToPay = tax - withholdings,
            MarginalRate = marginalRate,
            AverageRate = savingsBase > 0 ? tax / savingsBase : 0m,
            UnmatchedShares = yearSales.Sum(s => s.UnmatchedShares),
            OpenWindowSales = yearSales.Where(s => s.Gain < 0 && s.OpenWindowUntil.HasValue && s.OpenWindowUntil.Value >= today).ToList()
        };
    }
}
