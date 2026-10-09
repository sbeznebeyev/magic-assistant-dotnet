using System.Diagnostics.CodeAnalysis;

namespace MagicAssistant.Data.Model;

public class Location : IEquatable<Location>
{
    public static readonly Location NoWhere = new(0, string.Empty, false);

    public Location(int id, string name, bool isSideboard)
    {
        Id = id;
        Name = name;
        IsSideboard = isSideboard;
    }

    public int Id { get; } 
    public string Name { get; }
    public bool IsSideboard { get; }

    /// <summary>
    /// Creates a Location from an integer collection ID.
    /// </summary>
    public static Location FromId(int id, string name = "", bool isSideboard = false) => new Location(id, name, isSideboard);
    public static Location FromCollection(Collection col) => new(col.Id, col.Name, col.NodeType == NavigatorNodes.NodeType.Sideboard);

    public bool Equals(Location? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return Id == other.Id;
    }
}
