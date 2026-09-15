using IntelliTrader.Core;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace IntelliTrader.Trading.Processors
{
    /// <summary>
    /// Thread-safe in-memory sliding cache with cache expiration (TTL) for trading order history per pair.
    /// Prevents excessive disk/network reads by storing completed orders in memory.
    /// </summary>
    public class OrderCacheProcessor
    {
        private class CacheEntry
        {
            public List<IOrderDetails> Orders { get; }
            public DateTime AbsoluteExpiration { get; set; }
            public DateTime LastAccessed { get; set; }

            public CacheEntry(List<IOrderDetails> orders, DateTime absoluteExpiration, DateTime lastAccessed)
            {
                Orders = orders;
                AbsoluteExpiration = absoluteExpiration;
                LastAccessed = lastAccessed;
            }
        }

        private readonly ConcurrentDictionary<string, CacheEntry> _cache = new ConcurrentDictionary<string, CacheEntry>(StringComparer.OrdinalIgnoreCase);
        private readonly TimeSpan _defaultTtl;
        private readonly TimeSpan _slidingExpiration;

        public OrderCacheProcessor(TimeSpan? defaultTtl = null, TimeSpan? slidingExpiration = null)
        {
            _defaultTtl = defaultTtl ?? TimeSpan.FromMinutes(10);
            _slidingExpiration = slidingExpiration ?? TimeSpan.FromMinutes(5);
        }

        /// <summary>
        /// Attempts to get cached orders for the specified trading pair.
        /// Returns true if a valid, unexpired entry is found.
        /// </summary>
        public bool TryGetOrders(string pair, out IEnumerable<IOrderDetails> orders)
        {
            orders = null;
            if (string.IsNullOrEmpty(pair))
                return false;

            if (_cache.TryGetValue(pair, out var entry))
            {
                lock (entry)
                {
                    var now = DateTime.UtcNow;
                    if (now > entry.AbsoluteExpiration)
                    {
                        _cache.TryRemove(pair, out _);
                        return false;
                    }

                    entry.LastAccessed = now;
                    entry.AbsoluteExpiration = now.Add(_slidingExpiration);
                    orders = entry.Orders.ToList();
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Stores or updates orders in the cache for a given pair.
        /// </summary>
        public void SetOrders(string pair, IEnumerable<IOrderDetails> orders, TimeSpan? ttl = null)
        {
            if (string.IsNullOrEmpty(pair))
                return;

            var now = DateTime.UtcNow;
            var timeToLive = ttl ?? _defaultTtl;
            var entryList = orders?.ToList() ?? new List<IOrderDetails>();
            var entry = new CacheEntry(entryList, now.Add(timeToLive), now);

            _cache[pair] = entry;
        }

        /// <summary>
        /// Removes cached orders for a specific trading pair.
        /// </summary>
        public bool Invalidate(string pair)
        {
            if (string.IsNullOrEmpty(pair))
                return false;

            return _cache.TryRemove(pair, out _);
        }

        /// <summary>
        /// Clears all expired entries from the cache.
        /// </summary>
        public int PurgeExpired()
        {
            var now = DateTime.UtcNow;
            int removedCount = 0;

            foreach (var kvp in _cache)
            {
                lock (kvp.Value)
                {
                    if (now > kvp.Value.AbsoluteExpiration)
                    {
                        if (_cache.TryRemove(kvp.Key, out _))
                        {
                            removedCount++;
                        }
                    }
                }
            }

            return removedCount;
        }

        /// <summary>
        /// Completely clears the cache.
        /// </summary>
        public void Clear()
        {
            _cache.Clear();
        }

        /// <summary>
        /// Returns the number of cached trading pairs.
        /// </summary>
        public int Count => _cache.Count;
    }
}
