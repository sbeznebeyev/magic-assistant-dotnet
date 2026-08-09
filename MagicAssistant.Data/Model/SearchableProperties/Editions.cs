using MagicAssistant.Data.Contracts;

namespace MagicAssistant.Data.Model.SearchableProperties;

/// <summary>
/// Searchable edition catalog for filter grouping.
/// </summary>
public class Editions : ISearchableProperty
{
    private static readonly Lazy<Editions> _instance = new(() => new Editions());
    public static Editions Instance => _instance.Value;

    private CardSet[] _editions = Array.Empty<CardSet>();

    public string GetFilterField() => MagicCardField.Set.ToString();
    public IEnumerable<string> GetIds() => _editions.Select(e => e.SetCode);
    public string GetNameById(string id) => _editions.FirstOrDefault(e => e.SetCode == id)?.Name ?? id;

    /// <summary>
    /// Loads or reloads the edition catalog. Call this after KB sync to refresh the list.
    /// </summary>
    public void Reload(IEnumerable<CardSet> editions) => _editions = editions.ToArray();

    [Obsolete("Use Reload() instead. Kept for backward compatibility.")]
    public void Load(IEnumerable<CardSet> editions) => Reload(editions);
}
