using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace MagicAssistant.Data.Caches;

public class CollectionCache(IOptions<MemoryCacheOptions> optionsAccessor) : MemoryCache(optionsAccessor);