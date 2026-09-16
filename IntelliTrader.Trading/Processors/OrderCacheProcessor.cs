using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using IntelliTrader.Core;

namespace IntelliTrader.Trading.Processors
{
    public interface IOrderCacheProcessor
    {
        IEnumerable<IOrderDetails> GetOrAdd(string pair, Func<string, IEnumerable<IOrderDetails>> fetcher, TimeSpan? customTtl = null);
        void Invalidate(string pair);
        void Clear();
        int CachedPairsCount { get; }
    }

    public class OrderCacheProcessor : IOrderCacheProcessor
    {
        private class CacheEntry
        {
            public List<IOrderDetails> Orders { get; }
            public DateTime ExpireAt { get; }

            public CacheEntry(List<IOrderDetails> orders, DateTime expireAt)
            {
                Orders = orders;
                ExpireAt = expireAt;
            }
        }

        private readonly ConcurrentDictionary<string, CacheEntry> _cache = new ConcurrentDictionary<string, CacheEntry>(StringComparer.OrdinalIgnoreCase);
        private readonly TimeSpan _defaultTtl;

        public OrderCacheProcessor(TimeSpan? defaultTtl = null)
        {
            _defaultTtl = defaultTtl ?? TimeSpan.FromMinutes(5);
        }

        public int CachedPairsCount => _cache.Count;

        public IEnumerable<IOrderDetails> GetOrAdd(string pair, Func<string, IEnumerable<IOrderDetails>> fetcher, TimeSpan? customTtl = null)
        {
            if (string.IsNullOrEmpty(pair))
                return new List<IOrderDetails>();

            DateTime now = DateTime.UtcNow;
            if (_cache.TryGetValue(pair, out var entry) && entry.ExpireAt > now)
            {
                return entry.Orders;
            }

            TimeSpan ttl = customTtl ?? _defaultTtl;
            IEnumerable<IOrderDetails> fetched = fetcher(pair) ?? new List<IOrderDetails>();
            List<IOrderDetails> list = fetched.ToList();
            CacheEntry newEntry = new CacheEntry(list, now.Add(ttl));
            _cache[pair] = newEntry;

            return list;
        }

        public void Invalidate(string pair)
        {
            if (!string.IsNullOrEmpty(pair))
            {
                _cache.TryRemove(pair, out _);
            }
        }

        public void Clear()
        {
            _cache.Clear();
        }
    }
}
