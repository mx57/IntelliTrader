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
                decimal volatilityMultiplier = 1.0m;

                // 1. CurrentSpread relative to MaxTrailingSpread or 0.2% base
                decimal spreadBase = 0.2m;
                if (pairConfig.TrailingSafety?.MaxTrailingSpread > 0)
                {
                    spreadBase = pairConfig.TrailingSafety.MaxTrailingSpread;
                }

                if (spreadBase > 0)
                {
                    decimal spreadMultiplier = tradingPair.CurrentSpread / spreadBase;
                    if (spreadMultiplier > volatilityMultiplier)
                    {
                        volatilityMultiplier = spreadMultiplier;
                    }
                }

                // 2. Signal volatility relative to a 4.0 base
                double? maxSignalVolatility = null;
                var signals = signalsService.GetSignalsByPair(tradingPair.Pair);
                if (signals != null)
                {
                    foreach (var sig in signals)
                    {
                        if (sig.Volatility.HasValue && !double.IsNaN(sig.Volatility.Value) && !double.IsInfinity(sig.Volatility.Value))
                        {
                            if (maxSignalVolatility == null || sig.Volatility.Value > maxSignalVolatility.Value)
                            {
                                maxSignalVolatility = sig.Volatility.Value;
                            }
                        }
                    }
                }

                if (maxSignalVolatility.HasValue)
                {
                    decimal signalMultiplier = (decimal)(maxSignalVolatility.Value / 4.0);
                    if (signalMultiplier > volatilityMultiplier)
                    {
                        volatilityMultiplier = signalMultiplier;
                    }
                }

                // Limit the multiplier to [1.0, 5.0]
                if (volatilityMultiplier > 5.0m)
                {
                    volatilityMultiplier = 5.0m;
                }
                if (volatilityMultiplier < 1.0m)
                {
                    volatilityMultiplier = 1.0m;
                }

                decimal effectiveNextDCAMargin = pairConfig.NextDCAMargin.Value;
                if (effectiveNextDCAMargin < 0)
                {
                    effectiveNextDCAMargin *= volatilityMultiplier;
                }
                else
                {
                    effectiveNextDCAMargin /= volatilityMultiplier;
                }

                if (tradingPair.CurrentMargin <= effectiveNextDCAMargin)
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
                                $"Level: {pairConfig.NextDCAMargin:0.00}, Effective Level: {effectiveNextDCAMargin:0.00} (Volatility Multiplier: {volatilityMultiplier:0.00}x), " +
                                $"Multiplier: {pairConfig.BuyMultiplier}, " +
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
