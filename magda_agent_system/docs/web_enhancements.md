# IntelliTrader Web Dashboard Enhancement Plan

This document outlines the proposed enhancements for the `IntelliTrader.Web` dashboard. The enhancements focus on improving real-time system observability, advanced log visualization, and integrating metrics inspired by modern (2026) agentic cognitive architecture trends.

---

## 1. Advanced Log Visualization Dashboard

The current log page (`Views/Home/Log.cshtml`) is a basic list that dumps the latest 500 log lines without formatting, searchability, or pagination. To handle large-scale trading runs, we propose a complete overhaul of the log view.

### A. Server-Side Log Level Parsing and API
- **Implementation**: Update `HomeController` to serve logs via a JSON API with support for pagination (`skip`/`take`), filtering, and search.
- **Severity Parsing**: Parse log strings to extract log levels: `[INFO]`, `[WARN]`, `[ERROR]`, `[DEBUG]`. Return these as structured JSON objects:
  ```json
  {
    "timestamp": "2026-07-05T12:00:00Z",
    "level": "ERROR",
    "message": "Binance API call rate limit exceeded.",
    "source": "BinanceExchangeService"
  }
  ```

### B. Interactive Frontend Log Viewer (UI/UX)
- **DataTables/Custom Filter UI**: Implement a lightweight DataTable or modern vanilla JS grid specifically for logs.
- **Quick Filters**: Add buttons to filter by severity levels (Show Only Errors, Warnings, or Info).
- **Log Level Highlighting**:
  - `ERROR`: Soft red background with bold text.
  - `WARN`: Soft orange background.
  - `INFO`: Soft blue/green background or neutral text.
  - `DEBUG`: Dimmed grey text.
- **Log Collapsing**: Automatically group identical repeated logs (e.g., repeating network reconnection timeouts) into a single expandable row with a "count" badge to prevent log floods from drowning out critical events.

### C. Live Streaming Log Component
- **Tech Stack**: Introduce Server-Sent Events (SSE) or simple WebSocket polling via `IHubContext` (SignalR) if upgraded.
- **UX**: A "Live Stream" toggle. When enabled, new log lines slide in from the top in real-time, matching the lightning-fast Bolt ⚡ philosophy of instant observability.

---

## 2. Real-Time Stats & Interactive Dashboard

The main `Dashboard.cshtml` lists current positions and prices, but lacks deep visual indicator cues for active trading loops and market dynamics.

### A. Exchange WebSocket/API Connectivity Status
- **Connection Indicator**: A status bar at the header displaying connection states for the configured exchange API (e.g., Binance, Backtesting virtual engine).
- **Ping/Latency Indicator**: Display current API latency (ms) to help users spot network congestion or rate-limiting risk early.

### B. Trailing Logic Visualization
- **Visual Progress Bars**: In the trading pairs table, add a column or a sub-row element visualizing the trailing buy/sell distance.
- **Color-Coded Margin Drift**: Indicate how close the current price is to the target trailing margin:
    - Green/Yellow bar: Approaching trigger price.
    - Blue pulse: Trailing active and tracking higher/lower prices.
- **Spread-Aware Alert Banner**: A dashboard-wide warning alert when the `CurrentSpread` on any active pair exceeds `MaxTrailingSpread`, indicating that trading/trailing is temporarily paused for safety.

---

## 3. 2026 AI Agentic & Cognitive Integration Panel

Aligned with modern multi-agent systems, the IntelliTrader Web Dashboard should provide a dedicated workspace for the operating AI Agent's status, metrics, and self-improvement loops.

### A. Cognitive Core & Memory DB Observability
- **Working Context Monitor**: Display current active goals, target tokens, and the short-term working context of the agent.
- **Episodic Memory Stats**: Show a metric counter of stored trading episodes (successful vs. failed trades stored in the vector database/ChromaDB).
- **Semantic Search Widget**: A widget allowing human operators to search the Agent's episodic memory (e.g., "Find all trades that failed due to high spread during volatile BTC hours").

### B. Online Reinforcement Learning (RL) Parameter Tracking
- **Dynamic Parameter Tuning Charts**: Visual line charts demonstrating how the RL feedback loops (inspired by `online_rl_trading.py`) have adjusted key parameters like `BuyTrailing` and `SellMargin` over time.
- **Parameter Drift Insights**: A panel summarizing *why* the agent shifted a parameter (e.g., "Increased BuyTrailing by +0.15% due to 3 consecutive slippage losses in ETH/USDT").

### C. MCP (Model Context Protocol) Skill Metrics
- **Tool Concurrency & Success Rate**: Track and display metrics for each MCP-compliant tool/skill registered by the Magda agent system:
  - `execute_code`: Invocation count, average execution time, and error rate.
  - `market_analysis`: Token cost and predictive accuracy.
- **Trace Viewer**: A lightweight accordion view showing the recent tool-execution call stacks and JSON-RPC payloads, bringing unmatched transparency to the agent's actions.

---

## 4. Implementation Checklist & Phased Roadmap

### Phase 1: Foundation (Read-Only Diagnostics)
- [ ] Implement Server-side log parser in `HomeController`.
- [ ] Create a dedicated `Views/Home/LogViewer.cshtml` with basic level highlighting and sorting.
- [ ] Add the WebSocket connectivity health check in `IHealthCheckService`.

### Phase 2: Interactivity (Logs & Metrics)
- [ ] Add search, paging, and level filtering to the Log Viewer.
- [ ] Create the "Agent Status" tab in the main sidebar.
- [ ] Pull active agent metrics and episodic memory count from `magda_agent_system/metrics_db.sqlite3`.

### Phase 3: Advanced Optimization (RL & Real-Time)
- [ ] Integrate real-time log streaming using Server-Sent Events (SSE).
- [ ] Build the interactive parameter drift line charts using Chart.js on the Agent Status panel.
- [ ] Complete full accessibility (a11y) and mobile responsive layout testing.
