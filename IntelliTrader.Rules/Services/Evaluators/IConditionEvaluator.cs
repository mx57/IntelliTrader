using IntelliTrader.Core;
using System.Collections.Generic;

namespace IntelliTrader.Rules
{
    internal interface IConditionEvaluator
    {
        bool Evaluate(IRuleCondition condition, Dictionary<string, ISignal> signals, double? globalRating, string pair, ITradingPair tradingPair, decimal currentPrice, decimal currentSpread);
    }
}
