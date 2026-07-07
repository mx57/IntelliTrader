using IntelliTrader.Core;
using System;
using System.Collections.Concurrent;

namespace IntelliTrader.Trading.Processors
{
    internal class SellProcessor : ITradingProcessor
    {
        private readonly ILoggingService loggingService;
        private readonly ITradingService tradingService;
        private readonly IOrderingService orderingService;
        private readonly TradingTimedTask task;

        public SellProcessor(ILoggingService loggingService, ITradingService tradingService, IOrderingService orderingService, TradingTimedTask task)
        {
            this.loggingService = loggingService;
            this.tradingService = tradingService;
            this.orderingService = orderingService;
            this.task = task;
        }

        public void Process(ITradingPair tradingPair, IPairConfig pairConfig, ConcurrentDictionary<string, BuyTrailingInfo> trailingBuys, ConcurrentDictionary<string, SellTrailingInfo> trailingSells)
        {
            if (trailingSells.TryGetValue(tradingPair.Pair, out SellTrailingInfo sellTrailingInfo))
            {
                if (pairConfig.SellEnabled)
                {
                    var safety = pairConfig.TrailingSafety;
                    if (safety != null && safety.MaxTrailingSpread > 0 && tradingPair.CurrentSpread > safety.MaxTrailingSpread)
                    {
                        if (safety.PauseOnHighSpread && Math.Abs(tradingPair.CurrentMargin - sellTrailingInfo.LastTrailingMargin) < safety.MinPriceChangeWithHighSpread)
                        {
                            if (task.LoggingEnabled)
                            {
                                loggingService.Info($"Trailing sell paused for {tradingPair.FormattedName} due to high spread: {tradingPair.CurrentSpread:0.00}%");
                            }
                            return;
                        }
                    }

                    if (Math.Round(tradingPair.CurrentMargin, 1) != Math.Round(sellTrailingInfo.LastTrailingMargin, 1))
                    {
                        if (task.LoggingEnabled)
                        {
                            loggingService.Info($"Continue trailing sell {tradingPair.FormattedName}. " +
                                $"Price: {tradingPair.CurrentPrice:0.00000000}, Margin: {tradingPair.CurrentMargin:0.00}");
                        }
                    }

                    if (tradingPair.CurrentMargin <= sellTrailingInfo.TrailingStopMargin || tradingPair.CurrentMargin <
                        (sellTrailingInfo.BestTrailingMargin - sellTrailingInfo.Trailing))
                    {
                        task.StopTrailingSell(tradingPair.Pair);

                        if (tradingPair.CurrentMargin > 0 || sellTrailingInfo.SellMargin < 0)
                        {
                            if (sellTrailingInfo.TrailingStopAction == SellTrailingStopAction.Sell || tradingPair.CurrentMargin > sellTrailingInfo.TrailingStopMargin)
                            {
                                orderingService.PlaceSellOrder(sellTrailingInfo.SellOptions);
                            }
                            else
                            {
                                if (task.LoggingEnabled)
                                {
                                    loggingService.Info($"Stop trailing sell {tradingPair.FormattedName}. Reason: stop margin reached");
                                }
                            }
                        }
                        else
                        {
                            if (task.LoggingEnabled)
                            {
                                loggingService.Info($"Stop trailing sell {tradingPair.FormattedName}. Reason: negative margin");
                            }
                        }
                    }
                    else
                    {
                        sellTrailingInfo.LastTrailingMargin = tradingPair.CurrentMargin;
                        if (tradingPair.CurrentMargin > sellTrailingInfo.BestTrailingMargin)
                        {
                            sellTrailingInfo.BestTrailingMargin = tradingPair.CurrentMargin;
                        }
                    }
                }
                else
                {
                    task.StopTrailingSell(tradingPair.Pair);
                }
            }
            else
            {
                if (pairConfig.SellEnabled && tradingPair.CurrentMargin >= pairConfig.SellMargin)
                {
                    task.InitiateSell(new SellOptions(tradingPair.Pair));
                }
                else if (pairConfig.SellEnabled && pairConfig.SellStopLossEnabled &&
                    tradingPair.CurrentMargin <= pairConfig.SellStopLossMargin &&
                    tradingPair.CurrentAge >= pairConfig.SellStopLossMinAge &&
                    (pairConfig.NextDCAMargin == null || !pairConfig.SellStopLossAfterDCA))
                {
                    if (task.LoggingEnabled)
                    {
                        loggingService.Info($"Stop loss triggered for {tradingPair.FormattedName}. Margin: {tradingPair.CurrentMargin:0.00}");
                    }
                    orderingService.PlaceSellOrder(new SellOptions(tradingPair.Pair));
                }
            }
        }
    }
}
