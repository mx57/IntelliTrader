import logging
import re
import random
from typing import Optional, Dict, Any, List, Tuple
from magda_agent.memory.episodic import EpisodicMemory

class OnlineRLTradingOptimizer:
    """
    OnlineRLTradingOptimizer implements a reinforcement learning feedback loop
    to optimize trading parameters (BuyTrailing and SellMargin) based on trade success.

    It retrieves historical trade events from EpisodicMemory, evaluates their profit/ROI,
    uses a Policy Gradient (REINFORCE) continuous parameter update algorithm to shift
    parameters towards more profitable zones, and records the parameter adjustment
    history back into EpisodicMemory as episodic events.
    """

    def __init__(
        self,
        episodic_memory: EpisodicMemory,
        learning_rate: float = 0.1,
        min_buy_trailing: float = 0.05,
        max_buy_trailing: float = 2.0,
        min_sell_margin: float = 0.1,
        max_sell_margin: float = 10.0
    ) -> None:
        """
        Initializes the OnlineRLTradingOptimizer.

        Args:
            episodic_memory (EpisodicMemory): The episodic memory system for storing and retrieving events.
            learning_rate (float): The learning rate/step size for reinforcement learning updates.
            min_buy_trailing (float): The minimum safe value for BuyTrailing.
            max_buy_trailing (float): The maximum safe value for BuyTrailing.
            min_sell_margin (float): The minimum safe value for SellMargin.
            max_sell_margin (float): The maximum safe value for SellMargin.
        """
        self.episodic_memory: EpisodicMemory = episodic_memory
        self.learning_rate: float = learning_rate
        self.min_buy_trailing: float = min_buy_trailing
        self.max_buy_trailing: float = max_buy_trailing
        self.min_sell_margin: float = min_sell_margin
        self.max_sell_margin: float = max_sell_margin
        logging.info("OnlineRLTradingOptimizer initialized successfully.")

    def record_trade(
        self,
        pair: str,
        buy_trailing: float,
        sell_margin: float,
        profit_pct: float,
        metadata: Optional[Dict[str, Any]] = None
    ) -> None:
        """
        Records a completed trade into EpisodicMemory.

        Args:
            pair (str): The trading pair (e.g., BTC/USDT).
            buy_trailing (float): The BuyTrailing parameter used for this trade.
            sell_margin (float): The SellMargin parameter used for this trade.
            profit_pct (float): The realized profit percentage of the trade.
            metadata (Optional[Dict[str, Any]]): Additional trade-related metadata.
        """
        text = (
            f"Completed trade on {pair}: profit {profit_pct:+.2f}%, "
            f"BuyTrailing={buy_trailing:.3f}, SellMargin={sell_margin:.3f}"
        )

        meta = {
            "type": "trade",
            "pair": pair,
            "BuyTrailing": buy_trailing,
            "SellMargin": sell_margin,
            "profit_pct": profit_pct,
            "decayed": False
        }
        if metadata:
            meta.update(metadata)

        self.episodic_memory.store_event(text, metadata=meta)
        logging.info(f"OnlineRLTradingOptimizer: Recorded trade event: {text}")

    def _parse_trade_from_text(self, text: str) -> Tuple[Optional[float], Optional[float], Optional[float]]:
        """
        Helper method to parse trade parameters and profit from free text.

        Args:
            text (str): The episodic event text.

        Returns:
            Tuple[Optional[float], Optional[float], Optional[float]]:
                (profit_pct, buy_trailing, sell_margin) values if parsed, else None.
        """
        profit_pct: Optional[float] = None
        buy_trailing: Optional[float] = None
        sell_margin: Optional[float] = None

        # Regex for profit
        profit_match = re.search(r"profit\s*[:=]?\s*([+-]?\d+(?:\.\d+)?)%?", text, re.IGNORECASE)
        if profit_match:
            try:
                profit_pct = float(profit_match.group(1))
            except ValueError:
                pass

        # Regex for BuyTrailing
        buy_match = re.search(r"BuyTrailing\s*[:=]?\s*(\d+(?:\.\d+)?)", text, re.IGNORECASE)
        if buy_match:
            try:
                buy_trailing = float(buy_match.group(1))
            except ValueError:
                pass

        # Regex for SellMargin
        sell_match = re.search(r"SellMargin\s*[:=]?\s*(\d+(?:\.\d+)?)", text, re.IGNORECASE)
        if sell_match:
            try:
                sell_margin = float(sell_match.group(1))
            except ValueError:
                pass

        return profit_pct, buy_trailing, sell_margin

    def analyze_and_optimize(
        self,
        pair: str,
        current_buy_trailing: float,
        current_sell_margin: float,
        explore: bool = True
    ) -> Dict[str, Any]:
        """
        Analyzes historical trades from EpisodicMemory and proposes optimized trading parameters.
        Implements a Policy Gradient / REINFORCE continuous optimization algorithm.

        Args:
            pair (str): The trading pair to optimize parameters for.
            current_buy_trailing (float): Current BuyTrailing parameter value.
            current_sell_margin (float): Current SellMargin parameter value.
            explore (bool): If True, adds a small random perturbation for parameter exploration.

        Returns:
            Dict[str, Any]: A dictionary containing optimized parameters and execution status.
        """
        # Fetch all episodic events
        events = self.episodic_memory.get_all_events(limit=500)

        trades: List[Dict[str, Any]] = []
        for ev in events:
            metadata = ev.get("metadata", {})
            text = ev.get("text", "")

            # Check if this is a trade event
            is_trade = metadata.get("type") == "trade" or "completed trade" in text.lower()
            if not is_trade:
                continue

            # Filter by pair if possible
            event_pair = metadata.get("pair") or (pair if pair in text else None)
            if event_pair != pair:
                continue

            # Try to extract parameters
            profit_pct = metadata.get("profit_pct")
            buy_trailing = metadata.get("BuyTrailing")
            sell_margin = metadata.get("SellMargin")

            # Fallback to parsing text
            if profit_pct is None or buy_trailing is None or sell_margin is None:
                p, b, s = self._parse_trade_from_text(text)
                profit_pct = profit_pct if profit_pct is not None else p
                buy_trailing = buy_trailing if buy_trailing is not None else b
                sell_margin = sell_margin if sell_margin is not None else s

            if profit_pct is not None and buy_trailing is not None and sell_margin is not None:
                trades.append({
                    "profit_pct": profit_pct,
                    "BuyTrailing": buy_trailing,
                    "SellMargin": sell_margin
                })

        logging.info(f"OnlineRLTradingOptimizer: Found {len(trades)} trades for optimization of {pair}.")

        new_buy_trailing = current_buy_trailing
        new_sell_margin = current_sell_margin
        status = "unchanged"
        message = "No trades found to analyze."

        # If we have enough trades, perform policy gradient update
        if len(trades) >= 3:
            total_bt_update = 0.0
            total_sm_update = 0.0

            # Policy gradient update (REINFORCE derivative for gaussian policy mean update):
            # shift = reward * (action - mean)
            for trade in trades:
                # Use profit_pct as the reward signal
                reward = trade["profit_pct"]

                # BT update
                bt_action = trade["BuyTrailing"]
                bt_grad = bt_action - current_buy_trailing
                total_bt_update += reward * bt_grad

                # SM update
                sm_action = trade["SellMargin"]
                sm_grad = sm_action - current_sell_margin
                total_sm_update += reward * sm_grad

            avg_bt_update = total_bt_update / len(trades)
            avg_sm_update = total_sm_update / len(trades)

            # Adjust parameters
            new_buy_trailing += self.learning_rate * avg_bt_update
            new_sell_margin += self.learning_rate * avg_sm_update

            status = "optimized"
            message = f"Optimized based on {len(trades)} historical trades."
        else:
            # Not enough data, perform exploration step if allowed
            if explore:
                # Add uniform perturbation to encourage parameter space search
                new_buy_trailing += random.uniform(-0.05, 0.05)
                new_sell_margin += random.uniform(-0.1, 0.1)
                status = "explored"
                message = "Insufficient trade history. Performed random parameter exploration."
            else:
                status = "kept_current"
                message = "Insufficient trade history. Exploration disabled; kept current parameters."

        # Add optional exploration noise to optimized parameters to avoid local minima
        if status == "optimized" and explore:
            new_buy_trailing += random.normalvariate(0.0, 0.01)
            new_sell_margin += random.normalvariate(0.0, 0.02)

        # Enforce safety boundaries
        new_buy_trailing = max(self.min_buy_trailing, min(new_buy_trailing, self.max_buy_trailing))
        new_sell_margin = max(self.min_sell_margin, min(new_sell_margin, self.max_sell_margin))

        # Round to 3 decimal places for cleaner trading execution representation
        new_buy_trailing = round(new_buy_trailing, 3)
        new_sell_margin = round(new_sell_margin, 3)

        # Record adjustment event to episodic memory if parameters actually changed
        if new_buy_trailing != current_buy_trailing or new_sell_margin != current_sell_margin:
            adj_text = (
                f"Adjusted parameters for {pair}: "
                f"BuyTrailing={new_buy_trailing:.3f} (was {current_buy_trailing:.3f}), "
                f"SellMargin={new_sell_margin:.3f} (was {current_sell_margin:.3f})"
            )
            adj_meta = {
                "type": "parameter_adjustment",
                "pair": pair,
                "old_BuyTrailing": current_buy_trailing,
                "new_BuyTrailing": new_buy_trailing,
                "old_SellMargin": current_sell_margin,
                "new_SellMargin": new_sell_margin,
                "decayed": False
            }
            self.episodic_memory.store_event(adj_text, metadata=adj_meta)
            logging.info(f"OnlineRLTradingOptimizer: Recorded parameter adjustment event: {adj_text}")

        return {
            "BuyTrailing": new_buy_trailing,
            "SellMargin": new_sell_margin,
            "status": status,
            "trades_analyzed": len(trades),
            "message": message
        }
