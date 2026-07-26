import sys
import os
import logging

# Ensure repo root is in python path so we can import magda_agent correctly
sys.path.insert(0, os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "..", "app", "magda_agent_system")))
sys.path.insert(0, os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "magda_agent_system")))
sys.path.insert(0, os.path.abspath("magda_agent_system"))

from magda_agent.memory.episodic import EpisodicMemory
from magda_agent.learning.online_rl_trading import OnlineRLTradingOptimizer

def run_rl_simulation() -> None:
    """
    Simulates a reinforcement learning run to verify that OnlineRLTradingOptimizer
    correctly shifts parameters towards more profitable zones and records adjustment events.
    """
    logging.basicConfig(level=logging.INFO)
    logging.info("Starting Self-Created RL Trading Optimization Simulation...")

    # 1. Initialize memory system and optimizer
    memory = EpisodicMemory(persist_directory=":memory:")
    optimizer = OnlineRLTradingOptimizer(episodic_memory=memory, learning_rate=0.2)

    # 2. Test initial exploration (when no trades exist)
    initial_res = optimizer.analyze_and_optimize(
        pair="BTC/USDT",
        current_buy_trailing=0.5,
        current_sell_margin=2.0,
        explore=True
    )
    logging.info(f"Exploration result (no trades): {initial_res}")
    assert initial_res["status"] == "explored"
    assert initial_res["BuyTrailing"] != 0.5 or initial_res["SellMargin"] != 2.0

    # 3. Simulate trades where higher SellMargin and higher BuyTrailing are profitable
    # Trade 1: BT=0.5, SM=2.0 -> Profit = -1.0% (unprofitable)
    optimizer.record_trade("BTC/USDT", buy_trailing=0.5, sell_margin=2.0, profit_pct=-1.0)
    # Trade 2: BT=1.0, SM=4.0 -> Profit = +3.5% (very profitable)
    optimizer.record_trade("BTC/USDT", buy_trailing=1.0, sell_margin=4.0, profit_pct=3.5)
    # Trade 3: BT=0.8, SM=3.0 -> Profit = +1.5% (profitable)
    optimizer.record_trade("BTC/USDT", buy_trailing=0.8, sell_margin=3.0, profit_pct=1.5)

    # 4. Run optimization
    # Starting from low values (BT=0.5, SM=2.0), the optimizer should shift them UPWARDS
    # because higher values (1.0, 4.0) were highly profitable.
    opt_res = optimizer.analyze_and_optimize(
        pair="BTC/USDT",
        current_buy_trailing=0.5,
        current_sell_margin=2.0,
        explore=False  # Disable exploration noise to verify exact mathematical gradient shift
    )
    logging.info(f"Optimization result: {opt_res}")

    assert opt_res["status"] == "optimized"
    assert opt_res["trades_analyzed"] == 3
    # Check that BuyTrailing and SellMargin shifted upwards
    assert opt_res["BuyTrailing"] > 0.5, f"Expected BuyTrailing to increase above 0.5, got {opt_res['BuyTrailing']}"
    assert opt_res["SellMargin"] > 2.0, f"Expected SellMargin to increase above 2.0, got {opt_res['SellMargin']}"

    # 5. Check that adjustment history is recorded as an event in EpisodicMemory
    all_events = memory.get_all_events()
    adjust_events = [ev for ev in all_events if ev.get("metadata", {}).get("type") == "parameter_adjustment"]

    logging.info(f"Recorded adjustment events: {adjust_events}")
    assert len(adjust_events) >= 1, "No parameter adjustment events were recorded!"
    # Ensure the latest adjustment is recorded correctly
    assert adjust_events[-1]["metadata"]["pair"] == "BTC/USDT"
    assert adjust_events[-1]["metadata"]["old_BuyTrailing"] == 0.5
    assert adjust_events[-1]["metadata"]["new_BuyTrailing"] == opt_res["BuyTrailing"]

    logging.info("RL Trading Optimization Simulation completed successfully with all assertions passing!")

def test_rl_trading() -> None:
    """Pytest entrypoint wrapper."""
    run_rl_simulation()
