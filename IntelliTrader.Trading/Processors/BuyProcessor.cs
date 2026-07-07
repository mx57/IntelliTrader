using IntelliTrader.Core;
using System;
using System.Collections.Concurrent;

namespace IntelliTrader.Trading.Processors
{
    internal class BuyProcessor
    {
        private readonly ILoggingService loggingService;
        private readonly ITradingService tradingService;
        private readonly IOrderingService orderingService;
        private readonly TradingTimedTask task;

        public BuyProcessor(ILoggingService loggingService, ITradingService tradingService, IOrderingService orderingService, TradingTimedTask task)
        {
            this.loggingService = loggingService;
            this.tradingService = tradingService;
            this.orderingService = orderingService;
            this.task = task;
        }

        public void Process(string pair, BuyTrailingInfo buyTrailingInfo, ConcurrentDictionary<string, BuyTrailingInfo> trailingBuys)
        {
            ITradingPair tradingPair = tradingService.Account.GetTradingPair(pair);
            IPairConfig pairConfig = tradingService.GetPairConfig(pair);
            decimal currentPrice = tradingService.GetPrice(pair);
            decimal currentMargin = Utils.CalculatePercentage(buyTrailingInfo.InitialPrice, currentPrice);

            if (pairConfig.BuyEnabled)
            {
                var safety = pairConfig.TrailingSafety;
                decimal currentSpread = tradingService.Exchange.GetPriceSpread(pair);
                if (safety != null && safety.MaxTrailingSpread > 0 && currentSpread > safety.MaxTrailingSpread)
                {
                    if (safety.PauseOnHighSpread && Math.Abs(currentMargin - buyTrailingInfo.LastTrailingMargin) < safety.MinPriceChangeWithHighSpread)
                    {
                        if (task.LoggingEnabled)
                        {
                            loggingService.Info($"Trailing buy paused for {tradingPair?.FormattedName ?? pair} due to high spread: {currentSpread:0.00}%");
                        }
                        return;
                    }
                }

                if (Math.Round(currentMargin, 1) != Math.Round(buyTrailingInfo.LastTrailingMargin, 1))
                {
                    if (task.LoggingEnabled)
                    {
                        loggingService.Info($"Continue trailing buy {tradingPair?.FormattedName ?? pair}. Price: {currentPrice:0.00000000}, Margin: {currentMargin:0.00}");
                    }
                }

                if (currentMargin >= buyTrailingInfo.TrailingStopMargin || currentMargin > (buyTrailingInfo.BestTrailingMargin + buyTrailingInfo.Trailing))
                {
                    task.StopTrailingBuy(pair);

                    if (buyTrailingInfo.TrailingStopAction == BuyTrailingStopAction.Buy || currentMargin < buyTrailingInfo.TrailingStopMargin)
                    {
                        orderingService.PlaceBuyOrder(buyTrailingInfo.BuyOptions);
                    }
                    else
                    {
                        if (task.LoggingEnabled)
                        {
                            loggingService.Info($"Stop trailing buy {tradingPair?.FormattedName ?? pair}. Reason: stop margin reached");
                        }
                    }
                }
                else
                {
                    buyTrailingInfo.LastTrailingMargin = currentMargin;
                    if (currentMargin < buyTrailingInfo.BestTrailingMargin)
                    {
                        buyTrailingInfo.BestTrailingMargin = currentMargin;
                    }
                }
            }
            else
            {
                task.StopTrailingBuy(pair);
            }
        }
    }
}
