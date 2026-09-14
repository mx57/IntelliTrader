using IntelliTrader.Core;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace IntelliTrader.Trading.Processors
{
    public class OrderCacheProcessor
    {
        private class CacheEntry
        {
            public IEnumerable<IOrderDetails> Orders { get; }
            public DateTimeOffset ExpirationTime { get; }

            public CacheEntry(IEnumerable<IOrderDetails> orders, TimeSpan ttl)
            {
                Orders = orders;
                ExpirationTime = DateTimeOffset.UtcNow.Add(ttl);
            }

            public bool IsExpired => DateTimeOffset.UtcNow >= ExpirationTime;
        }

        private readonly ConcurrentDictionary<string, CacheEntry> _cache = new ConcurrentDictionary<string, CacheEntry>(StringComparer.OrdinalIgnoreCase);
        private readonly TimeSpan _defaultTtl;

        public OrderCacheProcessor(TimeSpan? defaultTtl = null)
        {
            _defaultTtl = defaultTtl ?? TimeSpan.FromMinutes(2);
        }

        public IEnumerable<IOrderDetails> GetOrFetchTrades(string pair, Func<string, IEnumerable<IOrderDetails>> fetchFunc, TimeSpan? customTtl = null)
        {
            if (string.IsNullOrWhiteSpace(pair))
            {
                return new List<IOrderDetails>();
            }

            if (_cache.TryGetValue(pair, out var entry) && !entry.IsExpired)
            {
                return entry.Orders;
            }

            var orders = fetchFunc != null ? (fetchFunc(pair) ?? new List<IOrderDetails>()) : new List<IOrderDetails>();
            var ttl = customTtl ?? _defaultTtl;
            _cache[pair] = new CacheEntry(orders, ttl);
            return orders;
        }

        public void Invalidate(string pair)
        {
            if (!string.IsNullOrWhiteSpace(pair))
            {
                _cache.TryRemove(pair, out _);
            }
        }

        public void Clear()
        {
            _cache.Clear();
        }

        public int Count => _cache.Count;
    }
}
