using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using IntelliTrader.Core;
using IntelliTrader.Trading.Processors;
using Moq;
using Xunit;

namespace IntelliTrader.Trading.Tests
{
    public class OrderCacheProcessorTests
    {
        [Fact]
        public void GetOrFetchTrades_ReturnsCachedOrders_WithoutReFetchingWithinTTL()
        {
            // Arrange
            var processor = new OrderCacheProcessor(TimeSpan.FromMinutes(1));
            var pair = "BTCUSDT";
            int fetchCount = 0;

            var mockOrder = new Mock<IOrderDetails>();
            mockOrder.Setup(o => o.OrderId).Returns("ORDER1");

            Func<string, IEnumerable<IOrderDetails>> fetchFunc = p =>
            {
                fetchCount++;
                return new List<IOrderDetails> { mockOrder.Object };
            };

            // Act
            var result1 = processor.GetOrFetchTrades(pair, fetchFunc);
            var result2 = processor.GetOrFetchTrades(pair, fetchFunc);

            // Assert
            Assert.Equal(1, fetchCount);
            Assert.Single(result1);
            Assert.Single(result2);
            Assert.Equal("ORDER1", result1.First().OrderId);
            Assert.Equal(1, processor.Count);
        }

        [Fact]
        public void GetOrFetchTrades_ReFetches_AfterExpirationTTL()
        {
            // Arrange - Very short TTL (10 ms)
            var processor = new OrderCacheProcessor(TimeSpan.FromMilliseconds(10));
            var pair = "ETHUSDT";
            int fetchCount = 0;

            Func<string, IEnumerable<IOrderDetails>> fetchFunc = p =>
            {
                fetchCount++;
                var mockOrder = new Mock<IOrderDetails>();
                mockOrder.Setup(o => o.OrderId).Returns($"ORDER_{fetchCount}");
                return new List<IOrderDetails> { mockOrder.Object };
            };

            // Act
            var result1 = processor.GetOrFetchTrades(pair, fetchFunc);

            // Wait for expiration
            System.Threading.Thread.Sleep(25);

            var result2 = processor.GetOrFetchTrades(pair, fetchFunc);

            // Assert
            Assert.Equal(2, fetchCount);
            Assert.Equal("ORDER_1", result1.First().OrderId);
            Assert.Equal("ORDER_2", result2.First().OrderId);
        }

        [Fact]
        public void Invalidate_RemovesPairFromCache()
        {
            // Arrange
            var processor = new OrderCacheProcessor(TimeSpan.FromMinutes(5));
            var pair = "SOLUSDT";
            int fetchCount = 0;

            Func<string, IEnumerable<IOrderDetails>> fetchFunc = p =>
            {
                fetchCount++;
                return new List<IOrderDetails>();
            };

            // Act
            processor.GetOrFetchTrades(pair, fetchFunc);
            Assert.Equal(1, fetchCount);
            Assert.Equal(1, processor.Count);

            processor.Invalidate(pair);

            // Assert
            Assert.Equal(0, processor.Count);

            // Next call should fetch again
            processor.GetOrFetchTrades(pair, fetchFunc);
            Assert.Equal(2, fetchCount);
        }

        [Fact]
        public void Clear_RemovesAllCachedPairs()
        {
            // Arrange
            var processor = new OrderCacheProcessor(TimeSpan.FromMinutes(5));
            Func<string, IEnumerable<IOrderDetails>> fetchFunc = p => new List<IOrderDetails>();

            processor.GetOrFetchTrades("BTCUSDT", fetchFunc);
            processor.GetOrFetchTrades("ETHUSDT", fetchFunc);
            Assert.Equal(2, processor.Count);

            // Act
            processor.Clear();

            // Assert
            Assert.Equal(0, processor.Count);
        }

        [Fact]
        public void ConcurrentAccess_ThreadSafe()
        {
            // Arrange
            var processor = new OrderCacheProcessor(TimeSpan.FromMinutes(1));
            int fetchCalls = 0;

            Func<string, IEnumerable<IOrderDetails>> fetchFunc = p =>
            {
                System.Threading.Interlocked.Increment(ref fetchCalls);
                return new List<IOrderDetails>();
            };

            // Act
            Parallel.For(0, 50, i =>
            {
                string pair = $"PAIR_{i % 5}";
                processor.GetOrFetchTrades(pair, fetchFunc);
            });

            // Assert
            Assert.True(processor.Count <= 5);
        }
    }
}
