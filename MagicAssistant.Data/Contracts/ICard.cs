using MagicAssistant.Data.Model;
using MagicAssistant.Data.Model.Enums;

namespace MagicAssistant.Data.Contracts;

public interface ICard : ICardSimple
{
    Guid Id { get; }
    string? ManaCost { get; }
    LayoutEnum Layout { get; }
    string? Type { get; }
    string? Power { get; }
    string? Toughness { get; }
    string? SetCode { get; }
    string? Rarity { get; }
    string? OracleText { get; }
    string? Artist { get; }
    string? Language { get; }
    int? CmcNumber { get; }
    string? CmcText { get; }
    string? ColorType { get; }
    Card GetBase();
    string? Text { get; }
    int Side { get; }
    int CollectorNumberId { get; }
    Model.Legality.LegalityMap LegalityMap { get; }
    bool IsBasicLand();
    CardSet? CardSet { get; }
}
