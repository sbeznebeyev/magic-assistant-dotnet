using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace MagicAssistant.Data.Caches;

public class CardSetsCache(IOptions<MemoryCacheOptions> optionsAccessor) : MemoryCache(optionsAccessor);
public class AllCardSetsCache(IOptions<MemoryCacheOptions> optionsAccessor) : MemoryCache(optionsAccessor);