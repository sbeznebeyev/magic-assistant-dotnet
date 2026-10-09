using MagicAssistant.Data.Model;

namespace MagicAssistant.Core.Analysis;

/// <summary>
/// Analyzes a collection of cards (deck) and produces aggregated statistics.
/// Single-pass computation — mirrors Java CardStoreUtils + aggr classes.
/// </summary>
public interface IAnalysisService
{
    /// <summary>
    /// Computes full deck analysis in one pass over the card list.
    /// Mirrors: buildManaCurveStats, buildSpellColorStats, buildTypeStats,
    ///          buildCreatureStats, buildAbilityStats, getMaxRepeats, getManaCost, getCount
    /// </summary>
    DeckAnalysisResult Analyze(IEnumerable<MyCard> cards);

    /// <summary>
    /// Quick mana curve only — for chart rendering.
    /// </summary>
    int[] ComputeManaCurve(IEnumerable<MyCard> cards);
}
