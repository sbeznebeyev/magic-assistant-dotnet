using MagicAssistant.Data.Model.Enums;

namespace MagicAssistant.Data.Extensions;

public static class RarityExtensions
{
    public static RarityEnum FromString(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return RarityEnum.Common;

        return value.ToLowerInvariant() switch
        {
            "common" or "c" => RarityEnum.Common,
            "uncommon" or "u" => RarityEnum.Uncommon,
            "rare" or "r" => RarityEnum.Rare,
            "mythic" or "mythic rare" or "m" => RarityEnum.Mythic,
            "special" or "s" => RarityEnum.Special,
            _ => RarityEnum.Common,
        };
    }

    public static string? Abbreviation(this RarityEnum rarity) => rarity switch
    {
        RarityEnum.Common => "C",
        RarityEnum.Uncommon => "U",
        RarityEnum.Rare => "R",
        RarityEnum.Mythic => "M",
        RarityEnum.Special => "S",
        _ => null,
    };
}
