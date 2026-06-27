# IntelliTrader Evolution Document

## Overview
IntelliTrader is a cross-platform cryptocurrency trading bot built on .NET Core 2.1. It supports virtual (paper) trading and live trading (currently Binance only). The system is highly modular, separating core logic, exchange interactions, trading strategies, and web-based management.

## Architecture

### 1. Core (IntelliTrader.Core)
- **Purpose**: Defines shared interfaces, constants, and base utility classes.
- **Key Components**:
  - `Constants`: Defines service names, health check keys, and supported markets (BTC, ETH, BNB, USDT).
  - `Utils`: Shared mathematical and helper functions.
  - `Application`: Simple service locator/DI container.

### 2. Exchange (IntelliTrader.Exchange.*)
- **IntelliTrader.Exchange.Base**:
  - `ExchangeService`: Orchestrates ticker updates via WebSockets and REST. Currently contains a logic bug in `Start()` that resets tickers to empty even if initial fetch succeeds.
- **IntelliTrader.Exchange.Binance**:
  - Implements Binance-specific logic. Many order-related methods are currently stubs with TODOs.

### 3. Trading (IntelliTrader.Trading)
- **Purpose**: Manages the trading lifecycle, including account balances (Virtual/Live), position tracking, and execution of buy/sell orders.
- **Key Components**:
  - `TradingService`: The main entry point for trading operations.
  - `Account`: Abstracted as `ITradingAccount`, with `VirtualAccount` and `ExchangeAccount` implementations.
  - `OrderingService`: Handles the mechanics of placing orders via the `ExchangeService`.

### 4. Web Interface (IntelliTrader.Web)
- **Purpose**: Provides a real-time dashboard for monitoring and manual control.
- **Tech Stack**: ASP.NET Core 2.1, MVC, jQuery, DataTables.
- **Performance Note**: `HomeController.Stats` uses an $O(N^2)$ algorithm to calculate historical balances, which should be optimized to $O(N)$.

### 5. Rules & Signals (IntelliTrader.Rules, IntelliTrader.Signals.*)
- **Rules**: Allows users to define complex conditions for entering and exiting trades.
- **Signals**: Integrates with TradingView and other sources to provide market data triggers.

## Current State & Identified Issues

1. **Critical Bug in `ExchangeService`**:
   - `GetPairMarket` is not implemented, causing incorrect pair normalization.
   - `Start()` method logic bug wipes out successfully fetched initial tickers.
2. **Performance Bottleneck**:
   - `HomeController.Stats` balance calculation is inefficient and has minor logic errors in date range handling.
3. **Incomplete Features**:
   - `BinanceExchangeService` has many TODOs for actual order execution.
   - Triangular arbitrage logic is present but may need verification against live data.

## Future Improvements
- [ ] Fix critical bugs in `ExchangeService`.
- [ ] Optimize `Stats` calculation in `Web` dashboard.
- [ ] Complete Binance order placement implementation.
- [ ] Add more exchange integrations.
- [ ] Implement advanced technical analysis indicators in the `Signals` module.
