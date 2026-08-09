using MagicAssistant.Core.Analysis;
using MagicAssistant.Data.Contracts;

namespace MagicAssistant.Core.Queries;

/// <summary>
/// Query for full deck analysis — mana curve, colors, types, creatures, abilities, stats.
/// Mirrors Java analysis pages: ManaCurvePage, SpellColourPage, CreaturePage, TypePage, AbilityPage, InfoPage.
/// </summary>
public record GetDeckAnalysisQuery(int DeckId);

public class GetDeckAnalysisQueryHandler
{
    private readonly ICollectionStore _collectionStore;
    private readonly IAnalysisService _analysisService;

    public GetDeckAnalysisQueryHandler(
        ICollectionStore collectionStore,
        IAnalysisService analysisService)
    {
        _collectionStore = collectionStore;
        _analysisService = analysisService;
    }

    public async Task<DeckAnalysisResult> Handle(GetDeckAnalysisQuery query, CancellationToken ct = default)
    {
        var deckCards = await _collectionStore.GetDeckWithSideboardAsync(query.DeckId, ct);
        return _analysisService.Analyze(deckCards);
    }
}
