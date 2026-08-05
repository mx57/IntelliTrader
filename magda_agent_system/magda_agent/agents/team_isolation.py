import asyncio
import logging
from typing import List, Dict, Any
from magda_agent.agents.sub_agent import SubAgent
from magda_agent.llm_client import LLMClient

class TeamIsolationOrchestrator:
    """
    Оркестрирует выполнение нескольких параллельных суб-агентов для набора задач
    с использованием изоляции через временные Git-ворктри.
    """
    def __init__(self, llm: LLMClient):
        """
        Инициализирует TeamIsolationOrchestrator.
        """
        self.llm = llm

    async def execute_isolated_tasks(self, tasks: List[Dict[str, Any]], base_context: str) -> List[str]:
        """
        Запускает несколько задач параллельно, создавая для каждого суб-агента изолированное Git-ворктри.
        Это предотвращает конфликты изменений конфигураций при параллельном бэктестинге.
        """
        logging.info(f"Оркестрирование {len(tasks)} изолированных задач суб-агентов.")

        async def run_task(task_spec: Dict[str, Any]) -> str:
            # Создаем изолированного суб-агента с использованием флага use_isolation=True
            sub_agent = SubAgent(llm=self.llm, use_isolation=True)
            task_description = task_spec.get('description', 'Unknown task')

            # Если задача содержит специфическую конфигурацию стратегии, добавляем ее в контекст
            strategy_config = task_spec.get('trading_config')
            task_context = base_context
            if strategy_config:
                task_context += f"\nStrategy Config: {strategy_config}"

            try:
                return await sub_agent.execute(task=task_description, context=task_context)
            except Exception as e:
                logging.error(f"Ошибка при выполнении изолированной задачи суб-агента: {e}")
                return f"Error: {e}"

        results = await asyncio.gather(*(run_task(task) for task in tasks))
        return list(results)
