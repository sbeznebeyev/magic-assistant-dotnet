using MagicAssistant.Data.Model;
using MagicAssistant.Data.Model.Enums;
using MagicAssistant.Data.Model.Legality;
using MagicAssistant.Http.Dtos;
using System.Globalization;

namespace MagicAssistant.Core;

public static class ScryfallMapper
{
    private static readonly Dictionary<string, Format> FormatMap = new()
    {
        ["standard"] = Format.Standard,
        ["future_standard"] = Format.FutureStandard,
        ["pioneer"] = Format.Pioneer,
        ["modern"] = Format.Modern,
        ["legacy"] = Format.Legacy,
        ["vintage"] = Format.Vintage,
        ["commander"] = Format.Commander,
        ["brawl"] = Format.Brawl,
        ["historic"] = Format.Historic,
        ["timeless"] = Format.Timeless,
        ["duelist"] = Format.Duelist,
        ["pauper"] = Format.Pauper,
    };

    public static Card ToMagicCard(ScryfallCardDto dto)
    {
        var collectorNumberId = ParseCollectorNumberId(dto.CollectorNumber);

        var card = new Card
        {
            Id = dto.Id,
            OracleId = dto.OracleId,
            Name = dto.Name,
            ManaCost = dto.ManaCost,
            Layout = ParseLayout(dto.Layout),
            Type = dto.TypeLine,
            OracleText = dto.OracleText,
            Power = dto.Power,
            PowerNumber = ParseIntOrNull(dto.Power),
            Toughness = dto.Toughness,
            ToughnessNumber = ParseIntOrNull(dto.Toughness),
            SetCode = dto.Set?.ToLowerInvariant(), // Abbreviation always lowercase
            Rarity = dto.Rarity,
            Artist = dto.Artist,
            Language = dto.Language,
            CollectorNumber = dto.CollectorNumber,
            CmcText = dto.Cmc,
            CmcNumber = double.TryParse(dto.Cmc, CultureInfo.InvariantCulture, out var parsedCmcNum) ? (int?)Math.Floor(parsedCmcNum) : null,
            Side = dto.Side ?? 0,
            CollectorNumberId = collectorNumberId,
            Legal = ScryfallHelper.GetLegality(dto.Legalities, ScryfallLegalitiesEnum.legal),
            NotLegal = ScryfallHelper.GetLegality(dto.Legalities, ScryfallLegalitiesEnum.not_legal),
            Restricted = ScryfallHelper.GetLegality(dto.Legalities, ScryfallLegalitiesEnum.restricted),
            Banned = ScryfallHelper.GetLegality(dto.Legalities, ScryfallLegalitiesEnum.banned),
            PrintedName = dto.PrintedName,
            PrintedText = dto.PrintedText,
            PrintedTypeLine = dto.PrintedTypeLine,
        };

        // Map Colors from Scryfall (the card's actual colors)
        if (dto.Colors is { Count: > 0 })
            card.Colors = dto.Colors.ToArray();

        // For double-faced cards, ColorBits/IdentityBits live on CardFaces, not the parent.
        // Parent card has null bits (like ManaCost is null for DFCs).
        bool isDoubleFaced = dto.Layout != null && IsDoubleFacedLayout(dto.Layout);

        if (!isDoubleFaced)
        {
            // Compute ColorBits from Scryfall colors array + mana cost
            card.ColorBits = ColorBitBuilder.Build(dto.Colors, dto.ManaCost);

            // Derive ColorType from ColorBits — single source of truth
            card.ColorType = ColorTypes.GetColorType(card.ColorBits.Value);

            // Compute IdentityBits from Scryfall color_identity + mana cost + oracle text
            card.IdentityBits = ColorBitBuilder.Build(null, dto.ManaCost, dto.ColorIdentity);
        }
        else
        {
            // DFC: parent has no bits — each face computes its own
            card.ColorBits = null;
            card.IdentityBits = null;
            card.ColorType = null;
        }

        // Map ColorIdentity from Scryfall
        if (dto.ColorIdentity is { Count: > 0 })
            card.ColorIdentity = dto.ColorIdentity.ToArray();

        // Map ColorIndicator from Scryfall
        if (dto.ColorIndicator is { Count: > 0 })
            card.ColorIndicator = dto.ColorIndicator.ToArray();

        card.GameChanger = dto.GameChanger;

        if (dto.Keywords is { Count: > 0 })
            card.Keywords = dto.Keywords.ToArray();

        if (dto.ProducedMana is { Count: > 0 })
            card.ProducedMana = dto.ProducedMana.ToArray();

        // Map ImageUris directly from Scryfall DTO.
        if (dto.ImageUris != null)
        {
            card.ImageUris = new ImageUris(
                dto.ImageUris.Small,
                dto.ImageUris.Normal,
                dto.ImageUris.Large,
                dto.ImageUris.Png);
        }

        // Map multifaced card faces (transform, mdfc, split, flip, reversible)
        if (dto.CardFaces is { Count: > 0 })
        {
            card.CardFaces = MapCardFacesToArray(dto.CardFaces);
        }

        // Map related cards (all_parts) — tokens, meld parts, combo pieces
        if (dto.AllParts is { Count: > 0 })
        {
            card.AllParts = MapAllPartsToArray(dto.AllParts);
        }

        card.LegalityMap = MapLegality(dto.Legalities);

        // Map prices from Scryfall
        if (dto.Prices != null)
        {
            card.Usd = dto.Prices.Usd;
            card.UsdFoil = dto.Prices.UsdFoil;
        }

        return card;
    }

    public static CardSet ToEdition(ScryfallSetDto dto)
    {
        DateTime? releaseDate = null;
        if (!string.IsNullOrEmpty(dto.ReleasedAt) && DateTime.TryParse(dto.ReleasedAt, out var parsed))
            releaseDate = parsed;

        return new CardSet(
            setCode: dto.Code?.ToLowerInvariant() ?? "???",
            name: dto.Name,
            id: dto.Id,
            uri: dto.Uri,
            scryfallUri: dto.ScryfallUri,
            searchUri: dto.SearchUri,
            setType: CardSetTypes.Parse(dto.SetType),
            cardCount: dto.CardCount,
            parentSetCode: dto.ParentSetCode,
            digital: dto.Digital,
            nonfoilOnly: dto.NonfoilOnly,
            foilOnly: dto.FoilOnly,
            iconSvgUri: dto.IconSvgUri,
            releaseDate: releaseDate);
    }

    /// <summary>
    /// Parses a string value as integer. Returns null if the value is not a valid integer.
    /// Used for Power/Toughness fields that may contain special values like "*", "half", "loose".
    /// </summary>
    private static int? ParseIntOrNull(string? value)
    {
        if (string.IsNullOrEmpty(value)) return null;
        if (int.TryParse(value, out var result)) return result;
        return null;
    }

    /// <summary>
    /// Parses the layout string from Scryfall into LayoutEnum.
    /// See https://scryfall.com/docs/api/layouts for all layout codes.
    /// </summary>
    private static LayoutEnum ParseLayout(string? layout)
    {
        if (string.IsNullOrEmpty(layout))
            return LayoutEnum.normal;

        // Scryfall uses underscore-separated names (e.g. "modal_dfc", "double_faced_token")
        // C# enum uses snake_case without underscores in names.
        if (Enum.TryParse<LayoutEnum>(layout, true, out var parsed))
            return parsed;

        return LayoutEnum.normal;
    }

    /// <summary>
    /// Checks if the layout is a double-faced card where faces have independent colors.
    /// For these layouts, ColorBits/IdentityBits live on CardFaces, not the parent card.
    /// </summary>
    private static bool IsDoubleFacedLayout(string layout)
        => layout is "transform" or "modal_dfc" or "meld" or "reversible_card" or "double_faced_token";

    /// <summary>
    /// Parses collector number from Scryfall format: "1", "1/234", "1a", "A5", "42b".
    /// </summary>
    private static int ParseCollectorNumberId(string? collectorNumber)
    {
        if (string.IsNullOrEmpty(collectorNumber))
            return 0;

        // Remove side suffix (e.g. "42a" → "42")
        var cleaned = collectorNumber.TrimEnd('a', 'b', 'c', 'd', 'e', 'f', 'g', 'h', 'i', 'j', 'k', 'l', 'm', 'n', 'o', 'p', 'q', 'r', 's', 't', 'u', 'v', 'w', 'x', 'y', 'z');

        // Handle formats like "1/234" — take the first number
        var slashIdx = cleaned.IndexOf('/');
        if (slashIdx > 0)
            cleaned = cleaned[..slashIdx];

        // Remove leading letters (e.g. "A5" → "5")
        var numStart = 0;
        while (numStart < cleaned.Length && char.IsLetter(cleaned[numStart]))
            numStart++;

        if (numStart >= cleaned.Length)
            return 0;

        if (int.TryParse(cleaned[numStart..], out var result))
            return result;

        return 0;
    }

    private static LegalityMap MapLegality(Dictionary<string, string>? legalities)
    {
        if (legalities == null || !legalities.Any())
            return LegalityMap.Empty;

        var map = new LegalityMap();

        foreach (var (key, value) in legalities)
        {
            if (value.Equals("legal", StringComparison.OrdinalIgnoreCase) &&
                FormatMap.TryGetValue(key, out var format))
            {
                map.SetLegal(format);
            }
        }

        return map;
    }

    private static RelatedCard[] MapAllPartsToArray(ICollection<RelatedCardDto> parts)
    {
        var result = new List<RelatedCard>(parts.Count);

        foreach (var part in parts)
        {
            var mapped = new RelatedCard
            {
                Id = part.Id,
                ObjectType = part.Object,
                Component = part.Component,
                Name = part.Name,
                TypeLine = part.TypeLine,
                Uri = part.Uri,
            };

            result.Add(mapped);
        }

        return result.ToArray();
    }

    private static CardFace[] MapCardFacesToArray(ICollection<Http.Dtos.CardFaceDto> faces)
    {
        var result = new List<CardFace>(faces.Count);

        foreach (var face in faces)
        {
            // Compute ColorBits for this face from its colors + mana cost
            int faceColorBits = ColorBitBuilder.Build(face.Colors, face.ManaCost);
            // For faces, identity = colors (no separate oracle text per face)
            int faceIdentityBits = ColorBitBuilder.Build(face.Colors, face.ManaCost);

            var mapped = new CardFace
            {
                Name = face.Name,
                ManaCost = face.ManaCost,
                TypeLine = face.TypeLine,
                OracleText = face.OracleText,
                Power = face.Power,
                PowerNumber = ParseIntOrNull(face.Power),
                Toughness = face.Toughness,
                ToughnessNumber = ParseIntOrNull(face.Toughness),
                CmcText = face.Cmc,
                CmcNumber = double.TryParse(face.Cmc, CultureInfo.InvariantCulture, out var parsedFaceCmcNum) ? (int?)Math.Floor(parsedFaceCmcNum) : null,
                Colors = face.Colors,
                ColorIndicator = face.ColorIndicator,
                Loyalty = face.Loyalty,
                Artist = face.Artist,
                IllustrationId = face.IllustrationId,
                // Map ImageUris directly from Scryfall DTO.
                ImageUris = face.ImageUris != null
                    ? new ImageUris(face.ImageUris.Small, face.ImageUris.Normal, face.ImageUris.Large, face.ImageUris.Png)
                    : null,
                FlavorText = face.FlavorText,
                PrintedName = face.PrintedName,
                PrintedText = face.PrintedText,
                PrintedTypeLine = face.PrintedTypeLine,
                ColorBits = faceColorBits,
                IdentityBits = faceIdentityBits,
            };

            result.Add(mapped);
        }

        return result.ToArray();
    }
}
