import asyncio
import logging
from datetime import datetime, timedelta
from typing import Any, Dict, List
from magda_agent.memory.episodic import EpisodicMemory
from magda_agent.llm_client import LLMClient
from magda_agent.learning.online_rl_trading import OnlineRLTradingOptimizer

# Setup basic logging
logging.basicConfig(level=logging.INFO)

class MockLLM(LLMClient):
    """
    Mock LLM client to prevent real API calls and cost.
    """
    def __init__(self, response_text: str) -> None:
        super().__init__(api_key="mock_key")
        self.response_text = response_text
        self.call_count = 0

    async def chat_completion(self, messages: list[dict[str, str]], temperature: float = 0.7) -> str:
        self.call_count += 1
        return self.response_text

def approx_equal(val1: float, val2: float, tolerance: float = 1e-4) -> bool:
    """
    Helper function to compare floating point numbers with a tolerance.
    """
    return abs(val1 - val2) < tolerance

async def test_online_rl_trading_optimizer() -> None:
    """
    Tests the OnlineRLTradingOptimizer class for storing trades, parsing performance metrics,
    proposing parameters under different conditions, tracking history, and evaluating impact.
    """
    logging.info("Starting OnlineRLTradingOptimizer Unit Tests...")

    # 1. Initialize EpisodicMemory in ephemeral mode
    memory = EpisodicMemory(persist_directory=":memory:")
    optimizer = OnlineRLTradingOptimizer(episodic_memory=memory)

    # 2. Test Store and Retrieve Trades
    pair_btc = "BTC/USDT"
    pair_eth = "ETH/USDT"

    # Store base trades for BTC/USDT (high performance: 4 wins, 1 loss)
    now = datetime.utcnow()
    t1 = (now - timedelta(hours=10)).isoformat()
    t2 = (now - timedelta(hours=9)).isoformat()
    t3 = (now - timedelta(hours=8)).isoformat()
    t4 = (now - timedelta(hours=7)).isoformat()
    t5 = (now - timedelta(hours=6)).isoformat()

    optimizer.store_trade_event(pair_btc, 0.5, 2.0, 10.5, 100.0, True, metadata={"timestamp": t1})
    optimizer.store_trade_event(pair_btc, 0.5, 2.0, 12.0, 100.0, True, metadata={"timestamp": t2})
    optimizer.store_trade_event(pair_btc, 0.5, 2.0, 15.0, 100.0, True, metadata={"timestamp": t3})
    optimizer.store_trade_event(pair_btc, 0.5, 2.0, -5.0, 100.0, False, metadata={"timestamp": t4})
    optimizer.store_trade_event(pair_btc, 0.5, 2.0, 8.5, 100.0, True, metadata={"timestamp": t5})

    # Store base trades for ETH/USDT (poor performance: 1 win, 3 losses)
    et1 = (now - timedelta(hours=10)).isoformat()
    et2 = (now - timedelta(hours=9)).isoformat()
    et3 = (now - timedelta(hours=8)).isoformat()
    et4 = (now - timedelta(hours=7)).isoformat()

    optimizer.store_trade_event(pair_eth, 0.3, 1.5, -4.0, 50.0, False, metadata={"timestamp": et1})
    optimizer.store_trade_event(pair_eth, 0.3, 1.5, -6.0, 50.0, False, metadata={"timestamp": et2})
    optimizer.store_trade_event(pair_eth, 0.3, 1.5, 1.2, 50.0, True, metadata={"timestamp": et3})
    optimizer.store_trade_event(pair_eth, 0.3, 1.5, -3.0, 50.0, False, metadata={"timestamp": et4})

    # 3. Test get_completed_trades
    all_trades = optimizer.get_completed_trades()
    assert len(all_trades) == 9

    btc_trades = optimizer.get_completed_trades(pair=pair_btc)
    assert len(btc_trades) == 5
    assert btc_trades[0]["pair"] == pair_btc
    assert btc_trades[0]["profit"] == 10.5
    assert btc_trades[0]["buy_trailing"] == 0.5
    assert btc_trades[0]["sell_margin"] == 2.0

    eth_trades = optimizer.get_completed_trades(pair=pair_eth)
    assert len(eth_trades) == 4

    # 4. Test analyze_trade_performance
    btc_perf = optimizer.analyze_trade_performance(pair_btc)
    assert btc_perf["total_trades"] == 5
    assert btc_perf["successful_trades"] == 4
    assert btc_perf["win_rate"] == 0.8
    assert btc_perf["total_profit"] == 41.0
    assert btc_perf["avg_profit"] == 8.2
    assert btc_perf["avg_buy_trailing"] == 0.5
    assert btc_perf["avg_sell_margin"] == 2.0

    eth_perf = optimizer.analyze_trade_performance(pair_eth)
    assert eth_perf["total_trades"] == 4
    assert eth_perf["successful_trades"] == 1
    assert eth_perf["win_rate"] == 0.25
    assert eth_perf["total_profit"] == -11.8

    # Test empty trade pair performance
    empty_perf = optimizer.analyze_trade_performance("DOGE/USDT")
    assert empty_perf["total_trades"] == 0
    assert empty_perf["win_rate"] == 0.0

    # 5. Test propose_parameter_adjustments (Algorithmic / Heuristic Proposal)
    # High performing pair (BTC/USDT): should increase SellMargin and decrease BuyTrailing
    btc_proposal = await optimizer.propose_parameter_adjustments(pair_btc, 0.5, 2.0)
    assert btc_proposal["proposed_buy_trailing"] == 0.45
    assert btc_proposal["proposed_sell_margin"] == 2.2
    assert btc_proposal["method"] == "algorithmic"
    assert "Excellent performance" in btc_proposal["explanation"]

    # Poor performing pair (ETH/USDT): should decrease SellMargin and increase BuyTrailing
    eth_proposal = await optimizer.propose_parameter_adjustments(pair_eth, 0.3, 1.5)
    assert eth_proposal["proposed_buy_trailing"] == 0.35
    assert eth_proposal["proposed_sell_margin"] == 1.35
    assert eth_proposal["method"] == "algorithmic"
    assert "Sub-optimal performance" in eth_proposal["explanation"]

    # Empty history pair: should retain same parameters
    empty_proposal = await optimizer.propose_parameter_adjustments("DOGE/USDT", 0.5, 2.0)
    assert empty_proposal["proposed_buy_trailing"] == 0.5
    assert empty_proposal["proposed_sell_margin"] == 2.0
    assert "No trade history" in empty_proposal["explanation"]

    # 6. Test propose_parameter_adjustments with Mock LLM
    mock_llm_response = '{"proposed_buy_trailing": 0.42, "proposed_sell_margin": 2.25, "explanation": "LLM proposes 0.42 and 2.25 based on trend analysis"}'
    llm_client = MockLLM(mock_llm_response)
    optimizer_with_llm = OnlineRLTradingOptimizer(episodic_memory=memory, llm=llm_client)

    llm_proposal = await optimizer_with_llm.propose_parameter_adjustments(pair_btc, 0.5, 2.0)
    assert llm_client.call_count == 1
    assert llm_proposal["proposed_buy_trailing"] == 0.42
    assert llm_proposal["proposed_sell_margin"] == 2.25
    assert llm_proposal["method"] == "llm"
    assert "LLM proposes" in llm_proposal["explanation"]

    # Test LLM Error / Invalid JSON Fallback
    invalid_llm_response = 'This is not valid json'
    bad_llm_client = MockLLM(invalid_llm_response)
    optimizer_bad_llm = OnlineRLTradingOptimizer(episodic_memory=memory, llm=bad_llm_client)

    # Should fallback gracefully to algorithmic proposal without throwing an error
    fallback_proposal = await optimizer_bad_llm.propose_parameter_adjustments(pair_btc, 0.5, 2.0)
    assert bad_llm_client.call_count == 1
    assert fallback_proposal["proposed_buy_trailing"] == 0.45
    assert fallback_proposal["proposed_sell_margin"] == 2.2
    assert fallback_proposal["method"] == "algorithmic"

    # 7. Test Apply and Record Adjustment
    old_params = {"buy_trailing": 0.5, "sell_margin": 2.0}
    new_params = {"buy_trailing": 0.45, "sell_margin": 2.2}
    adj_reason = "Optimizer identified strong upward trend and high win-rate."
    optimizer.apply_and_record_adjustment(pair_btc, old_params, new_params, adj_reason)

    # Verify adjustment is stored
    events = memory.get_all_events(include_decayed=True)
    adj_events = [e for e in events if e.get("metadata", {}).get("type") == "parameter_adjustment"]
    assert len(adj_events) == 1
    assert adj_events[0]["metadata"]["pair"] == pair_btc
    assert adj_events[0]["metadata"]["new_buy_trailing"] == 0.45
    assert adj_events[0]["metadata"]["new_sell_margin"] == 2.2

    # 8. Test Evaluate Adjustment Impact
    # Create trades AFTER the adjustment (which was timestamped now)
    future_time_1 = (now + timedelta(hours=1)).isoformat()
    future_time_2 = (now + timedelta(hours=2)).isoformat()

    # Success: 2 wins after adjustment (100% win-rate)
    optimizer.store_trade_event(pair_btc, 0.45, 2.2, 18.0, 100.0, True, metadata={"timestamp": future_time_1})
    optimizer.store_trade_event(pair_btc, 0.45, 2.2, 22.0, 100.0, True, metadata={"timestamp": future_time_2})

    impact = optimizer.evaluate_adjustment_impact(pair_btc)
    assert impact["status"] == "evaluated"
    assert impact["before_adjustment"]["count"] == 5
    assert impact["before_adjustment"]["win_rate"] == 0.8
    assert impact["after_adjustment"]["count"] == 2
    assert impact["after_adjustment"]["win_rate"] == 1.0
    assert impact["improvement"]["is_improvement"] is True
    assert approx_equal(impact["improvement"]["win_rate_change"], 0.2)
    assert approx_equal(impact["improvement"]["total_profit_change"], 40.0 - 41.0) # 40.0 profit after, 41.0 before
    assert approx_equal(impact["improvement"]["avg_profit_change"], 20.0 - 8.2) # avg 20 after, 8.2 before

    # Test impact evaluation for a pair with no adjustments
    no_adj_impact = optimizer.evaluate_adjustment_impact(pair_eth)
    assert no_adj_impact["status"] == "no_adjustments_recorded"

    logging.info("OnlineRLTradingOptimizer Unit Tests Passed successfully!")

if __name__ == "__main__":
    asyncio.run(test_online_rl_trading_optimizer())
