using IntelliTrader.Core;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace IntelliTrader.Trading
{
    public class TradingTimedTask : HighResolutionTimedTask
    {
        public bool LoggingEnabled { get; set; } = true;

        private readonly ILoggingService loggingService;
        private readonly INotificationService notificationService;
        private readonly IHealthCheckService healthCheckService;
        private readonly ISignalsService signalsService;
        private readonly ITradingService tradingService;
        private readonly IOrderingService orderingService;

        private readonly ConcurrentDictionary<string, BuyTrailingInfo> trailingBuys = new ConcurrentDictionary<string, BuyTrailingInfo>();
        private readonly ConcurrentDictionary<string, SellTrailingInfo> trailingSells = new ConcurrentDictionary<string, SellTrailingInfo>();
        private readonly List<Processors.ITradingProcessor> processors;
        private readonly Processors.BuyProcessor buyProcessor;

        public TradingTimedTask(ILoggingService loggingService, INotificationService notificationService,
            IHealthCheckService healthCheckService, ISignalsService signalsService, IOrderingService orderingService, ITradingService tradingService)
        {
            this.loggingService = loggingService;
            this.notificationService = notificationService;
            this.healthCheckService = healthCheckService;
            this.signalsService = signalsService;
            this.orderingService = orderingService;
            this.tradingService = tradingService;

            this.processors = new List<Processors.ITradingProcessor>
            {
                new Processors.SellProcessor(loggingService, tradingService, orderingService, this),
                new Processors.DcaProcessor(loggingService, tradingService, this)
            };
            this.buyProcessor = new Processors.BuyProcessor(loggingService, tradingService, orderingService, this);
        }

        protected override void Run()
        {
            ProcessTradingPairs();
        }

        public void InitiateBuy(BuyOptions options)
        {
            IPairConfig pairConfig = tradingService.GetPairConfig(options.Pair);
            if (!options.ManualOrder && pairConfig.BuyTrailing != 0)
            {
                if (!trailingBuys.ContainsKey(options.Pair))
                {
                    StopTrailingSell(options.Pair);
                    decimal currentPrice = tradingService.GetPrice(options.Pair);
                    decimal currentMargin = 0;

                    var trailingInfo = new BuyTrailingInfo
                    {
                        BuyOptions = options,
                        Trailing = pairConfig.BuyTrailing,
                        TrailingStopMargin = pairConfig.BuyTrailingStopMargin,
                        TrailingStopAction = pairConfig.BuyTrailingStopAction,
                        InitialPrice = currentPrice,
                        LastTrailingMargin = currentMargin,
                        BestTrailingMargin = currentMargin
                    };

                    if (trailingBuys.TryAdd(options.Pair, trailingInfo))
                    {
                        if (LoggingEnabled)
                        {
                            ITradingPair tradingPair = tradingService.Account.GetTradingPair(options.Pair);
                            loggingService.Info($"Start trailing buy {tradingPair?.FormattedName ?? options.Pair}. " +
                                $"Price: {currentPrice:0.00000000}, Margin: {currentMargin:0.00}");
                        }
                    }
                }
            }
            else
            {
                orderingService.PlaceBuyOrder(options);
            }
        }

        public void InitiateSell(SellOptions options)
        {
            if (tradingService.Account.HasTradingPair(options.Pair))
            {
                IPairConfig pairConfig = tradingService.GetPairConfig(options.Pair);
                if (!options.ManualOrder && pairConfig.SellTrailing != 0)
                {
                    if (!trailingSells.ContainsKey(options.Pair))
                    {
                        StopTrailingBuy(options.Pair);
                        ITradingPair tradingPair = tradingService.Account.GetTradingPair(options.Pair);
                        tradingPair.SetCurrentValues(tradingService.GetPrice(options.Pair), tradingService.Exchange.GetPriceSpread(options.Pair));

                        decimal effectiveSellMargin = pairConfig.SellMargin;
                        if (pairConfig.SellMarginDecay.HasValue && pairConfig.SellMarginDecayInterval.HasValue && pairConfig.SellMarginDecayInterval.Value > 0)
                        {
                            int intervals = (int)(tradingPair.CurrentAge / pairConfig.SellMarginDecayInterval.Value);
                            effectiveSellMargin -= intervals * pairConfig.SellMarginDecay.Value;
                        }

                        var trailingInfo = new SellTrailingInfo
                        {
                            SellOptions = options,
                            SellMargin = effectiveSellMargin,
                            Trailing = pairConfig.SellTrailing,
                            TrailingStopMargin = pairConfig.SellTrailingStopMargin,
                            TrailingStopAction = pairConfig.SellTrailingStopAction,
                            InitialPrice = tradingPair.CurrentPrice,
                            LastTrailingMargin = tradingPair.CurrentMargin,
                            BestTrailingMargin = tradingPair.CurrentMargin
                        };

                        if (trailingSells.TryAdd(options.Pair, trailingInfo))
                        {
                            if (LoggingEnabled)
                            {
                                loggingService.Info($"Start trailing sell {tradingPair.FormattedName}. " +
                                    $"Price: {tradingPair.CurrentPrice:0.00000000}, Margin: {tradingPair.CurrentMargin:0.00}");
                            }
                        }
                    }
                }
                else
                {
                    orderingService.PlaceSellOrder(options);
                }
            }
            else
            {
                loggingService.Info($"Cancel initiate sell for {options.Pair}. Reason: pair does not exist");
            }
        }

        public void ProcessTradingPairs()
        {
            int traidingPairsCount = 0;

            foreach (var tradingPair in tradingService.Account.GetTradingPairs())
            {
                IPairConfig pairConfig = tradingService.GetPairConfig(tradingPair.Pair);
                tradingPair.SetCurrentValues(tradingService.GetPrice(tradingPair.Pair), tradingService.Exchange.GetPriceSpread(tradingPair.Pair));
                tradingPair.Metadata.TradingRules = pairConfig.Rules.ToList();
                tradingPair.Metadata.CurrentRating = tradingPair.Metadata.Signals != null ? signalsService.GetRating(tradingPair.Pair, tradingPair.Metadata.Signals) : null;
                tradingPair.Metadata.CurrentGlobalRating = signalsService.GetGlobalRating();

                foreach (var processor in processors)
                {
                    processor.Process(tradingPair, pairConfig, trailingBuys, trailingSells);
                }

                traidingPairsCount++;
            }

            foreach (var kvp in trailingBuys)
            {
                buyProcessor.Process(kvp.Key, kvp.Value, trailingBuys);
            }

            healthCheckService.UpdateHealthCheck(Constants.HealthChecks.TradingPairsProcessed,
                $"Pairs: {traidingPairsCount}, Trailing buys: {trailingBuys.Count}, Trailing sells: {trailingSells.Count}");
        }

        public List<string> GetTrailingBuys()
        {
            return trailingBuys.Keys.ToList();
        }

        public List<string> GetTrailingSells()
        {
            return trailingSells.Keys.ToList();
        }

        public void StopTrailing()
        {
            trailingBuys.Clear();
            trailingSells.Clear();
        }

        public void StopTrailingBuy(string pair)
        {
            trailingBuys.TryRemove(pair, out BuyTrailingInfo buyTrailingInfo);
        }

        public void StopTrailingSell(string pair)
        {
            trailingSells.TryRemove(pair, out SellTrailingInfo sellTrailingInfo);
        }
    }
}
