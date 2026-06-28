using IntelliTrader.Core;
using IntelliTrader.Exchange.Base;
using ExchangeSharp;
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

        protected override ExchangeAPI InitializeApi()
        {
            return new ExchangeBinanceAPI();
        }

        public override IOrderDetails PlaceOrder(IOrder order)
        {
            var request = new ExchangeOrderRequest
            {
                Symbol = order.Pair,
                Amount = order.Amount,
                Price = order.Price,
                IsBuy = order.Side == IntelliTrader.Core.OrderSide.Buy,
                OrderType = order.Type == IntelliTrader.Core.OrderType.Limit ? ExchangeSharp.OrderType.Limit : ExchangeSharp.OrderType.Market
            };

            var result = Api.PlaceOrder(request);
            return MapOrderResult(result);
        }

        public override IEnumerable<IOrderDetails> GetTrades(string pair)
        {
            var results = Api.GetCompletedOrderDetails(pair);
            return results.Select(MapOrderResult);
        }

        private OrderDetails MapOrderResult(ExchangeOrderResult result)
        {
            return new OrderDetails
            {
                OrderId = result.OrderId,
                Result = MapOrderResult(result.Result),
                Message = result.Message,
                Amount = result.Amount,
                AmountFilled = result.AmountFilled,
                Price = result.Price,
                AveragePrice = result.AveragePrice,
                Date = result.OrderDate,
                Pair = result.Symbol,
                Side = result.IsBuy ? IntelliTrader.Core.OrderSide.Buy : IntelliTrader.Core.OrderSide.Sell,
                Fees = result.Fees,
                FeesCurrency = result.FeesCurrency
            };
        }

        private IntelliTrader.Core.OrderResult MapOrderResult(ExchangeAPIOrderResult result)
        {
            switch (result)
            {
                case ExchangeAPIOrderResult.Filled:
                    return IntelliTrader.Core.OrderResult.Filled;
                case ExchangeAPIOrderResult.FilledPartially:
                    return IntelliTrader.Core.OrderResult.FilledPartially;
                case ExchangeAPIOrderResult.Pending:
                    return IntelliTrader.Core.OrderResult.Pending;
                case ExchangeAPIOrderResult.Canceled:
                    return IntelliTrader.Core.OrderResult.Canceled;
                case ExchangeAPIOrderResult.Error:
                    return IntelliTrader.Core.OrderResult.Error;
                default:
                    return IntelliTrader.Core.OrderResult.Unknown;
            }
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
