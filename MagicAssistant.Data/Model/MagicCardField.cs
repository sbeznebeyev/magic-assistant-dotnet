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
    DbPrice,
    CommunityRating,
    EnglishName,
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
    Price,
    Comment,
    Special,
    DateAdded,
    Custom,
    ForTradeCount,

    // === Derived/computed fields ===
    OwnCount,
    OwnTotal,
    OwnTotalAll,
    OwnUnique,
    CreatureCount,
    Count4,
    PercentComplete,
    Percent4Complete,
    Error,
}