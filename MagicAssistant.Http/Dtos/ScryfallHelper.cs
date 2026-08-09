namespace MagicAssistant.Http.Dtos;

public static class ScryfallHelper
{
    public static string GetLegality(Dictionary<string, string>? legalities, ScryfallLegalitiesEnum scryfallLegalities)
    {
        var legality = scryfallLegalities.ToString();

        if (legalities is null || legalities.Count == 0) return string.Empty;
        return string.Join(",", legalities.Where(l => l.Value == legality).Select(l => l.Key));
    }
}
