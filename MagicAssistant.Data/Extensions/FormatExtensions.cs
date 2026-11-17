using MagicAssistant.Data.Model.Legality;

namespace MagicAssistant.Data.Model;

public static class FormatExtensions
{
    public static string DisplayName(this Format format) => format switch
    {
        Format.Standard => "Standard",
        Format.FutureStandard => "Future Standard",
        Format.Pioneer => "Pioneer",
        Format.Modern => "Modern",
        Format.Legacy => "Legacy",
        Format.Vintage => "Vintage",
        Format.Commander => "Commander",
        Format.Brawl => "Brawl",
        Format.Historic => "Historic",
        Format.Timeless => "Timeless",
        Format.Duelist => "Duelist",
        Format.Pauper => "Pauper",
        Format.PennyDreadful => "Penny Dreadful",
        _ => format.ToString(),
    };

    public static ICollection<Format> AllFormats() => (ICollection<Format>)Enum.GetValues(typeof(Format));

    /// <summary>
    /// Converts a Format enum value to its Scryfall API key (lowercase with underscores).
    /// Used for matching against the Legal column in the Cards table.
    /// </summary>
    public static string ToScryfallKey(this Format format) => format switch
    {
        Format.Standard => "standard",
        Format.FutureStandard => "future_standard",
        Format.Pioneer => "pioneer",
        Format.Modern => "modern",
        Format.Legacy => "legacy",
        Format.Vintage => "vintage",
        Format.Commander => "commander",
        Format.Brawl => "brawl",
        Format.Historic => "historic",
        Format.Timeless => "timeless",
        Format.Duelist => "duelist",
        Format.Pauper => "pauper",
        Format.PennyDreadful => "penny_dreadful",
        _ => format.ToString().ToLowerInvariant()
    };
}
