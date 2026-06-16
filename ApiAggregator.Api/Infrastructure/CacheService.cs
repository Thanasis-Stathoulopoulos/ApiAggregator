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

        public CacheService(IMemoryCache memoryCache, ILogger<CacheService> logger)
        {
            _memoryCache = memoryCache;
            _logger = logger;
        }

        public async Task<T?> GetOrCreateAsync<T>(string key, Func<Task<T>> factory, TimeSpan? expiration = null)
        {
            if (_memoryCache.TryGetValue(key, out T? cachedValue))
            {
                _logger.LogInformation("Cache hit for key: {Key}", key);
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

        public void Remove(string key)
        {
            _memoryCache.Remove(key);
            _logger.LogInformation("Evicted cache for key: {Key}", key);
        }
    }
}
