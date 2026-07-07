using IntelliTrader.Core;
using System;
using System.Collections.Concurrent;

namespace IntelliTrader.Trading.Processors
{
    internal class DcaProcessor : ITradingProcessor
    {
        private readonly ILoggingService loggingService;
        private readonly ITradingService tradingService;
        private readonly TradingTimedTask task;

        public DcaProcessor(ILoggingService loggingService, ITradingService tradingService, TradingTimedTask task)
        {
            this.loggingService = loggingService;
            this.tradingService = tradingService;
            this.task = task;
        }

        public void Process(ITradingPair tradingPair, IPairConfig pairConfig, ConcurrentDictionary<string, BuyTrailingInfo> trailingBuys, ConcurrentDictionary<string, SellTrailingInfo> trailingSells)
        {
            if (pairConfig.NextDCAMargin != null && pairConfig.BuyEnabled &&
                !trailingBuys.ContainsKey(tradingPair.Pair) && !trailingSells.ContainsKey(tradingPair.Pair))
            {
                if (tradingPair.CurrentMargin <= pairConfig.NextDCAMargin)
                {
                    var buyOptions = new BuyOptions(tradingPair.Pair)
                    {
                        MaxCost = tradingPair.Cost * pairConfig.BuyMultiplier,
                        IgnoreExisting = true
                    };

                    if (tradingService.CanBuy(buyOptions, message: out string message))
                    {
                        if (task.LoggingEnabled)
                        {
                            loggingService.Info($"DCA triggered for {tradingPair.FormattedName}. Margin: {tradingPair.CurrentMargin:0.00}, " +
                                $"Level: {pairConfig.NextDCAMargin:0.00}, Multiplier: {pairConfig.BuyMultiplier}");
                        }
                        task.InitiateBuy(buyOptions);
                    }
                }
            }
        }
    }
}
