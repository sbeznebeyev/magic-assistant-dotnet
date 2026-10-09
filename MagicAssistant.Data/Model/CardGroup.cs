using MagicAssistant.Data.Contracts;

namespace MagicAssistant.Data.Model;

public class CardGroup : ICard, IEnumerable<IMagicCard>
{
    private readonly List<IMagicCard> _children = new();

    public CardGroup(MagicCardField groupByField, string? label = null)
    {
        GroupByField = groupByField;
        Label = label ?? groupByField.ToString();
    }

    public MagicCardField GroupByField { get; }
    public string Label { get; set; }

    public void Add(IMagicCard card) => _children.Add(card);

    public void AddRange(IEnumerable<IMagicCard> cards) => _children.AddRange(cards);

    public bool Remove(IMagicCard card) => _children.Remove(card);

    public IReadOnlyList<IMagicCard> Children => _children.AsReadOnly();
    public int Count => _children.Sum(c => c.Count);

    // ICard implementation - aggregates from children
    public string Name => Label;
    public int OwnCount => _children.Sum(c => c.OwnCount);
    public int OwnTotal => _children.Sum(c => c.OwnTotal);
    public int OwnTotalAll => _children.Sum(c => c.OwnTotalAll);
    public int OwnUnique => _children.Sum(c => c.OwnUnique);

    public Collection? Collection => null;
    public bool IsSideboard => false;
    public int UniqueCount => _children.Count;
    public bool IsPhysical => false;

    public IEnumerator<IMagicCard> GetEnumerator() => _children.GetEnumerator();
    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => _children.GetEnumerator();
}
