using MagicAssistant.Data.Model.Enums;

namespace MagicAssistant.Data.Extensions;

public static class CardTypeExtensions
{
    public static CardTypeEnum FromString(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return CardTypeEnum.Instant;

        var lower = value.ToLowerInvariant();
        return lower switch
        {
            "creature" => CardTypeEnum.Creature,
            "instant" => CardTypeEnum.Instant,
            "sorcery" => CardTypeEnum.Sorcery,
            "enchantment" => CardTypeEnum.Enchantment,
            "artifact" => CardTypeEnum.Artifact,
            "planeswalker" or "legendary planeswalker" => CardTypeEnum.Planeswalker,
            "land" => CardTypeEnum.Land,
            "tribal" => CardTypeEnum.Tribal,
            "battle" => CardTypeEnum.Battle,
            "phenomenon" => CardTypeEnum.Phenomenon,
            _ => CardTypeEnum.Instant,
        };
    }

    public static bool HasType(this string? fullType, CardTypeEnum type)
    {
        if (string.IsNullOrEmpty(fullType))
            return false;

        var typeName = type switch
        {
            CardTypeEnum.Creature => "creature",
            CardTypeEnum.Instant => "instant",
            CardTypeEnum.Sorcery => "sorcery",
            CardTypeEnum.Enchantment => "enchantment",
            CardTypeEnum.Artifact => "artifact",
            CardTypeEnum.Planeswalker => "planeswalker",
            CardTypeEnum.Land => "land",
            CardTypeEnum.Tribal => "tribal",
            CardTypeEnum.Battle => "battle",
            CardTypeEnum.Phenomenon => "phenomenon",
            _ => "",
        };

        return fullType.IndexOf(typeName, StringComparison.OrdinalIgnoreCase) >= 0;
    }
}
