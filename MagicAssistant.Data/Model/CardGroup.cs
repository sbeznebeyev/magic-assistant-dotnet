using MagicAssistant.Data.Contracts;

namespace MagicAssistant.Data.Model;

public class CardGroup : ICardSimple, IEnumerable<ICard>
{
    private readonly List<ICard> _children = new();

    public CardGroup(MagicCardField groupByField, string? label = null)
    {
        GroupByField = groupByField;
        Label = label ?? groupByField.ToString();
    }

    public MagicCardField GroupByField { get; }
    public string Label { get; set; }

    public void Add(ICard card) => _children.Add(card);

    public void AddRange(IEnumerable<ICard> cards) => _children.AddRange(cards);

    public bool Remove(ICard card) => _children.Remove(card);

    public IReadOnlyList<ICard> Children => _children.AsReadOnly();
    public int Count => _children.Sum(c => c.Count);

    // ICard implementation - aggregates from children
    public string Name => Label;

    public Collection? Collection => null;
    public bool IsSideboard => false;
    public bool IsPhysical => false;

    public IEnumerator<ICard> GetEnumerator() => _children.GetEnumerator();
    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => _children.GetEnumerator();
}
