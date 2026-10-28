namespace CotizacionesBolsaWeb.Services;

public sealed class WorkIncomeResult
{
    public decimal CashIncome { get; init; }
    public decimal InKindValuation { get; init; }
    public decimal InKindPaymentOnAccount { get; init; }
    public decimal InKindPaymentCharged { get; init; }
    public decimal InKindIncome { get; init; }
    public decimal EmployerPensionContributions { get; init; }
    public decimal IncomeReductions { get; init; }
    public decimal GrossIncome { get; init; }
    public decimal SocialSecurity { get; init; }
    public decimal PreviousNet { get; init; }
    public decimal PreviousNetSum { get; init; }
    public decimal FixedExpenses { get; init; }
    public decimal UnionDues { get; init; }
    public decimal LegalDefense { get; init; }
    public decimal ExtraExpenses { get; init; }
    public decimal OtherExpenses { get; init; }
    public decimal NetBeforeReduction { get; init; }
    public decimal Reduction { get; init; }
    public decimal NetIncome { get; init; }
    public decimal Withholdings { get; init; }
    public decimal PersonalMinimum { get; init; }
}

public sealed record WorkIncomeOverrides
{
    public decimal? InKindIncome { get; init; }
    public decimal? GrossIncome { get; init; }
    public decimal? PreviousNet { get; init; }
    public decimal? PreviousNetSum { get; init; }
    public decimal? FixedExpenses { get; init; }
    public decimal? NetBeforeReduction { get; init; }
    public decimal? Reduction { get; init; }
    public decimal? NetIncome { get; init; }
}

public sealed record WorkIncomeInput(
    decimal CashIncome, decimal InKindValuation, decimal InKindPaymentOnAccount, decimal InKindPaymentCharged,
    decimal EmployerPensionContributions, decimal IncomeReductions, decimal SocialSecurity,
    decimal UnionDues, decimal LegalDefense, decimal ExtraExpenses, decimal Withholdings, decimal PersonalMinimum,
    WorkIncomeOverrides? Overrides = null);

/// <summary>
/// Cálculo orientativo del rendimiento neto del trabajo: gastos deducibles y reducción por obtención de
/// rendimientos del trabajo (art. 20 LIRPF). La liquidación del impuesto está en <see cref="IrpfCalculator"/>.
/// No sustituye a la declaración ni a un asesor fiscal.
/// </summary>
public static class WorkIncomeCalculator
{
    public const decimal FixedOtherExpenses = 2_000m;
    public const decimal LegalDefenseLimit = 300m;
    public const decimal DefaultPersonalMinimum = 5_550m;

    /// <summary>Reducción por obtención de rendimientos del trabajo (art. 20 LIRPF).</summary>
    public static decimal WorkReduction(decimal previousNet)
    {
        decimal reduction;
        if (previousNet <= 14_852m)
        {
            reduction = 7_302m;
        }
        else if (previousNet <= 17_673.52m)
        {
            reduction = 7_302m - 1.75m * (previousNet - 14_852m);
        }
        else if (previousNet <= 19_747.5m)
        {
            reduction = 2_364.34m - 1.14m * (previousNet - 17_673.52m);
        }
        else
        {
            reduction = 0m;
        }

        return Math.Round(Math.Min(Math.Max(0m, reduction), previousNet), 2);
    }

    public static WorkIncomeResult Compute(WorkIncomeInput input)
    {
        var ov = input.Overrides ?? new WorkIncomeOverrides();
        var cash = Math.Max(0m, input.CashIncome);
        var valuation = Math.Max(0m, input.InKindValuation);
        var onAccount = Math.Max(0m, input.InKindPaymentOnAccount);
        var charged = Math.Max(0m, input.InKindPaymentCharged);

        // Las filas calculadas pueden sobrescribirse a mano (ov); si no, se calculan con las fórmulas de la declaración.
        // Retribución en especie computable: valoración + ingresos a cuenta no repercutidos al trabajador.
        var inKind = ov.InKindIncome ?? valuation + Math.Max(0m, onAccount - charged);
        var contributions = Math.Max(0m, input.EmployerPensionContributions);
        var reductions = Math.Max(0m, input.IncomeReductions);

        var gross = ov.GrossIncome ?? Math.Max(0m, cash + inKind + contributions - reductions);
        var socialSecurity = Math.Max(0m, input.SocialSecurity);
        var previousNet = ov.PreviousNet ?? Math.Max(0m, gross - socialSecurity);
        var previousNetSum = ov.PreviousNetSum ?? previousNet;

        var fixedExpenses = ov.FixedExpenses ?? (previousNetSum > 0m ? FixedOtherExpenses : 0m);
        var union = Math.Max(0m, input.UnionDues);
        var legal = Math.Min(Math.Max(0m, input.LegalDefense), LegalDefenseLimit);
        var extra = Math.Max(0m, input.ExtraExpenses);
        var otherExpenses = fixedExpenses + union + legal + extra;

        var netBeforeReduction = ov.NetBeforeReduction ?? Math.Max(0m, previousNetSum - otherExpenses);
        var reduction = ov.Reduction ?? WorkReduction(netBeforeReduction);
        var net = ov.NetIncome ?? Math.Max(0m, netBeforeReduction - reduction);
        var minimum = Math.Max(0m, input.PersonalMinimum);

        return new WorkIncomeResult
        {
            CashIncome = cash,
            InKindValuation = valuation,
            InKindPaymentOnAccount = onAccount,
            InKindPaymentCharged = charged,
            InKindIncome = inKind,
            EmployerPensionContributions = contributions,
            IncomeReductions = reductions,
            GrossIncome = gross,
            SocialSecurity = socialSecurity,
            PreviousNet = previousNet,
            PreviousNetSum = previousNetSum,
            FixedExpenses = fixedExpenses,
            UnionDues = union,
            LegalDefense = legal,
            ExtraExpenses = extra,
            OtherExpenses = otherExpenses,
            NetBeforeReduction = netBeforeReduction,
            Reduction = reduction,
            NetIncome = net,
            PersonalMinimum = minimum,
            Withholdings = Math.Max(0m, input.Withholdings)
        };
    }
}
