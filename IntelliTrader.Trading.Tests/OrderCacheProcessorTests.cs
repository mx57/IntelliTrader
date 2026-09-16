using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using IntelliTrader.Core;
using IntelliTrader.Exchange.Base;
using IntelliTrader.Trading.Processors;
using Xunit;

namespace IntelliTrader.Trading.Tests
{
    public class OrderCacheProcessorTests
    {
        [Fact]
        public void OrderCacheProcessor_CachesFetcherResults_OnFirstCall()
        {
            // Arrange
            var cache = new OrderCacheProcessor(TimeSpan.FromMinutes(1));
            int fetchCount = 0;
            IEnumerable<IOrderDetails> Fetcher(string pair)
            {
                fetchCount++;
                return new List<IOrderDetails>
                {
                    new OrderDetails { OrderId = "1", Pair = pair, Price = 100 }
                };
            }

            // Act
            var result1 = cache.GetOrAdd("BTCUSDT", Fetcher).ToList();
            var result2 = cache.GetOrAdd("BTCUSDT", Fetcher).ToList();

            // Assert
            Assert.Equal(1, fetchCount);
            Assert.Single(result1);
            Assert.Single(result2);
            Assert.Equal(1, cache.CachedPairsCount);
        }

        [Fact]
        public void OrderCacheProcessor_RefreshesCache_WhenExpired()
        {
            // Arrange
            var cache = new OrderCacheProcessor(TimeSpan.FromMilliseconds(50));
            int fetchCount = 0;
            IEnumerable<IOrderDetails> Fetcher(string pair)
            {
                fetchCount++;
                return new List<IOrderDetails>
                {
                    new OrderDetails { OrderId = fetchCount.ToString(), Pair = pair }
                };
            }

            // Act
            var result1 = cache.GetOrAdd("BTCUSDT", Fetcher).ToList();
            Thread.Sleep(100);
            var result2 = cache.GetOrAdd("BTCUSDT", Fetcher).ToList();

            // Assert
            Assert.Equal(2, fetchCount);
            Assert.Equal("1", result1.First().OrderId);
            Assert.Equal("2", result2.First().OrderId);
        }

        [Fact]
        public void OrderCacheProcessor_Invalidate_RemovesPairFromCache()
        {
            // Arrange
            var cache = new OrderCacheProcessor(TimeSpan.FromMinutes(1));
            int fetchCount = 0;
            IEnumerable<IOrderDetails> Fetcher(string pair)
            {
                fetchCount++;
                return new List<IOrderDetails>
                {
                    new OrderDetails { OrderId = fetchCount.ToString(), Pair = pair }
                };
            }

            // Act
            cache.GetOrAdd("ETHUSDT", Fetcher);
            cache.Invalidate("ETHUSDT");
            cache.GetOrAdd("ETHUSDT", Fetcher);

            // Assert
            Assert.Equal(2, fetchCount);
            Assert.Equal(1, cache.CachedPairsCount);
        }

        [Fact]
        public void OrderCacheProcessor_Clear_EmptiesAllCachedPairs()
        {
            // Arrange
            var cache = new OrderCacheProcessor(TimeSpan.FromMinutes(1));
            cache.GetOrAdd("BTCUSDT", _ => new List<IOrderDetails>());
            cache.GetOrAdd("ETHUSDT", _ => new List<IOrderDetails>());

            Assert.Equal(2, cache.CachedPairsCount);

            // Act
            cache.Clear();

            // Assert
            Assert.Equal(0, cache.CachedPairsCount);
        }

        [Fact]
        public void OrderCacheProcessor_IsThreadSafe_UnderConcurrentRequests()
        {
            // Arrange
            var cache = new OrderCacheProcessor(TimeSpan.FromMinutes(1));
            int fetchCount = 0;
            IEnumerable<IOrderDetails> Fetcher(string pair)
            {
                Interlocked.Increment(ref fetchCount);
                return new List<IOrderDetails> { new OrderDetails { OrderId = pair } };
            }

            // Act
            Parallel.For(0, 50, i =>
            {
                cache.GetOrAdd("SOLUSDT", Fetcher);
            });

            // Assert
            Assert.Equal(1, cache.CachedPairsCount);
        }
    }
}
