namespace CotizacionesBolsaWeb.Models;

public sealed record CheckingAccountHolding(string Name, decimal Balance, string CountryCode = "ES")
{
    public string FlagUrl => CountryCode switch
    {
        "ES" => "https://flagcdn.com/w40/es.png",
        "DE" => "https://flagcdn.com/w40/de.png",
        "EU" => "https://flagcdn.com/w40/eu.png",
        "US" => "https://flagcdn.com/w40/us.png",
        "FR" => "https://flagcdn.com/w40/fr.png",
        "JP" => "https://flagcdn.com/w40/jp.png",
        "IT" => "https://flagcdn.com/w40/it.png",
        "CH" => "https://flagcdn.com/w40/ch.png",
        "LU" => "https://flagcdn.com/w40/lu.png",
        _ => "https://flagcdn.com/w40/gb.png"
    };
}
