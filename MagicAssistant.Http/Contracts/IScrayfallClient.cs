using MagicAssistant.Http.Dtos;

namespace MagicAssistant.Http.Contracts;

public interface IScrayfallClient
{
    Task<ICollection<ScryfallSetDto>> GetSetsAsync(CancellationToken ct = default);
    /// <param name="onPageDownloaded">Optional callback invoked after each page is downloaded with the cumulative card count.</param>
    Task<ICollection<ScryfallCardDto>> GetCardsAsync(string setCode, bool englishOnly, Action<int>? onPageDownloaded = null, CancellationToken ct = default);
    Task<ScryfallCardDto?> GetCardByIdAsync(Guid cardId, CancellationToken ct = default);
    Task<ICollection<ScryfallCardDto>> SearchAsync(string query, int limit = 100, CancellationToken ct = default);
}
