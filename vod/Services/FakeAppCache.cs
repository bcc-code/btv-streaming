using LazyCache;
using Microsoft.Extensions.Caching.Memory;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace VODFunctions.Services
{
    public class FakeAppCache : IAppCache
    {
        public ICacheProvider CacheProvider => null;

        public CacheDefaults DefaultCachePolicy => null;

        public void Add<T>(string key, T item, MemoryCacheEntryOptions policy)
        {
            return;
        }

        public T Get<T>(string key)
        {
            return default;
        }

        public Task<T> GetAsync<T>(string key)
        {
            return default;
        }

        public T GetOrAdd<T>(string key, Func<ICacheEntry, T> addItemFactory)
        {
            return addItemFactory(default);
        }

        public T GetOrAdd<T>(string key, Func<ICacheEntry, T> addItemFactory, MemoryCacheEntryOptions policy)
        {
            return addItemFactory(default);
        }

        public Task<T> GetOrAddAsync<T>(string key, Func<ICacheEntry, Task<T>> addItemFactory)
        {
            return addItemFactory(default);
        }

        public Task<T> GetOrAddAsync<T>(string key, Func<ICacheEntry, Task<T>> addItemFactory, MemoryCacheEntryOptions policy)
        {
            return addItemFactory(default);
        }

        public void Remove(string key)
        {
            return;
        }

        public bool TryGetValue<T>(string key, out object value)
        {
            value = default;
            return default;
        }
    }
}
