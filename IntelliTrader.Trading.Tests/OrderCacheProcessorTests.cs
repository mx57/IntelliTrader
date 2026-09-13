using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using IntelliTrader.Core;
using IntelliTrader.Trading.Processors;
using Moq;
using Xunit;

namespace IntelliTrader.Trading.Tests
{
    public class OrderCacheProcessorTests
    {
        [Fact]
        public void GetOrFetchOrders_CacheHit_InvokesFetcherOnlyOnce()
        {
            // Arrange
            var processor = new OrderCacheProcessor(TimeSpan.FromMinutes(5));
            int fetcherCallCount = 0;
            var sampleOrder = new Mock<IOrderDetails>();
            sampleOrder.Setup(o => o.Pair).Returns("BTCUSDT");

            IEnumerable<IOrderDetails> Fetcher()
            {
                fetcherCallCount++;
                return new List<IOrderDetails> { sampleOrder.Object };
            }

            // Act
            var result1 = processor.GetOrFetchOrders("BTCUSDT", Fetcher).ToList();
            var result2 = processor.GetOrFetchOrders("BTCUSDT", Fetcher).ToList();

            // Assert
            Assert.Equal(1, fetcherCallCount);
            Assert.Single(result1);
            Assert.Single(result2);
            Assert.Equal("BTCUSDT", result1[0].Pair);
        }

        [Fact]
        public void GetOrFetchOrders_CacheExpired_InvokesFetcherAgain()
        {
            // Arrange - set short expiration of 50ms
            var processor = new OrderCacheProcessor(TimeSpan.FromMilliseconds(50));
            int fetcherCallCount = 0;

            IEnumerable<IOrderDetails> Fetcher()
            {
                fetcherCallCount++;
                var order = new Mock<IOrderDetails>();
                order.Setup(o => o.Pair).Returns("ETHUSDT");
                return new List<IOrderDetails> { order.Object };
            }

            // Act
            var result1 = processor.GetOrFetchOrders("ETHUSDT", Fetcher).ToList();
            Thread.Sleep(100); // Wait for expiration
            var result2 = processor.GetOrFetchOrders("ETHUSDT", Fetcher).ToList();

            // Assert
            Assert.Equal(2, fetcherCallCount);
            Assert.Single(result1);
            Assert.Single(result2);
        }

        [Fact]
        public void TryGetCachedOrders_ReturnsTrueWhenValid_FalseWhenExpiredOrMissing()
        {
            // Arrange
            var processor = new OrderCacheProcessor(TimeSpan.FromMinutes(5));
            var sampleOrder = new Mock<IOrderDetails>();
            sampleOrder.Setup(o => o.Pair).Returns("ADAUSDT");

            processor.SetOrderCache("ADAUSDT", new List<IOrderDetails> { sampleOrder.Object });

            // Act & Assert
            Assert.True(processor.TryGetCachedOrders("ADAUSDT", out var cachedOrders));
            Assert.NotNull(cachedOrders);
            Assert.Single(cachedOrders);

            Assert.False(processor.TryGetCachedOrders("SOLUSDT", out _));
        }

        [Fact]
        public void InvalidateCache_RemovesPairOrClearsAll()
        {
            // Arrange
            var processor = new OrderCacheProcessor(TimeSpan.FromMinutes(5));
            processor.SetOrderCache("BTCUSDT", new List<IOrderDetails>());
            processor.SetOrderCache("ETHUSDT", new List<IOrderDetails>());

            Assert.Equal(2, processor.Count);

            // Act 1 - Invalidate specific pair
            processor.InvalidateCache("BTCUSDT");

            // Assert 1
            Assert.Equal(1, processor.Count);
            Assert.False(processor.TryGetCachedOrders("BTCUSDT", out _));
            Assert.True(processor.TryGetCachedOrders("ETHUSDT", out _));

            // Act 2 - Invalidate all
            processor.InvalidateCache();

            // Assert 2
            Assert.Equal(0, processor.Count);
            Assert.False(processor.TryGetCachedOrders("ETHUSDT", out _));
        }
    }
}
