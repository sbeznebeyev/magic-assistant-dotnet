using System.Diagnostics.CodeAnalysis;

namespace MagicAssistant.Data.Model.Legality;

public class LegalityMap : IEquatable<LegalityMap>
{
    public static readonly LegalityMap Empty = new(0);

    private readonly HashSet<Format> _formats = new();

    public LegalityMap() { }

    private LegalityMap(int dummy) // for Empty singleton
    { }

    public bool IsLegal(Format format) => _formats.Contains(format);

    public void SetLegal(Format format, bool legal)
    {
        if (legal)
            _formats.Add(format);
        else
            _formats.Remove(format);
    }

    public void SetLegal(Format format) => _formats.Add(format);

    public bool IsEmpty => !_formats.Any();

    public ICollection<Format> LegalFormats => _formats.ToList().AsReadOnly();

    public static LegalityMap CreateFromLegal(params Format[] formats)
    {
        var map = new LegalityMap();
        foreach (var format in formats)
            map._formats.Add(format);
        return map;
    }

    /// <summary>
    /// Creates a LegalityMap from a comma-separated string of format names.
    /// Mirrors Java LegalityMap.createFromLegal(String).
    /// </summary>
    public static LegalityMap CreateFromLegal(string formats)
    {
        var map = new LegalityMap();
        foreach (var fmt in formats.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (Enum.TryParse<Format>(fmt, true, out var format))
                map._formats.Add(format);
        }
        return map;
    }

    public static LegalityMap Merge(LegalityMap a, LegalityMap b)
    {
        var merged = new LegalityMap();
        foreach (var f in a._formats)
            merged._formats.Add(f);
        foreach (var f in b._formats)
            merged._formats.Add(f);
        return merged;
    }

    public static LegalityMap Intersect(LegalityMap a, LegalityMap b)
    {
        var result = new LegalityMap();
        foreach (var f in a._formats)
            if (b._formats.Contains(f))
                result._formats.Add(f);
        return result;
    }

    public override bool Equals([NotNullWhen(true)] object? obj) => Equals(obj as LegalityMap);

    public bool Equals(LegalityMap? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return _formats.SetEquals(other._formats);
    }

    public override int GetHashCode() => HashCode.Combine(_formats.Count, _formats.OrderBy(f => f).Aggregate(0, (h, f) => h ^ (int)f));

    public override string ToString() => IsEmpty ? "(empty)" : string.Join(", ", LegalFormats.Select(f => f.DisplayName()));
}
