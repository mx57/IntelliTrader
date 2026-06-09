# IntelliTrader Architectural Analysis

## Overview
IntelliTrader is a multi-modular C#/.NET based cryptocurrency trading bot. It features a dashboard, exchange integrations, and automated trading rules.

## Core Modules

### 1. IntelliTrader.Core
- **Application.cs**: Main entry point and application lifecycle management.
- **Services/**: Core utility services (Configuration, Logging, etc.).
- **Models/**: Shared data structures.

### 2. IntelliTrader.Trading
- **Services/TradingService.cs**: Orchestrates trading operations across different pairs and exchanges.
- **Services/OrderingService.cs**: Handles order placement and tracking.
- **Models/Accounts/**: Manages exchange and virtual accounts.
- **Models/TradingPair.cs**: Represents a pair being traded.

### 3. IntelliTrader.Exchange
- **IntelliTrader.Exchange.Base**: Abstract base for exchange implementations.
- **IntelliTrader.Exchange.Binance**: Concrete implementation for Binance exchange.
- Uses **ExchangeSharp** submodule for low-level API interactions.

### 4. IntelliTrader.Rules
- Contains trading strategies and decision-making logic.
- Implements trailing buy/sell logic and indicator-based signals.

### 5. IntelliTrader.Web
- **Controllers/HomeController.cs**: Main web interface controller.
- **Views/**: Razor views for Dashboard, Market, Rules, Stats, etc.
- Provides a user-friendly way to monitor and configure the bot.

## Future Evolution Goals
1. **Exchange Integration**: Add more exchange implementations beyond Binance.
2. **Strategy Optimization**: Refine trailing logic and add more technical indicators.
3. **Web UI Enhancement**: Improve real-time reporting and interactive chart visualizations.
4. **Risk Management**: Implement more advanced portfolio-level risk controls.
5. **Backtesting**: Enhance `IntelliTrader.Backtesting` for better strategy validation.
