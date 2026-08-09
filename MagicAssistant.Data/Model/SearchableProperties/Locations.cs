using MagicAssistant.Data.Contracts;

namespace MagicAssistant.Data.Model.SearchableProperties;

/// <summary>
/// Searchable location catalog for filter grouping.
/// </summary>
public class Locations : ISearchableProperty
{
    private static readonly Lazy<Locations> _instance = new(() => new Locations());
    public static Locations Instance => _instance.Value;

    public string GetFilterField() => MagicCardField.Location.ToString();

    public IEnumerable<string> GetIds() => ["library", "deck", "sideboard"];

    public string GetNameById(string id)
    {
        return id switch
        {
            "library" => "Library",
            "deck" => "Deck",
            "sideboard" => "Sideboard",
            _ => id
        };
    }
}
