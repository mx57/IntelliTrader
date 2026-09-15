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
        public void SetAndTryGetOrders_ValidPair_ReturnsCachedOrders()
        {
            // Arrange
            var processor = new OrderCacheProcessor();
            var pair = "BTCUSDT";
            var orders = new List<IOrderDetails>
            {
                new OrderDetails { OrderId = "1", Pair = pair, Price = 50000m, Amount = 0.1m, Side = OrderSide.Buy },
                new OrderDetails { OrderId = "2", Pair = pair, Price = 51000m, Amount = 0.1m, Side = OrderSide.Sell }
            };

            // Act
            processor.SetOrders(pair, orders);
            bool success = processor.TryGetOrders(pair, out var cachedOrders);

            // Assert
            Assert.True(success);
            Assert.NotNull(cachedOrders);
            Assert.Equal(2, cachedOrders.Count());
        }

        [Fact]
        public void TryGetOrders_NonExistentPair_ReturnsFalse()
        {
            // Arrange
            var processor = new OrderCacheProcessor();

            // Act
            bool success = processor.TryGetOrders("ETHUSDT", out var cachedOrders);

            // Assert
            Assert.False(success);
            Assert.Null(cachedOrders);
        }

        [Fact]
        public void TryGetOrders_ExpiredTtl_ReturnsFalseAndRemovesEntry()
        {
            // Arrange - TTL of 50 milliseconds
            var processor = new OrderCacheProcessor(defaultTtl: TimeSpan.FromMilliseconds(50), slidingExpiration: TimeSpan.FromMilliseconds(50));
            var pair = "BTCUSDT";
            var orders = new List<IOrderDetails>
            {
                new OrderDetails { OrderId = "1", Pair = pair, Price = 50000m, Amount = 0.1m }
            };

            processor.SetOrders(pair, orders, TimeSpan.FromMilliseconds(50));
            Thread.Sleep(100);

            // Act
            bool success = processor.TryGetOrders(pair, out var cachedOrders);

            // Assert
            Assert.False(success);
            Assert.Null(cachedOrders);
            Assert.Equal(0, processor.Count);
        }

        [Fact]
        public void Invalidate_ExistingPair_RemovesPairFromCache()
        {
            // Arrange
            var processor = new OrderCacheProcessor();
            var pair = "BTCUSDT";
            var orders = new List<IOrderDetails>
            {
                new OrderDetails { OrderId = "1", Pair = pair }
            };

            processor.SetOrders(pair, orders);

            // Act
            bool removed = processor.Invalidate(pair);
            bool success = processor.TryGetOrders(pair, out _);

            // Assert
            Assert.True(removed);
            Assert.False(success);
            Assert.Equal(0, processor.Count);
        }

        [Fact]
        public void PurgeExpired_RemovesOnlyExpiredEntries()
        {
            // Arrange
            var processor = new OrderCacheProcessor();
            var expiredPair = "BTCUSDT";
            var validPair = "ETHUSDT";

            processor.SetOrders(expiredPair, new List<IOrderDetails>(), TimeSpan.FromMilliseconds(50));
            processor.SetOrders(validPair, new List<IOrderDetails>(), TimeSpan.FromMinutes(10));

            Thread.Sleep(100);

            // Act
            int removed = processor.PurgeExpired();

            // Assert
            Assert.Equal(1, removed);
            Assert.Equal(1, processor.Count);
            Assert.True(processor.TryGetOrders(validPair, out _));
            Assert.False(processor.TryGetOrders(expiredPair, out _));
        }

        [Fact]
        public void Clear_RemovesAllEntries()
        {
            // Arrange
            var processor = new OrderCacheProcessor();
            processor.SetOrders("BTCUSDT", new List<IOrderDetails>());
            processor.SetOrders("ETHUSDT", new List<IOrderDetails>());

            // Act
            processor.Clear();

            // Assert
            Assert.Equal(0, processor.Count);
        }

        [Fact]
        public void ConcurrentAccess_ThreadSafetyCheck()
        {
            // Arrange
            var processor = new OrderCacheProcessor();

            // Act
            Parallel.For(0, 100, i =>
            {
                var pair = $"PAIR_{i % 10}";
                processor.SetOrders(pair, new List<IOrderDetails>
                {
                    new OrderDetails { OrderId = i.ToString(), Pair = pair }
                });

                processor.TryGetOrders(pair, out _);
            });

            // Assert
            Assert.Equal(10, processor.Count);
        }
    }
}
