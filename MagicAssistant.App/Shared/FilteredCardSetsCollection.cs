using MagicAssistant.Data.Contracts;
using MagicAssistant.Data.Model;

namespace MagicAssistant.App.Shared;

/// <summary>
/// Lazy DB-backed view of the set list, scoped by <see cref="FilterViewModel.FilteredSetCodes"/>.
/// Empty scope = all sets in the knowledge base; a non-empty scope returns only those sets.
/// Data is loaded on demand: the first <see cref="EnsureLoadedAsync"/>() (or any later call whose
/// scope composition changed) queries the database; calls with an unchanged scope return the cache.
/// <see cref="Invalidate"/> drops the cache after KB mutations (e.g. a sync added new sets).
/// </summary>
public sealed class FilteredCardSetsCollection
{
    private readonly FilterViewModel _viewModel;
    private readonly IDbCardStore _store;

    private List<CardSet> _cached = new();
    private string? _loadedScopeKey; // null = not loaded yet

    public FilteredCardSetsCollection(FilterViewModel viewModel, IDbCardStore store)
    {
        _viewModel = viewModel;
        _store = store;
    }

    /// <summary>Stable key of the current scope composition (order-insensitive).</summary>
    private static string ScopeKey(IEnumerable<string> codes) =>
        !codes.Any() ? string.Empty : string.Join('\u0001', codes.OrderBy(c => c, StringComparer.OrdinalIgnoreCase));

    /// <summary>Returns the sets for the current scope, loading from the database when needed.</summary>
    public async Task<IReadOnlyList<CardSet>> EnsureLoadedAsync(CancellationToken ct = default)
    {
        var key = ScopeKey(_viewModel.FilteredSetCodes);
        if (_loadedScopeKey == key)
            return _cached;

        var codes = _viewModel.FilteredSetCodes;
        ICollection<CardSet> sets = codes.Count == 0
            ? await _store.GetCardSetsAsync(ct)
            : await _store.GetCardSetsByCodesAsync(codes, ct);

        _cached = sets.ToList();
        _loadedScopeKey = key;
        return _cached;
    }

    /// <summary>Drops the cache so the next <see cref="EnsureLoadedAsync"/> re-queries the database.</summary>
    public void Invalidate() => _loadedScopeKey = null;
}
