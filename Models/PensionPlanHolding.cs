namespace CotizacionesBolsaWeb.Models;

public sealed record PensionPlanHolding(string Name, decimal Participaciones, decimal ValorLiquidativo, decimal Rentabilidad12Meses, string Broker, decimal RentabilidadUltimoMes = 0m, decimal RentabilidadDesdeInicio = 0m);
