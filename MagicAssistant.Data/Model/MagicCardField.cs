using MagicAssistant.Data.Contracts;

namespace MagicAssistant.Data.Model;

public enum MagicCardField
{
    // === Base card fields (non-physical) ===
    Id,
    Name,
    ManaCost,
    Layout,
    Type,
    Power,
    Toughness,
    Set,
    Rarity,
    OracleText,
    Artist,
    Language,
    CollectorNumber,
    Cmc,
    ColorType,
    Text,
    Side,
    Edition,

    // === Extended base fields (stored as properties) ===
    Rulings,
    SetBlock,
    SetCore,
    SetRelease,
    Sideboard,
    UniqueCount,
    Size,
    Legality,
    Colors,
    ColorIdentity,
    EnglishType,

    // === Physical card fields ===
    Count,
    IsOwn,
    Location,
    Comment,
    DateAdded,
    MyPrice,
    ForTradeCount,

    // Appended after ForTradeCount so existing numeric values stay stable;
    // the IsPhysical() range must be extended accordingly.
    Grade,

    // === Derived/computed fields ===
    OwnCount,
    CreatureCount,
    Count4,
    PercentComplete,
    Percent4Complete,
    Error,
}