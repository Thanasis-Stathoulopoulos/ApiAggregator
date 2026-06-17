using System.Collections.Concurrent;
using ApiAggregator.Api.Infrastructure;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;

namespace ApiAggregator.Tests.Infrastructure
{
    public class CacheServiceTests
    {
        // ── Helpers ───────────────────────────────────────────────────────────────

        private static CacheService BuildSut()
        {
            var memoryCache = new MemoryCache(new MemoryCacheOptions());
            return new CacheService(memoryCache, NullLogger<CacheService>.Instance);
        }

        // ── Happy path ────────────────────────────────────────────────────────────

        [Fact]
        public async Task GetOrCreateAsync_CallsFactory_OnCacheMiss()
        {
            var sut = BuildSut();
            var factoryCalled = 0;

            var result = await sut.GetOrCreateAsync("key1", () =>
            {
                factoryCalled++;
                return Task.FromResult("hello");
            });

            Assert.Equal("hello", result);
            Assert.Equal(1, factoryCalled);
        }

        [Fact]
        public async Task GetOrCreateAsync_ReturnsCachedValue_OnCacheHit()
        {
            var sut = BuildSut();
            var factoryCalled = 0;

            // Prime the cache
            await sut.GetOrCreateAsync("key1", () =>
            {
                factoryCalled++;
                return Task.FromResult("hello");
            });

            // Second call should hit the cache
            var result = await sut.GetOrCreateAsync("key1", () =>
            {
                factoryCalled++;
                return Task.FromResult("should-not-be-returned");
            });

            Assert.Equal("hello", result);
            Assert.Equal(1, factoryCalled); // factory invoked exactly once
        }

        [Fact]
        public async Task GetOrCreateAsync_UsesTtl_WhenProvided()
        {
            var sut = BuildSut();
            var factoryCalled = 0;

            // Set with 1ms TTL so it expires immediately
            await sut.GetOrCreateAsync("key1", () =>
            {
                factoryCalled++;
                return Task.FromResult("v1");
            }, TimeSpan.FromMilliseconds(1));

            await Task.Delay(20); // wait for entry to expire

            // Should miss and call factory again
            await sut.GetOrCreateAsync("key1", () =>
            {
                factoryCalled++;
                return Task.FromResult("v2");
            }, TimeSpan.FromSeconds(60));

            Assert.Equal(2, factoryCalled);
        }

        [Fact]
        public async Task Remove_EvictsEntry_SoNextCallInvokesFactory()
        {
            var sut = BuildSut();
            var factoryCalled = 0;

            await sut.GetOrCreateAsync("key1", () =>
            {
                factoryCalled++;
                return Task.FromResult("v1");
            });

            sut.Remove("key1");

            await sut.GetOrCreateAsync("key1", () =>
            {
                factoryCalled++;
                return Task.FromResult("v2");
            });

            Assert.Equal(2, factoryCalled);
        }

        // ── Cache stampede protection ─────────────────────────────────────────────

        /// <summary>
        /// Under concurrent load, the factory must be invoked exactly once even when
        /// many callers simultaneously encounter a cache miss for the same key.
        /// </summary>
        [Fact]
        public async Task GetOrCreateAsync_UnderConcurrentLoad_CallsFactoryOnlyOnce()
        {
            var sut = BuildSut();
            var factoryCalled = 0;

            // Simulate a slow factory (50ms) to increase the window for stampede
            async Task<string> SlowFactory()
            {
                Interlocked.Increment(ref factoryCalled);
                await Task.Delay(50);
                return "computed-value";
            }

            const int concurrency = 20;
            var tasks = Enumerable.Range(0, concurrency)
                .Select(_ => sut.GetOrCreateAsync("stampede-key", SlowFactory));

            var results = await Task.WhenAll(tasks);

            // Every caller must receive the correct value
            Assert.All(results, r => Assert.Equal("computed-value", r));

            // The factory must have been invoked exactly once — that's the stampede fix
            Assert.Equal(1, factoryCalled);
        }

        /// <summary>
        /// Different keys must not block each other — their semaphores are independent.
        /// </summary>
        [Fact]
        public async Task GetOrCreateAsync_DifferentKeys_AreIndependent()
        {
            var sut = BuildSut();
            var factoryCalls = new ConcurrentDictionary<string, int>();

            async Task<string> SlowFactory(string key)
            {
                factoryCalls.AddOrUpdate(key, 1, (_, v) => v + 1);
                await Task.Delay(20);
                return $"value-{key}";
            }

            var tasks = new[]
            {
                sut.GetOrCreateAsync("key-a", () => SlowFactory("key-a")),
                sut.GetOrCreateAsync("key-b", () => SlowFactory("key-b")),
                sut.GetOrCreateAsync("key-a", () => SlowFactory("key-a")),
                sut.GetOrCreateAsync("key-b", () => SlowFactory("key-b")),
            };

            var results = await Task.WhenAll(tasks);

            Assert.Contains("value-key-a", results);
            Assert.Contains("value-key-b", results);

            // Each key's factory should have been called exactly once
            Assert.Equal(1, factoryCalls["key-a"]);
            Assert.Equal(1, factoryCalls["key-b"]);
        }
    }
}
