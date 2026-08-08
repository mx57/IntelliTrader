using System;
using System.Diagnostics;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using IntelliTrader.Backtesting;

namespace BacktestingBenchmarkSuite
{
    public class TradeRecord
    {
        public string Pair { get; set; }
        public string Type { get; set; } // BUY (Initial Entry), BUY (DCA Level X), SELL (Take Profit), SELL (Stop Loss)
        public decimal Price { get; set; }
        public decimal Amount { get; set; }
        public decimal Cost { get; set; }
        public decimal ProfitPct { get; set; }
        public decimal ProfitUsdt { get; set; }
        public DateTime Timestamp { get; set; }
        public decimal ResultingBalance { get; set; }
    }

    public class EquityPoint
    {
        public DateTime Timestamp { get; set; }
        public decimal Balance { get; set; }
    }

    public class SimulatedPosition
    {
        public string Pair { get; set; }
        public decimal EntryPrice { get; set; }
        public decimal Amount { get; set; }
        public decimal Cost { get; set; }
        public int DcaLevel { get; set; }
        public DateTime EntryTime { get; set; }
    }

    public class BenchmarkResult
    {
        public int SnapshotCount { get; set; }
        public int SimulatedMonths { get; set; }

        // Сериализация
        public double SerializationTimeMs { get; set; }
        public double SerializationThroughput { get; set; } // снимков/сек
        public double TotalSizeMb { get; set; }

        // Десериализация
        public double DeserializationTimeMs { get; set; }
        public double DeserializationThroughput { get; set; } // снимков/сек
        public double DeserializationSpeedMbSec { get; set; } // МБ/сек

        // Цикл симуляции бэктеста
        public double ProcessingTimeMs { get; set; }
        public double ProcessingThroughput { get; set; } // снимков/сек
        public double SpeedupFactor { get; set; } // во сколько раз быстрее реального времени

        // Память
        public long BytesAllocated { get; set; }
        public int GcCollectionsGen0 { get; set; }
        public int GcCollectionsGen1 { get; set; }
        public int GcCollectionsGen2 { get; set; }

        // Метрики торговли (Трейдинг Бэктест)
        public decimal InitialBalance { get; set; }
        public decimal FinalBalance { get; set; }
        public decimal TotalProfitPct { get; set; }
        public int TotalTrades { get; set; }
        public int WinningTrades { get; set; }
        public int LosingTrades { get; set; }
        public decimal WinRatePct { get; set; }
        public decimal MaxDrawdownPct { get; set; }
        public decimal ProfitFactor { get; set; }
        public List<TradeRecord> Trades { get; set; } = new List<TradeRecord>();
        public List<EquityPoint> EquityCurve { get; set; } = new List<EquityPoint>();
    }

    public class BenchmarkRunner
    {
        private static readonly string[] TradingPairs = { "BTC_USDT", "ETH_USDT", "LTC_USDT", "XRP_USDT", "ADA_USDT", "SOL_USDT", "DOT_USDT" };

        /// <summary>
        /// Генерирует реалистичный набор снимков для бэктеста, симулирующий multi-month историю.
        /// </summary>
        public static (List<List<SignalData>> Signals, List<List<TickerData>> Tickers) GenerateDataset(int snapshotCount, int months)
        {
            var random = new Random(42); // Детерминированный seed для воспроизводимости
            var signalDataset = new List<List<SignalData>>(snapshotCount);
            var tickerDataset = new List<List<TickerData>>(snapshotCount);

            // Базовые цены для симуляции
            var basePrices = new Dictionary<string, decimal>
            {
                { "BTC_USDT", 65000m },
                { "ETH_USDT", 3500m },
                { "LTC_USDT", 85m },
                { "XRP_USDT", 0.55m },
                { "ADA_USDT", 0.45m },
                { "SOL_USDT", 140m },
                { "DOT_USDT", 6.5m }
            };

            for (int i = 0; i < snapshotCount; i++)
            {
                var signals = new List<SignalData>();
                var tickers = new List<TickerData>();

                foreach (var pair in TradingPairs)
                {
                    // Симулируем легкий дрейф цены
                    decimal changePercent = (decimal)(random.NextDouble() * 0.04 - 0.02); // -2% до +2%
                    decimal basePrice = basePrices[pair];
                    decimal currentPrice = basePrice * (1m + changePercent);
                    basePrices[pair] = currentPrice; // обновляем базу

                    // Добавляем сигнал
                    signals.Add(new SignalData
                    {
                        Name = "StrategyAlpha",
                        Pair = pair,
                        Volume = random.Next(100, 10000),
                        VolumeChange = random.NextDouble() * 20.0 - 10.0,
                        Price = currentPrice,
                        PriceChange = currentPrice * changePercent,
                        Rating = random.NextDouble() * 10.0,
                        RatingChange = random.NextDouble() * 2.0 - 1.0,
                        Volatility = random.NextDouble() * 5.0 + 0.5
                    });

                    // Добавляем тикер
                    decimal spread = currentPrice * 0.001m; // 0.1% спред
                    tickers.Add(new TickerData
                    {
                        Pair = pair,
                        BidPrice = currentPrice - spread / 2,
                        AskPrice = currentPrice + spread / 2,
                        LastPrice = currentPrice
                    });
                }

                signalDataset.Add(signals);
                tickerDataset.Add(tickers);
            }

            return (signalDataset, tickerDataset);
        }

        /// <summary>
        /// Запускает полный цикл бенчмаркинга.
        /// </summary>
        public static BenchmarkResult Run(int snapshotCount, int months)
        {
            // Подготовка окружения для чистых замеров памяти
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            long startAllocatedBytes = GC.GetAllocatedBytesForCurrentThread();
            int startGen0 = GC.CollectionCount(0);
            int startGen1 = GC.CollectionCount(1);
            int startGen2 = GC.CollectionCount(2);

            // 1. Генерация датасета
            var (signals, tickers) = GenerateDataset(snapshotCount, months);

            // 2. Бенчмарк сериализации (в оперативную память через MemoryStream для чистоты CPU-замеров)
            var stopwatch = Stopwatch.StartNew();
            var memoryStreams = new List<byte[]>(snapshotCount * 2);
            long totalBytesSerialized = 0;

            for (int i = 0; i < snapshotCount; i++)
            {
                using (var ms = new MemoryStream())
                using (var writer = new BinaryWriter(ms))
                {
                    SnapshotSerializer.SerializeSignals(writer, signals[i]);
                    byte[] bytes = ms.ToArray();
                    memoryStreams.Add(bytes);
                    totalBytesSerialized += bytes.Length;
                }

                using (var ms = new MemoryStream())
                using (var writer = new BinaryWriter(ms))
                {
                    SnapshotSerializer.SerializeTickers(writer, tickers[i]);
                    byte[] bytes = ms.ToArray();
                    memoryStreams.Add(bytes);
                    totalBytesSerialized += bytes.Length;
                }
            }
            stopwatch.Stop();
            double serializationTimeMs = stopwatch.Elapsed.TotalMilliseconds;
            double totalSizeMb = totalBytesSerialized / (1024.0 * 1024.0);

            // 3. Бенчмарк десериализации
            stopwatch.Restart();
            var deserializedSignals = new List<List<SignalData>>(snapshotCount);
            var deserializedTickers = new List<List<TickerData>>(snapshotCount);

            for (int i = 0; i < snapshotCount * 2; i += 2)
            {
                byte[] signalBytes = memoryStreams[i];
                using (var ms = new MemoryStream(signalBytes))
                using (var reader = new BinaryReader(ms))
                {
                    deserializedSignals.Add(SnapshotSerializer.DeserializeSignals(reader));
                }

                byte[] tickerBytes = memoryStreams[i + 1];
                using (var ms = new MemoryStream(tickerBytes))
                using (var reader = new BinaryReader(ms))
                {
                    deserializedTickers.Add(SnapshotSerializer.DeserializeTickers(reader));
                }
            }
            stopwatch.Stop();
            double deserializationTimeMs = stopwatch.Elapsed.TotalMilliseconds;

            // 4. Симуляция работы движка бэктестинга (эмуляция обработки снимков торговыми правилами)
            stopwatch.Restart();
            int processedSnapshots = 0;
            double dummyStateHash = 0;

            decimal initialBalance = 10000m;
            decimal balance = initialBalance;
            decimal peakBalance = initialBalance;
            decimal maxDrawdownPct = 0m;
            var activePositions = new Dictionary<string, SimulatedPosition>();
            var tradesList = new List<TradeRecord>();
            var equityCurveList = new List<EquityPoint>();

            DateTime startSimTime = DateTime.UtcNow.AddMonths(-months);
            equityCurveList.Add(new EquityPoint { Timestamp = startSimTime, Balance = balance });

            for (int i = 0; i < snapshotCount; i++)
            {
                var currentSigs = deserializedSignals[i];
                var currentTicks = deserializedTickers[i];
                DateTime timestamp = startSimTime.AddSeconds(i * 10);

                // Имитация прохода по торговым парам и вычисления DCA / Трейлинга
                foreach (var sig in currentSigs)
                {
                    var tick = currentTicks.FirstOrDefault(t => t.Pair == sig.Pair);
                    if (tick != null)
                    {
                        // Тяжелая математическая операция для имитации логики индикаторов/правил
                        double volatilityFactor = sig.Volatility ?? 1.0;
                        double priceSpread = (double)(tick.AskPrice - tick.BidPrice);
                        dummyStateHash += (double)tick.LastPrice * volatilityFactor + priceSpread;
                        processedSnapshots++;

                        // --- Торговый автомат бэктестинга ---
                        string pair = sig.Pair;
                        decimal currentPrice = tick.LastPrice;

                        if (activePositions.TryGetValue(pair, out var pos))
                        {
                            decimal priceChangePct = (currentPrice - pos.EntryPrice) / pos.EntryPrice;

                            // Тейк-Профит: +1.5% прибыль (после вычета имитируемой комиссии в 0.075%)
                            if (priceChangePct >= 0.015m)
                            {
                                decimal sellCost = pos.Amount * currentPrice * (1m - 0.00075m); // С учетом комиссии
                                decimal profitUsdt = sellCost - pos.Cost;
                                decimal profitPct = (sellCost - pos.Cost) / pos.Cost * 100m;
                                balance += sellCost;

                                tradesList.Add(new TradeRecord
                                {
                                    Pair = pair,
                                    Type = "SELL (Take Profit)",
                                    Price = currentPrice,
                                    Amount = pos.Amount,
                                    Cost = sellCost,
                                    ProfitPct = profitPct,
                                    ProfitUsdt = profitUsdt,
                                    Timestamp = timestamp,
                                    ResultingBalance = balance
                                });

                                activePositions.Remove(pair);

                                if (tradesList.Count % 5 == 0) // Сэмплируем точки для легковесного отображения
                                {
                                    equityCurveList.Add(new EquityPoint { Timestamp = timestamp, Balance = balance });
                                }
                            }
                            // DCA: Цена просела на 3% или более. Докупаем, если достаточно средств на балансе.
                            else if (priceChangePct <= -0.03m && pos.DcaLevel < 3 && balance >= 1000m)
                            {
                                decimal buyCost = 1000m;
                                decimal addedAmount = buyCost * (1m - 0.00075m) / currentPrice; // С учетом комиссии
                                pos.Amount += addedAmount;
                                pos.Cost += buyCost;
                                pos.EntryPrice = pos.Cost / pos.Amount; // Средневзвешенная цена входа
                                pos.DcaLevel++;
                                balance -= buyCost;

                                tradesList.Add(new TradeRecord
                                {
                                    Pair = pair,
                                    Type = $"BUY (DCA Level {pos.DcaLevel})",
                                    Price = currentPrice,
                                    Amount = addedAmount,
                                    Cost = buyCost,
                                    ProfitPct = 0m,
                                    ProfitUsdt = 0m,
                                    Timestamp = timestamp,
                                    ResultingBalance = balance
                                });
                            }
                            // Стоп-Лосс: Цена просела на 8% или более. Фиксируем убыток.
                            else if (priceChangePct <= -0.08m)
                            {
                                decimal sellCost = pos.Amount * currentPrice * (1m - 0.00075m);
                                decimal profitUsdt = sellCost - pos.Cost;
                                decimal profitPct = (sellCost - pos.Cost) / pos.Cost * 100m;
                                balance += sellCost;

                                tradesList.Add(new TradeRecord
                                {
                                    Pair = pair,
                                    Type = "SELL (Stop Loss)",
                                    Price = currentPrice,
                                    Amount = pos.Amount,
                                    Cost = sellCost,
                                    ProfitPct = profitPct,
                                    ProfitUsdt = profitUsdt,
                                    Timestamp = timestamp,
                                    ResultingBalance = balance
                                });

                                activePositions.Remove(pair);

                                if (tradesList.Count % 5 == 0)
                                {
                                    equityCurveList.Add(new EquityPoint { Timestamp = timestamp, Balance = balance });
                                }
                            }
                        }
                        else
                        {
                            // Начальный вход: Высокий рейтинг сигнала (>= 7.5)
                            if (sig.Rating >= 7.5 && balance >= 1000m)
                            {
                                decimal buyCost = 1000m;
                                decimal amount = buyCost * (1m - 0.00075m) / currentPrice; // С учетом комиссии
                                balance -= buyCost;

                                activePositions[pair] = new SimulatedPosition
                                {
                                    Pair = pair,
                                    EntryPrice = currentPrice,
                                    Amount = amount,
                                    Cost = buyCost,
                                    DcaLevel = 0,
                                    EntryTime = timestamp
                                };

                                tradesList.Add(new TradeRecord
                                {
                                    Pair = pair,
                                    Type = "BUY (Initial Entry)",
                                    Price = currentPrice,
                                    Amount = amount,
                                    Cost = buyCost,
                                    ProfitPct = 0m,
                                    ProfitUsdt = 0m,
                                    Timestamp = timestamp,
                                    ResultingBalance = balance
                                });
                            }
                        }

                        // --- Точный расчет просадки по эквити ---
                        decimal currentEquity = balance;
                        foreach (var openPos in activePositions.Values)
                        {
                            if (openPos.Pair == pair)
                                currentEquity += openPos.Amount * currentPrice;
                            else
                                currentEquity += openPos.Amount * openPos.EntryPrice;
                        }

                        if (currentEquity > peakBalance) peakBalance = currentEquity;
                        decimal currentDd = (peakBalance - currentEquity) / peakBalance * 100m;
                        if (currentDd > maxDrawdownPct) maxDrawdownPct = currentDd;
                    }
                }

                // Плавное сэмплирование общего эквити портфеля
                if (i % 50 == 0)
                {
                    decimal currentEquity = balance;
                    foreach (var pos in activePositions.Values)
                    {
                        var tick = currentTicks.FirstOrDefault(t => t.Pair == pos.Pair);
                        if (tick != null)
                        {
                            currentEquity += pos.Amount * tick.LastPrice;
                        }
                        else
                        {
                            currentEquity += pos.Cost;
                        }
                    }
                    equityCurveList.Add(new EquityPoint { Timestamp = timestamp, Balance = currentEquity });
                }
            }

            // Закрытие всех оставшихся позиций в конце бэктеста
            DateTime endSimTime = startSimTime.AddSeconds(snapshotCount * 10);
            foreach (var pos in activePositions.Values)
            {
                decimal lastPrice = pos.EntryPrice;
                var lastTicksList = tickers.LastOrDefault();
                if (lastTicksList != null)
                {
                    var tick = lastTicksList.FirstOrDefault(t => t.Pair == pos.Pair);
                    if (tick != null)
                    {
                        lastPrice = tick.LastPrice;
                    }
                }

                decimal sellCost = pos.Amount * lastPrice * (1m - 0.00075m);
                decimal profitUsdt = sellCost - pos.Cost;
                decimal profitPct = (sellCost - pos.Cost) / pos.Cost * 100m;
                balance += sellCost;

                tradesList.Add(new TradeRecord
                {
                    Pair = pos.Pair,
                    Type = "SELL (End of Backtest Close)",
                    Price = lastPrice,
                    Amount = pos.Amount,
                    Cost = sellCost,
                    ProfitPct = profitPct,
                    ProfitUsdt = profitUsdt,
                    Timestamp = endSimTime,
                    ResultingBalance = balance
                });
            }
            activePositions.Clear();
            equityCurveList.Add(new EquityPoint { Timestamp = endSimTime, Balance = balance });

            // Финальное обновление просадки
            if (balance > peakBalance) peakBalance = balance;
            decimal finalDd = (peakBalance - balance) / peakBalance * 100m;
            if (finalDd > maxDrawdownPct) maxDrawdownPct = finalDd;

            stopwatch.Stop();
            double processingTimeMs = stopwatch.Elapsed.TotalMilliseconds;

            // Расчет расширенных метрик торговли
            int totalTrades = tradesList.Count;
            var sellTrades = tradesList.Where(t => t.Type.StartsWith("SELL")).ToList();
            int winningTrades = sellTrades.Count(t => t.ProfitUsdt > 0);
            int losingTrades = sellTrades.Count(t => t.ProfitUsdt <= 0);
            decimal winRatePct = sellTrades.Count > 0 ? (decimal)winningTrades / sellTrades.Count * 100m : 0m;

            decimal totalGains = sellTrades.Where(t => t.ProfitUsdt > 0).Sum(t => t.ProfitUsdt);
            decimal totalLosses = Math.Abs(sellTrades.Where(t => t.ProfitUsdt < 0).Sum(t => t.ProfitUsdt));
            decimal profitFactor = totalLosses > 0m ? totalGains / totalLosses : totalGains;
            decimal totalProfitPct = (balance - initialBalance) / initialBalance * 100m;

            // Сбор метрик памяти
            long endAllocatedBytes = GC.GetAllocatedBytesForCurrentThread();
            int endGen0 = GC.CollectionCount(0);
            int endGen1 = GC.CollectionCount(1);
            int endGen2 = GC.CollectionCount(2);

            // Имитируем реальное время воспроизведения бэктеста.
            // Предположим, 1 снимок берется каждые 10 секунд.
            double simulatedIntervalSeconds = 10;
            double simulatedTotalSeconds = snapshotCount * simulatedIntervalSeconds;
            double actualTotalSeconds = processingTimeMs / 1000.0;
            double speedupFactor = actualTotalSeconds > 0 ? (simulatedTotalSeconds / actualTotalSeconds) : 0;

            return new BenchmarkResult
            {
                SnapshotCount = snapshotCount,
                SimulatedMonths = months,

                SerializationTimeMs = serializationTimeMs,
                SerializationThroughput = (snapshotCount / (serializationTimeMs / 1000.0)),
                TotalSizeMb = totalSizeMb,

                DeserializationTimeMs = deserializationTimeMs,
                DeserializationThroughput = (snapshotCount / (deserializationTimeMs / 1000.0)),
                DeserializationSpeedMbSec = (totalSizeMb / (deserializationTimeMs / 1000.0)),

                ProcessingTimeMs = processingTimeMs,
                ProcessingThroughput = (processedSnapshots / (processingTimeMs / 1000.0)),
                SpeedupFactor = speedupFactor,

                BytesAllocated = endAllocatedBytes - startAllocatedBytes,
                GcCollectionsGen0 = endGen0 - startGen0,
                GcCollectionsGen1 = endGen1 - startGen1,
                GcCollectionsGen2 = endGen2 - startGen2,

                // Торговые результаты
                InitialBalance = initialBalance,
                FinalBalance = balance,
                TotalProfitPct = totalProfitPct,
                TotalTrades = totalTrades,
                WinningTrades = winningTrades,
                LosingTrades = losingTrades,
                WinRatePct = winRatePct,
                MaxDrawdownPct = maxDrawdownPct,
                ProfitFactor = profitFactor,
                Trades = tradesList,
                EquityCurve = equityCurveList
            };
        }
    }
}
