using System.Text.Json.Serialization;

namespace MagicAssistant.Http.Dtos;

/// <summary>
/// An object containing daily price information for this card, including usd, usd_foil, usd_etched, eur, eur_foil, eur_etched, and tix prices, as strings. 
/// </summary>
public class ScryfallPricesDto
{
    [property: JsonPropertyName("usd")]
    public string? Usd {  get; set; }

    [property: JsonPropertyName("usd_foil")]
    public string? UsdFoil { get; set; }
    
    [property: JsonPropertyName("usd_etched")]
    public string? UsdEtched { get; set; }
    
    [property: JsonPropertyName("eur")]
    public string? Eur { get; set; }

    [property: JsonPropertyName("eur_foil")]
    public string? EurFoil {  get; set; }

    [property: JsonPropertyName("eur_etched")]
    public string? EurEtched { get; set; }

    [property: JsonPropertyName("tix")]
    public string? Tix { get; set; }
}
