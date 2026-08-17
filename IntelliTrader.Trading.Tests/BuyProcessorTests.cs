using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using IntelliTrader.Core;
using IntelliTrader.Trading;
using IntelliTrader.Trading.Processors;
using Moq;
using Xunit;

namespace IntelliTrader.Trading.Tests
{
    public class BuyProcessorTests
    {
        private readonly Mock<ILoggingService> _loggingService = new Mock<ILoggingService>();
        private readonly Mock<ITradingService> _tradingService = new Mock<ITradingService>();
        private readonly Mock<IExchangeService> _exchangeService = new Mock<IExchangeService>();
        private readonly Mock<ITradingAccount> _account = new Mock<ITradingAccount>();
        private readonly Mock<IOrderingService> _orderingService = new Mock<IOrderingService>();
        private readonly Mock<INotificationService> _notificationService = new Mock<INotificationService>();
        private readonly Mock<IHealthCheckService> _healthCheckService = new Mock<IHealthCheckService>();
        private readonly Mock<ISignalsService> _signalsService = new Mock<ISignalsService>();
        private readonly TradingTimedTask _task;
        private readonly BuyProcessor _processor;

        public BuyProcessorTests()
        {
            _tradingService.Setup(s => s.Exchange).Returns(_exchangeService.Object);
            _tradingService.Setup(s => s.Account).Returns(_account.Object);

            _task = new TradingTimedTask(
                _loggingService.Object,
                _notificationService.Object,
                _healthCheckService.Object,
                _signalsService.Object,
                _orderingService.Object,
                _tradingService.Object);

            _processor = new BuyProcessor(_loggingService.Object, _tradingService.Object, _orderingService.Object, _task);
        }

        [Fact]
        public void Process_BuyDisabled_StopsTrailingBuy()
        {
            // Arrange
            var pair = "BTCUSDT";
            var pairConfig = new Mock<IPairConfig>();
            pairConfig.Setup(c => c.BuyEnabled).Returns(false);

            _tradingService.Setup(s => s.GetPairConfig(pair)).Returns(pairConfig.Object);
            _tradingService.Setup(s => s.GetPrice(pair, It.IsAny<TradePriceType?>(), It.IsAny<bool>())).Returns(10000m);

            var buyOptions = new BuyOptions(pair) { MaxCost = 100 };
            var trailingBuys = new ConcurrentDictionary<string, BuyTrailingInfo>();
            var buyTrailingInfo = new BuyTrailingInfo
            {
                BuyOptions = buyOptions,
                InitialPrice = 10000m,
                Trailing = 1m
            };
            trailingBuys.TryAdd(pair, buyTrailingInfo);

            // Act
            _processor.Process(pair, buyTrailingInfo, trailingBuys);

            // Assert
            _orderingService.Verify(o => o.PlaceBuyOrder(It.IsAny<BuyOptions>()), Times.Never());
        }

        [Fact]
        public void Process_HighSpread_PausesTrailingBuy()
        {
            // Arrange
            var pair = "BTCUSDT";
            var safety = new TrailingSafetyOptions
            {
                MaxTrailingSpread = 0.5m,
                PauseOnHighSpread = true,
                MinPriceChangeWithHighSpread = 2.0m
            };

            var pairConfig = new Mock<IPairConfig>();
            pairConfig.Setup(c => c.BuyEnabled).Returns(true);
            pairConfig.Setup(c => c.TrailingSafety).Returns(safety);

            _tradingService.Setup(s => s.GetPairConfig(pair)).Returns(pairConfig.Object);
            // Current price gives margin change = +0.5% (10050 vs 10000)
            _tradingService.Setup(s => s.GetPrice(pair, It.IsAny<TradePriceType?>(), It.IsAny<bool>())).Returns(10050m);
            _exchangeService.Setup(e => e.GetPriceSpread(pair)).Returns(1.0m); // High spread > 0.5m

            var buyOptions = new BuyOptions(pair) { MaxCost = 100 };
            var trailingBuys = new ConcurrentDictionary<string, BuyTrailingInfo>();
            var buyTrailingInfo = new BuyTrailingInfo
            {
                BuyOptions = buyOptions,
                InitialPrice = 10000m,
                Trailing = 1m,
                LastTrailingMargin = 0m,
                BestTrailingMargin = 0m,
                TrailingStopMargin = 10m
            };
            trailingBuys.TryAdd(pair, buyTrailingInfo);

            // Act
            _processor.Process(pair, buyTrailingInfo, trailingBuys);

            // Assert - Should pause and NOT place order or trigger stop
            _orderingService.Verify(o => o.PlaceBuyOrder(It.IsAny<BuyOptions>()), Times.Never());
            Assert.Equal(0m, buyTrailingInfo.LastTrailingMargin);
        }

        [Fact]
        public void Process_TrailingBuyTriggeredOnPriceReversal_PlacesBuyOrder()
        {
            // Arrange
            var pair = "BTCUSDT";
            var pairConfig = new Mock<IPairConfig>();
            pairConfig.Setup(c => c.BuyEnabled).Returns(true);
            pairConfig.Setup(c => c.TrailingSafety).Returns((TrailingSafetyOptions)null!);

            _tradingService.Setup(s => s.GetPairConfig(pair)).Returns(pairConfig.Object);
            // Initial price was 10000. Price went down to 9900 (BestMargin = -1%), now price rises to 10050 (+0.5% margin)
            // currentMargin (+0.5%) > BestMargin (-1%) + Trailing (1%) = 0% -> Triggers Buy!
            _tradingService.Setup(s => s.GetPrice(pair, It.IsAny<TradePriceType?>(), It.IsAny<bool>())).Returns(10050m);
            _exchangeService.Setup(e => e.GetPriceSpread(pair)).Returns(0.1m);

            var buyOptions = new BuyOptions(pair) { MaxCost = 100 };
            var trailingBuys = new ConcurrentDictionary<string, BuyTrailingInfo>();
            var buyTrailingInfo = new BuyTrailingInfo
            {
                BuyOptions = buyOptions,
                InitialPrice = 10000m,
                Trailing = 1m,
                BestTrailingMargin = -1m,
                LastTrailingMargin = -1m,
                TrailingStopMargin = 10m,
                TrailingStopAction = BuyTrailingStopAction.Buy
            };
            trailingBuys.TryAdd(pair, buyTrailingInfo);

            // Act
            _processor.Process(pair, buyTrailingInfo, trailingBuys);

            // Assert
            _orderingService.Verify(o => o.PlaceBuyOrder(It.Is<BuyOptions>(opt => opt.Pair == pair)), Times.Once());
        }

        [Fact]
        public void Process_TrailingBuyUpdatesBestAndLastMarginWhenDropping()
        {
            // Arrange
            var pair = "BTCUSDT";
            var pairConfig = new Mock<IPairConfig>();
            pairConfig.Setup(c => c.BuyEnabled).Returns(true);

            _tradingService.Setup(s => s.GetPairConfig(pair)).Returns(pairConfig.Object);
            // Initial price 10000. Current price 9800 -> Margin = -2.0%
            _tradingService.Setup(s => s.GetPrice(pair, It.IsAny<TradePriceType?>(), It.IsAny<bool>())).Returns(9800m);
            _exchangeService.Setup(e => e.GetPriceSpread(pair)).Returns(0.1m);

            var buyOptions = new BuyOptions(pair) { MaxCost = 100 };
            var trailingBuys = new ConcurrentDictionary<string, BuyTrailingInfo>();
            var buyTrailingInfo = new BuyTrailingInfo
            {
                BuyOptions = buyOptions,
                InitialPrice = 10000m,
                Trailing = 1m,
                BestTrailingMargin = 0m,
                LastTrailingMargin = 0m,
                TrailingStopMargin = 10m
            };
            trailingBuys.TryAdd(pair, buyTrailingInfo);

            // Act
            _processor.Process(pair, buyTrailingInfo, trailingBuys);

            // Assert
            _orderingService.Verify(o => o.PlaceBuyOrder(It.IsAny<BuyOptions>()), Times.Never());
            Assert.Equal(-2.0m, buyTrailingInfo.LastTrailingMargin);
            Assert.Equal(-2.0m, buyTrailingInfo.BestTrailingMargin);
        }
    }
}
