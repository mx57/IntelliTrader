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
            // Strategy: Max Age Exit
            // If the position is older than the configured MaxAge, trigger an immediate emergency sell.
            if (pairConfig.SellEnabled && pairConfig.MaxAge.HasValue && tradingPair.CurrentAge >= pairConfig.MaxAge.Value)
            {
                if (task.LoggingEnabled)
                {
                    loggingService.Info($"Max age exit triggered for {tradingPair.FormattedName}. Age: {tradingPair.CurrentAge:0.00}, Max: {pairConfig.MaxAge.Value:0.00}");
                }
                task.StopTrailingSell(tradingPair.Pair);
                // Use PlaceSellOrder directly to bypass trailing and ensure immediate exit for stale positions.
                orderingService.PlaceSellOrder(new SellOptions(tradingPair.Pair));
                return;
            }

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

                    // Profit protection during high spread volatility: tighten trailing distance to trigger take profit earlier
                    decimal effectiveTrailing = sellTrailingInfo.Trailing;
                    decimal baseSpread = 0.2m;
                    if (safety != null && safety.MaxTrailingSpread > 0)
                    {
                        baseSpread = safety.MaxTrailingSpread;
                    }

                    if (tradingPair.CurrentSpread > baseSpread && sellTrailingInfo.Trailing > 0)
                    {
                        decimal spreadExcess = tradingPair.CurrentSpread - baseSpread;
                        decimal discountRatio = Math.Min(spreadExcess * 0.2m, 0.5m); // Cap trailing tightening at 50%
                        effectiveTrailing = Math.Max(0.01m, sellTrailingInfo.Trailing * (1.0m - discountRatio));
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
                        (sellTrailingInfo.BestTrailingMargin - effectiveTrailing))
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
                // Strategy: Dynamic Target Decay & Volatility Profit Protection
                // Gradually decrease target sell margin as position ages or when spread volatility is high to secure gains before trend reversal.
                decimal effectiveSellMargin = pairConfig.SellMargin - (decimal)(tradingPair.CurrentAge * (double)pairConfig.SellMarginDecay);

                decimal baseSpread = 0.2m;
                var safety = pairConfig.TrailingSafety;
                if (safety != null && safety.MaxTrailingSpread > 0)
                {
                    baseSpread = safety.MaxTrailingSpread;
                }

                if (tradingPair.CurrentSpread > baseSpread && effectiveSellMargin > 0)
                {
                    decimal spreadExcess = tradingPair.CurrentSpread - baseSpread;
                    decimal volatilityDiscount = Math.Min(spreadExcess * 0.25m, effectiveSellMargin * 0.5m);
                    effectiveSellMargin -= volatilityDiscount;
                }

                if (pairConfig.SellEnabled && tradingPair.CurrentMargin >= effectiveSellMargin)
                {
                    if (task.LoggingEnabled && effectiveSellMargin != pairConfig.SellMargin)
                    {
                        loggingService.Info($"Target decay / profit protection triggered sell for {tradingPair.FormattedName}. " +
                            $"Margin: {tradingPair.CurrentMargin:0.00}, Base Target: {pairConfig.SellMargin:0.00}, Effective: {effectiveSellMargin:0.00}");
                    }
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
