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
    }
}
