# IntelliTrader - Magda Agent / Jules Working Guide

This repository is a crypto trading bot being evolved by an experimental cognitive agent with a Jules/Codex self-improvement loop.

## Source Priority

Before changing code, read these files in order:

1. `magda_agent_system/agent_tasks.json`
2. `magda_agent_system/docs/jules_autonomous_loop.md`
3. `magda_agent_system/docs/cognitive_architecture.md`
4. `magda_agent_system/docs/codex_worker_plan.md`
5. `magda_agent_system/backlog.md`

Use `agent_tasks.json` as the machine-readable source of truth. Markdown backlog files are secondary compatibility surfaces.

## Task Selection

Use the Codex bridge when possible (from `magda_agent_system` directory):

```bash
cd magda_agent_system
python -m magda_agent.codex_bridge validate
python -m magda_agent.codex_bridge next-task
python -m magda_agent.codex_bridge render-prompt
```

Pick the first task with status `todo` unless it is blocked by high/critical risk.

If fewer than `replenishment_policy.minimum_todo_tasks` tasks remain, add a batch of low/medium-risk tasks from the docs and current failure signals instead of asking the user what to do next.

## Risk Rules

Do not modify these without explicit task permission and review:

- `.github/workflows/**`
- `requirements.txt`
- Docker/deployment files
- sandbox or code execution policy
- secrets/env handling
- auto-merge permissions
- external messaging providers

Keep one task id per PR.

## Validation

For manifest/docs/governance changes (from `magda_agent_system` directory):

```bash
cd magda_agent_system
python scripts/validate_agent_tasks.py agent_tasks.json
```

For code changes, add focused tests and run the smallest useful pytest target first. Full `pytest` may require Linux/Docker and all dependencies.

## Local Environment Notes

The current codebase has platform-sensitive areas:

- `magda_agent.skills.system_execute_code` historically depended on Linux-only `resource`.
- ChromaDB is required by memory modules.
- Speech dependencies are heavy and should remain optional where possible.

Prefer stdlib-only tools for repository automation.
