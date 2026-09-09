namespace CotizacionesBolsaWeb.Models;

public sealed record EtfHolding(
    string Name,
    string Symbol,
    string Isin,
    string? CountryCode,
    decimal PositionCount,
    decimal UnitPurchasePrice,
    string Broker);
