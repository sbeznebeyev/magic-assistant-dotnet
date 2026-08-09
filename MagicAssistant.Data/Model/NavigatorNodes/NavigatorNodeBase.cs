namespace MagicAssistant.Data.Model.NavigatorNodes;

public abstract class NavigatorNodeBase
{
    /// <summary>
    /// Node ID (Collection.Id)
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Node name (Collection.Name)
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Node type
    /// </summary>
    public abstract NodeType NodeType { get; }

    /// <summary>
    /// Total cards number
    /// </summary>
    public int CardCount { get; set; }

    /// <summary>
    /// Is locked for edit and delete
    /// </summary>
    public bool Locked { get; set; }
}
