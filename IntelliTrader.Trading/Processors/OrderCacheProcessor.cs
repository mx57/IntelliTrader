using IntelliTrader.Core;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace IntelliTrader.Trading.Processors
{
    internal class OrderCacheProcessor : ITradingProcessor
    {
        private readonly ConcurrentDictionary<string, CacheEntry> _cache = new ConcurrentDictionary<string, CacheEntry>();
        private readonly TimeSpan _defaultTtl;

        public TimeSpan DefaultTtl => _defaultTtl;
        public int Count => _cache.Count;

        public OrderCacheProcessor(TimeSpan? defaultTtl = null)
        {
            _defaultTtl = defaultTtl ?? TimeSpan.FromSeconds(60);
        }

        public void Process(ITradingPair tradingPair, IPairConfig pairConfig, ConcurrentDictionary<string, BuyTrailingInfo> trailingBuys, ConcurrentDictionary<string, SellTrailingInfo> trailingSells)
        {
            if (tradingPair == null) return;
            CleanExpiredEntries();
        }

        public IEnumerable<IOrderDetails> GetOrAdd(string pair, Func<IEnumerable<IOrderDetails>> orderFactory, TimeSpan? ttl = null)
        {
            if (string.IsNullOrEmpty(pair)) return Enumerable.Empty<IOrderDetails>();

            DateTimeOffset now = DateTimeOffset.UtcNow;
            TimeSpan duration = ttl ?? _defaultTtl;

            if (_cache.TryGetValue(pair, out CacheEntry entry))
            {
                if (now - entry.CachedAt < entry.Ttl)
                {
                    entry.LastAccessed = now;
                    return entry.Orders;
                }
            }

            IEnumerable<IOrderDetails> orders = orderFactory != null ? orderFactory() : Enumerable.Empty<IOrderDetails>();
            var newEntry = new CacheEntry
            {
                Orders = orders?.ToList() ?? new List<IOrderDetails>(),
                CachedAt = now,
                LastAccessed = now,
                Ttl = duration
            };

            _cache[pair] = newEntry;
            return newEntry.Orders;
        }

        public void Set(string pair, IEnumerable<IOrderDetails> orders, TimeSpan? ttl = null)
        {
            if (string.IsNullOrEmpty(pair)) return;

            DateTimeOffset now = DateTimeOffset.UtcNow;
            var entry = new CacheEntry
            {
                Orders = orders?.ToList() ?? new List<IOrderDetails>(),
                CachedAt = now,
                LastAccessed = now,
                Ttl = ttl ?? _defaultTtl
            };
            _cache[pair] = entry;
        }

        public bool TryGet(string pair, out IEnumerable<IOrderDetails> orders)
        {
            orders = null;
            if (string.IsNullOrEmpty(pair)) return false;

            if (_cache.TryGetValue(pair, out CacheEntry entry))
            {
                DateTimeOffset now = DateTimeOffset.UtcNow;
                if (now - entry.CachedAt < entry.Ttl)
                {
                    entry.LastAccessed = now;
                    orders = entry.Orders;
                    return true;
                }
            }
            return false;
        }

        public bool Invalidate(string pair)
        {
            if (string.IsNullOrEmpty(pair)) return false;
            return _cache.TryRemove(pair, out _);
        }

        public void Clear()
        {
            _cache.Clear();
        }

        public void CleanExpiredEntries()
        {
            DateTimeOffset now = DateTimeOffset.UtcNow;
            foreach (var kvp in _cache)
            {
                if (now - kvp.Value.CachedAt >= kvp.Value.Ttl)
                {
                    _cache.TryRemove(kvp.Key, out _);
                }
            }
        }

        private class CacheEntry
        {
            public List<IOrderDetails> Orders { get; set; }
            public DateTimeOffset CachedAt { get; set; }
            public DateTimeOffset LastAccessed { get; set; }
            public TimeSpan Ttl { get; set; }
        }
    }
}
