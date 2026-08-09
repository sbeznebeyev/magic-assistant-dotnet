using MagicAssistant.Data.Contracts;

namespace MagicAssistant.Data.Model.SearchableProperties;

/// <summary>
/// Searchable color type catalog for filter grouping.
/// </summary>
public class ColorTypesSearch : ISearchableProperty
{
    private static readonly Lazy<ColorTypesSearch> _instance = new(() => new ColorTypesSearch());
    public static ColorTypesSearch Instance => _instance.Value;

    private readonly Dictionary<string, string> _colors = new()
    {
        ["MONO-W"] = "White",
        ["MONO-U"] = "Blue",
        ["MONO-B"] = "Black",
        ["MONO-R"] = "Red",
        ["MONO-G"] = "Green",
        ["MULTI"] = "Multicolor",
        ["HYBRID"] = "Hybrid",
        ["C"] = "Colorless"
    };

    public string GetFilterField() => MagicCardField.ColorType.ToString();
    public IEnumerable<string> GetIds() => _colors.Keys;
    public string GetNameById(string id) => _colors[id];
}
