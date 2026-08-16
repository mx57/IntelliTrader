using System;
using System.Collections.Generic;
using System.Linq;
using IntelliTrader.Core;
using IntelliTrader.Trading;
using Moq;
using Xunit;

namespace IntelliTrader.Trading.Tests
{
    public class TradingTimedTaskTests
    {
        private readonly Mock<ILoggingService> _loggingService = new Mock<ILoggingService>();
        private readonly Mock<INotificationService> _notificationService = new Mock<INotificationService>();
        private readonly Mock<IHealthCheckService> _healthCheckService = new Mock<IHealthCheckService>();
        private readonly Mock<ISignalsService> _signalsService = new Mock<ISignalsService>();
        private readonly Mock<IOrderingService> _orderingService = new Mock<IOrderingService>();
        private readonly Mock<ITradingService> _tradingService = new Mock<ITradingService>();
        private readonly Mock<IExchangeService> _exchangeService = new Mock<IExchangeService>();
        private readonly Mock<ITradingAccount> _account = new Mock<ITradingAccount>();

        public TradingTimedTaskTests()
        {
            _tradingService.Setup(s => s.Exchange).Returns(_exchangeService.Object);
            _tradingService.Setup(s => s.Account).Returns(_account.Object);
            _tradingService.Setup(s => s.Config).Returns(new TradingConfig { Market = "USDT" });
            _exchangeService.Setup(e => e.GetPriceSpread(It.IsAny<string>())).Returns(0.1m);
        }

        [Fact]
        public void TrailingBuy_TriggersOnlyOnReversal()
        {
            // Arrange
            var pair = "BTCUSDT";
            var buyOptions = new BuyOptions(pair) { MaxCost = 100 };
            var pairConfig = new Mock<IPairConfig>();
            pairConfig.Setup(c => c.BuyTrailing).Returns(1m); // 1% trailing
            pairConfig.Setup(c => c.BuyTrailingStopMargin).Returns(10m); // 10% stop margin (to stay in trailing)
            pairConfig.Setup(c => c.BuyEnabled).Returns(true);
            pairConfig.Setup(c => c.Rules).Returns(new List<string>());

            _tradingService.Setup(s => s.GetPairConfig(pair)).Returns(pairConfig.Object);

            // Initial price 10000
            var price = 10000m;
            _tradingService.Setup(s => s.GetPrice(pair, It.IsAny<TradePriceType?>(), It.IsAny<bool>())).Returns(price);

            var task = new TradingTimedTask(
                _loggingService.Object,
                _notificationService.Object,
                _healthCheckService.Object,
                _signalsService.Object,
                _orderingService.Object,
                _tradingService.Object);

            // Step 1: Initiate trailing buy
            task.InitiateBuy(buyOptions);
            // After initiation: BestTrailingMargin = 0 (calculated from initial price 10000)

            // Step 2: Price goes down to 9900 (currentMargin = -1%)
            price = 9900m;
            _tradingService.Setup(s => s.GetPrice(pair, It.IsAny<TradePriceType?>(), It.IsAny<bool>())).Returns(price);
            task.ProcessTradingPairs();
            // BestTrailingMargin should become -1%

            // Step 3: Price goes up slightly to 9950 (currentMargin = -0.5%)
            // -0.5% is higher than -1% (Best) but hasn't recovered by 1% (Trailing) yet.
            // Condition for trigger: currentMargin > Best + Trailing => -0.5 > -1 + 1 => -0.5 > 0 (False)
            price = 9950m;
            _tradingService.Setup(s => s.GetPrice(pair, It.IsAny<TradePriceType?>(), It.IsAny<bool>())).Returns(price);
            task.ProcessTradingPairs();

            _orderingService.Verify(o => o.PlaceBuyOrder(It.IsAny<BuyOptions>()), Times.Never());

            // Step 4: Price goes up to 10050 (currentMargin = +0.5%)
            // Condition: 0.5 > -1 + 1 => 0.5 > 0 (True)
            price = 10050m;
            _tradingService.Setup(s => s.GetPrice(pair, It.IsAny<TradePriceType?>(), It.IsAny<bool>())).Returns(price);
            task.ProcessTradingPairs();

            // Assert
            _orderingService.Verify(o => o.PlaceBuyOrder(It.IsAny<BuyOptions>()), Times.Once());
        }

        [Fact]
        public void TrailingBuy_PausesOnHighSpread()
        {
            // Arrange
            var pair = "BTCUSDT";
            var buyOptions = new BuyOptions(pair) { MaxCost = 100 };
            var safety = new TrailingSafetyOptions
            {
                MaxTrailingSpread = 0.5m, // 0.5% max spread
                PauseOnHighSpread = true,
                MinPriceChangeWithHighSpread = 2.0m // 2% min price change to override pause
            };

            var pairConfig = new Mock<IPairConfig>();
            pairConfig.Setup(c => c.BuyTrailing).Returns(1m);
            pairConfig.Setup(c => c.BuyTrailingStopMargin).Returns(10m);
            pairConfig.Setup(c => c.BuyEnabled).Returns(true);
            pairConfig.Setup(c => c.Rules).Returns(new List<string>());
            pairConfig.Setup(c => c.TrailingSafety).Returns(safety);

            _tradingService.Setup(s => s.GetPairConfig(pair)).Returns(pairConfig.Object);

            var price = 10000m;
            _tradingService.Setup(s => s.GetPrice(pair, It.IsAny<TradePriceType?>(), It.IsAny<bool>())).Returns(price);
            _exchangeService.Setup(e => e.GetPriceSpread(pair)).Returns(0.1m); // Low spread initially

            var task = new TradingTimedTask(
                _loggingService.Object,
                _notificationService.Object,
                _healthCheckService.Object,
                _signalsService.Object,
                _orderingService.Object,
                _tradingService.Object);

            // Step 1: Initiate
            task.InitiateBuy(buyOptions);

            // Step 2: Price goes up to 10150 (currentMargin = +1.5%), but spread is HIGH (1.0%)
            // It SHOULD trigger (currentMargin 1.5% > Best 0% + Trailing 1%), but it's PAUSED.
            // Price change (1.5%) is less than MinPriceChangeWithHighSpread (2.0%).
            price = 10150m;
            _tradingService.Setup(s => s.GetPrice(pair, It.IsAny<TradePriceType?>(), It.IsAny<bool>())).Returns(price);
            _exchangeService.Setup(e => e.GetPriceSpread(pair)).Returns(1.0m); // High spread

            task.ProcessTradingPairs();

            _orderingService.Verify(o => o.PlaceBuyOrder(It.IsAny<BuyOptions>()), Times.Never());

            // Step 3: Spread returns to normal, now it should trigger
            _exchangeService.Setup(e => e.GetPriceSpread(pair)).Returns(0.1m);
            task.ProcessTradingPairs();

            _orderingService.Verify(o => o.PlaceBuyOrder(It.IsAny<BuyOptions>()), Times.Once());
        }

        [Fact]
        public void DcaProcessor_PausesOnHighSpread()
        {
            // Arrange
            var pair = "BTCUSDT";
            var pairConfig = new Mock<IPairConfig>();
            pairConfig.Setup(c => c.NextDCAMargin).Returns(-3.0m);
            pairConfig.Setup(c => c.BuyEnabled).Returns(true);
            pairConfig.Setup(c => c.BuyMultiplier).Returns(1.5m);
            pairConfig.Setup(c => c.BuyTrailing).Returns(0m);
            pairConfig.Setup(c => c.Rules).Returns(new List<string>());

            var safety = new TrailingSafetyOptions
            {
                MaxTrailingSpread = 1.0m,
                PauseOnHighSpread = true
            };
            pairConfig.Setup(c => c.TrailingSafety).Returns(safety);

            _tradingService.Setup(s => s.GetPairConfig(pair)).Returns(pairConfig.Object);

            var tradingPair = new Mock<ITradingPair>();
            tradingPair.Setup(p => p.Pair).Returns(pair);
            tradingPair.Setup(p => p.CurrentMargin).Returns(-5.0m); // below next DCA margin (-3.0m)
            tradingPair.Setup(p => p.CurrentSpread).Returns(1.5m); // high spread (1.5% > 1.0% max spread)
            tradingPair.Setup(p => p.Cost).Returns(100m);
            tradingPair.Setup(p => p.Metadata).Returns(new OrderMetadata());

            _account.Setup(a => a.GetTradingPairs(It.IsAny<bool>())).Returns(new List<ITradingPair> { tradingPair.Object });
            _tradingService.Setup(s => s.GetPrice(pair, It.IsAny<TradePriceType?>(), It.IsAny<bool>())).Returns(10000m);

            var task = new TradingTimedTask(
                _loggingService.Object,
                _notificationService.Object,
                _healthCheckService.Object,
                _signalsService.Object,
                _orderingService.Object,
                _tradingService.Object);

            // Act
            task.ProcessTradingPairs();

            // Assert - Should NOT trigger DCA because spread is high and PauseOnHighSpread is true
            _tradingService.Verify(s => s.CanBuy(It.IsAny<BuyOptions>(), out It.Ref<string>.IsAny), Times.Never());
            _orderingService.Verify(o => o.PlaceBuyOrder(It.IsAny<BuyOptions>()), Times.Never());
        }

        [Fact]
        public void DcaProcessor_ScalesCostBasedOnGlobalRating()
        {
            // Arrange
            var pair = "BTCUSDT";
            var pairConfig = new Mock<IPairConfig>();
            pairConfig.Setup(c => c.NextDCAMargin).Returns(-3.0m);
            pairConfig.Setup(c => c.BuyEnabled).Returns(true);
            pairConfig.Setup(c => c.BuyMultiplier).Returns(1.5m);
            pairConfig.Setup(c => c.BuyTrailing).Returns(0m);
            pairConfig.Setup(c => c.Rules).Returns(new List<string>());

            var safety = new TrailingSafetyOptions
            {
                MaxTrailingSpread = 1.0m,
                PauseOnHighSpread = true
            };
            pairConfig.Setup(c => c.TrailingSafety).Returns(safety);

            _tradingService.Setup(s => s.GetPairConfig(pair)).Returns(pairConfig.Object);

            var tradingPair = new Mock<ITradingPair>();
            tradingPair.Setup(p => p.Pair).Returns(pair);
            tradingPair.Setup(p => p.CurrentMargin).Returns(-5.0m); // below next DCA margin (-3.0m)
            tradingPair.Setup(p => p.CurrentSpread).Returns(0.2m); // low spread
            tradingPair.Setup(p => p.Cost).Returns(100m);
            tradingPair.Setup(p => p.Metadata).Returns(new OrderMetadata());

            _account.Setup(a => a.GetTradingPairs(It.IsAny<bool>())).Returns(new List<ITradingPair> { tradingPair.Object });
            _tradingService.Setup(s => s.GetPrice(pair, It.IsAny<TradePriceType?>(), It.IsAny<bool>())).Returns(10000m);

            // Positive global rating (+0.5) => multiplier scaling factor = 1.5
            // Expected cost = 100 * 1.5 (BuyMultiplier) * 1.5 (Scaling factor) = 225
            _signalsService.Setup(s => s.GetGlobalRating()).Returns(0.5);

            string outMsg = "";
            _tradingService.Setup(s => s.CanBuy(It.IsAny<BuyOptions>(), out outMsg)).Returns(true);

            var task = new TradingTimedTask(
                _loggingService.Object,
                _notificationService.Object,
                _healthCheckService.Object,
                _signalsService.Object,
                _orderingService.Object,
                _tradingService.Object);

            // Act
            task.ProcessTradingPairs();

            // Assert - Should trigger DCA with scaled cost
            _orderingService.Verify(o => o.PlaceBuyOrder(It.Is<BuyOptions>(opt =>
                opt.Pair == pair &&
                opt.MaxCost == 225m &&
                opt.Metadata != null &&
                opt.Metadata.BoughtGlobalRating == 0.5)), Times.Once());
        }

        [Fact]
        public void DcaProcessor_WidensSpacingUnderHighSpread()
        {
            // Arrange
            var pair = "BTCUSDT";
            var pairConfig = new Mock<IPairConfig>();
            pairConfig.Setup(c => c.NextDCAMargin).Returns(-3.0m);
            pairConfig.Setup(c => c.BuyEnabled).Returns(true);
            pairConfig.Setup(c => c.BuyMultiplier).Returns(1.5m);
            pairConfig.Setup(c => c.BuyTrailing).Returns(0m);
            pairConfig.Setup(c => c.Rules).Returns(new List<string>());

            var safety = new TrailingSafetyOptions
            {
                MaxTrailingSpread = 0.5m, // baseSpread is MaxTrailingSpread
                PauseOnHighSpread = false  // We don't pause, we just test widening
            };
            pairConfig.Setup(c => c.TrailingSafety).Returns(safety);

            _tradingService.Setup(s => s.GetPairConfig(pair)).Returns(pairConfig.Object);

            var tradingPair = new Mock<ITradingPair>();
            tradingPair.Setup(p => p.Pair).Returns(pair);
            // CurrentMargin is -4.0%. Base DCA is -3.0%.
            // Since CurrentSpread (1.5%) > baseSpread (0.5%), spreadFactor = 1.0 + (1.5 - 0.5) = 2.0.
            // effectiveNextDCAMargin = -3.0 * 2.0 = -6.0%.
            // Since CurrentMargin (-4.0%) > effective (-6.0%), DCA should NOT trigger yet!
            tradingPair.Setup(p => p.CurrentMargin).Returns(-4.0m);
            tradingPair.Setup(p => p.CurrentSpread).Returns(1.5m);
            tradingPair.Setup(p => p.Cost).Returns(100m);
            tradingPair.Setup(p => p.Metadata).Returns(new OrderMetadata());

            _account.Setup(a => a.GetTradingPairs(It.IsAny<bool>())).Returns(new List<ITradingPair> { tradingPair.Object });
            _tradingService.Setup(s => s.GetPrice(pair, It.IsAny<TradePriceType?>(), It.IsAny<bool>())).Returns(10000m);

            var signal = new Mock<ISignal>();
            signal.Setup(s => s.Volatility).Returns(8.0);
            _signalsService.Setup(s => s.GetSignalsByPair(pair)).Returns(new List<ISignal> { signal.Object });

            var task = new TradingTimedTask(
                _loggingService.Object,
                _notificationService.Object,
                _healthCheckService.Object,
                _signalsService.Object,
                _orderingService.Object,
                _tradingService.Object);

            // Act
            task.ProcessTradingPairs();

            // Assert - Should NOT trigger DCA because spacing is widened to -6.0% and CurrentMargin is -4.0%
            _orderingService.Verify(o => o.PlaceBuyOrder(It.IsAny<BuyOptions>()), Times.Never());
        }

        [Fact]
        public void DcaProcessor_WidensSpacingUnderHighSignalVolatility()
        {
            // Arrange
            var pair = "BTCUSDT";
            var pairConfig = new Mock<IPairConfig>();
            pairConfig.Setup(c => c.NextDCAMargin).Returns(-3.0m);
            pairConfig.Setup(c => c.BuyEnabled).Returns(true);
            pairConfig.Setup(c => c.BuyMultiplier).Returns(1.5m);
            pairConfig.Setup(c => c.BuyTrailing).Returns(0m);
            pairConfig.Setup(c => c.Rules).Returns(new List<string>());
            pairConfig.Setup(c => c.TrailingSafety).Returns((TrailingSafetyOptions)null!);

            _tradingService.Setup(s => s.GetPairConfig(pair)).Returns(pairConfig.Object);

            var tradingPair = new Mock<ITradingPair>();
            tradingPair.Setup(p => p.Pair).Returns(pair);
            // CurrentMargin is -5.0%. Base DCA is -3.0%.
            // Signal Volatility is 8.0 (baseline is 4.0).
            // signalVolatilityFactor = 1.0 + (8.0 - 4.0) / 4.0 = 2.0.
            // effectiveNextDCAMargin = -3.0 * 2.0 = -6.0%.
            // Since CurrentMargin (-5.0%) > effective (-6.0%), DCA should NOT trigger yet!
            tradingPair.Setup(p => p.CurrentMargin).Returns(-5.0m);
            tradingPair.Setup(p => p.CurrentSpread).Returns(0.1m); // low spread, spread factor is 1.0
            tradingPair.Setup(p => p.Cost).Returns(100m);
            tradingPair.Setup(p => p.Metadata).Returns(new OrderMetadata());

            _account.Setup(a => a.GetTradingPairs(It.IsAny<bool>())).Returns(new List<ITradingPair> { tradingPair.Object });
            _tradingService.Setup(s => s.GetPrice(pair, It.IsAny<TradePriceType?>(), It.IsAny<bool>())).Returns(10000m);

            _signalsService.Setup(s => s.GetGlobalRating()).Returns((double?)null);

            var mockSignal = new Mock<ISignal>();
            mockSignal.Setup(s => s.Volatility).Returns(8.0);
            var signalsList = new List<ISignal> { mockSignal.Object };
            _signalsService.Setup(s => s.GetSignalsByPair(pair)).Returns(signalsList);

            string outMsg = "";
            _tradingService.Setup(s => s.CanBuy(It.IsAny<BuyOptions>(), out outMsg)).Returns(true);

            var task = new TradingTimedTask(
                _loggingService.Object,
                _notificationService.Object,
                _healthCheckService.Object,
                _signalsService.Object,
                _orderingService.Object,
                _tradingService.Object);

            // Act
            task.ProcessTradingPairs();

            // Assert - Should NOT trigger DCA because spacing is widened to -6.0% and CurrentMargin is -5.0%
            _orderingService.Verify(o => o.PlaceBuyOrder(It.IsAny<BuyOptions>()), Times.Never());
        }

        [Fact]
        public void DcaProcessor_PostponesDcaOnExtremelyHighSpread()
        {
            // Arrange
            var pair = "BTCUSDT";
            var pairConfig = new Mock<IPairConfig>();
            pairConfig.Setup(c => c.NextDCAMargin).Returns(-3.0m);
            pairConfig.Setup(c => c.BuyEnabled).Returns(true);
            pairConfig.Setup(c => c.BuyMultiplier).Returns(1.5m);
            pairConfig.Setup(c => c.BuyTrailing).Returns(0m);
            pairConfig.Setup(c => c.Rules).Returns(new List<string>());

            var safety = new TrailingSafetyOptions
            {
                MaxTrailingSpread = 0.5m, // baseSpread = 0.5m
                PauseOnHighSpread = false
            };
            pairConfig.Setup(c => c.TrailingSafety).Returns(safety);

            _tradingService.Setup(s => s.GetPairConfig(pair)).Returns(pairConfig.Object);

            var tradingPair = new Mock<ITradingPair>();
            tradingPair.Setup(p => p.Pair).Returns(pair);
            tradingPair.Setup(p => p.CurrentMargin).Returns(-15.0m); // Deep negative margin
            // Extremely high spread: 1.6% >= 3 * 0.5% (1.5%)
            tradingPair.Setup(p => p.CurrentSpread).Returns(1.6m);
            tradingPair.Setup(p => p.Cost).Returns(100m);
            tradingPair.Setup(p => p.Metadata).Returns(new OrderMetadata());

            _account.Setup(a => a.GetTradingPairs(It.IsAny<bool>())).Returns(new List<ITradingPair> { tradingPair.Object });
            _tradingService.Setup(s => s.GetPrice(pair, It.IsAny<TradePriceType?>(), It.IsAny<bool>())).Returns(10000m);

            var task = new TradingTimedTask(
                _loggingService.Object,
                _notificationService.Object,
                _healthCheckService.Object,
                _signalsService.Object,
                _orderingService.Object,
                _tradingService.Object);

            // Act
            task.ProcessTradingPairs();

            // Assert - DCA must be postponed entirely due to extreme spread safety check
            _tradingService.Verify(s => s.CanBuy(It.IsAny<BuyOptions>(), out It.Ref<string>.IsAny), Times.Never());
            _orderingService.Verify(o => o.PlaceBuyOrder(It.IsAny<BuyOptions>()), Times.Never());
        }

        [Fact]
        public void DcaProcessor_WidensSpacingAndCapsAtTenOnModeratelyHighSpread()
        {
            // Arrange
            var pair = "BTCUSDT";
            var pairConfig = new Mock<IPairConfig>();
            pairConfig.Setup(c => c.NextDCAMargin).Returns(-3.0m);
            pairConfig.Setup(c => c.BuyEnabled).Returns(true);
            pairConfig.Setup(c => c.BuyMultiplier).Returns(1.5m);
            pairConfig.Setup(c => c.BuyTrailing).Returns(0m);
            pairConfig.Setup(c => c.Rules).Returns(new List<string>());

            var safety = new TrailingSafetyOptions
            {
                MaxTrailingSpread = 0.5m, // baseSpread = 0.5m
                PauseOnHighSpread = false
            };
            pairConfig.Setup(c => c.TrailingSafety).Returns(safety);

            _tradingService.Setup(s => s.GetPairConfig(pair)).Returns(pairConfig.Object);

            var tradingPair = new Mock<ITradingPair>();
            tradingPair.Setup(p => p.Pair).Returns(pair);
            // CurrentSpread = 1.2m, which is >= 2x baseSpread (1.0m) and < 3x baseSpread (1.5m).
            // spreadFactor = 1.0 + 2.0 * (1.2 - 0.5) = 2.4. Max cap is raised to 10.0m.
            // effectiveNextDCAMargin = -3.0 * 2.4 = -7.2%.
            // Since CurrentMargin (-5.0%) > effective (-7.2%), DCA should NOT trigger yet!
            tradingPair.Setup(p => p.CurrentMargin).Returns(-5.0m);
            tradingPair.Setup(p => p.CurrentSpread).Returns(1.2m);
            tradingPair.Setup(p => p.Cost).Returns(100m);
            tradingPair.Setup(p => p.Metadata).Returns(new OrderMetadata());

            _account.Setup(a => a.GetTradingPairs(It.IsAny<bool>())).Returns(new List<ITradingPair> { tradingPair.Object });
            _tradingService.Setup(s => s.GetPrice(pair, It.IsAny<TradePriceType?>(), It.IsAny<bool>())).Returns(10000m);

            var task = new TradingTimedTask(
                _loggingService.Object,
                _notificationService.Object,
                _healthCheckService.Object,
                _signalsService.Object,
                _orderingService.Object,
                _tradingService.Object);

            // Act
            task.ProcessTradingPairs();

            // Assert - Should NOT trigger because effective margin is widened to -7.2%
            _orderingService.Verify(o => o.PlaceBuyOrder(It.IsAny<BuyOptions>()), Times.Never());
        }
    }
}
