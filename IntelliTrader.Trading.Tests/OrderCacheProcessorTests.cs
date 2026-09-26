using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using IntelliTrader.Core;
using IntelliTrader.Trading;
using IntelliTrader.Trading.Processors;
using Moq;
using Xunit;

namespace IntelliTrader.Trading.Tests
{
    public class OrderCacheProcessorTests
    {
        [Fact]
        public void GetOrAdd_CachesOrdersAndReturnsFromCache()
        {
            var processor = new OrderCacheProcessor(TimeSpan.FromSeconds(10));
            int factoryCallCount = 0;

            var mockOrder = new Mock<IOrderDetails>();
            mockOrder.Setup(o => o.OrderId).Returns("order-1");

            Func<IEnumerable<IOrderDetails>> factory = () =>
            {
                factoryCallCount++;
                return new List<IOrderDetails> { mockOrder.Object };
            };

            var firstCall = processor.GetOrAdd("BTCUSDT", factory).ToList();
            var secondCall = processor.GetOrAdd("BTCUSDT", factory).ToList();

            Assert.Equal(1, factoryCallCount);
            Assert.Single(firstCall);
            Assert.Single(secondCall);
            Assert.Equal("order-1", firstCall[0].OrderId);
            Assert.Equal(1, processor.Count);
        }

        [Fact]
        public void GetOrAdd_ExpiresCache_WhenTtlElapsed()
        {
            var processor = new OrderCacheProcessor(TimeSpan.FromMilliseconds(50));
            int factoryCallCount = 0;

            Func<IEnumerable<IOrderDetails>> factory = () =>
            {
                factoryCallCount++;
                var mock = new Mock<IOrderDetails>();
                mock.Setup(o => o.OrderId).Returns($"order-{factoryCallCount}");
                return new List<IOrderDetails> { mock.Object };
            };

            var firstCall = processor.GetOrAdd("ETHUSDT", factory).ToList();
            Assert.Equal(1, factoryCallCount);
            Assert.Equal("order-1", firstCall[0].OrderId);

            Thread.Sleep(100);

            var secondCall = processor.GetOrAdd("ETHUSDT", factory).ToList();
            Assert.Equal(2, factoryCallCount);
            Assert.Equal("order-2", secondCall[0].OrderId);
        }

        [Fact]
        public void Set_UpdatesCacheAndSetsTtl()
        {
            var processor = new OrderCacheProcessor(TimeSpan.FromMinutes(1));
            var mockOrder = new Mock<IOrderDetails>();
            mockOrder.Setup(o => o.OrderId).Returns("custom-order");

            processor.Set("SOLUSDT", new[] { mockOrder.Object });

            bool found = processor.TryGet("SOLUSDT", out var cachedOrders);
            Assert.True(found);
            Assert.NotNull(cachedOrders);
            Assert.Equal("custom-order", cachedOrders.First().OrderId);
        }

        [Fact]
        public void TryGet_ReturnsFalseWhenExpiredOrMissing()
        {
            var processor = new OrderCacheProcessor(TimeSpan.FromMilliseconds(30));

            bool missing = processor.TryGet("NONEXISTENT", out var missingOrders);
            Assert.False(missing);
            Assert.Null(missingOrders);

            var mockOrder = new Mock<IOrderDetails>();
            processor.Set("ADAUSDT", new[] { mockOrder.Object });

            Thread.Sleep(60);

            bool expired = processor.TryGet("ADAUSDT", out var expiredOrders);
            Assert.False(expired);
            Assert.Null(expiredOrders);
        }

        [Fact]
        public void Invalidate_RemovesEntryFromCache()
        {
            var processor = new OrderCacheProcessor(TimeSpan.FromMinutes(5));
            var mockOrder = new Mock<IOrderDetails>();
            processor.Set("BNBUSDT", new[] { mockOrder.Object });

            Assert.Equal(1, processor.Count);

            bool removed = processor.Invalidate("BNBUSDT");
            Assert.True(removed);
            Assert.Equal(0, processor.Count);

            bool tryGetAfterInvalidate = processor.TryGet("BNBUSDT", out _);
            Assert.False(tryGetAfterInvalidate);
        }

        [Fact]
        public void Clear_RemovesAllEntries()
        {
            var processor = new OrderCacheProcessor();
            var mockOrder = new Mock<IOrderDetails>();

            processor.Set("BTCUSDT", new[] { mockOrder.Object });
            processor.Set("ETHUSDT", new[] { mockOrder.Object });
            Assert.Equal(2, processor.Count);

            processor.Clear();
            Assert.Equal(0, processor.Count);
        }

        [Fact]
        public void Process_CleansExpiredEntries()
        {
            var processor = new OrderCacheProcessor(TimeSpan.FromMilliseconds(40));
            var mockOrder = new Mock<IOrderDetails>();

            processor.Set("DOGEUSDT", new[] { mockOrder.Object });
            Assert.Equal(1, processor.Count);

            Thread.Sleep(80);

            var pairMock = new Mock<ITradingPair>();
            pairMock.Setup(p => p.Pair).Returns("DOGEUSDT");

            var trailingBuys = new ConcurrentDictionary<string, BuyTrailingInfo>();
            var trailingSells = new ConcurrentDictionary<string, SellTrailingInfo>();

            processor.Process(pairMock.Object, null, trailingBuys, trailingSells);

            Assert.Equal(0, processor.Count);
        }

        [Fact]
        public void EdgeCases_NullOrEmptyPairHandledGracefully()
        {
            var processor = new OrderCacheProcessor();

            var emptyOrders = processor.GetOrAdd(null, () => null);
            Assert.Empty(emptyOrders);

            bool tryGetNull = processor.TryGet("", out var orders);
            Assert.False(tryGetNull);
            Assert.Null(orders);

            bool invalidateNull = processor.Invalidate(null);
            Assert.False(invalidateNull);

            processor.Set(null, null);
            Assert.Equal(0, processor.Count);
        }
    }
}
