using IntelliTrader.Core;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace IntelliTrader.Rules
{
    internal class RulesService : ConfigrableServiceBase<RulesConfig>, IRulesService
    {
        public override string ServiceName => Constants.ServiceNames.RulesService;

        IRulesConfig IRulesService.Config => Config;

        private readonly ILoggingService loggingService;
        private readonly ITradingService tradingService;
        private readonly List<Action> rulesChangeCallbacks = new List<Action>();
        private readonly List<IConditionEvaluator> evaluators;

        public RulesService(ILoggingService loggingService, ITradingService tradingService)
        {
            this.loggingService = loggingService;
            this.tradingService = tradingService;
            this.evaluators = new List<IConditionEvaluator>
            {
                new PriceSpreadEvaluator(),
                new ArbitrageEvaluator(tradingService),
                new SignalEvaluator(),
                new RatingEvaluator(),
                new PairEvaluator(),
                new AgeEvaluator(),
                new MarginEvaluator(),
                new AmountCostEvaluator(),
                new DCALevelEvaluator(),
                new SignalRuleEvaluator()
            };
        }

        public IModuleRules GetRules(string module)
        {
            IModuleRules moduleRules = Config.Modules.FirstOrDefault(m => m.Module == module);
            if (moduleRules != null)
            {
                return moduleRules;
            }
            else
            {
                throw new Exception($"Unable to find rules for {module}");
            }
        }

        public bool CheckConditions(IEnumerable<IRuleCondition> conditions, Dictionary<string, ISignal> signals, double? globalRating, string pair, ITradingPair tradingPair)
        {
            if (conditions != null && conditions.Any())
            {
                decimal currentPrice = tradingService.GetPrice(pair);
                decimal currentSpread = tradingService.Exchange.GetPriceSpread(pair);

                foreach (var condition in conditions)
                {
                    foreach (var evaluator in evaluators)
                    {
                        if (!evaluator.Evaluate(condition, signals, globalRating, pair, tradingPair, currentPrice, currentSpread))
                        {
                            return false;
                        }
                    }
                }
            }
            return true;
        }

        public void RegisterRulesChangeCallback(Action callback)
        {
            rulesChangeCallbacks.Add(callback);
        }

        public void UnregisterRulesChangeCallback(Action callback)
        {
            rulesChangeCallbacks.Remove(callback);
        }

        protected override void OnConfigReloaded()
        {
            foreach (var callback in rulesChangeCallbacks)
            {
                callback();
            }
        }
    }
}
