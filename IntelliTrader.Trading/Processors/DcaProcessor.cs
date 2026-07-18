using IntelliTrader.Core;
using System;
using System.Collections.Concurrent;

namespace IntelliTrader.Trading.Processors
{
    internal class DcaProcessor : ITradingProcessor
    {
        private readonly ILoggingService loggingService;
        private readonly ITradingService tradingService;
        private readonly ISignalsService signalsService;
        private readonly TradingTimedTask task;

        public DcaProcessor(ILoggingService loggingService, ITradingService tradingService, ISignalsService signalsService, TradingTimedTask task)
        {
            this.loggingService = loggingService;
            this.tradingService = tradingService;
            this.signalsService = signalsService;
            this.task = task;
        }

        public void Process(ITradingPair tradingPair, IPairConfig pairConfig, ConcurrentDictionary<string, BuyTrailingInfo> trailingBuys, ConcurrentDictionary<string, SellTrailingInfo> trailingSells)
        {
            if (pairConfig.NextDCAMargin != null && pairConfig.BuyEnabled &&
                !trailingBuys.ContainsKey(tradingPair.Pair) && !trailingSells.ContainsKey(tradingPair.Pair))
            {
                if (tradingPair.CurrentMargin <= pairConfig.NextDCAMargin)
                {
                    // Enforce MaxTrailingSpread safety checks to prevent buying on high-volatility spikes
                    var safety = pairConfig.TrailingSafety;
                    if (safety != null && safety.MaxTrailingSpread > 0 && tradingPair.CurrentSpread > safety.MaxTrailingSpread)
                    {
                        if (safety.PauseOnHighSpread)
                        {
                            if (task.LoggingEnabled)
                            {
                                loggingService.Info($"DCA paused for {tradingPair.FormattedName} due to high spread: {tradingPair.CurrentSpread:0.00}%");
                            }
                            return;
                        }
                    }

                    // Dynamically scale DCA orders based on the global rating
                    double? globalRating = signalsService.GetGlobalRating();
                    decimal scalingFactor = 1.0m;
                    if (globalRating.HasValue)
                    {
                        double ratingVal = globalRating.Value;
                        if (Math.Abs(ratingVal) > 1.0)
                        {
                            ratingVal = ratingVal / 100.0;
                        }
                        scalingFactor = Math.Max(0.1m, (decimal)(1.0 + ratingVal));
                    }

                    var buyOptions = new BuyOptions(tradingPair.Pair)
                    {
                        MaxCost = tradingPair.Cost * pairConfig.BuyMultiplier * scalingFactor,
                        IgnoreExisting = true,
                        Metadata = new OrderMetadata
                        {
                            BoughtGlobalRating = globalRating
                        }
                    };

                    if (tradingService.CanBuy(buyOptions, message: out string message))
                    {
                        if (task.LoggingEnabled)
                        {
                            loggingService.Info($"DCA triggered for {tradingPair.FormattedName}. Margin: {tradingPair.CurrentMargin:0.00}, " +
                                $"Level: {pairConfig.NextDCAMargin:0.00}, Multiplier: {pairConfig.BuyMultiplier}, " +
                                $"Global Rating: {(globalRating.HasValue ? globalRating.Value.ToString("0.00") : "N/A")}, " +
                                $"Scaling Factor: {scalingFactor:0.00}, Base Cost: {tradingPair.Cost * pairConfig.BuyMultiplier:0.00}, Scaled Cost: {buyOptions.MaxCost:0.00}");
                        }
                        task.InitiateBuy(buyOptions);
                    }
                }
            }
        }
    }
}
