using IntelliTrader.Core;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace IntelliTrader.Trading.Processors
{
    internal class CacheEntry
    {
        public IEnumerable<IOrderDetails> Orders { get; set; }
        private long lastUpdatedTicks;
        public TimeSpan Expiration { get; set; }

        public DateTime LastUpdated
        {
            get => new DateTime(System.Threading.Volatile.Read(ref lastUpdatedTicks), DateTimeKind.Utc);
            set => System.Threading.Volatile.Write(ref lastUpdatedTicks, value.Ticks);
        }

        public CacheEntry()
        {
            lastUpdatedTicks = DateTime.UtcNow.Ticks;
        }

        public bool IsExpired => (DateTime.UtcNow - LastUpdated) > Expiration;
    }

    public class OrderCacheProcessor
    {
        private readonly ConcurrentDictionary<string, CacheEntry> cache = new ConcurrentDictionary<string, CacheEntry>(StringComparer.OrdinalIgnoreCase);
        private readonly TimeSpan defaultExpiration;

        public OrderCacheProcessor(TimeSpan? defaultExpiration = null)
        {
            this.defaultExpiration = defaultExpiration ?? TimeSpan.FromMinutes(5);
        }

        public IEnumerable<IOrderDetails> GetOrFetchOrders(string pair, Func<IEnumerable<IOrderDetails>> fetcher, TimeSpan? cacheDuration = null)
        {
            if (string.IsNullOrWhiteSpace(pair))
            {
                return fetcher != null ? fetcher() : Enumerable.Empty<IOrderDetails>();
            }

            TimeSpan expiration = cacheDuration ?? defaultExpiration;

            if (cache.TryGetValue(pair, out var entry) && !entry.IsExpired)
            {
                // Sliding cache hit: extend TTL in a thread-safe manner
                entry.LastUpdated = DateTime.UtcNow;
                return entry.Orders;
            }

            if (fetcher == null)
            {
                return Enumerable.Empty<IOrderDetails>();
            }

            var fetchedOrders = fetcher()?.ToList() ?? new List<IOrderDetails>();
            SetOrderCache(pair, fetchedOrders, expiration);
            return fetchedOrders;
        }

        public void SetOrderCache(string pair, IEnumerable<IOrderDetails> orders, TimeSpan? cacheDuration = null)
        {
            if (string.IsNullOrWhiteSpace(pair)) return;

            var entry = new CacheEntry
            {
                Orders = orders?.ToList() ?? new List<IOrderDetails>(),
                LastUpdated = DateTime.UtcNow,
                Expiration = cacheDuration ?? defaultExpiration
            };

            cache[pair] = entry;
        }

        public bool TryGetCachedOrders(string pair, out IEnumerable<IOrderDetails> orders)
        {
            orders = null;
            if (!string.IsNullOrWhiteSpace(pair) && cache.TryGetValue(pair, out var entry) && !entry.IsExpired)
            {
                entry.LastUpdated = DateTime.UtcNow;
                orders = entry.Orders;
                return true;
            }
            return false;
        }

        public void InvalidateCache(string pair = null)
        {
            if (string.IsNullOrWhiteSpace(pair))
            {
                cache.Clear();
            }
            else
            {
                cache.TryRemove(pair, out _);
            }
        }

        public int Count => cache.Count;
    }
}
