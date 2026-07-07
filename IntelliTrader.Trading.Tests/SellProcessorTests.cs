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
            // Expected target = 5.0 - (10.0 * 0.4) = 5.0 - 4.0 = 1.0%
            // Since CurrentMargin (2.0%) >= EffectiveTarget (1.0%), it should trigger.

            _tradingService.Setup(s => s.GetPairConfig(It.Is<string>(p => p == pair))).Returns(pairConfig.Object);
            _account.Setup(a => a.HasTradingPair(It.Is<string>(p => p == pair), It.IsAny<bool>())).Returns(true);
            ITradingPair tp = tradingPair.Object;
            _account.Setup(a => a.GetTradingPair(It.Is<string>(p => p == pair), It.IsAny<bool>())).Returns(tp);

            var trailingBuys = new ConcurrentDictionary<string, BuyTrailingInfo>();
            var trailingSells = new ConcurrentDictionary<string, SellTrailingInfo>();

            // Act
            _processor.Process(tradingPair.Object, pairConfig.Object, trailingBuys, trailingSells);

            // Assert
            // Since SellTrailing is 0, InitiateSell should call orderingService.PlaceSellOrder directly.
            _orderingService.Verify(o => o.PlaceSellOrder(It.Is<SellOptions>(opt => opt.Pair == pair)), Times.Once());
        }
    }
}
