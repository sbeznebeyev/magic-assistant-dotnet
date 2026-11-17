using MagicAssistant.Data.Model.Legality;

namespace MagicAssistant.Data.Model;

/// <summary>
/// One card's legality projection from a node — the only two columns the legality page reads.
/// Returned by ICollectionStore.GetDeckLegalityAsync instead of materializing full cards.
/// </summary>
public sealed record DeckLegalityEntry(string Name, LegalityMap Legality);
