namespace CotizacionesBolsaWeb.Services;

public sealed record IrpfExtraInput(
    decimal OtherReductions,
    decimal WorkerPensionContributions,
    decimal RegionalMinimum,
    decimal DonationsBase,
    bool DonationsRecurrent,
    decimal OtherStateDeductions,
    decimal RegionalDeductions);

/// <summary>Un tramo de una escala del impuesto con la parte de la base que cae en él.</summary>
public sealed record IrpfBracketRow(decimal From, decimal? To, decimal Rate, decimal AmountInBracket, decimal Tax, bool IsMarginal);

/// <summary>Liquidación orientativa del IRPF siguiendo el orden y las casillas de la declaración (modelo 100).</summary>
public sealed class IrpfResult
{
    // Tramos de las escalas aplicadas a las bases liquidables (antes de restar la cuota del mínimo personal y familiar).
    public List<IrpfBracketRow> StateGeneralBrackets { get; init; } = new();
    public List<IrpfBracketRow> RegionalGeneralBrackets { get; init; } = new();
    public List<IrpfBracketRow> SavingsBrackets { get; init; } = new();

    // Integración y compensación de rentas / bases imponibles.
    public decimal GainsSum { get; init; }
    public decimal PriorLossesApplied { get; init; }
    public decimal NetGainsIntegrated { get; init; }
    public decimal CapitalIncomeIntegrated { get; init; }
    public decimal GeneralBase { get; init; }
    public decimal SavingsBase { get; init; }

    // Reducciones por aportaciones y contribuciones a sistemas de previsión social.
    public decimal WorkerContributions { get; init; }
    public decimal EmployerContributions { get; init; }
    public decimal PensionRightToReduction { get; init; }
    public decimal OtherReductions { get; init; }
    public decimal TotalReductions { get; init; }
    public decimal ReductionOnGeneral { get; init; }
    public decimal ReductionOnSavings { get; init; }
    public decimal UnusedReductions { get; init; }

    // Bases liquidables.
    public decimal GeneralLiquidBase { get; init; }
    public decimal SavingsLiquidBase { get; init; }

    // Adecuación del impuesto a las circunstancias personales y familiares.
    public decimal StateMinimum { get; init; }
    public decimal RegionalMinimum { get; init; }
    public decimal StateMinimumOnGeneral { get; init; }
    public decimal StateMinimumOnSavings { get; init; }
    public decimal RegionalMinimumOnGeneral { get; init; }
    public decimal RegionalMinimumOnSavings { get; init; }

    // Cálculo del impuesto: base general.
    public decimal StateGeneralTaxed { get; init; }
    public decimal RegionalGeneralTaxed { get; init; }
    public decimal StateGeneralOnMinimum { get; init; }
    public decimal RegionalGeneralOnMinimum { get; init; }
    public decimal StateGeneralTax { get; init; }
    public decimal RegionalGeneralTax { get; init; }
    public decimal StateGeneralAverageRate { get; init; }
    public decimal RegionalGeneralAverageRate { get; init; }

    // Cálculo del impuesto: base del ahorro.
    public decimal StateSavingsTaxed { get; init; }
    public decimal RegionalSavingsTaxed { get; init; }
    public decimal StateSavingsOnMinimum { get; init; }
    public decimal RegionalSavingsOnMinimum { get; init; }
    public decimal StateSavingsTax { get; init; }
    public decimal RegionalSavingsTax { get; init; }
    public decimal StateSavingsAverageRate { get; init; }
    public decimal RegionalSavingsAverageRate { get; init; }

    public decimal StateGrossTax { get; init; }
    public decimal RegionalGrossTax { get; init; }
    public decimal TotalGrossTax { get; init; }

    // Deducciones.
    public decimal DonationsBase { get; init; }
    public decimal DonationsDeduction { get; init; }
    public decimal DonationsStatePart { get; init; }
    public decimal DonationsRegionalPart { get; init; }
    public decimal OtherStateDeductions { get; init; }
    public decimal RegionalDeductions { get; init; }
    public decimal StateDeductionsApplied { get; init; }
    public decimal RegionalDeductionsApplied { get; init; }

    // Cuotas líquidas y resultado.
    public decimal StateNetTax { get; init; }
    public decimal RegionalNetTax { get; init; }
    public decimal TotalNetTax { get; init; }
    public decimal SavingsWithholdings { get; init; }
    public decimal WorkWithholdings { get; init; }
    public decimal TotalWithholdings { get; init; }
    public decimal ToPay { get; init; }
}

/// <summary>
/// Cálculo orientativo de la liquidación del IRPF: bases imponibles, reducciones, bases liquidables, mínimo personal y
/// familiar, escalas general estatal y autonómica (Comunidad de Madrid) y del ahorro (mitad estatal, mitad autonómica),
/// deducciones por donativos y resultado. No sustituye a la declaración ni a un asesor fiscal.
/// </summary>
public static class IrpfCalculator
{
    public const decimal PensionReductionLimit = 10_000m;
    public const decimal DefaultRegionalMinimum = 5_956.65m;

    private static readonly (decimal From, decimal? To, decimal Rate)[] StateGeneralScale =
    {
        (0m, 12_450m, 0.095m),
        (12_450m, 20_200m, 0.12m),
        (20_200m, 35_200m, 0.15m),
        (35_200m, 60_000m, 0.185m),
        (60_000m, 300_000m, 0.225m),
        (300_000m, null, 0.245m)
    };

    // Escala autonómica de la Comunidad de Madrid.
    private static readonly (decimal From, decimal? To, decimal Rate)[] RegionalGeneralScale =
    {
        (0m, 13_362.22m, 0.085m),
        (13_362.22m, 19_004.63m, 0.107m),
        (19_004.63m, 35_425.68m, 0.128m),
        (35_425.68m, 57_320.40m, 0.174m),
        (57_320.40m, null, 0.205m)
    };

    private static List<IrpfBracketRow> BuildBrackets(IEnumerable<(decimal From, decimal? To, decimal Rate)> scale, decimal amount)
    {
        var rows = new List<IrpfBracketRow>();
        foreach (var (from, to, rate) in scale)
        {
            var inBracket = Math.Max(0m, Math.Min(amount, to ?? decimal.MaxValue) - from);
            rows.Add(new IrpfBracketRow(from, to, rate, inBracket, Math.Round(inBracket * rate, 2), false));
        }

        var last = rows.FindLastIndex(r => r.AmountInBracket > 0m);
        if (last >= 0)
        {
            rows[last] = rows[last] with { IsMarginal = true };
        }

        return rows;
    }

    private static decimal ApplyScale((decimal From, decimal? To, decimal Rate)[] scale, decimal amount)
    {
        var tax = 0m;
        foreach (var (from, to, rate) in scale)
        {
            tax += Math.Max(0m, Math.Min(amount, to ?? decimal.MaxValue) - from) * rate;
        }

        return Math.Round(tax, 2);
    }

    // Con las escalas del ahorro actuales el tramo estatal y el autonómico son iguales (mitad cada uno).
    private static decimal SavingsHalfTax(decimal amount) => Math.Round(TaxCalculator.SavingsTaxOn(amount) / 2m, 2);

    // La declaración trunca (no redondea) el tipo medio a dos decimales.
    private static decimal AverageRate(decimal tax, decimal liquidBase) =>
        liquidBase > 0m ? Math.Floor(tax / liquidBase * 10_000m) / 100m : 0m;

    public static IrpfResult Compute(TaxResult savings, WorkIncomeResult work, IrpfExtraInput extra)
    {
        var generalBase = work.NetIncome;
        var savingsBase = savings.SavingsBase;

        // Integración del ahorro: saldo neto de ganancias/pérdidas patrimoniales y rendimientos del capital mobiliario.
        var netGains = savings.NetResult > 0m ? Math.Max(0m, savings.NetResult - savings.PriorLossesApplied) : 0m;
        var capitalIncome = savingsBase - netGains;

        // Reducciones por aportaciones y contribuciones a sistemas de previsión social (máx. 10.000 € y 30 % del rendimiento neto).
        var workerContributions = Math.Max(0m, extra.WorkerPensionContributions);
        var employerContributions = work.EmployerPensionContributions;
        var rightToReduction = Math.Round(
            Math.Min(Math.Min(workerContributions + employerContributions, PensionReductionLimit), work.NetBeforeReduction * 0.30m), 2);
        var otherReductions = Math.Max(0m, extra.OtherReductions);
        var totalReductions = rightToReduction + otherReductions;
        var reductionOnGeneral = Math.Min(totalReductions, generalBase);
        var reductionOnSavings = Math.Min(totalReductions - reductionOnGeneral, savingsBase);
        var unused = totalReductions - reductionOnGeneral - reductionOnSavings;

        var generalLiquid = generalBase - reductionOnGeneral;
        var savingsLiquid = savingsBase - reductionOnSavings;

        // Mínimo personal y familiar (estatal y autonómico): primero contra la base general; el resto contra la del ahorro.
        var stateMinimum = work.PersonalMinimum;
        var regionalMinimum = Math.Max(0m, extra.RegionalMinimum);
        var stateMinOnGeneral = Math.Min(stateMinimum, generalLiquid);
        var stateMinOnSavings = Math.Min(stateMinimum - stateMinOnGeneral, savingsLiquid);
        var regionalMinOnGeneral = Math.Min(regionalMinimum, generalLiquid);
        var regionalMinOnSavings = Math.Min(regionalMinimum - regionalMinOnGeneral, savingsLiquid);

        // Base general.
        var stateGeneralTaxed = ApplyScale(StateGeneralScale, generalLiquid);
        var regionalGeneralTaxed = ApplyScale(RegionalGeneralScale, generalLiquid);
        var stateGeneralOnMin = ApplyScale(StateGeneralScale, stateMinOnGeneral);
        var regionalGeneralOnMin = ApplyScale(RegionalGeneralScale, regionalMinOnGeneral);
        var stateGeneralTax = Math.Max(0m, stateGeneralTaxed - stateGeneralOnMin);
        var regionalGeneralTax = Math.Max(0m, regionalGeneralTaxed - regionalGeneralOnMin);

        // Base del ahorro.
        var savingsTaxed = SavingsHalfTax(savingsLiquid);
        var stateSavingsOnMin = Math.Round(savingsTaxed - SavingsHalfTax(savingsLiquid - stateMinOnSavings), 2);
        var regionalSavingsOnMin = Math.Round(savingsTaxed - SavingsHalfTax(savingsLiquid - regionalMinOnSavings), 2);
        var stateSavingsTax = Math.Max(0m, savingsTaxed - stateSavingsOnMin);
        var regionalSavingsTax = Math.Max(0m, savingsTaxed - regionalSavingsOnMin);

        var stateGross = stateGeneralTax + stateSavingsTax;
        var regionalGross = regionalGeneralTax + regionalSavingsTax;

        // Deducciones por donativos (Ley 49/2002): 80 % de los primeros 250 €, 40 % del resto (45 % si son recurrentes),
        // con el límite del 10 % de la base liquidable. La deducción se reparte mitad estatal, mitad autonómica.
        var donationsBase = Math.Max(0m, extra.DonationsBase);
        var restRate = extra.DonationsRecurrent ? 0.45m : 0.40m;
        var donationsRaw = Math.Min(donationsBase, 250m) * 0.80m + Math.Max(0m, donationsBase - 250m) * restRate;
        var donationsLimit = Math.Max(0m, (generalLiquid + savingsLiquid) * 0.10m);
        var donationsDeduction = Math.Round(Math.Min(donationsRaw, donationsLimit), 2);
        var donationsState = Math.Round(donationsDeduction / 2m, 2);
        var donationsRegional = donationsDeduction - donationsState;

        var otherStateDeductions = Math.Max(0m, extra.OtherStateDeductions);
        var regionalDeductions = Math.Max(0m, extra.RegionalDeductions);
        var stateDeductionsApplied = Math.Min(donationsState + otherStateDeductions, stateGross);
        var regionalDeductionsApplied = Math.Min(donationsRegional + regionalDeductions, regionalGross);

        var stateNet = stateGross - stateDeductionsApplied;
        var regionalNet = regionalGross - regionalDeductionsApplied;
        var totalNet = stateNet + regionalNet;
        var withholdings = savings.Withholdings + work.Withholdings;

        return new IrpfResult
        {
            StateGeneralBrackets = BuildBrackets(StateGeneralScale, generalLiquid),
            RegionalGeneralBrackets = BuildBrackets(RegionalGeneralScale, generalLiquid),
            SavingsBrackets = BuildBrackets(TaxCalculator.SavingsScaleRows, savingsLiquid),
            GainsSum = savings.NetResult,
            PriorLossesApplied = savings.PriorLossesApplied,
            NetGainsIntegrated = netGains,
            CapitalIncomeIntegrated = capitalIncome,
            GeneralBase = generalBase,
            SavingsBase = savingsBase,
            WorkerContributions = workerContributions,
            EmployerContributions = employerContributions,
            PensionRightToReduction = rightToReduction,
            OtherReductions = otherReductions,
            TotalReductions = totalReductions,
            ReductionOnGeneral = reductionOnGeneral,
            ReductionOnSavings = reductionOnSavings,
            UnusedReductions = unused,
            GeneralLiquidBase = generalLiquid,
            SavingsLiquidBase = savingsLiquid,
            StateMinimum = stateMinimum,
            RegionalMinimum = regionalMinimum,
            StateMinimumOnGeneral = stateMinOnGeneral,
            StateMinimumOnSavings = stateMinOnSavings,
            RegionalMinimumOnGeneral = regionalMinOnGeneral,
            RegionalMinimumOnSavings = regionalMinOnSavings,
            StateGeneralTaxed = stateGeneralTaxed,
            RegionalGeneralTaxed = regionalGeneralTaxed,
            StateGeneralOnMinimum = stateGeneralOnMin,
            RegionalGeneralOnMinimum = regionalGeneralOnMin,
            StateGeneralTax = stateGeneralTax,
            RegionalGeneralTax = regionalGeneralTax,
            StateGeneralAverageRate = AverageRate(stateGeneralTax, generalLiquid),
            RegionalGeneralAverageRate = AverageRate(regionalGeneralTax, generalLiquid),
            StateSavingsTaxed = savingsTaxed,
            RegionalSavingsTaxed = savingsTaxed,
            StateSavingsOnMinimum = stateSavingsOnMin,
            RegionalSavingsOnMinimum = regionalSavingsOnMin,
            StateSavingsTax = stateSavingsTax,
            RegionalSavingsTax = regionalSavingsTax,
            StateSavingsAverageRate = AverageRate(stateSavingsTax, savingsLiquid),
            RegionalSavingsAverageRate = AverageRate(regionalSavingsTax, savingsLiquid),
            StateGrossTax = stateGross,
            RegionalGrossTax = regionalGross,
            TotalGrossTax = stateGross + regionalGross,
            DonationsBase = donationsBase,
            DonationsDeduction = donationsDeduction,
            DonationsStatePart = donationsState,
            DonationsRegionalPart = donationsRegional,
            OtherStateDeductions = otherStateDeductions,
            RegionalDeductions = regionalDeductions,
            StateDeductionsApplied = stateDeductionsApplied,
            RegionalDeductionsApplied = regionalDeductionsApplied,
            StateNetTax = stateNet,
            RegionalNetTax = regionalNet,
            TotalNetTax = totalNet,
            SavingsWithholdings = savings.Withholdings,
            WorkWithholdings = work.Withholdings,
            TotalWithholdings = withholdings,
            ToPay = totalNet - withholdings
        };
    }
}
