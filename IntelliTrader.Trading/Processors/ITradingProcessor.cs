using IntelliTrader.Core;
using System.Collections.Concurrent;

namespace IntelliTrader.Trading.Processors
{
    internal interface ITradingProcessor
    {
        void Process(ITradingPair tradingPair, IPairConfig pairConfig, ConcurrentDictionary<string, BuyTrailingInfo> trailingBuys, ConcurrentDictionary<string, SellTrailingInfo> trailingSells);
    }
}
