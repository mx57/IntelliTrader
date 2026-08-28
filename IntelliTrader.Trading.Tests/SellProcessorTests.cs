using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using IntelliTrader.Core;
using IntelliTrader.Trading.Processors;
using Moq;
using Xunit;

namespace IntelliTrader.Trading.Tests
{
    public class SellProcessorTests
    {
        private readonly Mock<ILoggingService> _loggingService = new Mock<ILoggingService>();
        private readonly Mock<ITradingService> _tradingService = new Mock<ITradingService>();
        private readonly Mock<ITradingAccount> _account = new Mock<ITradingAccount>();
        private readonly Mock<IOrderingService> _orderingService = new Mock<IOrderingService>();
        private readonly Mock<INotificationService> _notificationService = new Mock<INotificationService>();
        private readonly Mock<IHealthCheckService> _healthCheckService = new Mock<IHealthCheckService>();
        private readonly Mock<ISignalsService> _signalsService = new Mock<ISignalsService>();
        private readonly TradingTimedTask _task;
        private readonly SellProcessor _processor;

        public SellProcessorTests()
        {
            _tradingService.Setup(s => s.Account).Returns(_account.Object);
            _task = new TradingTimedTask(
                _loggingService.Object,
                _notificationService.Object,
                _healthCheckService.Object,
                _signalsService.Object,
                _orderingService.Object,
                _tradingService.Object);
            _processor = new SellProcessor(_loggingService.Object, _tradingService.Object, _orderingService.Object, _task);
        }

        [Fact]
        public void Process_MaxAgeExit_TriggersSell()
        {
            // Arrange
            var pair = "BTCUSDT";
            var tradingPair = new Mock<ITradingPair>();
            tradingPair.Setup(p => p.Pair).Returns(pair);
            tradingPair.Setup(p => p.CurrentAge).Returns(2.0); // 2 days old

            var pairConfig = new Mock<IPairConfig>();
            pairConfig.Setup(c => c.SellEnabled).Returns(true);
            pairConfig.Setup(c => c.MaxAge).Returns(1.5); // Max age 1.5 days

            var trailingBuys = new ConcurrentDictionary<string, BuyTrailingInfo>();
            var trailingSells = new ConcurrentDictionary<string, SellTrailingInfo>();

            // Act
            _processor.Process(tradingPair.Object, pairConfig.Object, trailingBuys, trailingSells);

            // Assert
            _orderingService.Verify(o => o.PlaceSellOrder(It.Is<SellOptions>(opt => opt.Pair == pair)), Times.Once());
        }

        [Fact]
        public void Process_DynamicTargetDecay_TriggersSell()
        {
            // Arrange
            var pair = "BTCUSDT";
            var tradingPair = new Mock<ITradingPair>();
            tradingPair.Setup(p => p.Pair).Returns(pair);
            tradingPair.Setup(p => p.FormattedName).Returns(pair);
            tradingPair.Setup(p => p.CurrentAge).Returns(10.0); // 10 days old
            tradingPair.Setup(p => p.CurrentMargin).Returns(2.0m); // 2% margin

            var pairConfig = new Mock<IPairConfig>();
            pairConfig.Setup(c => c.SellEnabled).Returns(true);
            pairConfig.Setup(c => c.SellMargin).Returns(5.0m); // Original target 5%
            pairConfig.Setup(c => c.SellMarginDecay).Returns(0.4m); // 0.4% decay per day
            pairConfig.Setup(c => c.SellTrailing).Returns(0m); // No trailing for simplicity

            _tradingService.Setup(s => s.GetPairConfig(It.Is<string>(p => p == pair))).Returns(pairConfig.Object);
            _account.Setup(a => a.HasTradingPair(It.Is<string>(p => p == pair), It.IsAny<bool>())).Returns(true);
            ITradingPair tp = tradingPair.Object;
            _account.Setup(a => a.GetTradingPair(It.Is<string>(p => p == pair), It.IsAny<bool>())).Returns(tp);

            var trailingBuys = new ConcurrentDictionary<string, BuyTrailingInfo>();
            var trailingSells = new ConcurrentDictionary<string, SellTrailingInfo>();

            // Act
            _processor.Process(tradingPair.Object, pairConfig.Object, trailingBuys, trailingSells);

            // Assert
            _orderingService.Verify(o => o.PlaceSellOrder(It.Is<SellOptions>(opt => opt.Pair == pair)), Times.Once());
        }

        [Fact]
        public void Process_HighSpreadVolatility_LowersSellMarginTarget()
        {
            // Arrange
            var pair = "ETHUSDT";
            var tradingPair = new Mock<ITradingPair>();
            tradingPair.Setup(p => p.Pair).Returns(pair);
            tradingPair.Setup(p => p.FormattedName).Returns(pair);
            tradingPair.Setup(p => p.CurrentAge).Returns(0.0); // 0 age, so decay is 0
            tradingPair.Setup(p => p.CurrentMargin).Returns(2.8m); // Margin is 2.8%
            tradingPair.Setup(p => p.CurrentSpread).Returns(1.2m); // High spread (base 0.2%, excess 1.0%)

            var pairConfig = new Mock<IPairConfig>();
            pairConfig.Setup(c => c.SellEnabled).Returns(true);
            pairConfig.Setup(c => c.SellMargin).Returns(3.0m); // Base target 3.0%
            pairConfig.Setup(c => c.SellMarginDecay).Returns(0m);
            pairConfig.Setup(c => c.SellTrailing).Returns(0m);
            pairConfig.Setup(c => c.TrailingSafety).Returns((TrailingSafetyOptions)null);

            // Discount calculation: excess = 1.0%, volatilityDiscount = Math.Min(1.0 * 0.25, 3.0 * 0.5) = 0.25%
            // Effective sell margin target = 3.0 - 0.25 = 2.75%
            // Since CurrentMargin (2.8%) >= Effective Target (2.75%), sell should be initiated.

            _tradingService.Setup(s => s.GetPairConfig(It.Is<string>(p => p == pair))).Returns(pairConfig.Object);
            _account.Setup(a => a.HasTradingPair(It.Is<string>(p => p == pair), It.IsAny<bool>())).Returns(true);
            ITradingPair tp = tradingPair.Object;
            _account.Setup(a => a.GetTradingPair(It.Is<string>(p => p == pair), It.IsAny<bool>())).Returns(tp);

            var trailingBuys = new ConcurrentDictionary<string, BuyTrailingInfo>();
            var trailingSells = new ConcurrentDictionary<string, SellTrailingInfo>();

            // Act
            _processor.Process(tradingPair.Object, pairConfig.Object, trailingBuys, trailingSells);

            // Assert
            _orderingService.Verify(o => o.PlaceSellOrder(It.Is<SellOptions>(opt => opt.Pair == pair)), Times.Once());
        }

        [Fact]
        public void Process_HighSpreadVolatility_TightensSellTrailingStop()
        {
            // Arrange
            var pair = "SOLUSDT";
            var tradingPair = new Mock<ITradingPair>();
            tradingPair.Setup(p => p.Pair).Returns(pair);
            tradingPair.Setup(p => p.FormattedName).Returns(pair);
            tradingPair.Setup(p => p.CurrentMargin).Returns(2.8m);
            tradingPair.Setup(p => p.CurrentSpread).Returns(1.2m); // Base 0.2%, excess 1.0%

            var pairConfig = new Mock<IPairConfig>();
            pairConfig.Setup(c => c.SellEnabled).Returns(true);

            var trailingBuys = new ConcurrentDictionary<string, BuyTrailingInfo>();
            var trailingSells = new ConcurrentDictionary<string, SellTrailingInfo>();

            // BestTrailingMargin = 3.0%, Trailing = 0.3%
            // Standard check requires CurrentMargin <= 3.0 - 0.3 = 2.7% (CurrentMargin 2.8% would NOT trigger sell)
            // Under high spread: excess = 1.0%, discountRatio = 1.0 * 0.2 = 0.2
            // Effective trailing = 0.3 * (1.0 - 0.2) = 0.24%
            // Effective trigger margin = 3.0 - 0.24 = 2.76%
            // Since CurrentMargin (2.8m) <= BestTrailingMargin - effectiveTrailing? Wait:
            // Let's set CurrentMargin to 2.75m.
            // 2.75m > 2.70m (so under normal trailing, no sell), but 2.75m <= 3.0 - 0.24 (2.76m), so under tightened trailing, sell triggers!

            tradingPair.Setup(p => p.CurrentMargin).Returns(2.75m);

            var sellTrailingInfo = new SellTrailingInfo
            {
                SellOptions = new SellOptions(pair),
                BestTrailingMargin = 3.0m,
                Trailing = 0.3m,
                TrailingStopMargin = 0m,
                TrailingStopAction = SellTrailingStopAction.Sell,
                LastTrailingMargin = 2.75m
            };
            trailingSells[pair] = sellTrailingInfo;

            // Act
            _processor.Process(tradingPair.Object, pairConfig.Object, trailingBuys, trailingSells);

            // Assert
            _orderingService.Verify(o => o.PlaceSellOrder(It.Is<SellOptions>(opt => opt.Pair == pair)), Times.Once());
        }
    }
}
