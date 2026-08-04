import os
import json
import shutil
import tempfile
import pytest
import asyncio
from unittest.mock import AsyncMock, MagicMock, patch
from magda_agent.scheduler.backtest_orchestrator import BacktestOrchestrator

class MockLLMClient:
    """Mock LLMClient for testing backtest lead evaluation."""
    async def chat_completion(self, messages, **kwargs):
        return "Лучшая стратегия - Conservative Strategy, так как она показывает более высокий Win Rate (85%) и минимальную просадку (1.5%)."

@pytest.mark.asyncio
async def test_backtest_orchestrator_simulated():
    """
    Проверяет полную цепочку параллельного бэктестинга стратегий с изоляцией в ворктри.
    Тест использует мок для менеджера ворктри, который создает настоящие папки на диске
    для верификации чтения-записи json-файлов конфигураций, а затем проверяет
    агрегацию результатов и оценку лид-агента.
    """
    # 1. Создаем временную директорию для эмуляции ворктри
    with tempfile.TemporaryDirectory() as temp_base_dir:
        llm = MockLLMClient()
        orchestrator = BacktestOrchestrator(llm_client=llm, base_worktree_dir=temp_base_dir)

        # 2. Мокаем create_worktree_async и remove_worktree_async
        # для создания временных директорий вместо реальных вызовов git.
        created_paths = []

        async def mock_create_worktree(*args, **kwargs):
            # Создаем уникальный временный каталог внутри temp_base_dir
            worktree_dir = tempfile.mkdtemp(dir=temp_base_dir)
            created_paths.append(worktree_dir)

            # Эмулируем структуру проекта IntelliTrader
            config_dir = os.path.join(worktree_dir, "IntelliTrader/config")
            os.makedirs(config_dir, exist_ok=True)

            # Создаем дефолтные конфигурационные файлы
            trading_conf = {"Trading": {"BuyTrailing": -0.15, "SellMargin": 1.5, "SellTrailing": 0.5}}
            backtest_conf = {"Backtesting": {"Enabled": False, "Replay": False}}

            with open(os.path.join(config_dir, "trading.json"), "w", encoding="utf-8") as f:
                json.dump(trading_conf, f)
            with open(os.path.join(config_dir, "backtesting.json"), "w", encoding="utf-8") as f:
                json.dump(backtest_conf, f)

            return worktree_dir

        async def mock_remove_worktree(path):
            if os.path.exists(path):
                shutil.rmtree(path)

        orchestrator.worktree_manager.create_worktree_async = mock_create_worktree
        orchestrator.worktree_manager.remove_worktree_async = mock_remove_worktree

        # 3. Задаем две различные тестовые стратегии
        strategies = [
            {
                "name": "Conservative Strategy",
                "trading_config": {
                    "BuyTrailing": -0.1,
                    "SellMargin": 2.0,
                    "SellTrailing": 0.5
                }
            },
            {
                "name": "Aggressive Strategy",
                "trading_config": {
                    "BuyTrailing": -0.3,
                    "SellMargin": 1.0,
                    "SellTrailing": 0.2
                }
            }
        ]

        # 4. Запускаем оркестратор бэктестинга
        res = await orchestrator.run_backtests(strategies)

        # 5. Проверки (Assertions)
        assert res["success"] is True
        assert len(res["results"]) == 2

        # Проверим результаты первой стратегии
        strat1 = res["results"][0]
        assert strat1["strategy_name"] == "Conservative Strategy"
        assert strat1["success"] is True
        assert strat1["trading_config"]["BuyTrailing"] == -0.1
        assert "metrics" in strat1
        assert strat1["metrics"]["num_trades"] > 0
        assert strat1["metrics"]["total_profit_pct"] is not None

        # Проверим результаты второй стратегии
        strat2 = res["results"][1]
        assert strat2["strategy_name"] == "Aggressive Strategy"
        assert strat2["success"] is True
        assert strat2["trading_config"]["BuyTrailing"] == -0.3

        # Проверим выбор лучшей стратегии алгоритмически
        assert res["best_strategy"] is not None
        assert res["best_strategy"]["strategy_name"] in ["Conservative Strategy", "Aggressive Strategy"]

        # Проверим, что лид-анализ успешно вызван и вернул структурированный отчет на русском языке
        assert "Лучшая стратегия" in res["lead_analysis_report"]
        assert "Win Rate" in res["lead_analysis_report"]
