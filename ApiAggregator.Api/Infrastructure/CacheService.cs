using System.Collections.Concurrent;
using Microsoft.Extensions.Caching.Memory;

namespace ApiAggregator.Api.Infrastructure
{
    public interface ICacheService
    {
        Task<T?> GetOrCreateAsync<T>(string key, Func<Task<T>> factory, TimeSpan? expiration = null);
        void Remove(string key);
    }

    public class CacheService : ICacheService
    {
        private readonly IMemoryCache _memoryCache;
        private readonly ILogger<CacheService> _logger;

        // Per-key semaphores ensure only one thread calls the factory for a given key at a time,
        // preventing cache stampede (thundering herd) under concurrent load.
        private readonly ConcurrentDictionary<string, SemaphoreSlim> _keyLocks = new();

        public CacheService(IMemoryCache memoryCache, ILogger<CacheService> logger)
        {
            _memoryCache = memoryCache;
            _logger = logger;
        }

        public async Task<T?> GetOrCreateAsync<T>(string key, Func<Task<T>> factory, TimeSpan? expiration = null)
        {
            // Fast path: lock-free check — avoids semaphore overhead on cache hits.
            if (_memoryCache.TryGetValue(key, out T? cachedValue))
            {
                _logger.LogInformation("Cache hit for key: {Key}", key);
                return cachedValue;
            }

            // Acquire the per-key semaphore so only one concurrent caller invokes the factory.
            var semaphore = _keyLocks.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));
            await semaphore.WaitAsync();
            try
            {
                // Double-check: another thread may have populated the cache while we waited.
                if (_memoryCache.TryGetValue(key, out cachedValue))
                {
                    _logger.LogInformation("Cache hit (after lock) for key: {Key}", key);
                    return cachedValue;
                }

                _logger.LogInformation("Cache miss for key: {Key}. Fetching data...", key);
                T value = await factory();

                if (value != null)
                {
                    var cacheOptions = new MemoryCacheEntryOptions();
                    if (expiration.HasValue)
                    {
                        cacheOptions.AbsoluteExpirationRelativeToNow = expiration.Value;
                    }
                    else
                    {
                        cacheOptions.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(2); // default
                    }

                    _memoryCache.Set(key, value, cacheOptions);
                }

                return value;
            }
            finally
            {
                semaphore.Release();
            }
        }

        public void Remove(string key)
        {
            _memoryCache.Remove(key);
            _logger.LogInformation("Evicted cache for key: {Key}", key);
        }
    }
}
