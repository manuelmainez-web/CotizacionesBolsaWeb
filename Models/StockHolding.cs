namespace CotizacionesBolsaWeb.Models;

public sealed record StockHolding(
    string Name,
    string Symbol,
    string Isin,
    string? CountryCode,
    decimal PositionCount,
    decimal UnitPurchasePrice,
    string Broker);
