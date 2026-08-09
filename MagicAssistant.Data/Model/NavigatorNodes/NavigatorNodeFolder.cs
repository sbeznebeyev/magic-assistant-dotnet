namespace MagicAssistant.Data.Model.NavigatorNodes;

public class NavigatorNodeFolder: NavigatorNodeBase
{
    public ICollection<NavigatorNodeBase>? Children { get; set; }

    public override NodeType NodeType => NodeType.Folder;
}
