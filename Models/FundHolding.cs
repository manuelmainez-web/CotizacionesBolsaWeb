namespace CotizacionesBolsaWeb.Models;

public sealed record FundHolding(
    string Name,
    string Symbol,
    string Isin,
    string? CountryCode,
    decimal PositionCount,
    decimal UnitPurchasePrice,
    string Broker);
