using System.Collections.Concurrent;
using MagicAssistant.Core;
using MagicAssistant.Core.Commands;
using MagicAssistant.Data.Model;

namespace MagicAssistant.App.Services;

public interface ICardImageLoader
{
    bool FrontExists(Card card);
    bool BackExists(Card card);
    bool Failed(Card card);
    Task EnsureAsync(Card card);
}

/// <summary>
/// Ленивая загрузка изображений карт: проверяет наличие файла на диске и при отсутствии
/// скачивает его через DownloadCardImagesCommand (URLs уже в БД, без обращения к Scryfall API).
/// Картинку одной карты одновременно качает только один вызов: конкуренты сериализуются
/// через ConcurrentDictionary&lt;Guid, SemaphoreSlim&gt; по Guid карты. Первый захватывает семафор
/// и скачивает; остальные ждут разблокировки, войдя в секцию — сначала проверяют наличие файла:
/// если предыдущий конкурент успел скачать — выход без повторного скачивания.
/// </summary>
public class CardImageLoader : ICardImageLoader
{
    private readonly DownloadCardImagesCommandHandler _download;
    private readonly IImageCacheService _imageCache;
    private readonly ConcurrentDictionary<Guid, SemaphoreSlim> _locks = new();
    private readonly HashSet<Guid> _failed = new();
    private readonly object _failedLock = new();

    public CardImageLoader(DownloadCardImagesCommandHandler download, IImageCacheService imageCache)
    {
        _download = download ?? throw new ArgumentNullException(nameof(download));
        _imageCache = imageCache ?? throw new ArgumentNullException(nameof(imageCache));
    }

    public bool FrontExists(Card card) => _imageCache.FrontImageExists(card);
    public bool BackExists(Card card) => _imageCache.BackImageExists(card);

    public bool Failed(Card card)
    {
        if (card is null) return false;
        lock (_failedLock) return _failed.Contains(card.Id);
    }

    public async Task EnsureAsync(Card card)
    {
        if (card is null) return;
        var id = card.Id;
        if (id == Guid.Empty) return;

        // One semaphore per card, kept for the loader's lifetime: every concurrent request
        // for the same card waits on the SAME instance, so each Release matches an acquire —
        // no SemaphoreFullException, no double download.
        var locker = Acquire(id);
        await locker.WaitAsync().ConfigureAwait(false);
        try
        {
            // Предыдущий конкурент уже скачал — выход без повторного скачивания.
            if (_imageCache.FrontImageExists(card)) return;

            bool failed;
            lock (_failedLock) failed = _failed.Contains(id);
            if (failed) return;

            try
            {
                var result = await _download.Handle(new DownloadCardImagesCommand(id)).ConfigureAwait(false);
                if (!result.CardFound || !result.ImagesDownloaded)
                    lock (_failedLock) _failed.Add(id);
            }
            catch
            {
                // Unexpected error (disk/network): mark as failed so the UI shows
                // "Image not downloaded" instead of an eternal spinner. Memo resets on app restart.
                lock (_failedLock) _failed.Add(id);
            }
        }
        finally
        {
            locker.Release();
        }
    }

    private SemaphoreSlim Acquire(Guid id) => _locks.GetOrAdd(id, _ => new SemaphoreSlim(1, 1));
}
