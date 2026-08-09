namespace MagicAssistant.Data.Model.NavigatorNodes;

public class NavigatorNodeDeck: NavigatorNodeBase
{
    /// <summary>
    /// Sideboard of the deck
    /// </summary>
    public NavigatorNodeSideboard? Sideboard { get; set; }

    public override NodeType NodeType => NodeType.Deck;
}
