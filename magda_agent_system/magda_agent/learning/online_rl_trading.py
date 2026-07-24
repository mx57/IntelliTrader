import json
import logging
import re
from datetime import datetime
from typing import Any, Dict, List, Optional
from magda_agent.memory.episodic import EpisodicMemory
from magda_agent.llm_client import LLMClient

class OnlineRLTradingOptimizer:
    """
    Online Reinforcement Learning Trading Parameter Optimizer.
    Adjusts trading parameters (BuyTrailing, SellMargin) based on trade success stored in EpisodicMemory.
    Tracks adjustment history and evaluates long-term improvements.
    """

    def __init__(
        self,
        episodic_memory: EpisodicMemory,
        llm: Optional[LLMClient] = None
    ) -> None:
        """
        Initializes the OnlineRLTradingOptimizer.

        Args:
            episodic_memory (EpisodicMemory): The episodic memory module used to store and retrieve trades and adjustments.
            llm (LLMClient, optional): The LLM client for advanced semantic adjustment proposals.
        """
        self.episodic_memory = episodic_memory
        self.llm = llm
        logging.info("Initialized OnlineRLTradingOptimizer")

    def store_trade_event(
        self,
        pair: str,
        buy_trailing: float,
        sell_margin: float,
        profit: float,
        cost: float,
        success: bool,
        user_id: Optional[int] = None,
        metadata: Optional[Dict[str, Any]] = None
    ) -> None:
        """
        Stores a completed trade event in episodic memory with rich metadata.

        Args:
            pair (str): The trading pair (e.g., BTC/USDT).
            buy_trailing (float): The buy trailing parameter when the trade occurred.
            sell_margin (float): The sell margin parameter when the trade occurred.
            profit (float): The profit/loss from the trade.
            cost (float): The cost basis of the trade.
            success (bool): Whether the trade was successful.
            user_id (int, optional): The user ID context.
            metadata (dict, optional): Additional metadata.
        """
        timestamp = datetime.utcnow().isoformat()
        text = f"Completed trade: Pair {pair}, BuyTrailing: {buy_trailing}, SellMargin: {sell_margin}, Profit: {profit}, Cost: {cost}, Success: {success}, Timestamp: {timestamp}"

        meta = {
            "type": "completed_trade",
            "pair": pair,
            "buy_trailing": float(buy_trailing),
            "sell_margin": float(sell_margin),
            "profit": float(profit),
            "cost": float(cost),
            "success": bool(success),
            "timestamp": timestamp
        }

        if metadata:
            meta.update(metadata)

        try:
            self.episodic_memory.store_event(text, metadata=meta, user_id=user_id)
            logging.info(f"Stored completed trade event for {pair} (Profit: {profit})")
        except Exception as e:
            logging.error(f"Failed to store completed trade event: {e}")

    def get_completed_trades(self, pair: Optional[str] = None) -> List[Dict[str, Any]]:
        """
        Retrieves and parses completed trades from episodic memory.

        Args:
            pair (str, optional): If specified, filters trades by this specific trading pair.

        Returns:
            List[Dict[str, Any]]: A list of parsed completed trades.
        """
        events = self.episodic_memory.get_all_events(include_decayed=True, limit=1000)
        trades: List[Dict[str, Any]] = []

        for ev in events:
            meta = ev.get("metadata") or {}
            doc_text = ev.get("text") or ""

            # Check if this is a completed trade by metadata or document content
            is_trade = meta.get("type") == "completed_trade" or doc_text.startswith("Completed trade:")

            if not is_trade:
                continue

            # Parse trade info preferring metadata with text fallbacks
            try:
                trade_pair = meta.get("pair")
                if not trade_pair:
                    # Fallback regex parsing
                    match = re.search(r"Pair ([A-Z0-9_/]+)", doc_text)
                    if match:
                        trade_pair = match.group(1)

                if pair and trade_pair != pair:
                    continue

                buy_trailing = meta.get("buy_trailing")
                if buy_trailing is None:
                    match = re.search(r"BuyTrailing:\s*([\d\.-]+)", doc_text)
                    buy_trailing = float(match.group(1)) if match else 0.0

                sell_margin = meta.get("sell_margin")
                if sell_margin is None:
                    match = re.search(r"SellMargin:\s*([\d\.-]+)", doc_text)
                    sell_margin = float(match.group(1)) if match else 0.0

                profit = meta.get("profit")
                if profit is None:
                    match = re.search(r"Profit:\s*([\d\.-]+)", doc_text)
                    profit = float(match.group(1)) if match else 0.0

                cost = meta.get("cost")
                if cost is None:
                    match = re.search(r"Cost:\s*([\d\.-]+)", doc_text)
                    cost = float(match.group(1)) if match else 0.0

                success = meta.get("success")
                if success is None:
                    match = re.search(r"Success:\s*(\w+)", doc_text)
                    success = match.group(1).lower() == "true" if match else False

                timestamp = meta.get("timestamp")
                if not timestamp:
                    match = re.search(r"Timestamp:\s*([\d\w\.-]+T[\d\w\.:-]+)", doc_text)
                    timestamp = match.group(1) if match else datetime.utcnow().isoformat()

                trades.append({
                    "id": ev["id"],
                    "pair": trade_pair,
                    "buy_trailing": float(buy_trailing),
                    "sell_margin": float(sell_margin),
                    "profit": float(profit),
                    "cost": float(cost),
                    "success": bool(success),
                    "timestamp": timestamp
                })
            except Exception as e:
                logging.error(f"Error parsing trade event: {e}")

        # Sort trades chronologically
        trades.sort(key=lambda t: t.get("timestamp") or "")
        return trades

    def analyze_trade_performance(self, pair: str) -> Dict[str, Any]:
        """
        Calculates performance metrics for a specific trading pair.

        Args:
            pair (str): The trading pair to analyze.

        Returns:
            Dict[str, Any]: Performance metrics dictionary.
        """
        trades = self.get_completed_trades(pair=pair)
        total_trades = len(trades)

        if total_trades == 0:
            return {
                "total_trades": 0,
                "successful_trades": 0,
                "win_rate": 0.0,
                "total_profit": 0.0,
                "avg_profit": 0.0,
                "avg_buy_trailing": 0.0,
                "avg_sell_margin": 0.0,
                "trades": []
            }

        successful_trades = sum(1 for t in trades if t["success"])
        win_rate = successful_trades / total_trades
        total_profit = sum(t["profit"] for t in trades)
        avg_profit = total_profit / total_trades

        avg_buy_trailing = sum(t["buy_trailing"] for t in trades) / total_trades
        avg_sell_margin = sum(t["sell_margin"] for t in trades) / total_trades

        return {
            "total_trades": total_trades,
            "successful_trades": successful_trades,
            "win_rate": win_rate,
            "total_profit": total_profit,
            "avg_profit": avg_profit,
            "avg_buy_trailing": avg_buy_trailing,
            "avg_sell_margin": avg_sell_margin,
            "trades": trades
        }

    async def propose_parameter_adjustments(
        self,
        pair: str,
        current_buy_trailing: float,
        current_sell_margin: float
    ) -> Dict[str, Any]:
        """
        Proposes adjustments to BuyTrailing and SellMargin based on trade success.
        Uses both an algorithmic/heuristic fallback policy and an optional LLM-based policy.

        Args:
            pair (str): The trading pair to optimize.
            current_buy_trailing (float): Current buy trailing setting.
            current_sell_margin (float): Current sell margin setting.

        Returns:
            Dict[str, Any]: Proposed parameters and explanatory reasoning.
        """
        performance = self.analyze_trade_performance(pair)
        total_trades = performance["total_trades"]
        win_rate = performance["win_rate"]
        total_profit = performance["total_profit"]

        # 1. Base Algorithmic / Heuristic Proposal (Always available)
        proposed_buy_trailing = current_buy_trailing
        proposed_sell_margin = current_sell_margin
        explanation = ""

        if total_trades == 0:
            explanation = f"No trade history available for {pair}. Retaining current parameters."
        elif win_rate < 0.6 or total_profit < 0:
            # Sub-optimal performance: optimize conservatively
            proposed_buy_trailing = round(current_buy_trailing + 0.05, 3)
            proposed_sell_margin = round(max(0.3, current_sell_margin - 0.15), 3)
            explanation = (
                f"Sub-optimal performance for {pair} (Win Rate: {win_rate:.1%}, Total Profit: {total_profit:.4f}). "
                f"Decreased SellMargin to exit trades faster and increased BuyTrailing to wait for deeper pullbacks before entering."
            )
        elif win_rate >= 0.8 and total_profit > 0:
            # High-performing strategy: scale up target margins
            proposed_buy_trailing = round(max(0.05, current_buy_trailing - 0.05), 3)
            proposed_sell_margin = round(current_sell_margin + 0.2, 3)
            explanation = (
                f"Excellent performance for {pair} (Win Rate: {win_rate:.1%}, Total Profit: {total_profit:.4f}). "
                f"Increased SellMargin to capture larger trends and decreased BuyTrailing to allow more entries."
            )
        else:
            explanation = f"Stable performance for {pair} (Win Rate: {win_rate:.1%}, Total Profit: {total_profit:.4f}). Parameters are kept stable."

        proposal = {
            "proposed_buy_trailing": float(proposed_buy_trailing),
            "proposed_sell_margin": float(proposed_sell_margin),
            "explanation": explanation,
            "method": "algorithmic"
        }

        # 2. Advanced LLM-based Proposal (Optional)
        if self.llm:
            try:
                prompt = f"""
                You are Magda's Trading RL Optimizer. Optimize the trading parameters for pair {pair}.
                Current Parameters:
                - BuyTrailing: {current_buy_trailing}
                - SellMargin: {current_sell_margin}

                Recent Trade History for {pair}:
                {json.dumps(performance, indent=2)}

                Analyze the trade history. Propose optimized parameters for 'BuyTrailing' and 'SellMargin'.
                Provide a JSON response with the following format:
                {{
                    "proposed_buy_trailing": <float>,
                    "proposed_sell_margin": <float>,
                    "explanation": "<string explaining the reasoning>"
                }}
                """
                response = await self.llm.chat_completion(
                    [{"role": "user", "content": prompt}],
                    temperature=0.2
                )

                # Attempt to extract json from response
                json_match = re.search(r"({.*})", response, re.DOTALL)
                if json_match:
                    data = json.loads(json_match.group(1))
                    proposal["proposed_buy_trailing"] = float(data["proposed_buy_trailing"])
                    proposal["proposed_sell_margin"] = float(data["proposed_sell_margin"])
                    proposal["explanation"] = str(data["explanation"])
                    proposal["method"] = "llm"
                    logging.info(f"LLM-based parameter optimization successful for {pair}")
            except Exception as e:
                logging.error(f"Failed to propose parameters via LLM, falling back to algorithmic: {e}")

        return proposal

    def apply_and_record_adjustment(
        self,
        pair: str,
        old_params: Dict[str, float],
        new_params: Dict[str, float],
        reason: str,
        user_id: Optional[int] = None
    ) -> None:
        """
        Applies and records the parameter adjustment in episodic memory.

        Args:
            pair (str): The trading pair.
            old_params (Dict[str, float]): Dictionary of old parameters.
            new_params (Dict[str, float]): Dictionary of new parameters.
            reason (str): The reasoning behind the adjustment.
            user_id (int, optional): The user ID context.
        """
        timestamp = datetime.utcnow().isoformat()
        text = f"Applied parameter adjustment for {pair}: old={old_params}, new={new_params}. Reason: {reason}, Timestamp: {timestamp}"

        metadata = {
            "type": "parameter_adjustment",
            "pair": pair,
            "old_buy_trailing": float(old_params.get("buy_trailing", 0.0)),
            "old_sell_margin": float(old_params.get("sell_margin", 0.0)),
            "new_buy_trailing": float(new_params.get("buy_trailing", 0.0)),
            "new_sell_margin": float(new_params.get("sell_margin", 0.0)),
            "reason": reason,
            "timestamp": timestamp
        }

        try:
            self.episodic_memory.store_event(text, metadata=metadata, user_id=user_id)
            logging.info(f"Recorded parameter adjustment for {pair} to episodic memory")
        except Exception as e:
            logging.error(f"Failed to record parameter adjustment: {e}")

    def evaluate_adjustment_impact(self, pair: str) -> Dict[str, Any]:
        """
        Compares trade performance before and after the last recorded adjustment for a pair.

        Args:
            pair (str): The trading pair.

        Returns:
            Dict[str, Any]: Comparison dictionary evaluating the long-term impact.
        """
        events = self.episodic_memory.get_all_events(include_decayed=True, limit=1000)
        adjustments: List[Dict[str, Any]] = []

        for ev in events:
            meta = ev.get("metadata") or {}
            doc_text = ev.get("text") or ""
            is_adj = meta.get("type") == "parameter_adjustment" or "Applied parameter adjustment" in doc_text

            if not is_adj:
                continue

            try:
                adj_pair = meta.get("pair")
                if not adj_pair:
                    match = re.search(r"Applied parameter adjustment for ([A-Z0-9_/]+)", doc_text)
                    if match:
                        adj_pair = match.group(1)

                if adj_pair != pair:
                    continue

                timestamp = meta.get("timestamp")
                if not timestamp:
                    match = re.search(r"Timestamp:\s*([\d\w\.-]+T[\d\w\.:-]+)", doc_text)
                    timestamp = match.group(1) if match else datetime.utcnow().isoformat()

                adjustments.append({
                    "timestamp": timestamp,
                    "new_buy_trailing": meta.get("new_buy_trailing"),
                    "new_sell_margin": meta.get("new_sell_margin"),
                    "reason": meta.get("reason", "")
                })
            except Exception as e:
                logging.error(f"Error parsing adjustment event: {e}")

        if not adjustments:
            return {
                "status": "no_adjustments_recorded",
                "message": "No adjustments recorded for this pair yet."
            }

        # Sort adjustments chronologically and pick the latest one
        adjustments.sort(key=lambda a: a["timestamp"])
        latest_adj = adjustments[-1]
        adj_time_str = latest_adj["timestamp"]
        try:
            adj_time = datetime.fromisoformat(adj_time_str)
        except Exception:
            # Fallback in case of string parsing issues
            adj_time = datetime.utcnow()

        # Split completed trades into before and after
        all_trades = self.get_completed_trades(pair=pair)
        before_trades: List[Dict[str, Any]] = []
        after_trades: List[Dict[str, Any]] = []

        for trade in all_trades:
            trade_time_str = trade.get("timestamp") or ""
            try:
                trade_time = datetime.fromisoformat(trade_time_str)
            except Exception:
                trade_time = datetime.utcnow()

            if trade_time < adj_time:
                before_trades.append(trade)
            else:
                after_trades.append(trade)

        def calculate_stats(trades_list: List[Dict[str, Any]]) -> Dict[str, Any]:
            count = len(trades_list)
            if count == 0:
                return {
                    "count": 0,
                    "win_rate": 0.0,
                    "total_profit": 0.0,
                    "avg_profit": 0.0
                }
            successes = sum(1 for t in trades_list if t["success"])
            total_prof = sum(t["profit"] for t in trades_list)
            return {
                "count": count,
                "win_rate": successes / count,
                "total_profit": total_prof,
                "avg_profit": total_prof / count
            }

        before_stats = calculate_stats(before_trades)
        after_stats = calculate_stats(after_trades)

        # Check for improvement
        win_rate_diff = after_stats["win_rate"] - before_stats["win_rate"]
        profit_diff = after_stats["total_profit"] - before_stats["total_profit"]
        avg_profit_diff = after_stats["avg_profit"] - before_stats["avg_profit"]

        return {
            "status": "evaluated",
            "last_adjustment": latest_adj,
            "before_adjustment": before_stats,
            "after_adjustment": after_stats,
            "improvement": {
                "win_rate_change": win_rate_diff,
                "total_profit_change": profit_diff,
                "avg_profit_change": avg_profit_diff,
                "is_improvement": avg_profit_diff > 0 or win_rate_diff > 0
            }
        }
