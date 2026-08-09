using System.Diagnostics.CodeAnalysis;

namespace MagicAssistant.Data.Model;

public class Location : IEquatable<Location>
{
    public static readonly Location NoWhere = new(0, string.Empty, false);

    private readonly int _id;
    private string? _legacyId;

    public Location(int? id, string name, bool isSideboard)
    {
        _id = id ?? 0;
        Id = _id > 0 ? _id.ToString() : string.Empty;
        Name = name;
        IsSideboard = isSideboard;
    }

    public string Id { get; private set; } = string.Empty;
    public int RawId => _id;
    public string Name { get; }
    public bool IsSideboard { get; }

    public bool IsEmpty => _id == 0 && string.IsNullOrEmpty(_legacyId);

    /// <summary>
    /// Creates a Location from an integer collection ID.
    /// </summary>
    public static Location FromId(int id, string name = "", bool isSideboard = false)
    {
        return new Location(id > 0 ? id : (int?)null, name, isSideboard);
    }

    /// <summary>
    /// Parses a legacy location string into a Location object.
    /// Mirrors Java Location.valueOf().
    /// </summary>
    public static Location ValueOf(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return NoWhere;

        // Check for sideboard suffix (legacy format)
        var isSide = value.EndsWith("_sb", StringComparison.Ordinal);
        var idStr = isSide ? value[..^3] : value;

        // If the value contains "/", use the last segment as name
        var slashIdx = idStr.LastIndexOf('/');
        var name = slashIdx >= 0 ? idStr[(slashIdx + 1)..] : idStr;

        // Try to parse as integer (new format)
        if (int.TryParse(idStr, out var intId))
            return new Location(intId, name, isSide);

        // Fallback for legacy string IDs
        return new(0, name, isSide);
    }

    public static Location FromPath(string path)
    {
        if (string.IsNullOrEmpty(path))
            return NoWhere;

        var isSide = path.EndsWith("_sb");
        var idStr = isSide ? path[..^3] : path;

        if (int.TryParse(idStr, out var intId))
            return new Location(intId, path, isSide);

        // Legacy string ID: create with placeholder, then set Id
        var loc = new Location((int?)null, path, isSide);
        loc.Id = path;  // Override empty Id with legacy path
        return loc;
    }

    public Location WithSideboard(bool sideboard) => new(_id, Name, sideboard);

    public override bool Equals([NotNullWhen(true)] object? obj) => Equals(obj as Location);

    public bool Equals(Location? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        // Compare by Id string (covers both int and legacy string IDs) + IsSideboard
        return Id == other.Id && IsSideboard == other.IsSideboard;
    }

    public override int GetHashCode() => HashCode.Combine(Id, IsSideboard);

    public override string ToString() => Id;
}
