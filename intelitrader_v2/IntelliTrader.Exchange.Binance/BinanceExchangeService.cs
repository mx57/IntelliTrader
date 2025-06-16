using IntelliTrader.Core;
using IntelliTrader.Exchange.Base;
using System;
using System.Linq;
using System.Collections.Generic;
using IntelliTrader.Exchange.Base.Services;

namespace IntelliTrader.Exchange.Binance
{
    internal class BinanceExchangeService : ExchangeService
    {
        public BinanceExchangeService(ILoggingService loggingService, IHealthCheckService healthCheckService, ITasksService tasksService) :
            base(loggingService, healthCheckService, tasksService)
        {

        }

        // Removed: protected override ExchangeAPI InitializeApi()


        public override IOrderDetails PlaceOrder(IOrder order)
        {
            // TODO: Implement actual order placement using a new API or method
            throw new NotImplementedException("Order placement is not yet implemented.");
        }

        public override IEnumerable<IOrderDetails> GetTrades(string pair)
        {
            var myTrades = new List<OrderDetails>();
            // TODO: var results = ((ExchangeBinanceAPI)Api).GetMyTrades(pair);
            // TODO: foreach (var result in results) {
            // TODO:     myTrades.Add(new OrderDetails {
            // TODO:         Side = result.IsBuy ? OrderSide.Buy : OrderSide.Sell,
            // TODO:         Result = (OrderResult)(int)result.Result,
            // TODO:         Date = result.OrderDate,
            // TODO:         OrderId = result.OrderId,
            // TODO:         Pair = result.Symbol,
            // TODO:         Message = result.Message,
            // TODO:         Amount = result.Amount,
            // TODO:         AmountFilled = result.AmountFilled,
            // TODO:         Price = result.Price,
            // TODO:         AveragePrice = result.AveragePrice,
            // TODO:         Fees = result.Fees,
            // TODO:         FeesCurrency = result.FeesCurrency
            // TODO:     });
            // TODO: }

            return myTrades;
        }

        public override Arbitrage GetArbitrage(string pair, string tradingMarket, List<ArbitrageMarket> arbitrageMarkets = null, ArbitrageType? arbitrageType = null)
        {
            if (arbitrageMarkets == null || !arbitrageMarkets.Any())
            {
                arbitrageMarkets = new List<ArbitrageMarket> { ArbitrageMarket.ETH, ArbitrageMarket.BNB, ArbitrageMarket.USDT };
            }
            Arbitrage arbitrage = new Arbitrage
            {
                Market = arbitrageMarkets.First(),
                Type = arbitrageType ?? ArbitrageType.Direct
            };

            try
            {
                if (tradingMarket == Constants.Markets.BTC)
                {
                    foreach (var market in arbitrageMarkets)
                    {
                        string marketPair = ChangeMarket(pair, market.ToString());
                        string arbitragePair = GetArbitrageMarketPair(market);

                        if (marketPair != pair &&
                            Tickers.TryGetValue(pair, out Ticker pairTicker) &&
                            Tickers.TryGetValue(marketPair, out Ticker marketTicker) &&
                            Tickers.TryGetValue(arbitragePair, out Ticker arbitrageTicker))
                        {
                            decimal directArbitragePercentage = 0;
                            decimal reverseArbitragePercentage = 0;

                            if (market == ArbitrageMarket.ETH)
                            {
                                directArbitragePercentage = (1 / pairTicker.AskPrice * marketTicker.BidPrice * arbitrageTicker.BidPrice - 1) * 100;
                                reverseArbitragePercentage = (1 / arbitrageTicker.AskPrice / marketTicker.AskPrice * pairTicker.BidPrice - 1) * 100;
                            }
                            else if (market == ArbitrageMarket.BNB)
                            {
                                directArbitragePercentage = (1 / pairTicker.AskPrice * marketTicker.BidPrice * arbitrageTicker.BidPrice - 1) * 100;
                                reverseArbitragePercentage = (1 / arbitrageTicker.AskPrice / marketTicker.AskPrice * pairTicker.BidPrice - 1) * 100;
                            }
                            else if (market == ArbitrageMarket.USDT)
                            {
                                directArbitragePercentage = (1 / pairTicker.AskPrice * marketTicker.BidPrice / arbitrageTicker.AskPrice - 1) * 100;
                                reverseArbitragePercentage = (arbitrageTicker.BidPrice / marketTicker.AskPrice * pairTicker.BidPrice - 1) * 100;
                            }

                            if ((directArbitragePercentage > arbitrage.Percentage || !arbitrage.IsAssigned) && (arbitrageType == null || arbitrageType == ArbitrageType.Direct))
                            {
                                arbitrage.IsAssigned = true;
                                arbitrage.Market = market;
                                arbitrage.Type = ArbitrageType.Direct;
                                arbitrage.Percentage = directArbitragePercentage;
                            }

                            if ((reverseArbitragePercentage > arbitrage.Percentage || !arbitrage.IsAssigned) && (arbitrageType == null || arbitrageType == ArbitrageType.Reverse))
                            {
                                arbitrage.IsAssigned = true;
                                arbitrage.Market = market;
                                arbitrage.Type = ArbitrageType.Reverse;
                                arbitrage.Percentage = reverseArbitragePercentage;
                            }
                        }
                    }
                }
            }
            catch { }
            return arbitrage;
        }

        public override string GetArbitrageMarketPair(ArbitrageMarket arbitrageMarket)
        {
            if (arbitrageMarket == ArbitrageMarket.ETH)
            {
                return Constants.Markets.ETH + Constants.Markets.BTC;
            }
            else if (arbitrageMarket == ArbitrageMarket.BNB)
            {
                return Constants.Markets.BNB + Constants.Markets.BTC;
            }
            else if (arbitrageMarket == ArbitrageMarket.USDT)
            {
                return Constants.Markets.BTC + Constants.Markets.USDT;
            }
            else
            {
                throw new NotSupportedException($"Unsupported arbitrage market: {arbitrageMarket}");
            }
        }
    }
}
