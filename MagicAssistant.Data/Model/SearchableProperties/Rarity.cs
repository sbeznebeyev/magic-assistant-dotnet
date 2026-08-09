using MagicAssistant.Data.Contracts;

namespace MagicAssistant.Data.Model.SearchableProperties;

/// <summary>
/// Searchable rarity catalog for filter grouping.
/// </summary>
public class Rarity : ISearchableProperty
{
    private static readonly Lazy<Rarity> _instance = new(() => new Rarity());
    public static Rarity Instance => _instance.Value;

    private readonly Dictionary<string, string> _rarities = new()
    {
        ["common"] = "Common",
        ["uncommon"] = "Uncommon",
        ["rare"] = "Rare",
        ["mythic"] = "Mythic Rare",
        ["special"] = "Special"
    };

    public string GetFilterField() => MagicCardField.Rarity.ToString();
    public IEnumerable<string> GetIds() => _rarities.Keys;
    public string GetNameById(string id) => _rarities[id];
}
