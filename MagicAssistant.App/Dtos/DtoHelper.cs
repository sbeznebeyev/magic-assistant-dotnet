using MagicAssistant.Data.Model;
using MagicAssistant.Http.Dtos;

namespace MagicAssistant.App.Dtos;

public static class DtoHelper
{
    private static DateTime ParseReleasedAt(string? releasedAt)
    {
        if (string.IsNullOrEmpty(releasedAt))
            return DateTime.MinValue;

        if (DateTime.TryParse(releasedAt, out var parsed))
            return parsed;

        return DateTime.MinValue;
    }

    public static CardSetDto ToCardSetDto(ScryfallSetDto scryfallSetDto) => new CardSetDto(
            scryfallSetDto.Id,
            scryfallSetDto.Code,
            scryfallSetDto.Name,
            scryfallSetDto.Uri,
            scryfallSetDto.ScryfallUri,
            scryfallSetDto.SearchUri,
            ParseReleasedAt(scryfallSetDto.ReleasedAt),
            scryfallSetDto.SetType,
            scryfallSetDto.CardCount,
            scryfallSetDto.ParentSetCode,
            scryfallSetDto.Digital,
            scryfallSetDto.NonfoilOnly,
            scryfallSetDto.FoilOnly,
            scryfallSetDto.IconSvgUri
        );

    public static CardSetDto ToCardSetDto(CardSet cardSet) => new CardSetDto(
            cardSet.Id,
            cardSet.SetCode,
            cardSet.Name,
            cardSet.Uri,
            cardSet.ScryfallUri,
            cardSet.SearchUri,
            cardSet.ReleaseDate ?? DateTime.MinValue,
            cardSet.SetType,
            cardSet.CardCount,
            cardSet.ParentSetCode,
            cardSet.Digital,
            cardSet.NonfoilOnly,
            cardSet.FoilOnly,
            cardSet.IconSvgUri
        );

}
