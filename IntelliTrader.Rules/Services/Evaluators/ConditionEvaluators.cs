using IntelliTrader.Core;
using System.Collections.Generic;

namespace IntelliTrader.Rules
{
    internal class PriceSpreadEvaluator : IConditionEvaluator
    {
        public bool Evaluate(IRuleCondition condition, Dictionary<string, ISignal> signals, double? globalRating, string pair, ITradingPair tradingPair, decimal currentPrice, decimal currentSpread)
        {
            if (condition.MinPrice != null && (currentPrice < condition.MinPrice)) return false;
            if (condition.MaxPrice != null && (currentPrice > condition.MaxPrice)) return false;
            if (condition.MinSpread != null && (currentSpread < condition.MinSpread)) return false;
            if (condition.MaxSpread != null && (currentSpread > condition.MaxSpread)) return false;
            return true;
        }
    }

    internal class ArbitrageEvaluator : IConditionEvaluator
    {
        private readonly ITradingService tradingService;

        public ArbitrageEvaluator(ITradingService tradingService)
        {
            this.tradingService = tradingService;
        }

        public bool Evaluate(IRuleCondition condition, Dictionary<string, ISignal> signals, double? globalRating, string pair, ITradingPair tradingPair, decimal currentPrice, decimal currentSpread)
        {
            if (condition.MinArbitrage == null && condition.MaxArbitrage == null) return true;

            var markets = condition.ArbitrageMarket != null ? new List<ArbitrageMarket> { condition.ArbitrageMarket.Value } : null;

            // Optimization: Cache GetArbitrage result to avoid redundant expensive exchange calls when both min and max thresholds are defined.
            var arbitrage = tradingService.Exchange.GetArbitrage(pair, tradingService.Config.Market, markets, condition.ArbitrageType);

            if (condition.MinArbitrage != null && arbitrage.Percentage < condition.MinArbitrage)
                return false;

            if (condition.MaxArbitrage != null && arbitrage.Percentage > condition.MaxArbitrage)
                return false;

            return true;
        }
    }

    internal class SignalEvaluator : IConditionEvaluator
    {
        public bool Evaluate(IRuleCondition condition, Dictionary<string, ISignal> signals, double? globalRating, string pair, ITradingPair tradingPair, decimal currentPrice, decimal currentSpread)
        {
            ISignal signal = null;
            if (condition.Signal != null && signals.TryGetValue(condition.Signal, out ISignal s))
            {
                signal = s;
            }

            if (condition.MinVolume != null && (signal == null || signal.Volume == null || signal.Volume < condition.MinVolume)) return false;
            if (condition.MaxVolume != null && (signal == null || signal.Volume == null || signal.Volume > condition.MaxVolume)) return false;
            if (condition.MinVolumeChange != null && (signal == null || signal.VolumeChange == null || signal.VolumeChange < condition.MinVolumeChange)) return false;
            if (condition.MaxVolumeChange != null && (signal == null || signal.VolumeChange == null || signal.VolumeChange > condition.MaxVolumeChange)) return false;
            if (condition.MinPriceChange != null && (signal == null || signal.PriceChange == null || signal.PriceChange < condition.MinPriceChange)) return false;
            if (condition.MaxPriceChange != null && (signal == null || signal.PriceChange == null || signal.PriceChange > condition.MaxPriceChange)) return false;
            if (condition.MinRating != null && (signal == null || signal.Rating == null || signal.Rating < condition.MinRating)) return false;
            if (condition.MaxRating != null && (signal == null || signal.Rating == null || signal.Rating > condition.MaxRating)) return false;
            if (condition.MinRatingChange != null && (signal == null || signal.RatingChange == null || signal.RatingChange < condition.MinRatingChange)) return false;
            if (condition.MaxRatingChange != null && (signal == null || signal.RatingChange == null || signal.RatingChange > condition.MaxRatingChange)) return false;
            if (condition.MinVolatility != null && (signal == null || signal.Volatility == null || signal.Volatility < condition.MinVolatility)) return false;
            if (condition.MaxVolatility != null && (signal == null || signal.Volatility == null || signal.Volatility > condition.MaxVolatility)) return false;

            return true;
        }
    }

    internal class RatingEvaluator : IConditionEvaluator
    {
        public bool Evaluate(IRuleCondition condition, Dictionary<string, ISignal> signals, double? globalRating, string pair, ITradingPair tradingPair, decimal currentPrice, decimal currentSpread)
        {
            if (condition.MinGlobalRating != null && (globalRating == null || globalRating < condition.MinGlobalRating)) return false;
            if (condition.MaxGlobalRating != null && (globalRating == null || globalRating > condition.MaxGlobalRating)) return false;
            return true;
        }
    }

    internal class PairEvaluator : IConditionEvaluator
    {
        public bool Evaluate(IRuleCondition condition, Dictionary<string, ISignal> signals, double? globalRating, string pair, ITradingPair tradingPair, decimal currentPrice, decimal currentSpread)
        {
            if (condition.Pairs != null && (pair == null || !condition.Pairs.Contains(pair))) return false;
            if (condition.NotPairs != null && (pair == null || condition.NotPairs.Contains(pair))) return false;
            return true;
        }
    }

    internal class AgeEvaluator : IConditionEvaluator
    {
        public bool Evaluate(IRuleCondition condition, Dictionary<string, ISignal> signals, double? globalRating, string pair, ITradingPair tradingPair, decimal currentPrice, decimal currentSpread)
        {
            if (condition.MinAge != null && (tradingPair == null || tradingPair.CurrentAge < condition.MinAge / Application.Speed)) return false;
            if (condition.MaxAge != null && (tradingPair == null || tradingPair.CurrentAge > condition.MaxAge / Application.Speed)) return false;
            if (condition.MinLastBuyAge != null && (tradingPair == null || tradingPair.LastBuyAge < condition.MinLastBuyAge / Application.Speed)) return false;
            if (condition.MaxLastBuyAge != null && (tradingPair == null || tradingPair.LastBuyAge > condition.MaxLastBuyAge / Application.Speed)) return false;
            return true;
        }
    }

    internal class MarginEvaluator : IConditionEvaluator
    {
        public bool Evaluate(IRuleCondition condition, Dictionary<string, ISignal> signals, double? globalRating, string pair, ITradingPair tradingPair, decimal currentPrice, decimal currentSpread)
        {
            if (condition.MinMargin != null && (tradingPair == null || tradingPair.CurrentMargin < condition.MinMargin)) return false;
            if (condition.MaxMargin != null && (tradingPair == null || tradingPair.CurrentMargin > condition.MaxMargin)) return false;
            if (condition.MinMarginChange != null && (tradingPair == null || tradingPair.Metadata.LastBuyMargin == null || (tradingPair.CurrentMargin - tradingPair.Metadata.LastBuyMargin) < condition.MinMarginChange)) return false;
            if (condition.MaxMarginChange != null && (tradingPair == null || tradingPair.Metadata.LastBuyMargin == null || (tradingPair.CurrentMargin - tradingPair.Metadata.LastBuyMargin) > condition.MaxMarginChange)) return false;
            return true;
        }
    }

    internal class AmountCostEvaluator : IConditionEvaluator
    {
        public bool Evaluate(IRuleCondition condition, Dictionary<string, ISignal> signals, double? globalRating, string pair, ITradingPair tradingPair, decimal currentPrice, decimal currentSpread)
        {
            if (condition.MinAmount != null && (tradingPair == null || tradingPair.Amount < condition.MinAmount)) return false;
            if (condition.MaxAmount != null && (tradingPair == null || tradingPair.Amount > condition.MaxAmount)) return false;
            if (condition.MinCost != null && (tradingPair == null || tradingPair.CurrentCost < condition.MinCost)) return false;
            if (condition.MaxCost != null && (tradingPair == null || tradingPair.CurrentCost > condition.MaxCost)) return false;
            return true;
        }
    }

    internal class DCALevelEvaluator : IConditionEvaluator
    {
        public bool Evaluate(IRuleCondition condition, Dictionary<string, ISignal> signals, double? globalRating, string pair, ITradingPair tradingPair, decimal currentPrice, decimal currentSpread)
        {
            if (condition.MinDCALevel != null && (tradingPair == null || tradingPair.DCALevel < condition.MinDCALevel)) return false;
            if (condition.MaxDCALevel != null && (tradingPair == null || tradingPair.DCALevel > condition.MaxDCALevel)) return false;
            return true;
        }
    }

    internal class SignalRuleEvaluator : IConditionEvaluator
    {
        public bool Evaluate(IRuleCondition condition, Dictionary<string, ISignal> signals, double? globalRating, string pair, ITradingPair tradingPair, decimal currentPrice, decimal currentSpread)
        {
            if (condition.SignalRules != null && (tradingPair == null || tradingPair.Metadata.SignalRule == null || !condition.SignalRules.Contains(tradingPair.Metadata.SignalRule))) return false;
            if (condition.NotSignalRules != null && (tradingPair == null || tradingPair.Metadata.SignalRule == null || condition.NotSignalRules.Contains(tradingPair.Metadata.SignalRule))) return false;
            return true;
        }
    }
}
