namespace MagicAssistant.Data.Model;

/// <summary>
/// Set type as stored in the CardSet table (TEXT) and returned by the Scryfall API.
/// Member names match the wire strings byte-for-byte, so <see cref="Enum.ToString"/> /
/// <see cref="System.Enum.Parse{TEnum}"/> round-trip without a mapping table.
/// </summary>
public enum CardSetType
{
    /// <summary>A yearly Magic core set (Tenth Edition, etc)</summary>
    core,

    /// <summary>A rotational expansion set in a block (Zendikar, etc)</summary>
    expansion,

    /// <summary>A reprint set that contains no new cards (Modern Masters, etc)</summary>
    masters,

    /// <summary>A set of new cards that only get added to high-power formats</summary>
    eternal,

    /// <summary>An Arena set designed for Alchemy</summary>
    alchemy,

    /// <summary>Masterpiece Series premium foil cards</summary>
    masterpiece,

    /// <summary>A Commander-oriented gift set</summary>
    arsenal,

    /// <summary>From the Vault gift sets</summary>
    from_the_vault,

    /// <summary>Spellbook series gift sets</summary>
    spellbook,

    /// <summary>Premium Deck Series decks</summary>
    premium_deck,

    /// <summary>Duel Decks</summary>
    duel_deck,

    /// <summary>Special draft sets, like Conspiracy and Battlebond</summary>
    draft_innovation,

    /// <summary>Magic Online treasure chest prize sets</summary>
    treasure_chest,

    /// <summary>Commander preconstructed decks</summary>
    commander,

    /// <summary>Planechase sets</summary>
    planechase,

    /// <summary>Archenemy sets</summary>
    archenemy,

    /// <summary>Vanguard card sets</summary>
    vanguard,

    /// <summary>A funny un-set or set with funny promos (Unglued, Happy Holidays, etc)</summary>
    funny,

    /// <summary>A starter/introductory set (Portal, etc)</summary>
    starter,

    /// <summary>A gift box set</summary>
    box,

    /// <summary>A set that contains purely promotional cards</summary>
    promo,

    /// <summary>A set made up of tokens and emblems.</summary>
    token,

    /// <summary>A set made up of gold-bordered, oversize, or trophy cards that are not legal</summary>
    memorabilia,

    /// <summary>A set that contains minigame card inserts from booster packs</summary>
    minigame,

    /// <summary>Fallback for values that don't map to a known type. Never produced by the Scryfall sync path.</summary>
    unknown
}
